using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PSTools
{
    class PlaceholderContext
    {
        public string[] Paths = new string[0];
        public string Dir;
        public string Clip = "";
        public DateTime Now = DateTime.Now;
        public Dictionary<string, string> Inputs = new Dictionary<string, string>();
        public Func<string, string> GitRoot;
    }

    class PlaceholderException : Exception
    {
        public PlaceholderException(string message) : base(message) { }
    }

    static class Placeholders
    {
        public const int MaxClip = 100 * 1024;

        const string Names = "paths?|dir|name|stem|ext|parent|clip|date|time|now|gitroot|input:[^{}'\"\\r\\n]+";

        static readonly Regex Token = new Regex("'\\{(" + Names + ")\\}'|\\{(" + Names + ")\\}", RegexOptions.Compiled);

        static readonly char[] PsQuotes = { '\'', '\u2018', '\u2019', '\u201A', '\u201B' };

        public static List<string> Tokens(string text)
        {
            var l = new List<string>();
            foreach (Match m in Token.Matches(text ?? ""))
            {
                string t = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                if (!l.Contains(t)) l.Add(t);
            }
            return l;
        }

        public static List<string> InputNames(string text)
        {
            return Tokens(text).Where(t => t.StartsWith("input:")).Select(t => t.Substring(6)).ToList();
        }

        public static string EscapePs(string value)
        {
            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                sb.Append(c);
                if (Array.IndexOf(PsQuotes, c) >= 0) sb.Append(c);
            }
            return sb.ToString();
        }

        public static string EscapeCmd(string value)
        {
            if (value.IndexOf('"') >= 0)
                throw new PlaceholderException("This path contains a quote character, which cmd cannot pass safely. Use PowerShell.");
            string v = value.Replace("%", "%%");
            int trailing = 0;
            while (trailing < v.Length && v[v.Length - 1 - trailing] == '\\') trailing++;
            if (trailing > 0) v += new string('\\', trailing);
            return v;
        }

        public static string Quote(string value, ActionShell shell)
        {
            value = value ?? "";
            if (shell == ActionShell.Cmd) return "\"" + EscapeCmd(value) + "\"";
            return "'" + EscapePs(value) + "'";
        }

        static string FirstPath(PlaceholderContext c) { return c.Paths != null && c.Paths.Length > 0 ? c.Paths[0] : ""; }

        static string Leaf(string p)
        {
            string t = p.TrimEnd('\\', '/');
            if (t.Length == 0) return p;
            int cut = t.LastIndexOfAny(new[] { '\\', '/' });
            string n = cut >= 0 ? t.Substring(cut + 1) : t;
            return n.Length == 0 ? p : n;
        }

        static string ParentOf(string p)
        {
            string t = p.TrimEnd('\\', '/');
            int cut = t.LastIndexOfAny(new[] { '\\', '/' });
            if (cut < 0) return "";
            string parent = t.Substring(0, cut);
            if (parent.Length == 2 && parent[1] == ':') parent += "\\";
            return parent;
        }

        static string DirOf(PlaceholderContext c)
        {
            if (c.Dir != null) return c.Dir;
            string p = FirstPath(c);
            if (p.Length == 0) return "";
            bool isDir;
            try { isDir = Directory.Exists(p); }
            catch { isDir = false; }
            if (isDir) return p;
            string parent = ParentOf(p);
            return parent.Length == 0 ? p : parent;
        }

        static string Single(string token, PlaceholderContext c)
        {
            string p = FirstPath(c);
            switch (token)
            {
                case "path": return p;
                case "dir": return DirOf(c);
                case "name": return Leaf(p);
                case "stem": { string n = Leaf(p); int dot = n.LastIndexOf('.'); return dot > 0 ? n.Substring(0, dot) : n; }
                case "ext": { string n = Leaf(p); int dot = n.LastIndexOf('.'); return dot > 0 ? n.Substring(dot) : ""; }
                case "parent": return ParentOf(p);
                case "clip": { string s = c.Clip ?? ""; return s.Length > MaxClip ? s.Substring(0, MaxClip) : s; }
                case "date": return c.Now.ToString("yyyy-MM-dd");
                case "time": return c.Now.ToString("HH-mm-ss");
                case "now": return c.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                case "gitroot": { string d = DirOf(c); return c.GitRoot == null || d.Length == 0 ? "" : (c.GitRoot(d) ?? ""); }
            }
            if (token.StartsWith("input:"))
            {
                string name = token.Substring(6);
                string v;
                if (c.Inputs != null && c.Inputs.TryGetValue(name, out v)) return v ?? "";
                throw new PlaceholderException("The input '" + name + "' was not provided.");
            }
            return "";
        }

        public static string Expand(string text, ActionShell shell, PlaceholderContext ctx)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            bool cmd = shell == ActionShell.Cmd;
            return Token.Replace(text, m =>
            {
                bool inQuotes = m.Groups[1].Success;
                string token = inQuotes ? m.Groups[1].Value : m.Groups[2].Value;
                if (token == "paths") return ExpandList(ctx, cmd, inQuotes);
                string v = Single(token, ctx);
                if (cmd) return "\"" + EscapeCmd(v) + "\"";
                return "'" + EscapePs(v) + "'";
            });
        }

        static string ExpandList(PlaceholderContext ctx, bool cmd, bool inQuotes)
        {
            var items = ctx.Paths ?? new string[0];
            if (cmd) return string.Join(" ", items.Select(i => "\"" + EscapeCmd(i) + "\"").ToArray());
            var esc = items.Select(i => EscapePs(i)).ToArray();
            if (inQuotes) return "'" + string.Join("','", esc) + "'";
            return "@(" + string.Join(",", esc.Select(e => "'" + e + "'").ToArray()) + ")";
        }
    }
}
