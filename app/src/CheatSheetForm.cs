using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class CheatRow
    {
        public string Title = "", Mono = "", Id = "";
        public Risk Risk = Risk.Safe;
        public bool Admin;
        public object Source;
    }

    class FEntryList : Control
    {
        public readonly List<CheatRow> Rows = new List<CheatRow>();
        int selected = -1, hover = -1, scroll;
        static Font small;

        public event EventHandler SelectionChanged;
        public event EventHandler Activated;

        public static int RowH { get { return UI.S(52); } }

        static Font SmallMono
        {
            get
            {
                if (small == null) small = new Font(Tok.Mono.FontFamily, 9f);
                return small;
            }
        }

        public FEntryList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AccessibleName = "Entries";
        }

        public int Selected { get { return selected; } }

        public CheatRow SelectedRow { get { return selected >= 0 && selected < Rows.Count ? Rows[selected] : null; } }

        public string EmptyText = "Nothing matches.";

        public void SetRows(IEnumerable<CheatRow> rows)
        {
            Rows.Clear();
            Rows.AddRange(rows);
            scroll = 0;
            selected = Rows.Count > 0 ? 0 : -1;
            Invalidate();
            Raise();
        }

        void Raise() { if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); }

        public void Select(int index)
        {
            if (index < 0 || index >= Rows.Count) return;
            selected = index;
            EnsureVisible();
            Invalidate();
            Raise();
        }

        public void Move(int delta)
        {
            if (Rows.Count == 0) return;
            int i = Math.Max(0, Math.Min(Rows.Count - 1, (selected < 0 ? 0 : selected) + delta));
            if (i != selected) Select(i);
        }

        public void MoveTo(bool end)
        {
            if (Rows.Count == 0) return;
            Select(end ? Rows.Count - 1 : 0);
        }

        public int PageRows { get { return Math.Max(1, Height / RowH - 1); } }

        public Rectangle RowBounds(int index)
        {
            return new Rectangle(0, index * RowH - scroll, Width, RowH);
        }

        void EnsureVisible()
        {
            if (selected < 0) return;
            int top = selected * RowH;
            if (top < scroll) scroll = top;
            else if (top + RowH > scroll + Height) scroll = top + RowH - Height;
            Clamp();
        }

        void Clamp()
        {
            int max = Math.Max(0, Rows.Count * RowH - Height);
            if (scroll > max) scroll = max;
            if (scroll < 0) scroll = 0;
        }

        int IndexAt(int y)
        {
            int i = (y + scroll) / RowH;
            return i >= 0 && i < Rows.Count ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = IndexAt(e.Y);
            if (i != hover) { hover = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != -1) { hover = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = IndexAt(e.Y);
            if (i >= 0) Select(i);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (IndexAt(e.Y) >= 0 && Activated != null) Activated(this, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll -= Math.Sign(e.Delta) * RowH * 2;
            Clamp();
            Invalidate();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Clamp(); }

        protected override AccessibleObject CreateAccessibilityInstance() { return new ListAccessible(this); }

        class ListAccessible : Control.ControlAccessibleObject
        {
            readonly FEntryList owner;
            public ListAccessible(FEntryList o) : base(o) { owner = o; }
            public override AccessibleRole Role { get { return AccessibleRole.List; } }
            public override int GetChildCount() { return owner.Rows.Count; }
            public override AccessibleObject GetChild(int index) { return index >= 0 && index < owner.Rows.Count ? new RowAccessible(owner, this, index) : null; }
        }

        class RowAccessible : AccessibleObject
        {
            readonly FEntryList owner;
            readonly AccessibleObject parent;
            readonly int index;
            public RowAccessible(FEntryList o, AccessibleObject p, int i) { owner = o; parent = p; index = i; }
            CheatRow Row { get { return index < owner.Rows.Count ? owner.Rows[index] : null; } }
            public override string Name { get { return Row == null ? "" : Row.Title; } }
            public override string Value { get { return Row == null ? "" : Row.Id; } }
            public override string Description { get { return Row == null ? "" : Row.Mono; } }
            public override AccessibleRole Role { get { return AccessibleRole.ListItem; } }
            public override AccessibleObject Parent { get { return parent; } }
            public override Rectangle Bounds
            {
                get
                {
                    var r = owner.RowBounds(index);
                    return new Rectangle(owner.PointToScreen(new Point(r.X, r.Y)), r.Size);
                }
            }
            public override AccessibleStates State
            {
                get
                {
                    var s = AccessibleStates.Selectable | AccessibleStates.Focusable;
                    if (owner.Selected == index) s |= AccessibleStates.Selected | AccessibleStates.Focused;
                    return s;
                }
            }
            public override void DoDefaultAction() { owner.Select(index); if (owner.Activated != null) owner.Activated(owner, EventArgs.Empty); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent == null ? Tok.Window : Parent.BackColor);
            var frame = new Rectangle(0, 0, Width - 1, Height - 1);
            FGfx.FillRound(g, frame, Tok.RadiusLarge, Tok.Surface);
            var oldClip = g.Clip;
            g.SetClip(new Rectangle(1, 1, Width - 2, Height - 2));
            if (Rows.Count == 0)
                FGfx.Text(g, EmptyText, Tok.Body, new Rectangle(Tok.S3, Tok.S3, Width - Tok.S3 * 2, UI.S(28)), Tok.TextSecondary, FGfx.Line);
            for (int i = 0; i < Rows.Count; i++)
            {
                int y = i * RowH - scroll;
                if (y + RowH < 0) continue;
                if (y > Height) break;
                var row = Rows[i];
                var inner = new Rectangle(Tok.S1, y + 1, Width - Tok.S1 * 2, RowH - 2);
                if (i == selected)
                {
                    FGfx.FillRound(g, inner, Tok.RadiusSmall, UI.Blend(Tok.Surface, Tok.Accent, UI.Dark ? 0.18f : 0.12f));
                    FGfx.FillRound(g, new Rectangle(inner.X, inner.Y + (inner.Height - UI.S(16)) / 2, UI.S(3), UI.S(16)), 1, Tok.Accent);
                }
                else if (i == hover) FGfx.FillRound(g, inner, Tok.RadiusSmall, UI.Blend(Tok.Surface, UI.Dark ? Color.White : Color.Black, 0.06f));
                int right = inner.Right - Tok.S3;
                if (row.Risk != Risk.Safe || row.Admin)
                {
                    int d = UI.S(8);
                    var dot = new Rectangle(right - d, inner.Y + UI.S(10), d, d);
                    Color c = row.Risk == Risk.Destructive ? Tok.RiskDestructive : Tok.RiskModifies;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var b = new SolidBrush(c)) g.FillEllipse(b, dot);
                    if (row.Admin) using (var p = new Pen(c, 1.5f)) g.DrawEllipse(p, dot.X - 3, dot.Y - 3, dot.Width + 6, dot.Height + 6);
                    g.SmoothingMode = SmoothingMode.Default;
                    right -= d + UI.S(10);
                }
                int left = inner.X + Tok.S3;
                FGfx.Text(g, row.Title, Tok.Body, new Rectangle(left, inner.Y + UI.S(3), Math.Max(10, right - left), UI.S(22)), Tok.Text, FGfx.Line);
                var chip = new Rectangle(left, inner.Y + UI.S(26), Math.Max(10, right + Tok.S1 - left), UI.S(20));
                FGfx.FillRound(g, chip, Tok.RadiusSmall, Tok.Window);
                FGfx.Text(g, row.Mono.Replace("\r", " ").Replace("\n", " "), SmallMono, new Rectangle(chip.X + Tok.S2, chip.Y, chip.Width - Tok.S2 * 2, chip.Height), Tok.TextSecondary, FGfx.Line);
            }
            g.Clip = oldClip;
            FGfx.StrokeRound(g, frame, Tok.RadiusLarge, Tok.Stroke);
            int total = Rows.Count * RowH;
            if (total > Height)
            {
                int trackH = Height - Tok.S2 * 2;
                int thumb = Math.Max(UI.S(24), trackH * Height / total);
                int top = Tok.S2 + (trackH - thumb) * scroll / Math.Max(1, total - Height);
                FGfx.FillRound(g, new Rectangle(Width - UI.S(6), top, UI.S(3), thumb), 1, Tok.StrokeStrong);
            }
        }
    }

    class CheatDetail : Control
    {
        enum Kind { Title, Meta, Body, Heading, Code, Param, ParamText, Link, Caption }

        class Block
        {
            public Kind Kind;
            public string Text = "";
            public string Link = "";
            public Rectangle Rect;
            public Color Color;
        }

        readonly List<Block> blocks = new List<Block>();
        readonly List<Block> pending = new List<Block>();
        int scroll, contentHeight;
        string emptyText = "Pick an entry on the left.";
        static Font small;

        public event Action<string> LinkClicked;

        public CheatDetail()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AccessibleName = "Details";
        }

        static Font SmallMono
        {
            get
            {
                if (small == null) small = new Font(Tok.Mono.FontFamily, 9f);
                return small;
            }
        }

        public string Summary = "";

        void Add(Kind k, string text, string link)
        {
            pending.Add(new Block { Kind = k, Text = text, Link = link ?? "" });
        }

        public void Clear(string message)
        {
            pending.Clear();
            blocks.Clear();
            emptyText = message;
            Summary = "";
            scroll = 0;
            Invalidate();
        }

        public void ShowEntry(CheatEntry e, CheatSheetData data)
        {
            pending.Clear();
            Add(Kind.Title, e.Title, null);
            var meta = new List<string> { data.CategoryLabel(e.Category), e.Risk == "destructive" ? "Destructive" : e.Risk == "modifies" ? "Changes things" : "Safe to look at" };
            if (e.NeedsPwsh) meta.Add("PowerShell 7");
            if (e.Admin) meta.Add("Administrator");
            Add(Kind.Meta, string.Join("  -  ", meta.ToArray()), e.Risk);
            Add(Kind.Body, e.Explain, null);
            Add(Kind.Heading, "Command", null);
            Add(Kind.Code, e.Command, null);
            if (e.Params.Count > 0)
            {
                Add(Kind.Heading, "Parameters", null);
                foreach (var p in e.Params) { Add(Kind.Param, p.Name, null); Add(Kind.ParamText, p.Text, null); }
            }
            if (e.Example.Length > 0 && e.Example != e.Command)
            {
                Add(Kind.Heading, "Example", null);
                Add(Kind.Code, e.Example, null);
            }
            if (e.ExampleOutput.Length > 0)
            {
                Add(Kind.Heading, "Example output", null);
                Add(Kind.Code, e.ExampleOutput, null);
            }
            if (e.Aliases.Length > 0)
            {
                Add(Kind.Heading, "Also known as", null);
                Add(Kind.Caption, string.Join(",  ", e.Aliases), null);
            }
            var related = e.Related.Select(r => data.Find(r)).Where(x => x != null).ToList();
            if (related.Count > 0)
            {
                Add(Kind.Heading, "Related", null);
                foreach (var r in related) Add(Kind.Link, r.Title, r.Id);
            }
            Summary = e.Title;
            Commit();
        }

        public void ShowTranslation(string kind, CheatTranslation t)
        {
            pending.Clear();
            Add(Kind.Title, t.From, null);
            Add(Kind.Meta, kind == "cmd" ? "Command Prompt to PowerShell" : "Linux and bash to PowerShell", "safe");
            Add(Kind.Heading, kind == "cmd" ? "In cmd" : "In bash", null);
            Add(Kind.Code, t.From, null);
            Add(Kind.Heading, "In PowerShell", null);
            Add(Kind.Code, t.To, null);
            if (t.Note.Length > 0)
            {
                Add(Kind.Heading, "Note", null);
                Add(Kind.Body, t.Note, null);
            }
            Summary = t.From;
            Commit();
        }

        void Commit()
        {
            scroll = 0;
            blocks.Clear();
            blocks.AddRange(pending);
            LayoutBlocks();
            Invalidate();
        }

        Font FontFor(Kind k)
        {
            switch (k)
            {
                case Kind.Title: return Tok.Semibold;
                case Kind.Heading: return UI.SmallBold;
                case Kind.Code: return SmallMono;
                case Kind.Param: return SmallMono;
                case Kind.Meta:
                case Kind.Caption: return Tok.Caption;
                default: return Tok.Body;
            }
        }

        const TextFormatFlags Wrap = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.TextBoxControl;

        void LayoutBlocks()
        {
            int pad = Tok.S3;
            int w = Math.Max(40, Width - pad * 2 - UI.S(6));
            int y = pad;
            foreach (var b in blocks)
            {
                Font f = FontFor(b.Kind);
                int inner = b.Kind == Kind.Code ? w - Tok.S2 * 2 : w;
                int h = TextRenderer.MeasureText(b.Text.Length == 0 ? " " : b.Text, f, new Size(inner, int.MaxValue), Wrap).Height;
                if (b.Kind == Kind.Code) h += Tok.S2 * 2;
                if (b.Kind == Kind.Heading) y += Tok.S2;
                if (b.Kind == Kind.Body) y += Tok.S1;
                b.Rect = new Rectangle(pad, y, w, h);
                y += h + (b.Kind == Kind.Title || b.Kind == Kind.Meta ? Tok.S1 : b.Kind == Kind.Param ? 0 : Tok.S1);
            }
            contentHeight = y + pad;
            ClampScroll();
        }

        void ClampScroll()
        {
            int max = Math.Max(0, contentHeight - Height);
            if (scroll > max) scroll = max;
            if (scroll < 0) scroll = 0;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (blocks.Count > 0) LayoutBlocks();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll -= Math.Sign(e.Delta) * UI.S(60);
            ClampScroll();
            Invalidate();
        }

        Block LinkAt(Point p)
        {
            foreach (var b in blocks)
                if (b.Kind == Kind.Link && new Rectangle(b.Rect.X, b.Rect.Y - scroll, b.Rect.Width, b.Rect.Height).Contains(p)) return b;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = LinkAt(e.Location) != null ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var b = LinkAt(e.Location);
            if (b != null && LinkClicked != null) LinkClicked(b.Link);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent == null ? Tok.Window : Parent.BackColor);
            var frame = new Rectangle(0, 0, Width - 1, Height - 1);
            FGfx.FillRound(g, frame, Tok.RadiusLarge, Tok.Surface);
            var oldClip = g.Clip;
            g.SetClip(new Rectangle(1, 1, Width - 2, Height - 2));
            if (blocks.Count == 0)
                TextRenderer.DrawText(g, emptyText, Tok.Body, new Rectangle(Tok.S3, Tok.S3, Width - Tok.S3 * 2, UI.S(60)), Tok.TextSecondary, Wrap);
            foreach (var b in blocks)
            {
                var r = new Rectangle(b.Rect.X, b.Rect.Y - scroll, b.Rect.Width, b.Rect.Height);
                if (r.Bottom < 0 || r.Top > Height) continue;
                Font f = FontFor(b.Kind);
                switch (b.Kind)
                {
                    case Kind.Title:
                        TextRenderer.DrawText(g, b.Text, Tok.Semibold, r, Tok.Text, Wrap);
                        break;
                    case Kind.Meta:
                        {
                            Color c = b.Link == "destructive" ? Tok.RiskDestructive : b.Link == "modifies" ? Tok.RiskModifies : Tok.TextSecondary;
                            TextRenderer.DrawText(g, b.Text, f, r, c, Wrap);
                            break;
                        }
                    case Kind.Heading:
                        TextRenderer.DrawText(g, b.Text, f, r, Tok.TextSecondary, Wrap);
                        break;
                    case Kind.Code:
                        FGfx.FillRound(g, r, Tok.RadiusSmall, Tok.Window);
                        FGfx.StrokeRound(g, r, Tok.RadiusSmall, Tok.Stroke);
                        TextRenderer.DrawText(g, b.Text, f, new Rectangle(r.X + Tok.S2, r.Y + Tok.S2, r.Width - Tok.S2 * 2, r.Height - Tok.S2 * 2), Tok.Text, Wrap);
                        break;
                    case Kind.Param:
                        TextRenderer.DrawText(g, b.Text, f, r, Tok.Text, Wrap);
                        break;
                    case Kind.ParamText:
                    case Kind.Caption:
                        TextRenderer.DrawText(g, b.Text, f, r, Tok.TextSecondary, Wrap);
                        break;
                    case Kind.Link:
                        using (var u = new Font(f, FontStyle.Underline))
                            TextRenderer.DrawText(g, b.Text, u, r, UI.Dark ? Tok.Accent : Tok.Text, Wrap);
                        break;
                    default:
                        TextRenderer.DrawText(g, b.Text, f, r, Tok.Text, Wrap);
                        break;
                }
            }
            g.Clip = oldClip;
            FGfx.StrokeRound(g, frame, Tok.RadiusLarge, Tok.Stroke);
            if (contentHeight > Height)
            {
                int trackH = Height - Tok.S2 * 2;
                int thumb = Math.Max(UI.S(24), trackH * Height / contentHeight);
                int top = Tok.S2 + (trackH - thumb) * scroll / Math.Max(1, contentHeight - Height);
                FGfx.FillRound(g, new Rectangle(Width - UI.S(6), top, UI.S(3), thumb), 1, Tok.StrokeStrong);
            }
        }
    }

    class CheatSheetForm : Form
    {
        readonly CheatSheetData data;
        readonly string dir;
        readonly Label title = new Label { Text = "Cheat sheet", AutoSize = false, AccessibleName = "Cheat sheet title" };
        readonly FSegment tabs = new FSegment { AccessibleName = "Cheat sheet section" };
        readonly FSearchBox search = new FSearchBox("Search tasks, commands and aliases, for example grep, port 3000, ls -la");
        readonly FTreeList categories = new FTreeList { AccessibleName = "Categories" };
        readonly FEntryList list = new FEntryList();
        readonly CheatDetail detail = new CheatDetail();
        readonly FInfoBar info = new FInfoBar { Visible = false };
        readonly FButton tryHere = new FButton { Text = "Try here", Kind = FButton.Look.Accent, AccessibleName = "Try here" };
        readonly FButton copy = new FButton { Text = "Copy", AccessibleName = "Copy" };
        readonly FButton saveAction = new FButton { Text = "Save as action...", AccessibleName = "Save as action" };
        readonly Label count = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, AccessibleName = "Result count" };
        readonly List<string> categoryIds = new List<string>();
        string category;
        bool loading;

        public CheatSheetForm(string workDir, CheatSheetData sheet)
        {
            data = sheet ?? CheatSheetData.Current;
            dir = string.IsNullOrEmpty(workDir) ? Environment.CurrentDirectory : workDir;
            Text = "PS Tools Cheat sheet";
            AccessibleName = "PS Tools Cheat sheet";
            Font = UI.Font;
            ClientSize = new Size(UI.S(1100), UI.S(700));
            MinimumSize = new Size(UI.S(880), UI.S(560));
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            title.Font = Tok.Semibold;
            title.Font = UI.Title;
            tabs.Items.AddRange(new[] { "PowerShell", "cmd to PS", "bash to PS" });
            tabs.SelectedChanged += (s, e) => { category = null; Refill(); LayoutAll(); search.Input.Focus(); };
            search.AccessibleName = "Search";
            search.Input.AccessibleName = "Search";
            search.Input.TextChanged += (s, e) => { if (!loading) Refill(); };
            search.Input.KeyDown += OnSearchKey;
            categories.SelectionChanged += (s, e) =>
            {
                if (loading) return;
                int at = categories.Selected;
                string id = at <= 0 || at >= categoryIds.Count ? null : categoryIds[at];
                if (id != category) { category = id; Refill(); }
            };
            list.SelectionChanged += (s, e) => ShowSelected();
            list.Activated += (s, e) => TryHere();
            detail.LinkClicked += id => GoTo(id);
            tryHere.Click += (s, e) => TryHere();
            copy.Click += (s, e) => CopyCommand();
            saveAction.Click += (s, e) => SaveAsAction();
            count.Font = Tok.Caption;

            foreach (Control c in new Control[] { title, tabs, search, categories, list, detail, info, tryHere, copy, saveAction, count })
                Controls.Add(c);

            UI.Apply(this, false);
            BackColor = Tok.Window;
            search.RefreshTheme();
            count.ForeColor = Tok.TextSecondary;
            Resize += (s, e) => LayoutAll();
            Shown += (s, e) => { LayoutAll(); search.Input.Focus(); };

            if (data.Error.Length > 0) info.Show(data.Error, true);
            BuildCategories();
            Refill();
            LayoutAll();
        }

        public int Tab { get { return tabs.SelectedIndex; } }

        public string Query { get { return search.Input.Text; } }

        public string WorkDir { get { return dir; } }

        public int RowCount { get { return list.Rows.Count; } }

        public string SelectedId { get { var r = list.SelectedRow; return r == null ? "" : r.Id; } }

        public void SetTab(int index) { tabs.SelectedIndex = index; }

        public void SetQuery(string text) { search.Input.Text = text; }

        public void SelectCategory(string id)
        {
            for (int i = 0; i < categoryIds.Count; i++)
                if (categoryIds[i] == (id ?? ""))
                {
                    categories.Select(i);
                    return;
                }
        }

        public void SelectRow(int index) { list.Select(index); }

        public bool SelectEntry(string id)
        {
            for (int i = 0; i < list.Rows.Count; i++)
                if (list.Rows[i].Id == id) { list.Select(i); return true; }
            return false;
        }

        void BuildCategories()
        {
            loading = true;
            var rows = new List<PmRow>();
            categoryIds.Clear();
            categoryIds.Add("");
            rows.Add(new PmRow { Title = "All", Secondary = data.Entries.Count.ToString(), Glyph = "\uE8FD" });
            foreach (var c in data.Categories)
            {
                string glyph = "";
                if (c.Icon.Length == 4) { try { glyph = ((char)Convert.ToInt32(c.Icon, 16)).ToString(); } catch { } }
                if (glyph.Length > 0 && !Fx.GlyphOk(glyph)) glyph = "";
                var row = new PmRow { Title = c.Label, Secondary = data.CountIn(c.Id).ToString(), Glyph = glyph };
                categoryIds.Add(c.Id);
                rows.Add(row);
            }
            categories.SetRows(rows, 0);
            loading = false;
        }

        List<CheatTranslation> CurrentTranslations { get { return tabs.SelectedIndex == 1 ? data.Cmd : data.Bash; } }

        string TabKind { get { return tabs.SelectedIndex == 1 ? "cmd" : "bash"; } }

        static Risk ToRisk(string r) { return r == "destructive" ? Risk.Destructive : r == "modifies" ? Risk.Modifies : Risk.Safe; }

        void Refill()
        {
            string q = search.Input.Text;
            var rows = new List<CheatRow>();
            if (tabs.SelectedIndex == 0)
            {
                foreach (var e in data.Search(q, category))
                    rows.Add(new CheatRow { Title = e.Title, Mono = e.Command, Id = e.Id, Risk = ToRisk(e.Risk), Admin = e.Admin, Source = e });
                list.EmptyText = "No entry matches. Try fewer words, or look in the cmd and bash tabs.";
            }
            else
            {
                foreach (var t in data.SearchTranslations(CurrentTranslations, q))
                    rows.Add(new CheatRow { Title = t.From, Mono = t.To, Id = TabKind + ":" + t.From, Source = t });
                list.EmptyText = "No translation matches.";
            }
            list.SetRows(rows);
            UpdateTabLabels();
            count.Text = rows.Count + (tabs.SelectedIndex == 0 ? " of " + data.Entries.Count : " of " + CurrentTranslations.Count);
        }

        void UpdateTabLabels()
        {
            string q = search.Input.Text;
            string[] names = { "PowerShell", "cmd to PS", "bash to PS" };
            if (q.Trim().Length > 0)
            {
                int c = data.SearchTranslations(data.Cmd, q).Count, b = data.SearchTranslations(data.Bash, q).Count;
                names[1] += " (" + c + ")";
                names[2] += " (" + b + ")";
                names[0] += " (" + data.Search(q, null).Count + ")";
            }
            for (int i = 0; i < names.Length; i++) tabs.Items[i] = names[i];
            tabs.Invalidate();
        }

        CheatEntry SelectedEntry { get { var r = list.SelectedRow; return r == null ? null : r.Source as CheatEntry; } }

        CheatTranslation SelectedTranslation { get { var r = list.SelectedRow; return r == null ? null : r.Source as CheatTranslation; } }

        string SelectedText
        {
            get
            {
                var e = SelectedEntry;
                if (e != null) return e.RunText;
                var t = SelectedTranslation;
                return t == null ? "" : t.To;
            }
        }

        void ShowSelected()
        {
            var e = SelectedEntry;
            var t = SelectedTranslation;
            if (e != null) detail.ShowEntry(e, data);
            else if (t != null) detail.ShowTranslation(TabKind, t);
            else detail.Clear(data.Error.Length > 0 ? data.Error : "Nothing selected.");
            bool has = e != null || (t != null && !t.To.TrimStart().StartsWith("#"));
            tryHere.Enabled = has;
            copy.Enabled = has;
            saveAction.Enabled = has;
        }

        void GoTo(string id)
        {
            if (tabs.SelectedIndex != 0) tabs.SelectedIndex = 0;
            if (search.Input.Text.Length > 0) { loading = true; search.Input.Text = ""; loading = false; }
            category = null;
            SelectCategory(null);
            Refill();
            SelectEntry(id);
        }

        void OnSearchKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { list.Move(1); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Up) { list.Move(-1); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.PageDown) { list.Move(list.PageRows); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.PageUp) { list.Move(-list.PageRows); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter && !e.Control) { TryHere(); e.SuppressKeyPress = true; }
        }

        public void CopyCommand()
        {
            string text = SelectedEntry != null ? SelectedEntry.Command : SelectedTranslation != null ? SelectedTranslation.To : "";
            if (text.Length > 0) Clip.Set(text);
        }

        public RunForm LastRun;

        public void TryHere()
        {
            string text = SelectedText;
            if (text.Length == 0 || text.TrimStart().StartsWith("#")) return;
            var e = SelectedEntry;
            bool wantsPwsh = e != null && e.NeedsPwsh;
            bool havePwsh = RunEngine.FindPwsh() != null;
            var run = new RunForm(dir, text, "background");
            run.SetShell(wantsPwsh && havePwsh ? 1 : 0);
            if (wantsPwsh && !havePwsh)
                info.Show("This example needs PowerShell 7 (pwsh.exe), which was not found. It opened in Windows PowerShell 5.1 and may fail.", true, new KeyValuePair<string, System.Action>("Dismiss", () => { info.Hide2(); LayoutAll(); }));
            else if (info.Visible && data.Error.Length == 0) info.Hide2();
            LastRun = run;
            run.Show(this);
            LayoutAll();
        }

        public void SaveAsAction()
        {
            var e = SelectedEntry;
            var t = SelectedTranslation;
            if (e == null && t == null) return;
            string text = e != null ? (e.Template != null ? e.Template.Command : e.RunText) : t.To;
            if (text.TrimStart().StartsWith("#")) return;
            bool pwsh = e != null && e.NeedsPwsh && RunEngine.FindPwsh() != null;
            string[] when = e != null ? CheatSheetData.Context(e) : new[] { "folder", "background" };
            using (var f = new SaveActionForm(text, pwsh ? ActionShell.Pwsh : ActionShell.PowerShell, dir, when[0]))
            {
                string titleText = e != null ? e.Title : t.From;
                f.SetTitle(titleText.Length > 60 ? titleText.Substring(0, 60).TrimEnd() : titleText);
                f.SetMenu(e != null ? CheatSheetData.MenuFor(e.Category) : MenuTree.MyActions);
                f.SetContexts(when.Contains("file"), when.Contains("folder"), when.Contains("background"), when.Contains("drive"));
                if (e != null && e.Template != null)
                {
                    int idx = Array.IndexOf(new[] { "console", "clipboard", "grid", "toast" }, e.Template.Output);
                    if (idx >= 0) f.SetResult(idx);
                }
                f.ShowDialog(this);
            }
        }

        void LayoutAll()
        {
            if (ClientSize.Width < 10) return;
            int pad = Tok.S4, gap = Tok.S2, w = ClientSize.Width - pad * 2, y = pad;
            title.SetBounds(pad, y, UI.S(300), UI.S(36));
            count.SetBounds(ClientSize.Width - pad - UI.S(160), y, UI.S(160), UI.S(36));
            y += UI.S(36) + Tok.S1;
            int tabW = UI.S(420);
            tabs.SetBounds(pad, y, tabW, Tok.ButtonHeight);
            search.SetBounds(pad + tabW + gap, y, Math.Max(120, w - tabW - gap), Tok.InputHeight);
            y += Tok.ButtonHeight + gap;
            if (info.Visible) { info.SetBounds(pad, y, w, UI.S(44)); y += UI.S(44) + gap; }
            int bottom = ClientSize.Height - pad;
            bool showCats = tabs.SelectedIndex == 0;
            int catW = showCats ? UI.S(200) : 0;
            int detW = UI.S(360);
            categories.Visible = showCats;
            categories.SetBounds(pad, y, catW, Math.Max(40, bottom - y));
            int listX = pad + (showCats ? catW + gap : 0);
            int detX = ClientSize.Width - pad - detW;
            list.SetBounds(listX, y, Math.Max(100, detX - gap - listX), Math.Max(40, bottom - y));
            int btnY = bottom - Tok.ButtonHeight;
            detail.SetBounds(detX, y, detW, Math.Max(40, btnY - gap - y));
            int x = detX;
            foreach (var b in new[] { tryHere, copy, saveAction })
            {
                var size = b.GetPreferredSize(Size.Empty);
                int bw = Math.Max(size.Width, UI.S(96));
                b.SetBounds(x, btnY, bw, Tok.ButtonHeight);
                x += bw + gap;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Enter)) { TryHere(); return true; }
            if (keyData == (Keys.Control | Keys.D1)) { tabs.SelectedIndex = 0; return true; }
            if (keyData == (Keys.Control | Keys.D2)) { tabs.SelectedIndex = 1; return true; }
            if (keyData == (Keys.Control | Keys.D3)) { tabs.SelectedIndex = 2; return true; }
            if (keyData == (Keys.Control | Keys.F) || keyData == (Keys.Control | Keys.L)) { search.Input.Focus(); search.Input.SelectAll(); return true; }
            if (keyData == (Keys.Control | Keys.S)) { SaveAsAction(); return true; }
            if (keyData == (Keys.Control | Keys.C))
            {
                var tb = ActiveControl as TextBox;
                if (tb == null || tb.SelectionLength == 0) { CopyCommand(); return true; }
            }
            if (keyData == Keys.Escape)
            {
                if (search.Input.Text.Length > 0) { search.Input.Text = ""; return true; }
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
