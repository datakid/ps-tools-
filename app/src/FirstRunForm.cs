using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class FirstRunForm : Form
    {
        readonly FToggle addMenu = new FCheck { Text = "Add PS Tools to the Explorer right-click menu", Checked = true, AutoSize = true, AccessibleName = "Add to Explorer menu" };
        readonly FToggle classic = new FCheck { Text = "Use the classic right-click menu on Windows 11", AutoSize = true, AccessibleName = "Use the classic menu" };
        readonly FButton go = new FButton { Text = "Get started", Kind = FButton.Look.Accent, AccessibleName = "Get started" };
        readonly Label heading = new Label { Text = "Welcome to PS Tools", AutoSize = false, AccessibleName = "Welcome" };
        readonly Label[] bullets = new Label[3];
        readonly Label classicNote = new Label { AutoSize = false, Text = "Windows 11 hides new menu items behind Show more options. The classic menu shows them straight away. Explorer restarts once.", AccessibleName = "Classic menu note" };
        readonly bool again;
        readonly bool win11;
        bool installed;

        public FirstRunForm() : this(false) { }

        public FirstRunForm(bool reopened)
        {
            again = reopened;
            win11 = Tweaks.IsWindows11;
            Text = "PS Tools";
            AccessibleName = "Welcome to PS Tools";
            Font = UI.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(UI.S(640), UI.S(480));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            heading.Font = UI.Title;
            string[] lines =
            {
                "Power menu. Right-click, open the PS Tools menu, type two or three letters and press Enter.",
                "Run anything. A small console that shows output, keeps history and warns before it changes files.",
                "Save commands as actions. Turn a command you ran into a menu entry, with the folder filled in for you."
            };
            for (int i = 0; i < 3; i++)
            {
                bullets[i] = new Label { Text = lines[i], AutoSize = false, Font = UI.Font, AccessibleName = "Feature " + (i + 1) };
                Controls.Add(bullets[i]);
            }
            Controls.Add(heading);
            Controls.Add(addMenu);
            Controls.Add(classic);
            Controls.Add(classicNote);
            Controls.Add(go);
            classic.Visible = win11;
            classicNote.Visible = win11;
            classic.Checked = win11 && again && Tweaks.Classic;
            addMenu.Checked = !again || !Shell.IsInstalled();
            if (again && Shell.IsInstalled()) addMenu.Text = "Keep PS Tools in the Explorer right-click menu";
            go.Click += (s, e) => Finish();
            AcceptButton = go;
            UI.Apply(this, false);
            BackColor = Tok.Window;
            foreach (var l in bullets) { l.ForeColor = Tok.Text; l.Font = Tok.Body; }
            classicNote.ForeColor = Tok.TextSecondary;
            classicNote.Font = Tok.Caption;
            heading.ForeColor = Tok.Text;
            LayoutAll();
            FormClosed += (s, e) => { if (!installed && !again) { try { Settings.MarkConfigured(); } catch { } } };
        }

        void LayoutAll()
        {
            int pad = UI.S(32);
            int w = ClientSize.Width - pad * 2;
            int y = pad;
            heading.SetBounds(pad, y, w, UI.S(44));
            y += UI.S(60);
            foreach (var l in bullets)
            {
                l.SetBounds(pad, y, w, UI.S(48));
                y += UI.S(56);
            }
            y += UI.S(8);
            addMenu.SetBounds(pad, y, w, UI.S(28));
            y += UI.S(36);
            if (win11)
            {
                classic.SetBounds(pad, y, w, UI.S(28));
                y += UI.S(30);
                classicNote.SetBounds(pad + UI.S(28), y, w - UI.S(28), UI.S(40));
            }
            go.SetBounds(ClientSize.Width - pad - UI.S(160), ClientSize.Height - pad - UI.S(36), UI.S(160), UI.S(36));
        }

        void Finish()
        {
            try
            {
                Settings.MarkConfigured();
                if (addMenu.Checked) { Shell.Install(); installed = true; }
                bool restart = false;
                if (win11 && classic.Checked != Tweaks.Classic) { Tweaks.ToggleClassic(); restart = true; }
                if (restart && Program.Ask("The classic menu setting needs Explorer to restart.\n\nRestart Explorer now? Open File Explorer windows will close.")) Actions.RestartExplorerNow();
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
