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
        public const int PageHome = 0, PageActions = 1, PageSettings = 2;
        static readonly int[] RailOf = { 0, 1, 6 };

        readonly NavRail rail = new NavRail();
        readonly BPanel host = new BPanel { Dock = DockStyle.Fill };
        readonly Label status = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, AccessibleName = "Status" };
        readonly FButton undo = new FButton { Text = "Undo last", Glyph = Fx.Undo, AccessibleName = "Undo last" };
        readonly Timer statusTimer = new Timer { Interval = 7000 };
        readonly Timer scanTimer = new Timer { Interval = 400 };
        readonly BPanel bottom = new BPanel { Dock = DockStyle.Bottom };
        readonly HomePage home;
        readonly ActionsPage actions;
        readonly SettingsPage settingsPage;
        string folder;
        int page = -1;
        int scanId;
        bool building = true;

        public MainForm(string startPath)
        {
            Text = "PS Tools";
            Font = UI.Font;
            AccessibleName = "PS Tools";
            var area = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Math.Min(UI.S(1100), area.Width - UI.S(40)), Math.Min(UI.S(720), area.Height - UI.S(90)));
            MinimumSize = new Size(Math.Min(UI.S(880), area.Width), Math.Min(UI.S(560), area.Height));
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            KeyPreview = true;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            folder = string.IsNullOrEmpty(startPath) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads" : startPath;

            statusTimer.Tick += (s, e) => { statusTimer.Stop(); status.Text = ""; };
            scanTimer.Tick += (s, e) => StartScan();

            rail.Dock = DockStyle.Left;
            rail.Width = UI.S(216);
            rail.Heading = "PS Tools";
            rail.Footer = "Version " + UI.Version;
            rail.AccessibleName = "Navigation";
            rail.Items.Add(new NavRail.Item { Text = "Home", Glyph = Fx.Home, Page = true });
            rail.Items.Add(new NavRail.Item { Text = "Actions", Glyph = Fx.Play, Page = true });
            rail.Items.Add(new NavRail.Item { Text = "Run", Glyph = Fx.Code });
            rail.Items.Add(new NavRail.Item { Text = "Cheat sheet", Glyph = Fx.Help });
            rail.Items.Add(new NavRail.Item { Divider = true });
            rail.Items.Add(new NavRail.Item { Text = "History", Glyph = Fx.History });
            rail.Items.Add(new NavRail.Item { Text = "Settings", Glyph = Fx.Settings, Page = true });
            rail.ItemClicked += RailClicked;

            home = new HomePage(this);
            actions = new ActionsPage(this);
            settingsPage = new SettingsPage(this);
            home.SetFolderText(folder);
            foreach (var p in new Control[] { home, actions, settingsPage }) { p.Visible = false; host.Controls.Add(p); }

            status.Dock = DockStyle.Fill;
            status.ForeColor = Tok.TextSecondary;
            status.Font = Tok.Caption;
            undo.Dock = DockStyle.Right;
            undo.Width = UI.S(128);
            undo.Click += (s, e) => DoUndo();
            bottom.Height = UI.S(46);
            bottom.Padding = new Padding(0, UI.S(10), 0, 0);
            bottom.Controls.Add(status);
            bottom.Controls.Add(undo);
            host.Padding = new Padding(Tok.S5, Tok.S5, Tok.S5, UI.S(6));
            var content = new BPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, UI.S(6)) };
            bottom.Padding = new Padding(Tok.S5, UI.S(10), Tok.S5, 0);
            content.Controls.Add(host);
            content.Controls.Add(bottom);
            Controls.Add(content);
            Controls.Add(rail);

            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Link; };
            DragDrop += (s, e) =>
            {
                var p = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
                home.SetFolderText(Directory.Exists(p) ? p : Path.GetDirectoryName(p));
            };
            KeyDown += OnKey;
            Shown += (s, e) => { LayoutPages(); StartScan(); };
            Resize += (s, e) => LayoutPages();
            FormClosing += (s, e) => SaveWindow();

            UI.Apply(this, false);
            BackColor = Tok.Window;
            RestoreWindow();
            building = false;
            ShowPage(PageHome);
            RefreshState();
            if (Program.StartupNote != null) SetStatus(Program.StartupNote);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                p.ExStyle |= 0x02000000;
                return p;
            }
        }

        public string Folder { get { return Program.Clean(folder); } }

        public int CurrentPage { get { return page; } }

        public HomePage HomePageControl { get { return home; } }

        public ActionsPage ActionsPageControl { get { return actions; } }

        public SettingsPage SettingsPageControl { get { return settingsPage; } }

        internal static bool Matches(Action a, string q)
        {
            return q.Length == 0 || (a.Label + " " + a.Hint + " " + a.Group + " " + a.Id).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void RestoreWindow()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (Width > wa.Width || Height > wa.Height) Size = new Size(Math.Min(Width, wa.Width), Math.Min(Height, wa.Height));
            Rectangle r; bool max;
            if (!Settings.TryGetWindow(out r, out max)) return;
            if (r.Width < MinimumSize.Width || r.Height < MinimumSize.Height) return;
            bool visible = false;
            foreach (Screen sc in Screen.AllScreens) if (sc.WorkingArea.IntersectsWith(r)) visible = true;
            if (!visible) return;
            StartPosition = FormStartPosition.Manual;
            Bounds = r;
            if (max) WindowState = FormWindowState.Maximized;
        }

        void SaveWindow()
        {
            bool max = WindowState == FormWindowState.Maximized;
            if (WindowState == FormWindowState.Minimized) return;
            Settings.SetWindow(max ? RestoreBounds : Bounds, max);
        }

        void LayoutPages()
        {
            if (building) return;
            home.DoLayout();
            actions.DoLayout();
            settingsPage.DoLayout();
        }

        void RailClicked(int i)
        {
            if (i == 0) ShowPage(PageHome);
            else if (i == 1) ShowPage(PageActions);
            else if (i == 2) ActionLauncher.OpenConsole(this, Folder, "");
            else if (i == 3) new CheatSheetForm(Folder, null).Show(this);
            else if (i == 5) new UndoHistoryForm().Show(this);
            else if (i == 6) ShowPage(PageSettings);
        }

        public void ShowPage(int which)
        {
            page = which;
            home.Visible = which == PageHome;
            actions.Visible = which == PageActions;
            settingsPage.Visible = which == PageSettings;
            rail.Selected = RailOf[which];
            if (which == PageHome) { home.Fill(); home.DoLayout(); }
            else if (which == PageActions) { actions.EnsureBuilt(); actions.DoLayout(); }
            else { settingsPage.Refresh2(); }
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.K) { OpenPalette(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.D1) { ShowPage(PageHome); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.D2) { ShowPage(PageActions); actions.FocusSearch(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.D3) { RailClicked(2); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.D4) { RailClicked(3); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.D5) { RailClicked(5); e.SuppressKeyPress = true; }
            else if (e.Control && (e.KeyCode == Keys.D6 || e.KeyCode == Keys.Oemcomma)) { ShowPage(PageSettings); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.F) { ShowPage(PageActions); actions.FocusSearch(); e.SuppressKeyPress = true; }
        }

        public void SetFolderFromBox(string text)
        {
            folder = text;
            scanTimer.Stop();
            scanTimer.Start();
        }

        public void BrowseFolder()
        {
            using (var d = new FolderBrowserDialog { SelectedPath = Directory.Exists(Folder) ? Folder : "" })
                if (d.ShowDialog(this) == DialogResult.OK) home.SetFolderText(d.SelectedPath);
        }

        public void OpenPalette()
        {
            var q = PowerMenuLauncher.QueryFor(ContextKind.Background, new string[0]);
            q.BackgroundFolder = Folder;
            var opt = PowerMenuLauncher.Options(Folder, "background");
            opt.Location = new Point(Left + (Width - UI.S(420)) / 2, Top + Math.Max(24, Height / 8));
            var pm = new PowerMenu(q, opt);
            pm.FormClosed += (s, e) => { if (pm.ToRun != null) BeginInvoke(pm.ToRun); };
            pm.Show(this);
        }

        public void OpenSpaceMap()
        {
            if (Directory.Exists(Folder)) new SpaceForm(Folder).Show(this);
            else SetStatus("Folder not found: " + Folder);
        }

        void DoUndo()
        {
            string last = Journal.LastTitle();
            if (last == null) { SetStatus("Nothing to undo."); return; }
            if (!Program.Ask("Undo:\n" + last + " ?")) return;
            SetStatus(Journal.Undo());
            RefreshState();
            StartScan();
        }

        public void AfterRun()
        {
            RefreshState();
            home.Fill();
            home.DoLayout();
            StartScan();
        }

        public void FavoritesChanged()
        {
            home.Fill();
            home.DoLayout();
        }

        public void AfterSetup()
        {
            settingsPage.Refresh2();
            home.Fill();
            home.DoLayout();
            SetStatus("Explorer menu set up.");
        }

        public void AfterUndo(string message)
        {
            SetStatus(message);
            RefreshState();
        }

        public void RefreshRecipes(string note)
        {
            ActionCatalog.Reset();
            home.Fill();
            home.DoLayout();
            if (!string.IsNullOrEmpty(note)) SetStatus(note);
        }

        public void SetStatus(string s)
        {
            status.Text = s;
            statusTimer.Stop();
            if (!string.IsNullOrEmpty(s)) statusTimer.Start();
        }

        void RefreshState()
        {
            string last = Journal.LastTitle();
            undo.Enabled = last != null;
            new ToolTip().SetToolTip(undo, last ?? "");
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RefreshState();
        }

        void StartScan()
        {
            scanTimer.Stop();
            string path = Folder;
            int id = ++scanId;
            actions.Invalidate();
            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                List<Suggestion> result;
                try { result = Suggest.For(path); }
                catch { result = new List<Suggestion>(); }
                try { BeginInvoke((MethodInvoker)(() => { if (id == scanId && !IsDisposed) { home.SetSuggestions(result); if (page == PageActions) actions.EnsureBuilt(); } })); }
                catch { }
            });
        }
    }
}
