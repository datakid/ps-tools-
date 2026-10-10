using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PSTools
{
    static class Settings
    {
        static string FilePath { get { return Path.Combine(Journal.Dir, "settings.txt"); } }

        public static string Theme = "auto";
        static HashSet<string> enabled;
        static HashSet<string> favs;
        static HashSet<string> pins = new HashSet<string>();
        static bool loaded;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            bool any = false;
            var en = new HashSet<string>();
            var fv = new HashSet<string>();
            try
            {
                if (File.Exists(FilePath))
                    foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        int eq = line.IndexOf('=');
                        if (eq < 1) continue;
                        string k = line.Substring(0, eq), v = line.Substring(eq + 1).Trim();
                        if (k == "theme") Theme = v;
                        else if (k == "enabled") { en.Add(v); any = true; }
                        else if (k == "fav") fv.Add(v);
                        else if (k == "pin") { pins.Add(v); any = true; }
                        else if (k == "configured") any = true;
                    }
            }
            catch { }
            if (any) { enabled = en; favs = fv; }
        }

        static void Defaults()
        {
            enabled = new HashSet<string>(Actions.All.Where(a => a.Targets != Target.None && a.Tier != Tier.Elevated && Modules.Find(a.Group) != null && Modules.Find(a.Group).DefaultOn).Select(a => a.Id));
            favs = new HashSet<string>(Actions.All.Where(a => a.DefaultFav).Select(a => a.Id));
        }

        public static void EnsureLoaded()
        {
            Load();
            if (enabled == null) Defaults();
        }

        public static bool IsEnabled(string id)
        {
            EnsureLoaded();
            if (id.StartsWith("lib-")) return pins.Contains(id.Substring(4));
            return enabled.Contains(id);
        }

        public static HashSet<string> Pins { get { Load(); return pins; } }

        public static void TogglePin(string cmdId)
        {
            EnsureLoaded();
            if (!pins.Remove(cmdId)) pins.Add(cmdId);
            Save();
        }
        public static bool IsFav(string id) { EnsureLoaded(); return favs.Contains(id); }

        public static void Set(IEnumerable<string> enabledIds, IEnumerable<string> favIds)
        {
            enabled = new HashSet<string>(enabledIds);
            favs = new HashSet<string>(favIds.Where(f => enabled.Contains(f)));
            loaded = true;
            Save();
        }

        public static void ResetDefaults()
        {
            loaded = true;
            Defaults();
            Save();
        }

        public static void SetTheme(string t) { EnsureLoaded(); Theme = t; Save(); }

        public static void Save()
        {
            EnsureLoaded();
            Directory.CreateDirectory(Journal.Dir);
            var sb = new StringBuilder();
            sb.AppendLine("configured=1");
            sb.AppendLine("theme=" + Theme);
            foreach (var e in enabled.OrderBy(x => x)) sb.AppendLine("enabled=" + e);
            foreach (var f in favs.OrderBy(x => x)) sb.AppendLine("fav=" + f);
            foreach (var p in pins.OrderBy(x => x)) sb.AppendLine("pin=" + p);
            File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
        }

        public static void PreloadTheme()
        {
            Load();
        }
    }
}
