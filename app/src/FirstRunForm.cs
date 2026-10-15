using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class FirstRunForm : Form
    {
        readonly List<RadioButton> choices = new List<RadioButton>();
        readonly CheckBox classic = new CheckBox { AutoSize = false, Checked = true };
        bool installed;

        public FirstRunForm()
        {
            bool win11 = Tweaks.IsWindows11;
            Text = "PS Tools"; Font = UI.Font; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            int y = 20;
            Controls.Add(new Label { Text = "Add PS Tools to your right-click menu", Font = UI.Title, AutoSize = true, Location = new Point(24, y) });
            y += 42;
            string intro = "Pick how much goes in the menu. You can change it any time on the Menu page. Nothing runs in the background, and Full uninstall puts everything back.";
            int introH = UI.MeasureHeight(intro, UI.Font, 520) + 6;
            Controls.Add(new Label { Text = intro, ForeColor = UI.Muted, Location = new Point(26, y), Size = new Size(520, introH) });
            y += introH + 16;

            foreach (var name in Presets.Names)
            {
                var rb = new RadioButton { Text = name, Font = UI.Bold, AutoSize = true, Location = new Point(28, y), Checked = name == "Everyday", Tag = name };
                choices.Add(rb);
                Controls.Add(rb);
                string desc = Presets.Describe(name);
                int dh = UI.MeasureHeight(desc, UI.Font, 496) + 6;
                Controls.Add(new Label { Text = desc, ForeColor = UI.Muted, Location = new Point(50, y + 26), Size = new Size(496, dh) });
                y += 26 + dh + 14;
            }

            if (win11)
            {
                classic.Text = "Use the classic right-click menu. Windows 11 hides PS Tools behind \"Show more options\" otherwise.";
                int ch = UI.MeasureHeight(classic.Text, UI.Font, 490) + 8;
                classic.Location = new Point(28, y + 4);
                classic.Size = new Size(520, Math.Max(28, ch));
                Controls.Add(classic);
                y += classic.Height + 12;
            }

            var install = new FButton { Text = "Install", Width = 130, Height = 34, Location = new Point(28, y + 10) };
            UI.Primary(install);
            install.Click += (s, e) => DoInstall();
            var later = new FButton { Text = "Not now", Width = 110, Height = 34, Location = new Point(168, y + 10) };
            later.Click += (s, e) => Close();
            var preview = new FButton { Text = "Preview menu", Width = 130, Height = 34, Location = new Point(288, y + 10) };
            preview.Click += (s, e) => ShowPreview();
            Controls.Add(install); Controls.Add(later); Controls.Add(preview);
            AcceptButton = install; CancelButton = later;
            ClientSize = new Size(570, y + 64);
            UI.Apply(this);
            classic.ForeColor = UI.Text;
            FormClosed += (s, e) => { if (!installed) { try { Settings.MarkConfigured(); } catch { } } };
        }

        void ShowPreview()
        {
            var ids = Presets.Ids(Chosen());
            var favs = new HashSet<string>(Actions.All.Where(a => a.DefaultFav && ids.Contains(a.Id)).Select(a => a.Id));
            using (var f = new PreviewForm(ids, favs)) f.ShowDialog(this);
        }

        string Chosen()
        {
            foreach (var rb in choices) if (rb.Checked) return (string)rb.Tag;
            return "Everyday";
        }

        void DoInstall()
        {
            try
            {
                Settings.ApplyPreset(Chosen());
                Shell.Install();
                installed = true;
                bool restart = false;
                if (classic.Parent != null && classic.Checked && !Tweaks.Classic) { Tweaks.ToggleClassic(); restart = true; }
                if (restart && Program.Ask("The classic menu needs Explorer to restart.\n\nRestart Explorer now? Open File Explorer windows will close."))
                    Actions.RestartExplorerNow();
                Close();
            }
            catch (Exception ex) { Program.Fail("Could not install the menu:\n" + ex.Message); }
        }
    }
}
