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
    }
}
