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

        public static bool Dark;
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
            };
            Walk(f);
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
            if (lv != null) { lv.BackColor = Field; lv.ForeColor = Text; Native(lv); return; }
            var tv = c as TreeView;
            if (tv != null) { tv.BackColor = Field; tv.ForeColor = Text; tv.BorderStyle = BorderStyle.None; Native(tv); return; }
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
                return;
            }
            var sc = c as SplitContainer;
            if (sc != null) { sc.BackColor = Border; sc.Panel1.BackColor = Back; sc.Panel2.BackColor = Back; }
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

        Toast(string text)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = UI.Panel;
            ForeColor = UI.Text;
            Font = UI.Font;
            var l = new Label { Text = text, AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = UI.Text, Location = new Point(18, 12) };
            Controls.Add(l);
            ClientSize = new Size(l.PreferredSize.Width + 36, l.PreferredSize.Height + 24);
            Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(UI.Border), 0, 0, Width - 1, Height - 1);
            Shown += (s, e) =>
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(wa.Right - Width - 24, wa.Bottom - Height - 24);
            };
            timer.Tick += (s, e) => { timer.Stop(); Close(); };
            Load += (s, e) => timer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; }
        }

        public static void Run(string text)
        {
            Application.Run(new Toast(text));
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
