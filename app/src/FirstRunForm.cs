using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class FirstRunForm : Form
    {
        readonly List<FToggle> choices = new List<FToggle>();
        readonly FToggle classic = new FCheck { AutoSize = true, Checked = true };
        bool installed;
        readonly bool again;

        public FirstRunForm() : this(false) { }

        public FirstRunForm(bool reopened)
        {
            again = reopened;
            bool win11 = Tweaks.IsWindows11;
            Text = "PS Tools"; Font = UI.Font; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            int content = UI.S(540);
            var table = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(UI.S(24), UI.S(20), UI.S(24), UI.S(20)), Dock = DockStyle.Fill };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, content));

            Action<Control, Padding> add = (c, margin) =>
            {
                c.Margin = margin;
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                table.Controls.Add(c, 0, table.RowStyles.Count - 1);
            };

            add(new Label { Text = again ? "Set up the right-click menu" : "Add PS Tools to your right-click menu", Font = UI.Title, AutoSize = true, MaximumSize = new Size(content, 0) }, new Padding(0, 0, 0, UI.S(10)));
            string intro = again
                ? "Choose a starting point for the right-click menu. Your pinned actions are kept if you want them to be; fine-tune afterwards on the Menu page. Nothing runs in the background, and Full uninstall puts everything back."
                : "Pick how much goes in the menu. You can change it any time on the Menu page. Nothing runs in the background, and Full uninstall puts everything back.";
            string start = again && Array.IndexOf(Presets.Names, Settings.CurrentPreset) >= 0 ? Settings.CurrentPreset : "Everyday";
            add(new Label { Text = intro, ForeColor = UI.Muted, AutoSize = true, MaximumSize = new Size(content, 0) }, new Padding(0, 0, 0, UI.S(14)));

            foreach (var name in Presets.Names)
            {
                var rb = new FRadio { Text = name, Font = UI.Bold, AutoSize = true, Checked = name == start, Tag = name };
                choices.Add(rb);
                add(rb, new Padding(0, UI.S(2), 0, 0));
                add(new Label { Text = Presets.Describe(name), ForeColor = UI.Muted, AutoSize = true, MaximumSize = new Size(content - UI.S(30), 0) }, new Padding(UI.S(30), 0, 0, UI.S(10)));
            }

            if (win11)
            {
                classic.Checked = again ? Tweaks.Classic : true;
                classic.Text = "Use the classic right-click menu. Windows 11 hides PS Tools behind \"Show more options\" otherwise.";
                classic.MaximumSize = new Size(content, 0);
                add(classic, new Padding(0, UI.S(4), 0, UI.S(6)));
            }

            var install = new FButton { Text = again ? "Apply" : "Install", Width = UI.S(130), Height = UI.S(34), Margin = new Padding(0, 0, UI.S(10), 0) };
            UI.Primary(install);
            install.Click += (s, e) => DoInstall();
            var later = new FButton { Text = again ? "Close" : "Not now", Width = UI.S(110), Height = UI.S(34), Margin = new Padding(0, 0, UI.S(10), 0) };
            later.Click += (s, e) => Close();
            var preview = new FButton { Text = "Preview menu", Width = UI.S(130), Height = UI.S(34), Margin = new Padding(0) };
            preview.Click += (s, e) => ShowPreview();
            var buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
            buttons.Controls.Add(install); buttons.Controls.Add(later); buttons.Controls.Add(preview);
            add(buttons, new Padding(0, UI.S(10), 0, 0));

            Controls.Add(table);
            AcceptButton = install; CancelButton = later;
            UI.Apply(this, false);
            classic.ForeColor = UI.Text;
            FormClosed += (s, e) => { if (!installed && !again) { try { Settings.MarkConfigured(); } catch { } } };
        }

        void ShowPreview()
        {
            var state = SettingsData.ForPreset(Chosen(), null, false);
            using (var f = new PreviewForm(state)) f.ShowDialog(this);
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
                string preset = Chosen();
                bool keep = false;
                if (again && Settings.Configured && Settings.HasPins)
                {
                    var answer = PinChoice.Ask(this);
                    if (answer == PinChoice.Result.Cancel) return;
                    keep = answer == PinChoice.Result.Keep;
                }
                Settings.ApplyPreset(preset, keep);
                Shell.Install();
                installed = true;
                bool restart = false;
                if (classic.Parent != null && classic.Checked && !Tweaks.Classic) { Tweaks.ToggleClassic(); restart = true; }
                if (restart && Program.Ask("The classic menu needs Explorer to restart.\n\nRestart Explorer now? Open File Explorer windows will close."))
                    Actions.RestartExplorerNow();
                if (again)
                    foreach (Form f in Application.OpenForms)
                    {
                        var m = f as MainForm;
                        if (m != null) m.AfterSetup();
                    }
                Close();
            }
            catch (Exception ex) { Program.Fail("Could not install the menu:\n" + ex.Message); }
        }
    }
}
