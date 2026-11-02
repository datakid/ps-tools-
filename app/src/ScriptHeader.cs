using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PSTools
{
    class ScriptHeaderResult
    {
        public Dictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string Body = "";
        public bool HasHeader;
        public List<string> Warnings = new List<string>();
    }

    static class ScriptHeader
    {
        public static ScriptHeaderResult Parse(string text, bool cmd)
        {
            var r = new ScriptHeaderResult();
            text = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            string[] lines = text.Split('\n');
            return cmd ? ParseCmd(lines, r) : ParsePowerShell(lines, r);
        }

        static ScriptHeaderResult ParsePowerShell(string[] lines, ScriptHeaderResult r)
        {
            int i = 0;
            while (i < lines.Length && lines[i].Trim().Length == 0) i++;
            if (i >= lines.Length || !lines[i].TrimStart().StartsWith("<#")) { r.Body = string.Join("\n", lines); return r; }
            int end = -1;
            for (int j = i; j < lines.Length; j++)
                if (lines[j].Contains("#>")) { end = j; break; }
            if (end < 0) { r.Warnings.Add("header comment is not closed with #>"); r.Body = string.Join("\n", lines); return r; }
            var block = new List<string>();
            for (int j = i; j <= end; j++)
            {
                string l = lines[j];
                if (j == i) l = l.Substring(l.IndexOf("<#", StringComparison.Ordinal) + 2);
                if (j == end) { int k = l.IndexOf("#>", StringComparison.Ordinal); if (k >= 0) l = l.Substring(0, k); }
                block.Add(l);
            }
            bool any = ReadKeys(block, r);
            if (!any) { r.Body = string.Join("\n", lines); return r; }
            r.HasHeader = true;
            r.Body = string.Join("\n", lines.Skip(end + 1)).Trim('\n');
            return r;
        }

        static ScriptHeaderResult ParseCmd(string[] lines, ScriptHeaderResult r)
        {
            var block = new List<string>();
            int i = 0;
            if (i < lines.Length && lines[i].Trim().Equals("@echo off", StringComparison.OrdinalIgnoreCase)) i++;
            int start = i;
            while (i < lines.Length)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("::")) { block.Add(t.Substring(2)); i++; }
                else if (t.StartsWith("REM ", StringComparison.OrdinalIgnoreCase)) { block.Add(t.Substring(4)); i++; }
                else if (t.Length == 0 && block.Count == 0) i++;
                else break;
            }
            bool any = ReadKeys(block, r);
            if (!any) { r.Body = string.Join("\n", lines); return r; }
            r.HasHeader = true;
            var rest = new List<string>();
            if (start > 0) rest.AddRange(lines.Take(start));
            rest.AddRange(lines.Skip(i));
            r.Body = string.Join("\n", rest).Trim('\n');
            return r;
        }

        static bool ReadKeys(List<string> block, ScriptHeaderResult r)
        {
            bool any = false;
            string lastKey = null;
            foreach (string raw in block)
            {
                string l = raw.Trim();
                if (l.StartsWith("@"))
                {
                    int sp = 1;
                    while (sp < l.Length && !char.IsWhiteSpace(l[sp])) sp++;
                    string key = l.Substring(1, sp - 1).ToLowerInvariant();
                    string val = l.Substring(sp).Trim();
                    if (key.Length == 0) { r.Warnings.Add("empty @key"); continue; }
                    r.Keys[key] = val;
                    lastKey = key;
                    any = true;
                }
                else if (l.Length > 0 && lastKey == "description" && !l.StartsWith("."))
                    r.Keys[lastKey] = r.Keys[lastKey] + " " + l;
            }
            return any;
        }

        public static List<string> List(string s)
        {
            return (s ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        }
    }
}
