using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class ActionDetail : Control
    {
        public ActionSpec Spec;
        public string FolderTitle;

        public ActionDetail()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AccessibleName = "Action details";
        }

        public static string ShowsFor(ActionSpec s)
        {
            var names = SpecRules.ContextNames(s.When);
            if (names.Count == 0) return "Anywhere";
            return string.Join(", ", names.Select(n => n == "background" ? "empty space" : n).ToArray());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Tok.Surface);
            int w = Math.Max(20, Width);
            int y = 0;
            const TextFormatFlags wrap = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
            if (Spec == null)
            {
                string msg = FolderTitle != null ? FolderTitle + "\nA group of actions. Press Enter or Right to open it." : "Select an action to see what it does.";
                FGfx.Text(g, msg, Tok.Body, new Rectangle(0, 0, w, UI.S(80)), Tok.TextSecondary, wrap);
                return;
            }
            var t = TextRenderer.MeasureText(g, Spec.Title, UI.Head, new Size(w, 0), wrap);
            FGfx.Text(g, Spec.Title, UI.Head, new Rectangle(0, y, w, t.Height), Tok.Text, wrap);
            y += t.Height + Tok.S1;
            string path = Spec.Menu.Length == 0 ? "Top level" : Spec.Menu.Replace("/", " \u203A ");
            FGfx.Text(g, path + "   -   " + (Spec.Source == SpecSource.User ? "My action" : "Built in"), Tok.Caption, new Rectangle(0, y, w, UI.S(18)), Tok.TextSecondary, FGfx.Line);
            y += UI.S(26);
            var d = TextRenderer.MeasureText(g, Spec.Description, Tok.Body, new Size(w, 0), wrap);
            int dh = Math.Min(d.Height, UI.S(96));
            FGfx.Text(g, Spec.Description, Tok.Body, new Rectangle(0, y, w, dh), Tok.Text, wrap | TextFormatFlags.EndEllipsis);
            y += dh + Tok.S2;
            string risk = Spec.Risk == Risk.Safe ? "Safe: does not change anything" : Spec.Risk == Risk.Modifies ? "Changes files" + (Spec.Undo == UndoMode.Journal ? " (can be undone)" : "") : "Destructive: cannot be undone";
            if (Spec.Admin) risk += ". Needs administrator rights";
            Color rc = Spec.Risk == Risk.Safe ? Tok.TextSecondary : Spec.Risk == Risk.Modifies ? Tok.RiskModifies : Tok.RiskDestructive;
            FGfx.Text(g, risk, Tok.Caption, new Rectangle(0, y, w, UI.S(36)), rc, wrap);
            y += UI.S(24);
            FGfx.Text(g, "Shows for: " + ShowsFor(Spec), Tok.Caption, new Rectangle(0, y, w, UI.S(36)), Tok.TextSecondary, wrap);
            y += UI.S(32);
            string code = Spec.Legacy != null ? (Spec.Legacy.Ps ?? "") : Spec.Command;
            if (code.Length > 0)
            {
                FGfx.Text(g, "PowerShell equivalent", UI.SmallBold, new Rectangle(0, y, w, UI.S(20)), Tok.TextSecondary, FGfx.Line);
                y += UI.S(24);
                int boxH = Math.Max(UI.S(44), Height - y);
                var box = new Rectangle(0, y, w - 1, boxH - 1);
                FGfx.FillRound(g, box, Tok.RadiusSmall, Tok.Window);
                FGfx.StrokeRound(g, box, Tok.RadiusSmall, Tok.Stroke);
                FGfx.Text(g, code, Tok.Mono, new Rectangle(box.X + Tok.S2, box.Y + Tok.S2, box.Width - Tok.S2 * 2, box.Height - Tok.S2 * 2), Tok.Text, wrap | TextFormatFlags.EndEllipsis);
            }
        }
    }

    class ActionsPage : Panel
    {
        readonly MainForm main;
        readonly Label title = new Label { Text = "Actions", AutoSize = false, AccessibleName = "Actions title" };
        readonly FSearchBox search = new FSearchBox("Search actions");
        readonly FBreadcrumb crumbs = new FBreadcrumb { Visible = false };
        readonly FTreeList list = new FTreeList { ClickActivates = false, AccessibleName = "Actions list" };
        readonly V4Card card = new V4Card { ShowTitle = false, AccessibleName = "Action details card" };
        readonly ActionDetail detail = new ActionDetail();
        readonly FButton run = new FButton { Text = "Run", Kind = FButton.Look.Accent, AccessibleName = "Run" };
        readonly FButton star = new FButton { Text = "Add to menu", AccessibleName = "Favorite" };
        readonly FButton console = new FButton { Text = "Run console", AccessibleName = "Open in Run console" };
        readonly FButton copy = new FButton { Text = "Copy", AccessibleName = "Copy command" };
        readonly FButton duplicate = new FButton { Text = "Duplicate", AccessibleName = "Duplicate as my action" };
        readonly FButton hide = new FButton { Text = "Hide", AccessibleName = "Hide" };
        readonly FInfoBar info = new FInfoBar { Visible = false };
        readonly List<MenuNode> stack = new List<MenuNode>();
        readonly List<int> stackSelection = new List<int>();
        List<MenuNode> root = new List<MenuNode>();
        MenuQuery query;
        string builtFor = "";

        public ActionsPage(MainForm owner)
        {
            main = owner;
            Dock = DockStyle.Fill;
            AccessibleName = "Actions";
            title.Font = UI.Title;
            Controls.Add(title);
            Controls.Add(search);
            Controls.Add(crumbs);
            Controls.Add(list);
            Controls.Add(card);
            Controls.Add(info);
            card.Controls.Add(detail);
            foreach (var b in new[] { run, star, console, copy, duplicate, hide }) card.Controls.Add(b);
            search.AccessibleName = "Search actions";
            search.Input.AccessibleName = "Search actions";
            search.Input.TextChanged += (s, e) => Rebuild(0);
            search.Input.KeyDown += OnSearchKey;
            list.SelectionChanged += (s, e) => UpdateDetail();
            list.Activated += (s, e) => Activate();
            list.RowRightClicked += (s, e) => { };
            crumbs.LevelClicked += level => { while (stack.Count > level) stack.RemoveAt(stack.Count - 1); stackSelection.Clear(); Rebuild(0); };
            run.Click += (s, e) => RunSelected();
            star.Click += (s, e) => ToggleFavorite();
            console.Click += (s, e) => { var sp = Current(); if (sp != null) ActionLauncher.OpenConsole(main, main.Folder, Expanded(sp)); };
            copy.Click += (s, e) => { var sp = Current(); if (sp != null) Clip.Set(Code(sp)); };
            duplicate.Click += (s, e) => DuplicateOrEdit();
            hide.Click += (s, e) => HideSelected();
            Resize += (s, e) => DoLayout();
        }

        public FSearchBox SearchBox { get { return search; } }

        public int RowCount { get { return list.Rows.Count; } }

        public string SelectedTitle { get { var r = list.SelectedRow; return r == null ? "" : r.Title; } }

        public void FocusSearch() { search.Input.Focus(); search.Input.SelectAll(); }

        public void SetQuery(string text) { search.Input.Text = text; }

        public void SelectTitle(string text)
        {
            for (int i = 0; i < list.Rows.Count; i++)
                if (list.Rows[i].Title == text) { list.Select(i); return; }
        }

        public void Drill(string folderTitle)
        {
            var f = CurrentNodes().FirstOrDefault(n => n.Kind == NodeKind.Folder && n.Title == folderTitle);
            if (f != null) DrillInto(f);
        }

        public void Reload()
        {
            query = PowerMenuLauncher.QueryFor(ContextKind.Background, new string[0]);
            query.BackgroundFolder = main.Folder;
            query.Shift = true;
            root = MenuTree.Build(query);
            stack.Clear();
            stackSelection.Clear();
            Rebuild(0);
        }

        public void EnsureBuilt()
        {
            if (builtFor != main.Folder || root.Count == 0) { builtFor = main.Folder; Reload(); }
        }

        List<MenuNode> CurrentNodes() { return stack.Count == 0 ? root : stack[stack.Count - 1].Children; }

        bool Searching { get { return search.Input.Text.Trim().Length > 0; } }

        ActionSpec Current()
        {
            var r = list.SelectedRow;
            return r == null || r.Node == null || r.Folder ? null : r.Node.Spec;
        }

        static string Code(ActionSpec s) { return s.Legacy != null ? (s.Legacy.Ps ?? "") : s.Command; }

        string Expanded(ActionSpec s)
        {
            try { return Placeholders.Expand(Code(s), s.Shell == ActionShell.Builtin ? ActionShell.PowerShell : s.Shell, Executor.ContextFor(new[] { main.Folder })); }
            catch (PlaceholderException) { return Code(s); }
        }

        void Rebuild(int select)
        {
            if (query == null) return;
            var rows = new List<PmRow>();
            var favs = new HashSet<string>(query.Favorites, StringComparer.OrdinalIgnoreCase);
            string text = search.Input.Text.Trim();
            if (text.Length > 0)
            {
                foreach (var n in MenuTree.Search(query, text)) rows.Add(ActionRow(n, favs, true));
            }
            else
            {
                foreach (var n in CurrentNodes())
                {
                    if (n.Kind == NodeKind.Section)
                    {
                        if (stack.Count > 0) continue;
                        rows.Add(new PmRow { Title = n.Title, Header = true });
                        foreach (var c in n.Children) rows.Add(ActionRow(c, favs, false));
                    }
                    else if (n.Kind == NodeKind.Folder) rows.Add(new PmRow { Title = n.Title, Folder = true, Node = n, Glyph = PowerMenu.GlyphForFolder(n.Title) });
                    else rows.Add(ActionRow(n, favs, false));
                }
            }
            crumbs.Visible = stack.Count > 0 && text.Length == 0;
            crumbs.Trail = stack.Count == 0 ? "" : "Actions \u203A " + string.Join(" \u203A ", stack.Select(s => s.Title).ToArray());
            crumbs.Parts = stack.Select(x => x.Title).ToList();
            crumbs.Invalidate();
            list.SetRows(rows, select);
            DoLayout();
            UpdateDetail();
        }

        static PmRow ActionRow(MenuNode n, HashSet<string> favs, bool withPath)
        {
            return new PmRow { Title = n.Title, Node = n, Favorite = favs.Contains(n.Spec.Id), Risk = n.Spec.Risk, Admin = n.Spec.Admin, Secondary = withPath ? n.Path : "", Glyph = PowerMenu.GlyphFor(n.Spec), Extended = n.Spec.Extended };
        }

        void UpdateDetail()
        {
            var r = list.SelectedRow;
            var s = Current();
            detail.Spec = s;
            detail.FolderTitle = r != null && r.Folder ? r.Title : null;
            detail.AccessibleDescription = s == null ? "" : s.Id;
            detail.Invalidate();
            bool has = s != null;
            run.Enabled = has || (r != null && r.Folder);
            run.Text = r != null && r.Folder ? "Open" : has && s.Inputs.Count > 0 ? "Run with options..." : "Run";
            star.Enabled = has;
            console.Enabled = has && Code(s).Length > 0;
            copy.Enabled = has && Code(s).Length > 0;
            duplicate.Enabled = has && Code(s).Length > 0;
            hide.Enabled = has;
            star.Text = has && Settings.IsFavorite(s.Id) ? "Remove from menu" : "Add to menu";
            duplicate.Text = has && s.Source == SpecSource.User && s.SourceFile.Length > 0 ? "Edit file" : "Duplicate";
            DoLayout();
        }

        void Activate()
        {
            var r = list.SelectedRow;
            if (r == null || r.Node == null) return;
            if (r.Folder) DrillInto(r.Node);
            else RunSelected();
        }

        void DrillInto(MenuNode folder)
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
            int sel = stackSelection.Count > 0 ? stackSelection[stackSelection.Count - 1] : 0;
            if (stackSelection.Count > 0) stackSelection.RemoveAt(stackSelection.Count - 1);
            Rebuild(sel);
        }

        void RunSelected()
        {
            var r = list.SelectedRow;
            if (r != null && r.Folder && r.Node != null) { DrillInto(r.Node); return; }
            var s = Current();
            if (s == null) return;
            ActionLauncher.Run(s, main.Folder, main, main.SetStatus);
            main.AfterRun();
        }

        void ToggleFavorite()
        {
            var s = Current();
            if (s == null) return;
            Settings.ToggleFavorite(s.Id);
            Shell.RefreshIfInstalled();
            int keep = list.Selected;
            Reload();
            list.Select(Math.Min(keep, Math.Max(0, list.Rows.Count - 1)));
            main.FavoritesChanged();
        }

        void HideSelected()
        {
            var s = Current();
            if (s == null) return;
            Settings.SetHidden(s.Id, true);
            s.UserHidden = true;
            Reload();
            main.SetStatus(s.Title + " is hidden. Show it again in Settings.");
        }

        void DuplicateOrEdit()
        {
            var s = Current();
            if (s == null) return;
            if (s.Source == SpecSource.User && s.SourceFile.Length > 0)
            {
                try { Process.Start(s.SourceFile); } catch (Exception ex) { main.SetStatus("Could not open the file: " + ex.Message); }
                return;
            }
            string raw = Code(s);
            var shellKind = s.Shell == ActionShell.Builtin ? ActionShell.PowerShell : s.Shell;
            string dir = Directory.Exists(main.Folder) ? main.Folder : Environment.CurrentDirectory;
            using (var f = new SaveActionForm(raw, shellKind, dir, "background"))
            {
                f.SetTitle(s.Title + " (copy)");
                f.ShowDialog(main);
            }
            ActionCatalog.Reset();
            Reload();
        }

        void OnSearchKey(object sender, KeyEventArgs e)
        {
            var input = search.Input;
            if (e.KeyCode == Keys.Down) { list.Move(1); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Up) { list.Move(-1); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.PageDown) { list.Move(8); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.PageUp) { list.Move(-8); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { Activate(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Right && (input.TextLength == 0 || input.SelectionStart >= input.TextLength) && list.SelectedRow != null && list.SelectedRow.Folder) { Activate(); e.SuppressKeyPress = true; }
            else if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.Back) && input.TextLength == 0 && stack.Count > 0) { Back(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.D && e.Control) { ToggleFavorite(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape && input.TextLength > 0) { input.Text = ""; e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape && stack.Count > 0) { Back(); e.SuppressKeyPress = true; }
        }

        public void DoLayout()
        {
            if (Width < 50 || card == null) return;
            int gap = Tok.S3;
            title.SetBounds(0, 0, Width, UI.S(40));
            int y = UI.S(48);
            int total = Width;
            int minPane = UI.S(280);
            int left = Math.Max(minPane, Math.Min(total - minPane - gap, total * 45 / 100));
            int right = total - left - gap;
            search.SetBounds(0, y, left, Tok.InputHeight);
            int top = y + Tok.InputHeight + Tok.S2;
            if (crumbs.Visible) { crumbs.SetBounds(0, top, left, UI.S(28)); top += UI.S(28) + Tok.S1; }
            int infoH = 0;
            if (info.Visible) { info.SetBounds(0, Height - UI.S(44), total, UI.S(44)); infoH = UI.S(44) + gap; }
            int bottom = Height - infoH;
            list.SetBounds(0, top, left, Math.Max(60, bottom - top));
            card.SetBounds(left + gap, y, right, Math.Max(100, bottom - y));
            var inner = card.Inner;
            int bh = Tok.ButtonHeight;
            var rowOne = new[] { run, star, hide };
            var rowTwo = new[] { console, copy, duplicate };
            int by2 = card.Height - Tok.S3 - bh;
            int by1 = by2 - Tok.S2 - bh;
            PlaceRow(rowOne, inner.X, by1, inner.Width);
            PlaceRow(rowTwo, inner.X, by2, inner.Width);
            detail.SetBounds(inner.X, Tok.S3, inner.Width, Math.Max(40, by1 - Tok.S3 - Tok.S2));
        }

        static void PlaceRow(FButton[] buttons, int x, int y, int room)
        {
            int gap = Tok.S2;
            var widths = buttons.Select(b => Math.Max(b.GetPreferredSize(Size.Empty).Width, UI.S(84))).ToArray();
            int avail = room - gap * (buttons.Length - 1);
            int sum = widths.Sum();
            for (int i = 0; i < buttons.Length; i++)
            {
                int bw = sum > avail ? widths[i] * avail / sum : widths[i];
                buttons[i].SetBounds(x, y, bw, Tok.ButtonHeight);
                x += bw + gap;
            }
        }
    }
}
