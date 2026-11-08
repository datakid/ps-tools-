using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PSTools
{
    static class ClipOps
    {
        public static string Nl { get { return "\r\n"; } }

        public static List<string> Lines(string text)
        {
            var l = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            if (l.Count > 0 && l[l.Count - 1].Length == 0) l.RemoveAt(l.Count - 1);
            return l;
        }

        public static string Join(IEnumerable<string> lines) { return string.Join(Nl, lines.ToArray()); }

        public static List<string> Words(string s)
        {
            var words = new List<string>();
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!char.IsLetterOrDigit(c)) { if (sb.Length > 0) { words.Add(sb.ToString()); sb.Length = 0; } continue; }
                if (sb.Length > 0)
                {
                    char p = s[i - 1];
                    bool boundary = (char.IsLower(p) && char.IsUpper(c)) || (char.IsUpper(p) && char.IsUpper(c) && i + 1 < s.Length && char.IsLower(s[i + 1])) || (char.IsDigit(p) != char.IsDigit(c) && char.IsLetterOrDigit(p));
                    if (boundary) { words.Add(sb.ToString()); sb.Length = 0; }
                }
                sb.Append(c);
            }
            if (sb.Length > 0) words.Add(sb.ToString());
            return words;
        }

        public static string Kebab(string s) { return string.Join("-", Words(s).Select(w => w.ToLowerInvariant()).ToArray()); }

        public static string Snake(string s) { return string.Join("_", Words(s).Select(w => w.ToLowerInvariant()).ToArray()); }

        public static string Camel(string s)
        {
            var w = Words(s);
            var sb = new StringBuilder();
            for (int i = 0; i < w.Count; i++)
            {
                string x = w[i].ToLowerInvariant();
                sb.Append(i == 0 ? x : char.ToUpperInvariant(x[0]) + x.Substring(1));
            }
            return sb.ToString();
        }

        public static string TitleCase(string s)
        {
            var sb = new StringBuilder();
            bool start = true;
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) { sb.Append(start ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c)); start = false; }
                else { sb.Append(c); start = true; }
            }
            return sb.ToString();
        }

        static void WriteJson(object o, StringBuilder sb, int indent, bool pretty)
        {
            string pad = pretty ? new string(' ', indent * 2) : "";
            string pad1 = pretty ? new string(' ', (indent + 1) * 2) : "";
            string nl = pretty ? "\n" : "";
            var d = o as IDictionary<string, object>;
            if (d != null)
            {
                if (d.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{").Append(nl);
                int i = 0;
                foreach (var kv in d)
                {
                    sb.Append(pad1).Append(new JavaScriptSerializer().Serialize(kv.Key)).Append(pretty ? ": " : ":");
                    WriteJson(kv.Value, sb, indent + 1, pretty);
                    sb.Append(++i < d.Count ? "," : "").Append(nl);
                }
                sb.Append(pad).Append("}");
                return;
            }
            var list = o as IList;
            if (list != null && !(o is string))
            {
                if (list.Count == 0) { sb.Append("[]"); return; }
                sb.Append("[").Append(nl);
                for (int i = 0; i < list.Count; i++)
                {
                    sb.Append(pad1);
                    WriteJson(list[i], sb, indent + 1, pretty);
                    sb.Append(i + 1 < list.Count ? "," : "").Append(nl);
                }
                sb.Append(pad).Append("]");
                return;
            }
            if (o == null) sb.Append("null");
            else if (o is bool) sb.Append((bool)o ? "true" : "false");
            else if (o is string) sb.Append(new JavaScriptSerializer().Serialize((string)o));
            else if (o is IFormattable) sb.Append(((IFormattable)o).ToString(null, CultureInfo.InvariantCulture));
            else sb.Append(o.ToString());
        }

        public static string Json(string text, bool pretty)
        {
            var o = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(text);
            var sb = new StringBuilder();
            WriteJson(o, sb, 0, pretty);
            return sb.ToString().Replace("\n", Nl);
        }

        public static string Apply(string key, string text, out string summary)
        {
            summary = null;
            var lines = Lines(text);
            switch (key)
            {
                case "sort": summary = "Sorted " + lines.Count + " lines"; return Join(lines.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ThenBy(x => x, StringComparer.Ordinal));
                case "unique":
                    var seen = new HashSet<string>();
                    var kept = lines.Where(x => seen.Add(x)).ToList();
                    summary = "Removed " + (lines.Count - kept.Count) + " duplicate" + (lines.Count - kept.Count == 1 ? "" : "s");
                    return Join(kept);
                case "trim": summary = "Trimmed " + lines.Count + " lines"; return Join(lines.Select(x => x.Trim()));
                case "remove-blank":
                    var nonBlank = lines.Where(x => x.Trim().Length > 0).ToList();
                    summary = "Removed " + (lines.Count - nonBlank.Count) + " blank line" + (lines.Count - nonBlank.Count == 1 ? "" : "s");
                    return Join(nonBlank);
                case "json-pretty": summary = "JSON formatted"; return Json(text, true);
                case "json-minify": summary = "JSON minified"; return Json(text, false);
                case "base64-encode": summary = "Encoded as Base64"; return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                case "base64-decode": summary = "Decoded from Base64"; return Encoding.UTF8.GetString(Convert.FromBase64String(text.Trim()));
                case "url-encode": summary = "URL encoded"; return Uri.EscapeDataString(text);
                case "url-decode": summary = "URL decoded"; return Uri.UnescapeDataString(text.Replace('+', ' '));
                case "lower": summary = "Lower-cased"; return text.ToLowerInvariant();
                case "upper": summary = "Upper-cased"; return text.ToUpperInvariant();
                case "kebab": summary = "Converted to kebab-case"; return Join(lines.Select(Kebab));
                case "snake": summary = "Converted to snake_case"; return Join(lines.Select(Snake));
                case "camel": summary = "Converted to camelCase"; return Join(lines.Select(Camel));
                case "paths-fwd": summary = "Slashes flipped"; return text.Replace('\\', '/');
                case "quote-lines": summary = "Quoted " + lines.Count + " lines"; return string.Join(", ", lines.Where(x => x.Length > 0).Select(x => "'" + x.Replace("'", "''") + "'").ToArray());
                case "to-ps-array": summary = "Made a PowerShell array of " + lines.Count(x => x.Length > 0) + " items"; return "@(" + string.Join(",", lines.Where(x => x.Length > 0).Select(x => "'" + x.Replace("'", "''") + "'").ToArray()) + ")";
                case "guid": summary = "New GUID"; return Guid.NewGuid().ToString();
                case "timestamp": summary = "Timestamp"; return DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            }
            throw new ArgumentException("Unknown clipboard operation " + key);
        }

        public static string Count(string text)
        {
            var lines = Lines(text);
            int words = (text ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return lines.Count + " line" + (lines.Count == 1 ? "" : "s") + ", " + words + " word" + (words == 1 ? "" : "s") + ", " + (text ?? "").Length + " character" + ((text ?? "").Length == 1 ? "" : "s");
        }

        public static readonly string[] Keys =
        {
            "sort", "unique", "trim", "remove-blank", "json-pretty", "json-minify", "base64-encode", "base64-decode", "url-encode", "url-decode",
            "lower", "upper", "kebab", "snake", "camel", "paths-fwd", "quote-lines", "to-ps-array", "guid", "timestamp"
        };
    }
}
