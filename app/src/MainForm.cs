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
        readonly Label title = new Label(), subtitle = new Label(), hint = new Label();
        readonly Label[] factKey = new Label[4], factVal = new Label[4];
        readonly Label psCaption = new Label { Text = "PowerShell", AutoSize = true, ForeColor = UI.Muted, Font = UI.SmallBold, Margin = new Padding(0, 10, 0, 0) };
        readonly Label psNote = new Label { AutoSize = true, ForeColor = UI.Muted, Font = UI.Small, Margin = new Padding(0, 8, 0, 0), Text = "{path} is replaced by the folder in the box above when you copy. Nothing here runs the command." };
        readonly TextBox psBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = UI.Mono };
        readonly FButton psCopy = new FButton { Text = "Copy command", AutoSize = true, Height = 32, Margin = new Padding(0, 0, 8, 0) };
        readonly FButton psOpen = new FButton { Text = "Open in PowerShell", AutoSize = true, Height = 32, Margin = new Padding(0) };
        readonly FButton run = new FButton(), undo = new FButton();
        readonly NavRail rail = new NavRail();
        readonly ToolTip tips = new ToolTip();
        readonly Label status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        readonly Panel pageActions = new BPanel { Dock = DockStyle.Fill };
        readonly Panel pageMenu = new BPanel { Dock = DockStyle.Fill, Visible = false };
        readonly Label menuState = new Label(), dataLine = new Label();
        readonly FButton removeBtn = new FButton { Text = "Remove Explorer menu" };
        readonly FButton uninstallBtn = new FButton { Text = "Full uninstall..." };
        readonly FDrop theme = new FDrop();
        readonly FDrop preset = new FDrop();
        readonly FButton pinAction = new FButton { Text = "Pin to menu...", AutoSize = true, Height = 36, Glyph = Fx.Pin, Margin = new Padding(0, 0, 0, 0) };
        readonly Timer statusTimer = new Timer { Interval = 7000 };
        readonly Timer applyTimer = new Timer { Interval = 700 };
        readonly Timer scanTimer = new Timer { Interval = 300 };
        readonly FButton[] contextButtons = new FButton[4];
        readonly Label pinsTitle = new Label(), pinsNote = new Label(), previewNote = new Label();
        readonly HashSet<string> folded = new HashSet<string>();
        readonly ListView pinList = new ListView();
        readonly ListView allList = new ListView();
        readonly TextBox allFilter = new TextBox();
        readonly ListView previewList = new ListView();
        readonly HashSet<string> previewOpen = new HashSet<string>();
        readonly List<MethodInvoker> metrics = new List<MethodInvoker>();
        readonly FToggle previewShift = new FCheck { Text = "Holding Shift", AutoSize = true };
        readonly FButton installBtn = new FButton { Text = "Install Explorer menu", Visible = false };
        BTable detail;
        BTable facts;
        bool building;
        Target ctx = Target.Folder;
        List<Suggestion> suggestions = new List<Suggestion>();
        string suggestionKey = "";
        int scanId;

        public MainForm(string startPath)
        {
            Text = "PS Tools"; Font = UI.Font; ClientSize = new Size(1040, 720); MinimumSize = new Size(900, 540); DoubleBuffered = true;
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

            var top = new BTable { Dock = DockStyle.Top, Height = 62, ColumnCount = 3, Padding = new Padding(0, 14, 0, 8) };
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

            metrics.Add(() =>
            {
                top.Height = UI.S(62); top.Padding = new Padding(0, UI.S(14), 0, UI.S(8));
                foreach (var tb in new[] { browse, open }) { tb.Height = UI.S(32); tb.Margin = new Padding(UI.S(8), 0, 0, 0); }
                browse.Width = UI.S(110); open.Width = UI.S(92);
            });
            BuildActionsPage();
            BuildMenuPage();

            undo.Text = "Undo last"; undo.Glyph = Fx.Undo; undo.Width = 128; undo.Height = 32; undo.Dock = DockStyle.Right; undo.Click += (s, e) => DoUndo();
            status.ForeColor = UI.Muted; status.Font = UI.Small;
            var bottom = new BPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(2, 10, 0, 0) };
            bottom.Controls.Add(status); bottom.Controls.Add(undo);

            metrics.Add(() => { bottom.Height = UI.S(46); bottom.Padding = new Padding(UI.S(2), UI.S(10), 0, 0); undo.Width = UI.S(128); undo.Height = UI.S(32); });
            var content = new BPanel { Dock = DockStyle.Fill };
            metrics.Add(() => content.Padding = new Padding(UI.S(4), 0, UI.S(18), UI.S(12)));
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
            Shown += (s, e) => { ApplyMetrics(); search.Focus(); StartScan(); };
            FormClosing += (s, e) => { SaveWindow(); if (applyTimer.Enabled) { applyTimer.Stop(); ApplyNow(); } };

            UI.Apply(this);
            ApplyMetrics();
            RestoreWindow();
            Fill();
            RefreshState();
            RefreshMenu();
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
            if (detail == null) return;
            int inner = Math.Max(120, detail.ClientSize.Width - detail.Padding.Horizontal);
            hint.MaximumSize = new Size(inner, 0);
            psNote.MaximumSize = new Size(inner, 0);
            int key = UI.S(110);
            foreach (var v in factVal) v.MaximumSize = new Size(Math.Max(60, inner - key), 0);
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
            var left = new BPanel { Dock = DockStyle.Left, Width = 340, Padding = new Padding(0, 0, 10, 0) };
            search.Dock = DockStyle.Fill;
            ResultsForm.SetCue(search, "Search actions  (Ctrl+F)");
            search.TextChanged += (s, e) => Fill();
            search.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down && list.Items.Count > 0) { list.Focus(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { RunSelected(); e.SuppressKeyPress = true; }
            };
            var searchWrap = new BPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 0, 0, 10) };
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
            metrics.Add(() =>
            {
                left.Width = UI.S(340); left.Padding = new Padding(0, 0, UI.S(10), 0);
                searchWrap.Height = UI.S(46); searchWrap.Padding = new Padding(0, 0, 0, UI.S(10));
            });

            var right = new FCard { Dock = DockStyle.Fill, Padding = new Padding(26, 22, 22, 18) };
            detail = new BTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
            detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 8; i++) detail.RowStyles.Add(i == 5 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));

            title.AutoSize = false; title.Dock = DockStyle.Fill; title.Font = UI.Title; title.AutoEllipsis = true; title.Height = UI.Title.Height + 8; title.Margin = new Padding(0);
            subtitle.AutoSize = false; subtitle.Dock = DockStyle.Fill; subtitle.Font = UI.Small; subtitle.ForeColor = UI.Muted; subtitle.AutoEllipsis = true; subtitle.Height = UI.Small.Height + 6; subtitle.Margin = new Padding(0);
            hint.AutoSize = true; hint.Font = UI.Font; hint.Margin = new Padding(0, 12, 0, 12);

            facts = new BTable { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 4, Margin = new Padding(0) };
            facts.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++)
            {
                facts.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                factKey[i] = new Label { AutoSize = true, Font = UI.Small, ForeColor = UI.Muted, Margin = new Padding(0, 2, 0, 2) };
                factVal[i] = new Label { AutoSize = true, Font = UI.Small, ForeColor = UI.Text, Margin = new Padding(0, 2, 0, 2) };
                facts.Controls.Add(factKey[i], 0, i);
                facts.Controls.Add(factVal[i], 1, i);
            }

            var psHeader = new BTable { Dock = DockStyle.Fill, Height = 46, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 6, 0, 0), Padding = new Padding(0, 0, 0, 6) };
            psHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            psHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            psHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            psCaption.Dock = DockStyle.Fill; psCaption.AutoSize = false; psCaption.TextAlign = ContentAlignment.MiddleLeft; psCaption.Margin = new Padding(0);
            var psButtons = new BFlow { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Right };
            psButtons.Controls.Add(psCopy); psButtons.Controls.Add(psOpen);
            psHeader.Controls.Add(psCaption, 0, 0); psHeader.Controls.Add(psButtons, 1, 0);
            psCopy.Click += (s, e) => CopyCommand();
            psOpen.Click += (s, e) => OpenWithCommand();
            tips.SetToolTip(psCopy, "Copies the command with the folder from the box above filled in");
            tips.SetToolTip(psOpen, "Opens PowerShell in the folder with the command on the clipboard. Paste it with a right-click; nothing runs by itself.");
            psBox.Margin = new Padding(0);

            var buttons = new BFlow { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 14, 0, 0) };
            run.Text = "Run"; run.Glyph = Fx.Play; run.Width = 120; run.Height = 36; run.Margin = new Padding(0, 0, 8, 0); UI.Primary(run); run.Click += (s, e) => RunSelected();
            buttons.Controls.Add(run);
            pinAction.Margin = new Padding(0);
            pinAction.Click += (s, e) => ShowPinMenu();
            tips.SetToolTip(pinAction, "Choose where to pin this action at the top level of the right-click menu");
            buttons.Controls.Add(pinAction);

            detail.Controls.Add(title, 0, 0);
            detail.Controls.Add(subtitle, 0, 1);
            detail.Controls.Add(hint, 0, 2);
            detail.Controls.Add(facts, 0, 3);
            detail.Controls.Add(psHeader, 0, 4);
            detail.Controls.Add(psBox, 0, 5);
            detail.Controls.Add(psNote, 0, 6);
            detail.Controls.Add(buttons, 0, 7);
            detail.Resize += (s, e) => FitActions();
            right.Controls.Add(detail);
            metrics.Add(() =>
            {
                right.Padding = new Padding(UI.S(26), UI.S(22), UI.S(22), UI.S(18));
                hint.Margin = new Padding(0, UI.S(12), 0, UI.S(12));
                facts.ColumnStyles[0].Width = UI.S(120);
                foreach (var l in factKey) l.Margin = new Padding(0, UI.S(2), 0, UI.S(2));
                foreach (var l in factVal) l.Margin = new Padding(0, UI.S(2), 0, UI.S(2));
                psHeader.Height = UI.S(48); psHeader.Margin = new Padding(0, UI.S(6), 0, 0); psHeader.Padding = new Padding(0, 0, 0, UI.S(8));
                psCopy.AutoSize = false; psOpen.AutoSize = false;
                psCopy.Height = psOpen.Height = UI.S(34);
                psCopy.Width = Math.Max(UI.S(120), psCopy.GetPreferredSize(Size.Empty).Width);
                psOpen.Width = Math.Max(UI.S(150), psOpen.GetPreferredSize(Size.Empty).Width);
                psCopy.Margin = new Padding(0, 0, UI.S(8), 0);
                psNote.Margin = new Padding(0, UI.S(8), 0, 0);
                buttons.Margin = new Padding(0, UI.S(14), 0, 0);
                run.Width = UI.S(120); run.Height = UI.S(38); run.Margin = new Padding(0, 0, UI.S(8), 0);
                pinAction.AutoSize = false; pinAction.Height = UI.S(38); pinAction.Width = Math.Max(UI.S(160), pinAction.GetPreferredSize(Size.Empty).Width + UI.S(8));
            });

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
            else { title.Text = ""; subtitle.Text = ""; hint.Text = "No match."; SetFacts(null, null, null, null); ClearCommand(); FitActions(); }
        }

        internal static bool Matches(Action a, string q)
        {
            return q.Length == 0 || (a.Label + " " + a.Hint + " " + a.Group + " " + a.Id).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        Action Selected { get { return list.SelectedItems.Count == 0 ? null : ActionOf(list.SelectedItems[0]); } }

        static string Where(Action a)
        {
            if (a.Targets == Target.None) return "Runs from this window only";
            var w = new List<string>();
            if ((a.Targets & Target.Folder) != 0) w.Add("folders");
            if ((a.Targets & Target.Background) != 0) w.Add("folder background");
            if ((a.Targets & Target.Drive) != 0) w.Add("drives");
            if ((a.Targets & Target.File) != 0) w.Add("files");
            return string.Join(", ", w.ToArray()) + (a.Multi ? " (multi-select)" : "");
        }

        void SetFacts(string changes, string undoText, string where, string safety)
        {
            string[] keys = { "Changes files", "Undo", "Right-click", "Safety" };
            string[] vals = { changes, undoText, where, safety };
            for (int i = 0; i < 4; i++)
            {
                bool on = !string.IsNullOrEmpty(vals[i]);
                factKey[i].Text = on ? keys[i] : "";
                factVal[i].Text = on ? vals[i] : "";
                factKey[i].Visible = factVal[i].Visible = on;
            }
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
            subtitle.Text = a.Group + (a.Tier == Tier.Elevated ? "  \u00B7  Administrator" : "");
            hint.Text = a.Hint;
            string safety = null;
            if (a.Tier == Tier.Elevated) safety = "Needs administrator rights. Shows exactly what it will run first.";
            else if (a.Confirm != null) safety = "Asks for confirmation before changing anything.";
            SetFacts(ChangesText(a), a.Undoable ? "Yes, with Undo last" : "No", Where(a), safety);
            run.Text = a.Window != null ? "Open" : "Run";
            ShowCommand(a);
            UpdatePinAction(a);
            FitActions();
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
            pinAction.Text = a != null && Settings.IsPinnedAnywhere(a.Id) ? "Pinned to menu..." : "Pin to menu...";
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

        static void DockLeft(Control host, params Control[] visualOrder)
        {
            for (int i = visualOrder.Length - 1; i >= 0; i--)
            {
                visualOrder[i].Dock = DockStyle.Left;
                host.Controls.Add(visualOrder[i]);
            }
        }

        static void DockRight(Control host, params Control[] visualOrder)
        {
            foreach (var c in visualOrder)
            {
                c.Dock = DockStyle.Right;
                host.Controls.Add(c);
            }
        }

        BPanel Gap(int logical)
        {
            var g = new BPanel { Dock = DockStyle.Left };
            metrics.Add(() => g.Width = UI.S(logical));
            return g;
        }

        Label Caption(string text)
        {
            var l = new Label { Text = text, AutoSize = false, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft, Font = UI.Font };
            metrics.Add(() => l.Width = TextRenderer.MeasureText(text, l.Font, new Size(2000, 100), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width + UI.S(6));
            return l;
        }

        void Pad(Control c, int l, int t, int r, int b)
        {
            metrics.Add(() => c.Padding = new Padding(UI.S(l), UI.S(t), UI.S(r), UI.S(b)));
        }

        void Tall(Control c, int logical)
        {
            metrics.Add(() => c.Height = UI.S(logical));
        }

        void Wide(FButton b, int minLogical)
        {
            metrics.Add(() => b.Width = Math.Max(UI.S(minLogical), b.GetPreferredSize(Size.Empty).Width));
        }

        void ApplyMetrics()
        {
            foreach (var m in metrics) m();
            FitList(list); FitList(pinList); FitList(allList); FitList(previewList);
            FitActions();
        }

        void BuildMenuPage()
        {
            var cards = new BTable { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var pinsCard = new FCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
            Pad(pinsCard, 16, 14, 12, 12);
            metrics.Add(() => pinsCard.Margin = new Padding(0, 0, UI.S(10), 0));
            pinList.Dock = DockStyle.Fill; pinList.View = View.Details; pinList.HeaderStyle = ColumnHeaderStyle.None; pinList.FullRowSelect = true;
            pinList.MultiSelect = false; pinList.HideSelection = false; pinList.BorderStyle = BorderStyle.None; pinList.Font = UI.Font;
            pinList.Columns.Add("", 200);
            pinList.Resize += (s, e) => FitList(pinList);
            pinList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) { UnpinSelected(); e.Handled = true; } };
            pinList.SelectedIndexChanged += (s, e) => UpdatePinButtons();
            pinsTitle.Dock = DockStyle.Top; pinsTitle.Font = UI.Bold; pinsTitle.Text = "Top of the menu"; pinsTitle.TextAlign = ContentAlignment.MiddleLeft;
            pinsNote.Dock = DockStyle.Top; pinsNote.Font = UI.Small; pinsNote.ForeColor = UI.Muted; pinsNote.AutoEllipsis = true;
            Tall(pinsTitle, 26); Tall(pinsNote, 26);
            var addRow = new BPanel { Dock = DockStyle.Bottom };
            Tall(addRow, 48); Pad(addRow, 0, 8, 0, 8);
            var add = new FButton { Text = "Add...", Glyph = Fx.Add, Dock = DockStyle.Fill, Kind = FButton.Look.Accent };
            add.Click += (s, e) => ShowAddMenu(add);
            addRow.Controls.Add(add);
            var moveRow = new BTable { Dock = DockStyle.Bottom, ColumnCount = 3, RowCount = 1 };
            for (int i = 0; i < 3; i++) moveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            moveRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Tall(moveRow, 38);
            var up = new FButton { Text = "Up", Dock = DockStyle.Fill, Margin = new Padding(0) };
            var down = new FButton { Text = "Down", Dock = DockStyle.Fill, Margin = new Padding(0) };
            var unpin = new FButton { Text = "Unpin", Dock = DockStyle.Fill, Margin = new Padding(0) };
            metrics.Add(() =>
            {
                up.Margin = new Padding(0, 0, UI.S(3), 0);
                down.Margin = new Padding(UI.S(3), 0, UI.S(3), 0);
                unpin.Margin = new Padding(UI.S(3), 0, 0, 0);
            });
            up.Click += (s, e) => MoveSelected(-1);
            down.Click += (s, e) => MoveSelected(1);
            unpin.Click += (s, e) => UnpinSelected();
            moveRow.Controls.Add(up, 0, 0); moveRow.Controls.Add(down, 1, 0); moveRow.Controls.Add(unpin, 2, 0);
            pinButtonsHost = new[] { up, down, unpin };
            pinsCard.Controls.Add(pinList); pinsCard.Controls.Add(addRow); pinsCard.Controls.Add(moveRow); pinsCard.Controls.Add(pinsNote); pinsCard.Controls.Add(pinsTitle);

            var allCard = new FCard { Dock = DockStyle.Fill };
            Pad(allCard, 16, 14, 12, 12);
            metrics.Add(() => allCard.Margin = new Padding(0, 0, UI.S(10), 0));
            allList.Dock = DockStyle.Fill; allList.View = View.Details; allList.HeaderStyle = ColumnHeaderStyle.None; allList.FullRowSelect = true;
            allList.MultiSelect = false; allList.HideSelection = false; allList.BorderStyle = BorderStyle.None; allList.Font = UI.Font;
            allList.Columns.Add("", 300);
            FList.Attach(allList, 34, null);
            FList.Decorate(allList, new RowDeco
            {
                Check = AllCheckState,
                OnCheck = AllToggleCheck,
                Pin = AllPinState,
                OnPin = AllTogglePin,
                Fold = AllFoldState,
                OnFold = AllToggleFold
            });
            allList.Resize += (s, e) => FitList(allList);
            allList.KeyDown += (s, e) =>
            {
                if (allList.SelectedItems.Count == 0) return;
                if (e.KeyCode == Keys.Space) { AllToggleCheck(allList.SelectedItems[0]); e.Handled = true; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { AllTogglePin(allList.SelectedItems[0]); e.Handled = true; e.SuppressKeyPress = true; }
            };
            allList.DoubleClick += (s, e) =>
            {
                var pt = allList.PointToClient(Cursor.Position);
                var it = allList.GetItemAt(Math.Min(pt.X, Math.Max(0, allList.ClientSize.Width - 2)), pt.Y);
                if (it != null && it.Tag is Action && pt.X > UI.S(48) && pt.X < allList.ClientSize.Width - UI.S(48)) AllTogglePin(it);
            };
            allList.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                var it = allList.GetItemAt(e.X, e.Y);
                if (it == null || FList.IsHeader(it)) return;
                it.Selected = true;
                ShowAllMenu((Action)it.Tag, e.Location);
            };
            var allTitle = new Label { Text = "All actions", Dock = DockStyle.Top, Font = UI.Bold, TextAlign = ContentAlignment.MiddleLeft };
            var allNote = new Label { Text = "Tick to show it, star to pin it.", Dock = DockStyle.Top, Font = UI.Small, ForeColor = UI.Muted, AutoEllipsis = true };
            Tall(allTitle, 26); Tall(allNote, 26);
            ResultsForm.SetCue(allFilter, "Filter actions");
            allFilter.Dock = DockStyle.Fill;
            allFilter.TextChanged += (s, e) => FillAll();
            allFilter.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { allFilter.Text = ""; e.SuppressKeyPress = true; } };
            var filterWrap = new BPanel { Dock = DockStyle.Top };
            Tall(filterWrap, 48); Pad(filterWrap, 0, 4, 0, 8);
            filterWrap.Controls.Add(allFilter);
            allCard.Controls.Add(allList); allCard.Controls.Add(filterWrap); allCard.Controls.Add(allNote); allCard.Controls.Add(allTitle);

            var previewCard = new FCard { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Pad(previewCard, 16, 14, 12, 12);
            var previewTitle = new Label { Text = "What you will see", Dock = DockStyle.Top, Font = UI.Bold, TextAlign = ContentAlignment.MiddleLeft };
            Tall(previewTitle, 26);
            previewShift.Dock = DockStyle.Top; previewShift.Font = UI.Font;
            Tall(previewShift, 34);
            previewShift.CheckedChanged += (s, e) => FillPreview();
            previewList.Dock = DockStyle.Fill; previewList.View = View.Details; previewList.HeaderStyle = ColumnHeaderStyle.None; previewList.FullRowSelect = true;
            previewList.MultiSelect = false; previewList.HideSelection = true; previewList.BorderStyle = BorderStyle.None; previewList.Font = UI.Font;
            previewList.Columns.Add("", 300);
            previewList.Tag = "static";
            FList.Attach(previewList, 30, null);
            FList.Decorate(previewList, new RowDeco { Fold = PreviewFoldState, OnFold = PreviewToggleFold });
            previewList.Resize += (s, e) => FitList(previewList);
            previewNote.Dock = DockStyle.Bottom; previewNote.Font = UI.Small; previewNote.ForeColor = UI.Muted;
            Tall(previewNote, 44);
            previewCard.Controls.Add(previewList); previewCard.Controls.Add(previewNote); previewCard.Controls.Add(previewShift); previewCard.Controls.Add(previewTitle);

            cards.Controls.Add(pinsCard, 0, 0); cards.Controls.Add(allCard, 1, 0); cards.Controls.Add(previewCard, 2, 0);

            var selector = new BFlow { Dock = DockStyle.Top, WrapContents = false };
            Tall(selector, 54); Pad(selector, 0, 8, 0, 8);
            for (int i = 0; i < DefaultPins.Contexts.Length; i++)
            {
                var t = DefaultPins.Contexts[i];
                var b = new FButton { Text = DefaultPins.Title(t) };
                b.Click += (s, e) => SetContext(t);
                contextButtons[i] = b;
                selector.Controls.Add(b);
                var bt = b;
                metrics.Add(() => { bt.Width = UI.S(140); bt.Height = UI.S(38); bt.Margin = new Padding(0, 0, UI.S(8), 0); });
            }

            var footer = new BPanel { Dock = DockStyle.Bottom };
            Tall(footer, 124);

            var prefs = new BPanel { Dock = DockStyle.Top };
            Tall(prefs, 46); Pad(prefs, 0, 6, 0, 4);
            var themeNote = new Label { Text = "Applies next start", Dock = DockStyle.Fill, Font = UI.Small, ForeColor = UI.Muted, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            preset.Items.AddRange(Presets.Names);
            preset.Items.Add(Presets.Custom);
            preset.SelectedIndexChanged += (s, e) => PresetChosen();
            theme.Items.AddRange(new[] { "auto", "light", "dark" });
            Settings.EnsureLoaded();
            theme.SelectedItem = Settings.Theme;
            if (theme.SelectedIndex < 0) theme.SelectedIndex = 0;
            theme.SelectedIndexChanged += (s, e) => { Settings.SetTheme(theme.SelectedItem); SetStatus("Theme saved. It applies the next time PS Tools starts."); };
            var guide = new FButton { Text = "Setup guide..." };
            guide.Click += (s, e) => new FirstRunForm(true).Show(this);
            Wide(guide, 140);
            metrics.Add(() => { preset.Width = UI.S(150); theme.Width = UI.S(110); });
            prefs.Controls.Add(themeNote);
            DockRight(prefs, guide);
            DockLeft(prefs, Caption("Preset"), Gap(8), preset, Gap(24), Caption("Theme"), Gap(8), theme, Gap(16));

            var state = new BPanel { Dock = DockStyle.Top };
            Tall(state, 44); Pad(state, 0, 4, 0, 4);
            menuState.Dock = DockStyle.Fill; menuState.AutoSize = false; menuState.TextAlign = ContentAlignment.MiddleLeft; menuState.AutoEllipsis = true; menuState.Font = UI.Font;
            installBtn.Kind = FButton.Look.Accent; installBtn.AutoSize = false;
            installBtn.Click += (s, e) => InstallNow();
            removeBtn.AutoSize = false; uninstallBtn.AutoSize = false;
            removeBtn.Click += (s, e) => { applyTimer.Stop(); Shell.RemoveMenu(); RefreshMenuState(); SetStatus("Explorer menu removed."); };
            uninstallBtn.Click += (s, e) => FullUninstall();
            tips.SetToolTip(removeBtn, "Removes the right-click entries only. Your settings stay.");
            tips.SetToolTip(uninstallBtn, "Removes the menu, settings, undo data and temporary files, and restores any Windows settings PS Tools changed");
            Wide(installBtn, 170); Wide(removeBtn, 170); Wide(uninstallBtn, 150);
            state.Controls.Add(menuState);
            var g1 = Gap(8); var g2 = Gap(8);
            DockRight(state, installBtn, g1, removeBtn, g2, uninstallBtn);
            foreach (var g in new[] { g1, g2 }) g.Dock = DockStyle.Right;

            dataLine.Dock = DockStyle.Fill; dataLine.AutoEllipsis = true; dataLine.Font = UI.Small; dataLine.ForeColor = UI.Muted; dataLine.TextAlign = ContentAlignment.TopLeft;
            Pad(dataLine, 0, 4, 0, 0);

            footer.Controls.Add(dataLine); footer.Controls.Add(state); footer.Controls.Add(prefs);

            pageMenu.Controls.Add(cards); pageMenu.Controls.Add(footer); pageMenu.Controls.Add(selector);
        }

        FButton[] pinButtonsHost;

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
            pinsNote.Text = "Up to " + Settings.MaxPins + " pins, in this order";
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
            var buttons = pinButtonsHost;
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
            var keepItem = allList.SelectedItems.Count == 0 ? null : allList.SelectedItems[0].Tag as Action;
            string keep = keepItem == null ? null : keepItem.Id;
            bool filtering = allFilter.Text.Trim().Length > 0;
            allList.BeginUpdate();
            allList.Items.Clear();
            foreach (var m in Modules.All)
            {
                if (m.Name == "Library" || m.Name == "Recipes") continue;
                var acts = AllActionsFor(m.Name).ToList();
                if (acts.Count == 0) continue;
                allList.Items.Add(FList.Header(m.Name));
                if (!filtering && folded.Contains(m.Name)) continue;
                foreach (var a in acts) allList.Items.Add(new ListViewItem(a.Label.TrimEnd('.') + (a.Tier == Tier.Elevated ? "   (admin)" : "")) { Tag = a });
            }
            allList.EndUpdate();
            FitList(allList);
            if (keep != null)
                foreach (ListViewItem it in allList.Items)
                    if (it.Tag is Action && ((Action)it.Tag).Id == keep) { it.Selected = true; break; }
        }

        int AllFoldState(ListViewItem it)
        {
            var h = it.Tag as HeaderRow;
            if (h == null || allFilter.Text.Trim().Length > 0) return -1;
            return folded.Contains(h.Name) ? 1 : 0;
        }

        void AllToggleFold(ListViewItem it)
        {
            var h = it.Tag as HeaderRow;
            if (h == null) return;
            if (!folded.Remove(h.Name)) folded.Add(h.Name);
            BeginInvoke((MethodInvoker)FillAll);
        }

        void ShowAddMenu(Control anchor)
        {
            var menu = new ContextMenuStrip();
            Fx.ThemeMenu(menu);
            var pinned = new HashSet<string>(Settings.PinsFor(ctx));
            foreach (var m in Modules.All)
            {
                if (m.Name == "Library" || m.Name == "Recipes") continue;
                var acts = Actions.All.Where(a => a.Group == m.Name && (a.Targets & ctx) != 0 && a.Usable && a.Tier != Tier.Elevated).ToList();
                if (acts.Count == 0) continue;
                int free = acts.Count(a => !pinned.Contains(a.Id));
                var sub = new ToolStripMenuItem(m.Name + "   (" + free + ")") { Enabled = free > 0 };
                sub.DropDown.Renderer = menu.Renderer;
                sub.DropDown.BackColor = menu.BackColor;
                sub.DropDown.ForeColor = menu.ForeColor;
                sub.DropDown.Font = menu.Font;
                foreach (var a in acts)
                {
                    if (pinned.Contains(a.Id)) continue;
                    var action = a;
                    var item = new ToolStripMenuItem(a.Label.TrimEnd('.'));
                    item.Click += (s, e) => { if (TryPin(action, ctx)) { MenuChanged(); SetStatus("Pinned \"" + action.Label.TrimEnd('.') + "\"."); } };
                    sub.DropDownItems.Add(item);
                }
                menu.Items.Add(sub);
            }
            menu.Items.Add(new ToolStripSeparator());
            var defaults = new ToolStripMenuItem("Use the suggested pins for " + DefaultPins.Title(ctx).ToLowerInvariant());
            defaults.Click += (s, e) =>
            {
                var ids = DefaultPins.For(ctx).Where(id => { var a = Actions.Find(id); return a != null && a.Usable && (a.Targets & ctx) != 0; }).ToList();
                Settings.SetPins(ctx, ids);
                MenuChanged();
                SetStatus("Suggested pins restored.");
            };
            menu.Items.Add(defaults);
            menu.Show(anchor, new Point(0, anchor.Height));
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
            var plan = MenuPlan.Build(ctx, previewShift.Checked);
            previewList.BeginUpdate();
            previewList.Items.Clear();
            if (plan.OpenEntry) previewList.Items.Add(new ListViewItem("Open in PS Tools"));
            foreach (var a in plan.Pinned) previewList.Items.Add(new ListViewItem("\u2605  " + a.Label.TrimEnd('.') + PreviewForm.Qualifier(a)));
            foreach (var g in plan.Groups)
            {
                previewList.Items.Add(new ListViewItem((g.Pinned ? "\u2605  " : "") + g.Name + "   " + g.Items.Count) { Tag = new HeaderRow { Name = g.Name } });
                if (!previewOpen.Contains(g.Name)) continue;
                foreach (var a in g.Items) previewList.Items.Add(new ListViewItem(a.Label.TrimEnd('.') + PreviewForm.Qualifier(a) + (a.Tier == Tier.Elevated ? "   (admin)" : "")));
            }
            previewList.EndUpdate();
            FitList(previewList);
            previewNote.Text = PreviewForm.Note(plan);
        }

        int PreviewFoldState(ListViewItem it)
        {
            var h = it.Tag as HeaderRow;
            return h == null ? -1 : previewOpen.Contains(h.Name) ? 0 : 1;
        }

        void PreviewToggleFold(ListViewItem it)
        {
            var h = it.Tag as HeaderRow;
            if (h == null) return;
            if (!previewOpen.Remove(h.Name)) previewOpen.Add(h.Name);
            BeginInvoke((MethodInvoker)FillPreview);
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
            string n = preset.SelectedItem;
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
            removeBtn.Visible = installed;
            menuState.Text = installed ? "Explorer menu installed. Changes apply right away." : "Explorer menu is not installed.";
            dataLine.Text = "Data folder: " + Journal.Dir + (pending.Length > 0 ? "     Windows settings changed: " + pending : "");
        }
    }
}
