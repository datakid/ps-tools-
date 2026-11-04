using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PSTools
{
    class RiskResult
    {
        public Risk Risk = Risk.Safe;
        public List<string> Reasons = new List<string>();
        public List<string> Verbs = new List<string>();
    }

    static class RiskScan
    {
        static readonly HashSet<string> Destructive = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "remove-item", "rm", "del", "erase", "rd", "rmdir", "ri", "clear-content", "format-volume", "format", "diskpart", "clear-disk",
            "initialize-disk", "stop-computer", "restart-computer", "shutdown", "remove-itemproperty", "bcdedit", "set-executionpolicy", "clear-recyclebin"
        };

        static readonly HashSet<string> ModifyingExact = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "out-file", "expand-archive", "compress-archive", "mkdir", "md", "move", "ren", "copy", "xcopy", "robocopy", "mv", "cp", "ni", "mi", "ci", "rni", "sc", "tee-object"
        };

        static readonly HashSet<string> ReadOnlyVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "new-object", "new-guid", "new-timespan", "set-location", "set-variable", "set-strictmode", "set-psdebug", "set-clipboard", "add-type", "add-member", "set-alias", "new-variable", "new-alias"
        };

        static readonly Regex Quoted = new Regex("\"([^\"`]|`.)*\"|'([^']|'')*'", RegexOptions.Compiled);
        static readonly Regex Redirect = new Regex(@"(?<![-=<>\w])(\d?>>?)(?![&=])", RegexOptions.Compiled);

        static string[] Tokens(string command)
        {
            string stripped = Quoted.Replace(command ?? "", "\"\"");
            return stripped.Split(new[] { ' ', '\t', '\r', '\n', '|', ';', '(', ')', '{', '}', ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        static void Mark(RiskResult r, Risk level, string reason, string verb)
        {
            if (level > r.Risk) r.Risk = level;
            if (!r.Reasons.Contains(reason)) r.Reasons.Add(reason);
            if (verb != null && !r.Verbs.Contains(verb)) r.Verbs.Add(verb);
        }

        static bool Has(string[] t, int i, string value)
        {
            return i < t.Length && string.Equals(t[i], value, StringComparison.OrdinalIgnoreCase);
        }

        static bool AnyAfter(string[] t, int from, Func<string, bool> test)
        {
            for (int i = from; i < t.Length; i++) if (test(t[i])) return true;
            return false;
        }

        public static RiskResult Classify(string command, ActionShell shell)
        {
            var r = new RiskResult();
            string[] t = Tokens(command);
            bool whatIf = t.Any(x => string.Equals(x, "-whatif", StringComparison.OrdinalIgnoreCase));
            bool force = t.Any(x => string.Equals(x, "-force", StringComparison.OrdinalIgnoreCase));
            bool recurse = t.Any(x => string.Equals(x, "-recurse", StringComparison.OrdinalIgnoreCase) || string.Equals(x, "/s", StringComparison.OrdinalIgnoreCase));
            bool modifyingVerb = false;

            for (int i = 0; i < t.Length; i++)
            {
                string w = t[i];
                string lower = w.ToLowerInvariant();
                if (Destructive.Contains(lower))
                {
                    if (lower == "format" && !(i + 1 < t.Length && (t[i + 1].Length == 2 && t[i + 1][1] == ':' || t[i + 1].StartsWith("/")))) continue;
                    Mark(r, Risk.Destructive, w + " removes or overwrites data", w);
                    modifyingVerb = true;
                }
                else if (lower == "reg" && Has(t, i + 1, "delete")) Mark(r, Risk.Destructive, "reg delete removes registry data", "reg delete");
                else if (lower == "cipher" && AnyAfter(t, i + 1, x => x.Equals("/w", StringComparison.OrdinalIgnoreCase) || x.StartsWith("/w:", StringComparison.OrdinalIgnoreCase))) Mark(r, Risk.Destructive, "cipher /w wipes free space", "cipher /w");
                else if (lower == "vssadmin" && Has(t, i + 1, "delete")) Mark(r, Risk.Destructive, "vssadmin delete removes shadow copies", "vssadmin delete");
                else if (lower == "git" && Has(t, i + 1, "clean") && AnyAfter(t, i + 2, x => x.StartsWith("-") && x.Contains("f"))) Mark(r, Risk.Destructive, "git clean -f deletes untracked files", "git clean");
                else if (lower == "git" && Has(t, i + 1, "reset") && AnyAfter(t, i + 2, x => x.Equals("--hard", StringComparison.OrdinalIgnoreCase))) Mark(r, Risk.Destructive, "git reset --hard discards local changes", "git reset --hard");
                else if (lower == "git" && Has(t, i + 1, "push") && AnyAfter(t, i + 2, x => x.Equals("--force", StringComparison.OrdinalIgnoreCase) || x.Equals("-f", StringComparison.OrdinalIgnoreCase))) Mark(r, Risk.Destructive, "git push --force rewrites remote history", "git push --force");
                else if (lower == "takeown" || (lower == "icacls" && AnyAfter(t, i + 1, x => x.Equals("/grant", StringComparison.OrdinalIgnoreCase)) && AnyAfter(t, i + 1, x => x.IndexOf("windows", StringComparison.OrdinalIgnoreCase) >= 0)))
                    Mark(r, Risk.Destructive, w + " changes ownership or permissions on system files", w);
                else if (lower == "start-process" && AnyAfter(t, i + 1, x => x.Equals("runas", StringComparison.OrdinalIgnoreCase)))
                    Mark(r, Risk.Modifies, "Start-Process -Verb RunAs starts something as administrator", "Start-Process -Verb RunAs");
                else if (lower == "git" && i + 1 < t.Length && new[] { "commit", "pull", "merge", "checkout", "switch", "stash", "rebase", "cherry-pick" }.Contains(t[i + 1].ToLowerInvariant()))
                    Mark(r, Risk.Modifies, "git " + t[i + 1] + " changes the repository", "git " + t[i + 1]);
                else if ((lower == "npm" || lower == "pnpm" || lower == "yarn") && (Has(t, i + 1, "install") || Has(t, i + 1, "add") || Has(t, i + 1, "remove") || Has(t, i + 1, "uninstall")))
                    Mark(r, Risk.Modifies, w + " " + t[i + 1] + " changes installed packages", w + " " + t[i + 1]);
                else if (lower == "winget" && (Has(t, i + 1, "install") || Has(t, i + 1, "upgrade") || Has(t, i + 1, "uninstall")))
                    Mark(r, Risk.Modifies, "winget " + t[i + 1] + " changes installed apps", "winget " + t[i + 1]);
                else if (ModifyingExact.Contains(lower))
                {
                    Mark(r, Risk.Modifies, w + " creates or changes files", w);
                    modifyingVerb = true;
                }
                else if (!ReadOnlyVerbs.Contains(lower) && Regex.IsMatch(lower, "^(new|set|move|rename|copy|add|remove|clear|save|export|install|uninstall|update)-"))
                {
                    if (lower.StartsWith("remove-") || lower.StartsWith("clear-")) Mark(r, Risk.Destructive, w + " removes data", w);
                    else Mark(r, Risk.Modifies, w + " changes things", w);
                    modifyingVerb = true;
                }
            }

            string noQuotes = Quoted.Replace(command ?? "", "\"\"");
            if (Redirect.IsMatch(noQuotes)) Mark(r, Risk.Modifies, "output redirection (>) writes a file", ">");

            if (force && modifyingVerb) Mark(r, Risk.Destructive, "-Force with a command that changes things skips safety checks", "-Force");
            if (recurse && r.Verbs.Any(v => Destructive.Contains(v))) Mark(r, Risk.Destructive, "-Recurse applies the removal to everything below", "-Recurse");

            if (whatIf && r.Risk != Risk.Safe)
            {
                var safe = new RiskResult();
                safe.Reasons.Add("-WhatIf only previews what would happen");
                return safe;
            }
            return r;
        }
    }
}
