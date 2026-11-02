using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PSTools
{
    class SettingsData
    {
        public const int CurrentVersion = 3;
        public const int SchemaVersion = 4;
        public int Schema;
        public List<string> Favorites = new List<string>();
        public HashSet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, OverrideEdit> Edits = new Dictionary<string, OverrideEdit>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> Used = new Dictionary<string, List<string>>();

        public string Theme = "auto";
        public int Version;
        public bool Configured;
        public HashSet<string> Enabled = new HashSet<string>();
        public Dictionary<Target, List<string>> ContextPins = new Dictionary<Target, List<string>>();
        public HashSet<string> LegacyFavs = new HashSet<string>();
        public HashSet<string> GroupPins = new HashSet<string>();
        public HashSet<string> LibPins = new HashSet<string>();
        public string Win;
        public List<string> Recent = new List<string>();

        public SettingsData()
        {
            foreach (var t in DefaultPins.Contexts) ContextPins[t] = new List<string>();
        }

        public static SettingsData Parse(IEnumerable<string> lines)
        {
            var d = new SettingsData();
            foreach (var line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq < 1) continue;
                string k = line.Substring(0, eq), v = line.Substring(eq + 1).Trim();
                if (k == "theme") d.Theme = v;
                else if (k == "version") { int n; if (int.TryParse(v, out n)) d.Version = n; d.Configured = true; }
                else if (k == "enabled") { d.Enabled.Add(v); d.Configured = true; }
                else if (k == "fav") d.LegacyFavs.Add(v);
                else if (k == "pingroup") d.GroupPins.Add(v);
                else if (k == "pin") { d.LibPins.Add(v); d.Configured = true; }
                else if (k.StartsWith("pin."))
                {
                    Target t = DefaultPins.FromKey(k.Substring(4));
                    if (t != Target.None && v.Length > 0 && !d.ContextPins[t].Contains(v)) d.ContextPins[t].Add(v);
                }
                else if (k == "configured") d.Configured = true;
                else if (k == "win") d.Win = v;
                else if (k == "recent" && d.Recent.Count < 8) d.Recent.Add(v);
                else if (k == "schema") { int n; if (int.TryParse(v, out n)) d.Schema = n; }
                else if (k == "favorite") { if (v.Length > 0 && !d.Favorites.Contains(v)) d.Favorites.Add(v); }
                else if (k == "hidden") { if (v.Length > 0) d.Hidden.Add(v); }
                else if (k == "override")
                {
                    string[] p = v.Split('\t');
                    if (p.Length >= 4 && p[0].Length > 0)
                    {
                        var e = new OverrideEdit { Title = p[1].Length > 0 ? p[1] : null, Menu = p[2].Length > 0 ? p[2] : null };
                        int n;
                        if (int.TryParse(p[3], out n)) e.Order = n;
                        d.Edits[p[0]] = e;
                    }
                }
                else if (k.StartsWith("used."))
                {
                    string ctx = k.Substring(5);
                    List<string> l;
                    if (!d.Used.TryGetValue(ctx, out l)) d.Used[ctx] = l = new List<string>();
                    if (l.Count < 5 && !l.Contains(v)) l.Add(v);
                }
            }
            return d;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.AppendLine("version=" + CurrentVersion);
            sb.AppendLine("configured=1");
            sb.AppendLine("theme=" + Theme);
            foreach (var e in Enabled.OrderBy(x => x, StringComparer.Ordinal)) sb.AppendLine("enabled=" + e);
            foreach (var t in DefaultPins.Contexts)
                foreach (var id in ContextPins[t]) sb.AppendLine("pin." + DefaultPins.Key(t) + "=" + id);
            foreach (var g in GroupPins.OrderBy(x => x, StringComparer.Ordinal)) sb.AppendLine("pingroup=" + g);
            foreach (var p in LibPins.OrderBy(x => x, StringComparer.Ordinal)) sb.AppendLine("pin=" + p);
            if (Win != null) sb.AppendLine("win=" + Win);
            foreach (var r in Recent) sb.AppendLine("recent=" + r);
            if (Schema >= SchemaVersion)
            {
                sb.AppendLine("schema=" + Schema);
                foreach (var f in Favorites) sb.AppendLine("favorite=" + f);
                foreach (var h in Hidden.OrderBy(x => x, StringComparer.Ordinal)) sb.AppendLine("hidden=" + h);
                foreach (var kv in Edits.OrderBy(x => x.Key, StringComparer.Ordinal))
                    sb.AppendLine("override=" + kv.Key + "\t" + (kv.Value.Title ?? "") + "\t" + (kv.Value.Menu ?? "") + "\t" + (kv.Value.Order.HasValue ? kv.Value.Order.Value.ToString() : ""));
                foreach (var kv in Used.OrderBy(x => x.Key, StringComparer.Ordinal))
                    foreach (var id in kv.Value) sb.AppendLine("used." + kv.Key + "=" + id);
            }
            return sb.ToString();
        }

        public SettingsData Clone()
        {
            var d = Parse(Serialize().Replace("\r", "").Split('\n'));
            d.Version = Version;
            d.Configured = Configured;
            return d;
        }

        public bool IsEnabled(string id)
        {
            if (id.StartsWith("lib-")) return LibPins.Contains(id.Substring(4));
            if (id.StartsWith("recipe-")) return RecipeData.InMenu(id);
            return Enabled.Contains(id);
        }

        public bool TryPin(Target t, string id)
        {
            var a = Actions.Find(id);
            if (a == null || (a.Targets & t) == 0 || !ContextPins.ContainsKey(t)) return false;
            if (ContextPins[t].Contains(id)) { Enabled.Add(id); AddFavorite(id); return true; }
            if (ContextPins[t].Count >= Settings.MaxPins) return false;
            ContextPins[t].Add(id);
            Enabled.Add(id);
            AddFavorite(id);
            return true;
        }

        public void Unpin(Target t, string id)
        {
            if (ContextPins.ContainsKey(t)) ContextPins[t].Remove(id);
            if (!DefaultPins.Contexts.Any(c => ContextPins[c].Contains(id))) RemoveFavorite(id);
        }

        public string FavoriteIdOf(string anyId)
        {
            var s = ActionCatalog.Current.Find(anyId);
            return s == null ? null : s.Id;
        }

        public void AddFavorite(string anyId)
        {
            string id = FavoriteIdOf(anyId);
            if (id != null && !Favorites.Contains(id)) Favorites.Add(id);
        }

        public void RemoveFavorite(string anyId)
        {
            string id = FavoriteIdOf(anyId);
            if (id != null) Favorites.Remove(id);
        }

        public bool IsFavorite(string anyId)
        {
            string id = FavoriteIdOf(anyId);
            return id != null && Favorites.Contains(id);
        }

        public void NoteUsed(string ctxName, string anyId)
        {
            string id = FavoriteIdOf(anyId);
            if (id == null) return;
            List<string> l;
            if (!Used.TryGetValue(ctxName, out l)) Used[ctxName] = l = new List<string>();
            l.Remove(id);
            l.Insert(0, id);
            if (l.Count > 5) l.RemoveRange(5, l.Count - 5);
        }

        public CatalogOverrides ToOverrides()
        {
            var o = new CatalogOverrides();
            foreach (var h in Hidden) o.Hidden.Add(h);
            foreach (var kv in Edits) o.Edits[kv.Key] = kv.Value;
            return o;
        }

        public static List<string> FavoritesFromPins(SettingsData d)
        {
            var list = new List<string>();
            var cat = ActionCatalog.Current;
            foreach (var t in new[] { Target.Folder, Target.Background, Target.File, Target.Drive })
            {
                List<string> pins;
                if (!d.ContextPins.TryGetValue(t, out pins)) continue;
                foreach (var id in pins)
                {
                    var s = cat.Find(id);
                    if (s != null && !list.Contains(s.Id)) list.Add(s.Id);
                }
            }
            foreach (var id in d.LibPins.OrderBy(x => x, StringComparer.Ordinal))
            {
                var s = cat.Find("lib-" + id);
                if (s != null && !list.Contains(s.Id)) list.Add(s.Id);
            }
            foreach (var r in RecipeData.All.Where(x => x.Menu))
            {
                var s = cat.Find("recipe-" + r.Id);
                if (s != null && !list.Contains(s.Id)) list.Add(s.Id);
            }
            return list;
        }

        public static bool MigrateToV4(SettingsData d)
        {
            if (d.Schema >= SchemaVersion) return false;
            d.Favorites = FavoritesFromPins(d);
            d.Schema = SchemaVersion;
            return true;
        }

        public bool MovePin(Target t, string id, int delta)
        {
            List<string> l;
            if (!ContextPins.TryGetValue(t, out l)) return false;
            int i = l.IndexOf(id), j = i + delta;
            if (i < 0 || j < 0 || j >= l.Count) return false;
            l.RemoveAt(i);
            l.Insert(j, id);
            return true;
        }

        public void SetEnabled(string id, bool on)
        {
            if (on) { Enabled.Add(id); return; }
            Enabled.Remove(id);
            foreach (var t in DefaultPins.Contexts) ContextPins[t].Remove(id);
        }

        public bool SameAs(SettingsData o, out string difference)
        {
            difference = null;
            if (Theme != o.Theme) { difference = "theme"; return false; }
            if (!Enabled.SetEquals(o.Enabled)) { difference = "enabled set"; return false; }
            foreach (var t in DefaultPins.Contexts)
                if (!ContextPins[t].SequenceEqual(o.ContextPins[t])) { difference = "pins for " + DefaultPins.Key(t); return false; }
            if (!GroupPins.SetEquals(o.GroupPins)) { difference = "group pins"; return false; }
            if (!LibPins.SetEquals(o.LibPins)) { difference = "library pins"; return false; }
            if (Win != o.Win) { difference = "window"; return false; }
            if (!Recent.SequenceEqual(o.Recent)) { difference = "recent"; return false; }
            if (!Favorites.SequenceEqual(o.Favorites)) { difference = "favorites"; return false; }
            if (!Hidden.SetEquals(o.Hidden)) { difference = "hidden"; return false; }
            return true;
        }

        public static void Migrate(SettingsData d, IList<Action> actions)
        {
            if (d.Version >= CurrentVersion) return;
            foreach (var t in DefaultPins.Contexts) d.ContextPins[t] = new List<string>();
            foreach (var t in DefaultPins.Contexts)
                foreach (var id in DefaultPins.For(t))
                {
                    var a = actions.FirstOrDefault(x => x.Id == id);
                    if (a != null && (a.Targets & t) != 0 && d.LegacyFavs.Contains(id) && d.Enabled.Contains(id)) d.ContextPins[t].Add(id);
                }
            foreach (var id in d.LegacyFavs.OrderBy(x => x, StringComparer.Ordinal))
            {
                var a = actions.FirstOrDefault(x => x.Id == id);
                if (a == null || !d.Enabled.Contains(id)) continue;
                bool inDefaults = DefaultPins.Contexts.Any(t => (a.Targets & t) != 0 && DefaultPins.For(t).Contains(id));
                if (inDefaults) continue;
                foreach (var t in DefaultPins.Contexts)
                    if ((a.Targets & t) != 0 && !d.ContextPins[t].Contains(id)) d.ContextPins[t].Add(id);
            }
            foreach (var t in DefaultPins.Contexts)
                if (d.ContextPins[t].Count > Settings.MaxPins) d.ContextPins[t].RemoveRange(Settings.MaxPins, d.ContextPins[t].Count - Settings.MaxPins);
            d.LegacyFavs.Clear();
            d.Version = CurrentVersion;
        }

        public static HashSet<string> DefaultEnabled()
        {
            return Presets.Ids("Everyday");
        }

        public static void FillDefaultPins(SettingsData d)
        {
            foreach (var t in DefaultPins.Contexts)
            {
                var list = new List<string>();
                foreach (var id in DefaultPins.For(t))
                {
                    var a = Actions.Find(id);
                    if (a != null && (a.Targets & t) != 0 && d.Enabled.Contains(id)) list.Add(id);
                }
                d.ContextPins[t] = list;
            }
        }

        public static SettingsData ForPreset(string name, SettingsData current, bool keepPins)
        {
            var d = current == null ? new SettingsData() : current.Clone();
            d.Version = CurrentVersion;
            d.Configured = true;
            d.Enabled = Presets.Ids(name);
            if (keepPins && current != null)
            {
                foreach (var t in DefaultPins.Contexts)
                    foreach (var id in current.ContextPins[t]) d.Enabled.Add(id);
            }
            else
            {
                FillDefaultPins(d);
                d.GroupPins = new HashSet<string>();
            }
            return d;
        }
    }

    static class Settings
    {
        public const int MaxPins = 12;

        public static string FileOverride;

        static string FilePath { get { return FileOverride ?? Path.Combine(Journal.Dir, "settings.txt"); } }

        static SettingsData data = new SettingsData();
        static bool loaded;
        static bool prepared;

        public static string Theme { get { Load(); return data.Theme; } }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (File.Exists(FilePath)) data = SettingsData.Parse(File.ReadAllLines(FilePath, Encoding.UTF8));
            }
            catch { }
        }

        static void Defaults()
        {
            data.Enabled = SettingsData.DefaultEnabled();
            SettingsData.FillDefaultPins(data);
            data.Version = SettingsData.CurrentVersion;
            SettingsData.MigrateToV4(data);
        }

        public static SettingsData Snapshot()
        {
            EnsureLoaded();
            return data.Clone();
        }

        public static SettingsData DefaultState()
        {
            var d = new SettingsData();
            d.Enabled = SettingsData.DefaultEnabled();
            SettingsData.FillDefaultPins(d);
            d.Version = SettingsData.CurrentVersion;
            d.Configured = true;
            return d;
        }

        public static bool Configured { get { Load(); return data.Configured; } }

        public static string CurrentPreset { get { EnsureLoaded(); return Presets.Match(data.Enabled); } }

        public static bool HasPins
        {
            get
            {
                EnsureLoaded();
                return data.GroupPins.Count > 0 || DefaultPins.Contexts.Any(t => data.ContextPins[t].Count > 0);
            }
        }

        public static void ApplyPreset(string name) { ApplyPreset(name, false); }

        public static void ApplyPreset(string name, bool keepPins)
        {
            EnsureLoaded();
            var next = SettingsData.ForPreset(name, data, keepPins);
            data.Enabled = next.Enabled;
            data.ContextPins = next.ContextPins;
            data.GroupPins = next.GroupPins;
            data.Version = SettingsData.CurrentVersion;
            data.Configured = true;
            Save();
        }

        public static void MarkConfigured()
        {
            EnsureLoaded();
            data.Configured = true;
            Save();
        }

        public static void EnsureLoaded()
        {
            Load();
            if (prepared) return;
            prepared = true;
            if (!data.Configured) Defaults();
            else
            {
                SettingsData.Migrate(data, Actions.All);
                if (data.Schema < SettingsData.SchemaVersion)
                {
                    BackupV3();
                    SettingsData.MigrateToV4(data);
                    try { Save(); } catch { }
                }
            }
        }

        static void BackupV3()
        {
            try
            {
                string backup = Path.Combine(Path.GetDirectoryName(FilePath), "settings.v3.bak.txt");
                if (File.Exists(FilePath) && !File.Exists(backup)) File.Copy(FilePath, backup);
            }
            catch { }
        }

        public static List<string> Favorites { get { EnsureLoaded(); return new List<string>(data.Favorites); } }

        public static CatalogOverrides Overrides { get { EnsureLoaded(); return data.ToOverrides(); } }

        public static List<string> UsedIn(string ctxName)
        {
            EnsureLoaded();
            List<string> l;
            return data.Used.TryGetValue(ctxName, out l) ? new List<string>(l) : new List<string>();
        }

        public static bool IsFavorite(string id) { EnsureLoaded(); return data.IsFavorite(id); }

        public static bool ToggleFavorite(string id)
        {
            EnsureLoaded();
            bool now;
            if (data.IsFavorite(id)) { data.RemoveFavorite(id); now = false; }
            else { data.AddFavorite(id); now = true; }
            Save();
            return now;
        }

        public static void NoteUsed(string ctxName, string id)
        {
            EnsureLoaded();
            data.NoteUsed(ctxName, id);
            try { Save(); } catch { }
        }

        public static void SetHidden(string id, bool hidden)
        {
            EnsureLoaded();
            var s = ActionCatalog.Current.Find(id);
            string key = s == null ? id : s.Id;
            if (hidden) data.Hidden.Add(key); else data.Hidden.Remove(key);
            Save();
            ActionCatalog.Reset();
        }

        public static bool IsEnabled(string id)
        {
            EnsureLoaded();
            return data.IsEnabled(id);
        }

        public static HashSet<string> Pins { get { Load(); return data.LibPins; } }

        public static void TogglePin(string cmdId)
        {
            EnsureLoaded();
            if (!data.LibPins.Remove(cmdId)) data.LibPins.Add(cmdId);
            Save();
        }

        public static void SetEnabled(IEnumerable<string> ids, bool on)
        {
            EnsureLoaded();
            foreach (var id in ids.ToList()) data.SetEnabled(id, on);
            data.Configured = true;
            Save();
        }

        public static List<string> PinsFor(Target t)
        {
            EnsureLoaded();
            List<string> l;
            return data.ContextPins.TryGetValue(t, out l) ? new List<string>(l) : new List<string>();
        }

        public static bool IsPinned(string id, Target t)
        {
            EnsureLoaded();
            List<string> l;
            return data.ContextPins.TryGetValue(t, out l) && l.Contains(id);
        }

        public static bool IsPinnedAnywhere(string id)
        {
            EnsureLoaded();
            return DefaultPins.Contexts.Any(t => data.ContextPins[t].Contains(id));
        }

        public static void SetPins(Target t, List<string> ids)
        {
            EnsureLoaded();
            if (!data.ContextPins.ContainsKey(t)) return;
            var clean = new List<string>();
            foreach (var id in ids) if (!clean.Contains(id)) clean.Add(id);
            if (clean.Count > MaxPins) clean.RemoveRange(MaxPins, clean.Count - MaxPins);
            data.ContextPins[t] = clean;
            foreach (var id in clean) data.Enabled.Add(id);
            data.Configured = true;
            Save();
        }

        public static bool Pin(Target t, string id)
        {
            EnsureLoaded();
            if (!data.TryPin(t, id)) return false;
            data.Configured = true;
            Save();
            return true;
        }

        public static void Unpin(Target t, string id)
        {
            EnsureLoaded();
            data.Unpin(t, id);
            Save();
        }

        public static void UnpinEverywhere(string id)
        {
            EnsureLoaded();
            foreach (var t in DefaultPins.Contexts) data.Unpin(t, id);
            Save();
        }

        public static void MovePin(Target t, string id, int delta)
        {
            EnsureLoaded();
            if (data.MovePin(t, id, delta)) Save();
        }

        public static bool IsGroupPinned(string group) { Load(); return data.GroupPins.Contains(group); }

        public static HashSet<string> GroupPins { get { Load(); return data.GroupPins; } }

        public static void SetGroupPins(IEnumerable<string> groups)
        {
            EnsureLoaded();
            data.GroupPins = new HashSet<string>(groups);
            Save();
        }

        public static void ResetDefaults()
        {
            Load();
            prepared = true;
            data.GroupPins = new HashSet<string>();
            Defaults();
            data.Configured = true;
            Save();
        }

        public static void SetTheme(string t) { EnsureLoaded(); data.Theme = t; Save(); }

        public static void Save()
        {
            EnsureLoaded();
            Directory.CreateDirectory(Journal.Dir);
            File.WriteAllText(FilePath, data.Serialize(), Encoding.UTF8);
        }

        public static List<string> Recent { get { Load(); return data.Recent; } }

        public static void AddRecent(string id)
        {
            Load();
            data.Recent.Remove(id);
            data.Recent.Insert(0, id);
            if (data.Recent.Count > 8) data.Recent.RemoveRange(8, data.Recent.Count - 8);
            if (!data.Configured) return;
            try { Save(); } catch { }
        }

        public static bool TryGetWindow(out System.Drawing.Rectangle bounds, out bool maximized)
        {
            Load();
            bounds = System.Drawing.Rectangle.Empty;
            maximized = false;
            if (data.Win == null) return false;
            string[] p = data.Win.Split(',');
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
            if (v == data.Win) return;
            data.Win = v;
            try { Save(); } catch { }
        }

        public static void PreloadTheme()
        {
            Load();
        }
    }
}
