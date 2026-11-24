using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace HundredPlayerWorld
{
    /// <summary>Persistent bans keyed by platform user id. File format: one "id|name" per line.</summary>
    internal static class BanList
    {
        private static readonly string FilePath = Path.Combine(Paths.ConfigPath, "HundredPlayerWorld.bans.txt");
        private static Dictionary<string, string> _bans;

        public static IReadOnlyDictionary<string, string> All { get { Load(); return _bans; } }
        public static bool Contains(string id) { Load(); return !string.IsNullOrEmpty(id) && _bans.ContainsKey(id); }

        public static void Add(string id, string name) { Load(); if (string.IsNullOrEmpty(id)) return; _bans[id] = name; Save(); }
        public static void Remove(string id) { Load(); if (_bans.Remove(id)) Save(); }

        private static void Load()
        {
            if (_bans != null) return;
            _bans = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                    var i = line.IndexOf('|');
                    if (i <= 0) continue;
                    _bans[line.Substring(0, i)] = line.Substring(i + 1);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Could not read ban list: " + e.Message); }
        }

        private static void Save()
        {
            try { File.WriteAllLines(FilePath, new[] { "# Hundred Player World bans: platform-user-id|name" }.Concat(_bans.Select(b => b.Key + "|" + b.Value))); }
            catch (Exception e) { Plugin.Log.LogWarning("Could not save ban list: " + e.Message); }
        }
    }
}
