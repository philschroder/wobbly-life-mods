using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HundredPlayerWorld
{
    /// <summary>In-game host panel (IMGUI). Toggle with the configured key (default F8).</summary>
    internal class PanelUI : MonoBehaviour
    {
        private bool _open;
        private Rect _window = new Rect(40, 60, 460, 520);
        private Vector2 _scroll;
        private List<PlayerEntry> _players = new List<PlayerEntry>();
        private float _nextRefresh, _nextEnforce;
        private string _confirmId;                      // id awaiting a 2nd click (kick / ban)
        private string _confirmKind;
        private readonly Dictionary<string, float> _lastKick = new Dictionary<string, float>();

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(Plugin.ToggleKey.Value)) _open = !_open;

                if (Time.unscaledTime >= _nextRefresh && (_open || BanList.All.Count > 0))
                {
                    _nextRefresh = Time.unscaledTime + 1f;
                    _players = Game.IsHost() ? Game.Players() : new List<PlayerEntry>();
                }

                // Keep banned players out: kick anyone on the list who is (re)connected. Throttled per id.
                if (Time.unscaledTime >= _nextEnforce)
                {
                    _nextEnforce = Time.unscaledTime + 2f;
                    foreach (var p in _players.Where(p => !p.IsLocal && BanList.Contains(p.Id)))
                    {
                        if (_lastKick.TryGetValue(p.Id, out var t) && Time.unscaledTime - t < 10f) continue;
                        _lastKick[p.Id] = Time.unscaledTime;
                        Plugin.Log.LogInfo($"[Ban] Banned player '{p.Name}' present - kicking.");
                        Game.Kick(p);
                    }
                }
            }
            catch (System.Exception e) { Game.LogOnce("ui-update", "Panel update error: " + e.Message); }
        }

        private void OnGUI()
        {
            try
            {
                if (!_open)
                {
                    if (Plugin.ShowHint.Value)
                        GUI.Label(new Rect(8, 4, 320, 22), $"Press {Plugin.ToggleKey.Value} for Hundred Player World");
                    return;
                }
                _window = GUILayout.Window(0x48B1, _window, Draw, "Hundred Player World");
            }
            catch (System.Exception e) { Game.LogOnce("ui-gui", "Panel draw error: " + e.Message); }
        }

        private void Draw(int id)
        {
            bool host = Game.IsHost();
            int cur = Game.CurrentCount(), max = Game.MaxCount();

            GUILayout.Label(host
                ? $"Hosting  -  players: {(cur >= 0 ? cur.ToString() : "?")} / {(max >= 0 ? max.ToString() : "?")}   (target cap {Plugin.MaxPlayers.Value})"
                : "Not hosting. Start or load an online world to manage players.");

            GUILayout.Space(4);
            GUILayout.Label($"Players ({_players.Count})");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(250));
            foreach (var p in _players)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(p.IsLocal ? p.Name + "  (you)" : p.Name, GUILayout.Width(250));
                if (!p.IsLocal)
                {
                    if (Button("Kick", "kick", p)) Game.Kick(p);
                    if (Button("Ban", "ban", p)) { BanList.Add(p.Id, p.Name); Game.Kick(p); }
                }
                GUILayout.EndHorizontal();
            }
            if (_players.Count == 0) GUILayout.Label(host ? "No players found." : "");
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label($"Banned ({BanList.All.Count})");
            foreach (var b in BanList.All.ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{b.Value}  [{b.Key}]", GUILayout.Width(340));
                if (GUILayout.Button("Unban")) BanList.Remove(b.Key);
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Click Kick/Ban twice to confirm. Bans persist in the config folder.");
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        /// Two-click confirmation so a mis-click doesn't remove someone.
        private bool Button(string label, string kind, PlayerEntry p)
        {
            bool armed = _confirmId == p.Id && _confirmKind == kind && !string.IsNullOrEmpty(p.Id);
            if (!GUILayout.Button(armed ? "Sure?" : label, GUILayout.Width(70))) return false;
            if (armed) { _confirmId = null; _confirmKind = null; return true; }
            _confirmId = p.Id; _confirmKind = kind;
            return false;
        }
    }
}
