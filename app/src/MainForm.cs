using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class MainForm : Form
    {
        readonly TextBox folder = new TextBox();
        readonly TextBox search = new TextBox();
        readonly ListView list = new ListView();
        readonly Label title = new Label(), hint = new Label(), meta = new Label();
        readonly Button run = new Button(), undo = new Button(), tabActions = new Button(), tabMenu = new Button();
        readonly ToolTip tips = new ToolTip();
        readonly ToolStripStatusLabel status = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly Panel pageActions = new Panel { Dock = DockStyle.Fill };
        readonly Panel pageMenu = new Panel { Dock = DockStyle.Fill, Visible = false };
        readonly TreeView tree = new TreeView();
        readonly Label menuInfo = new Label(), menuState = new Label();
        readonly ComboBox theme = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        readonly HashSet<string> favSet = new HashSet<string>();
        bool building;

        public MainForm(string startPath)
        {
            Text = "PS Tools"; Font = UI.Font; ClientSize = new Size(900, 580); MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.CenterScreen; AllowDrop = true; KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, ColumnCount = 3, Padding = new Padding(14, 12, 14, 8), BackColor = UI.Panel };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            folder.Dock = DockStyle.Fill; folder.Font = new Font("Segoe UI", 10f);
            folder.Text = startPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
            folder.AutoCompleteMode = AutoCompleteMode.SuggestAppend; folder.AutoCompleteSource = AutoCompleteSource.FileSystemDirectories;
            var browse = Btn("Browse...", 90); browse.Click += (s, e) => Browse();
            var open = Btn("Open", 70); open.Click += (s, e) => { if (Directory.Exists(Path0)) System.Diagnostics.Process.Start("explorer.exe", "\"" + Path0 + "\""); };
            top.Controls.Add(folder, 0, 0); top.Controls.Add(browse, 1, 0); top.Controls.Add(open, 2, 0);

            var tabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(14, 6, 14, 0), BackColor = UI.Back };
            Tab(tabActions, "Actions"); Tab(tabMenu, "Menu and settings");
            tabActions.Click += (s, e) => ShowPage(true);
            tabMenu.Click += (s, e) => ShowPage(false);
            tabs.Controls.Add(tabActions); tabs.Controls.Add(tabMenu);

            BuildActionsPage();
            BuildMenuPage();

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 7, 10, 4), BackColor = UI.Panel };
            undo.Width = 110; undo.Height = 30; undo.Text = "Undo last"; undo.Click += (s, e) => DoUndo();
            bottom.Controls.Add(undo);
            var strip = new StatusStrip { SizingGrip = false, BackColor = UI.Panel, ForeColor = UI.Muted };
            status.ForeColor = UI.Muted; strip.Items.Add(status);

            Controls.Add(pageActions); Controls.Add(pageMenu); Controls.Add(tabs); Controls.Add(top); Controls.Add(bottom); Controls.Add(strip);

            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Link; };
            DragDrop += (s, e) =>
            {
                var p = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
                folder.Text = Directory.Exists(p) ? p : Path.GetDirectoryName(p);
            };
            KeyDown += (s, e) =>
            {
                if (e.Control && (e.KeyCode == Keys.K || e.KeyCode == Keys.F)) { ShowPage(true); search.Focus(); search.SelectAll(); e.SuppressKeyPress = true; }
            };

            UI.Apply(this);
            StyleTabs();
            Fill();
            RefreshState();
        }

        string Path0 { get { return Program.Clean(folder.Text); } }

        static Button Btn(string text, int w) { return new Button { Text = text, Width = w, Height = 30, Margin = new Padding(6, 0, 0, 0) }; }

        void Tab(Button b, string text)
        {
            b.Text = text; b.Height = 32; b.AutoSize = true; b.Margin = new Padding(0, 0, 4, 0); b.Padding = new Padding(8, 0, 8, 0);
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.Font = UI.Bold; b.Cursor = Cursors.Hand;
        }

        void StyleTabs()
        {
            bool a = pageActions.Visible;
            foreach (var pair in new[] { new KeyValuePair<Button, bool>(tabActions, a), new KeyValuePair<Button, bool>(tabMenu, !a) })
            {
                pair.Key.UseVisualStyleBackColor = false;
                pair.Key.BackColor = pair.Value ? UI.Accent : UI.Back;
                pair.Key.ForeColor = pair.Value ? UI.OnAccent : UI.Text;
                pair.Key.FlatAppearance.BorderSize = 0;
                pair.Key.Tag = "accent";
            }
        }

        void ShowPage(bool actions)
        {
            pageActions.Visible = actions;
            pageMenu.Visible = !actions;
            StyleTabs();
            if (!actions) RefreshMenuState();
        }

        void BuildActionsPage()
        {
            var left = new Panel { Dock = DockStyle.Left, Width = 320 };
            search.Dock = DockStyle.Top; search.Font = new Font("Segoe UI", 10f);
            ResultsForm.SetCue(search, "Search actions  (Ctrl+K)");
            search.TextChanged += (s, e) => Fill();
            search.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down && list.Items.Count > 0) { list.Focus(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { RunSelected(); e.SuppressKeyPress = true; }
            };
            var searchWrap = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 10, 12, 4) };
            searchWrap.Controls.Add(search);

            list.Dock = DockStyle.Fill; list.View = View.Details; list.HeaderStyle = ColumnHeaderStyle.None;
            list.FullRowSelect = true; list.MultiSelect = false; list.HideSelection = false; list.BorderStyle = BorderStyle.None;
            list.Columns.Add("", 290); list.Font = new Font("Segoe UI", 9.5f);
            list.SmallImageList = new ImageList { ImageSize = new Size(1, 28) };
            list.SelectedIndexChanged += (s, e) => ShowSelected();
            list.DoubleClick += (s, e) => RunSelected();
            list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) RunSelected(); };
            list.Resize += (s, e) => { if (list.Columns.Count > 0) list.Columns[0].Width = Math.Max(120, list.ClientSize.Width - 4); };
            left.Controls.Add(list); left.Controls.Add(searchWrap);
            var sep = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = UI.Border };

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 28, 14) };
            title.Dock = DockStyle.Top; title.Height = 38; title.Font = UI.Title;
            hint.Dock = DockStyle.Top; hint.Height = 110; hint.ForeColor = UI.Muted;
            meta.Dock = DockStyle.Top; meta.Height = 44; meta.ForeColor = UI.Muted;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(0, 8, 0, 0) };
            run.Text = "Run"; run.Width = 130; run.Height = 34; UI.Primary(run); run.Click += (s, e) => RunSelected();
            buttons.Controls.Add(run);
            var tip = new Label
            {
                Dock = DockStyle.Bottom, Height = 54, ForeColor = UI.Muted,
                Text = "Tip: drop a folder on this window. Right-click menu entries are chosen on the Menu page.\nWindows 11: the classic menu is under \"Show more options\" (Shift+F10)."
            };
            right.Controls.Add(tip); right.Controls.Add(buttons); right.Controls.Add(meta); right.Controls.Add(hint); right.Controls.Add(title);

            pageActions.Controls.Add(right); pageActions.Controls.Add(sep); pageActions.Controls.Add(left);
        }

        void Fill()
        {
            string q = search.Text.Trim();
            list.BeginUpdate(); list.Items.Clear(); list.Groups.Clear();
            foreach (var m in Modules.All)
            {
                var acts = Actions.All.Where(a => a.Group == m.Name && a.Usable && Matches(a, q)).ToList();
                if (acts.Count == 0) continue;
                var g = new ListViewGroup(m.Name, m.Name);
                list.Groups.Add(g);
                foreach (var a in acts)
                    list.Items.Add(new ListViewItem("   " + a.Label + (a.Tier == Tier.Elevated ? "   (admin)" : ""), g) { Tag = a });
            }
            list.EndUpdate();
            if (list.Items.Count > 0) list.Items[0].Selected = true; else { title.Text = ""; hint.Text = "No match."; meta.Text = ""; }
        }

        static bool Matches(Action a, string q)
        {
            return q.Length == 0 || (a.Label + " " + a.Hint + " " + a.Group + " " + a.Id).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        Action Selected { get { return list.SelectedItems.Count == 0 ? null : (Action)list.SelectedItems[0].Tag; } }

        static string Where(Action a)
        {
            if (a.Targets == Target.None) return "Runs from this window only.";
            var w = new List<string>();
            if ((a.Targets & Target.Folder) != 0) w.Add("folders");
            if ((a.Targets & Target.Background) != 0) w.Add("folder background");
            if ((a.Targets & Target.Drive) != 0) w.Add("drives");
            if ((a.Targets & Target.File) != 0) w.Add("files");
            return "Right-click: " + string.Join(", ", w.ToArray()) + (a.Multi ? " (multi-select)" : "") + ".";
        }

        void ShowSelected()
        {
            var a = Selected; if (a == null) return;
            title.Text = a.Label.TrimEnd('.');
            hint.Text = a.Hint;
            string safety = a.Tier == Tier.Elevated ? "Needs administrator rights. Shows exactly what it will run first." : a.Confirm != null ? "Asks for confirmation before changing anything." : "Does not change your files.";
            meta.Text = safety + "\n" + Where(a);
            run.Text = a.Window != null ? "Open" : "Run";
        }

        void RunSelected()
        {
            var a = Selected; if (a == null) return;
            string[] paths;
            bool needsFile = (a.Targets & (Target.Dirs)) == 0 && (a.Targets & Target.File) != 0;
            if (needsFile)
            {
                using (var d = new OpenFileDialog { Multiselect = a.Multi, InitialDirectory = Directory.Exists(Path0) ? Path0 : null })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    paths = d.FileNames;
                }
            }
            else
            {
                if (!Directory.Exists(Path0) && a.Targets != Target.None) { SetStatus("Folder not found: " + Path0); return; }
                paths = new[] { Directory.Exists(Path0) ? Path0 : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
            }
            Runner.Execute(a, paths, this);
            RefreshState();
        }

        void Browse()
        {
            using (var d = new FolderBrowserDialog { SelectedPath = Directory.Exists(Path0) ? Path0 : "" })
                if (d.ShowDialog(this) == DialogResult.OK) folder.Text = d.SelectedPath;
        }

        void DoUndo()
        {
            string last = Journal.LastTitle();
            if (last == null) { SetStatus("Nothing to undo."); return; }
            if (!Program.Ask("Undo:\n" + last + " ?")) return;
            SetStatus(Journal.Undo());
            RefreshState();
        }

        public void SetStatus(string s) { status.Text = s; }

        void RefreshState()
        {
            string last = Journal.LastTitle();
            undo.Enabled = last != null;
            tips.SetToolTip(undo, last ?? "");
        }

        protected override void OnActivated(EventArgs e) { base.OnActivated(e); RefreshState(); }

        void BuildMenuPage()
        {
            var right = new Panel { Dock = DockStyle.Right, Width = 300, Padding = new Padding(20, 16, 16, 12) };
            menuInfo.Dock = DockStyle.Top; menuInfo.Height = 120; menuInfo.ForeColor = UI.Muted;
            menuInfo.Text = "Tick what you want in the Explorer right-click menu. Ticking a module ticks all its actions. Pin an action to put it at the top level of the menu. Nothing changes in Explorer until you press Apply.";

            var apply = new Button { Text = "Apply to Explorer menu", Dock = DockStyle.Top, Height = 36 };
            UI.Primary(apply); apply.Click += (s, e) => ApplyMenu();
            var pinBtn = new Button { Text = "Pin or unpin selected", Dock = DockStyle.Top, Height = 32 };
            pinBtn.Click += (s, e) => PinSelected();
            var reset = new Button { Text = "Reset to defaults", Dock = DockStyle.Top, Height = 32 };
            reset.Click += (s, e) => { Settings.ResetDefaults(); BuildTree(); SetStatus("Menu selection reset. Press Apply to update Explorer."); };

            var themeRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(0, 8, 0, 0) };
            themeRow.Controls.Add(new Label { Text = "Theme", AutoSize = true, Margin = new Padding(0, 5, 8, 0) });
            theme.Items.AddRange(new object[] { "auto", "light", "dark" });
            Settings.EnsureLoaded();
            theme.SelectedItem = Settings.Theme;
            if (theme.SelectedIndex < 0) theme.SelectedIndex = 0;
            theme.SelectedIndexChanged += (s, e) => { Settings.SetTheme((string)theme.SelectedItem); SetStatus("Theme saved. It applies the next time PS Tools starts."); };
            themeRow.Controls.Add(theme);

            var removeMenu = new Button { Text = "Remove Explorer menu", Dock = DockStyle.Top, Height = 32 };
            removeMenu.Click += (s, e) => { Shell.RemoveMenu(); RefreshMenuState(); SetStatus("Explorer menu removed."); };
            var full = new Button { Text = "Full uninstall...", Dock = DockStyle.Top, Height = 32 };
            full.Click += (s, e) => FullUninstall();
            menuState.Dock = DockStyle.Fill; menuState.ForeColor = UI.Muted;

            Func<int, Panel> gap = h => new Panel { Dock = DockStyle.Top, Height = h };
            right.Controls.Add(menuState);
            right.Controls.Add(full); right.Controls.Add(gap(6)); right.Controls.Add(removeMenu); right.Controls.Add(gap(14));
            right.Controls.Add(themeRow); right.Controls.Add(gap(8));
            right.Controls.Add(reset); right.Controls.Add(gap(6)); right.Controls.Add(pinBtn); right.Controls.Add(gap(6)); right.Controls.Add(apply);
            right.Controls.Add(gap(10)); right.Controls.Add(menuInfo);

            tree.Dock = DockStyle.Fill; tree.CheckBoxes = true; tree.ShowLines = false; tree.FullRowSelect = true; tree.HideSelection = false; tree.ItemHeight = 26;
            tree.AfterCheck += TreeChecked;
            tree.AfterSelect += (s, e) => TreeSelected();
            pageMenu.Controls.Add(tree); pageMenu.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 1, BackColor = UI.Border }); pageMenu.Controls.Add(right);
            BuildTree();
        }

        string NodeText(Action a)
        {
            return (favSet.Contains(a.Id) ? "\u2605 " : "") + a.Label + (a.Tier == Tier.Elevated ? "   (admin)" : "");
        }

        void BuildTree()
        {
            building = true;
            Settings.EnsureLoaded();
            favSet.Clear();
            foreach (var a in Actions.All) if (Settings.IsFav(a.Id)) favSet.Add(a.Id);
            tree.BeginUpdate(); tree.Nodes.Clear();
            foreach (var m in Modules.All)
            {
                var acts = Actions.All.Where(a => a.Group == m.Name && a.Targets != Target.None && a.Usable).ToList();
                if (acts.Count == 0 || m.Name == "Library") continue;
                var mn = new TreeNode(m.Name) { Tag = m, NodeFont = UI.Bold };
                foreach (var a in acts) mn.Nodes.Add(new TreeNode(NodeText(a)) { Tag = a, Checked = Settings.IsEnabled(a.Id) });
                mn.Checked = mn.Nodes.Cast<TreeNode>().All(n => n.Checked);
                tree.Nodes.Add(mn);
            }
            tree.EndUpdate();
            building = false;
            foreach (TreeNode n in tree.Nodes) n.Collapse();
            TreeSelected();
        }

        void TreeChecked(object s, TreeViewEventArgs e)
        {
            if (building) return;
            building = true;
            if (e.Node.Tag is Module) foreach (TreeNode c in e.Node.Nodes) c.Checked = e.Node.Checked;
            else if (e.Node.Parent != null) e.Node.Parent.Checked = e.Node.Parent.Nodes.Cast<TreeNode>().All(n => n.Checked);
            building = false;
        }

        void TreeSelected()
        {
            var n = tree.SelectedNode;
            if (n == null) { return; }
            var a = n.Tag as Action;
            var m = n.Tag as Module;
            if (a != null) menuInfo.Text = a.Hint + "\n\n" + Where(a);
            else if (m != null) menuInfo.Text = m.Name + ": " + m.Blurb;
        }

        void PinSelected()
        {
            var n = tree.SelectedNode;
            var a = n == null ? null : n.Tag as Action;
            if (a == null) { SetStatus("Select an action first."); return; }
            if (!favSet.Remove(a.Id)) { favSet.Add(a.Id); n.Checked = true; }
            n.Text = NodeText(a);
        }

        void ApplyMenu()
        {
            var enabled = new List<string>();
            foreach (TreeNode m in tree.Nodes)
                foreach (TreeNode n in m.Nodes)
                    if (n.Checked) enabled.Add(((Action)n.Tag).Id);
            Settings.Set(enabled, favSet);
            try
            {
                Shell.Install();
                SetStatus("Explorer menu updated: " + enabled.Count + " actions enabled.");
            }
            catch (Exception ex) { Program.Msg("Could not update the menu:\n" + ex.Message); }
            RefreshMenuState();
        }

        void FullUninstall()
        {
            if (!Program.Ask("Remove the Explorer menu, delete PS Tools' settings, undo data and temporary files, and restore any Windows settings it changed?\n\nPSTools.exe itself stays until you delete it.")) return;
            Program.Msg(Shell.UninstallAll());
            RefreshMenuState();
            RefreshState();
        }

        void RefreshMenuState()
        {
            string pending = Tweaks.Pending();
            menuState.Text = (Shell.IsInstalled() ? "Explorer menu: installed" : "Explorer menu: not installed")
                + "\nData folder: " + Journal.Dir
                + (pending.Length > 0 ? "\nWindows settings changed: " + pending : "");
        }
    }
}
