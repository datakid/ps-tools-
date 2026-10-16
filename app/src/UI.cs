using System;
using System.Drawing;
using System.Linq;
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
        public const string Version = "3.0.0";

        static string Pick(params string[] names)
        {
            foreach (string n in names)
            {
                try { using (new FontFamily(n)) return n; }
                catch (ArgumentException) { }
            }
            return "Segoe UI";
        }

        public static readonly string Family = Pick("Segoe UI Variable Text", "Segoe UI");
        public static string IconFamily = PickIcons();

        static string PickIcons()
        {
            foreach (string n in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
            {
                try { using (new FontFamily(n)) return n; }
                catch (ArgumentException) { }
            }
            return null;
        }

        public static Font Font = new Font(Family, 10f);
        public static Font Small = new Font(Family, 9f);
        public static Font Bold = new Font("Segoe UI Semibold", 10f);
        public static Font SmallBold = new Font("Segoe UI Semibold", 9f);
        public static Font Head = new Font("Segoe UI Semibold", 12f);
        public static Font Title = new Font("Segoe UI Semibold", 15f);
        public static Font Mono = new Font(Pick("Cascadia Mono", "Consolas"), 10f);
        public static Font Icons(float size) { return new Font(IconFamily ?? "Segoe UI", size); }

        public static Color Accent = Color.FromArgb(0, 103, 192);
        public static Color Back = Color.FromArgb(243, 243, 243);
        public static Color Panel = Color.FromArgb(243, 243, 243);
        public static Color Card = Color.White;
        public static Color Field = Color.FromArgb(251, 251, 251);
        public static Color Text = Color.FromArgb(26, 26, 26);
        public static Color Muted = Color.FromArgb(93, 93, 93);
        public static Color Border = Color.FromArgb(229, 229, 229);
        public static Color Danger = Color.FromArgb(196, 43, 28);
        public static Color CtrlFill = Color.FromArgb(251, 251, 251);
        public static Color CtrlHover = Color.FromArgb(246, 246, 246);
        public static Color CtrlDown = Color.FromArgb(240, 240, 240);
        public static Color CtrlStroke = Color.FromArgb(222, 222, 222);
        public static Color ListHover = Color.FromArgb(245, 245, 245);
        public static Color ListSelected = Color.FromArgb(236, 236, 236);

        public static void Init()
        {
            Settings.PreloadTheme();
            string t = Settings.Theme;
            UsePalette(t == "dark" || (t != "light" && SystemDark()));
        }

        public static void UsePalette(bool dark)
        {
            Dark = dark;
            Color sys = SystemAccent();
            float h, s, l;
            ToHsl(sys, out h, out s, out l);
            if (dark)
            {
                Accent = FromHsl(h, s, Math.Min(0.72f, Math.Max(l + 0.27f, 0.62f)));
                Back = Color.FromArgb(32, 32, 32);
                Panel = Back;
                Card = Color.FromArgb(43, 43, 43);
                Field = Color.FromArgb(45, 45, 45);
                Text = Color.FromArgb(255, 255, 255);
                Muted = Color.FromArgb(197, 197, 197);
                Border = Color.FromArgb(58, 58, 58);
                Danger = Color.FromArgb(255, 153, 164);
                CtrlFill = Color.FromArgb(50, 50, 50);
                CtrlHover = Color.FromArgb(56, 56, 56);
                CtrlDown = Color.FromArgb(42, 42, 42);
                CtrlStroke = Color.FromArgb(66, 66, 66);
                ListHover = Color.FromArgb(55, 55, 55);
                ListSelected = Color.FromArgb(63, 63, 63);
            }
            else
            {
                Accent = FromHsl(h, s, Math.Max(0.30f, Math.Min(l - 0.04f, 0.45f)));
                Back = Color.FromArgb(243, 243, 243);
                Panel = Back;
                Card = Color.White;
                Field = Color.FromArgb(251, 251, 251);
                Text = Color.FromArgb(26, 26, 26);
                Muted = Color.FromArgb(93, 93, 93);
                Border = Color.FromArgb(229, 229, 229);
                Danger = Color.FromArgb(196, 43, 28);
                CtrlFill = Color.FromArgb(251, 251, 251);
                CtrlHover = Color.FromArgb(246, 246, 246);
                CtrlDown = Color.FromArgb(240, 240, 240);
                CtrlStroke = Color.FromArgb(222, 222, 222);
                ListHover = Color.FromArgb(245, 245, 245);
                ListSelected = Color.FromArgb(236, 236, 236);
            }
        }

        static Color SystemAccent()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    object v = k == null ? null : k.GetValue("AccentColor");
                    if (v is int)
                    {
                        int c = (int)v;
                        return Color.FromArgb(c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF);
                    }
                }
            }
            catch { }
            return Color.FromArgb(0, 120, 212);
        }

        static void ToHsl(Color c, out float h, out float s, out float l)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2f;
            float d = max - min;
            if (d < 0.0001f) { h = 0; s = 0; return; }
            s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6f : 0f);
            else if (max == g) h = (b - r) / d + 2f;
            else h = (r - g) / d + 4f;
            h /= 6f;
        }

        static float Hue(float p, float q, float t)
        {
            if (t < 0) t += 1f;
            if (t > 1) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 0.5f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        static Color FromHsl(float h, float s, float l)
        {
            if (s < 0.0001f) { int v = (int)Math.Round(l * 255); return Color.FromArgb(v, v, v); }
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s, p = 2f * l - q;
            return Color.FromArgb((int)Math.Round(Hue(p, q, h + 1f / 3f) * 255), (int)Math.Round(Hue(p, q, h) * 255), (int)Math.Round(Hue(p, q, h - 1f / 3f) * 255));
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
            var fb = b as FButton;
            if (fb != null) { fb.Kind = FButton.Look.Accent; return; }
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Accent;
            b.ForeColor = OnAccent;
            b.Font = Bold;
            b.UseVisualStyleBackColor = false;
        }

        static int Rgb(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static int MeasureHeight(string text, Font font, int width)
        {
            return TextRenderer.MeasureText(text ?? "", font, new Size(Math.Max(20, width), 100000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        }

        public static int TextHeight(Label l, int min)
        {
            int w = l.ClientSize.Width;
            if (w < 40 && l.Parent != null) w = l.Parent.ClientSize.Width;
            if (w < 40) return min;
            Size sz = TextRenderer.MeasureText(l.Text ?? "", l.Font, new Size(w - l.Padding.Horizontal, 100000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return Math.Max(min, sz.Height + l.Padding.Vertical + 8);
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

        public static void Apply(Form f) { Apply(f, true); }

        public static void Apply(Form f, bool scaleLayout)
        {
            f.BackColor = Back;
            f.ForeColor = Text;
            f.HandleCreated += (s, e) =>
            {
                int on = Dark ? 1 : 0;
                try { DwmSetWindowAttribute(f.Handle, 20, ref on, 4); } catch { }
                try { DwmSetWindowAttribute(f.Handle, 19, ref on, 4); } catch { }
                if (!Tweaks.IsWindows11) return;
                int cap = Rgb(Back), txt = Rgb(Text), brd = Rgb(Border);
                try { DwmSetWindowAttribute(f.Handle, 35, ref cap, 4); } catch { }
                try { DwmSetWindowAttribute(f.Handle, 36, ref txt, 4); } catch { }
                try { DwmSetWindowAttribute(f.Handle, 34, ref brd, 4); } catch { }
            };
            Walk(f);
            object seen;
            if (scaleLayout && Scale > 1f && !scaledForms.TryGetValue(f, out seen))
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
            foreach (Control c in parent.Controls.Cast<Control>().ToArray())
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
                tb.BackColor = Field; tb.ForeColor = Text;
                tb.BorderStyle = BorderStyle.None;
                if (tb.Multiline) Native(tb);
                Fx.Wrap(tb, true);
                return;
            }
            var lv = c as ListView;
            if (lv != null)
            {
                lv.BackColor = Card; lv.ForeColor = Text; lv.BorderStyle = BorderStyle.None;
                Native(lv); Smooth(lv, 0x1036, 0x10000);
                if (!lv.CheckBoxes && lv.View == View.Details) FList.Attach(lv, 34, null);
                Fx.Wrap(lv, false);
                return;
            }
            var tv = c as TreeView;
            if (tv != null)
            {
                tv.BackColor = Card; tv.ForeColor = Text; tv.BorderStyle = BorderStyle.None;
                Native(tv); Smooth(tv, 0x112C, 0x24);
                Fx.Wrap(tv, false);
                return;
            }
            var cb = c as ComboBox;
            if (cb != null) { cb.BackColor = Field; cb.ForeColor = Text; cb.FlatStyle = FlatStyle.Standard; ThemeCombo(cb); return; }
            var nu = c as NumericUpDown;
            if (nu != null) { nu.BackColor = Field; nu.ForeColor = Text; Native(nu); return; }
            var b = c as Button;
            if (b != null)
            {
                if (b is FButton || b.Tag as string == "accent") return;
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
            if (sc != null) { sc.BackColor = Back; sc.Panel1.BackColor = Back; sc.Panel2.BackColor = Back; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ComboRect { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct ComboInfo
        {
            public int Size;
            public ComboRect Item, Button;
            public int State;
            public IntPtr Combo, Edit, List;
        }

        [DllImport("user32.dll")]
        static extern bool GetComboBoxInfo(IntPtr hwnd, ref ComboInfo info);

        static void ThemeCombo(ComboBox cb)
        {
            if (!Dark) return;
            EventHandler apply = (s, e) =>
            {
                try
                {
                    SetWindowTheme(cb.Handle, "DarkMode_CFD", null);
                    var info = new ComboInfo();
                    info.Size = Marshal.SizeOf(typeof(ComboInfo));
                    if (GetComboBoxInfo(cb.Handle, ref info) && info.List != IntPtr.Zero) SetWindowTheme(info.List, "DarkMode_Explorer", null);
                }
                catch { }
            };
            if (cb.IsHandleCreated) apply(cb, EventArgs.Empty); else cb.HandleCreated += apply;
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
            BackColor = UI.Card;
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
