using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PSTools
{
    static class TestSink
    {
        public static bool Active;
        public static string ClipboardIn = "";
        public static List<string> Clips = new List<string>();
        public static List<string> Toasts = new List<string>();
        public static List<string> Dialogs = new List<string>();
        public static List<string> Tables = new List<string>();

        public static void Reset()
        {
            Clips.Clear();
            Toasts.Clear();
            Dialogs.Clear();
            Tables.Clear();
        }

        public static string Render(Result r)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(r.Summary)) sb.AppendLine(r.Summary);
            if (r.Columns != null) sb.AppendLine(string.Join("\t", r.Columns));
            if (r.Rows != null)
                foreach (var row in r.Rows) sb.AppendLine(string.Join("\t", row));
            return sb.ToString();
        }

        public static string Collect()
        {
            var sb = new StringBuilder();
            Section(sb, "clipboard", Clips);
            Section(sb, "table", Tables);
            Section(sb, "toast", Toasts.Select(t => t.EndsWith("...") ? "<truncated>" : t).ToList());
            Section(sb, "dialog", Dialogs);
            return sb.ToString();
        }

        static void Section(StringBuilder sb, string name, List<string> items)
        {
            foreach (var item in items)
            {
                sb.AppendLine("[" + name + "]");
                sb.AppendLine(item.TrimEnd());
            }
        }
    }

    static class TestTools
    {
        public static string Option(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        public static bool Flag(string[] args, string name)
        {
            return args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }

        static void Emit(string[] args, string text)
        {
            string file = Option(args, "--out");
            if (file != null) File.WriteAllText(file, text, new UTF8Encoding(false));
            else Console.Write(text);
        }

        public static int ListActions(string[] args)
        {
            var sb = new StringBuilder();
            bool json = Flag(args, "--json");
            if (Flag(args, "--legacy"))
            {
                foreach (var a in Actions.All.OrderBy(a => a.Id, StringComparer.Ordinal)) sb.AppendLine(a.Id);
                Emit(args, sb.ToString());
                return 0;
            }
            var all = ActionCatalog.Current.All.OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
            if (json)
            {
                var list = new List<Dictionary<string, object>>();
                foreach (var a in all)
                    list.Add(new Dictionary<string, object>
                    {
                        { "id", a.Id }, { "aliases", a.Aliases.ToArray() }, { "title", a.Title }, { "menu", a.Menu }, { "order", a.Order },
                        { "when", SpecRules.ContextNames(a.When).ToArray() }, { "matchExt", a.MatchExt.ToArray() }, { "requires", a.Requires.ToArray() },
                        { "shell", a.Shell.ToString().ToLowerInvariant() }, { "output", a.Output.ToString().ToLowerInvariant() },
                        { "risk", a.Risk.ToString().ToLowerInvariant() }, { "admin", a.Admin }, { "multi", a.Multi.ToString().ToLowerInvariant() },
                        { "undo", a.Undo.ToString().ToLowerInvariant() }, { "extended", a.Extended }, { "keywords", a.Keywords.ToArray() },
                        { "description", a.Description }, { "source", a.Source.ToString().ToLowerInvariant() }, { "sourceFile", a.SourceFile },
                        { "overrides", a.Overrides }, { "popupHidden", a.PopupHidden }, { "appOnly", a.AppOnly }, { "hidden", a.UserHidden }
                    });
                sb.AppendLine(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(new Dictionary<string, object>
                {
                    { "count", list.Count },
                    { "problems", ActionCatalog.Current.Problems.Select(p => p.ToString()).ToArray() },
                    { "actions", list }
                }));
            }
            else
                foreach (var a in all) sb.AppendLine(a.Id);
            Emit(args, sb.ToString());
            return 0;
        }

        static string Normalize(string text, string root)
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            if (!string.IsNullOrEmpty(root))
            {
                text = text.Replace(root, "<FX>");
                text = text.Replace(root.Replace('\\', '/'), "<FX>");
                text = text.Replace(root.Replace("\\", "\\\\"), "<FX>");
            }
            var lines = text.Split('\n').Select(l => l.TrimEnd()).ToList();
            return string.Join("\n", lines).TrimEnd('\n') + "\n";
        }

        static List<string> Strings(object o)
        {
            var l = new List<string>();
            var arr = o as IEnumerable;
            if (arr == null || o is string) return l;
            foreach (var x in arr) l.Add(Convert.ToString(x));
            return l;
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : null;
        }

        static void CopyTree(string from, string to)
        {
            if (to.Length > 230) return;
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from))
            {
                string dest = Path.Combine(to, Path.GetFileName(f));
                if (dest.Length < 250) File.Copy(f, dest, true);
            }
            foreach (var d in Directory.GetDirectories(from))
                CopyTree(d, Path.Combine(to, Path.GetFileName(d)));
        }

        class Outcome
        {
            public string Name, Id, Status, Detail;
            public long Ms;
        }

        public static int RunActionTests(string[] args)
        {
            string fixtures = Option(args, "--test-actions");
            string reportPath = Option(args, "--report");
            string specPath = Option(args, "--spec");
            string goldens = Option(args, "--goldens");
            bool update = Flag(args, "--update-goldens");
            var outcomes = new List<Outcome>();

            string dataDir = Path.Combine(Path.GetTempPath(), "pstools-test-data-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataDir);
            Journal.DirOverride = dataDir;
            TestSink.Active = true;
            Program.Quiet = true;

            if (fixtures == null || !Directory.Exists(fixtures) || specPath == null || !File.Exists(specPath) || goldens == null)
            {
                Console.WriteLine("Usage: --test-actions <fixturesDir> --spec <file> --goldens <dir> [--report <file>] [--update-goldens]");
                return 2;
            }
            Directory.CreateDirectory(goldens);

            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var specs = ((IEnumerable)ser.DeserializeObject(File.ReadAllText(specPath, Encoding.UTF8))).Cast<Dictionary<string, object>>().ToList();

            foreach (var spec in specs)
            {
                var o = new Outcome { Name = Str(spec, "name"), Id = Str(spec, "id") };
                var sw = Stopwatch.StartNew();
                string work = Path.Combine(Path.GetTempPath(), "pstools-test-" + Guid.NewGuid().ToString("N"));
                try
                {
                    string skip = Str(spec, "skipUnless");
                    if (skip == "git" && !(Fs.Which("git") != null)) { o.Status = "skip"; o.Detail = "git not found"; continue; }

                    string fixture = Str(spec, "fixture");
                    string src = Path.Combine(fixtures, fixture);
                    if (!Directory.Exists(src)) { o.Status = "skip"; o.Detail = "fixture missing: " + fixture; continue; }
                    CopyTree(src, Path.Combine(work, fixture));

                    var catalogSpec = ActionCatalog.Current.Find(o.Id);
                    var action = catalogSpec == null ? null : catalogSpec.Legacy;
                    if (action == null) { o.Status = "fail"; o.Detail = catalogSpec == null ? "unknown action id" : "action has no built-in implementation to run"; continue; }

                    var targets = Strings(spec.ContainsKey("targets") ? spec["targets"] : null)
                        .Select(t => t.Length == 0 ? Path.Combine(work, fixture) : Path.Combine(work, fixture, t.Replace('/', '\\')))
                        .ToArray();
                    if (targets.Length == 0) targets = new[] { Path.Combine(work, fixture) };

                    TestSink.Reset();
                    TestSink.ClipboardIn = Str(spec, "clipboard") ?? "";
                    Journal.Wrote = false;
                    Runner.Execute(action, action.Multi ? targets : new[] { targets[0] }, null);
                    string actual = Normalize(TestSink.Collect(), work);

                    var problems = new List<string>();
                    bool compareGolden = !spec.ContainsKey("golden") || Convert.ToBoolean(spec["golden"]);
                    if (compareGolden)
                    {
                        string gpath = Path.Combine(goldens, o.Name + ".txt");
                        if (update) File.WriteAllText(gpath, actual, new UTF8Encoding(false));
                        else if (!File.Exists(gpath)) problems.Add("golden missing: " + o.Name + ".txt");
                        else
                        {
                            string expected = Normalize(File.ReadAllText(gpath, Encoding.UTF8), null);
                            if (expected != actual) problems.Add("output differs from golden\n--- expected\n" + expected + "--- actual\n" + actual);
                        }
                    }

                    var files = Strings(spec.ContainsKey("expectFiles") ? spec["expectFiles"] : null);
                    foreach (var rel in files)
                        if (!File.Exists(Path.Combine(work, fixture, rel.Replace('/', '\\'))) && !Directory.Exists(Path.Combine(work, fixture, rel.Replace('/', '\\'))))
                            problems.Add("expected path missing after run: " + rel);

                    if (spec.ContainsKey("undo") && Convert.ToBoolean(spec["undo"]))
                    {
                        string undoText = Journal.Undo();
                        foreach (var rel in files)
                            if (File.Exists(Path.Combine(work, fixture, rel.Replace('/', '\\'))) || Directory.Exists(Path.Combine(work, fixture, rel.Replace('/', '\\'))))
                                problems.Add("path still present after undo: " + rel + " (" + undoText + ")");
                    }

                    o.Status = problems.Count == 0 ? "pass" : "fail";
                    o.Detail = string.Join("\n", problems);
                }
                catch (Exception ex)
                {
                    o.Status = "fail";
                    o.Detail = ex.GetType().Name + ": " + ex.Message;
                }
                finally
                {
                    sw.Stop();
                    o.Ms = sw.ElapsedMilliseconds;
                    if (o.Status == null) o.Status = "skip";
                    outcomes.Add(o);
                    try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch { }
                }
            }

            try { Directory.Delete(dataDir, true); } catch { }

            var report = outcomes.Select(x => new Dictionary<string, object>
            {
                { "name", x.Name }, { "id", x.Id }, { "status", x.Status }, { "ms", x.Ms }, { "detail", x.Detail ?? "" }
            }).ToList();
            if (reportPath != null) File.WriteAllText(reportPath, ser.Serialize(report), new UTF8Encoding(false));

            int pass = outcomes.Count(x => x.Status == "pass"), fail = outcomes.Count(x => x.Status == "fail"), skipped = outcomes.Count(x => x.Status == "skip");
            foreach (var x in outcomes)
            {
                Console.WriteLine(string.Format("{0,-5} {1,-34} {2,5} ms", x.Status.ToUpperInvariant(), x.Name, x.Ms));
                if (x.Status == "fail") Console.WriteLine("      " + (x.Detail ?? "").Replace("\n", "\n      "));
            }
            Console.WriteLine(string.Format("{0} passed, {1} failed, {2} skipped{3}", pass, fail, skipped, update ? " (goldens rewritten)" : ""));
            return fail == 0 ? 0 : 1;
        }
    }
}
