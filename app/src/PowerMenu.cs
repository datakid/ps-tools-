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
            list.SelectionChanged += (s, e) => { UpdateDetails(); footer.Folder = SelectedFolder(); footer.Invalidate(); };
            empty.RunRequested += RunTypedText;
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

            Shown += (s, e) =>
            {
                if (!options.Harness) { Activate(); try { SetForegroundWindow(Handle); } catch { } }
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
                return paths.Length + " items selected";
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

        void PlaceNearCursor()
        {
            var pt = Cursor.Position;
            var area = Screen.FromPoint(pt).WorkingArea;
            int x = Math.Min(Math.Max(area.Left, pt.X - Width / 3), Math.Max(area.Left, area.Right - Width));
            int y = Math.Min(Math.Max(area.Top, pt.Y), Math.Max(area.Top, area.Bottom - Height));
            Location = new Point(x, y);
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
                    else if (n.Kind == NodeKind.Folder) rows.Add(new PmRow { Title = n.Title, Folder = true, Node = n });
                    else rows.Add(ActionRow(n, favs, false));
                }
            }
            crumbs.Visible = stack.Count > 0 && text.Length == 0;
            crumbs.Trail = stack.Count == 0 ? "" : "Menu \u203A " + string.Join(" \u203A ", stack.Select(s => s.Title).ToArray());
            crumbs.Invalidate();
            list.SetRows(rows, select);
            empty.Query = text;
            empty.Visible = text.Length > 0 && rows.Count == 0;
            list.Visible = !empty.Visible;
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
                Secondary = withPath ? n.Path : ""
            };
        }

        void Relayout()
        {
            int width = UI.S(420);
            int detailsWidth = showDetails ? UI.S(280) : 0;
            int headerH = UI.S(44), searchH = UI.S(48), footerH = UI.S(32);
            int crumbH = crumbs.Visible ? UI.S(28) : 0;
            int minList = Tok.RowHeight * 4, maxList = Tok.RowHeight * 12;
            int content = empty.Visible ? minList : list.ContentHeight;
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
            if (options.NoteUsed != null) options.NoteUsed(s.Id);
            var spec = s;
            var targets = paths.Length == 0 ? new[] { FallbackFolder() } : paths;
            ToRun = () => Executor.Run(spec, targets);
            CloseMenu();
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
            else if (e.KeyCode == Keys.Left && input.TextLength == 0 && stack.Count > 0) { Back(); e.SuppressKeyPress = true; e.Handled = true; }
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

        class HeaderBar : Control
        {
            readonly PowerMenu owner;
            Rectangle openRect;

            public HeaderBar(PowerMenu o)
            {
                owner = o;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
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
            public bool Folder;

            public FooterBar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Tok.SurfaceAlt);
                using (var p = new Pen(Tok.Stroke)) g.DrawLine(p, 0, 0, Width, 0);
                var hints = new List<string[]>
                {
                    new[] { "\u21B5", Folder ? "Open" : "Run" }, new[] { "\u2192", "Open" }, new[] { "\u2190", "Back" },
                    new[] { "Ctrl D", "\u2605" }, new[] { "?", "Details" }
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
