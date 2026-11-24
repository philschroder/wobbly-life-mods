using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HundredPlayerWorld
{
    /// <summary>
    /// Raises Wobbly Life's online player cap (default 4) when YOU host. Host-only: guests do not need the mod.
    ///
    /// Approach (learned from how the game's network stack is structured, not copied from another mod):
    ///   * The game uses HawkNetworking.HawkNetworkManager with one subclass per platform
    ///     (EOSNetworkManager, GameCoreNetworkManager, and presumably a Steam one).
    ///   * Each subclass has a HostServer(...) that takes the max-player count.
    ///   * We patch every HostServer we can find (by reflection) and raise any int parameter named like "max*".
    ///   * We also post-fix GetMaxPlayerCount() so the game's own capacity checks agree with the new value.
    ///
    /// Everything is resolved by name at runtime, so no game DLLs are needed to build. The cost is that
    /// names are UNVERIFIED for the Steam build until run once with DiagnosticsMode = true (see README).
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "local.wobblylife.hundredplayerworld";
        public const string Name = "Hundred Player World";
        public const string Version = "0.2.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<int> MaxPlayers;
        internal static ConfigEntry<bool> Diagnostics;
        internal static ConfigEntry<string> ArgNameHint;
        internal static ConfigEntry<UnityEngine.KeyCode> ToggleKey;
        internal static ConfigEntry<bool> ShowHint;

        private const string BaseTypeName = "HawkNetworking.HawkNetworkManager";

        private void Awake()
        {
            Log = Logger;
            MaxPlayers = Config.Bind("General", "MaxPlayers", 100,
                new ConfigDescription("Online lobby size used when you host. Only ever raises the game's own value.",
                    new AcceptableValueRange<int>(1, 250)));  // Steam lobbies top out at 250 members
            Diagnostics = Config.Bind("General", "DiagnosticsMode", true,
                "Log every network-manager type, HostServer signature and player-count call found. Leave ON until patches are confirmed.");
            ArgNameHint = Config.Bind("General", "MaxArgNameContains", "max",
                "HostServer int parameters whose name contains this (case-insensitive) are treated as the player cap.");

            ToggleKey = Config.Bind("Panel", "ToggleKey", UnityEngine.KeyCode.F8, "Key that opens/closes the host panel.");
            ShowHint = Config.Bind("Panel", "ShowHint", true, "Show a small 'Press F8' hint in the top-left corner.");

            Log.LogInfo($"{Name} {Version} loaded. MaxPlayers={MaxPlayers.Value}");

            gameObject.AddComponent<PanelUI>();

            try { PatchAll(); }
            catch (Exception e) { Log.LogError("Patching failed: " + e); }
        }

        private void PatchAll()
        {
            var harmony = new Harmony(Guid);
            var baseType = AccessTools.TypeByName(BaseTypeName);
            if (baseType == null)
            {
                Log.LogError($"Could not find {BaseTypeName}. Is this a different game version? Run tools/inspect_assembly.py on Assembly-CSharp.dll and update BaseTypeName.");
                return;
            }

            var managers = AccessTools.AllTypes()
                .Where(t => !t.IsAbstract && (t == baseType || baseType.IsAssignableFrom(t)))
                .ToList();
            if (Diagnostics.Value)
                Log.LogInfo("Network manager types: " + string.Join(", ", managers.Select(t => t.FullName)));

            int patched = 0;
            foreach (var type in AccessTools.AllTypes().Where(t => t == baseType || baseType.IsAssignableFrom(t)))
            {
                foreach (var m in DeclaredMethods(type, "HostServer"))
                {
                    if (Diagnostics.Value) Log.LogInfo("HostServer candidate: " + Describe(m));
                    harmony.Patch(m, prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.HostServerPrefix)));
                    patched++;
                }
                foreach (var m in DeclaredMethods(type, "GetMaxPlayerCount").Where(x => x.ReturnType == typeof(int)))
                {
                    if (Diagnostics.Value) Log.LogInfo("GetMaxPlayerCount candidate: " + Describe(m));
                    harmony.Patch(m, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.MaxCountPostfix)));
                    patched++;
                }
            }

            if (patched == 0)
                Log.LogWarning("No HostServer/GetMaxPlayerCount methods found. Names differ in this build - see README 'Finding the patch targets'.");
            else
                Log.LogInfo($"Applied {patched} patch(es).");
        }

        private static IEnumerable<MethodInfo> DeclaredMethods(Type t, string name) =>
            t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
             .Where(m => m.Name == name && !m.IsAbstract && m.GetMethodBody() != null);

        internal static string Describe(MethodBase m) =>
            $"{m.DeclaringType?.FullName}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})";
    }

    internal static class Patches
    {
        private static readonly HashSet<MethodBase> Seen = new HashSet<MethodBase>();

        // __args lets us rewrite parameters without knowing the exact signature at compile time.
        public static void HostServerPrefix(MethodBase __originalMethod, object[] __args)
        {
            var target = Plugin.MaxPlayers.Value;
            var hint = Plugin.ArgNameHint.Value ?? "max";
            var ps = __originalMethod.GetParameters();
            bool changed = false;

            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType != typeof(int)) continue;
                if (ps[i].Name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var old = (int)__args[i];
                if (old >= target) continue; // never lower the game's own value
                __args[i] = target;
                changed = true;
                Plugin.Log.LogInfo($"{Plugin.Describe(__originalMethod)}: {ps[i].Name} {old} -> {target}");
            }

            if (!changed && Seen.Add(__originalMethod))
                Plugin.Log.LogWarning($"HostServer ran but no int parameter matched '{hint}' (or already >= cap): {Plugin.Describe(__originalMethod)}. " +
                                      "Adjust MaxArgNameContains in the config.");
        }

        public static void MaxCountPostfix(ref int __result)
        {
            var target = Plugin.MaxPlayers.Value;
            if (__result < target) __result = target;
        }
    }
}
