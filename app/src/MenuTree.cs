using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PSTools
{
    enum NodeKind { Folder, Action, Section }

    class MenuNode
    {
        public NodeKind Kind;
        public string Title = "";
        public string Path = "";
        public ActionSpec Spec;
        public List<MenuNode> Children = new List<MenuNode>();
        public bool Favorite;
        public double Score;
        public int Index;

        public int ActionCount()
        {
            if (Kind == NodeKind.Action) return 1;
            int n = 0;
            foreach (var c in Children) n += c.ActionCount();
            return n;
        }
    }

    class MenuQuery
    {
        public ContextKind Context = ContextKind.Folder;
        public string[] Paths = new string[0];
        public bool Shift;
        public ActionCatalog Catalog;
        public IList<string> Favorites = new List<string>();
        public IList<string> Recent = new List<string>();
        public Func<string, bool> HasTool;
        public Func<string, bool> IsDirectory;
        public bool SelectionKindsKnown;
        public string BackgroundFolder;
    }

    static class Tools
    {
        static readonly Dictionary<string, KeyValuePair<DateTime, bool>> cache = new Dictionary<string, KeyValuePair<DateTime, bool>>();

        public static Func<string, bool> Override;

        public static bool Has(string tool)
        {
            if (Override != null) return Override(tool);
            lock (cache)
            {
                KeyValuePair<DateTime, bool> hit;
                if (cache.TryGetValue(tool, out hit) && (DateTime.UtcNow - hit.Key).TotalMinutes < 10) return hit.Value;
                bool found;
                try { found = Fs.Which(tool) != null; }
                catch { found = false; }
                cache[tool] = new KeyValuePair<DateTime, bool>(DateTime.UtcNow, found);
                return found;
            }
        }
    }

    static class MenuTree
    {
        public static readonly string[] Canonical =
        {
            "Copy", "Paste & New", "Search", "Open", "Rename", "Organize", "Archive", "Image", "Media", "Inspect", "Compare",
            "Dev", "Git", "Links", "Clean up", "Fix", "Clipboard", "System", "Recipes"
        };

        public const string MyActions = "My actions";
        public const string RecentSection = "Recent";
        public const string SuggestedSection = "Suggested";
        public const string FavoritesSection = "Favorites";

        static readonly HashSet<string> ImageExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };

        public static ContextKind ContextFor(string[] paths, bool background)
        {
            if (background) return ContextKind.Background;
            if (paths == null || paths.Length == 0) return ContextKind.Background;
            bool anyDir = false, anyFile = false, anyDrive = false;
            foreach (var p in paths)
            {
                string t = p.TrimEnd('\\', '/');
                if (p.Length <= 3 && p.Length >= 2 && p[1] == ':') anyDrive = true;
                else if (SafeIsDir(p)) anyDir = true;
                else anyFile = true;
                if (t.Length == 0) anyDir = true;
            }
            var k = ContextKind.None;
            if (anyDrive && !anyDir && !anyFile) return ContextKind.Drive;
            if (anyDir || anyDrive) k |= ContextKind.Folder;
            if (anyFile) k |= ContextKind.File;
            return k;
        }

        static bool SafeIsDir(string p)
        {
            try { return Directory.Exists(p); }
            catch { return false; }
        }

        static Func<string, bool> DirTest(MenuQuery q)
        {
            return q.IsDirectory ?? SafeIsDir;
        }

        public static bool Applies(ActionSpec s, MenuQuery q, bool forSearch)
        {
            if (s.AppOnly || s.UserHidden) return false;
            if (s.PopupHidden && !forSearch && !q.Favorites.Contains(s.Id)) return false;
            if (s.Extended && !q.Shift) return false;
            ContextKind ctx = q.Context == ContextKind.Desktop ? ContextKind.Background : q.Context;
            if ((s.When & ctx) == 0 && !((s.When & ContextKind.Desktop) != 0 && ctx == ContextKind.Background)) return false;
            int count = q.Paths == null ? 0 : q.Paths.Length;
            if (count > 1 && s.Multi == MultiMode.Single) return false;
            if (s.MatchMin > 0 && count < s.MatchMin) return false;
            if (s.MatchMax > 0 && count > s.MatchMax) return false;
            if (count > 0 && (s.MatchKind != MatchKind.Any || s.MatchExt.Count > 0))
            {
                var isDir = DirTest(q);
                foreach (var p in q.Paths)
                {
                    bool dir = isDir(p);
                    if (s.MatchKind == MatchKind.File && dir) return false;
                    if (s.MatchKind == MatchKind.Folder && !dir) return false;
                    if (s.MatchExt.Count > 0)
                    {
                        if (dir) return false;
                        string ext = (System.IO.Path.GetExtension(p) ?? "").TrimStart('.').ToLowerInvariant();
                        if (!s.MatchExt.Contains(ext)) return false;
                    }
                }
            }
            if (s.MatchHas.Count > 0)
            {
                var dirs = new List<string>();
                var isDirTest = DirTest(q);
                if (q.Paths != null && q.Paths.Length > 0) foreach (var p in q.Paths) { if (isDirTest(p)) dirs.Add(p); }
                else if (!string.IsNullOrEmpty(q.BackgroundFolder)) dirs.Add(q.BackgroundFolder);
                if (dirs.Count == 0) return false;
                foreach (var d in dirs)
                {
                    bool any = false;
                    foreach (var f in s.MatchHas)
                    {
                        try { if (System.IO.File.Exists(System.IO.Path.Combine(d, f))) { any = true; break; } }
                        catch { }
                    }
                    if (!any) return false;
                }
            }
            var has = q.HasTool ?? Tools.Has;
            foreach (var r in s.Requires) if (!has(r)) return false;
            return true;
        }

        static int TopIndex(string top)
        {
            int i = Array.IndexOf(Canonical, top);
            return i >= 0 ? i : (top == MyActions ? 2000 : 1000);
        }

        static string TopOf(ActionSpec s)
        {
            string[] parts = s.MenuParts;
            if (parts.Length == 0) return s.Source == SpecSource.User ? MyActions : "";
            return parts[0];
        }

        static string[] FullPath(ActionSpec s)
        {
            string[] parts = s.MenuParts;
            if (parts.Length == 0 && s.Source == SpecSource.User) return new[] { MyActions };
            return parts;
        }

        public static List<MenuNode> Build(MenuQuery q)
        {
            var root = new List<MenuNode>();
            var applicable = q.Catalog.All.Where(s => Applies(s, q, false)).ToList();
            var byId = new Dictionary<string, ActionSpec>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in q.Catalog.All.Where(x => Applies(x, q, true))) byId[s.Id] = s;

            var recent = new List<MenuNode>();
            foreach (var id in q.Recent)
            {
                ActionSpec s;
                if (recent.Count < 5 && byId.TryGetValue(id, out s) && !recent.Any(n => n.Spec == s)) recent.Add(ActionNode(s, q));
            }
            AddSection(root, RecentSection, recent);

            var suggested = new List<MenuNode>();
            foreach (var id in SuggestIds(q))
            {
                ActionSpec s;
                if (suggested.Count < 4 && byId.TryGetValue(id, out s) && !suggested.Any(n => n.Spec == s)) suggested.Add(ActionNode(s, q));
            }
            AddSection(root, SuggestedSection, suggested);

            var favs = new List<MenuNode>();
            foreach (var id in q.Favorites)
            {
                ActionSpec s = q.Catalog.Find(id);
                if (s != null && byId.ContainsKey(s.Id) && !favs.Any(n => n.Spec == s)) favs.Add(ActionNode(s, q));
            }
            AddSection(root, FavoritesSection, favs);

            var folders = new Dictionary<string, MenuNode>(StringComparer.Ordinal);
            var rootItems = new List<MenuNode>();
            var topFolders = new List<MenuNode>();
            foreach (var s in applicable.OrderBy(x => x.Order).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase))
            {
                string[] parts = FullPath(s);
                var node = ActionNode(s, q);
                if (parts.Length == 0) { rootItems.Add(node); continue; }
                List<MenuNode> siblings = topFolders;
                string key = "";
                MenuNode parent = null;
                foreach (var part in parts)
                {
                    key = key.Length == 0 ? part : key + "/" + part;
                    MenuNode f;
                    if (!folders.TryGetValue(key, out f))
                    {
                        f = new MenuNode { Kind = NodeKind.Folder, Title = part, Path = key.Replace("/", " \u203A ") };
                        folders[key] = f;
                        siblings.Add(f);
                    }
                    parent = f;
                    siblings = f.Children;
                }
                parent.Children.Add(node);
            }

            foreach (var f in topFolders) SortFolder(f);
            topFolders.Sort((a, b) =>
            {
                int c = TopIndex(a.Title).CompareTo(TopIndex(b.Title));
                return c != 0 ? c : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
            });

            root.AddRange(rootItems);
            foreach (var f in topFolders) root.Add(Inline(f));
            Number(root, new int[1]);
            return root;
        }

        static void AddSection(List<MenuNode> root, string name, List<MenuNode> items)
        {
            if (items.Count == 0) return;
            var sec = new MenuNode { Kind = NodeKind.Section, Title = name };
            sec.Children.AddRange(items);
            root.Add(sec);
        }

        static MenuNode ActionNode(ActionSpec s, MenuQuery q)
        {
            return new MenuNode
            {
                Kind = NodeKind.Action, Title = s.Title, Spec = s,
                Path = string.Join(" \u203A ", FullPath(s)),
                Favorite = q.Favorites.Contains(s.Id)
            };
        }

        static void SortFolder(MenuNode f)
        {
            foreach (var c in f.Children) if (c.Kind == NodeKind.Folder) SortFolder(c);
            var folders = f.Children.Where(c => c.Kind == NodeKind.Folder).OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase).ToList();
            var actions = f.Children.Where(c => c.Kind == NodeKind.Action).ToList();
            f.Children = new List<MenuNode>();
            f.Children.AddRange(folders);
            f.Children.AddRange(actions);
        }

        static MenuNode Inline(MenuNode f)
        {
            for (int i = 0; i < f.Children.Count; i++)
                if (f.Children[i].Kind == NodeKind.Folder) f.Children[i] = Inline(f.Children[i]);
            if (f.Children.Count == 1 && f.Children[0].Kind == NodeKind.Action)
            {
                var a = f.Children[0];
                return new MenuNode { Kind = NodeKind.Action, Title = f.Title + ": " + a.Title, Spec = a.Spec, Path = a.Path, Favorite = a.Favorite };
            }
            return f;
        }

        static void Number(List<MenuNode> nodes, int[] counter)
        {
            foreach (var n in nodes)
            {
                n.Index = counter[0]++;
                if (n.Kind != NodeKind.Action) Number(n.Children, counter);
            }
        }

        static IEnumerable<string> SuggestIds(MenuQuery q)
        {
            var ids = new List<string>();
            if (q.Paths == null || q.Paths.Length == 0) return ids;
            var isDir = DirTest(q);
            bool allFiles = q.Paths.All(p => !isDir(p));
            if (allFiles && q.Paths.All(p => ImageExt.Contains(System.IO.Path.GetExtension(p) ?? "")))
                ids.AddRange(new[] { "image.png", "image.jpg", "image.resize-1600", "image.strip" });
            if (allFiles && q.Paths.All(p => string.Equals(System.IO.Path.GetExtension(p), ".zip", StringComparison.OrdinalIgnoreCase)))
                ids.Add("archive.extract");
            if (q.Paths.Length == 1 && isDir(q.Paths[0]) && SafeIsDir(System.IO.Path.Combine(q.Paths[0], ".git")))
                ids.AddRange(new[] { "git.copy.branch", "git.status.repos", "dev.clean-build" });
            return ids;
        }

        public static List<MenuNode> Search(MenuQuery q, string text)
        {
            text = (text ?? "").Trim();
            var result = new List<MenuNode>();
            if (text.Length == 0) return result;
            var all = q.Catalog.All.Where(s => Applies(s, q, true)).ToList();
            var tree = Build(q);
            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            IndexOrder(tree, order);
            string[] tokens = text.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in all)
            {
                double score = Score(s, tokens, text.ToLowerInvariant());
                if (score < 0) continue;
                int ri = -1;
                for (int i = 0; i < q.Recent.Count; i++) if (string.Equals(q.Recent[i], s.Id, StringComparison.OrdinalIgnoreCase)) { ri = i; break; }
                if (ri >= 0) score += 5 - ri;
                int idx;
                if (!order.TryGetValue(s.Id, out idx)) idx = 100000;
                var n = ActionNode(s, q);
                n.Score = score;
                n.Index = idx;
                result.Add(n);
            }
            return result.OrderByDescending(n => n.Score).ThenBy(n => n.Index).ThenBy(n => n.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static void IndexOrder(List<MenuNode> nodes, Dictionary<string, int> order)
        {
            foreach (var n in nodes)
            {
                if (n.Kind == NodeKind.Action) { if (!order.ContainsKey(n.Spec.Id)) order[n.Spec.Id] = n.Index; }
                else if (n.Kind != NodeKind.Section) IndexOrder(n.Children, order);
            }
        }

        static string[] Words(string s)
        {
            var words = new List<string>();
            var sb = new StringBuilder();
            foreach (char c in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (sb.Length > 0) { words.Add(sb.ToString()); sb.Length = 0; }
            }
            if (sb.Length > 0) words.Add(sb.ToString());
            return words.ToArray();
        }

        public static double Score(ActionSpec s, string[] tokens, string whole)
        {
            string title = s.Title.ToLowerInvariant();
            string[] words = Words(s.Title);
            if (title == whole) return 120;
            if (tokens.Length == 1 && whole.Length >= 2 && whole.Length <= 3)
            {
                string initials = string.Concat(words.Select(w => w.Substring(0, 1)).ToArray());
                if (initials.StartsWith(whole, StringComparison.Ordinal)) return 100;
            }
            double total = 0;
            foreach (string t in tokens)
            {
                double best = -1;
                if (title.StartsWith(t, StringComparison.Ordinal)) best = 90;
                else if (words.Any(w => w.StartsWith(t, StringComparison.Ordinal))) best = 80;
                else if (title.Contains(t)) best = 60;
                else if (s.Keywords.Any(k => k.ToLowerInvariant().Contains(t))) best = 40;
                else if (s.Description.ToLowerInvariant().Contains(t)) best = 20;
                if (best < 0) return -1;
                total += best;
            }
            double score = total / tokens.Length;
            if (s.Source == SpecSource.Library) score -= 25;
            return score;
        }

        public static string Dump(List<MenuNode> tree)
        {
            var sb = new StringBuilder();
            DumpNodes(tree, 0, sb);
            return sb.ToString();
        }

        static void DumpNodes(List<MenuNode> nodes, int depth, StringBuilder sb)
        {
            string pad = new string(' ', depth * 2);
            foreach (var n in nodes)
            {
                if (n.Kind == NodeKind.Section) sb.AppendLine(pad + "# " + n.Title);
                else if (n.Kind == NodeKind.Folder) sb.AppendLine(pad + "+ " + n.Title + "/");
                else
                {
                    string risk = n.Spec.Risk == Risk.Safe ? "" : "  [" + n.Spec.Risk.ToString().ToLowerInvariant() + (n.Spec.Admin ? ",admin" : "") + "]";
                    sb.AppendLine(pad + "- " + n.Title + "  (" + n.Spec.Id + ")" + risk);
                }
                if (n.Kind != NodeKind.Action) DumpNodes(n.Children, depth + 1, sb);
            }
        }
    }
}
