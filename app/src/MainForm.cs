// Main window: folder bar on top, grouped action list on the left, details + Run on the right.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class MainForm : Form
    {
        readonly TextBox folder = new TextBox();
        readonly ListView list = new ListView();
        readonly Label title = new Label(), hint = new Label();
        readonly Button run = new Button(), undo = new Button(), menuToggle = new Button();
        readonly ToolTip tips = new ToolTip();
        readonly ToolStripStatusLabel status = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

        public MainForm(string startPath)
        {
            Text = "PS Tools"; Font = UI.Font; ClientSize = new Size(760, 480); MinimumSize = new Size(620, 400);
            StartPosition = FormStartPosition.CenterScreen; BackColor = Color.White; AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // ---- Top: folder bar ----
            var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 48, ColumnCount = 3, Padding = new Padding(12, 10, 12, 6), BackColor = UI.Panel };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            folder.Dock = DockStyle.Fill; folder.Font = new Font("Segoe UI", 10f);
            folder.Text = startPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
            folder.AutoCompleteMode = AutoCompleteMode.SuggestAppend; folder.AutoCompleteSource = AutoCompleteSource.FileSystemDirectories;
            var browse = Btn("Browse...", 84); browse.Click += (s, e) => Browse();
            var open = Btn("Open", 64); open.Click += (s, e) => { if (Directory.Exists(Path0)) System.Diagnostics.Process.Start("explorer.exe", "\"" + Path0 + "\""); };
            top.Controls.Add(folder, 0, 0); top.Controls.Add(browse, 1, 0); top.Controls.Add(open, 2, 0);

            // ---- Left: actions ----
            list.Dock = DockStyle.Left; list.Width = 280; list.View = View.Details; list.HeaderStyle = ColumnHeaderStyle.None;
            list.FullRowSelect = true; list.MultiSelect = false; list.HideSelection = false; list.BorderStyle = BorderStyle.None;
            list.Columns.Add("", 256); list.Font = new Font("Segoe UI", 9.5f);
            foreach (var g in Actions.All.Select(a => a.Group).Distinct()) list.Groups.Add(g, g);
            foreach (var a in Actions.All) list.Items.Add(new ListViewItem("   " + a.Label, list.Groups[a.Group]) { Tag = a });
            list.SelectedIndexChanged += (s, e) => ShowSelected();
            list.DoubleClick += (s, e) => RunSelected();
            list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) RunSelected(); };
            var sep = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Color.FromArgb(225, 225, 225) };

            // ---- Right: details ----
            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 18, 22, 14) };
            title.Dock = DockStyle.Top; title.Height = 30; title.Font = new Font("Segoe UI Semibold", 13f);
            hint.Dock = DockStyle.Top; hint.Height = 90; hint.ForeColor = UI.Muted;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(0, 6, 0, 0) };
            run.Text = "Run"; run.Width = 110; run.Height = 32; run.BackColor = UI.Accent; run.ForeColor = Color.White;
            run.FlatStyle = FlatStyle.Flat; run.FlatAppearance.BorderSize = 0; run.Font = UI.Bold; run.Click += (s, e) => RunSelected();
            buttons.Controls.Add(run);
            var tip = new Label { Dock = DockStyle.Bottom, Height = 54, ForeColor = UI.Muted,
                Text = "Tip: drop a folder on this window. Moves and renames are recorded - Undo last reverts them.\nWindows 11: the Explorer menu is under \"Show more options\" (Shift+F10)." };
            right.Controls.Add(buttons); right.Controls.Add(hint); right.Controls.Add(title); right.Controls.Add(tip);

            // ---- Bottom: status + undo + context-menu toggle ----
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 6, 8, 4), BackColor = UI.Panel };
            menuToggle.Width = 190; menuToggle.Height = 28; menuToggle.Click += (s, e) => ToggleMenu();
            undo.Width = 110; undo.Height = 28; undo.Text = "Undo last"; undo.Click += (s, e) => DoUndo();
            bottom.Controls.Add(menuToggle); bottom.Controls.Add(undo);
            var strip = new StatusStrip { SizingGrip = false, BackColor = UI.Panel }; strip.Items.Add(status);

            Controls.Add(right); Controls.Add(sep); Controls.Add(list); Controls.Add(top); Controls.Add(bottom); Controls.Add(strip);

            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Link; };
            DragDrop += (s, e) =>
            {
                var p = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
                folder.Text = Directory.Exists(p) ? p : Path.GetDirectoryName(p);
            };

            list.Items[0].Selected = true;
            RefreshState();
        }

        string Path0 { get { return Program.Clean(folder.Text); } }

        static Button Btn(string text, int w) { return new Button { Text = text, Width = w, Height = 28, Margin = new Padding(6, 0, 0, 0) }; }

        void Browse()
        {
            using (var d = new FolderBrowserDialog { SelectedPath = Directory.Exists(Path0) ? Path0 : "" })
                if (d.ShowDialog(this) == DialogResult.OK) folder.Text = d.SelectedPath;
        }

        Action Selected { get { return list.SelectedItems.Count == 0 ? null : (Action)list.SelectedItems[0].Tag; } }

        void ShowSelected()
        {
            var a = Selected; if (a == null) return;
            title.Text = a.Label.TrimEnd('.');
            hint.Text = a.Hint + (a.Confirm != null ? "\n\nAsks for confirmation before changing anything." : "");
        }

        void RunSelected()
        {
            var a = Selected; if (a == null) return;
            if (!Directory.Exists(Path0)) { SetStatus("Folder not found: " + Path0); return; }
            Runner.RunWithUI(a, Path0, this);
            RefreshState();
        }

        void DoUndo()
        {
            string last = Journal.LastTitle();
            if (last == null) { SetStatus("Nothing to undo."); return; }
            if (!Program.Ask("Undo:\n" + last + " ?")) return;
            SetStatus(Journal.Undo());
            RefreshState();
        }

        void ToggleMenu()
        {
            if (Shell.IsInstalled()) Shell.Uninstall(); else Shell.Install();
            SetStatus(Shell.IsInstalled() ? "Explorer context menu added." : "Explorer context menu removed.");
            RefreshState();
        }

        public void SetStatus(string s) { status.Text = s; }

        void RefreshState()
        {
            menuToggle.Text = Shell.IsInstalled() ? "Remove Explorer menu" : "Add to Explorer menu";
            string last = Journal.LastTitle();
            undo.Enabled = last != null;
            tips.SetToolTip(undo, last ?? "");
        }

        protected override void OnActivated(EventArgs e) { base.OnActivated(e); RefreshState(); }
    }
}
