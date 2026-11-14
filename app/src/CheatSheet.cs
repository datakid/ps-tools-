using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PSTools
{
    class CheatParam
    {
        public string Name = "", Text = "";
    }

    class CheatTemplate
    {
        public string[] When = new string[0];
        public string Output = "console";
        public string Command = "";
    }

    class CheatCategory
    {
        public string Id = "", Label = "", Icon = "";
    }

    class CheatTranslation
    {
        public string From = "", To = "", Note = "";
    }

    class CheatEntry
    {
        public string Id = "", Category = "", Title = "", Command = "", Explain = "", Example = "", ExampleOutput = "", MinVersion = "5.1", Risk = "safe", RunIn = "";
        public string[] Aliases = new string[0], Keywords = new string[0], Related = new string[0];
        public List<CheatParam> Params = new List<CheatParam>();
        public bool SafeToRun, ExpectError, Admin;
        public CheatTemplate Template;

        public string RunText { get { return Example.Length > 0 ? Example : Command; } }

        public bool NeedsPwsh { get { return MinVersion == "7.0"; } }
    }

    class CheatSheetData
    {
        public List<CheatCategory> Categories = new List<CheatCategory>();
        public List<CheatEntry> Entries = new List<CheatEntry>();
        public List<CheatTranslation> Cmd = new List<CheatTranslation>();
        public List<CheatTranslation> Bash = new List<CheatTranslation>();
        public List<string> Commands = new List<string>();
        public string Error = "";

        static CheatSheetData current;

        public static string DefaultPath { get { return Path.Combine(Program.ExeDir, "cheatsheet.json"); } }

        public static CheatSheetData Current
        {
            get { if (current == null) current = Load(DefaultPath); return current; }
        }

        public static void Reset() { current = null; }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : "";
        }

        static bool Bool(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v is bool && (bool)v;
        }

        static string[] Strings(Dictionary<string, object> d, string key)
        {
            object v;
            var l = new List<string>();
            if (d.TryGetValue(key, out v) && v is IEnumerable && !(v is string))
                foreach (object x in (IEnumerable)v) if (x != null) l.Add(x.ToString());
            return l.ToArray();
        }

        static IEnumerable<Dictionary<string, object>> Objects(Dictionary<string, object> d, string key)
        {
            object v;
            if (d.TryGetValue(key, out v) && v is IEnumerable && !(v is string))
                foreach (object x in (IEnumerable)v)
                {
                    var o = x as Dictionary<string, object>;
                    if (o != null) yield return o;
                }
        }

        public static CheatSheetData Load(string path)
        {
            var data = new CheatSheetData();
            try
            {
                if (!File.Exists(path)) { data.Error = "cheatsheet.json was not found next to PSTools.exe."; return data; }
                var root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null) { data.Error = "cheatsheet.json is not a JSON object."; return data; }
                foreach (var c in Objects(root, "categories"))
                    data.Categories.Add(new CheatCategory { Id = Str(c, "id"), Label = Str(c, "label"), Icon = Str(c, "icon") });
                foreach (var d in Objects(root, "entries"))
                {
                    var e = new CheatEntry
                    {
                        Id = Str(d, "id"), Category = Str(d, "category"), Title = Str(d, "title"), Command = Str(d, "command"), Explain = Str(d, "explain"),
                        Example = Str(d, "example"), ExampleOutput = Str(d, "exampleOutput"), RunIn = Str(d, "runIn"),
                        Aliases = Strings(d, "aliases"), Keywords = Strings(d, "keywords"), Related = Strings(d, "related"),
                        SafeToRun = Bool(d, "safeToRun"), ExpectError = Bool(d, "expectError"), Admin = Bool(d, "admin")
                    };
                    string v = Str(d, "minVersion");
                    if (v.Length > 0) e.MinVersion = v;
                    string r = Str(d, "risk");
                    if (r.Length > 0) e.Risk = r;
                    foreach (var p in Objects(d, "params")) e.Params.Add(new CheatParam { Name = Str(p, "name"), Text = Str(p, "text") });
                    object t;
                    var tpl = d.TryGetValue("actionTemplate", out t) ? t as Dictionary<string, object> : null;
                    if (tpl != null) e.Template = new CheatTemplate { When = Strings(tpl, "when"), Output = Str(tpl, "output").Length > 0 ? Str(tpl, "output") : "console", Command = Str(tpl, "command") };
                    if (e.Id.Length > 0 && e.Title.Length > 0 && e.Command.Length > 0) data.Entries.Add(e);
                }
                object tr;
                var trans = root.TryGetValue("translations", out tr) ? tr as Dictionary<string, object> : null;
                if (trans != null)
                {
                    object list;
                    if (trans.TryGetValue("cmd", out list)) data.Cmd = Translations(list);
                    if (trans.TryGetValue("bash", out list)) data.Bash = Translations(list);
                }
                data.Commands.AddRange(Strings(root, "commands"));
            }
            catch (Exception ex) { data.Error = "cheatsheet.json could not be read: " + ex.Message; }
            return data;
        }

        static List<CheatTranslation> Translations(object list)
        {
            var l = new List<CheatTranslation>();
            var seq = list as IEnumerable;
            if (seq == null || list is string) return l;
            foreach (object x in seq)
            {
                var o = x as Dictionary<string, object>;
                if (o == null) continue;
                var t = new CheatTranslation { From = Str(o, "from"), To = Str(o, "to"), Note = Str(o, "note") };
                if (t.From.Length > 0 && t.To.Length > 0) l.Add(t);
            }
            return l;
        }

        public CheatEntry Find(string id)
        {
            return Entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public string CategoryLabel(string id)
        {
            var c = Categories.FirstOrDefault(x => x.Id == id);
            return c == null ? id : c.Label;
        }

        public int CountIn(string category)
        {
            return Entries.Count(e => e.Category == category);
        }

        static string[] Tokens(string query)
        {
            return (query ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        static int Has(string hay, string token, int starts, int contains)
        {
            if (hay == null || hay.Length == 0) return 0;
            int i = hay.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return 0;
            return i == 0 ? starts : contains;
        }

        static int ScoreToken(CheatEntry e, string token)
        {
            int best = Has(e.Title, token, 100, 80);
            foreach (string a in e.Aliases) best = Math.Max(best, Has(a, token, 90, 70));
            best = Math.Max(best, Has(e.Command, token, 60, 55));
            foreach (string k in e.Keywords) best = Math.Max(best, Has(k, token, 65, 50));
            best = Math.Max(best, Has(e.Explain, token, 30, 25));
            best = Math.Max(best, Has(e.Id, token, 20, 15));
            return best;
        }

        public List<CheatEntry> Search(string query, string category)
        {
            string[] tokens = Tokens(query);
            var scored = new List<KeyValuePair<int, CheatEntry>>();
            int order = 0;
            foreach (var e in Entries)
            {
                order++;
                if (!string.IsNullOrEmpty(category) && e.Category != category) continue;
                int total = 0;
                bool all = true;
                foreach (string t in tokens)
                {
                    int s = ScoreToken(e, t);
                    if (s == 0) { all = false; break; }
                    total += s;
                }
                if (!all) continue;
                scored.Add(new KeyValuePair<int, CheatEntry>(total * 10000 - order, e));
            }
            if (tokens.Length == 0) return scored.OrderByDescending(x => x.Key).Select(x => x.Value).ToList();
            return scored.OrderByDescending(x => x.Key).Select(x => x.Value).ToList();
        }

        public List<CheatTranslation> SearchTranslations(List<CheatTranslation> source, string query)
        {
            string[] tokens = Tokens(query);
            if (tokens.Length == 0) return source.ToList();
            var hits = new List<KeyValuePair<int, CheatTranslation>>();
            int order = 0;
            foreach (var t in source)
            {
                order++;
                int total = 0;
                bool all = true;
                foreach (string tok in tokens)
                {
                    int s = Math.Max(Math.Max(Has(t.From, tok, 100, 80), Has(t.To, tok, 60, 50)), Has(t.Note, tok, 20, 15));
                    if (s == 0) { all = false; break; }
                    total += s;
                }
                if (all) hits.Add(new KeyValuePair<int, CheatTranslation>(total * 10000 - order, t));
            }
            return hits.OrderByDescending(x => x.Key).Select(x => x.Value).ToList();
        }

        public static string MenuFor(string category)
        {
            switch (category)
            {
                case "files": return "Inspect";
                case "search": return "Search";
                case "nav": return "Open";
                case "clipboard": return "Copy";
                case "archive": return "Archive";
                case "git": return "Git";
                case "env": return "Dev";
                case "sysinfo":
                case "disks":
                case "perm":
                case "events": return "Inspect";
                case "process":
                case "services":
                case "network":
                case "registry":
                case "tasks":
                case "security":
                case "apps":
                case "remoting": return "System";
                default: return MenuTree.MyActions;
            }
        }

        public static string[] Context(CheatEntry e)
        {
            return e.Template != null && e.Template.When.Length > 0 ? e.Template.When : new[] { "folder", "background" };
        }
    }
}
