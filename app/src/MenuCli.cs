using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PSTools
{
    static class MenuCli
    {
        static void Emit(string[] args, string text)
        {
            string file = TestTools.Option(args, "--out");
            if (file != null) File.WriteAllText(file, text, new UTF8Encoding(false));
            else Console.Write(text);
        }

        public static void ApplyGlobalOptions(string[] args)
        {
            string settings = TestTools.Option(args, "--settings");
            if (settings != null) Settings.FileOverride = settings;
            string tools = TestTools.Option(args, "--tools");
            if (tools != null)
            {
                var set = new HashSet<string>(tools.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase);
                Tools.Override = t => set.Contains(t);
            }
        }

        public static int DumpMenu(string[] args)
        {
            Settings.EnsureLoaded();
            string exe = TestTools.Option(args, "--exe") ?? "<EXE>";
            Emit(args, Shell.DumpMenu(Settings.Snapshot(), exe));
            return 0;
        }

        public static MenuQuery QueryFrom(string[] args)
        {
            Settings.EnsureLoaded();
            string ctxName = (TestTools.Option(args, "--ctx") ?? "folder").ToLowerInvariant();
            var ctx = SpecRules.ParseContext(ctxName);
            if (ctx == ContextKind.None) ctx = ContextKind.Folder;
            string paths = TestTools.Option(args, "--paths");
            var q = new MenuQuery
            {
                Context = ctx,
                Paths = paths == null ? new string[0] : paths.Split('|'),
                Shift = TestTools.Flag(args, "--shift"),
                Catalog = ActionCatalog.Current,
                Favorites = Settings.Favorites,
                Recent = Settings.UsedIn(SpecRules.ContextName(ctx))
            };
            if (TestTools.Option(args, "--dirs") != null)
            {
                var dirs = new HashSet<string>(TestTools.Option(args, "--dirs").Split('|'), StringComparer.OrdinalIgnoreCase);
                q.IsDirectory = p => dirs.Contains(p);
            }
            return q;
        }

        static Dictionary<string, object> ToJson(MenuNode n)
        {
            var d = new Dictionary<string, object> { { "kind", n.Kind.ToString().ToLowerInvariant() }, { "title", n.Title } };
            if (n.Spec != null) d["id"] = n.Spec.Id;
            if (n.Kind != NodeKind.Action) d["children"] = n.Children.Select(ToJson).ToList();
            return d;
        }

        static int CountActions(List<MenuNode> nodes)
        {
            int n = 0;
            foreach (var x in nodes) if (x.Kind != NodeKind.Section) n += x.ActionCount();
            return n;
        }

        public static int DumpTree(string[] args)
        {
            var q = QueryFrom(args);
            var tree = MenuTree.Build(q);
            string search = TestTools.Option(args, "--search");
            if (search != null)
            {
                var sb = new StringBuilder();
                foreach (var n in MenuTree.Search(q, search)) sb.AppendLine(n.Title + "  (" + n.Spec.Id + ")  " + n.Path + "  " + n.Score.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                Emit(args, sb.ToString());
                return 0;
            }
            if (TestTools.Flag(args, "--json"))
            {
                Emit(args, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(new Dictionary<string, object>
                {
                    { "context", SpecRules.ContextName(q.Context) }, { "rows", tree.Count }, { "actions", CountActions(tree) }, { "tree", tree.Select(ToJson).ToList() }
                }));
                return 0;
            }
            Emit(args, MenuTree.Dump(tree));
            return 0;
        }
    }
}
