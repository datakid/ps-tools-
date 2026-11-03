using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PSTools
{
    static class Collector
    {
        static bool TryLead(string gate)
        {
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    using (File.Open(gate, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                    return true;
                }
                catch (IOException)
                {
                    try
                    {
                        if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(gate)).TotalSeconds > 15) { File.Delete(gate); continue; }
                    }
                    catch { }
                    return false;
                }
            }
            return false;
        }

        public static string[] Gather(string id, string path, int waitMs = 320, bool settle = false, int maxMs = 3000)
        {
            string dir = Path.Combine(Journal.Dir, "tmp");
            Directory.CreateDirectory(dir);
            string mine = Path.Combine(dir, "p-" + id + "-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(mine, path, Encoding.UTF8);
            string gate = Path.Combine(dir, "lead-" + id + ".lock");
            if (!TryLead(gate)) return null;

            Thread.Sleep(waitMs);
            if (settle)
            {
                int last = -1, stable = 0;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < maxMs)
                {
                    int n = Directory.GetFiles(dir, "p-" + id + "-*.txt").Length;
                    if (n == last) { stable += 40; if (stable >= waitMs) break; }
                    else { stable = 0; last = n; }
                    Thread.Sleep(40);
                }
            }
            try { File.Delete(gate); } catch { }

            var list = new List<string>();
            foreach (var f in Directory.GetFiles(dir, "p-" + id + "-*.txt"))
            {
                try
                {
                    if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(f)).TotalSeconds > 15) { File.Delete(f); continue; }
                    string claimed = Path.ChangeExtension(f, ".claimed");
                    File.Move(f, claimed);
                    list.Add(File.ReadAllText(claimed, Encoding.UTF8).Trim());
                    File.Delete(claimed);
                }
                catch { }
            }
            try { Directory.Delete(dir); } catch { }
            var result = list.Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return result.Length == 0 ? null : result;
        }
    }
}
