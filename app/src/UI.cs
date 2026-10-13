using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PSTools
{
    static class UI
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp);

        [DllImport("user32.dll")]
        static extern bool ShowScrollBar(IntPtr hwnd, int bar, bool show);

        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(Point pt);

        class WheelFilter : IMessageFilter
        {
            readonly Panel panel;
            public WheelFilter(Panel p) { panel = p; }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x020A || !panel.IsHandleCreated || !panel.Visible || !panel.VerticalScroll.Visible) return false;
                long lp = m.LParam.ToInt64();
                var pt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                Control c = Control.FromHandle(WindowFromPoint(pt));
                while (c != null && c != panel) c = c.Parent;
                if (c == null) return false;
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                int y = -panel.AutoScrollPosition.Y - delta * 48 / 120;
                panel.AutoScrollPosition = new Point(0, Math.Max(0, y));
                return true;
            }
        }

        public static void WheelScroll(Panel p)
        {
            var f = new WheelFilter(p);
            Application.AddMessageFilter(f);
            p.Disposed += (s, e) => Application.RemoveMessageFilter(f);
        }

        public static bool Dark;
        static float scale = -1f;
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, object> scaledForms = new System.Runtime.CompilerServices.ConditionalWeakTable<Form, object>();

        public static float Scale
        {
            get
            {
                if (scale < 0)
                {
                    try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f; }
                    catch { scale = 1f; }
                    if (scale < 1.01f) scale = 1f;
                }
                return scale;
            }
        }

        public static int S(int v) { return (int)Math.Round(v * Scale); }
        public static Font Font = new Font("Segoe UI", 9f);
        public static Font Bold = new Font("Segoe UI Semibold", 9f);
        public static Font Head = new Font("Segoe UI Semibold", 11f);
        public static Font Title = new Font("Segoe UI Semibold", 14f);
        public static Font Mono = new Font("Consolas", 10f);
        public static Color Accent = Color.FromArgb(0, 103, 192);
        public static Color Back = Color.White;
        public static Color Panel = Color.FromArgb(243, 243, 243);
        public static Color Field = Color.White;
        public static Color Text = Color.FromArgb(28, 28, 28);
        public static Color Muted = Color.FromArgb(96, 96, 96);
        public static Color Border = Color.FromArgb(214, 214, 214);
        public static Color Danger = Color.FromArgb(196, 43, 28);

        public static void Init()
        {
            Settings.PreloadTheme();
            string t = Settings.Theme;
            Dark = t == "dark" || (t != "light" && SystemDark());
            if (Dark)
            {
                Accent = Color.FromArgb(76, 160, 255);
                Back = Color.FromArgb(32, 32, 32);
                Panel = Color.FromArgb(43, 43, 43);
                Field = Color.FromArgb(50, 50, 50);
                Text = Color.FromArgb(240, 240, 240);
                Muted = Color.FromArgb(170, 170, 170);
                Border = Color.FromArgb(72, 72, 72);
                Danger = Color.FromArgb(255, 120, 110);
            }
        }

        static bool SystemDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        public static Color OnAccent { get { return Dark ? Color.FromArgb(10, 10, 10) : Color.White; } }

        public static void Primary(Button b)
        {
            b.Tag = "accent";
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Accent;
            b.ForeColor = OnAccent;
            b.Font = Bold;
            b.UseVisualStyleBackColor = false;
            Color target = Dark ? Color.White : Color.Black;
            b.FlatAppearance.MouseOverBackColor = Blend(Accent, target, 0.12f);
            b.FlatAppearance.MouseDownBackColor = Blend(Accent, target, 0.24f);
        }

        static int Rgb(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static int TextHeight(Label l, int min)
        {
            int w = l.ClientSize.Width;
            if (w < 40 && l.Parent != null) w = l.Parent.ClientSize.Width;
            if (w < 40) return min;
            Size sz = TextRenderer.MeasureText(l.Text ?? "", l.Font, new Size(w, 100000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return Math.Max(min, sz.Height + 8);
        }

        public static void FitScroll(Panel p)
        {
            int h = 0;
            foreach (Control c in p.Controls) if (c.Dock == DockStyle.Top) h += c.Height;
            if (p.AutoScrollMinSize.Height != h) p.AutoScrollMinSize = new Size(0, h);
        }

        public static void HideHScroll(Control c)
        {
            try { if (c.IsHandleCreated) ShowScrollBar(c.Handle, 0, false); } catch { }
        }

        public static void Round(Form f)
        {
            f.HandleCreated += (s, e) =>
            {
                int pref = 2;
                try { DwmSetWindowAttribute(f.Handle, 33, ref pref, 4); } catch { }
            };
        }

        public static void Apply(Form f)
        {
            f.BackColor = Back;
            f.ForeColor = Text;
            f.HandleCreated += (s, e) =>
            {
                int on = Dark ? 1 : 0;
                try { DwmSetWindowAttribute(f.Handle, 20, ref on, 4); } catch { }
                try { DwmSetWindowAttribute(f.Handle, 19, ref on, 4); } catch { }
                if (!Tweaks.IsWindows11) return;
                int cap = Rgb(Panel), txt = Rgb(Text), brd = Rgb(Border);
                try { DwmSetWindowAttribute(f.Handle, 35, ref cap, 4); } catch { }
                try { DwmSetWindowAttribute(f.Handle, 36, ref txt, 4); } catch { }
                try { DwmSetWindowAttribute(f.Handle, 34, ref brd, 4); } catch { }
            };
            Walk(f);
            object seen;
            if (Scale > 1f && !scaledForms.TryGetValue(f, out seen))
            {
                scaledForms.Add(f, new object());
                f.Scale(new SizeF(Scale, Scale));
                ScaleColumns(f);
            }
        }

        static void ScaleColumns(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                var lv = c as ListView;
                if (lv != null) foreach (ColumnHeader h in lv.Columns) h.Width = S(h.Width);
                if (c.HasChildren) ScaleColumns(c);
            }
        }

        public static void Walk(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                Style(c);
                if (c.HasChildren) Walk(c);
            }
        }

        public static void Style(Control c)
        {
            var tb = c as TextBox;
            if (tb != null)
            {
                if (tb.Tag as string != "code") { tb.BackColor = Field; tb.ForeColor = Text; }
                tb.BorderStyle = BorderStyle.FixedSingle;
                return;
            }
            var lv = c as ListView;
            if (lv != null) { lv.BackColor = Field; lv.ForeColor = Text; Native(lv); Smooth(lv, 0x1036, 0x10000); return; }
            var tv = c as TreeView;
            if (tv != null) { tv.BackColor = Field; tv.ForeColor = Text; tv.BorderStyle = BorderStyle.None; Native(tv); Smooth(tv, 0x112C, 0x24); return; }
            var cb = c as ComboBox;
            if (cb != null) { cb.BackColor = Field; cb.ForeColor = Text; cb.FlatStyle = FlatStyle.Flat; return; }
            var nu = c as NumericUpDown;
            if (nu != null) { nu.BackColor = Field; nu.ForeColor = Text; return; }
            var b = c as Button;
            if (b != null)
            {
                if (b.Tag as string == "accent") return;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = Border;
                b.FlatAppearance.BorderSize = 1;
                b.UseVisualStyleBackColor = false;
                b.BackColor = Dark ? Color.FromArgb(58, 58, 58) : Color.White;
                b.ForeColor = b.ForeColor == Danger ? Danger : Text;
                Color target = Dark ? Color.White : Color.Black;
                b.FlatAppearance.MouseOverBackColor = Blend(b.BackColor, target, 0.07f);
                b.FlatAppearance.MouseDownBackColor = Blend(b.BackColor, target, 0.14f);
                return;
            }
            var pn = c as Panel;
            if (pn != null && pn.AutoScroll) { Native(pn); return; }
            var sc = c as SplitContainer;
            if (sc != null) { sc.BackColor = Border; sc.Panel1.BackColor = Back; sc.Panel2.BackColor = Back; }
        }

        static void Smooth(Control c, int msg, int flags)
        {
            EventHandler apply = (s, e) =>
            {
                try { SendMessage(c.Handle, msg, (IntPtr)flags, (IntPtr)flags); } catch { }
            };
            if (c.IsHandleCreated) apply(c, EventArgs.Empty); else c.HandleCreated += apply;
        }

        static void Native(Control c)
        {
            EventHandler apply = (s, e) =>
            {
                try { SetWindowTheme(c.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
            };
            if (c.IsHandleCreated) apply(c, EventArgs.Empty); else c.HandleCreated += apply;
        }
    }

    class Toast : Form
    {
        readonly Timer timer = new Timer { Interval = 1700 };
        readonly Timer fade = new Timer { Interval = 15 };
        readonly Label text;
        LinkLabel undo;
        bool fadingOut;

        Toast(string message, bool undoable)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = UI.Panel;
            ForeColor = UI.Text;
            Font = UI.Font;
            text = new Label { Text = message, AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = UI.Text, Location = new Point(18, 12) };
            Controls.Add(text);
            if (undoable)
            {
                timer.Interval = 5000;
                undo = new LinkLabel { Text = "Undo", AutoSize = true, LinkColor = UI.Accent, ActiveLinkColor = UI.Accent, Cursor = Cursors.Hand };
                undo.LinkClicked += (s, e) => DoUndo();
                Controls.Add(undo);
            }
            Fit();
            Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(UI.Border), 0, 0, Width - 1, Height - 1);
            Shown += (s, e) => Place();
            UI.Round(this);
            Opacity = 0;
            fade.Tick += (s, e) =>
            {
                if (fadingOut)
                {
                    Opacity = Math.Max(0, Opacity - 0.1);
                    if (Opacity <= 0.05) { fade.Stop(); Close(); }
                }
                else
                {
                    Opacity = Math.Min(1, Opacity + 0.12);
                    if (Opacity >= 1) fade.Stop();
                }
            };
            timer.Tick += (s, e) => { timer.Stop(); fadingOut = true; fade.Start(); };
            Load += (s, e) => { timer.Start(); fade.Start(); };
        }

        void Fit()
        {
            int w = text.PreferredSize.Width + 36;
            int h = text.PreferredSize.Height + 24;
            if (undo != null)
            {
                undo.Location = new Point(18, 12 + text.PreferredSize.Height + 6);
                h += undo.PreferredSize.Height + 6;
                w = Math.Max(w, undo.PreferredSize.Width + 36);
            }
            ClientSize = new Size(w, h);
        }

        void Place()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 24, wa.Bottom - Height - 24);
        }

        void DoUndo()
        {
            timer.Stop();
            fade.Stop();
            fadingOut = false;
            Opacity = 1;
            string result = Journal.Undo();
            Controls.Remove(undo);
            undo = null;
            text.Text = result;
            Fit();
            Place();
            timer.Interval = 2500;
            timer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; }
        }

        public static void Run(string message, bool undoable)
        {
            Application.Run(new Toast(message, undoable));
        }
    }

    static class Clip
    {
        public static void Set(string text)
        {
            if (text == null) return;
            for (int i = 0; i < 5; i++)
            {
                try { Clipboard.SetDataObject(text, true, 3, 80); return; }
                catch { System.Threading.Thread.Sleep(80); }
            }
        }
    }
}
