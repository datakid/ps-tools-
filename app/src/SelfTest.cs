using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PSTools
{
    static class SelfTest
    {
        static int passes, failures, warnings;

        static void Line(string status, string name, string detail)
        {
            Console.WriteLine(status + "  " + name + (string.IsNullOrEmpty(detail) ? "" : "  -  " + detail));
            if (status == "FAIL") failures++;
            else if (status == "WARN") warnings++;
            else passes++;
        }

        static string Preview(List<string> items)
        {
            return string.Join(", ", items.Take(8).ToArray()) + (items.Count > 8 ? " (+" + (items.Count - 8) + " more)" : "");
        }

        static void Report(string name, List<string> problems, string okDetail)
        {
            if (problems.Count == 0) Line("PASS", name, okDetail);
            else Line("FAIL", name, Preview(problems));
        }

        public static int Run()
        {
            var clock = Stopwatch.StartNew();
            var all = Actions.All;
            CheckFields(all);
            CheckIds(all);
            CheckGroups(all);
            CheckPresets(all);
            CheckDefaultPins(all);
            CheckSuggestRules(all);
            CheckSuggestScan();
            CheckPs(all);
            CheckRoundTripV3();
            CheckMigration(all);
            CheckPinOperations();
            CheckMenuPlans();
            Console.WriteLine();
            Console.WriteLine(passes + " passed, " + failures + " failed, " + warnings + " warnings in " + clock.ElapsedMilliseconds + " ms");
            return failures == 0 ? 0 : 1;
        }

        public static int DumpSnippets()
        {
            var map = new Dictionary<string, string>();
            foreach (var a in Actions.All)
                if (a.Targets != Target.None && !string.IsNullOrEmpty(a.Ps)) map[a.Id] = a.Ps;
            Console.Out.Write(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(map));
            Console.Out.Flush();
            return 0;
        }

        static void CheckFields(List<Action> all)
        {
            var bad = new List<string>();
            foreach (var a in all)
            {
                var missing = new List<string>();
                if (string.IsNullOrWhiteSpace(a.Id)) missing.Add("Id");
                if (string.IsNullOrWhiteSpace(a.Label)) missing.Add("Label");
                if (string.IsNullOrWhiteSpace(a.Group)) missing.Add("Group");
                if (string.IsNullOrWhiteSpace(a.Hint)) missing.Add("Hint");
                if (missing.Count > 0) bad.Add((a.Id ?? "(no id)") + " lacks " + string.Join("/", missing.ToArray()));
            }
            Report("actions: Id, Label, Group and Hint are filled in", bad, all.Count + " actions");
        }

        static void CheckIds(List<Action> all)
        {
            var dup = all.GroupBy(a => a.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Report("actions: ids are unique", dup, all.Count + " ids");
        }

        static void CheckGroups(List<Action> all)
        {
            var bad = all.Where(a => Modules.Find(a.Group) == null).Select(a => a.Id + " (" + a.Group + ")").ToList();
            Report("actions: every Group exists in Modules.All", bad, Modules.All.Count + " modules");
        }

        static void CheckPresets(List<Action> all)
        {
            var ids = new HashSet<string>(all.Select(a => a.Id));
            var bad = Presets.ReferencedIds().Where(id => !ids.Contains(id)).Distinct().ToList();
            foreach (var n in Presets.Names) if (Presets.Ids(n).Count == 0) bad.Add("preset " + n + " is empty");
            Report("presets: every referenced id exists", bad, Presets.Names.Length + " presets");
        }

        static void CheckDefaultPins(List<Action> all)
        {
            var bad = new List<string>();
            int count = 0;
            foreach (var t in DefaultPins.Contexts)
            {
                var list = DefaultPins.For(t);
                count += list.Count;
                if (list.Count > Settings.MaxPins) bad.Add(DefaultPins.Key(t) + " has more than " + Settings.MaxPins + " pins");
                if (list.Distinct().Count() != list.Count) bad.Add(DefaultPins.Key(t) + " repeats an id");
                foreach (var id in list)
                {
                    var a = all.FirstOrDefault(x => x.Id == id);
                    if (a == null) bad.Add(DefaultPins.Key(t) + ": " + id + " does not exist");
                    else if ((a.Targets & t) == 0) bad.Add(DefaultPins.Key(t) + ": " + id + " does not support it");
                }
            }
            Report("default pins: ids exist and fit their context", bad, count + " pins in 4 contexts");
        }

        static void CheckSuggestRules(List<Action> all)
        {
            var ids = new HashSet<string>(all.Select(a => a.Id));
            var bad = Suggest.RuleIds.Where(id => !ids.Contains(id)).ToList();
            Report("suggestions: every rule maps to an existing id", bad, Suggest.RuleIds.Length + " ids");
        }

        static void CheckSuggestScan()
        {
            var problems = new List<string>();
            string dir = Path.Combine(Path.GetTempPath(), "pstools-selftest-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(dir);
                for (int i = 0; i < 6; i++) File.WriteAllText(Path.Combine(dir, "pic" + i + ".png"), "x" + i);
                File.WriteAllText(Path.Combine(dir, "pack.zip"), "z");
                var scan = Suggest.ScanFolder(dir);
                var list = Suggest.Combine(Suggest.ClipKind.Image, scan);
                var pictures = list.FirstOrDefault(s => s.ActionId == "img-resize-1600");
                var zips = list.FirstOrDefault(s => s.ActionId == "unzip-here");
                var paste = list.FirstOrDefault(s => s.ActionId == "paste-image");
                if (pictures == null || pictures.Reason != "6 pictures here") problems.Add("pictures suggestion: " + (pictures == null ? "missing" : pictures.Reason));
                if (zips == null || zips.Reason != "1 zip file here") problems.Add("zip suggestion: " + (zips == null ? "missing" : zips.Reason));
                if (paste == null) problems.Add("clipboard image suggestion missing");
                if (list.Count > Suggest.MaxItems) problems.Add("more than " + Suggest.MaxItems + " suggestions");
                var big = Suggest.Combine(Suggest.ClipKind.None, new Suggest.Scan { TopItems = 312 });
                if (!big.Any(s => s.ActionId == "space-map") || !big.Any(s => s.ActionId == "organize-type")) problems.Add("large folder suggestions missing");
                var repos = Suggest.Combine(Suggest.ClipKind.Link, new Suggest.Scan { GitRepos = 3, BuildFolders = true });
                if ((Actions.Find("git-status").Usable && !repos.Any(s => s.ActionId == "git-status")) || !repos.Any(s => s.ActionId == "clean-build") || !repos.Any(s => s.ActionId == "download-clip")) problems.Add("repository suggestions missing");
                var clock = Stopwatch.StartNew();
                Suggest.ScanFolder(Environment.SystemDirectory);
                if (clock.ElapsedMilliseconds > Suggest.MaxMillis + 600) problems.Add("system folder scan took " + clock.ElapsedMilliseconds + " ms");
            }
            catch (Exception ex) { problems.Add(ex.Message); }
            finally { try { Directory.Delete(dir, true); } catch { } }
            Report("suggestions: counts, caps and time limit", problems, "6 pictures, 1 zip, 312 items, repos, time cap");
        }

        static void CheckPs(List<Action> all)
        {
            var missing = all.Where(a => a.Targets != Target.None && string.IsNullOrWhiteSpace(a.Ps) && string.IsNullOrWhiteSpace(a.PsNone)).Select(a => a.Id).ToList();
            int withPs = all.Count(a => a.Targets != Target.None && !string.IsNullOrWhiteSpace(a.Ps));
            int none = all.Count(a => a.Targets != Target.None && string.IsNullOrWhiteSpace(a.Ps) && !string.IsNullOrWhiteSpace(a.PsNone));
            Report("ps: every menu action has Ps or PsNone", missing, withPs + " with a command, " + none + " with a reason");
        }

        static SettingsData Sample()
        {
            var d = new SettingsData();
            d.Version = SettingsData.CurrentVersion;
            d.Configured = true;
            d.Theme = "dark";
            d.Enabled = Presets.Ids("Developer");
            d.ContextPins[Target.Folder] = new List<string> { "zip-smart", "copy-path-quoted", "space-map" };
            d.ContextPins[Target.Background] = new List<string> { "open-ps", "paste-image" };
            d.ContextPins[Target.File] = new List<string> { "copy-sha256", "copy-name" };
            d.ContextPins[Target.Drive] = new List<string> { "folder-sizes" };
            d.GroupPins.Add("Dev");
            d.LibPins.Add("flatten-copy");
            d.Win = "10,20,980,640,0";
            d.Recent.AddRange(new[] { "copy-name", "zip-smart" });
            return d;
        }

        static void CheckRoundTripV3()
        {
            var problems = new List<string>();
            var a = Sample();
            string text = a.Serialize();
            var b = SettingsData.Parse(text.Replace("\r", "").Split('\n'));
            string diff;
            if (!a.SameAs(b, out diff)) problems.Add("parse differs in " + diff);
            string again = b.Serialize();
            if (again != text) problems.Add("second serialization differs");
            if (text.Contains("\nfav=") || text.StartsWith("fav=")) problems.Add("fav= lines are still written");
            if (!text.Contains("version=3")) problems.Add("version line missing");
            if (!text.Contains("pin.folder=zip-smart")) problems.Add("pin.folder lines missing");
            var order = b.ContextPins[Target.Folder];
            if (order.Count != 3 || order[0] != "zip-smart" || order[1] != "copy-path-quoted" || order[2] != "space-map") problems.Add("pin order changed");
            if (!b.LibPins.Contains("flatten-copy")) problems.Add("library pin lost");
            Report("settings: v3 parse and serialize round trip", problems, text.Replace("\r", "").Split('\n').Length + " lines");
        }

        static void CheckMigration(List<Action> all)
        {
            var problems = new List<string>();
            var enabled = Presets.Ids("Everyday").ToList();
            enabled.Add("copy-md5");
            enabled.Add("attr-hide");
            var lines = new List<string> { "configured=1", "theme=light" };
            foreach (var e in enabled) lines.Add("enabled=" + e);
            var favs = new[] { "copy-name", "zip-smart", "copy-md5", "attr-hide", "paste-image", "search-here", "missing-action" };
            foreach (var f in favs) lines.Add("fav=" + f);
            lines.Add("pingroup=Dev");
            lines.Add("pin=flatten-copy");
            lines.Add("win=1,2,3,4,0");
            lines.Add("recent=copy-name");
            var d = SettingsData.Parse(lines);
            if (d.Version != 0) problems.Add("a file without a version line was not treated as v2");
            SettingsData.Migrate(d, all);
            if (d.Version != SettingsData.CurrentVersion) problems.Add("migration did not set the version");
            foreach (var e in enabled) if (!d.Enabled.Contains(e)) problems.Add("enabled action lost: " + e);
            foreach (var f in favs)
            {
                var a = all.FirstOrDefault(x => x.Id == f);
                if (a == null) continue;
                if (!DefaultPins.Contexts.Any(t => d.ContextPins[t].Contains(f))) problems.Add("pin lost: " + f);
            }
            if (!d.ContextPins[Target.File].Contains("copy-name")) problems.Add("copy-name should be pinned for files");
            if (d.ContextPins[Target.Folder].Contains("copy-name")) problems.Add("copy-name should not be pinned for folders");
            if (!d.ContextPins[Target.Folder].Contains("zip-smart")) problems.Add("zip-smart should be pinned for folders");
            if (!d.ContextPins[Target.Background].Contains("paste-image")) problems.Add("paste-image should be pinned inside folders");
            if (!d.ContextPins[Target.File].Contains("copy-md5")) problems.Add("copy-md5 should be pinned for files");
            if (!d.ContextPins[Target.Folder].Contains("attr-hide") || !d.ContextPins[Target.File].Contains("attr-hide")) problems.Add("attr-hide should be pinned wherever it applies");
            if (d.ContextPins[Target.Drive].Contains("attr-hide")) problems.Add("attr-hide should not be pinned for drives");
            if (!d.GroupPins.Contains("Dev") || !d.LibPins.Contains("flatten-copy")) problems.Add("group or library pins lost");
            string text = d.Serialize();
            if (text.Contains("fav=")) problems.Add("migrated settings still contain fav=");
            var back = SettingsData.Parse(text.Replace("\r", "").Split('\n'));
            SettingsData.Migrate(back, all);
            string diff;
            if (!d.SameAs(back, out diff)) problems.Add("round trip differs in " + diff);
            Report("settings: v2 file migrates without losing enabled actions or pins", problems, enabled.Count + " enabled, " + favs.Length + " old favorites");
        }

        static void CheckPinOperations()
        {
            var problems = new List<string>();
            var d = new SettingsData();
            d.Enabled = new HashSet<string>();
            var folderIds = Actions.All.Where(a => (a.Targets & Target.Folder) != 0 && a.Usable && a.Group != "Library" && a.Group != "Recipes").Select(a => a.Id).Take(Settings.MaxPins + 2).ToList();
            int accepted = 0;
            foreach (var id in folderIds) if (d.TryPin(Target.Folder, id)) accepted++;
            if (accepted != Settings.MaxPins) problems.Add("accepted " + accepted + " pins, expected " + Settings.MaxPins);
            if (d.ContextPins[Target.Folder].Count != Settings.MaxPins) problems.Add("list holds " + d.ContextPins[Target.Folder].Count);
            foreach (var id in d.ContextPins[Target.Folder]) if (!d.Enabled.Contains(id)) problems.Add("pinned action not enabled: " + id);
            if (d.TryPin(Target.File, "open-wsl")) problems.Add("an action was pinned in a context it does not support");
            var first = d.ContextPins[Target.Folder][0];
            if (!d.MovePin(Target.Folder, first, 1) || d.ContextPins[Target.Folder][1] != first) problems.Add("move down failed");
            d.SetEnabled(first, false);
            if (d.ContextPins[Target.Folder].Contains(first)) problems.Add("an action that was turned off stayed pinned");
            Report("settings: pin limit, move, unpin and enable rules", problems, Settings.MaxPins + " pins per context");
        }

        static void CheckMenuPlans()
        {
            foreach (var name in Presets.Names)
            {
                var state = SettingsData.ForPreset(name, null, false);
                var problems = new List<string>();
                var detail = new List<string>();
                foreach (var t in DefaultPins.Contexts)
                {
                    var plan = MenuPlan.Build(t, true, state);
                    var ids = plan.AllActions.Select(a => a.Id).ToList();
                    var dup = ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                    foreach (var d in dup) problems.Add(DefaultPins.Key(t) + ": duplicate verb " + d);
                    var expected = state.ContextPins[t].Where(id =>
                    {
                        var a = Actions.Find(id);
                        return a != null && a.Usable && (a.Targets & t) != 0 && state.IsEnabled(id);
                    }).ToList();
                    var actual = plan.Pinned.Select(a => a.Id).ToList();
                    if (!expected.SequenceEqual(actual)) problems.Add(DefaultPins.Key(t) + ": pinned items differ from the pin list");
                    if (plan.Groups.Any(g => g.Items.Any(i => actual.Contains(i.Id)))) problems.Add(DefaultPins.Key(t) + ": a pinned item is also inside a group");
                    if (plan.Groups.Any(g => Modules.Find(g.Name) == null)) problems.Add(DefaultPins.Key(t) + ": unknown group");
                    bool limited = name == "Minimal" || name == "Everyday";
                    if (limited && plan.Pinned.Count + plan.Groups.Count > 12) problems.Add(DefaultPins.Key(t) + ": " + (plan.Pinned.Count + plan.Groups.Count) + " top-level entries (" + string.Join(", ", plan.Pinned.Select(a => a.Id).Concat(plan.Groups.Select(g => "[" + g.Name + "]")).ToArray()) + ")");
                    var quiet = MenuPlan.Build(t, false, state);
                    if (quiet.AllActions.Any(a => a.Extended)) problems.Add(DefaultPins.Key(t) + ": Shift-only action shown without Shift");
                    if (quiet.AllActions.Any(a => !ids.Contains(a.Id))) problems.Add(DefaultPins.Key(t) + ": plan without Shift is not a subset");
                    detail.Add(DefaultPins.Key(t) + " " + (plan.Pinned.Count + plan.Groups.Count) + (plan.OpenEntry ? "+1" : ""));
                }
                Report("menu plan: " + name + " preset in all four contexts", problems, "top-level entries: " + string.Join(", ", detail.ToArray()));
            }
        }
    }
}
