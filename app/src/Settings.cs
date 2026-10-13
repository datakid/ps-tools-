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
        static bool configured;
        static string win;
        static List<string> recent = new List<string>();

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
                        else if (k == "win") win = v;
                        else if (k == "recent" && recent.Count < 8) recent.Add(v);
                    }
            }
            catch { }
            if (any) { enabled = en; favs = fv; configured = true; }
        }

        static void Defaults()
        {
            enabled = Presets.Ids("Everyday");
            favs = new HashSet<string>(Actions.All.Where(a => a.DefaultFav && enabled.Contains(a.Id)).Select(a => a.Id));
        }

        public static bool Configured { get { Load(); return configured; } }

        public static string CurrentPreset { get { EnsureLoaded(); return Presets.Match(enabled); } }

        public static void ApplyPreset(string name)
        {
            EnsureLoaded();
            enabled = Presets.Ids(name);
            favs = new HashSet<string>(Actions.All.Where(a => a.DefaultFav && enabled.Contains(a.Id)).Select(a => a.Id));
            loaded = true;
            configured = true;
            Save();
        }

        public static void MarkConfigured()
        {
            EnsureLoaded();
            configured = true;
            Save();
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
            if (id.StartsWith("recipe-")) return RecipeData.InMenu(id);
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
            if (win != null) sb.AppendLine("win=" + win);
            foreach (var r in recent) sb.AppendLine("recent=" + r);
            File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
        }

        public static List<string> Recent { get { Load(); return recent; } }

        public static void AddRecent(string id)
        {
            Load();
            recent.Remove(id);
            recent.Insert(0, id);
            if (recent.Count > 8) recent.RemoveRange(8, recent.Count - 8);
            if (!configured) return;
            try { Save(); } catch { }
        }

        public static bool TryGetWindow(out System.Drawing.Rectangle bounds, out bool maximized)
        {
            Load();
            bounds = System.Drawing.Rectangle.Empty;
            maximized = false;
            if (win == null) return false;
            string[] p = win.Split(',');
            int x, y, w, h;
            if (p.Length < 5 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out w) || !int.TryParse(p[3], out h)) return false;
            bounds = new System.Drawing.Rectangle(x, y, w, h);
            maximized = p[4] == "1";
            return true;
        }

        public static void SetWindow(System.Drawing.Rectangle bounds, bool maximized)
        {
            if (!Configured) return;
            string v = bounds.X + "," + bounds.Y + "," + bounds.Width + "," + bounds.Height + "," + (maximized ? "1" : "0");
            if (v == win) return;
            win = v;
            try { Save(); } catch { }
        }

        public static void PreloadTheme()
        {
            Load();
        }
    }
}
