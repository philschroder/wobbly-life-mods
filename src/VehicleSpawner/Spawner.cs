using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace VehicleSpawner
{
    internal static class Spawner
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                         BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        private static readonly Regex VehicleRx = new Regex("vehicle|aircraft|plane|heli|rocket|ufo|ship|boat|car(?!d)|truck|bus|tractor|jet|bike",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static bool _loggedNet;

        public static List<GameObject> Templates(string filter)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(g => g != null && VehicleRx.IsMatch(g.name) && g.GetComponent<Rigidbody>() != null)
                .Where(g => string.IsNullOrEmpty(filter) || g.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(g => g.scene.IsValid() ? 1 : 0).ThenBy(g => g.name)   // real prefabs first
                .Take(80).ToList();
        }

        public static Vector3 SpawnPoint()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position + cam.transform.forward * 25f + Vector3.up * 3f : new Vector3(0, 10, 0);
        }

        /// Spawns a copy of `template`. Tries the game's own networked spawn first (so guests can see it),
        /// else falls back to a plain local Instantiate (host-only, will NOT be visible to guests).
        public static GameObject Spawn(GameObject template, float scale, out string how)
        {
            var pos = SpawnPoint();
            var rot = Quaternion.identity;
            GameObject go = null;
            how = "local (host-only)";

            if (Plugin.TryNetworkSpawn.Value && !template.scene.IsValid())
            {
                go = TryNetworkSpawn(template, pos, rot);
                if (go != null) how = "networked";
            }
            if (go == null) go = UnityEngine.Object.Instantiate(template, pos, rot);

            go.SetActive(true);
            go.transform.localScale = template.transform.localScale * scale;
            return go;
        }

        // Looks for a non-void spawn method taking (GameObject-ish prefab, Vector3, Quaternion) on the game's managers.
        private static GameObject TryNetworkSpawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            foreach (var typeName in new[] { "WobblyNetworkManager", "HawkNetworking.HawkNetworkManager" })
            {
                var t = AccessTools.TypeByName(typeName);
                if (t == null) continue;
                object inst = null;
                foreach (var n in new[] { "Instance", "DefaultInstance" })
                {
                    var p = t.GetProperty(n, All);
                    if (p != null) { try { inst = p.GetValue(null, null); } catch { } }
                    if (inst != null) break;
                }
                if (inst == null) continue;

                var cands = inst.GetType().GetMethods(All).Where(m => m.Name.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (!_loggedNet)
                {
                    Plugin.Log.LogInfo($"Spawn-like methods on {inst.GetType().Name}: " +
                        string.Join(" | ", cands.Select(m => $"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")));
                }

                foreach (var m in cands.Where(m => m.ReturnType != typeof(void)))
                {
                    var ps = m.GetParameters();
                    if (ps.Length < 3 || ps.Length > 3) continue;
                    object first = ps[0].ParameterType.IsAssignableFrom(typeof(GameObject)) ? prefab
                                 : typeof(Component).IsAssignableFrom(ps[0].ParameterType) ? (object)prefab.GetComponent(ps[0].ParameterType) : null;
                    if (first == null) continue;
                    var args = new object[3]; args[0] = first;
                    bool ok = true;
                    for (int i = 1; i < 3; i++)
                    {
                        if (ps[i].ParameterType == typeof(Vector3)) args[i] = pos;
                        else if (ps[i].ParameterType == typeof(Quaternion)) args[i] = rot;
                        else ok = false;
                    }
                    if (!ok) continue;
                    try
                    {
                        var r = m.Invoke(m.IsStatic ? null : inst, args);
                        var go = r as GameObject ?? (r as Component)?.gameObject;
                        if (go != null) { Plugin.Log.LogInfo("Network-spawned via " + m.Name); return go; }
                    }
                    catch (Exception e) { Plugin.Log.LogWarning($"{m.Name} threw: {e.InnerException?.Message ?? e.Message}"); }
                }
            }
            if (!_loggedNet) Plugin.Log.LogWarning("No usable networked spawn method matched; using local spawn. Send the 'Spawn-like methods' log line so we can wire it.");
            _loggedNet = true;
            return null;
        }
    }
}
