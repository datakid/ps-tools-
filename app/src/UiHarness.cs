using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PSTools
{
    static class UiHarness
    {
        [DllImport("user32.dll")]
        static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, int flags);

        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int processId);

        class Scenario
        {
            public string Name;
            public Func<Form> Build;
        }

        static StreamWriter log;
        static string fixturesDir;
        static string scratch;

        static void Log(string text)
        {
            if (log != null) { log.WriteLine(text); log.Flush(); }
            try { Console.WriteLine(text); } catch { }
        }

        static string WorkFolder()
        {
            if (fixturesDir != null)
            {
                string p = Path.Combine(fixturesDir, "tree-basic");
                if (Directory.Exists(p)) return p;
            }
            string d = Path.Combine(scratch, "work");
            Directory.CreateDirectory(Path.Combine(d, "src", "app"));
            Directory.CreateDirectory(Path.Combine(d, "docs"));
            File.WriteAllText(Path.Combine(d, "src", "app", "main.py"), "print('hello')\n");
            File.WriteAllText(Path.Combine(d, "docs", "readme.md"), "# Readme\n");
            return d;
        }

        static Form Show(Form f)
        {
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(40, 40);
            f.ShowInTaskbar = false;
            return f;
        }

        static T Create<T>(params object[] args) where T : class
        {
            return (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, CultureInfo.InvariantCulture);
        }

        static void Invoke(object target, string method, params object[] args)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) throw new MissingMethodException(target.GetType().Name, method);
            m.Invoke(target, args);
        }

        static Result SampleResult()
        {
            var r = new Result { Summary = "50 files", Columns = new[] { "Path", "Size", "Modified" }, Rows = new List<string[]>(), PathColumn = 0 };
            string root = WorkFolder();
            for (int i = 1; i <= 50; i++)
                r.Rows.Add(new[] { Path.Combine(root, "src", "app", "module_" + i.ToString("00") + ".py"), (i * 1337).ToString("N0") + " B", "2026-01-15 10:30" });
            return r;
        }

        static List<Scenario> Scenarios()
        {
            var l = new List<Scenario>();
            l.Add(new Scenario { Name = "main-actions", Build = () => { var f = Create<MainForm>(WorkFolder()); Show(f); Invoke(f, "ShowPage", true); return f; } });
            l.Add(new Scenario { Name = "main-menu", Build = () => { var f = Create<MainForm>(WorkFolder()); Show(f); Invoke(f, "ShowPage", false); return f; } });
            l.Add(new Scenario { Name = "first-run", Build = () => Show(new FirstRunForm()) });
            l.Add(new Scenario { Name = "results-grid", Build = () => Show(new ResultsForm("Largest files  -  " + WorkFolder(), SampleResult())) });
            l.Add(new Scenario { Name = "history", Build = () => Show(new UndoHistoryForm()) });
            l.Add(new Scenario { Name = "preview", Build = () => Show(new PreviewForm(Settings.DefaultState())) });
            l.Add(new Scenario { Name = "recipes", Build = () => Show(new RecipesForm()) });
            l.Add(new Scenario { Name = "structure", Build = () => Show(new StructureForm(WorkFolder())) });
            l.Add(new Scenario { Name = "search", Build = () => Show(new SearchForm(WorkFolder())) });
            l.Add(new Scenario { Name = "rename", Build = () => Show(new RenameForm(WorkFolder())) });
            l.Add(new Scenario { Name = "library", Build = () => Show(new LibraryForm(WorkFolder())) });
            l.Add(new Scenario
            {
                Name = "palette",
                Build = () =>
                {
                    var owner = Create<MainForm>(WorkFolder());
                    Show(owner);
                    var p = new PaletteForm(owner, a => { }, new List<Suggestion>());
                    p.StartPosition = FormStartPosition.Manual;
                    p.Location = new Point(80, 120);
                    return p;
                }
            });
            l.Add(new Scenario { Name = "popup-root", Build = () => Popup(new PowerMenuOptions()) });
            l.Add(new Scenario { Name = "popup-drill", Build = () => Popup(new PowerMenuOptions { Drill = new[] { "Copy", "Paths" } }) });
            l.Add(new Scenario { Name = "popup-search", Build = () => Popup(new PowerMenuOptions { InitialSearch = "ta" }) });
            l.Add(new Scenario { Name = "popup-details", Build = () => Popup(new PowerMenuOptions { Details = true, Drill = new[] { "Copy" } }) });
            l.Add(new Scenario { Name = "popup-empty", Build = () => Popup(new PowerMenuOptions { InitialSearch = "zzqx" }) });
            l.Add(new Scenario { Name = "popup-many", Build = () => Popup(new PowerMenuOptions { InitialSearch = "e" }) });
            l.Add(new Scenario { Name = "dialog-info", Build = () => Show(Create<FDialog>("Explorer menu installed.\n\nWindows 11: right-click > Show more options.", "PS Tools", FDialog.Kind.Info, false)) });
            l.Add(new Scenario { Name = "dialog-question", Build = () => Show(Create<FDialog>("Delete everything under\nC:\\Users\\demo\\proj ?\n\nThis cannot be undone.", "PS Tools", FDialog.Kind.Question, true)) });
            l.Add(new Scenario { Name = "dialog-error", Build = () => Show(Create<FDialog>("Not found:\nC:\\Users\\demo\\missing", "PS Tools - error", FDialog.Kind.Error, false)) });
            l.Add(new Scenario
            {
                Name = "toast",
                Build = () =>
                {
                    var f = Create<Toast>("Copied: C:\\Users\\demo\\proj\\src", true);
                    f.StartPosition = FormStartPosition.Manual;
                    f.Location = new Point(40, 40);
                    f.Opacity = 1.0;
                    return f;
                }
            });
            return l;
        }

        static Form Popup(PowerMenuOptions opt)
        {
            opt.Harness = true;
            opt.Summary = @"C:\Users\demo\projects\app\src";
            opt.Location = new Point(40, 40);
            var q = new MenuQuery
            {
                Context = ContextKind.Folder, Paths = new[] { @"C:\Users\demo\projects\app\src" }, Catalog = ActionCatalog.Load(new CatalogInput { Builtins = Actions.All, ShippedPath = Path.Combine(Program.ExeDir, "actions.json"), LibraryPath = Path.Combine(Program.ExeDir, "commands.json") }),
                Favorites = new List<string> { "copy.path.quoted", "copy.tree.ascii", "archive.zip-smart", "inspect.space-map", "git.copy.branch" },
                Recent = new List<string> { "copy.tree.ascii", "search.large" },
                HasTool = t => true, IsDirectory = p => true
            };
            return new PowerMenu(q, opt);
        }

        static void Pump()
        {
            for (int i = 0; i < 3; i++)
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }
        }

        static Bitmap Grab(Form f, out string method)
        {
            int w = Math.Max(1, f.Width), h = Math.Max(1, f.Height);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                try { PrintWindow(f.Handle, hdc, 2); }
                finally { g.ReleaseHdc(hdc); }
            }
            method = "printwindow";
            if (MostlyUniform(bmp))
            {
                bmp.Dispose();
                bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                f.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
                method = "drawtobitmap";
            }
            return bmp;
        }

        static bool MostlyUniform(Bitmap bmp)
        {
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int count = bmp.Width * bmp.Height;
                var px = new int[count];
                Marshal.Copy(data.Scan0, px, 0, count);
                var freq = new Dictionary<int, int>();
                int sampled = 0;
                for (int i = 0; i < count; i += 3)
                {
                    int c;
                    freq.TryGetValue(px[i], out c);
                    freq[px[i]] = c + 1;
                    sampled++;
                }
                return sampled == 0 || freq.Values.Max() > sampled * 0.98;
            }
            finally { bmp.UnlockBits(data); }
        }

        static void Walk(Control parent, Point origin, List<Dictionary<string, object>> list, int depth, int parentId)
        {
            foreach (Control c in parent.Controls)
            {
                if (list.Count > 3000) return;
                Rectangle r = c.RectangleToScreen(c.ClientRectangle);
                var label = c as Label;
                int id = list.Count;
                var d = new Dictionary<string, object>
                {
                    { "id", id }, { "parentId", parentId },
                    { "padH", c.Padding.Horizontal }, { "padV", c.Padding.Vertical }, { "bold", c.Font.Bold },
                    { "name", string.IsNullOrEmpty(c.Name) ? c.GetType().Name : c.Name },
                    { "type", c.GetType().Name },
                    { "x", r.X - origin.X }, { "y", r.Y - origin.Y }, { "w", r.Width }, { "h", r.Height },
                    { "text", c.Text ?? "" },
                    { "visible", c.Visible && IsShown(c) }, { "enabled", c.Enabled },
                    { "font", c.Font.Name }, { "size", c.Font.SizeInPoints },
                    { "depth", depth },
                    { "parent", string.IsNullOrEmpty(parent.Name) ? parent.GetType().Name : parent.Name },
                    { "ellipsis", label != null && label.AutoEllipsis },
                    { "autosize", label != null && label.AutoSize },
                    { "scrolls", c is ScrollableControl && ((ScrollableControl)c).AutoScroll },
                    { "tag", c.Tag as string ?? "" },
                    { "dock", c.Dock.ToString() }
                };
                list.Add(d);
                if (c.HasChildren) Walk(c, origin, list, depth + 1, id);
            }
        }

        static bool IsShown(Control c)
        {
            for (Control p = c; p != null; p = p.Parent) if (!p.Visible) return false;
            return true;
        }

        static string Num(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        public static int Run(string[] args)
        {
            try { AttachConsole(-1); } catch { }
            string outDir = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : null;
            if (outDir == null) { Console.WriteLine("Usage: --screenshot <outDir> [--theme light|dark] [--accent RRGGBB] [--scale 1|1.25|1.5] [--only a,b] [--fixtures dir]"); return 2; }
            Directory.CreateDirectory(outDir);

            string theme = (TestTools.Option(args, "--theme") ?? "light").ToLowerInvariant();
            string accentText = TestTools.Option(args, "--accent") ?? "0067C0";
            float scale = 1f;
            string scaleText = TestTools.Option(args, "--scale");
            if (scaleText != null) float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out scale);
            if (scale < 1f) scale = 1f;
            string only = TestTools.Option(args, "--only");
            fixturesDir = TestTools.Option(args, "--fixtures");

            scratch = Path.Combine(Path.GetTempPath(), "pstools-shot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            Journal.DirOverride = Path.Combine(scratch, "data");
            Directory.CreateDirectory(Journal.DirOverride);

            log = new StreamWriter(Path.Combine(outDir, "harness." + theme + "." + Num(scale) + ".log"), false, new UTF8Encoding(false));
            Log("screen " + Screen.PrimaryScreen.Bounds.Width + "x" + Screen.PrimaryScreen.Bounds.Height + ", work area " + Screen.PrimaryScreen.WorkingArea.Width + "x" + Screen.PrimaryScreen.WorkingArea.Height);

            int rgb = Convert.ToInt32(accentText, 16);
            UI.AccentOverride = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            if (scale > 1.01f) UI.ForceScale(scale);
            UI.UsePalette(theme == "dark");

            var wanted = only == null ? null : new HashSet<string>(only.Split(',').Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
            var entries = new List<Dictionary<string, object>>();
            int failures = 0;

            foreach (var sc in Scenarios())
            {
                if (wanted != null && !wanted.Contains(sc.Name)) continue;
                Form f = null;
                try
                {
                    f = sc.Build();
                    UI.Apply(f);
                    f.Show();
                    Pump();
                    if (f is Toast) f.Opacity = 1.0;
                    Pump();
                    string method;
                    using (var bmp = Grab(f, out method))
                    {
                        string file = sc.Name + "." + theme + "." + Num(scale) + ".png";
                        bmp.Save(Path.Combine(outDir, file), ImageFormat.Png);
                        var controls = new List<Dictionary<string, object>>();
                        Point origin = f.Location;
                        Walk(f, origin, controls, 0, -1);
                        entries.Add(new Dictionary<string, object>
                        {
                            { "name", sc.Name }, { "file", file }, { "width", bmp.Width }, { "height", bmp.Height },
                            { "theme", theme }, { "scale", scale }, { "font", UI.Family }, { "method", method },
                            { "clientX", f.PointToScreen(Point.Empty).X - origin.X }, { "clientY", f.PointToScreen(Point.Empty).Y - origin.Y },
                            { "controls", controls }
                        });
                        Log("ok    " + file + "  " + bmp.Width + "x" + bmp.Height + "  " + method + "  " + controls.Count + " controls");
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    Log("FAIL  " + sc.Name + ": " + (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message);
                }
                finally
                {
                    try
                    {
                        foreach (Form open in Application.OpenForms.Cast<Form>().ToList()) { open.Close(); open.Dispose(); }
                        if (f != null && !f.IsDisposed) f.Dispose();
                    }
                    catch { }
                    Pump();
                }
            }

            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            File.WriteAllText(Path.Combine(outDir, "manifest." + theme + "." + Num(scale) + ".json"), ser.Serialize(entries), new UTF8Encoding(false));
            Log(string.Format("{0} screenshots, {1} failed, theme={2}, scale={3}, font={4}", entries.Count, failures, theme, Num(scale), UI.Family));
            log.Dispose();
            try { Directory.Delete(scratch, true); } catch { }
            return failures == 0 ? 0 : 1;
        }
    }
}
