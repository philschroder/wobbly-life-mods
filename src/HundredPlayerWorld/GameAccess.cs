using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HundredPlayerWorld
{
    internal class PlayerEntry
    {
        public object Controller;
        public string Name;
        public string Id;
        public bool IsLocal;
    }

    /// <summary>
    /// Reflection-only access to Wobbly Life's classes (names taken from the Game Pass build; UNVERIFIED on Steam).
    /// Every call is defensive: on failure it logs once and returns a safe default rather than throwing.
    /// </summary>
    internal static class Game
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                         BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        private static readonly HashSet<string> Logged = new HashSet<string>();

        private static Type _base, _controller, _wobbly, _instance;
        private static Type BaseType => _base ?? (_base = AccessTools.TypeByName("HawkNetworking.HawkNetworkManager"));
        private static Type ControllerType => _controller ?? (_controller = AccessTools.TypeByName("PlayerController"));
        private static Type WobblyType => _wobbly ?? (_wobbly = AccessTools.TypeByName("WobblyNetworkManager"));
        private static Type InstanceType => _instance ?? (_instance = AccessTools.TypeByName("GameInstance"));

        public static void LogOnce(string key, string msg)
        {
            if (Logged.Add(key)) Plugin.Log.LogWarning(msg);
        }

        // ---- reflection helpers -------------------------------------------------------------

        /// Reads a property, field, or zero-arg method named `name`. Works on static (target == null) or instance.
        private static bool TryGet(object target, Type type, string name, out object value)
        {
            value = null;
            type = type ?? target?.GetType();
            if (type == null) return false;
            try
            {
                var p = type.GetProperty(name, All);
                if (p != null && p.GetIndexParameters().Length == 0) { value = p.GetValue(p.GetGetMethod(true).IsStatic ? null : target, null); return true; }
                var f = type.GetField(name, All);
                if (f != null) { value = f.GetValue(f.IsStatic ? null : target); return true; }
                var m = type.GetMethods(All).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == 0);
                if (m != null) { value = m.Invoke(m.IsStatic ? null : target, null); return true; }
            }
            catch (Exception e) { LogOnce("get:" + type.Name + name, $"Reading {type.Name}.{name} failed: {e.InnerException?.Message ?? e.Message}"); }
            return false;
        }

        private static object Singleton(Type t, params string[] names)
        {
            if (t == null) return null;
            foreach (var n in names)
                if (TryGet(null, t, n, out var v) && v != null) return v;
            try
            {
                var o = UnityEngine.Object.FindObjectOfType(t);
                if (o != null) return o;
            }
            catch { }
            return null;
        }

        private static object Manager => Singleton(BaseType, "DefaultInstance", "Instance");

        // ---- lobby info ---------------------------------------------------------------------

        public static bool IsHost()
        {
            var m = Manager;
            return m != null && TryGet(m, null, "IsServer", out var v) && v is bool b && b;
        }

        public static int CurrentCount() => IntFrom(Manager, "GetCurrentPlayerCount");
        public static int MaxCount() => IntFrom(Manager, "GetMaxPlayerCount");

        private static int IntFrom(object m, string name) =>
            m != null && TryGet(m, null, name, out var v) && v is int i ? i : -1;

        // ---- players ------------------------------------------------------------------------

        public static List<PlayerEntry> Players()
        {
            var list = new List<PlayerEntry>();
            if (ControllerType == null) { LogOnce("noctl", "PlayerController type not found - player list unavailable."); return list; }

            object local = LocalController();
            UnityEngine.Object[] all;
            try { all = UnityEngine.Object.FindObjectsOfType(ControllerType); }
            catch (Exception e) { LogOnce("find", "FindObjectsOfType(PlayerController) failed: " + e.Message); return list; }

            foreach (var c in all)
            {
                TryGet(c, ControllerType, "GetPlayerName", out var name);
                TryGet(c, ControllerType, "PlatformUserId", out var id);
                list.Add(new PlayerEntry
                {
                    Controller = c,
                    Name = name?.ToString() ?? "(unknown)",
                    Id = id?.ToString() ?? "",
                    IsLocal = local != null && ReferenceEquals(local, c)
                });
            }
            return list;
        }

        private static object LocalController()
        {
            var gi = Singleton(InstanceType, "Instance");
            if (gi == null) return null;
            return TryGet(gi, null, "GetFirstLocalPlayerController", out var v) ? v : null;
        }

        // ---- kick ---------------------------------------------------------------------------

        public static bool Kick(PlayerEntry p)
        {
            if (p.IsLocal) { Plugin.Log.LogWarning("Refusing to kick the local player."); return false; }
            if (!IsHost()) { Plugin.Log.LogWarning("Kick ignored: you are not hosting."); return false; }

            var wm = Singleton(WobblyType, "Instance");
            if (wm == null) { LogOnce("nowm", "WobblyNetworkManager instance not found - cannot kick."); return false; }

            var candidates = wm.GetType().GetMethods(All).Where(m => m.Name == "KickPlayer").ToList();
            if (candidates.Count == 0) { LogOnce("nokick", "No KickPlayer method found on " + wm.GetType().FullName); return false; }

            foreach (var m in candidates)
            {
                var ps = m.GetParameters();
                if (ps.Length != 1) continue;
                var pt = ps[0].ParameterType;
                object arg = null;
                if (pt.IsInstanceOfType(p.Controller)) arg = p.Controller;
                else if (pt == typeof(string)) arg = p.Id;
                else if (TryGet(p.Controller, ControllerType, "connection", out var c1) && pt.IsInstanceOfType(c1)) arg = c1;
                else if (TryGet(p.Controller, ControllerType, "Connection", out var c2) && pt.IsInstanceOfType(c2)) arg = c2;
                if (arg == null) continue;
                try
                {
                    m.Invoke(m.IsStatic ? null : wm, new[] { arg });
                    Plugin.Log.LogInfo($"Kicked '{p.Name}' via {Plugin.Describe(m)}");
                    return true;
                }
                catch (Exception e) { Plugin.Log.LogWarning($"KickPlayer threw: {e.InnerException?.Message ?? e.Message}"); }
            }

            LogOnce("kickshape", "KickPlayer exists but no overload matched a PlayerController/id/connection. Signatures: " +
                    string.Join(" | ", candidates.Select(Plugin.Describe)));
            return false;
        }
    }
}
