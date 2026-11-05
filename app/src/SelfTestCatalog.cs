using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PSTools
{
    static partial class SelfTest
    {
        static string FindTestFile(string name)
        {
            string dir = Program.ExeDir;
            for (int i = 0; i < 6 && dir != null; i++)
            {
                foreach (string rel in new[] { Path.Combine("tests", name), Path.Combine("app", "tests", name) })
                {
                    string p = Path.Combine(dir, rel);
                    if (File.Exists(p)) return p;
                }
                var parent = Directory.GetParent(dir);
                dir = parent == null ? null : parent.FullName;
            }
            return null;
        }

        static void CheckCatalog()
        {
            var cat = ActionCatalog.Current;
            var problems = new List<string>();
            foreach (var p in cat.ShippedProblems) problems.Add(p.ToString());
            Report("Catalog: shipped, library and built-in entries are valid", problems, cat.All.Count + " actions");

            problems = new List<string>();
            foreach (var g in cat.All.GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)) problems.Add("duplicate id " + g.Key);
            foreach (var a in cat.All)
                foreach (var alias in a.Aliases)
                    if (cat.Find(alias) != a) problems.Add("alias " + alias + " of " + a.Id + " resolves to " + (cat.Find(alias) == null ? "nothing" : cat.Find(alias).Id));
            Report("Catalog: ids unique, every alias resolves back", problems, "");

            problems = new List<string>();
            foreach (var a in cat.All.Where(x => x.Source == SpecSource.Builtin || x.Source == SpecSource.Shipped))
            {
                if (a.Title.Length > SpecRules.MaxTitleStrict) problems.Add(a.Id + " title too long");
                if (a.Description.Length < 8) problems.Add(a.Id + " description too short");
                if (a.Source == SpecSource.Shipped && a.Shell != ActionShell.Builtin && a.Command.IndexOf("Invoke-Expression", StringComparison.OrdinalIgnoreCase) >= 0) problems.Add(a.Id + " uses Invoke-Expression");
            }
            Report("Catalog: titles and descriptions of shipped entries", problems, "");

            string json = File.Exists(Path.Combine(Program.ExeDir, "commands.json")) ? File.ReadAllText(Path.Combine(Program.ExeDir, "commands.json"), Encoding.UTF8) : null;
            problems = new List<string>();
            if (json == null) problems.Add("commands.json not found next to the exe");
            else
            {
                int expected = ((System.Collections.IEnumerable)((Dictionary<string, object>)new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json))["commands"]).Cast<object>().Count();
                int got = cat.All.Count(a => a.Source == SpecSource.Library);
                if (got != expected) problems.Add("library has " + expected + " commands but the catalog loaded " + got);
                foreach (var a in cat.All.Where(x => x.Source == SpecSource.Library))
                    if (!a.PopupHidden || a.Shell != ActionShell.PowerShell || !a.Menu.StartsWith("Library/")) problems.Add(a.Id + " not converted as specified");
            }
            Report("Catalog: commands.json converted (hidden from popup, Library/<category>)", problems, "");

            var fake = new List<Action>
            {
                new Action { Id = "recipe-demo", Group = "Recipes", Label = "Demo recipe", Hint = "Runs two steps.", Targets = Target.Folder, Tier = Tier.Confirm, Undoable = true, Run = (p, c) => null }
            };
            var rc = ActionCatalog.Load(new CatalogInput { Builtins = fake });
            problems = new List<string>();
            var rs = rc.Find("recipe-demo");
            if (rs == null || rs.Id != "recipe.demo" || rs.Menu != "Recipes" || rs.Source != SpecSource.Recipe) problems.Add("recipe not converted to recipe.demo under Recipes");
            Report("Catalog: recipes become Recipes/<name> nodes with the old alias", problems, "");
        }

        static void CheckV3Aliases()
        {
            var problems = new List<string>();
            string file = FindTestFile("v3-ids.txt");
            var cat = ActionCatalog.Current;
            if (file == null) { Line("WARN", "Catalog: every v3 action id resolves", "tests\\v3-ids.txt not found next to this build"); return; }
            var ids = File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            foreach (var id in ids)
            {
                var s = cat.Find(id);
                if (s == null) problems.Add(id + " does not resolve");
                else if (s.Legacy == null) problems.Add(id + " has no built-in implementation");
                else if (s.Id.StartsWith("legacy.")) problems.Add(id + " has no assigned v4 id");
            }
            if (cat.LegacyFallbacks > 0) problems.Add(cat.LegacyFallbacks + " built-ins fell back to legacy.* ids");
            if (cat.All.Count < ids.Count) problems.Add("catalog (" + cat.All.Count + ") is smaller than the v3 list (" + ids.Count + ")");
            Report("Catalog: every v3 action id resolves to a v4 id", problems, ids.Count + " ids");
        }

        static string ParsePsLiteral(string literal, out bool ok)
        {
            ok = false;
            if (literal.Length < 2 || literal[0] != '\'' || literal[literal.Length - 1] != '\'') return null;
            string inner = literal.Substring(1, literal.Length - 2);
            char[] quotes = { '\'', '\u2018', '\u2019', '\u201A', '\u201B' };
            var sb = new StringBuilder();
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (Array.IndexOf(quotes, c) >= 0)
                {
                    if (i + 1 < inner.Length && inner[i + 1] == c) { sb.Append(c); i++; }
                    else return null;
                }
                else sb.Append(c);
            }
            ok = true;
            return sb.ToString();
        }

        static void CheckPlaceholders()
        {
            var values = new[]
            {
                @"C:\proj\plain.txt", @"C:\My Docs\a b.txt", @"C:\x\O'Brien.txt", @"C:\x\100% done.txt", @"C:\x\a&b.txt", @"C:\x\a^b.txt",
                @"C:\x\wow!.txt", @"C:\x\$env.txt", "C:\\x\\back`tick.txt", "C:\\x\\\u00DCn\u00EFc\u00F8d\u00E9 \u65E5\u672C.txt", @"C:\x\trailing.dot.", @"C:\x\[draft] v1.txt",
                "C:\\x\\it\u2019s.txt", @"C:\x\" + new string('a', 230) + ".txt", @"C:\"
            };
            var problems = new List<string>();
            foreach (var v in values)
            {
                var ctx = new PlaceholderContext { Paths = new[] { v }, Clip = "clip text", Now = new DateTime(2026, 1, 15, 10, 30, 5) };
                foreach (var shell in new[] { ActionShell.PowerShell, ActionShell.Pwsh })
                {
                    string outp = Placeholders.Expand("{path}", shell, ctx);
                    bool ok;
                    string back = ParsePsLiteral(outp, out ok);
                    if (!ok || back != v) problems.Add("PS {path} does not round-trip for " + (v.Length > 40 ? v.Substring(0, 40) + "..." : v));
                    string legacy = Placeholders.Expand("'{path}'", shell, ctx);
                    bool ok2;
                    string back2 = ParsePsLiteral(legacy, out ok2);
                    if (!ok2 || back2 != v) problems.Add("PS '{path}' back-compat does not round-trip for " + (v.Length > 40 ? v.Substring(0, 40) + "..." : v));
                    if (legacy != outp) problems.Add("PS '{path}' and {path} must give the same literal");
                }
                string cmd = Placeholders.Expand("{path}", ActionShell.Cmd, ctx);
                string expectedCmd = "\"" + v.Replace("%", "%%") + (v.EndsWith("\\") ? "\\" : "") + "\"";
                if (cmd != expectedCmd) problems.Add("cmd {path} gave " + cmd + " for " + v);
            }

            try { Placeholders.Expand("{path}", ActionShell.Cmd, new PlaceholderContext { Paths = new[] { "C:\\x\\a\"b.txt" } }); problems.Add("cmd must refuse a quote character"); }
            catch (PlaceholderException ex) { if (!ex.Message.Contains("Use PowerShell")) problems.Add("cmd refusal message changed"); }

            var c1 = new PlaceholderContext { Paths = new[] { @"C:\proj\src\main.tar.gz", @"C:\proj\src\b.txt" }, Clip = "clip text", Now = new DateTime(2026, 1, 15, 10, 30, 5), GitRoot = d => @"C:\proj" };
            c1.Inputs["depth"] = "3";
            string[,] table =
            {
                { "{name}", "'main.tar.gz'" }, { "{stem}", "'main.tar'" }, { "{ext}", "'.gz'" }, { "{parent}", "'C:\\proj\\src'" },
                { "{dir}", "'C:\\proj\\src'" }, { "{clip}", "'clip text'" }, { "{date}", "'2026-01-15'" }, { "{time}", "'10-30-05'" },
                { "{now}", "'2026-01-15_10-30-05'" }, { "{gitroot}", "'C:\\proj'" }, { "{input:depth}", "'3'" },
                { "{paths}", "@('C:\\proj\\src\\main.tar.gz','C:\\proj\\src\\b.txt')" }, { "'{paths}'", "'C:\\proj\\src\\main.tar.gz','C:\\proj\\src\\b.txt'" }
            };
            for (int i = 0; i < table.GetLength(0); i++)
            {
                string got = Placeholders.Expand(table[i, 0], ActionShell.PowerShell, c1);
                if (got != table[i, 1]) problems.Add(table[i, 0] + " => " + got + " (expected " + table[i, 1] + ")");
            }
            string cmdList = Placeholders.Expand("{paths}", ActionShell.Cmd, c1);
            if (cmdList != "\"C:\\proj\\src\\main.tar.gz\" \"C:\\proj\\src\\b.txt\"") problems.Add("cmd {paths} gave " + cmdList);
            string untouched = Placeholders.Expand("$x = @{ a = 1 }; {1} {0:N2}", ActionShell.PowerShell, c1);
            if (untouched != "$x = @{ a = 1 }; {1} {0:N2}") problems.Add("unknown braces must be left alone");
            string longClip = Placeholders.Expand("{clip}", ActionShell.PowerShell, new PlaceholderContext { Clip = new string('x', 200000) });
            if (longClip.Length != Placeholders.MaxClip + 2) problems.Add("clip must be capped at 100 KB");
            try { Placeholders.Expand("{input:missing}", ActionShell.PowerShell, c1); problems.Add("missing input must throw"); }
            catch (PlaceholderException) { }
            Report("Placeholders: quoting matrix (PowerShell, back-compat, cmd, tokens)", problems, values.Length + " values x 3 shells");
        }

        static void CheckHeaders()
        {
            var problems = new List<string>();
            var h = ScriptHeader.Parse("<#\n@id        user.copy.git-branch\n@title     Git branch name\n@menu      Copy/Git\n@when      folder, background\n@description Copies the current git branch\n  of this folder.\n#>\ngit -C {dir} rev-parse --abbrev-ref HEAD\n", false);
            if (!h.HasHeader || h.Keys["id"] != "user.copy.git-branch" || h.Keys["menu"] != "Copy/Git") problems.Add("ps1 header keys not read");
            if (h.Keys["description"] != "Copies the current git branch of this folder.") problems.Add("multi-line description not joined: " + h.Keys["description"]);
            if (h.Body != "git -C {dir} rev-parse --abbrev-ref HEAD") problems.Add("ps1 body wrong: " + h.Body);
            var none = ScriptHeader.Parse("Get-Date\n", false);
            if (none.HasHeader || none.Body.Trim() != "Get-Date") problems.Add("headerless script must keep its body");
            var ordinary = ScriptHeader.Parse("<# just a comment, no keys #>\nGet-Date\n", false);
            if (ordinary.HasHeader) problems.Add("comment without @keys is not a header");
            var cmd = ScriptHeader.Parse("@echo off\n:: @title  Show IP\n:: @risk   safe\n:: @output console\nipconfig\n", true);
            if (!cmd.HasHeader || cmd.Keys["title"] != "Show IP" || cmd.Keys["risk"] != "safe" || cmd.Body.Trim() != "@echo off\nipconfig") problems.Add("cmd header not read: " + cmd.Body);
            var bom = ScriptHeader.Parse("\uFEFF<#\n@title With BOM\n#>\nx", false);
            if (!bom.HasHeader || bom.Keys["title"] != "With BOM") problems.Add("BOM header not read");
            var open = ScriptHeader.Parse("<#\n@title Broken\nx", false);
            if (open.HasHeader || open.Warnings.Count == 0) problems.Add("unclosed header must warn");
            Report("Script headers: ps1 and cmd parsing", problems, "");
        }

        static void CheckUserFolder()
        {
            string root = Path.Combine(Path.GetTempPath(), "pstools-actions-" + Guid.NewGuid().ToString("N"));
            var problems = new List<string>();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Copy", "Tree"));
                File.WriteAllText(Path.Combine(root, "Copy", "Tree", "Folders only.ps1"), "Get-ChildItem -Directory -LiteralPath {dir} | Select-Object -ExpandProperty Name\n");
                File.WriteAllText(Path.Combine(root, "headed.ps1"), "<#\n@id user.headed\n@title Headed\n@menu Tools/Mine\n@when file\n@output toast\n@risk safe\n@description A script with a header.\n#>\nGet-Date\n");
                File.WriteAllText(Path.Combine(root, "show.cmd"), ":: @title Show IP\n:: @risk safe\n:: @description Runs ipconfig.\nipconfig\n");
                File.WriteAllText(Path.Combine(root, "bad.ps1"), "<#\n@title Bad\n@risk explosive\n#>\nGet-Date\n");
                File.WriteAllText(Path.Combine(root, "broken.json"), "{ not json");
                File.WriteAllText(Path.Combine(root, "_ignored.ps1"), "Get-Date");
                File.WriteAllText(Path.Combine(root, "dup.json"), "{\"actions\":[{\"id\":\"git.copy.branch\",\"title\":\"My branch\",\"menu\":\"Git\",\"when\":[\"folder\"],\"shell\":\"powershell\",\"command\":\"git branch --show-current\",\"output\":\"clipboard\",\"risk\":\"safe\",\"description\":\"Overrides the shipped one.\"},{\"id\":\"BadId\",\"title\":\"x\",\"menu\":\"\",\"when\":[\"file\"],\"shell\":\"cmd\",\"command\":\"dir\",\"output\":\"console\",\"risk\":\"safe\",\"description\":\"bad id\"}]}");
                var cat = ActionCatalog.Load(new CatalogInput { Builtins = Actions.All, ShippedPath = Path.Combine(Program.ExeDir, "actions.json"), UserDir = root });

                var tree = cat.Find("user.copy-tree-folders-only");
                if (tree == null) problems.Add("folder structure id not derived");
                else
                {
                    if (tree.Menu != "Copy/Tree" || tree.Title != "Folders only") problems.Add("folder path must become the menu: " + tree.Menu + " / " + tree.Title);
                    if (tree.Risk != Risk.Modifies || tree.Output != ActionOutput.Console) problems.Add("headerless defaults must be modifies + console");
                }
                var headed = cat.Find("user.headed");
                if (headed == null || headed.Menu != "Tools/Mine" || headed.When != ContextKind.File || headed.Risk != Risk.Safe) problems.Add("headed script not read");
                var show = cat.Find("user.show");
                if (show == null || show.Shell != ActionShell.Cmd || show.Title != "Show IP") problems.Add("cmd script not read");
                if (cat.Find("user.bad") != null) problems.Add("invalid risk must be skipped");
                if (!cat.Problems.Any(p => p.File.EndsWith("bad.ps1"))) problems.Add("bad.ps1 must be listed in Problems");
                if (!cat.Problems.Any(p => p.File.EndsWith("broken.json"))) problems.Add("broken.json must be listed in Problems");
                if (!cat.Problems.Any(p => p.Reason.Contains("BadId"))) problems.Add("invalid id must be listed in Problems");
                if (cat.Find("user.ignored") != null) problems.Add("files starting with _ must be ignored");
                var branch = cat.Find("git.copy.branch");
                if (branch == null || branch.Source != SpecSource.User || branch.Title != "My branch" || !branch.Overrides.StartsWith("Shipped")) problems.Add("user file must override the shipped entry and say so");
                if (cat.All.Count(a => a.Id == "git.copy.branch") != 1) problems.Add("overridden entry must appear once");
                if (!cat.Problems.All(p => p.Line >= 0)) problems.Add("problem lines must not be negative");
            }
            catch (Exception ex) { problems.Add(ex.GetType().Name + ": " + ex.Message); }
            finally { try { Directory.Delete(root, true); } catch { } }
            Report("Catalog: user folder (structure = menu, headers, problems, override)", problems, "");
        }

        static void CheckOverrides()
        {
            var problems = new List<string>();
            var ov = CatalogOverrides.Parse("{\"hidden\":[\"copy-name\",\"copy.size\"],\"overrides\":{\"copy.tree.ascii\":{\"title\":\"My tree\",\"menu\":\"Copy/Mine\",\"order\":7},\"copy.hash.md5\":{\"title\":\"\"}}}");
            var cat = ActionCatalog.Load(new CatalogInput { Builtins = Actions.All, Overrides = ov });
            var name = cat.Find("copy.names.selected");
            if (name == null || !name.UserHidden) problems.Add("hidden by v3 alias not applied");
            var size = cat.Find("copy.size");
            if (size == null || !size.UserHidden) problems.Add("hidden by id not applied");
            var tree = cat.Find("copy.tree.ascii");
            if (tree == null || tree.Title != "My tree" || tree.Menu != "Copy/Mine" || tree.Order != 7) problems.Add("title/menu/order override not applied");
            var md5 = cat.Find("copy.hash.md5");
            if (md5 == null || md5.Title != "MD5") problems.Add("an invalid override (empty title) must be ignored");
            if (!cat.Problems.Any(p => p.Reason.Contains("copy.hash.md5"))) problems.Add("invalid override must be listed in Problems");
            var empty = CatalogOverrides.Parse("");
            if (empty.Hidden.Count != 0) problems.Add("empty settings must parse");
            Report("Catalog: hidden and overrides from settings", problems, "");
        }

        static void CheckCatalogSpeed()
        {
            var input = CatalogInput.Default();
            ActionCatalog.Load(input);
            var sw = Stopwatch.StartNew();
            var cat = ActionCatalog.Load(input);
            sw.Stop();
            if (sw.ElapsedMilliseconds > 500) Line("FAIL", "Catalog: load time", sw.ElapsedMilliseconds + " ms for " + cat.All.Count + " actions (limit 500 ms)");
            else if (sw.ElapsedMilliseconds > 150) Line("WARN", "Catalog: load time", sw.ElapsedMilliseconds + " ms for " + cat.All.Count + " actions");
            else Line("PASS", "Catalog: load time", sw.ElapsedMilliseconds + " ms for " + cat.All.Count + " actions");
        }

        static MenuQuery Query(ContextKind ctx, string[] paths, Func<string, bool> tools, params string[] favorites)
        {
            return new MenuQuery
            {
                Context = ctx, Paths = paths, Catalog = ActionCatalog.Current, Favorites = new List<string>(favorites), HasTool = tools,
                IsDirectory = p => !System.IO.Path.HasExtension(p)
            };
        }

        static void CheckMenuTree()
        {
            var problems = new List<string>();
            var allTools = (Func<string, bool>)(t => true);
            var noTools = (Func<string, bool>)(t => false);

            var folder = MenuTree.Build(Query(ContextKind.Folder, new[] { @"C:\x\src" }, allTools, "copy.path.quoted", "archive.zip-smart"));
            var names = folder.Select(n => n.Kind + ":" + n.Title).ToList();
            if (names.Count == 0 || names[0] != "Section:Favorites") problems.Add("Favorites must be the first node, got " + (names.Count == 0 ? "nothing" : names[0]));
            int copyAt = names.IndexOf("Folder:Copy"), pasteAt = names.IndexOf("Folder:Paste & New"), searchAt = names.IndexOf("Folder:Search");
            if (copyAt < 0 || pasteAt < copyAt || searchAt < pasteAt) problems.Add("canonical folder order broken: " + string.Join(", ", names.ToArray()));
            if (!names.Contains("Action:Rename: Batch rename...")) problems.Add("a folder with one action must be inlined as 'Folder: Action'");
            var allIds = new List<string>();
            Collect(folder.Where(n => n.Kind != NodeKind.Section).ToList(), allIds);
            if (allIds.GroupBy(x => x).Any(g => g.Count() > 1)) problems.Add("an action appears twice in the folder part: " + string.Join(", ", allIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToArray()));
            if (allIds.Any(id => id.StartsWith("lib."))) problems.Add("library entries must stay out of the tree unless favorited");
            if (allIds.Any(id => id.StartsWith("system.dism"))) problems.Add("extended or app-only actions leaked into the tree");

            var withLib = MenuTree.Build(Query(ContextKind.Folder, new[] { @"C:\x\src" }, allTools, "lib.flatten-copy"));
            var libIds = new List<string>();
            Collect(withLib, libIds);
            if (!libIds.Contains("lib.flatten-copy")) problems.Add("a favorited library entry must appear");

            var bg = MenuTree.Build(Query(ContextKind.Background, new string[0], allTools));
            var bgIds = new List<string>();
            Collect(bg, bgIds);
            if (!bgIds.Contains("new.paste-image") || bgIds.Contains("archive.extract")) problems.Add("background context filtering is wrong");

            var file = MenuTree.Build(Query(ContextKind.File, new[] { @"C:\x\a.txt" }, allTools));
            var fileIds = new List<string>();
            Collect(file, fileIds);
            if (fileIds.Contains("image.png")) problems.Add("image actions must not apply to a .txt file");
            if (!fileIds.Contains("copy.contents")) problems.Add("copy.contents must apply to a file");
            var pic = MenuTree.Build(Query(ContextKind.File, new[] { @"C:\x\a.png" }, allTools));
            if (!pic.Any(n => n.Kind == NodeKind.Section && n.Title == "Suggested" && n.Children.Any(c => c.Spec.Id == "image.png"))) problems.Add("images must get a Suggested section");

            var multi = MenuTree.Build(Query(ContextKind.File, new[] { @"C:\x\a.txt", @"C:\x\b.txt" }, allTools));
            var multiIds = new List<string>();
            Collect(multi, multiIds);
            if (multiIds.Contains("copy.hash.verify")) problems.Add("single-only actions must hide when several items are selected");
            if (!multiIds.Contains("copy.names.selected")) problems.Add("multi-capable actions must stay");

            var gitless = new List<string>();
            Collect(MenuTree.Build(Query(ContextKind.Folder, new[] { @"C:\x\src" }, noTools)), gitless);
            if (gitless.Contains("git.copy.branch") || gitless.Contains("git.status.repos") || gitless.Contains("open.vscode")) problems.Add("actions that require a missing tool must be hidden");
            var gitful = new List<string>();
            Collect(MenuTree.Build(Query(ContextKind.Folder, new[] { @"C:\x\src" }, allTools)), gitful);
            if (!gitful.Contains("git.copy.branch")) problems.Add("git.copy.branch must show when git exists");

            var shift = Query(ContextKind.Background, new string[0], allTools);
            shift.Shift = false;
            var plain = new List<string>(); Collect(MenuTree.Build(shift), plain);
            shift.Shift = true;
            var shifted = new List<string>(); Collect(MenuTree.Build(shift), shifted);
            if (shifted.Count < plain.Count) problems.Add("Shift must never remove actions");

            Report("MenuTree: sections, order, inlining, filtering, requires, extended", problems, "");

            problems = new List<string>();
            var q = Query(ContextKind.Folder, new[] { @"C:\x\src" }, allTools);
            var ta = MenuTree.Search(q, "ta");
            if (ta.Count == 0 || ta[0].Spec.Id != "copy.tree.ascii") problems.Add("searching 'ta' must rank Tree (ASCII) first, got " + (ta.Count == 0 ? "nothing" : ta[0].Title));
            var cop = MenuTree.Search(q, "copy name");
            if (cop.Count == 0 || !cop.Take(3).Any(n => n.Spec.Id == "copy.names.selected" || n.Spec.Id == "copy.names.stem")) problems.Add("'copy name' must find the name actions near the top");
            if (MenuTree.Search(q, "zzzzqq").Count != 0) problems.Add("nonsense must match nothing");
            var lib = MenuTree.Search(q, "flatten");
            if (!lib.Any(n => n.Spec.Id == "lib.flatten-copy")) problems.Add("search must include library entries that the tree hides");
            var kw = MenuTree.Search(q, "ls");
            if (kw.Count == 0) problems.Add("keywords must be searched");
            Report("MenuTree: search ranking (initials, prefix, keywords, library)", problems, "");

            var swSearch = Stopwatch.StartNew();
            var sq = Query(ContextKind.Folder, new[] { @"C:\\x\\src" }, allTools);
            string[] typed = { "t", "ta", "tre", "tree", "tree a", "copy", "e", "pa", "zip", "git" };
            foreach (var t in typed) MenuTree.Search(sq, t);
            swSearch.Stop();
            double perKey = swSearch.Elapsed.TotalMilliseconds / typed.Length;
            string sdetail = perKey.ToString("0.0") + " ms per keystroke over " + ActionCatalog.Current.All.Count + " actions";
            if (perKey > 30) Line("FAIL", "MenuTree: search time", sdetail + " (limit 30 ms)");
            else Line("PASS", "MenuTree: search time", sdetail);

            var sw = Stopwatch.StartNew();
            int runs = 20;
            for (int i = 0; i < runs; i++) MenuTree.Build(Query(ContextKind.Folder, new[] { @"C:\x\src" }, allTools, "copy.path.quoted"));
            sw.Stop();
            double perBuild = sw.Elapsed.TotalMilliseconds / runs;
            int total = ActionCatalog.Current.All.Count;
            string detail = perBuild.ToString("0.0") + " ms per build, " + total + " actions in the catalog";
            if (perBuild > 50) Line("FAIL", "MenuTree: build time", detail + " (limit 50 ms)");
            else if (perBuild > 15) Line("WARN", "MenuTree: build time", detail + " (target 15 ms)");
            else Line("PASS", "MenuTree: build time", detail);
        }

        static void Collect(List<MenuNode> nodes, List<string> ids)
        {
            foreach (var n in nodes)
            {
                if (n.Kind == NodeKind.Action) ids.Add(n.Spec.Id);
                else Collect(n.Children, ids);
            }
        }

        static void CheckRegistryPlan()
        {
            var problems = new List<string>();
            var state = new SettingsData { Schema = SettingsData.SchemaVersion, Configured = true };
            foreach (var id in new[] { "copy.path.quoted", "image.png", "new.paste-image", "inspect.space-map", "archive.extract", "open.ps", "search.here", "new.paste-text", "new.download-clip", "copy.size", "copy.names.selected" })
                state.Favorites.Add(id);
            string dump = Shell.DumpMenu(state, "C:\\Tools\\PSTools.exe");
            string[] lines = dump.Replace("\r", "").Split('\n');
            foreach (string root in new[] { "Directory\\shell\\PSTools", "Directory\\Background\\shell\\PSTools", "Drive\\shell\\PSTools", "*\\shell\\PSTools" })
                if (!lines.Any(l => l == "HKCU\\Software\\Classes\\" + root)) problems.Add("root missing: " + root);
            if (!dump.Contains("menu-bg \"%V\"")) problems.Add("background must use menu-bg and pass %V");
            if (!dump.Contains("00power")) problems.Add("Power menu entry missing");
            if (dump.Split(new[] { "99open" }, StringSplitOptions.None).Length - 1 != 6) problems.Add("Open in PS Tools must be written for folder, background and drive only (3 roots x key and command)");
            foreach (string root in new[] { "Directory\\shell", "Directory\\Background\\shell", "Drive\\shell", "*\\shell" })
            {
                int favs = lines.Count(l => l.StartsWith("HKCU\\Software\\Classes\\" + root + "\\PSTools\\shell\\") && !l.EndsWith("\\command") && !l.EndsWith("00power") && !l.EndsWith("99open"));
                if (favs > Shell.MaxFavoritesPerContext) problems.Add(root + " has " + favs + " favorites, cap is " + Shell.MaxFavoritesPerContext);
            }
            if (!dump.Contains("image.png")) problems.Add("image favorite missing");
            int imageAt = Array.FindIndex(lines, l => l.Contains("\\shell\\PSTools\\shell\\1") && l.EndsWith("image.png"));
            if (imageAt >= 0 && !lines.Skip(imageAt).Take(6).Any(l => l.Contains("AppliesTo = System.FileExtension"))) problems.Add("image favorite must carry an AppliesTo filter");
            int multi = lines.Count(l => l.Trim() == "MultiSelectModel = Player");
            if (multi < 8) problems.Add("MultiSelectModel=Player must be written on the root and every verb");
            Report("Explorer menu: v4 registry layout (power menu, favorites, caps, filters)", problems, "");
        }

        static void CheckMigrationV4()
        {
            var problems = new List<string>();
            string file = FindTestFile("v3-settings-sample.txt");
            if (file == null) { Line("WARN", "Settings: v3 to v4 migration", "tests\\v3-settings-sample.txt not found next to this build"); return; }
            var d = SettingsData.Parse(File.ReadAllLines(file, Encoding.UTF8));
            if (d.Schema != 0) problems.Add("a v3 file must parse with schema 0");
            SettingsData.Migrate(d, Actions.All);
            bool changed = SettingsData.MigrateToV4(d);
            if (!changed || d.Schema != SettingsData.SchemaVersion) problems.Add("migration did not run");
            var expected = new[] { "copy.path.quoted", "archive.zip-smart", "inspect.space-map", "new.paste-image", "new.paste-text", "search.here", "copy.names.selected", "image.png", "lib.flatten-copy" };
            if (!d.Favorites.SequenceEqual(expected)) problems.Add("favorites were " + string.Join(", ", d.Favorites.ToArray()) + " but expected " + string.Join(", ", expected));
            if (d.Favorites.GroupBy(x => x).Any(g => g.Count() > 1)) problems.Add("favorites must be de-duplicated");
            var again = SettingsData.Parse(d.Serialize().Replace("\r", "").Split('\n'));
            if (again.Schema != SettingsData.SchemaVersion || !again.Favorites.SequenceEqual(d.Favorites)) problems.Add("schema 4 must round-trip through Serialize/Parse");
            if (SettingsData.MigrateToV4(again)) problems.Add("migration must be idempotent");
            if (!again.Used.Count.Equals(0) || again.Hidden.Count != 0) problems.Add("unexpected data appeared");
            d.NoteUsed("folder", "copy-name");
            d.NoteUsed("folder", "copy.tree.ascii");
            d.NoteUsed("folder", "copy.names.selected");
            if (!d.Used["folder"].SequenceEqual(new[] { "copy.names.selected", "copy.tree.ascii" })) problems.Add("recently used must be most-recent-first and de-duplicated: " + string.Join(", ", d.Used["folder"].ToArray()));
            Report("Settings: v3 pins become v4 favorites, schema 4 round-trips", problems, "");
        }

        static void CheckRiskScan()
        {
            var cases = new[]
            {
                new object[] { "Get-ChildItem -Recurse -Filter *.py | Select-Object Name, Length", Risk.Safe },
                new object[] { "Get-Process | Sort-Object CPU -Descending | Select-Object -First 5", Risk.Safe },
                new object[] { "echo \"Remove-Item is dangerous\"", Risk.Safe },
                new object[] { "Write-Host 'rm -rf /'", Risk.Safe },
                new object[] { "Set-Location C:\\x; Get-Date", Risk.Safe },
                new object[] { "$g = New-Guid; $g.ToString()", Risk.Safe },
                new object[] { "$o = New-Object System.Text.StringBuilder", Risk.Safe },
                new object[] { "Get-Content a.txt 2>&1", Risk.Safe },
                new object[] { "if ($a -gt 3) { 'big' }", Risk.Safe },
                new object[] { "git status", Risk.Safe },
                new object[] { "git log --oneline -n 5", Risk.Safe },
                new object[] { "winget search git", Risk.Safe },
                new object[] { "Remove-Item .\\x -Recurse -WhatIf", Risk.Safe },
                new object[] { "dir /s /b", Risk.Safe },
                new object[] { "Remove-Item C:\\temp\\a.txt", Risk.Destructive },
                new object[] { "rm -r -fo .\\build", Risk.Destructive },
                new object[] { "del /q *.tmp", Risk.Destructive },
                new object[] { "Get-ChildItem *.log | Remove-Item", Risk.Destructive },
                new object[] { "rd /s /q build", Risk.Destructive },
                new object[] { "Clear-Content .\\log.txt", Risk.Destructive },
                new object[] { "Format-Volume -DriveLetter E", Risk.Destructive },
                new object[] { "format E: /fs:ntfs", Risk.Destructive },
                new object[] { "Stop-Computer -Force", Risk.Destructive },
                new object[] { "shutdown /s /t 0", Risk.Destructive },
                new object[] { "Restart-Computer", Risk.Destructive },
                new object[] { "reg delete HKCU\\Software\\X /f", Risk.Destructive },
                new object[] { "Remove-ItemProperty -Path HKCU:\\Software\\X -Name Y", Risk.Destructive },
                new object[] { "bcdedit /set testsigning on", Risk.Destructive },
                new object[] { "cipher /w:C:\\", Risk.Destructive },
                new object[] { "vssadmin delete shadows /all", Risk.Destructive },
                new object[] { "takeown /f C:\\Windows\\x /r", Risk.Destructive },
                new object[] { "Set-ExecutionPolicy Unrestricted", Risk.Destructive },
                new object[] { "git clean -fdx", Risk.Destructive },
                new object[] { "git reset --hard HEAD~3", Risk.Destructive },
                new object[] { "git push --force origin main", Risk.Destructive },
                new object[] { "Move-Item a.txt b.txt -Force", Risk.Destructive },
                new object[] { "Copy-Item a b -Recurse -Force", Risk.Destructive },
                new object[] { "Move-Item a.txt b.txt", Risk.Modifies },
                new object[] { "Copy-Item a b", Risk.Modifies },
                new object[] { "New-Item -ItemType Directory x", Risk.Modifies },
                new object[] { "Set-Content a.txt 'hi'", Risk.Modifies },
                new object[] { "Rename-Item a.txt b.txt", Risk.Modifies },
                new object[] { "Get-Date > now.txt", Risk.Modifies },
                new object[] { "Get-Date >> log.txt", Risk.Modifies },
                new object[] { "Get-Process | Out-File procs.txt", Risk.Modifies },
                new object[] { "Expand-Archive a.zip .", Risk.Modifies },
                new object[] { "Compress-Archive -Path x -DestinationPath x.zip", Risk.Modifies },
                new object[] { "mkdir newdir", Risk.Modifies },
                new object[] { "robocopy a b /E", Risk.Modifies },
                new object[] { "git commit -m 'x'", Risk.Modifies },
                new object[] { "git pull", Risk.Modifies },
                new object[] { "npm install", Risk.Modifies },
                new object[] { "winget install Git.Git", Risk.Modifies },
                new object[] { "Start-Process notepad -Verb RunAs", Risk.Modifies }
            };
            var problems = new List<string>();
            foreach (var c in cases)
            {
                var r = RiskScan.Classify((string)c[0], ActionShell.PowerShell);
                if (r.Risk != (Risk)c[1]) problems.Add("'" + c[0] + "' was " + r.Risk + ", expected " + c[1]);
                else if (r.Risk != Risk.Safe && r.Reasons.Count == 0) problems.Add("'" + c[0] + "' has no reason");
            }
            var cmd = RiskScan.Classify("del /q *.tmp", ActionShell.Cmd);
            if (cmd.Risk != Risk.Destructive) problems.Add("cmd del must be destructive");
            Report("RiskScan: " + cases.Length + " commands classified", problems, "");
        }

        static void CheckRunTools()
        {
            var problems = new List<string>();
            if (RunEngine.StripAnsi("\u001b[31mred\u001b[0m plain \u001b[1;32mgreen\u001b[0m") != "red plain green") problems.Add("ANSI colour codes must be stripped");

            string text;
            var t = RunEngine.ParseTable("##PST-JSON##\n[{\"Name\":\"a.py\",\"Length\":\"12\"},{\"Name\":\"b.py\",\"Extra\":\"x\"}]", out text);
            if (t == null || t.Rows.Count != 2 || t.Columns.Length != 3) problems.Add("JSON table must have 2 rows and the union of 3 columns");
            else if (t.Rows[1][1] != "" || t.Rows[1][2] != "x") problems.Add("missing cells must be empty");
            var one = RunEngine.ParseTable("##PST-JSON##\n{\"FullName\":\"C:\\\\x\\\\a.txt\",\"Length\":\"1\"}", out text);
            if (one == null || one.Rows.Count != 1 || one.PathColumn != 0) problems.Add("a single object must become one row and FullName must be the path column");
            if (RunEngine.ParseTable("##PST-EMPTY##", out text) != null || text == null) problems.Add("empty marker must give a message");
            if (RunEngine.ParseTable("##PST-TEXT##\nhello\nworld", out text) != null || text != "hello\nworld") problems.Add("text marker must give plain text");
            if (RunEngine.ParseTable("just text", out text) != null || text != "just text") problems.Add("output without a marker must stay text");

            var script = RunEngine.BuildScript(new RunRequest { Script = "Get-Date", Dir = "C:\\it's", Paths = new[] { "C:\\a b.txt" } });
            if (!script.Contains("Set-Location -LiteralPath 'C:\\it''s'") || !script.Contains("$Paths = @('C:\\a b.txt')")) problems.Add("prelude must set location and $Paths with PowerShell quoting");
            if (!script.Contains("Get-Date") || !script.Contains("-is [System.Management.Automation.ErrorRecord]")) problems.Add("script must wrap the command and convert errors");

            string root = Path.Combine(Path.GetTempPath(), "pstools-hist-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "ConsoleHost_history.txt"), "Get-Date\nGet-ChildItem `\n  -Recurse\nGet-Date\n\nwinget list\n");
                var imported = RunHistory.ImportPsReadLine(root);
                if (imported.Count != 3) problems.Add("import must de-duplicate: got " + imported.Count);
                else if (imported[0] != "winget list" || imported[1] != "Get-Date" || !imported[2].Contains("Get-ChildItem") || !imported[2].Contains("-Recurse")) problems.Add("import must be newest first and join continuation lines: " + string.Join(" | ", imported.ToArray()));
                RunHistory.FileOverride = Path.Combine(root, "run-history.json");
                RunHistory.Reset();
                RunHistory.Add("Get-Date", "ps", "C:\\x", 0);
                RunHistory.Add("dir", "cmd", "C:\\x", 1);
                RunHistory.Add("Get-Date", "ps", "C:\\y", 0);
                RunHistory.Reset();
                var all = RunHistory.All;
                if (all.Count != 2 || all[0].Command != "Get-Date" || all[0].Dir != "C:\\y") problems.Add("history must persist, de-duplicate and put the newest first");
            }
            catch (Exception ex) { problems.Add(ex.GetType().Name + ": " + ex.Message); }
            finally
            {
                RunHistory.FileOverride = null;
                RunHistory.Reset();
                try { Directory.Delete(root, true); } catch { }
            }

            var sug = RunComplete.Suggest("Get-Chi", 7, null);
            if (!sug.Contains("Get-ChildItem")) problems.Add("autocomplete must suggest Get-ChildItem for Get-Chi");
            if (RunComplete.Suggest("g", 1, null).Count != 0) problems.Add("one character must not trigger suggestions");

            string from;
            string g = SaveActionForm.Generalize("Get-ChildItem -LiteralPath 'D:\\photos' -Recurse", "D:\\photos", out from);
            if (g != "Get-ChildItem -LiteralPath {dir} -Recurse" || from != "D:\\photos") problems.Add("quoted folder must become {dir}: " + g);
            g = SaveActionForm.Generalize("cd d:\\PHOTOS; dir", "D:\\photos", out from);
            if (g != "cd {dir}; dir") problems.Add("bare folder match is case-insensitive: " + g);
            g = SaveActionForm.Generalize("Get-Date", "D:\\photos", out from);
            if (g != "Get-Date" || from != null) problems.Add("nothing to replace must leave the command alone");
            Report("Run console: ANSI, table parsing, prelude, history import, autocomplete, generalization", problems, "");

            if (Environment.OSVersion.Platform != PlatformID.Win32NT) { Line("WARN", "Run console: real process runs", "skipped, needs Windows"); return; }
            problems = new List<string>();
            string previousDir = Journal.DirOverride;
            string scratch = Path.Combine(Path.GetTempPath(), "pstools-run-" + Guid.NewGuid().ToString("N"));
            Journal.DirOverride = scratch;
            int code;
            string outp = RunEngine.RunSync(new RunRequest { Script = "Write-Output 'hello-run'" }, out code, 60);
            if (!outp.Contains("hello-run") || code != 0) problems.Add("echo gave '" + outp.Trim() + "' exit " + code);
            outp = RunEngine.RunSync(new RunRequest { Script = "Get-Item C:\\nope-does-not-exist-pst" }, out code, 60);
            if (outp.Contains("CLIXML") || !outp.Contains("Cannot find path") || code != 1) problems.Add("error must be plain text with exit 1, got exit " + code + ": " + outp.Trim());
            RunEngine.RunSync(new RunRequest { Script = "exit 3" }, out code, 60);
            if (code != 3) problems.Add("exit 3 must be honoured, got " + code);
            outp = RunEngine.RunSync(new RunRequest { Shell = ActionShell.Cmd, Script = "echo hello-cmd & exit /b 4" }, out code, 60);
            if (!outp.Contains("hello-cmd") || code != 4) problems.Add("cmd gave '" + outp.Trim() + "' exit " + code);
            outp = RunEngine.RunSync(new RunRequest { Script = "Get-ChildItem -LiteralPath $env:windir -File | Select-Object -First 3 Name, Length", AsTable = true }, out code, 60);
            var table = RunEngine.ParseTable(outp, out text);
            if (table == null || table.Rows.Count != 3 || table.Columns.Length != 2) problems.Add("table run gave " + (table == null ? "text: " + (text ?? outp).Trim() : table.Rows.Count + " rows"));
            outp = RunEngine.RunSync(new RunRequest { Script = "$a = 1", AsTable = true }, out code, 60);
            RunEngine.ParseTable(outp, out text);
            if (text == null || !text.Contains("nothing")) problems.Add("a command with no output must say so in table mode: " + outp.Trim());
            Journal.DirOverride = previousDir;
            try { Directory.Delete(scratch, true); } catch { }
            Report("Run console: real PowerShell and cmd runs (output, errors, exit codes, table)", problems, "");
        }
    }
}
