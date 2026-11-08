using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PSTools
{
    class OverrideEdit
    {
        public string Title, Menu;
        public int? Order;
    }

    class CatalogOverrides
    {
        public HashSet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, OverrideEdit> Edits = new Dictionary<string, OverrideEdit>(StringComparer.OrdinalIgnoreCase);

        public static CatalogOverrides Parse(string json)
        {
            var o = new CatalogOverrides();
            if (string.IsNullOrWhiteSpace(json)) return o;
            var root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) return o;
            object v;
            if (root.TryGetValue("hidden", out v) && v is IEnumerable && !(v is string))
                foreach (object x in (IEnumerable)v) if (x != null) o.Hidden.Add(x.ToString());
            if (root.TryGetValue("overrides", out v) && v is Dictionary<string, object>)
                foreach (var kv in (Dictionary<string, object>)v)
                {
                    var d = kv.Value as Dictionary<string, object>;
                    if (d == null) continue;
                    var e = new OverrideEdit();
                    object t;
                    if (d.TryGetValue("title", out t) && t != null) e.Title = t.ToString();
                    if (d.TryGetValue("menu", out t) && t != null) e.Menu = t.ToString();
                    if (d.TryGetValue("order", out t) && t != null) { int n; if (int.TryParse(t.ToString(), out n)) e.Order = n; }
                    o.Edits[kv.Key] = e;
                }
            return o;
        }
    }

    class CatalogInput
    {
        public List<Action> Builtins = new List<Action>();
        public string ShippedPath, LibraryPath, UserDir;
        public CatalogOverrides Overrides = new CatalogOverrides();

        public static CatalogInput Default()
        {
            return new CatalogInput
            {
                Builtins = Actions.All,
                ShippedPath = Path.Combine(Program.ExeDir, "actions.json"),
                LibraryPath = Path.Combine(Program.ExeDir, "commands.json"),
                UserDir = Path.Combine(Journal.Dir, "actions"),
                Overrides = Settings.Overrides
            };
        }
    }

    class ActionCatalog
    {
        public List<ActionSpec> All = new List<ActionSpec>();
        public List<CatalogProblem> Problems = new List<CatalogProblem>();
        public int LegacyFallbacks;

        readonly Dictionary<string, ActionSpec> lookup = new Dictionary<string, ActionSpec>(StringComparer.OrdinalIgnoreCase);

        static ActionCatalog current;

        public static ActionCatalog Current
        {
            get { if (current == null) current = Load(CatalogInput.Default()); return current; }
        }

        public static void Reset() { current = null; }

        public ActionSpec Find(string idOrAlias)
        {
            ActionSpec s;
            return idOrAlias != null && lookup.TryGetValue(idOrAlias, out s) ? s : null;
        }

        public IEnumerable<CatalogProblem> UserProblems { get { return Problems.Where(p => !p.File.StartsWith("builtin") && p.File != "actions.json" && p.File != "commands.json"); } }

        public IEnumerable<CatalogProblem> ShippedProblems { get { return Problems.Where(p => p.File.StartsWith("builtin") || p.File == "actions.json" || p.File == "commands.json"); } }

        void Problem(string file, int line, string reason)
        {
            Problems.Add(new CatalogProblem { File = file, Line = line, Reason = reason });
        }

        void Add(ActionSpec s, string file, int line)
        {
            string bad = SpecRules.Validate(s);
            if (bad != null) { Problem(file, line, bad); return; }
            ActionSpec old;
            if (lookup.TryGetValue(s.Id, out old))
            {
                if (old.Id.Equals(s.Id, StringComparison.OrdinalIgnoreCase))
                {
                    s.Overrides = old.Source + (old.SourceFile.Length > 0 ? " " + old.SourceFile : "");
                    All.Remove(old);
                    foreach (var key in lookup.Where(kv => kv.Value == old).Select(kv => kv.Key).ToList()) lookup.Remove(key);
                }
                else Problem(file, line, "id '" + s.Id + "' is already used as an alias of '" + old.Id + "'");
            }
            All.Add(s);
            lookup[s.Id] = s;
            foreach (var a in s.Aliases)
            {
                ActionSpec other;
                if (lookup.TryGetValue(a, out other) && other != s)
                {
                    if (other.Id.Equals(a, StringComparison.OrdinalIgnoreCase)) { Problem(file, line, "alias '" + a + "' is the id of another action"); continue; }
                    lookup[a] = s;
                }
                else lookup[a] = s;
            }
        }

        public static ActionCatalog Load(CatalogInput input)
        {
            var c = new ActionCatalog();
            c.LoadBuiltins(input.Builtins);
            if (input.ShippedPath != null) c.LoadJsonFile(input.ShippedPath, SpecSource.Shipped);
            if (input.LibraryPath != null) c.LoadLibrary(input.LibraryPath);
            if (input.UserDir != null && Directory.Exists(input.UserDir)) c.LoadUserFolder(input.UserDir);
            c.ApplyOverrides(input.Overrides);
            return c;
        }

        static string Slug(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in (s ?? "").ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            return sb.ToString().Trim('-');
        }

        void LoadBuiltins(List<Action> list)
        {
            int position = 0;
            foreach (var a in list)
            {
                position++;
                if (a.Id.StartsWith("lib-")) continue;
                var s = new ActionSpec { Source = a.Id.StartsWith("recipe-") ? SpecSource.Recipe : SpecSource.Builtin, SourceFile = a.Id.StartsWith("recipe-") ? "recipes.json" : "" };
                s.Legacy = a;
                s.Aliases.Add(a.Id);
                s.Builtin = a.Id;
                s.Order = 100 + position * 5;
                s.AppOnly = a.Targets == Target.None;
                s.When = SpecRules.FromTarget(a.Targets);
                if (s.When == ContextKind.None) s.When = ContextKind.Background;
                s.Description = string.IsNullOrWhiteSpace(a.Hint) ? a.Label : a.Hint;
                s.Ps = a.Ps ?? "";
                s.PsNote = a.PsNone ?? "";
                s.Extended = a.Extended;
                s.Admin = a.Tier == Tier.Elevated;
                s.Multi = a.Multi ? MultiMode.Join : MultiMode.Single;
                s.Undo = a.Undoable ? UndoMode.Journal : UndoMode.None;
                s.Keywords = a.Id.Split('-').Where(x => x.Length > 1).ToList();
                if (a.AppliesTo == Modules.ImageQuery) s.MatchExt = new List<string> { "png", "jpg", "jpeg", "bmp", "gif", "tif", "tiff" };
                else if (a.AppliesTo == Modules.ZipQuery) s.MatchExt = new List<string> { "zip" };
                string tool;
                if (V3Map.Requires.TryGetValue(a.Id, out tool)) s.Requires.Add(tool);

                if (a.Window != null) s.Output = ActionOutput.Window;
                else if (V3Map.Grid.Contains(a.Id)) s.Output = ActionOutput.Grid;
                else if (a.Group == "Copy") s.Output = ActionOutput.Clipboard;
                else if (a.Script != null) s.Output = ActionOutput.Console;
                else s.Output = ActionOutput.Toast;

                if (V3Map.Destructive.Contains(a.Id)) s.Risk = Risk.Destructive;
                else if (a.Tier != Tier.Instant || a.Undoable) s.Risk = Risk.Modifies;
                else s.Risk = Risk.Safe;

                if (s.Source == SpecSource.Recipe)
                {
                    string rid = a.Id.Substring("recipe-".Length);
                    s.Id = "recipe." + Slug(rid);
                    s.Title = a.Label.Length > SpecRules.MaxTitleLoose ? a.Label.Substring(0, SpecRules.MaxTitleLoose - 3) + "..." : a.Label;
                    s.Menu = "Recipes";
                    s.Builtin = "recipe:" + rid;
                    s.Output = ActionOutput.Toast;
                    s.Risk = a.Tier == Tier.Instant ? Risk.Modifies : Risk.Modifies;
                    s.Undo = UndoMode.Journal;
                    s.Description = string.IsNullOrWhiteSpace(a.Hint) ? "Recipe: runs its steps in order on the same selection." : a.Hint;
                }
                else
                {
                    var row = V3Map.Find(a.Id);
                    if (a.V4Menu != null) { s.Id = a.Id; s.Menu = a.V4Menu; s.Title = a.V4Title ?? a.Label; s.Aliases.Clear(); }
                    else if (row != null) { s.Id = row[1]; s.Menu = row[2]; s.Title = row[3]; }
                    else
                    {
                        LegacyFallbacks++;
                        s.Id = "legacy." + Slug(a.Group) + "." + a.Id;
                        s.Menu = a.Group;
                        s.Title = a.Label.Length > SpecRules.MaxTitleStrict ? a.Label.Substring(0, SpecRules.MaxTitleStrict - 3) + "..." : a.Label;
                    }
                }
                Add(s, "builtin:" + a.Id, 0);
            }
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : null;
        }

        static List<string> Strings(Dictionary<string, object> d, string key)
        {
            var l = new List<string>();
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return l;
            if (v is string) { l.AddRange(ScriptHeader.List((string)v)); return l; }
            var seq = v as IEnumerable;
            if (seq != null)
            {
                foreach (object x in seq)
                {
                    if (x != null) l.Add(x.ToString());
                }
            }
            return l;
        }

        static T Enum<T>(string s, out bool ok) where T : struct
        {
            T r;
            ok = s != null && System.Enum.TryParse<T>(s, true, out r);
            if (!ok) return default(T);
            System.Enum.TryParse<T>(s, true, out r);
            return r;
        }

        static int LineOf(string text, string id)
        {
            int i = text.IndexOf("\"" + id + "\"", StringComparison.Ordinal);
            if (i < 0) return 0;
            int line = 1;
            for (int k = 0; k < i; k++) if (text[k] == '\n') line++;
            return line;
        }

        public static ActionSpec ParseSpec(Dictionary<string, object> d, SpecSource source, string file, out string error)
        {
            error = null;
            var s = new ActionSpec { Source = source, SourceFile = file };
            s.Id = (Str(d, "id") ?? "").Trim();
            s.Aliases = Strings(d, "aliases");
            s.Title = (Str(d, "title") ?? "").Trim();
            s.Menu = (Str(d, "menu") ?? "").Trim();
            s.Description = (Str(d, "description") ?? "").Trim();
            s.Ps = Str(d, "ps") ?? "";
            s.Command = Str(d, "command") ?? "";
            s.Builtin = Str(d, "builtin") ?? "";
            s.Icon = Str(d, "icon") ?? "";
            s.Keywords = Strings(d, "keywords");
            s.Requires = Strings(d, "requires");
            string tmp = Str(d, "order");
            int order;
            if (tmp != null && int.TryParse(tmp, out order)) s.Order = order;
            foreach (var w in Strings(d, "when"))
            {
                var k = SpecRules.ParseContext(w);
                if (k == ContextKind.None) { error = "when contains unknown context '" + w + "'"; return null; }
                s.When |= k;
            }
            bool ok;
            s.Shell = Enum<ActionShell>(Str(d, "shell"), out ok);
            if (!ok) { error = "shell must be builtin, powershell, pwsh or cmd"; return null; }
            s.Output = Enum<ActionOutput>(Str(d, "output"), out ok);
            if (!ok) { error = "output must be clipboard, toast, grid, console, window or none"; return null; }
            s.Risk = Enum<Risk>(Str(d, "risk"), out ok);
            if (!ok) { error = "risk must be safe, modifies or destructive"; return null; }
            if (Str(d, "multi") != null) { s.Multi = Enum<MultiMode>(Str(d, "multi"), out ok); if (!ok) { error = "multi must be join, each or single"; return null; } }
            if (Str(d, "undo") != null) { s.Undo = Enum<UndoMode>(Str(d, "undo"), out ok); if (!ok) { error = "undo must be none or journal"; return null; } }
            object v;
            s.Admin = d.TryGetValue("admin", out v) && v is bool && (bool)v;
            s.Extended = d.TryGetValue("extended", out v) && v is bool && (bool)v;
            s.Fallback = d.TryGetValue("fallback", out v) && v is bool && (bool)v;
            if (d.TryGetValue("match", out v) && v is Dictionary<string, object>)
            {
                var m = (Dictionary<string, object>)v;
                s.MatchExt = Strings(m, "ext").Select(x => x.TrimStart('.').ToLowerInvariant()).ToList();
                if (Str(m, "kind") != null) { s.MatchKind = Enum<MatchKind>(Str(m, "kind"), out ok); if (!ok) { error = "match.kind must be file, folder or any"; return null; } }
                s.MatchHas = Strings(m, "has");
                int n;
                if (Str(m, "min") != null && int.TryParse(Str(m, "min"), out n)) s.MatchMin = n;
                if (Str(m, "max") != null && int.TryParse(Str(m, "max"), out n)) s.MatchMax = n;
            }
            if (d.TryGetValue("inputs", out v) && v is Dictionary<string, object>)
                foreach (var kv in (Dictionary<string, object>)v)
                {
                    var id = kv.Value as Dictionary<string, object>;
                    if (id == null) { error = "input '" + kv.Key + "' must be an object"; return null; }
                    var inp = new InputSpec { Name = kv.Key, Label = Str(id, "label") ?? kv.Key, Type = (Str(id, "type") ?? "text").ToLowerInvariant(), Default = Str(id, "default") ?? "", Options = Strings(id, "options") };
                    s.Inputs.Add(inp);
                }
            if (d.TryGetValue("test", out v) && v is Dictionary<string, object>)
            {
                var t = (Dictionary<string, object>)v;
                s.Test = new ActionTest { Fixture = Str(t, "fixture") ?? "", Target = Str(t, "target") ?? "", Expect = Str(t, "expect") ?? "golden" };
            }
            foreach (string required in new[] { "id", "title", "when", "shell", "output", "risk", "description" })
                if (!d.ContainsKey(required)) { error = "required field '" + required + "' is missing"; return null; }
            foreach (string name in Placeholders.InputNames(s.Command))
                if (!s.Inputs.Any(i => i.Name == name)) { error = "command uses {input:" + name + "} but no input '" + name + "' is declared"; return null; }
            if (s.Shell == ActionShell.Builtin && s.Builtin.Length == 0) { error = "builtin key is missing"; return null; }
            return s;
        }

        static List<Dictionary<string, object>> SpecList(object root)
        {
            var l = new List<Dictionary<string, object>>();
            var d = root as Dictionary<string, object>;
            object v;
            if (d != null && d.TryGetValue("actions", out v)) root = v;
            else if (d != null) { l.Add(d); return l; }
            var arr = root as IEnumerable;
            if (arr != null && !(root is string))
                foreach (object x in arr) { var e = x as Dictionary<string, object>; if (e != null) l.Add(e); }
            return l;
        }

        void LoadJsonFile(string path, SpecSource source)
        {
            if (!File.Exists(path)) return;
            string name = Path.GetFileName(path);
            string text;
            object root;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
                root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(text);
            }
            catch (Exception ex) { Problem(source == SpecSource.Shipped ? name : path, 0, "could not read JSON: " + ex.Message); return; }
            foreach (var d in SpecList(root))
            {
                string error;
                var s = ParseSpec(d, source, path, out error);
                string id = Str(d, "id") ?? "?";
                string shown = source == SpecSource.Shipped ? name : path;
                if (s == null) { Problem(shown, LineOf(text, id), id + ": " + error); continue; }
                Add(s, shown, LineOf(text, id));
            }
        }

        void LoadLibrary(string path)
        {
            if (!File.Exists(path)) return;
            string text;
            Dictionary<string, object> root;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
                root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(text) as Dictionary<string, object>;
            }
            catch (Exception ex) { Problem("commands.json", 0, "could not read JSON: " + ex.Message); return; }
            if (root == null) { Problem("commands.json", 0, "root must be an object"); return; }
            var labels = new Dictionary<string, string>();
            object v;
            if (root.TryGetValue("categories", out v) && v is IEnumerable)
                foreach (var x in (IEnumerable)v) { var cd = x as Dictionary<string, object>; if (cd != null) labels[Str(cd, "id") ?? ""] = Str(cd, "label") ?? ""; }
            if (!root.TryGetValue("commands", out v) || !(v is IEnumerable)) return;
            foreach (var x in (IEnumerable)v)
            {
                var d = x as Dictionary<string, object>;
                if (d == null) continue;
                string rawId = Str(d, "id") ?? "";
                string cat = Str(d, "category") ?? "";
                string label;
                if (!labels.TryGetValue(cat, out label) || label.Length == 0) label = cat.Length == 0 ? "Other" : cat;
                var s = new ActionSpec { Source = SpecSource.Library, SourceFile = "commands.json" };
                s.Id = "lib." + rawId;
                s.Aliases.Add("lib-" + rawId);
                s.Title = (Str(d, "title") ?? "").Trim();
                s.Menu = "Library/" + label.Replace("/", "-");
                s.When = ContextKind.Folder | ContextKind.Background | ContextKind.Drive;
                s.Shell = ActionShell.PowerShell;
                s.Command = Str(d, "command") ?? "";
                s.Output = ActionOutput.Console;
                string risk = (Str(d, "risk") ?? "modifies").ToLowerInvariant();
                s.Risk = risk == "safe" ? Risk.Safe : risk == "destructive" ? Risk.Destructive : Risk.Modifies;
                s.Admin = d.TryGetValue("admin", out v) && v is bool && (bool)v;
                s.Keywords = Strings(d, "tags");
                string desc = (Str(d, "description") ?? "").Trim();
                string note = (Str(d, "note") ?? "").Trim();
                s.Description = note.Length > 0 ? desc + " " + note : desc;
                s.PopupHidden = true;
                s.Multi = MultiMode.Single;
                Add(s, "commands.json", LineOf(text, rawId));
            }
        }

        static readonly string[] UserKeys = { "id", "title", "menu", "when", "output", "risk", "requires", "keywords", "description", "aliases", "order", "admin", "multi", "extended", "icon", "shell", "ext" };

        void LoadUserFolder(string dir)
        {
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith("_") || name.StartsWith(".")) continue;
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".json") LoadJsonFile(file, SpecSource.User);
                else if (ext == ".ps1" || ext == ".cmd" || ext == ".bat") LoadScriptFile(dir, file, ext);
            }
        }

        void LoadScriptFile(string root, string file, string ext)
        {
            string text;
            try { text = File.ReadAllText(file, Encoding.UTF8); }
            catch (Exception ex) { Problem(file, 0, "could not read: " + ex.Message); return; }
            bool cmd = ext != ".ps1";
            var h = ScriptHeader.Parse(text, cmd);
            foreach (var w in h.Warnings) Problem(file, 0, "warning: " + w);
            foreach (var key in h.Keys.Keys)
                if (Array.IndexOf(UserKeys, key) < 0) Problem(file, 0, "warning: unknown header key @" + key);

            string rel = file.Substring(root.Length).TrimStart('\\', '/');
            string relDir = Path.GetDirectoryName(rel) ?? "";
            var s = new ActionSpec { Source = SpecSource.User, SourceFile = file };
            string stem = Path.GetFileNameWithoutExtension(rel);
            string relNoExt = string.IsNullOrEmpty(relDir) ? stem : relDir + "/" + stem;
            string v;
            s.Id = h.Keys.TryGetValue("id", out v) && v.Length > 0 ? v : "user." + Slug(relNoExt.Replace('\\', '/'));
            s.Title = h.Keys.TryGetValue("title", out v) && v.Length > 0 ? v : stem;
            s.Menu = h.Keys.TryGetValue("menu", out v) ? v : relDir.Replace('\\', '/');
            s.Description = h.Keys.TryGetValue("description", out v) && v.Length > 0 ? v : "Your script " + Path.GetFileName(file) + ".";
            s.Command = h.Body;
            string shell;
            s.Shell = cmd ? ActionShell.Cmd : (h.Keys.TryGetValue("shell", out shell) && shell.Equals("pwsh", StringComparison.OrdinalIgnoreCase) ? ActionShell.Pwsh : ActionShell.PowerShell);
            if (h.Keys.TryGetValue("when", out v))
            {
                foreach (var w in ScriptHeader.List(v))
                {
                    var k = SpecRules.ParseContext(w);
                    if (k == ContextKind.None) { Problem(file, 0, "when contains unknown context '" + w + "'"); return; }
                    s.When |= k;
                }
            }
            else s.When = ContextKind.Folder | ContextKind.Background | ContextKind.File;
            bool ok = true;
            if (h.Keys.TryGetValue("output", out v)) { s.Output = Enum<ActionOutput>(v, out ok); if (!ok) { Problem(file, 0, "output '" + v + "' is not valid"); return; } }
            else s.Output = ActionOutput.Console;
            if (h.Keys.TryGetValue("risk", out v)) { s.Risk = Enum<Risk>(v, out ok); if (!ok) { Problem(file, 0, "risk '" + v + "' is not valid"); return; } }
            else s.Risk = Risk.Modifies;
            if (h.Keys.TryGetValue("multi", out v)) { s.Multi = Enum<MultiMode>(v, out ok); if (!ok) { Problem(file, 0, "multi '" + v + "' is not valid"); return; } }
            if (h.Keys.TryGetValue("order", out v)) { int n; if (int.TryParse(v, out n)) s.Order = n; }
            if (h.Keys.TryGetValue("admin", out v)) s.Admin = v.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (h.Keys.TryGetValue("extended", out v)) s.Extended = v.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (h.Keys.TryGetValue("icon", out v)) s.Icon = v;
            if (h.Keys.TryGetValue("requires", out v)) s.Requires = ScriptHeader.List(v);
            if (h.Keys.TryGetValue("keywords", out v)) s.Keywords = ScriptHeader.List(v);
            if (h.Keys.TryGetValue("aliases", out v)) s.Aliases = ScriptHeader.List(v);
            if (h.Keys.TryGetValue("ext", out v)) s.MatchExt = ScriptHeader.List(v).Select(x => x.TrimStart('.').ToLowerInvariant()).ToList();
            Add(s, file, 0);
        }

        void ApplyOverrides(CatalogOverrides o)
        {
            if (o == null) return;
            foreach (var id in o.Hidden)
            {
                var s = Find(id);
                if (s != null) s.UserHidden = true;
            }
            foreach (var kv in o.Edits)
            {
                var s = Find(kv.Key);
                if (s == null) continue;
                var copy = new ActionSpec { Id = s.Id, Title = kv.Value.Title ?? s.Title, Menu = kv.Value.Menu ?? s.Menu, When = s.When, Description = s.Description, Source = s.Source, Shell = s.Shell, Builtin = s.Builtin, Command = s.Command, Legacy = s.Legacy };
                string bad = SpecRules.Validate(copy);
                if (bad != null) { Problem("settings overrides", 0, kv.Key + ": " + bad); continue; }
                s.Title = copy.Title;
                s.Menu = copy.Menu;
                if (kv.Value.Order.HasValue) s.Order = kv.Value.Order.Value;
            }
        }
    }
}
