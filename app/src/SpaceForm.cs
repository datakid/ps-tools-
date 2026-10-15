using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace PSTools
{
    static class SpaceColors
    {
        public static readonly string[] Names = { "Folders", "Images", "Video", "Audio", "Archives", "Documents", "Code", "Programs", "Other" };

        static readonly Color[] Palette =
        {
            Color.FromArgb(71, 85, 112), Color.FromArgb(46, 125, 190), Color.FromArgb(142, 90, 190), Color.FromArgb(190, 90, 140), Color.FromArgb(201, 125, 40),
            Color.FromArgb(60, 150, 90), Color.FromArgb(30, 150, 150), Color.FromArgb(190, 70, 60), Color.FromArgb(110, 115, 120)
        };

        static readonly Dictionary<string, int> ByExt = Build();

        static Dictionary<string, int> Build()
        {
            var d = new Dictionary<string, int>();
            Action<int, string> add = (k, list) => { foreach (string e in list.Split(' ')) d["." + e] = k; };
            add(1, "png jpg jpeg gif bmp webp tif tiff heic svg ico raw cr2 nef psd");
            add(2, "mp4 mkv avi mov wmv webm flv m4v mpg mpeg");
            add(3, "mp3 wav flac aac ogg m4a wma opus");
            add(4, "zip rar 7z tar gz bz2 xz iso cab img vhd vhdx wim");
            add(5, "pdf doc docx xls xlsx ppt pptx txt md rtf csv odt ods epub");
            add(6, "cs js ts jsx tsx py java c cpp h hpp go rs rb php html css json xml yml yaml sql ps1 sh bat cmd log");
            add(7, "exe dll msi sys com scr");
            return d;
        }

        public static int Category(SpaceNode n)
        {
            if (n.IsGroup) return 8;
            if (n.IsDir) return 0;
            int k;
            return ByExt.TryGetValue(n.Ext, out k) ? k : 8;
        }

        public static Color For(SpaceNode n)
        {
            if (n.IsGroup) return Color.FromArgb(96, 100, 108);
            return Palette[Category(n)];
        }

        public static Color Of(int category) { return Palette[category]; }
    }

    class SpaceView : Control
    {
        public event Action<SpaceNode> Drilled;
        public event Action<SpaceNode> Activated;
        public event Action<SpaceNode, Point> TileMenu;
        public event Action<SpaceNode> Hovered;

        SpaceNode current;
        List<Tile> tiles = new List<Tile>();
        Tile hot, picked;
        string message = "";
        readonly Font small = new Font("Segoe UI", 8.5f);
        readonly Font bold = new Font("Segoe UI", 8.5f, FontStyle.Bold);

        public SpaceView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = true;
        }

        public SpaceNode Current
        {
            get { return current; }
            set { current = value; hot = picked = null; Relayout(); }
        }

        public string Message
        {
            get { return message; }
            set { message = value; Invalidate(); }
        }

        public int TileCount { get { return tiles.Count; } }
        public IList<Tile> Tiles { get { return tiles; } }

        void Relayout()
        {
            tiles = current == null || ClientSize.Width < 4 || ClientSize.Height < 4
                ? new List<Tile>()
                : Treemap.Layout(current, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 300);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        public void RenderTo(Bitmap bmp)
        {
            using (var g = Graphics.FromImage(bmp)) Draw(g, bmp.Size);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw(e.Graphics, ClientSize);
        }

        void Centered(Graphics g, Size size, string text)
        {
            using (var f = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(UI.Muted))
                g.DrawString(text, small, b, new RectangleF(0, 0, size.Width, size.Height), f);
        }

        void Draw(Graphics g, Size size)
        {
            g.Clear(UI.Back);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            if (current == null) { Centered(g, size, message); return; }
            if (tiles.Count == 0) { Centered(g, size, "Nothing here takes up space."); return; }
            foreach (var t in tiles) DrawTile(g, t);
            foreach (var t in new[] { picked, hot })
            {
                if (t == null) continue;
                var rc = RectangleF.Inflate(t.Rect, -1.5f, -1.5f);
                using (var p = new Pen(t == hot ? Color.White : UI.Accent, 2f)) g.DrawRectangle(p, rc.X, rc.Y, rc.Width, rc.Height);
            }
        }

        static Color Darker(Color c, float k)
        {
            return Color.FromArgb((int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }

        void DrawTile(Graphics g, Tile t)
        {
            var rc = RectangleF.Inflate(t.Rect, -1f, -1f);
            if (rc.Width < 1f || rc.Height < 1f) return;
            var n = t.Node;
            Color c = SpaceColors.For(n);
            if (n.IsDir && !n.IsGroup && rc.Width >= 110 && rc.Height >= 70)
            {
                using (var b = new SolidBrush(Darker(c, 0.75f))) g.FillRectangle(b, rc);
                var head = new RectangleF(rc.X, rc.Y, rc.Width, 20f);
                using (var b = new SolidBrush(c)) g.FillRectangle(b, head);
                Label(g, head, n, true);
                var inner = new RectangleF(rc.X + 3, rc.Y + 23, rc.Width - 6, rc.Height - 26);
                if (inner.Width > 8 && inner.Height > 8) Nested(g, n, inner, 1);
            }
            else
            {
                using (var b = new SolidBrush(c)) g.FillRectangle(b, rc);
                Label(g, rc, n, false);
            }
        }

        void Nested(Graphics g, SpaceNode n, RectangleF area, int depth)
        {
            foreach (var k in Treemap.Layout(n, area, 40))
            {
                var kr = RectangleF.Inflate(k.Rect, -0.5f, -0.5f);
                if (kr.Width < 1f || kr.Height < 1f) continue;
                bool deeper = depth < 3 && k.Node.IsDir && !k.Node.IsGroup && k.Node.Kids.Count > 0 && kr.Width > 8f && kr.Height > 8f;
                using (var b = new SolidBrush(deeper ? Darker(SpaceColors.For(k.Node), 0.7f) : SpaceColors.For(k.Node))) g.FillRectangle(b, kr);
                if (deeper) Nested(g, k.Node, RectangleF.Inflate(kr, -1.5f, -1.5f), depth + 1);
            }
        }

        void Label(Graphics g, RectangleF rc, SpaceNode n, bool header)
        {
            if (rc.Width < 56f) return;
            if (!header && rc.Height < 30f) return;
            using (var f = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var white = new SolidBrush(Color.White))
            using (var soft = new SolidBrush(Color.FromArgb(205, 255, 255, 255)))
            {
                if (header)
                {
                    g.DrawString(n.Name + "   " + Fs.Size(n.Size), bold, white, new RectangleF(rc.X + 5, rc.Y + 2, rc.Width - 8, 16), f);
                    return;
                }
                g.DrawString(n.Name, bold, white, new RectangleF(rc.X + 5, rc.Y + 4, rc.Width - 8, 16), f);
                if (rc.Height >= 46f) g.DrawString(Fs.Size(n.Size), small, soft, new RectangleF(rc.X + 5, rc.Y + 21, rc.Width - 8, 16), f);
            }
        }

        Tile Hit(Point p)
        {
            foreach (var t in tiles) if (t.Rect.Contains(p.X, p.Y)) return t;
            return null;
        }

        static bool CanEnter(SpaceNode n) { return n.IsDir && !n.IsGroup && n.Kids.Count > 0; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var t = Hit(e.Location);
            if (t != hot)
            {
                hot = t;
                Cursor = t != null && CanEnter(t.Node) ? Cursors.Hand : Cursors.Default;
                Invalidate();
                if (Hovered != null) Hovered(t == null ? null : t.Node);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot == null) return;
            hot = null;
            Invalidate();
            if (Hovered != null) Hovered(null);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            var t = Hit(e.Location);
            if (t == null) return;
            if (e.Button == MouseButtons.Right)
            {
                picked = t; Invalidate();
                if (TileMenu != null) TileMenu(t.Node, e.Location);
            }
            else if (e.Button == MouseButtons.Left)
            {
                if (CanEnter(t.Node)) { if (Drilled != null) Drilled(t.Node); }
                else { picked = t; Invalidate(); }
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            var t = Hit(e.Location);
            if (t != null && !t.Node.IsDir && !t.Node.IsGroup && Activated != null) Activated(t.Node);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { small.Dispose(); bold.Dispose(); }
            base.Dispose(disposing);
        }
    }

    class SpaceLegend : Control
    {
        public SpaceLegend()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 26;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(UI.Panel);
            float x = 12;
            using (var f = new Font("Segoe UI", 8.5f))
            using (var tb = new SolidBrush(UI.Muted))
                for (int i = 0; i < SpaceColors.Names.Length; i++)
                {
                    using (var b = new SolidBrush(SpaceColors.Of(i))) g.FillRectangle(b, x, 8, 10, 10);
                    float w = g.MeasureString(SpaceColors.Names[i], f).Width;
                    g.DrawString(SpaceColors.Names[i], f, tb, x + 14, 5);
                    x += 14 + w + 10;
                }
        }
    }

    class SpaceForm : Form
    {
        readonly string startPath;
        readonly SpaceView view = new SpaceView { Dock = DockStyle.Fill };
        readonly Label pathLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Font = UI.Bold, Padding = new Padding(10, 0, 0, 0) };
        readonly Label sizeLabel = new Label { Dock = DockStyle.Right, Width = 220, TextAlign = ContentAlignment.MiddleRight, ForeColor = UI.Muted };
        readonly Label hover = new Label { Dock = DockStyle.Bottom, Height = 30, ForeColor = UI.Muted, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(12, 0, 12, 0) };
        readonly Button up = new FButton { Text = "Up", Dock = DockStyle.Left, Width = 70 };
        readonly Button rescan = new FButton { Text = "Rescan", Dock = DockStyle.Right, Width = 100 };
        readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer { Interval = 200 };
        SpaceScan scan;
        SpaceNode root, current;
        bool scanning;

        public SpaceForm(string path)
        {
            startPath = path;
            Text = "Space map - " + path; Font = UI.Font; ClientSize = new Size(1000, 660); MinimumSize = new Size(640, 420); StartPosition = FormStartPosition.CenterScreen; KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var top = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(10, 8, 10, 6), BackColor = UI.Panel };
            top.Controls.Add(pathLabel); top.Controls.Add(up); top.Controls.Add(sizeLabel); top.Controls.Add(rescan);
            var legend = new SpaceLegend { Dock = DockStyle.Bottom };

            Controls.Add(view); Controls.Add(legend); Controls.Add(hover); Controls.Add(top);

            up.Click += (s, e) => GoUp();
            rescan.Click += (s, e) => { if (scanning) scan.Cancel = true; else StartScan(); };
            view.Drilled += n => Navigate(n);
            view.Activated += n => OpenItem(n);
            view.Hovered += n => ShowHover(n);
            view.TileMenu += (n, p) => ShowMenu(n, p);
            ticker.Tick += (s, e) =>
            {
                if (scanning && scan != null) view.Message = "Scanning " + startPath + "\n\n" + scan.FilesSeen.ToString("N0") + " files, " + Fs.Size(scan.BytesSeen);
            };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Back || (e.Alt && e.KeyCode == Keys.Left)) { GoUp(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.F5 && !scanning) { StartScan(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { Close(); e.SuppressKeyPress = true; }
            };
            FormClosing += (s, e) => { if (scan != null) scan.Cancel = true; ticker.Stop(); };

            UI.Apply(this);
            up.Enabled = false;
            Shown += (s, e) => StartScan();
        }

        void StartScan()
        {
            if (scan != null) scan.Cancel = true;
            var mine = new SpaceScan();
            scan = mine;
            scanning = true;
            rescan.Text = "Cancel";
            up.Enabled = false;
            sizeLabel.Text = "";
            hover.Text = "Links and junctions are skipped. Large folders can take a while; Cancel shows what was found so far.";
            view.Current = null;
            view.Message = "Scanning " + startPath;
            ticker.Start();
            var t = new Thread(() =>
            {
                SpaceNode result = null;
                try { result = mine.Run(startPath); } catch { }
                try { BeginInvoke((MethodInvoker)(() => Finished(mine, result))); } catch { }
            }) { IsBackground = true };
            t.Start();
        }

        void Finished(SpaceScan done, SpaceNode result)
        {
            if (done != scan) return;
            scanning = false;
            ticker.Stop();
            rescan.Text = "Rescan";
            root = result;
            if (root == null) { view.Message = "Could not read this folder."; return; }
            Navigate(root);
            hover.Text = done.Cancelled ? "Scan cancelled. This map is partial." : "Click a folder to open it, right-click for more. Backspace goes up.";
        }

        void Navigate(SpaceNode n)
        {
            current = n;
            view.Current = n;
            pathLabel.Text = n.Path;
            sizeLabel.Text = Fs.Size(n.Size) + "   " + n.Files.ToString("N0") + " files";
            up.Enabled = n.Parent != null;
        }

        void GoUp()
        {
            if (current != null && current.Parent != null) Navigate(current.Parent);
        }

        void ShowHover(SpaceNode n)
        {
            if (n == null || current == null) return;
            double pct = current.Size > 0 ? n.Size * 100.0 / current.Size : 0;
            hover.Text = n.Name + "   " + Fs.Size(n.Size) + "   " + pct.ToString("0.#") + "% of this folder"
                + (n.IsDir || n.IsGroup ? "   " + n.Files.ToString("N0") + " files" : "") + "   " + n.Path;
        }

        void OpenItem(SpaceNode n)
        {
            try { Process.Start(new ProcessStartInfo(n.Path) { UseShellExecute = true }); }
            catch (Exception ex) { Program.Fail("Could not open it:\n" + ex.Message); }
        }

        void ShowMenu(SpaceNode n, Point at)
        {
            var m = new ContextMenuStrip();
            var open = m.Items.Add(n.IsDir && !n.IsGroup ? "Open this folder in the map" : "Open");
            open.Enabled = !n.IsGroup && (!n.IsDir || n.Kids.Count > 0);
            open.Click += (s, e) => { if (n.IsDir) Navigate(n); else OpenItem(n); };
            var show = m.Items.Add("Show in Explorer");
            show.Click += (s, e) =>
            {
                try { Process.Start("explorer.exe", n.IsDir || n.IsGroup ? "\"" + n.Path + "\"" : "/select,\"" + n.Path + "\""); } catch { }
            };
            var copy = m.Items.Add("Copy path");
            copy.Click += (s, e) => { try { Clipboard.SetText(n.Path); } catch { } };
            m.Items.Add(new ToolStripSeparator());
            var del = m.Items.Add("Move to Recycle Bin...");
            del.Enabled = !n.IsGroup && n != root;
            del.Click += (s, e) => Recycle(n);
            m.Show(view, at);
        }

        void Recycle(SpaceNode n)
        {
            if (!Program.Ask("Move to the Recycle Bin?\n\n" + n.Path + "\n" + Fs.Size(n.Size) + (n.IsDir ? "   " + n.Files.ToString("N0") + " files" : "")
                + "\n\nYou can restore it from the Recycle Bin.")) return;
            try { Fs.Recycle(n.Path); }
            catch (Exception ex) { Program.Fail("Could not move it to the Recycle Bin:\n" + ex.Message); return; }
            var p = n.Parent;
            if (p == null) return;
            p.Kids.Remove(n);
            for (var a = p; a != null; a = a.Parent) { a.Size -= n.Size; a.Files -= n.Files; }
            Navigate(current);
            hover.Text = "Moved " + n.Name + " to the Recycle Bin (" + Fs.Size(n.Size) + ").";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { ticker.Dispose(); if (scan != null) scan.Cancel = true; }
            base.Dispose(disposing);
        }
    }
}
