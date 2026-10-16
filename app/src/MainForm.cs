using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class SuggestTag
    {
        public Action Action;
        public string Reason;
    }

    class MainForm : Form
    {
        readonly TextBox folder = new TextBox();
        readonly TextBox search = new TextBox();
        readonly ListView list = new ListView();
        readonly Label title = new Label(), hint = new Label(), meta = new Label();
        readonly Label whatCaption = new Label { Text = "What it does", AutoSize = false, Dock = DockStyle.Top, Height = 22, ForeColor = UI.Muted, Font = UI.SmallBold };
        readonly Label psCaption = new Label { Text = "PowerShell equivalent", AutoSize = false, Dock = DockStyle.Top, Height = 26, ForeColor = UI.Muted, Font = UI.SmallBold };
        readonly Label psNote = new Label { Dock = DockStyle.Top, Height = 24, ForeColor = UI.Muted, Text = "{path} is replaced by the folder in the box above when you copy. Nothing here runs the command." };
        readonly TextBox psBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Top, Height = 170, Font = UI.Mono };
        readonly FButton psCopy = new FButton { Text = "Copy command", Width = 140, Height = 32, Margin = new Padding(0, 0, 8, 0) };
        readonly FButton psOpen = new FButton { Text = "Open PowerShell with command", Width = 250, Height = 32, Margin = new Padding(0) };
        readonly FButton run = new FButton(), undo = new FButton();
        readonly NavRail rail = new NavRail();
        readonly Label tip = new Label { Dock = DockStyle.Bottom, Height = 56, ForeColor = UI.Muted };
        readonly ToolTip tips = new ToolTip();
        readonly Label status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        readonly Panel pageActions = new Panel { Dock = DockStyle.Fill };
        readonly Panel pageMenu = new Panel { Dock = DockStyle.Fill, Visible = false };
        readonly Label menuState = new Label();
        readonly ComboBox theme = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        readonly ComboBox preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly FButton pinAction = new FButton { Text = "Pin...", Width = 120, Height = 36, Glyph = Fx.Pin, Margin = new Padding(0, 0, 0, 0) };
        readonly Timer statusTimer = new Timer { Interval = 7000 };
        readonly Timer applyTimer = new Timer { Interval = 700 };
        readonly Timer scanTimer = new Timer { Interval = 300 };
        readonly FButton[] contextButtons = new FButton[4];
        readonly Label pinsTitle = new Label(), previewNote = new Label();
        readonly ListView pinList = new ListView();
        readonly ListView allList = new ListView();
        readonly TextBox allFilter = new TextBox();
        readonly TreeView previewTree = new TreeView();
        readonly FToggle previewShift = new FCheck { Text = "Holding Shift", AutoSize = true };
        readonly FButton installBtn = new FButton { Text = "Install Explorer menu", Width = 190, Height = 32, Margin = new Padding(8, 0, 0, 0), Visible = false };
        Panel actionsScroll;
        bool building;
        Target ctx = Target.Folder;
        List<Suggestion> suggestions = new List<Suggestion>();
        string suggestionKey = "";
        int scanId;

        public MainForm(string startPath)
        {
            Text = "PS Tools"; Font = UI.Font; ClientSize = new Size(980, 640); MinimumSize = new Size(860, 520);
            StartPosition = FormStartPosition.CenterScreen; AllowDrop = true; KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            statusTimer.Tick += (s, e) => { statusTimer.Stop(); status.Text = ""; };
            applyTimer.Tick += (s, e) => { applyTimer.Stop(); ApplyNow(); };
            scanTimer.Tick += (s, e) => StartScan();

            rail.Dock = DockStyle.Left; rail.Width = 216; rail.Heading = "PS Tools"; rail.Footer = "Version " + UI.Version;
            rail.Items.Add(new NavRail.Item { Text = "Actions", Glyph = Fx.Play, Page = true });
            rail.Items.Add(new NavRail.Item { Text = "Menu and settings", Glyph = Fx.Settings, Page = true });
            rail.Items.Add(new NavRail.Item { Divider = true });
            rail.Items.Add(new NavRail.Item { Text = "Recipes", Glyph = Fx.List });
            rail.Items.Add(new NavRail.Item { Text = "Undo history", Glyph = Fx.History });
            rail.Items.Add(new NavRail.Item { Text = "Space map", Glyph = Fx.Folder });
            rail.Items.Add(new NavRail.Item { Divider = true });
            rail.Items.Add(new NavRail.Item { Text = "Setup guide", Glyph = Fx.Help });
            rail.ItemClicked += i =>
            {
                if (i == 0) ShowPage(true);
                else if (i == 1) ShowPage(false);
                else if (i == 3) new RecipesForm().Show(this);
                else if (i == 4) new UndoHistoryForm().Show(this);
                else if (i == 5)
                {
                    if (Directory.Exists(Path0)) new SpaceForm(Path0).Show(this);
                    else SetStatus("Folder not found: " + Path0);
                }
                else if (i == 7) new FirstRunForm(true).Show(this);
            };

            var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 62, ColumnCount = 3, Padding = new Padding(0, 14, 0, 8) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            folder.Dock = DockStyle.Fill;
            folder.Text = startPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
            folder.AutoCompleteMode = AutoCompleteMode.SuggestAppend; folder.AutoCompleteSource = AutoCompleteSource.FileSystemDirectories;
            folder.TextChanged += (s, e) => { scanTimer.Stop(); scanTimer.Start(); };
            var browse = Btn("Browse...", 110); browse.Glyph = Fx.FolderOpen; browse.Click += (s, e) => Browse();
            var open = Btn("Open", 92); open.Glyph = Fx.OpenWindow; open.Click += (s, e) => { if (Directory.Exists(Path0)) System.Diagnostics.Process.Start("explorer.exe", "\"" + Path0 + "\""); };
            top.Controls.Add(folder, 0, 0); top.Controls.Add(browse, 1, 0); top.Controls.Add(open, 2, 0);

            BuildActionsPage();
            BuildMenuPage();

            undo.Text = "Undo last"; undo.Glyph = Fx.Undo; undo.Width = 128; undo.Height = 32; undo.Dock = DockStyle.Right; undo.Click += (s, e) => DoUndo();
            status.ForeColor = UI.Muted;
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(2, 10, 0, 0) };
            bottom.Controls.Add(status); bottom.Controls.Add(undo);

            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 18, 12) };
            content.Controls.Add(pageActions); content.Controls.Add(pageMenu); content.Controls.Add(bottom); content.Controls.Add(top);
            Controls.Add(content); Controls.Add(rail);
            rail.Selected = 0;

            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Link; };
            DragDrop += (s, e) =>
            {
                var p = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
                folder.Text = Directory.Exists(p) ? p : Path.GetDirectoryName(p);
            };
            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.K) { OpenPalette(); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.F) { ShowPage(true); search.Focus(); search.SelectAll(); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.D1) { ShowPage(true); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.D2) { ShowPage(false); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.Tab) { ShowPage(!pageActions.Visible); e.SuppressKeyPress = true; }
            };
            Shown += (s, e) => { previewTree.ItemHeight = UI.S(26); search.Focus(); FitActions(); FitList(list); FitList(pinList); FitList(allList); StartScan(); };
            FormClosing += (s, e) => { SaveWindow(); if (applyTimer.Enabled) { applyTimer.Stop(); ApplyNow(); } };

            UI.Apply(this);
            RestoreWindow();
            Fill();
            RefreshState();
            RefreshMenu();
            if (Program.StartupNote != null) SetStatus(Program.StartupNote);
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

        void FitActions()
        {
            if (actionsScroll == null) return;
            hint.Height = UI.TextHeight(hint, 24);
            meta.Height = UI.TextHeight(meta, 24);
            psNote.Height = UI.TextHeight(psNote, 24);
            tip.Height = UI.TextHeight(tip, 40);
            UI.FitScroll(actionsScroll);
        }

        static void FitList(ListView lv)
        {
            if (lv.Columns.Count > 0) lv.Columns[0].Width = Math.Max(120, lv.ClientSize.Width);
            UI.HideHScroll(lv);
        }

        string Path0 { get { return Program.Clean(folder.Text); } }

        static FButton Btn(string text, int w) { return new FButton { Text = text, Width = w, Height = 32, Margin = new Padding(8, 0, 0, 0) }; }

        void ShowPage(bool actions)
        {
            pageActions.Visible = actions;
            pageMenu.Visible = !actions;
            rail.Selected = actions ? 0 : 1;
            if (!actions) RefreshMenu();
            FitActions();
        }

        static string ModuleGlyph(string group)
        {
            switch (group)
            {
                case "Copy": return Fx.Copy;
                case "Create": return Fx.Add;
                case "Archive": return Fx.Package;
                case "Image": return Fx.Image;
                case "Organize": return Fx.Folder;
                case "Clean up": return Fx.Delete;
                case "Inspect": return Fx.Search;
                case "Export": return Fx.Share;
                case "Fix": return Fx.Edit;
                case "Links": return Fx.Link;
                case "Dev": return Fx.Code;
                case "Disk": return Fx.Folder;
                case "System": return Fx.Settings;
                case "Recipes": return Fx.List;
                default: return Fx.Code;
            }
        }

        static Action ActionOf(ListViewItem it)
        {
            if (it == null) return null;
            var a = it.Tag as Action;
            if (a != null) return a;
            var s = it.Tag as SuggestTag;
            return s == null ? null : s.Action;
        }

        void BuildActionsPage()
        {
            var left = new Panel { Dock = DockStyle.Left, Width = 340, Padding = new Padding(0, 0, 10, 0) };
            search.Dock = DockStyle.Fill;
            ResultsForm.SetCue(search, "Search actions  (Ctrl+F)");
            search.TextChanged += (s, e) => Fill();
            search.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down && list.Items.Count > 0) { list.Focus(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { RunSelected(); e.SuppressKeyPress = true; }
            };
            var searchWrap = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 0, 0, 10) };
            searchWrap.Controls.Add(search);

            list.Dock = DockStyle.Fill; list.View = View.Details; list.HeaderStyle = ColumnHeaderStyle.None;
            list.FullRowSelect = true; list.MultiSelect = false; list.HideSelection = false; list.BorderStyle = BorderStyle.None;
            list.Columns.Add("", 290);
            FList.Attach(list, 38, it => { var a = ActionOf(it); return a == null ? null : ModuleGlyph(a.Group); }, it => { var s = it.Tag as SuggestTag; return s == null ? null : s.Reason; });
            list.SelectedIndexChanged += (s, e) => ShowSelected();
            list.DoubleClick += (s, e) => RunSelected();
            list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) RunSelected(); };
            list.Resize += (s, e) => FitList(list);
            left.Controls.Add(list); left.Controls.Add(searchWrap);

            var right = new FCard { Dock = DockStyle.Fill, Padding = new Padding(28, 24, 24, 16) };
            actionsScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            title.Dock = DockStyle.Top; title.Height = 44; title.Font = UI.Title; title.AutoEllipsis = true;
            hint.Dock = DockStyle.Top; hint.Height = 60; hint.ForeColor = UI.Muted;
            meta.Dock = DockStyle.Top; meta.Height = 66; meta.ForeColor = UI.Muted;
            var psButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
            psButtons.Controls.Add(psCopy); psButtons.Controls.Add(psOpen);
            psCopy.Click += (s, e) => CopyCommand();
            psOpen.Click += (s, e) => OpenWithCommand();
            actionsScroll.Controls.Add(psButtons); actionsScroll.Controls.Add(psBox); actionsScroll.Controls.Add(psNote); actionsScroll.Controls.Add(psCaption);
            actionsScroll.Controls.Add(meta); actionsScroll.Controls.Add(hint); actionsScroll.Controls.Add(whatCaption);
            actionsScroll.Resize += (s, e) => FitActions();
            UI.WheelScroll(actionsScroll);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(0, 10, 0, 0) };
            run.Text = "Run"; run.Glyph = Fx.Play; run.Width = 112; run.Height = 36; run.Margin = new Padding(0, 0, 8, 0); UI.Primary(run); run.Click += (s, e) => RunSelected();
            buttons.Controls.Add(run);
            pinAction.Click += (s, e) => ShowPinMenu();
            tips.SetToolTip(pinAction, "Choose where to pin this action at the top level of the right-click menu");
            buttons.Controls.Add(pinAction);
            tip.Text = "Tip: drop a folder on this window. Right-click menu entries are chosen on the Menu page.\nWindows 11: the classic menu is under \"Show more options\" (Shift+F10).";
            right.Controls.Add(actionsScroll); right.Controls.Add(buttons); right.Controls.Add(tip); right.Controls.Add(title);

            pageActions.Controls.Add(right); pageActions.Controls.Add(left);
        }

        void Fill()
        {
            string q = search.Text.Trim();
            var current = Selected;
            string keep = current == null ? null : current.Id;
            list.BeginUpdate(); list.Items.Clear();
            if (q.Length == 0 && suggestions.Count > 0)
            {
                list.Items.Add(FList.Header("Suggested here"));
                foreach (var s in suggestions)
                    list.Items.Add(new ListViewItem(s.Action.Label + (s.Action.Tier == Tier.Elevated ? "   (admin)" : "")) { Tag = new SuggestTag { Action = s.Action, Reason = s.Reason } });
            }
            foreach (var m in Modules.All)
            {
                var acts = Actions.All.Where(a => a.Group == m.Name && a.Usable && Matches(a, q)).ToList();
                if (acts.Count == 0) continue;
                list.Items.Add(FList.Header(m.Name));
                foreach (var a in acts)
                    list.Items.Add(new ListViewItem(a.Label + (a.Tier == Tier.Elevated ? "   (admin)" : "")) { Tag = a });
            }
            list.EndUpdate();
            FitList(list);
            ListViewItem pick = null;
            if (keep != null) foreach (ListViewItem it in list.Items) if (it.Tag is Action && ((Action)it.Tag).Id == keep) { pick = it; break; }
            if (pick == null) pick = FList.FirstRow(list);
            if (pick != null)
            {
                pick.Selected = true;
                pick.EnsureVisible();
            }
            else { title.Text = ""; hint.Text = "No match."; meta.Text = ""; ClearCommand(); FitActions(); }
        }

        internal static bool Matches(Action a, string q)
        {
            return q.Length == 0 || (a.Label + " " + a.Hint + " " + a.Group + " " + a.Id).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        Action Selected { get { return list.SelectedItems.Count == 0 ? null : ActionOf(list.SelectedItems[0]); } }

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

        static readonly string[] ListsThenDeletes = { "duplicates", "clean-build", "largest", "recent", "long-paths" };

        static string ChangesText(Action a)
        {
            if (ListsThenDeletes.Contains(a.Id)) return "No, unless you tick items in the list and delete them";
            return a.Undoable || a.Confirm != null || a.Tier == Tier.Elevated || a.Script != null ? "Yes" : "No";
        }

        void ShowSelected()
        {
            var a = Selected; if (a == null) return;
            title.Text = a.Label.TrimEnd('.');
            hint.Text = a.Hint;
            var lines = new List<string>();
            lines.Add("Changes files: " + ChangesText(a));
            lines.Add("Undo: " + (a.Undoable ? "Yes (Undo last)" : "No"));
            lines.Add(Where(a));
            if (a.Tier == Tier.Elevated) lines.Add("Needs administrator rights. Shows exactly what it will run first.");
            else if (a.Confirm != null) lines.Add("Asks for confirmation before changing anything.");
            meta.Text = string.Join("\n", lines.ToArray());
            run.Text = a.Window != null ? "Open" : "Run";
            ShowCommand(a);
            UpdatePinAction(a);
            FitActions();
            actionsScroll.AutoScrollPosition = new Point(0, 0);
        }

        void ShowCommand(Action a)
        {
            bool has = !string.IsNullOrWhiteSpace(a.Ps);
            if (has) psBox.Text = a.Ps.Replace("\r\n", "\n").Replace("\n", "\r\n");
            else if (!string.IsNullOrWhiteSpace(a.PsNone)) psBox.Text = "No PowerShell equivalent.\r\n\r\n" + a.PsNone;
            else psBox.Text = "No PowerShell equivalent for this action.";
            psCopy.Enabled = psOpen.Enabled = has;
        }

        void ClearCommand()
        {
            psBox.Text = "";
            psCopy.Enabled = psOpen.Enabled = false;
        }

        string FilledCommand()
        {
            var a = Selected;
            if (a == null || string.IsNullOrWhiteSpace(a.Ps)) return null;
            return LibraryData.Fill(a.Ps.Replace("\r\n", "\n").Replace("\n", "\r\n"), Path0);
        }

        void CopyCommand()
        {
            string c = FilledCommand();
            if (c == null) return;
            Clip.Set(c);
            SetStatus("Command copied. Paste it into PowerShell to run it.");
        }

        void OpenWithCommand()
        {
            string c = FilledCommand();
            if (c == null) return;
            if (!Directory.Exists(Path0)) { SetStatus("Folder not found: " + Path0); return; }
            Clip.Set(c);
            try
            {
                Launch.PowerShell(Path0, "Write-Host 'The command is on your clipboard. Paste it with a right-click, read it, then press Enter.' -ForegroundColor Cyan", false);
                SetStatus("Command copied. Paste it with a right-click in the PowerShell window.");
            }
            catch (Exception ex) { SetStatus("Could not open PowerShell: " + ex.Message); }
        }

        public void RefreshRecipes(string note)
        {
            Fill();
            FitList(list);
            if (!string.IsNullOrEmpty(note)) SetStatus(note);
        }

        void OpenPalette()
        {
            new PaletteForm(this, RunAction, suggestions.ToList()).Show(this);
        }

        void RunSelected()
        {
            var a = Selected; if (a == null) return;
            RunAction(a);
        }

        void RunAction(Action a)
        {
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
            Settings.AddRecent(a.Id);
            Runner.Execute(a, paths, this);
            RefreshState();
            StartScan();
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
            StartScan();
        }

        public void AfterSetup()
        {
            RefreshMenu();
            SetStatus("Explorer menu set up. Fine-tune it on the Menu page.");
        }

        public void AfterUndo(string message)
        {
            SetStatus(message);
            RefreshState();
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
            tips.SetToolTip(undo, last ?? "");
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RefreshState();
            if (IsHandleCreated) StartScan();
        }

        void StartScan()
        {
            scanTimer.Stop();
            string path = Path0;
            int id = ++scanId;
            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                List<Suggestion> result;
                try { result = Suggest.For(path); }
                catch { result = new List<Suggestion>(); }
                try { BeginInvoke((MethodInvoker)(() => ApplySuggestions(id, result))); }
                catch { }
            });
        }

        void ApplySuggestions(int id, List<Suggestion> result)
        {
            if (id != scanId || IsDisposed) return;
            string key = string.Join("|", result.Select(s => s.ActionId + ":" + s.Reason).ToArray());
            if (key == suggestionKey) return;
            suggestionKey = key;
            suggestions = result;
            Fill();
        }

        static bool Pinnable(Action a)
        {
            return a != null && a.Targets != Target.None && a.Usable && a.Group != "Library" && a.Group != "Recipes";
        }

        void UpdatePinAction(Action a)
        {
            pinAction.Enabled = Pinnable(a);
            pinAction.Text = a != null && Settings.IsPinnedAnywhere(a.Id) ? "Pinned..." : "Pin...";
        }

        void ShowPinMenu()
        {
            var a = Selected;
            if (!Pinnable(a)) return;
            var menu = new ContextMenuStrip();
            Fx.ThemeMenu(menu);
            var applies = DefaultPins.Contexts.Where(t => (a.Targets & t) != 0).ToList();
            foreach (var t in DefaultPins.Contexts)
            {
                var target = t;
                var item = new ToolStripMenuItem("Pin to: " + DefaultPins.Title(t)) { Checked = Settings.IsPinned(a.Id, t), Enabled = (a.Targets & t) != 0 };
                item.Click += (s, e) => TogglePinHere(a, target);
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            bool all = applies.All(t => Settings.IsPinned(a.Id, t));
            var every = new ToolStripMenuItem(all ? "Unpin from all that apply" : "Pin to all that apply");
            every.Click += (s, e) => PinEverywhere(a, !all);
            menu.Items.Add(every);
            menu.Show(pinAction, new Point(0, pinAction.Height));
        }

        bool TryPin(Action a, Target t)
        {
            if (Settings.Pin(t, a.Id)) return true;
            Program.Msg("\"" + DefaultPins.Title(t) + "\" already has " + Settings.MaxPins + " pinned actions, which is the most the top of the menu can hold. Unpin one first.");
            return false;
        }

        void TogglePinHere(Action a, Target t)
        {
            bool now = !Settings.IsPinned(a.Id, t);
            if (now) { if (!TryPin(a, t)) return; }
            else Settings.Unpin(t, a.Id);
            AfterPinChange(a, (now ? "Pinned" : "Unpinned") + " \"" + a.Label.TrimEnd('.') + "\" for " + DefaultPins.Title(t).ToLowerInvariant() + ".");
        }

        void PinEverywhere(Action a, bool pin)
        {
            bool failed = false;
            foreach (var t in DefaultPins.Contexts)
            {
                if ((a.Targets & t) == 0) continue;
                if (pin) { if (!TryPin(a, t)) failed = true; }
                else Settings.Unpin(t, a.Id);
            }
            AfterPinChange(a, (pin ? "Pinned" : "Unpinned") + " \"" + a.Label.TrimEnd('.') + "\" everywhere it applies." + (failed ? " Some lists were full." : ""));
        }

        void AfterPinChange(Action a, string message)
        {
            UpdatePinAction(a);
            RefreshMenu();
            ScheduleApply();
            SetStatus(message);
        }

        void ScheduleApply()
        {
            applyTimer.Stop();
            applyTimer.Start();
        }

        void ApplyNow()
        {
            if (!Shell.IsInstalled())
            {
                SetStatus("Saved. The Explorer menu is not installed yet: use Install Explorer menu on the Menu page.");
                RefreshMenuState();
                return;
            }
            try
            {
                Shell.Install();
                SetStatus("Explorer menu updated");
            }
            catch (Exception ex) { Program.Msg("Could not update the Explorer menu:\n" + ex.Message); }
            RefreshMenuState();
        }

        void BuildMenuPage()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));

            var selector = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 4), WrapContents = false };
            for (int i = 0; i < DefaultPins.Contexts.Length; i++)
            {
                var t = DefaultPins.Contexts[i];
                var b = new FButton { Text = DefaultPins.Title(t), Width = 130, Height = 34, Margin = new Padding(0, 0, 8, 0) };
                b.Click += (s, e) => SetContext(t);
                contextButtons[i] = b;
                selector.Controls.Add(b);
            }
            var selectorNote = new Label { Text = "Choose where you right-click", AutoSize = true, ForeColor = UI.Muted, Margin = new Padding(8, 8, 0, 0) };
            selector.Controls.Add(selectorNote);

            var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
            cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var pinsCard = new FCard { Dock = DockStyle.Fill, Padding = new Padding(14, 12, 10, 10), Margin = new Padding(0, 0, 10, 0) };
            pinList.Dock = DockStyle.Fill; pinList.View = View.Details; pinList.HeaderStyle = ColumnHeaderStyle.None; pinList.FullRowSelect = true;
            pinList.MultiSelect = false; pinList.HideSelection = false; pinList.BorderStyle = BorderStyle.None;
            pinList.Columns.Add("", 200);
            pinList.Resize += (s, e) => FitList(pinList);
            pinList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) { UnpinSelected(); e.Handled = true; } };
            pinList.SelectedIndexChanged += (s, e) => UpdatePinButtons();
            pinsTitle.Dock = DockStyle.Top; pinsTitle.Height = 44; pinsTitle.Font = UI.Bold; pinsTitle.AutoEllipsis = false;
            var pinButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
            var up = new FButton { Text = "Up", Width = 64, Height = 32, Margin = new Padding(0, 0, 6, 0) };
            var down = new FButton { Text = "Down", Width = 74, Height = 32, Margin = new Padding(0, 0, 6, 0) };
            var unpin = new FButton { Text = "Unpin", Width = 80, Height = 32, Margin = new Padding(0) };
            up.Click += (s, e) => MoveSelected(-1);
            down.Click += (s, e) => MoveSelected(1);
            unpin.Click += (s, e) => UnpinSelected();
            pinButtons.Controls.Add(up); pinButtons.Controls.Add(down); pinButtons.Controls.Add(unpin);
            pinButtons.Tag = new[] { up, down, unpin };
            pinsCard.Controls.Add(pinList); pinsCard.Controls.Add(pinButtons); pinsCard.Controls.Add(pinsTitle);
            pinsCard.Tag = pinButtons;

            var allCard = new FCard { Dock = DockStyle.Fill, Padding = new Padding(14, 12, 10, 10), Margin = new Padding(0, 0, 10, 0) };
            allList.Dock = DockStyle.Fill; allList.View = View.Details; allList.HeaderStyle = ColumnHeaderStyle.None; allList.FullRowSelect = true;
            allList.MultiSelect = false; allList.HideSelection = false; allList.BorderStyle = BorderStyle.None;
            allList.Columns.Add("", 300);
            FList.Attach(allList, 34, null);
            FList.Decorate(allList, new RowDeco
            {
                Check = AllCheckState,
                OnCheck = AllToggleCheck,
                Pin = AllPinState,
                OnPin = AllTogglePin
            });
            allList.Resize += (s, e) => FitList(allList);
            allList.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Space && allList.SelectedItems.Count > 0) { AllToggleCheck(allList.SelectedItems[0]); e.Handled = true; e.SuppressKeyPress = true; }
            };
            allList.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                var it = allList.GetItemAt(e.X, e.Y);
                if (it == null || FList.IsHeader(it)) return;
                it.Selected = true;
                ShowAllMenu((Action)it.Tag, e.Location);
            };
            var allTitle = new Label { Text = "All actions", Dock = DockStyle.Top, Height = 28, Font = UI.Bold };
            ResultsForm.SetCue(allFilter, "Filter actions");
            allFilter.Dock = DockStyle.Fill;
            allFilter.TextChanged += (s, e) => FillAll();
            allFilter.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { allFilter.Text = ""; e.SuppressKeyPress = true; } };
            var filterWrap = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 0, 0, 10) };
            filterWrap.Controls.Add(allFilter);
            allCard.Controls.Add(allList); allCard.Controls.Add(filterWrap); allCard.Controls.Add(allTitle);

            var previewCard = new FCard { Dock = DockStyle.Fill, Padding = new Padding(14, 12, 10, 10), Margin = new Padding(0) };
            var previewTitle = new Label { Text = "What you will see", Dock = DockStyle.Top, Height = 28, Font = UI.Bold };
            previewShift.Dock = DockStyle.Top;
            previewShift.CheckedChanged += (s, e) => FillPreview();
            previewTree.Dock = DockStyle.Fill; previewTree.ShowLines = false; previewTree.FullRowSelect = true; previewTree.HideSelection = true; previewTree.ShowRootLines = true; previewTree.ItemHeight = 26;
            previewNote.Dock = DockStyle.Bottom; previewNote.Height = 52; previewNote.ForeColor = UI.Muted;
            previewCard.Controls.Add(previewTree); previewCard.Controls.Add(previewNote); previewCard.Controls.Add(previewShift); previewCard.Controls.Add(previewTitle);

            cards.Controls.Add(pinsCard, 0, 0); cards.Controls.Add(allCard, 1, 0); cards.Controls.Add(previewCard, 2, 0);

            var below = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 8, 0, 0) };
            below.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            below.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            below.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            row.Controls.Add(new Label { Text = "Preset", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
            preset.Items.AddRange(Presets.Names);
            preset.Items.Add(Presets.Custom);
            preset.Margin = new Padding(0, 4, 16, 0);
            preset.SelectedIndexChanged += (s, e) => PresetChosen();
            row.Controls.Add(preset);
            row.Controls.Add(new Label { Text = "Theme", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
            theme.Items.AddRange(new object[] { "auto", "light", "dark" });
            Settings.EnsureLoaded();
            theme.SelectedItem = Settings.Theme;
            if (theme.SelectedIndex < 0) theme.SelectedIndex = 0;
            theme.Margin = new Padding(0, 4, 8, 0);
            theme.SelectedIndexChanged += (s, e) => { Settings.SetTheme((string)theme.SelectedItem); SetStatus("Theme saved."); };
            row.Controls.Add(theme);
            row.Controls.Add(new Label { Text = "Applies the next time PS Tools starts", AutoSize = true, ForeColor = UI.Muted, Margin = new Padding(0, 8, 16, 0) });
            var guide = new FButton { Text = "Setup guide...", Width = 130, Height = 32, Glyph = Fx.Help, Margin = new Padding(0, 0, 8, 0) };
            guide.Click += (s, e) => new FirstRunForm(true).Show(this);
            var removeMenu = new FButton { Text = "Remove Explorer menu", Width = 190, Height = 32, Margin = new Padding(0, 0, 8, 0) };
            removeMenu.Click += (s, e) => { applyTimer.Stop(); Shell.RemoveMenu(); RefreshMenuState(); SetStatus("Explorer menu removed."); };
            var full = new FButton { Text = "Full uninstall...", Width = 140, Height = 32, Margin = new Padding(0) };
            full.Click += (s, e) => FullUninstall();
            installBtn.Click += (s, e) => InstallNow();
            row.Controls.Add(guide); row.Controls.Add(removeMenu); row.Controls.Add(full); row.Controls.Add(installBtn);
            menuState.Dock = DockStyle.Fill; menuState.ForeColor = UI.Muted; menuState.Padding = new Padding(0, 4, 0, 0);
            below.Controls.Add(row, 0, 0); below.Controls.Add(menuState, 0, 1);

            root.Controls.Add(selector, 0, 0); root.Controls.Add(cards, 0, 1); root.Controls.Add(below, 0, 2);
            pageMenu.Controls.Add(root);
            pinButtonsHost = pinButtons;
        }

        FlowLayoutPanel pinButtonsHost;

        void SetContext(Target t)
        {
            ctx = t;
            RefreshMenu();
        }

        void RefreshMenu()
        {
            for (int i = 0; i < contextButtons.Length; i++)
            {
                if (contextButtons[i] == null) continue;
                contextButtons[i].Kind = DefaultPins.Contexts[i] == ctx ? FButton.Look.Accent : FButton.Look.Standard;
                contextButtons[i].Invalidate();
            }
            pinsTitle.Text = "Top of the menu for " + DefaultPins.Title(ctx).ToLowerInvariant() + "\nUp to " + Settings.MaxPins + " entries, in this order";
            RefreshPins();
            FillAll();
            FillPreview();
            RefreshPreset();
            RefreshMenuState();
        }

        void RefreshPins()
        {
            string keep = pinList.SelectedItems.Count == 0 ? null : ((Action)pinList.SelectedItems[0].Tag).Id;
            pinList.BeginUpdate();
            pinList.Items.Clear();
            foreach (var id in Settings.PinsFor(ctx))
            {
                var a = Actions.Find(id);
                if (a == null) continue;
                var it = new ListViewItem(a.Label.TrimEnd('.') + (a.Tier == Tier.Elevated ? "   (admin)" : "")) { Tag = a };
                pinList.Items.Add(it);
                if (a.Id == keep) it.Selected = true;
            }
            pinList.EndUpdate();
            FitList(pinList);
            UpdatePinButtons();
        }

        void UpdatePinButtons()
        {
            var buttons = pinButtonsHost == null ? null : pinButtonsHost.Tag as FButton[];
            if (buttons == null) return;
            int i = pinList.SelectedIndices.Count == 0 ? -1 : pinList.SelectedIndices[0];
            buttons[0].Enabled = i > 0;
            buttons[1].Enabled = i >= 0 && i < pinList.Items.Count - 1;
            buttons[2].Enabled = i >= 0;
        }

        Action SelectedPin { get { return pinList.SelectedItems.Count == 0 ? null : (Action)pinList.SelectedItems[0].Tag; } }

        void MoveSelected(int delta)
        {
            var a = SelectedPin; if (a == null) return;
            Settings.MovePin(ctx, a.Id, delta);
            MenuChanged();
        }

        void UnpinSelected()
        {
            var a = SelectedPin; if (a == null) return;
            Settings.Unpin(ctx, a.Id);
            MenuChanged();
            SetStatus("Unpinned \"" + a.Label.TrimEnd('.') + "\".");
        }

        void MenuChanged()
        {
            RefreshPins();
            allList.Invalidate();
            FillPreview();
            RefreshPreset();
            var sel = Selected;
            if (sel != null) UpdatePinAction(sel);
            ScheduleApply();
        }

        IEnumerable<Action> AllActionsFor(string group)
        {
            string q = allFilter.Text.Trim();
            return Actions.All.Where(a => a.Group == group && (a.Targets & ctx) != 0 && a.Usable && a.Group != "Library" && a.Group != "Recipes" && Matches(a, q));
        }

        void FillAll()
        {
            string keep = allList.SelectedItems.Count == 0 ? null : (allList.SelectedItems[0].Tag as Action == null ? null : ((Action)allList.SelectedItems[0].Tag).Id);
            allList.BeginUpdate();
            allList.Items.Clear();
            foreach (var m in Modules.All)
            {
                if (m.Name == "Library" || m.Name == "Recipes") continue;
                var acts = AllActionsFor(m.Name).ToList();
                if (acts.Count == 0) continue;
                allList.Items.Add(FList.Header(m.Name));
                foreach (var a in acts) allList.Items.Add(new ListViewItem(a.Label.TrimEnd('.') + (a.Tier == Tier.Elevated ? "   (admin)" : "")) { Tag = a });
            }
            allList.EndUpdate();
            FitList(allList);
            if (keep != null)
                foreach (ListViewItem it in allList.Items)
                    if (it.Tag is Action && ((Action)it.Tag).Id == keep) { it.Selected = true; break; }
        }

        int AllCheckState(ListViewItem it)
        {
            var a = it.Tag as Action;
            if (a != null) return Settings.IsEnabled(a.Id) ? 1 : 0;
            var h = it.Tag as HeaderRow;
            if (h == null) return -1;
            var acts = AllActionsFor(h.Name).ToList();
            if (acts.Count == 0) return 0;
            int on = acts.Count(x => Settings.IsEnabled(x.Id));
            return on == 0 ? 0 : on == acts.Count ? 1 : 2;
        }

        void AllToggleCheck(ListViewItem it)
        {
            var a = it.Tag as Action;
            if (a != null) Settings.SetEnabled(new[] { a.Id }, !Settings.IsEnabled(a.Id));
            else
            {
                var h = it.Tag as HeaderRow;
                if (h == null) return;
                var acts = AllActionsFor(h.Name).ToList();
                bool turnOn = acts.Any(x => !Settings.IsEnabled(x.Id));
                Settings.SetEnabled(acts.Select(x => x.Id), turnOn);
            }
            MenuChanged();
        }

        int AllPinState(ListViewItem it)
        {
            var a = it.Tag as Action;
            if (a != null) return Settings.IsPinned(a.Id, ctx) ? 1 : 0;
            var h = it.Tag as HeaderRow;
            return h == null ? -1 : Settings.IsGroupPinned(h.Name) ? 1 : 0;
        }

        void AllTogglePin(ListViewItem it)
        {
            var a = it.Tag as Action;
            if (a != null)
            {
                if (Settings.IsPinned(a.Id, ctx)) Settings.Unpin(ctx, a.Id);
                else if (!TryPin(a, ctx)) return;
            }
            else
            {
                var h = it.Tag as HeaderRow;
                if (h == null) return;
                var groups = new HashSet<string>(Settings.GroupPins);
                if (!groups.Remove(h.Name)) groups.Add(h.Name);
                Settings.SetGroupPins(groups);
            }
            MenuChanged();
        }

        void ShowAllMenu(Action a, Point at)
        {
            var menu = new ContextMenuStrip();
            Fx.ThemeMenu(menu);
            bool here = Settings.IsPinned(a.Id, ctx);
            var pinHere = menu.Items.Add(here ? "Unpin here" : "Pin here");
            pinHere.Click += (s, e) =>
            {
                if (here) Settings.Unpin(ctx, a.Id);
                else if (!TryPin(a, ctx)) return;
                MenuChanged();
            };
            var pinAll = menu.Items.Add("Pin in all contexts");
            pinAll.Click += (s, e) =>
            {
                foreach (var t in DefaultPins.Contexts) if ((a.Targets & t) != 0 && !TryPin(a, t)) break;
                MenuChanged();
            };
            menu.Items.Add(new ToolStripSeparator());
            var remove = menu.Items.Add("Remove from menu");
            remove.Click += (s, e) => { Settings.SetEnabled(new[] { a.Id }, false); MenuChanged(); };
            menu.Show(allList, at);
        }

        void FillPreview()
        {
            previewNote.Text = PreviewForm.Render(previewTree, MenuPlan.Build(ctx, previewShift.Checked));
        }

        void RefreshPreset()
        {
            building = true;
            preset.SelectedItem = Settings.CurrentPreset;
            building = false;
        }

        void PresetChosen()
        {
            if (building) return;
            string n = (string)preset.SelectedItem;
            if (n == null || n == Presets.Custom) return;
            bool keep = false;
            if (Settings.HasPins)
            {
                var answer = PinChoice.Ask(this);
                if (answer == PinChoice.Result.Cancel) { RefreshPreset(); return; }
                keep = answer == PinChoice.Result.Keep;
            }
            Settings.ApplyPreset(n, keep);
            RefreshMenu();
            ScheduleApply();
            SetStatus(n + " selected" + (keep ? ", your pins kept." : "."));
        }

        void InstallNow()
        {
            try
            {
                Settings.MarkConfigured();
                Shell.Install();
                SetStatus("Explorer menu updated");
            }
            catch (Exception ex) { Program.Msg("Could not install the menu:\n" + ex.Message); }
            RefreshMenuState();
        }

        void FullUninstall()
        {
            if (!Program.Ask("Remove the Explorer menu, delete PS Tools' settings, undo data and temporary files, and restore any Windows settings it changed?\n\nPSTools.exe itself stays until you delete it.")) return;
            applyTimer.Stop();
            Program.Msg(Shell.UninstallAll());
            RefreshMenuState();
            RefreshState();
        }

        void RefreshMenuState()
        {
            string pending = Tweaks.Pending();
            bool installed = Shell.IsInstalled();
            installBtn.Visible = !installed;
            menuState.Text = (installed ? "Explorer menu: installed. Changes here are saved and applied right away." : "Explorer menu: not installed")
                + "\nData folder: " + Journal.Dir
                + (pending.Length > 0 ? "\nWindows settings changed: " + pending : "");
        }
    }
}
