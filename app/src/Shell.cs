using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace PSTools
{
    static class Shell
    {
        const string Base = @"Software\Classes\";

        static readonly string[] Roots =
        {
            Base + @"Directory\shell\PSTools",
            Base + @"Directory\Background\shell\PSTools",
            Base + @"Drive\shell\PSTools",
            Base + @"*\shell\PSTools"
        };

        static readonly Target[] Kinds = { Target.Folder, Target.Background, Target.Drive, Target.File };

        public static bool IsInstalled()
        {
            foreach (var r in Roots)
                using (var k = Registry.CurrentUser.OpenSubKey(r)) if (k != null) return true;
            return false;
        }

        public static string RegisteredExe()
        {
            foreach (var r in Roots)
                using (var k = Registry.CurrentUser.OpenSubKey(r))
                {
                    if (k == null) continue;
                    string icon = k.GetValue("Icon") as string;
                    if (string.IsNullOrEmpty(icon)) continue;
                    icon = icon.Trim();
                    if (icon.StartsWith("\"")) { int end = icon.IndexOf('"', 1); return end > 1 ? icon.Substring(1, end - 1) : null; }
                    int comma = icon.LastIndexOf(',');
                    return comma > 0 ? icon.Substring(0, comma) : icon;
                }
            return null;
        }

        public static bool PointsElsewhere()
        {
            string reg = RegisteredExe();
            return reg != null && !string.Equals(reg, System.Windows.Forms.Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }

        public static bool HealIfMoved()
        {
            if (!IsInstalled() || !PointsElsewhere()) return false;
            try { Install(); return true; }
            catch { return false; }
        }

        public static void Install()
        {
            try { Write(); }
            catch { RemoveMenu(); throw; }
        }

        class RegValue
        {
            public string Name;
            public object Value;
        }

        class RegEntry
        {
            public string Key;
            public List<RegValue> Values = new List<RegValue>();

            public RegEntry Set(string name, object value)
            {
                Values.Add(new RegValue { Name = name, Value = value });
                return this;
            }
        }

        public const int MaxFavoritesPerContext = 8;

        static ContextKind ContextOf(Target t)
        {
            switch (t)
            {
                case Target.Folder: return ContextKind.Folder;
                case Target.Background: return ContextKind.Background;
                case Target.Drive: return ContextKind.Drive;
                default: return ContextKind.File;
            }
        }

        static string ExtQuery(List<string> exts)
        {
            return string.Join(" OR ", exts.Select(e => "System.FileExtension:=\"." + e + "\"").ToArray());
        }

        static List<RegEntry> BuildPlan(SettingsData state, string exe)
        {
            var plan = new List<RegEntry>();
            var cat = ActionCatalog.Current;
            for (int i = 0; i < Roots.Length; i++)
            {
                var t = Kinds[i];
                var ctx = ContextOf(t);
                string root = Roots[i];
                string arg = t == Target.Background ? "%V" : "%1";
                plan.Add(new RegEntry { Key = root }
                    .Set("MUIVerb", "PS Tools").Set("Icon", "\"" + exe + "\",0").Set("SubCommands", "").Set("MultiSelectModel", "Player"));
                string shell = root + @"\shell";
                plan.Add(new RegEntry { Key = shell + @"\00power" }.Set("MUIVerb", "Power menu...").Set("MultiSelectModel", "Player"));
                plan.Add(new RegEntry { Key = shell + @"\00power\command" }.Set("", "\"" + exe + "\" menu \"" + arg + "\""));
                int n = 0;
                var seen = new HashSet<string>();
                foreach (var fav in state.Favorites)
                {
                    if (n >= MaxFavoritesPerContext) break;
                    var s = cat.Find(fav);
                    if (s == null || !seen.Add(s.Id) || s.AppOnly || s.UserHidden || !s.AppliesTo(ctx)) continue;
                    if (s.Legacy == null && !s.IsScript) continue;
                    string key = shell + @"\" + (10 + n).ToString("D2") + s.Id;
                    var e = new RegEntry { Key = key }.Set("MUIVerb", s.Title);
                    if (n == 0) e.Set("CommandFlags", 0x20);
                    if (s.MatchExt.Count > 0) e.Set("AppliesTo", ExtQuery(s.MatchExt));
                    else if (s.Legacy != null && !string.IsNullOrEmpty(s.Legacy.AppliesTo)) e.Set("AppliesTo", s.Legacy.AppliesTo);
                    if (s.Extended) e.Set("Extended", "");
                    e.Set("MultiSelectModel", "Player");
                    plan.Add(e);
                    plan.Add(new RegEntry { Key = key + @"\command" }.Set("", "\"" + exe + "\" " + s.Id + " \"" + arg + "\""));
                    n++;
                }
                if (t != Target.File)
                {
                    plan.Add(new RegEntry { Key = shell + @"\99open" }.Set("MUIVerb", "Open in PS Tools").Set("CommandFlags", 0x20));
                    plan.Add(new RegEntry { Key = shell + @"\99open\command" }.Set("", "\"" + exe + "\" open \"" + arg + "\""));
                }
            }
            return plan;
        }

        public static string DumpMenu(SettingsData state, string exe)
        {
            var sb = new StringBuilder();
            foreach (var e in BuildPlan(state, exe))
            {
                sb.AppendLine(@"HKCU\" + e.Key);
                foreach (var v in e.Values)
                    sb.AppendLine("  " + (v.Name.Length == 0 ? "(default)" : v.Name) + " = " + (v.Value is int ? "0x" + ((int)v.Value).ToString("X") : (string)v.Value));
            }
            return sb.ToString();
        }

        static void Write()
        {
            RemoveMenu();
            Settings.EnsureLoaded();
            var state = Settings.Snapshot();
            string exe = System.Windows.Forms.Application.ExecutablePath;
            foreach (var e in BuildPlan(state, exe))
                using (var k = Registry.CurrentUser.CreateSubKey(e.Key))
                    foreach (var v in e.Values)
                        k.SetValue(v.Name, v.Value, v.Value is int ? RegistryValueKind.DWord : RegistryValueKind.String);
        }

        public static void RemoveMenu()
        {
            foreach (var r in Roots)
            {
                Registry.CurrentUser.DeleteSubKeyTree(r, false);
                PruneEmptyParents(r);
            }
        }

        static void PruneEmptyParents(string path)
        {
            const string stop = @"Software\Classes";
            string p = path;
            while (true)
            {
                int cut = p.LastIndexOf('\\');
                if (cut < 0) return;
                p = p.Substring(0, cut);
                if (p.Length <= stop.Length) return;
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(p))
                    {
                        if (k == null) continue;
                        if (k.SubKeyCount != 0 || k.ValueCount != 0) return;
                    }
                    Registry.CurrentUser.DeleteSubKey(p, false);
                }
                catch { return; }
            }
        }

        public static string UninstallAll()
        {
            var sb = new StringBuilder();
            RemoveMenu();
            sb.AppendLine("Explorer menu removed.");
            string pending = Tweaks.Pending();
            Tweaks.RevertAll();
            if (pending.Length > 0) sb.AppendLine("Windows settings restored: " + pending + ".");
            try { if (Directory.Exists(Journal.Dir)) { Directory.Delete(Journal.Dir, true); sb.AppendLine("Settings, undo data and temporary files deleted."); } } catch (Exception ex) { sb.AppendLine("Could not delete " + Journal.Dir + ": " + ex.Message); }
            sb.AppendLine();
            sb.Append("You can now delete PSTools.exe and commands.json.");
            return sb.ToString();
        }

        static long FolderBytes(string dir)
        {
            long sum = 0;
            try { foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) try { sum += new FileInfo(f).Length; } catch { } }
            catch { }
            return sum;
        }

        public static string Status()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Explorer menu keys (HKCU):");
            bool any = false;
            foreach (var r in Roots)
                using (var k = Registry.CurrentUser.OpenSubKey(r)) if (k != null) { sb.AppendLine("  " + r); any = true; }
            if (!any) sb.AppendLine("  none");
            sb.AppendLine();
            string reg = RegisteredExe();
            if (reg != null) sb.AppendLine("Menu points to: " + reg + (PointsElsewhere() ? "  (differs from this exe: run PSTools.exe --repair)" : "  (this exe)"));
            sb.AppendLine("Data folder: " + Journal.Dir + (Directory.Exists(Journal.Dir) ? "  (" + Fs.Size(FolderBytes(Journal.Dir)) + ")" : "  (not created)"));
            if (Settings.Configured) sb.AppendLine("Menu selection: " + Settings.CurrentPreset);
            string pending = Tweaks.Pending();
            sb.AppendLine("Windows settings changed by PS Tools: " + (pending.Length == 0 ? "none" : pending));
            sb.AppendLine("Executable: " + System.Windows.Forms.Application.ExecutablePath);
            return sb.ToString();
        }
    }
}
