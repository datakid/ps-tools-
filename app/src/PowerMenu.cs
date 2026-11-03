using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PSTools
{
    class PowerMenuOptions
    {
        public string InitialSearch = "";
        public string[] Drill = new string[0];
        public bool Details;
        public bool Harness;
        public Point? Location;
        public string Summary;
        public Func<string, bool> ToggleFavorite;
        public Action<string> NoteUsed;
    }

    class PowerMenu : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        readonly MenuQuery query;
        readonly PowerMenuOptions options;
        readonly string[] paths;
        readonly HeaderBar header;
        readonly FSearchBox search;
        readonly FBreadcrumb crumbs = new FBreadcrumb();
        readonly FTreeList list = new FTreeList();
        readonly EmptyState empty = new EmptyState();
        readonly FooterBar footer = new FooterBar();
        readonly DetailsPane details = new DetailsPane();
        readonly List<MenuNode> stack = new List<MenuNode>();
        readonly List<int> stackSelection = new List<int>();
        List<MenuNode> root = new List<MenuNode>();
        bool showDetails;
        bool finished;
        bool crumbsOn, emptyOn;

        public System.Action ToRun;

        public PowerMenu(MenuQuery q, PowerMenuOptions opt)
        {
            query = q;
            options = opt ?? new PowerMenuOptions();
            paths = q.Paths ?? new string[0];
            showDetails = options.Details;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            Font = UI.Font;
            Text = "PS Tools Power menu";
            Padding = new Padding(1);
            TopMost = !options.Harness;

            header = new HeaderBar(this);
            search = new FSearchBox("Type to search actions");
            search.Input.TextChanged += (s, e) => { stack.Clear(); stackSelection.Clear(); Rebuild(0); };
            search.Input.KeyDown += OnSearchKeyDown;
            list.Activated += (s, e) => ActivateSelected();
            list.MouseUp += (s, e) => { if (inputPanel == null) search.Input.Focus(); };
            list.SelectionChanged += (s, e) => { UpdateDetails(); footer.Folder = SelectedFolder(); footer.Invalidate(); };
            empty.RunRequested += RunTypedText;
            crumbs.LevelClicked += level => { while (stack.Count > level) { stack.RemoveAt(stack.Count - 1); stackSelection.Clear(); } Rebuild(0); };
            list.RowRightClicked += (s, e) => ShowRowMenu(e.Location);
            AccessibleName = "PS Tools Power menu";
            AccessibleDescription = "";
            Controls.Add(list);
            Controls.Add(empty);
            Controls.Add(crumbs);
            Controls.Add(search);
            Controls.Add(header);
            Controls.Add(footer);
            Controls.Add(details);

            UI.Round(this);
            UI.Apply(this, false);
            BackColor = Tok.Stroke;
            header.BackColor = Tok.SurfaceAlt;
            footer.BackColor = Tok.SurfaceAlt;
            search.BackColor = Tok.SurfaceAlt;
            search.RefreshTheme();

            root = MenuTree.Build(query);
            foreach (string title in options.Drill)
            {
                var folder = Current().FirstOrDefault(n => n.Kind == NodeKind.Folder && n.Title == title);
                if (folder == null) break;
                stack.Add(folder);
                stackSelection.Add(0);
            }
            if (options.InitialSearch.Length > 0) search.Input.Text = options.InitialSearch;
            else Rebuild(0);
            Relayout();
            AccessibleDescription = SummaryText;

            Shown += (s, e) =>
            {
                if (!options.Harness) TakeForeground();
                search.Input.Focus();
                search.Input.SelectionStart = search.Input.TextLength;
            };
            Deactivate += (s, e) => { if (!options.Harness && !finished) CloseMenu(); };
            if (options.Location.HasValue) Location = options.Location.Value;
            else PlaceNearCursor();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return options != null && options.Harness; } }

        public string SummaryText
        {
            get
            {
                if (!string.IsNullOrEmpty(options.Summary)) return FGfx.MiddleEllipsis(options.Summary, 38);
                if (paths.Length == 0) return "This folder";
                if (paths.Length == 1) return FGfx.MiddleEllipsis(paths[0], 38);
                var isDir = query.IsDirectory ?? (p => { try { return System.IO.Directory.Exists(p); } catch { return false; } });
                int dirs = paths.Count(p => isDir(p));
                int files = paths.Length - dirs;
                return paths.Length + " items \u00B7 " + dirs + " folder" + (dirs == 1 ? "" : "s") + ", " + files + " file" + (files == 1 ? "" : "s");
            }
        }

        public string SummaryGlyph
        {
            get
            {
                if (paths.Length > 1) return "\uE8B7";
                if (paths.Length == 1 && (query.Context & ContextKind.File) != 0 && (query.Context & ContextKind.Folder) == 0) return "\uE7C3";
                return "\uE8B7";
            }
        }

        public static Point PlaceAt(Point cursor, Size size, Rectangle area)
        {
            int x = cursor.X + 2, y = cursor.Y + 2;
            if (x + size.Width > area.Right) x = area.Right - size.Width;
            if (y + size.Height > area.Bottom)
            {
                int up = cursor.Y - 2 - size.Height;
                y = up >= area.Top ? up : area.Bottom - size.Height;
            }
            return new Point(Math.Max(area.Left, x), Math.Max(area.Top, y));
        }

        void PlaceNearCursor()
        {
            var pt = Cursor.Position;
            Location = PlaceAt(pt, Size, Screen.FromPoint(pt).WorkingArea);
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        void TakeForeground()
        {
            Activate();
            try
            {
                if (SetForegroundWindow(Handle) && GetForegroundWindow() == Handle) return;
                uint pid;
                uint fore = GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                uint me = GetCurrentThreadId();
                if (fore != 0 && fore != me)
                {
                    AttachThreadInput(me, fore, true);
                    SetForegroundWindow(Handle);
                    AttachThreadInput(me, fore, false);
                }
            }
            catch { }
        }

        List<MenuNode> Current()
        {
            return stack.Count == 0 ? root : stack[stack.Count - 1].Children;
        }

        bool Searching { get { return search.Input.Text.Trim().Length > 0; } }

        bool SelectedFolder()
        {
            var r = list.SelectedRow;
            return r != null && r.Folder;
        }

        void Rebuild(int select)
        {
            var rows = new List<PmRow>();
            string text = search.Input.Text.Trim();
            var favs = new HashSet<string>(query.Favorites, StringComparer.OrdinalIgnoreCase);
            if (text.Length > 0)
            {
                foreach (var n in MenuTree.Search(query, text))
                    rows.Add(ActionRow(n, favs, true));
            }
            else
            {
                foreach (var n in Current())
                {
                    if (n.Kind == NodeKind.Section)
                    {
                        if (stack.Count > 0) continue;
                        rows.Add(new PmRow { Title = n.Title, Header = true });
                        foreach (var c in n.Children) rows.Add(ActionRow(c, favs, false));
                    }
                    else if (n.Kind == NodeKind.Folder) rows.Add(new PmRow { Title = n.Title, Folder = true, Node = n, Glyph = GlyphForFolder(n.Title) });
                    else rows.Add(ActionRow(n, favs, false));
                }
            }
            crumbsOn = stack.Count > 0 && text.Length == 0;
            crumbs.Visible = crumbsOn;
            crumbs.Trail = stack.Count == 0 ? "" : "Menu \u203A " + string.Join(" \u203A ", stack.Select(s => s.Title).ToArray());
            crumbs.Parts = stack.Select(x => x.Title).ToList();
            list.ShowDigits = text.Length == 0;
            crumbs.Invalidate();
            list.SetRows(rows, select);
            empty.Query = text;
            emptyOn = text.Length > 0 && rows.Count == 0;
            empty.Visible = emptyOn;
            list.Visible = !emptyOn;
            Relayout();
            UpdateDetails();
            footer.Folder = SelectedFolder();
            footer.Invalidate();
        }

        static PmRow ActionRow(MenuNode n, HashSet<string> favs, bool withPath)
        {
            return new PmRow
            {
                Title = n.Title, Node = n, Favorite = favs.Contains(n.Spec.Id), Risk = n.Spec.Risk, Admin = n.Spec.Admin,
                Secondary = withPath ? n.Path : "", Glyph = GlyphFor(n.Spec), Extended = n.Spec.Extended
            };
        }

        static readonly Dictionary<string, string> FolderGlyphs = new Dictionary<string, string>
        {
            { "Copy", "\uE8C8" }, { "Paste & New", "\uE710" }, { "Search", "\uE721" }, { "Open", "\uE8E5" }, { "Rename", "\uE8AC" },
            { "Organize", "\uE8FD" }, { "Archive", "\uE7B8" }, { "Image", "\uEB9F" }, { "Media", "\uE714" }, { "Inspect", "\uE946" },
            { "Compare", "\uE8A1" }, { "Dev", "\uE943" }, { "Git", "\uE8F1" }, { "Links", "\uE71B" }, { "Clean up", "\uE74D" },
            { "Fix", "\uE90F" }, { "Clipboard", "\uE77F" }, { "System", "\uE770" }, { "Recipes", "\uE8D4" }, { "My actions", "\uE734" },
            { "Library", "\uE8F1" }
        };

        static string GlyphForFolder(string top)
        {
            string g;
            return FolderGlyphs.TryGetValue(top, out g) ? g : "\uE8B7";
        }

        static string GlyphFor(ActionSpec s)
        {
            if (s.Icon.Length > 0 && s.Icon.Length <= 2) return s.Icon;
            string[] parts = s.MenuParts;
            return GlyphForFolder(parts.Length == 0 ? (s.Source == SpecSource.User ? "My actions" : "") : parts[0]);
        }

        void Relayout()
        {
            int width = UI.S(420);
            int detailsWidth = showDetails ? UI.S(280) : 0;
            int headerH = UI.S(44), searchH = inputPanel != null ? 0 : UI.S(48), footerH = UI.S(32);
            int crumbH = crumbsOn && inputPanel == null ? UI.S(28) : 0;
            int minList = Tok.RowHeight * 4, maxList = Tok.RowHeight * 12;
            int content = inputPanel != null ? inputPanel.ContentHeight : emptyOn ? minList : list.ContentHeight;
            int listH = Math.Max(minList, Math.Min(maxList, content));
            int total = headerH + searchH + crumbH + listH + footerH;
            ClientSize = new Size(width + detailsWidth, total + 2);
            int x = 1, w = width - 2;
            header.SetBounds(x, 1, w, headerH);
            search.SetBounds(x + Tok.S2, 1 + headerH + Tok.S2 / 2 + UI.S(4), w - Tok.S2 * 2, Tok.InputHeight);
            int y = 1 + headerH + searchH;
            crumbs.SetBounds(x, y, w, crumbH);
            y += crumbH;
            list.SetBounds(x, y, w, listH);
            empty.SetBounds(x, y, w, listH);
            if (inputPanel != null) inputPanel.SetBounds(x, y, w, listH);
            footer.SetBounds(x, y + listH, w, footerH);
            details.Visible = showDetails;
            details.SetBounds(width, 1, detailsWidth - 1, total);
            list.EnsureVisible();
        }

        void UpdateDetails()
        {
            var r = list.SelectedRow;
            details.Spec = r == null || r.Node == null ? null : r.Node.Spec;
            details.FolderTitle = r != null && r.Folder ? r.Title : null;
            details.Invalidate();
        }

        void ActivateSelected()
        {
            var r = list.SelectedRow;
            if (r == null || r.Node == null) return;
            if (r.Folder) { Drill(r.Node); return; }
            RunSpec(r.Node.Spec);
        }

        void Drill(MenuNode folder)
        {
            stackSelection.Add(list.Selected);
            stack.Add(folder);
            search.Input.Text = "";
            Rebuild(0);
        }

        void Back()
        {
            if (stack.Count == 0) return;
            stack.RemoveAt(stack.Count - 1);
            int sel = stackSelection[stackSelection.Count - 1];
            stackSelection.RemoveAt(stackSelection.Count - 1);
            Rebuild(sel);
        }

        void RunSpec(ActionSpec s)
        {
            if (s.Inputs.Count > 0) { ShowInputs(s); return; }
            RunWith(s, null);
        }

        void RunWith(ActionSpec s, Dictionary<string, string> inputs)
        {
            if (options.NoteUsed != null) options.NoteUsed(s.Id);
            var spec = s;
            var targets = paths.Length == 0 ? new[] { FallbackFolder() } : paths;
            ToRun = () => Executor.Run(spec, targets, inputs);
            CloseMenu();
        }

        InputPanel inputPanel;

        public void ShowInputsFor(ActionSpec s) { ShowInputs(s); }

        void ShowInputs(ActionSpec s)
        {
            HideInputs();
            inputPanel = new InputPanel(s, UI.S(420) - 2 - Tok.S3 * 2);
            Controls.Add(inputPanel);
            list.Visible = false;
            empty.Visible = false;
            crumbs.Visible = false;
            search.Visible = false;
            footer.InputMode = true;
            Relayout();
            inputPanel.BringToFront();
            UI.Walk(inputPanel);
            footer.Invalidate();
            inputPanel.FocusFirst();
        }

        void HideInputs()
        {
            if (inputPanel == null) return;
            Controls.Remove(inputPanel);
            inputPanel.Dispose();
            inputPanel = null;
            footer.InputMode = false;
            list.Visible = !emptyOn;
            empty.Visible = emptyOn;
            crumbs.Visible = crumbsOn;
            search.Visible = true;
            Relayout();
            search.Input.Focus();
            footer.Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (inputPanel != null)
            {
                if (keyData == Keys.Enter)
                {
                    string error;
                    var values = inputPanel.Collect(out error);
                    if (values == null) { inputPanel.ShowError(error); return true; }
                    var spec = inputPanel.Spec;
                    RunWith(spec, values);
                    return true;
                }
                if (keyData == Keys.Escape) { HideInputs(); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        string FallbackFolder()
        {
            return string.IsNullOrEmpty(options.Summary) ? Environment.CurrentDirectory : options.Summary;
        }

        void RunTypedText()
        {
            string text = search.Input.Text.Trim();
            if (text.Length == 0) return;
            string dir = paths.Length > 0 ? paths[0] : Environment.CurrentDirectory;
            ToRun = () => Program.Msg(Executor.RunText(text, System.IO.Directory.Exists(dir) ? dir : null));
            CloseMenu();
        }

        void CloseMenu()
        {
            if (finished) return;
            finished = true;
            Close();
        }

        void ToggleFavorite()
        {
            var r = list.SelectedRow;
            if (r == null || r.Node == null || r.Folder) return;
            string id = r.Node.Spec.Id;
            bool now;
            if (options.ToggleFavorite != null) now = options.ToggleFavorite(id);
            else
            {
                var l = query.Favorites as List<string>;
                if (l == null) { l = new List<string>(query.Favorites); query.Favorites = l; }
                if (l.Contains(id)) { l.Remove(id); now = false; } else { l.Add(id); now = true; }
            }
            if (now && !query.Favorites.Contains(id)) { var l2 = new List<string>(query.Favorites); l2.Add(id); query.Favorites = l2; }
            if (!now) { var l3 = query.Favorites.Where(f => !string.Equals(f, id, StringComparison.OrdinalIgnoreCase)).ToList(); query.Favorites = l3; }
            int keep = list.Selected;
            root = MenuTree.Build(query);
            var drill = stack.Select(s => s.Title).ToList();
            stack.Clear();
            foreach (var t in drill)
            {
                var f = Current().FirstOrDefault(n => n.Kind == NodeKind.Folder && n.Title == t);
                if (f == null) break;
                stack.Add(f);
            }
            Rebuild(Math.Min(keep, Math.Max(0, list.Rows.Count - 1)));
        }

        void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            var input = search.Input;
            if (e.KeyCode == Keys.Down) { list.Move(1); e.SuppressKeyPress = true; e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { list.Move(-1); e.SuppressKeyPress = true; e.Handled = true; }
            else if (e.KeyCode == Keys.PageDown) { list.Move(8); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.PageUp) { list.Move(-8); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Home && input.TextLength == 0) { list.MoveTo(false); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.End && input.TextLength == 0) { list.MoveTo(true); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { ActivateSelected(); e.SuppressKeyPress = true; e.Handled = true; }
            else if (e.KeyCode == Keys.Right && (input.TextLength == 0 || input.SelectionStart >= input.TextLength) && SelectedFolder())
            { ActivateSelected(); e.SuppressKeyPress = true; e.Handled = true; }
            else if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.Back) && input.TextLength == 0 && stack.Count > 0) { Back(); e.SuppressKeyPress = true; e.Handled = true; }
            else if (input.TextLength == 0 && !e.Control && !e.Alt && !e.Shift && DigitOf(e.KeyCode) > 0)
            {
                var visible = list.VisibleSelectable();
                int n = DigitOf(e.KeyCode);
                e.SuppressKeyPress = true;
                e.Handled = true;
                if (n <= visible.Count) { list.Select(visible[n - 1]); ActivateSelected(); }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (input.TextLength > 0) input.Text = "";
                else if (stack.Count > 0) Back();
                else if (showDetails) { showDetails = false; Relayout(); }
                else CloseMenu();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.D && e.Control) { ToggleFavorite(); e.SuppressKeyPress = true; e.Handled = true; }
            else if (e.KeyCode == Keys.F1) { ToggleDetails(); e.SuppressKeyPress = true; e.Handled = true; }
            else if (e.KeyCode == Keys.OemQuestion && e.Shift && input.TextLength == 0) { ToggleDetails(); e.SuppressKeyPress = true; e.Handled = true; }
        }

        static int DigitOf(Keys k)
        {
            if (k >= Keys.D1 && k <= Keys.D9) return k - Keys.D0;
            if (k >= Keys.NumPad1 && k <= Keys.NumPad9) return k - Keys.NumPad0;
            return 0;
        }

        void ShowRowMenu(Point at)
        {
            var r = list.SelectedRow;
            if (r == null || r.Node == null) return;
            var menu = new ContextMenuStrip();
            Fx.ThemeMenu(menu);
            if (r.Folder)
            {
                var node = r.Node;
                menu.Items.Add("Open", null, (s, e) => Drill(node));
            }
            else
            {
                var spec = r.Node.Spec;
                menu.Items.Add(spec.Inputs.Count > 0 ? "Run with options..." : "Run", null, (s, e) => RunSpec(spec));
                string code = spec.Legacy != null ? (spec.Legacy.Ps ?? "") : spec.Command;
                var copy = menu.Items.Add("Copy PowerShell equivalent", null, (s, e) => Clip.Set(code));
                copy.Enabled = code.Length > 0;
                menu.Items.Add(new ToolStripSeparator());
                bool fav = r.Favorite;
                menu.Items.Add(fav ? "Remove from Explorer menu" : "Add to Explorer menu", null, (s, e) => ToggleFavorite());
                menu.Items.Add("Hide from menu", null, (s, e) => HideSpec(spec));
                menu.Items.Add(new ToolStripSeparator());
                if (spec.Source == SpecSource.User && spec.SourceFile.Length > 0)
                    menu.Items.Add("Edit", null, (s, e) => { try { Process.Start(spec.SourceFile); } catch { } });
                else
                {
                    var dup = menu.Items.Add("Duplicate as my action", null, (s, e) => DuplicateSpec(spec));
                    dup.Enabled = code.Length > 0;
                }
            }
            menu.Show(list, at);
        }

        void HideSpec(ActionSpec spec)
        {
            if (!options.Harness) Settings.SetHidden(spec.Id, true);
            spec.UserHidden = true;
            root = MenuTree.Build(query);
            Rebuild(0);
        }

        void DuplicateSpec(ActionSpec spec)
        {
            try
            {
                string code = spec.Legacy != null ? (spec.Legacy.Ps ?? "") : spec.Command;
                string dir = System.IO.Path.Combine(Journal.Dir, "actions", "My actions");
                System.IO.Directory.CreateDirectory(dir);
                string name = new string(spec.Title.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').ToArray()).Trim();
                if (name.Length == 0) name = spec.Id;
                string file = System.IO.Path.Combine(dir, name + ".ps1");
                string header = "<#\n@title " + spec.Title + " (copy)\n@description " + spec.Description + "\n@when " + string.Join(", ", SpecRules.ContextNames(spec.When).ToArray()) + "\n@output " + spec.Output.ToString().ToLowerInvariant() + "\n@risk " + spec.Risk.ToString().ToLowerInvariant() + "\n#>\n";
                System.IO.File.WriteAllText(file, header + code + "\n", new System.Text.UTF8Encoding(true));
                ActionCatalog.Reset();
                Runner.Notify(null, "Saved " + name + ".ps1 in your actions folder", false);
                try { Process.Start("explorer.exe", "/select,\"" + file + "\""); } catch { }
            }
            catch (Exception ex) { Program.Fail("Could not save the copy: " + ex.Message); }
        }

        void ToggleDetails()
        {
            showDetails = !showDetails;
            Relayout();
            UpdateDetails();
        }

        public void OpenApp()
        {
            string target = paths.Length > 0 ? paths[0] : Environment.CurrentDirectory;
            ToRun = () =>
            {
                try { Process.Start(Application.ExecutablePath, "open \"" + target + "\""); }
                catch { }
            };
            CloseMenu();
        }

        class InputPanel : Panel
        {
            public readonly ActionSpec Spec;
            readonly List<KeyValuePair<InputSpec, Control>> fields = new List<KeyValuePair<InputSpec, Control>>();
            readonly Label error = new Label();

            public int ContentHeight;

            public InputPanel(ActionSpec spec, int fieldWidth)
            {
                Spec = spec;
                BackColor = Tok.SurfaceAlt;
                int y = Tok.S3;
                var title = new Label { Text = spec.Title, Font = Tok.Semibold, AutoSize = false, Left = Tok.S3, Top = y, Width = fieldWidth, Height = UI.S(24) };
                Controls.Add(title);
                y += UI.S(30);
                foreach (var input in spec.Inputs)
                {
                    var lbl = new Label { Text = input.Label, Font = Tok.Caption, AutoSize = false, Left = Tok.S3, Top = y, Width = fieldWidth, Height = UI.S(20) };
                    Controls.Add(lbl);
                    y += UI.S(22);
                    Control field;
                    if (input.Type == "bool")
                    {
                        var t = new FToggle { Text = input.Label, Left = Tok.S3, Top = y, Width = fieldWidth, Height = UI.S(28) };
                        t.Checked = string.Equals(input.Default, "true", StringComparison.OrdinalIgnoreCase);
                        lbl.Visible = false;
                        y -= UI.S(22);
                        field = t;
                        field.Top = y;
                        y += UI.S(36);
                    }
                    else if (input.Type == "pick")
                    {
                        var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = Tok.S3, Top = y, Width = fieldWidth, Font = Tok.Body };
                        foreach (var o in input.Options) cb.Items.Add(o);
                        int idx = input.Options.IndexOf(input.Default);
                        cb.SelectedIndex = idx >= 0 ? idx : 0;
                        field = cb;
                        y += UI.S(38);
                    }
                    else
                    {
                        var box = new FSearchBox("") { ShowGlyph = false, Left = Tok.S3, Top = y, Width = fieldWidth, Height = Tok.InputHeight };
                        box.Input.Text = input.Default;
                        field = box;
                        y += UI.S(44);
                    }
                    Controls.Add(field);
                    fields.Add(new KeyValuePair<InputSpec, Control>(input, field));
                }
                error.AutoSize = false;
                error.Left = Tok.S3; error.Top = y; error.Width = fieldWidth; error.Height = UI.S(36);
                ContentHeight = y + UI.S(40);
                error.Font = Tok.Caption;
                error.ForeColor = Tok.RiskDestructive;
                Controls.Add(error);
            }

            public void FocusFirst()
            {
                foreach (var f in fields)
                {
                    var box = f.Value as FSearchBox;
                    if (box != null) { box.Input.Focus(); box.Input.SelectAll(); return; }
                    f.Value.Focus();
                    return;
                }
            }

            public void ShowError(string text)
            {
                error.Text = text;
            }

            public Dictionary<string, string> Collect(out string problem)
            {
                problem = null;
                var values = new Dictionary<string, string>();
                foreach (var f in fields)
                {
                    string v;
                    var box = f.Value as FSearchBox;
                    var cb = f.Value as ComboBox;
                    var tg = f.Value as FToggle;
                    if (box != null) v = box.Input.Text;
                    else if (cb != null) v = cb.SelectedItem == null ? "" : cb.SelectedItem.ToString();
                    else v = tg != null && tg.Checked ? "true" : "false";
                    if (f.Key.Type == "number")
                    {
                        double d;
                        if (!double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) { problem = f.Key.Label + " must be a number."; return null; }
                    }
                    if ((f.Key.Type == "text" || f.Key.Type == "folder" || f.Key.Type == "file") && v.Trim().Length == 0) { problem = f.Key.Label + " is required."; return null; }
                    values[f.Key.Name] = v;
                }
                return values;
            }
        }

        class HeaderBar : Control
        {
            readonly PowerMenu owner;
            Rectangle openRect;

            public HeaderBar(PowerMenu o)
            {
                owner = o;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, false);
                Cursor = Cursors.Default;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (openRect.Contains(e.Location)) owner.OpenApp();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Tok.SurfaceAlt);
                using (var f = UI.Icons(10f))
                    TextRenderer.DrawText(g, owner.SummaryGlyph, f, new Rectangle(Tok.S3, 0, UI.S(20), Height), Tok.TextSecondary, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                openRect = new Rectangle(Width - Tok.S3 - UI.S(24), (Height - UI.S(24)) / 2, UI.S(24), UI.S(24));
                FGfx.Text(g, owner.SummaryText, Tok.Semibold, new Rectangle(Tok.S3 + UI.S(26), 0, Width - Tok.S3 * 2 - UI.S(26) - UI.S(32), Height), Tok.Text, FGfx.Line);
                using (var f = UI.Icons(10f))
                    TextRenderer.DrawText(g, "\uE8A7", f, openRect, Tok.TextSecondary, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                using (var p = new Pen(Tok.Stroke)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            }
        }

        class FooterBar : Control
        {
            public bool Folder, InputMode;

            public FooterBar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, false);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Tok.SurfaceAlt);
                using (var p = new Pen(Tok.Stroke)) g.DrawLine(p, 0, 0, Width, 0);
                var hints = InputMode
                    ? new List<string[]> { new[] { "\u21B5", "Run" }, new[] { "Esc", "Back" } }
                    : new List<string[]>
                    {
                        new[] { "\u21B5", Folder ? "Open" : "Run" }, new[] { "\u2192", "Open" }, new[] { "\u2190", "Back" },
                        new[] { "Ctrl D", "\u2605" }, new[] { "?", "Details" }, new[] { "1-9", "Run" }, new[] { "Esc", "Close" }
                    };
                int gap = Tok.S3;
                int x = Tok.S3, used = x;
                var widths = hints.Select(h => FKbd.Measure(g, h[0], h[1])).ToList();
                int fit = 0;
                for (int i = 0; i < hints.Count; i++)
                {
                    int need = used + widths[i] + (i > 0 ? gap : 0);
                    if (need > Width - Tok.S3) break;
                    used = need;
                    fit++;
                }
                for (int i = 0; i < fit; i++)
                {
                    x += i > 0 ? gap : 0;
                    x += FKbd.Draw(g, x, 0, Height, hints[i][0], hints[i][1]);
                }
            }
        }

        class EmptyState : Control
        {
            public string Query = "";
            Rectangle link;
            public event System.Action RunRequested;

            public EmptyState()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, false);
                Visible = false;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (link.Contains(e.Location) && RunRequested != null) RunRequested();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                Cursor = link.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Tok.SurfaceAlt);
                string shown = Query.Length > 28 ? Query.Substring(0, 27) + "\u2026" : Query;
                var top = new Rectangle(Tok.S3, Height / 2 - UI.S(30), Width - Tok.S3 * 2, UI.S(24));
                FGfx.Text(g, "No actions match \"" + shown + "\"", Tok.Body, top, Tok.Text, FGfx.Line | TextFormatFlags.HorizontalCenter);
                string label = "Run \"" + shown + "\" in console";
                var size = TextRenderer.MeasureText(g, label, Tok.Caption, new Size(int.MaxValue, 0), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                link = new Rectangle((Width - size.Width) / 2 - Tok.S2, top.Bottom + Tok.S1, size.Width + Tok.S2 * 2, UI.S(28));
                FGfx.Text(g, label, Tok.Caption, link, Tok.Accent, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        class DetailsPane : Control
        {
            public ActionSpec Spec;
            public string FolderTitle;

            public DetailsPane()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, false);
                Visible = false;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Tok.Surface);
                using (var p = new Pen(Tok.Stroke)) g.DrawLine(p, 0, 0, 0, Height);
                int pad = Tok.S3;
                int w = Width - pad * 2;
                int y = pad;
                if (Spec == null)
                {
                    string msg = FolderTitle != null ? FolderTitle + "\nA group of actions. Press Right or Enter to open it." : "Select an action to see what it does.";
                    FGfx.Text(g, msg, Tok.Caption, new Rectangle(pad, y, w, UI.S(80)), Tok.TextSecondary, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
                    return;
                }
                var title = TextRenderer.MeasureText(g, Spec.Title, Tok.Semibold, new Size(w, 0), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
                FGfx.Text(g, Spec.Title, Tok.Semibold, new Rectangle(pad, y, w, title.Height), Tok.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
                y += title.Height + Tok.S1;
                FGfx.Text(g, Spec.Menu.Length == 0 ? "Top level" : Spec.Menu.Replace("/", " \u203A "), Tok.Caption, new Rectangle(pad, y, w, UI.S(18)), Tok.TextSecondary, FGfx.Line);
                y += UI.S(26);
                var desc = TextRenderer.MeasureText(g, Spec.Description, Tok.Body, new Size(w, 0), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
                int dh = Math.Min(desc.Height, UI.S(110));
                FGfx.Text(g, Spec.Description, Tok.Body, new Rectangle(pad, y, w, dh), Tok.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
                y += dh + Tok.S2;
                string risk = Spec.Risk == Risk.Safe ? "Safe: does not change anything" : Spec.Risk == Risk.Modifies ? "Changes files" + (Spec.Undo == UndoMode.Journal ? " (can be undone)" : "") : "Destructive: cannot be undone";
                if (Spec.Admin) risk += ". Needs administrator rights";
                Color rc = Spec.Risk == Risk.Safe ? Tok.TextSecondary : Spec.Risk == Risk.Modifies ? Tok.RiskModifies : Tok.RiskDestructive;
                var rr = new Rectangle(pad, y, w, UI.S(36));
                FGfx.Text(g, risk, Tok.Caption, rr, rc, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
                y += UI.S(40);
                string code = Spec.Legacy != null ? (Spec.Legacy.Ps ?? "") : Spec.Command;
                if (code.Length > 0)
                {
                    int boxH = Math.Max(UI.S(40), Height - y - pad);
                    var box = new Rectangle(pad, y, w, boxH);
                    FGfx.FillRound(g, box, Tok.RadiusSmall, UI.Dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243));
                    FGfx.StrokeRound(g, box, Tok.RadiusSmall, Tok.Stroke);
                    var inner = new Rectangle(box.X + Tok.S2, box.Y + Tok.S2, box.Width - Tok.S2 * 2, box.Height - Tok.S2 * 2);
                    FGfx.Text(g, code, Tok.Mono, inner, Tok.Text, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }
}
