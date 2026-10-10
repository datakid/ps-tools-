using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PSTools
{
    class StructureOptions
    {
        public bool Files;
        public int Depth;
        public bool Hidden;
        public bool Counts;
        public string Format = "Tree";
    }

    static class Structure
    {
        public static readonly string[] Formats = { "Tree", "Tree (ASCII)", "Markdown list", "Indented", "Full paths", "Relative paths" };

        const int Cap = 200000;

        class Node
        {
            public string Name, Path;
            public bool IsDir, Cut;
            public int Files = -1;
            public List<Node> Kids = new List<Node>();
        }

        class State { public int N; public bool Capped; }

        static bool Skip(string path, StructureOptions o)
        {
            if (o.Hidden) return false;
            try { return (File.GetAttributes(path) & (FileAttributes.Hidden | FileAttributes.System)) != 0; }
            catch { return false; }
        }

        static string[] Safe(Func<string[]> get)
        {
            try { return get().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); }
            catch { return new string[0]; }
        }

        static Node Read(string dir, StructureOptions o, State st, Ctx c, int depth, string display)
        {
            if (c != null) c.Check();
            var node = new Node { Name = display ?? System.IO.Path.GetFileName(dir), Path = dir, IsDir = true };
            string[] files = (o.Files || o.Counts) ? Safe(() => Directory.GetFiles(dir)) : new string[0];
            if (!o.Hidden) files = files.Where(f => !Skip(f, o)).ToArray();
            if (o.Counts) node.Files = files.Length;
            if (o.Depth > 0 && depth > o.Depth) { node.Cut = true; return node; }
            foreach (var d in Safe(() => Directory.GetDirectories(dir)))
            {
                if (Skip(d, o)) continue;
                if (++st.N > Cap) { st.Capped = true; break; }
                if (Fs.IsLink(d)) node.Kids.Add(new Node { Name = System.IO.Path.GetFileName(d), Path = d, IsDir = true, Cut = true });
                else node.Kids.Add(Read(d, o, st, c, depth + 1, null));
            }
            if (o.Files)
                foreach (var f in files)
                {
                    if (++st.N > Cap) { st.Capped = true; break; }
                    node.Kids.Add(new Node { Name = System.IO.Path.GetFileName(f), Path = f });
                }
            return node;
        }

        static string Label(Node n, StructureOptions o)
        {
            string s = n.Name;
            if (n.IsDir && o.Files) s += "/";
            if (n.IsDir && o.Counts && n.Files >= 0) s += "  (" + n.Files + (n.Files == 1 ? " file)" : " files)");
            return s;
        }

        public static string Build(string root, StructureOptions o, Ctx c)
        {
            root = Fs.Full(root);
            string rootName = System.IO.Path.GetFileName(root);
            if (rootName.Length == 0) rootName = root;
            var st = new State();
            var top = Read(root, o, st, c, 1, rootName);
            var sb = new StringBuilder();
            string f = o.Format ?? "Tree";
            if (f == "Tree" || f == "Tree (ASCII)")
            {
                sb.Append(Label(top, o)).Append("\r\n");
                Tree(top, "", sb, o, f == "Tree (ASCII)");
            }
            else if (f == "Markdown list")
            {
                sb.Append("- ").Append(Label(top, o)).Append("\r\n");
                Indent(top, 1, sb, o, "- ");
            }
            else if (f == "Indented")
            {
                sb.Append(Label(top, o)).Append("\r\n");
                Indent(top, 1, sb, o, "");
            }
            else
            {
                bool rel = f == "Relative paths";
                Flat(top, root, rel, sb);
            }
            if (st.Capped) sb.Append("... stopped at ").Append(Cap.ToString("N0")).Append(" entries\r\n");
            return sb.ToString();
        }

        static void Tree(Node n, string prefix, StringBuilder sb, StructureOptions o, bool ascii)
        {
            for (int i = 0; i < n.Kids.Count; i++)
            {
                bool last = i == n.Kids.Count - 1;
                var k = n.Kids[i];
                string branch = ascii ? (last ? "\\-- " : "+-- ") : (last ? "\u2514\u2500\u2500 " : "\u251C\u2500\u2500 ");
                sb.Append(prefix).Append(branch).Append(Label(k, o)).Append("\r\n");
                string pad = ascii ? (last ? "    " : "|   ") : (last ? "    " : "\u2502   ");
                if (k.Kids.Count > 0) Tree(k, prefix + pad, sb, o, ascii);
            }
        }

        static void Indent(Node n, int level, StringBuilder sb, StructureOptions o, string bullet)
        {
            foreach (var k in n.Kids)
            {
                sb.Append(new string(' ', level * 2)).Append(bullet).Append(Label(k, o)).Append("\r\n");
                if (k.Kids.Count > 0) Indent(k, level + 1, sb, o, bullet);
            }
        }

        static void Flat(Node n, string root, bool rel, StringBuilder sb)
        {
            foreach (var k in n.Kids)
            {
                string p = k.Path;
                if (rel) p = p.Substring(root.Length).TrimStart(System.IO.Path.DirectorySeparatorChar);
                sb.Append(p).Append("\r\n");
                if (k.Kids.Count > 0) Flat(k, root, rel, sb);
            }
        }
    }
}
