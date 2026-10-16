using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PSTools
{
    class Recipe
    {
        public string Id = "", Name = "", Desc = "";
        public List<string> Steps = new List<string>();
        public bool Menu;

        public Recipe Copy()
        {
            return new Recipe { Id = Id, Name = Name, Desc = Desc, Menu = Menu, Steps = new List<string>(Steps) };
        }
    }

    static class RecipeData
    {
        static List<Recipe> cache;
        static string loadError;

        public static string JsonPath { get { return Path.Combine(Program.ExeDir, "recipes.json"); } }

        public static string LoadError { get { EnsureLoaded(); return loadError; } }

        public static List<Recipe> All { get { EnsureLoaded(); return cache; } }

        static void EnsureLoaded()
        {
            if (cache != null) return;
            cache = Read(JsonPath, out loadError);
        }

        public static bool InMenu(string actionId)
        {
            foreach (var r in All) if ("recipe-" + r.Id == actionId) return r.Menu;
            return false;
        }

        static string S(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        public static string Slug(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in (name ?? "").ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            string s = sb.ToString().Trim('-');
            return s.Length == 0 ? "recipe" : s;
        }

        public static string UniqueId(string wanted, IEnumerable<Recipe> existing)
        {
            string id = Slug(wanted);
            string candidate = id;
            int n = 2;
            while (existing.Any(r => r.Id == candidate)) candidate = id + "-" + n++;
            return candidate;
        }

        public static List<Recipe> Read(string path, out string error)
        {
            error = null;
            var list = new List<Recipe>();
            if (!File.Exists(path)) return list;
            try
            {
                var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = (Dictionary<string, object>)js.DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
                object o;
                if (!root.TryGetValue("recipes", out o) || o == null) return list;
                foreach (Dictionary<string, object> d in (IEnumerable)o)
                {
                    var r = new Recipe { Name = S(d, "name").Trim(), Desc = S(d, "description"), Menu = d.ContainsKey("menu") && d["menu"] is bool && (bool)d["menu"] };
                    if (r.Name.Length == 0) continue;
                    object st;
                    if (d.TryGetValue("steps", out st) && st != null)
                        foreach (object x in (IEnumerable)st) if (x != null) r.Steps.Add(x.ToString());
                    string id = S(d, "id").Trim();
                    r.Id = UniqueId(id.Length > 0 ? id : r.Name, list);
                    list.Add(r);
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                list = new List<Recipe>();
            }
            return list;
        }

        public static string Serialize(IList<Recipe> list)
        {
            var js = new JavaScriptSerializer();
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"recipes\": [");
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                var d = new Dictionary<string, object>();
                d["id"] = r.Id; d["name"] = r.Name; d["description"] = r.Desc; d["menu"] = r.Menu; d["steps"] = r.Steps.ToArray();
                sb.Append("    ").Append(js.Serialize(d));
                sb.AppendLine(i < list.Count - 1 ? "," : "");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static void Save(IList<Recipe> list)
        {
            File.WriteAllText(JsonPath, Serialize(list), new UTF8Encoding(false));
            cache = null;
            Actions.Rebuild();
        }
    }

    static class RecipeRunner
    {
        static readonly HashSet<string> Groups = new HashSet<string> { "Copy", "Create", "Archive", "Image", "Organize", "Clean up", "Export", "Fix", "Dev", "Disk" };

        static readonly HashSet<string> Blocked = new HashSet<string>
        {
            "group-selected", "undo-last", "paste-image", "paste-text", "new-folder-clip",
            "duplicates", "clean-build", "git-status", "git-pull", "compare-marked", "mark"
        };

        public static bool Eligible(Action a)
        {
            return a.Run != null && a.Window == null && a.Script == null && a.Tier != Tier.Elevated
                && Groups.Contains(a.Group) && !Blocked.Contains(a.Id) && a.Usable;
        }

        static string E(string s) { return s.Replace("{", "{{").Replace("}", "}}"); }

        public static string Check(Recipe r, IList<Action> lookup, out Target targets, out bool multi, out string applies)
        {
            targets = Target.All; multi = true; applies = null;
            if (r.Steps.Count == 0) { targets = Target.None; multi = false; return "This recipe has no steps yet."; }
            bool first = true, same = true;
            for (int i = 0; i < r.Steps.Count; i++)
            {
                var a = lookup.FirstOrDefault(x => x.Id == r.Steps[i]);
                if (a == null || !Eligible(a))
                {
                    targets = Target.None; multi = false;
                    return "Step " + (i + 1) + " (" + r.Steps[i] + ") is not available in this version of PS Tools.";
                }
                targets &= a.Targets;
                multi &= a.Multi;
                if (first) { applies = a.AppliesTo; first = false; }
                else if (applies != a.AppliesTo) same = false;
            }
            if (!same) applies = null;
            if (targets == Target.None)
            {
                multi = false;
                return "These steps never apply to the same kind of item (some need files, others folders).";
            }
            return null;
        }

        public static string Works(Target t)
        {
            var w = new List<string>();
            if ((t & Target.Folder) != 0) w.Add("folders");
            if ((t & Target.File) != 0) w.Add("files");
            if ((t & Target.Drive) != 0) w.Add("drives");
            if ((t & Target.Background) != 0) w.Add("folder background");
            return w.Count == 0 ? "nothing" : string.Join(", ", w.ToArray());
        }

        public static string StepList(Recipe r, IList<Action> lookup)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < r.Steps.Count; i++)
            {
                var a = lookup.FirstOrDefault(x => x.Id == r.Steps[i]);
                sb.Append(i + 1).Append(". ").Append(a != null ? a.Label.TrimEnd('.') : "(missing: " + r.Steps[i] + ")");
                if (i < r.Steps.Count - 1) sb.AppendLine();
            }
            return sb.ToString();
        }

        static string ConfirmText(Recipe r, List<Action> steps)
        {
            if (!steps.Any(a => a.Confirm != null)) return null;
            var sb = new StringBuilder();
            sb.Append("Run recipe \"").Append(E(r.Name)).Append("\" on {0}?\n\nSteps, in order:\n");
            for (int i = 0; i < steps.Count; i++) sb.Append(i + 1).Append(". ").Append(E(steps[i].Label.TrimEnd('.'))).Append("\n");
            sb.Append("\nThese steps ask for your OK, and this one OK covers all of them:\n");
            foreach (var a in steps.Where(x => x.Confirm != null))
            {
                string text;
                try { text = string.Format(a.Confirm, "\u0001"); }
                catch (FormatException) { text = a.Label; }
                sb.Append("\n- ").Append(E(a.Label.TrimEnd('.'))).Append(": ").Append(E(text).Replace("\u0001", "{0}"));
            }
            sb.Append("\n\nThe whole recipe can be reverted with Undo last.");
            return sb.ToString();
        }

        public static Action ToAction(Recipe rec, List<Action> basis)
        {
            Target t; bool multi; string applies;
            string problem = Check(rec, basis, out t, out multi, out applies);
            var steps = problem == null ? rec.Steps.Select(id => basis.First(x => x.Id == id)).ToList() : new List<Action>();
            string hint = (rec.Desc.Trim().Length > 0 ? rec.Desc.Trim() + "\n\n" : "") + "Steps, in order, on the same selection:\n" + StepList(rec, basis)
                + (problem != null ? "\n\nCannot run yet: " + problem : "");
            string confirm = ConfirmText(rec, steps);
            return new Action
            {
                Id = "recipe-" + rec.Id, Group = "Recipes", Label = rec.Name, Hint = hint,
                Targets = t, Multi = multi, AppliesTo = applies, Confirm = confirm, Undoable = true,
                PsNone = "A recipe chains several PS Tools actions with one confirmation and one Undo last. Each step's own PowerShell equivalent is shown on its action.",
                Tier = confirm != null ? Tier.Confirm : Tier.Instant,
                Run = (paths, ctx) => Run(rec, paths, ctx)
            };
        }

        static Target KindOf(string path)
        {
            if (!Directory.Exists(path)) return Target.File;
            string root = Path.GetPathRoot(path);
            bool isRoot = root != null && string.Equals(root.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            return Target.Folder | Target.Background | (isRoot ? Target.Drive : Target.None);
        }

        static List<Action> Resolve(Recipe r, string[] paths)
        {
            Target t; bool multi; string applies;
            string problem = Check(r, Actions.All, out t, out multi, out applies);
            if (problem != null) throw new InvalidOperationException("Recipe \"" + r.Name + "\" cannot run.\n\n" + problem);
            var steps = r.Steps.Select(id => Actions.Find(id)).ToList();
            foreach (string p in paths)
            {
                Target kind = KindOf(p);
                for (int i = 0; i < steps.Count; i++)
                    if ((steps[i].Targets & kind) == 0)
                        throw new InvalidOperationException("Recipe \"" + r.Name + "\" cannot run on this item.\n\nStep " + (i + 1) + " (" + steps[i].Label.TrimEnd('.') + ") does not work on "
                            + ((kind & Target.File) != 0 ? "files" : "folders") + ".");
            }
            return steps;
        }

        public static Result Run(Recipe r, string[] paths, Ctx ctx)
        {
            var steps = Resolve(r, paths);
            int done = 0, lists = 0;
            string clip = null, last = null;
            Journal.BeginBatch("Recipe " + r.Name + " (" + Actions.Describe(paths) + ")");
            try
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    ctx.Check();
                    var a = steps[i];
                    ctx.Report("Step " + (i + 1) + " of " + steps.Count + ": " + a.Label.TrimEnd('.'));
                    Result res;
                    try { res = a.Run(paths, ctx); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        throw new Exception("Recipe \"" + r.Name + "\" stopped at step " + (i + 1) + " (" + a.Label.TrimEnd('.') + "):\n" + ex.Message
                            + (done > 0 ? "\n\nThe " + done + " earlier step" + (done == 1 ? " is" : "s are") + " done and can be reverted with Undo last." : ""));
                    }
                    done++;
                    if (res == null) continue;
                    if (res.ClipText != null) clip = res.ClipText;
                    if (res.Rows != null && res.Rows.Count > 0) lists++;
                    if (!string.IsNullOrEmpty(res.Summary)) last = res.Summary;
                }
            }
            finally { Journal.EndBatch(); }

            string summary = "Recipe \"" + r.Name + "\": " + done + (done == 1 ? " step" : " steps") + " done.";
            if (lists > 0) summary += " (" + lists + (lists == 1 ? " list" : " lists") + " not shown)";
            if (last != null && last.IndexOf('\n') < 0) summary += "  Last: " + last;
            if (summary.Length > 150) summary = summary.Substring(0, 147) + "...";
            return new Result { Summary = summary, ClipText = clip };
        }
    }

    static partial class Actions
    {
        static IEnumerable<Action> RecipeActions(List<Action> basis)
        {
            yield return new Action
            {
                Id = "recipes-manage", Group = "Recipes", Label = "Manage recipes...", Targets = Target.None,
                Hint = "Create, edit and share recipes. A recipe is a list of actions that run one after another on the same selection, with a single confirmation and a single Undo last for the whole chain.",
                Window = paths => new RecipesForm()
            };
            foreach (var r in RecipeData.All) yield return RecipeRunner.ToAction(r, basis);
        }
    }
}
