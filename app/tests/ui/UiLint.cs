using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class LintIssue
{
    public string Image;
    public string Rule;
    public string Message;
}

public class DiffResult
{
    public string Image;
    public string Status;
    public double Ratio;
}

public class LintReport
{
    public List<LintIssue> Issues = new List<LintIssue>();
    public List<DiffResult> Diffs = new List<DiffResult>();
    public int Images;
}

public static class UiLint
{
    class Pic
    {
        public int W, H;
        public int[] Px;
    }

    class Region
    {
        public int Area, MinX, MinY, MaxX, MaxY;
    }

    static Pic Load(string path)
    {
        using (var src = new Bitmap(path))
        {
            var pic = new Pic { W = src.Width, H = src.Height, Px = new int[src.Width * src.Height] };
            using (var copy = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(copy)) g.DrawImageUnscaled(src, 0, 0);
                var data = copy.LockBits(new Rectangle(0, 0, copy.Width, copy.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try { Marshal.Copy(data.Scan0, pic.Px, 0, pic.Px.Length); }
                finally { copy.UnlockBits(data); }
            }
            return pic;
        }
    }

    static void Save(Pic pic, string path)
    {
        using (var bmp = new Bitmap(pic.W, pic.H, PixelFormat.Format32bppArgb))
        {
            var data = bmp.LockBits(new Rectangle(0, 0, pic.W, pic.H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(pic.Px, 0, data.Scan0, pic.Px.Length); }
            finally { bmp.UnlockBits(data); }
            bmp.Save(path, ImageFormat.Png);
        }
    }

    static double Lum(int argb)
    {
        int r = (argb >> 16) & 255, g = (argb >> 8) & 255, b = argb & 255;
        return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;
    }

    static double Rel(int argb)
    {
        double[] c = { (argb >> 16) & 255, (argb >> 8) & 255, argb & 255 };
        for (int i = 0; i < 3; i++)
        {
            double v = c[i] / 255.0;
            c[i] = v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
    }

    static double Contrast(int a, int b)
    {
        double x = Rel(a), y = Rel(b);
        double hi = Math.Max(x, y), lo = Math.Min(x, y);
        return (hi + 0.05) / (lo + 0.05);
    }

    static string Hex(int argb) { return (argb & 0xFFFFFF).ToString("X6"); }

    static List<Region> Regions(Pic pic, Func<int, bool> pred, int minArea)
    {
        var result = new List<Region>();
        var seen = new bool[pic.Px.Length];
        var stack = new int[pic.Px.Length];
        for (int start = 0; start < pic.Px.Length; start++)
        {
            if (seen[start] || !pred(pic.Px[start])) continue;
            int sp = 0;
            stack[sp++] = start;
            seen[start] = true;
            var r = new Region { MinX = int.MaxValue, MinY = int.MaxValue, MaxX = -1, MaxY = -1 };
            while (sp > 0)
            {
                int p = stack[--sp];
                int x = p % pic.W, y = p / pic.W;
                r.Area++;
                if (x < r.MinX) r.MinX = x;
                if (x > r.MaxX) r.MaxX = x;
                if (y < r.MinY) r.MinY = y;
                if (y > r.MaxY) r.MaxY = y;
                if (x > 0 && !seen[p - 1] && pred(pic.Px[p - 1])) { seen[p - 1] = true; stack[sp++] = p - 1; }
                if (x < pic.W - 1 && !seen[p + 1] && pred(pic.Px[p + 1])) { seen[p + 1] = true; stack[sp++] = p + 1; }
                if (y > 0 && !seen[p - pic.W] && pred(pic.Px[p - pic.W])) { seen[p - pic.W] = true; stack[sp++] = p - pic.W; }
                if (y < pic.H - 1 && !seen[p + pic.W] && pred(pic.Px[p + pic.W])) { seen[p + pic.W] = true; stack[sp++] = p + pic.W; }
            }
            if (r.Area >= minArea) result.Add(r);
        }
        return result;
    }

    static int I(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : 0; }
    static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : ""; }
    static bool B(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v is bool && (bool)v; }
    static float F(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToSingle(v, CultureInfo.InvariantCulture) : 0f; }

    static Rectangle Box(Dictionary<string, object> c) { return new Rectangle(I(c, "x"), I(c, "y"), I(c, "w"), I(c, "h")); }

    static bool Enabled(Dictionary<string, object> c) { object v; return !c.TryGetValue("enabled", out v) || !(v is bool) || (bool)v; }

    static bool IsTextual(Dictionary<string, object> c)
    {
        string t = S(c, "type");
        return t == "Label" || t == "LinkLabel" || t.EndsWith("Button") || t.EndsWith("CheckBox") || t.EndsWith("RadioButton") || t == "FLabel";
    }

    static void Add(LintReport rep, string image, string rule, string msg)
    {
        if (rep.Issues.Count(i => i.Image == image && i.Rule == rule) >= 12) return;
        rep.Issues.Add(new LintIssue { Image = image, Rule = rule, Message = msg });
    }

    static void LintControls(LintReport rep, string image, Dictionary<string, object> entry, List<Dictionary<string, object>> controls, Pic pic)
    {
        var byId = new Dictionary<int, Dictionary<string, object>>();
        foreach (var c in controls) byId[I(c, "id")] = c;
        var visible = controls.Where(c => B(c, "visible")).ToList();
        string theme = S(entry, "theme");

        foreach (var c in visible)
        {
            if (!IsTextual(c)) continue;
            string text = S(c, "text");
            if (text.Length == 0 || B(c, "autosize")) continue;
            Rectangle b = Box(c);
            if (b.Width < 12 || b.Height < 8) continue;
            using (var font = new Font(S(c, "font"), F(c, "size"), B(c, "bold") ? FontStyle.Bold : FontStyle.Regular))
            {
                bool wrap = S(c, "type").EndsWith("Label");
                int availW = b.Width - I(c, "padH");
                var flags = TextFormatFlags.NoPrefix | (wrap ? TextFormatFlags.WordBreak : TextFormatFlags.Default);
                Size need = TextRenderer.MeasureText(text, font, new Size(wrap ? Math.Max(availW, 1) : int.MaxValue, 0), flags);
                if (wrap && need.Height > b.Height - I(c, "padV") + 3)
                    Add(rep, image, "text-clip", S(c, "type") + " '" + Trim(text) + "' needs " + need.Height + " px of height, has " + (b.Height - I(c, "padV")));
                else if (!wrap && need.Width > b.Width + 1 && !B(c, "ellipsis"))
                    Add(rep, image, "text-clip", S(c, "type") + " '" + Trim(text) + "' needs " + need.Width + " px of width, has " + b.Width);
            }
        }

        foreach (var group in visible.GroupBy(c => I(c, "parentId")))
        {
            var sibs = group.ToList();
            for (int i = 0; i < sibs.Count; i++)
            {
                for (int j = i + 1; j < sibs.Count; j++)
                {
                    var a = sibs[i]; var b2 = sibs[j];
                    if (S(a, "tag") == "overlay" || S(b2, "tag") == "overlay") continue;
                    if (S(a, "dock") == "Fill" || S(b2, "dock") == "Fill") continue;
                    Rectangle ra = Box(a), rb = Box(b2);
                    if (ra.Width == 0 || ra.Height == 0 || rb.Width == 0 || rb.Height == 0) continue;
                    Rectangle x = Rectangle.Intersect(ra, rb);
                    if (x.Width > 1 && x.Height > 1)
                        Add(rep, image, "overlap", S(a, "type") + " '" + Trim(S(a, "text")) + "' overlaps " + S(b2, "type") + " '" + Trim(S(b2, "text")) + "' by " + x.Width + "x" + x.Height);
                }
            }

            var free = sibs.Where(c => S(c, "dock") == "None").ToList();
            for (int i = 0; i < free.Count; i++)
                for (int j = i + 1; j < free.Count; j++)
                {
                    Rectangle ra = Box(free[i]), rb = Box(free[j]);
                    bool stacked = ra.Bottom <= rb.Top || rb.Bottom <= ra.Top;
                    int dx = Math.Abs(ra.Left - rb.Left);
                    if (stacked && dx >= 1 && dx <= 3)
                        Add(rep, image, "alignment", S(free[i], "type") + " '" + Trim(S(free[i], "text")) + "' and " + S(free[j], "type") + " '" + Trim(S(free[j], "text")) + "' left edges differ by " + dx + " px");
                }
        }

        foreach (var c in visible)
        {
            int pid = I(c, "parentId");
            if (pid < 0 || !byId.ContainsKey(pid)) continue;
            var p = byId[pid];
            if (!B(p, "visible") || B(p, "scrolls") || S(p, "tag") == "overlay") continue;
            Rectangle rp = Box(p), rc = Box(c);
            if (rc.Width == 0 || rc.Height == 0) continue;
            if (rc.Right > rp.Right + 1 || rc.Bottom > rp.Bottom + 1 || rc.Left < rp.Left - 1 || rc.Top < rp.Top - 1)
                Add(rep, image, "out-of-bounds", S(c, "type") + " '" + Trim(S(c, "text")) + "' " + rc + " exceeds parent " + S(p, "type") + " " + rp);
        }

        foreach (var card in visible.Where(c => S(c, "tag") == "card"))
        {
            Rectangle rc = Box(card);
            foreach (var k in visible.Where(c => I(c, "parentId") == I(card, "id") && IsTextual(c) && S(c, "text").Length > 0))
            {
                Rectangle rk = Box(k);
                int edge = Math.Min(Math.Min(rk.Left - rc.Left, rk.Top - rc.Top), Math.Min(rc.Right - rk.Right, rc.Bottom - rk.Bottom));
                if (edge < 8) Add(rep, image, "edge-padding", S(k, "type") + " '" + Trim(S(k, "text")) + "' is " + edge + " px from the card edge");
            }
        }

        foreach (var c in visible)
        {
            if (!IsTextual(c) || S(c, "text").Length == 0 || !Enabled(c)) continue;
            Rectangle b = Rectangle.Intersect(Box(c), new Rectangle(0, 0, pic.W, pic.H));
            if (b.Width < 16 || b.Height < 10) continue;
            var freq = new Dictionary<int, int>();
            for (int y = b.Top; y < b.Bottom; y++)
                for (int x = b.Left; x < b.Right; x++)
                {
                    int px = pic.Px[y * pic.W + x] | unchecked((int)0xFF000000);
                    int n;
                    freq.TryGetValue(px, out n);
                    freq[px] = n + 1;
                }
            if (freq.Count < 2) continue;
            int bg = freq.OrderByDescending(kv => kv.Value).First().Key;
            double best = 1.0; int fg = bg;
            foreach (var kv in freq)
            {
                double cr = Contrast(kv.Key, bg);
                if (cr > best) { best = cr; fg = kv.Key; }
            }
            if (best > 1.2 && best < 4.5)
                Add(rep, image, "contrast", S(c, "type") + " '" + Trim(S(c, "text")) + "' contrast " + best.ToString("0.0", CultureInfo.InvariantCulture) + ":1 (text #" + Hex(fg) + " on #" + Hex(bg) + ")");
        }
    }

    static string Trim(string s)
    {
        s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
        return s.Length > 36 ? s.Substring(0, 33) + "..." : s;
    }

    static bool Covered(Region r, IEnumerable<Dictionary<string, object>> controls, string tag)
    {
        var rect = new Rectangle(r.MinX, r.MinY, r.MaxX - r.MinX + 1, r.MaxY - r.MinY + 1);
        foreach (var c in controls)
            if (S(c, "tag") == tag && B(c, "visible") && Box(c).IntersectsWith(rect)) return true;
        return false;
    }

    static bool IsFrame(Region r, Pic pic)
    {
        return r.MinX == 0 && r.MinY == 0 && r.MaxX == pic.W - 1 && r.MaxY == pic.H - 1 && r.Area < pic.W * pic.H / 10;
    }

    static void LintPixels(LintReport rep, string image, string theme, List<Dictionary<string, object>> controls, Pic pic)
    {
        if (theme == "dark")
        {
            foreach (var r in Regions(pic, px => Lum(px) > 0.85, 400))
                if (!Covered(r, controls, "allow-light") && !IsFrame(r, pic))
                    Add(rep, image, "dark-white-box", "light region " + (r.MaxX - r.MinX + 1) + "x" + (r.MaxY - r.MinY + 1) + " at " + r.MinX + "," + r.MinY + " (" + r.Area + " px) in a dark screenshot");
        }
        else
        {
            foreach (var r in Regions(pic, px => Lum(px) < 0.12, 400))
                if (!Covered(r, controls, "allow-dark") && !IsFrame(r, pic))
                    Add(rep, image, "light-dark-box", "dark region " + (r.MaxX - r.MinX + 1) + "x" + (r.MaxY - r.MinY + 1) + " at " + r.MinX + "," + r.MinY + " (" + r.Area + " px) in a light screenshot");
        }
        foreach (var r in Regions(pic, px => (px & 0xFFFFFF) == 0xF0F0F0 || (px & 0xFFFFFF) == 0xE1E1E1, 400))
            Add(rep, image, "default-grey", "default WinForms grey region " + (r.MaxX - r.MinX + 1) + "x" + (r.MaxY - r.MinY + 1) + " at " + r.MinX + "," + r.MinY);
    }

    static DiffResult Diff(string name, Pic shot, string baselinePath, string diffDir, int tolerance, double maxRatio)
    {
        if (!File.Exists(baselinePath)) return new DiffResult { Image = name, Status = "new", Ratio = 0 };
        var basePic = Load(baselinePath);
        if (basePic.W != shot.W || basePic.H != shot.H) return new DiffResult { Image = name, Status = "size-differs", Ratio = 1 };
        int changed = 0;
        var diff = new Pic { W = shot.W, H = shot.H, Px = new int[shot.Px.Length] };
        for (int i = 0; i < shot.Px.Length; i++)
        {
            int a = shot.Px[i], b = basePic.Px[i];
            bool differs = Math.Abs(((a >> 16) & 255) - ((b >> 16) & 255)) > tolerance || Math.Abs(((a >> 8) & 255) - ((b >> 8) & 255)) > tolerance || Math.Abs((a & 255) - (b & 255)) > tolerance;
            if (differs) { changed++; diff.Px[i] = unchecked((int)0xFFFF00FF); }
            else
            {
                int r = 255 - (255 - ((a >> 16) & 255)) * 3 / 10, g = 255 - (255 - ((a >> 8) & 255)) * 3 / 10, bl = 255 - (255 - (a & 255)) * 3 / 10;
                diff.Px[i] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | bl;
            }
        }
        double ratio = (double)changed / shot.Px.Length;
        if (ratio > 0)
        {
            Directory.CreateDirectory(diffDir);
            Save(diff, Path.Combine(diffDir, name));
        }
        return new DiffResult { Image = name, Status = ratio > maxRatio ? "changed" : "same", Ratio = ratio };
    }

    public static LintReport Execute(string shotsDir, string baselineDir, string outDir)
    {
        var rep = new LintReport();
        Directory.CreateDirectory(outDir);
        var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        var entries = new List<Dictionary<string, object>>();
        string merged = Path.Combine(shotsDir, "manifest.json");
        if (File.Exists(merged))
            foreach (var o in (System.Collections.IEnumerable)ser.DeserializeObject(File.ReadAllText(merged, Encoding.UTF8)))
                entries.Add((Dictionary<string, object>)o);

        foreach (var entry in entries)
        {
            string file = S(entry, "file");
            string path = Path.Combine(shotsDir, file);
            if (!File.Exists(path)) { Add(rep, file, "missing", "screenshot file not found"); continue; }
            rep.Images++;
            var pic = Load(path);
            var controls = new List<Dictionary<string, object>>();
            object raw;
            if (entry.TryGetValue("controls", out raw) && raw is System.Collections.IEnumerable)
                foreach (var o in (System.Collections.IEnumerable)raw) controls.Add((Dictionary<string, object>)o);
            LintControls(rep, file, entry, controls, pic);
            LintPixels(rep, file, S(entry, "theme"), controls, pic);
            rep.Diffs.Add(Diff(file, pic, Path.Combine(baselineDir, file), Path.Combine(outDir, "diff"), 16, 0.005));
        }
        return rep;
    }

    public static string ToJson(LintReport rep)
    {
        var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        return ser.Serialize(new Dictionary<string, object>
        {
            { "images", rep.Images },
            { "issues", rep.Issues.Select(i => new Dictionary<string, object> { { "image", i.Image }, { "rule", i.Rule }, { "message", i.Message } }).ToList() },
            { "diffs", rep.Diffs.Select(d => new Dictionary<string, object> { { "image", d.Image }, { "status", d.Status }, { "ratio", d.Ratio } }).ToList() }
        });
    }
}
