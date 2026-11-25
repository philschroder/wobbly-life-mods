using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VehicleSpawner
{
    internal class SpawnerPanel : MonoBehaviour
    {
        private bool _open;
        private Rect _win = new Rect(40, 60, 480, 560);
        private Vector2 _scroll;
        private string _filter = "";
        private float _scale = 3f;
        private List<GameObject> _templates = new List<GameObject>();
        private readonly List<ShipController> _ships = new List<ShipController>();
        private string _status = "";

        private void Update()
        {
            if (Input.GetKeyDown(Plugin.PanelKey.Value))
            {
                _open = !_open;
                if (_open) { _templates = Spawner.Templates(_filter); _scale = Plugin.ShipScale.Value; }
            }
            _ships.RemoveAll(s => s == null);
        }

        private void OnGUI()
        {
            if (!_open) return;
            try { _win = GUILayout.Window(0x5A17, _win, Draw, "Vehicle Spawner"); }
            catch (System.Exception e) { Plugin.Log.LogWarning("Panel error: " + e.Message); }
        }

        private void Draw(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", GUILayout.Width(40));
            var f = GUILayout.TextField(_filter);
            if (f != _filter) { _filter = f; _templates = Spawner.Templates(_filter); }
            if (GUILayout.Button("Refresh", GUILayout.Width(70))) _templates = Spawner.Templates(_filter);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Scale {_scale:0.0}x", GUILayout.Width(80));
            _scale = GUILayout.HorizontalSlider(_scale, 0.5f, 20f);
            GUILayout.EndHorizontal();

            if (GUILayout.Button($"Spawn SPACESHIP preset (template '{Plugin.ShipTemplate.Value}', flies)"))
            {
                var t = Spawner.Templates(Plugin.ShipTemplate.Value).FirstOrDefault();
                if (t == null) _status = $"No template matching '{Plugin.ShipTemplate.Value}'. Pick one below or change ShipTemplate in the config.";
                else SpawnShip(t, true);
            }

            GUILayout.Label($"Templates found: {_templates.Count}  (PREFAB = real asset, SCENE = existing object copied)");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(240));
            foreach (var t in _templates)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label((t.scene.IsValid() ? "SCENE  " : "PREFAB ") + t.name, GUILayout.Width(300));
                if (GUILayout.Button("Spawn")) SpawnShip(t, false);
                if (GUILayout.Button("Spawn+Fly")) SpawnShip(t, true);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Label($"Spawned ({_ships.Count})");
            foreach (var s in _ships.ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(s.name, GUILayout.Width(240));
                if (GUILayout.Button(s.Active ? "Stop flying" : "Fly"))
                {
                    bool on = !s.Active;
                    foreach (var o in _ships) o.SetActive(false);
                    s.SetActive(on);
                }
                if (GUILayout.Button("Despawn")) { Destroy(s.gameObject); }
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Fly: arrows = move, RightShift/RightCtrl = up/down, ,/. = turn");
            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private void SpawnShip(GameObject template, bool fly)
        {
            var go = Spawner.Spawn(template, _scale, out var how);
            var ctl = go.GetComponent<ShipController>() ?? go.AddComponent<ShipController>();
            _ships.Add(ctl);
            if (fly) { foreach (var o in _ships) o.SetActive(false); ctl.SetActive(true); }
            _status = $"Spawned '{go.name}' ({how}){(how.StartsWith("local") ? " - guests will NOT see it" : "")}.";
            Plugin.Log.LogInfo(_status);
        }
    }
}
