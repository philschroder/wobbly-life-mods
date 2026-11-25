using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace VehicleSpawner
{
    /// <summary>
    /// Step 1 of the vehicle spawner: DISCOVERY ONLY. Press the key (default F9) in game and it writes
    /// BepInEx/VehicleSpawner.discovery.txt listing game types that look vehicle/spawn related and
    /// vehicle-looking GameObjects/prefabs currently loaded. We use that to decide how to spawn a
    /// networked spaceship. Nothing is spawned or modified yet.
    /// </summary>
    [BepInPlugin("local.wobblylife.vehiclespawner", "Vehicle Spawner", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static readonly Regex TypeRx = new Regex("vehicle|spawn|aircraft|plane|heli|rocket|ufo|ship|boat|car(?!d)|prefab|seat|pilot|drive",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ObjRx = new Regex("vehicle|aircraft|plane|heli|rocket|ufo|ship|boat|car(?!d)|truck|bus|tractor|jet|bike",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private ConfigEntry<KeyCode> _key;

        private void Awake()
        {
            _key = Config.Bind("General", "DumpKey", KeyCode.F9, "Press in game to write the discovery file.");
            Logger.LogInfo($"Vehicle Spawner 0.1.0 loaded. Press {_key.Value} in game to dump discovery info.");
        }

        private void Update()
        {
            try { if (Input.GetKeyDown(_key.Value)) Dump(); }
            catch (Exception e) { Logger.LogError("Dump failed: " + e); }
        }

        private void Dump()
        {
            var sb = new StringBuilder();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            sb.AppendLine("== Game types (Assembly-CSharp) matching vehicle/spawn keywords ==");
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (asm == null) sb.AppendLine("Assembly-CSharp not found (IL2CPP build?)");
            else
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types.Where(t => TypeRx.IsMatch(t.Name)).OrderBy(t => t.FullName))
                {
                    sb.AppendLine($"{t.FullName} : {t.BaseType?.Name}");
                    foreach (var f in t.GetFields(F)) sb.AppendLine($"    field {f.FieldType.Name} {f.Name}");
                    foreach (var m in t.GetMethods(F).Where(m => !m.IsSpecialName))
                        sb.AppendLine($"    {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
                }
            }

            sb.AppendLine().AppendLine("== GameObjects (scene + loaded prefabs) with vehicle-like names ==");
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => ObjRx.IsMatch(g.name)).OrderBy(g => g.name).Take(400))
            {
                var comps = go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name);
                var rb = go.GetComponent<Rigidbody>();
                sb.AppendLine($"{go.name}  scene={(go.scene.IsValid() ? go.scene.name : "PREFAB/asset")}  rb={(rb ? rb.mass.ToString("0") + "kg" : "-")}  [{string.Join(", ", comps)}]");
            }

            var path = Path.Combine(Paths.BepInExRootPath, "VehicleSpawner.discovery.txt");
            File.WriteAllText(path, sb.ToString());
            Logger.LogInfo("Wrote " + path);
        }
    }
}
