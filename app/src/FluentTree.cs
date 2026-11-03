using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    static class FGfx
    {
        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = Math.Max(1, radius * 2);
            if (radius <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color c)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Round(r, radius))
            using (var b = new SolidBrush(c)) g.FillPath(b, path);
        }

        public static void StrokeRound(Graphics g, Rectangle r, int radius, Color c)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Round(r, radius))
            using (var pen = new Pen(c)) g.DrawPath(pen, path);
        }

        public const TextFormatFlags Line = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine;

        public static void Text(Graphics g, string text, Font font, Rectangle r, Color c, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text ?? "", font, r, c, flags);
        }

        public static string MiddleEllipsis(string path, int maxChars)
        {
            if (path == null) return "";
            if (path.Length <= maxChars || maxChars < 8) return path;
            int head = (maxChars - 1) / 3;
            int tail = maxChars - 1 - head;
            return path.Substring(0, head) + "\u2026" + path.Substring(path.Length - tail);
        }
    }

    static class FKbd
    {
        public static int Measure(Graphics g, string key, string label)
        {
            Size k = TextRenderer.MeasureText(g, key, Tok.Caption, new Size(int.MaxValue, 0), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            Size l = TextRenderer.MeasureText(g, label, Tok.Caption, new Size(int.MaxValue, 0), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            return k.Width + Tok.S2 + Tok.S1 + l.Width;
        }

        public static int Draw(Graphics g, int x, int y, int height, string key, string label)
        {
            Size k = TextRenderer.MeasureText(g, key, Tok.Caption, new Size(int.MaxValue, 0), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            Size l = TextRenderer.MeasureText(g, label, Tok.Caption, new Size(int.MaxValue, 0), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            var cap = new Rectangle(x, y + (height - (k.Height + Tok.S1)) / 2, k.Width + Tok.S2, k.Height + Tok.S1);
            FGfx.FillRound(g, cap, Tok.RadiusSmall, UI.Dark ? Color.FromArgb(58, 58, 58) : Color.FromArgb(238, 238, 238));
            FGfx.StrokeRound(g, cap, Tok.RadiusSmall, Tok.Stroke);
            FGfx.Text(g, key, Tok.Caption, cap, Tok.TextSecondary, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            var lr = new Rectangle(cap.Right + Tok.S1, y, l.Width + 2, height);
            FGfx.Text(g, label, Tok.Caption, lr, Tok.TextSecondary, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.Left);
            return lr.Right - x;
        }
    }

    class FSearchBox : Control
    {
        public readonly TextBox Input = new TextBox();
        public bool ShowGlyph = true;
        bool focused;

        public FSearchBox(string cue)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Input.Tag = "raw";
            Input.BorderStyle = BorderStyle.None;
            Input.Font = Tok.Body;
            Input.BackColor = Tok.Input;
            Input.ForeColor = Tok.Text;
            Input.GotFocus += (s, e) => { focused = true; Invalidate(); };
            Input.LostFocus += (s, e) => { focused = false; Invalidate(); };
            Controls.Add(Input);
            ResultsForm.SetCue(Input, cue);
            TabStop = false;
            Height = Tok.InputHeight;
        }

        public override string Text { get { return Input.Text; } set { Input.Text = value; } }

        public void RefreshTheme()
        {
            Input.BackColor = Tok.Input;
            Input.ForeColor = Tok.Text;
            Invalidate();
        }

        void PlaceInput()
        {
            int left = ShowGlyph ? Tok.S3 + UI.S(20) : Tok.S3;
            int h = Input.PreferredHeight;
            var want = new Rectangle(left, (Height - h) / 2, Math.Max(10, Width - left - Tok.S3), h);
            if (Input.Bounds != want) Input.SetBounds(want.X, want.Y, want.Width, want.Height);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            PlaceInput();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            PlaceInput();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            PlaceInput();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            PlaceInput();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            PlaceInput();
            var g = e.Graphics;
            g.Clear(Parent == null ? Tok.SurfaceAlt : Parent.BackColor);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            FGfx.FillRound(g, r, Tok.RadiusSmall, Tok.Input);
            FGfx.StrokeRound(g, r, Tok.RadiusSmall, Tok.InputStroke);
            if (focused)
            {
                var bar = new Rectangle(Tok.RadiusSmall, Height - 3, Width - Tok.RadiusSmall * 2, 2);
                g.SmoothingMode = SmoothingMode.None;
                using (var b = new SolidBrush(Tok.Accent)) g.FillRectangle(b, bar);
            }
            if (ShowGlyph)
                using (var f = UI.Icons(11f))
                    TextRenderer.DrawText(g, "\uE721", f, new Rectangle(Tok.S3, 0, UI.S(20), Height), Tok.TextSecondary, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    class PmRow
    {
        public string Title = "", Secondary = "", Glyph = "";
        public bool Header, Folder, Favorite, Admin, Extended;
        public Risk Risk = Risk.Safe;
        public MenuNode Node;
    }

    class FTreeList : Control
    {
        public readonly List<PmRow> Rows = new List<PmRow>();
        int selected = -1, hover = -1, scroll;

        public event EventHandler SelectionChanged;
        public event EventHandler<EventArgs> Activated;
        public event EventHandler<MouseEventArgs> RowRightClicked;
        public bool ShowDigits;

        public List<int> VisibleSelectable()
        {
            var l = new List<int>();
            int pos = -scroll;
            for (int i = 0; i < Rows.Count; i++)
            {
                int h = HeightOf(Rows[i]);
                if (!Rows[i].Header && pos + h > 0 && pos < Height && pos >= -h / 2) l.Add(i);
                pos += h;
            }
            return l;
        }

        public void Select(int index)
        {
            if (index < 0 || index >= Rows.Count || Rows[index].Header) return;
            selected = index;
            EnsureVisible();
            Invalidate();
            RaiseSelection();
        }

        public Rectangle RowBounds(int index)
        {
            int y = -scroll;
            for (int i = 0; i < index && i < Rows.Count; i++) y += HeightOf(Rows[i]);
            return new Rectangle(0, y, Width, index >= 0 && index < Rows.Count ? HeightOf(Rows[index]) : 0);
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new ListAccessible(this);
        }

        class ListAccessible : Control.ControlAccessibleObject
        {
            readonly FTreeList owner;

            public ListAccessible(FTreeList o) : base(o) { owner = o; }

            public override AccessibleRole Role { get { return AccessibleRole.MenuPopup; } }

            public override int GetChildCount() { return owner.Rows.Count; }

            public override AccessibleObject GetChild(int index)
            {
                return index >= 0 && index < owner.Rows.Count ? new RowAccessible(owner, this, index) : null;
            }
        }

        class RowAccessible : AccessibleObject
        {
            readonly FTreeList owner;
            readonly AccessibleObject parent;
            readonly int index;

            public RowAccessible(FTreeList o, AccessibleObject p, int i) { owner = o; parent = p; index = i; }

            PmRow Row { get { return index < owner.Rows.Count ? owner.Rows[index] : null; } }

            public override string Name { get { return Row == null ? "" : Row.Title; } }

            public override string Description
            {
                get { return Row == null ? "" : (Row.Header ? "Section" : Row.Folder ? "Folder" : Row.Risk == Risk.Safe ? "Action" : "Action, " + Row.Risk.ToString().ToLowerInvariant()); }
            }

            public override string Value { get { return Row != null && Row.Node != null && Row.Node.Spec != null ? Row.Node.Spec.Id : ""; } }

            public override AccessibleRole Role { get { return Row != null && Row.Header ? AccessibleRole.StaticText : AccessibleRole.MenuItem; } }

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

            public override void DoDefaultAction()
            {
                owner.Select(index);
                if (owner.Activated != null) owner.Activated(owner, EventArgs.Empty);
            }
        }

        public FTreeList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
        }

        public int Selected { get { return selected; } }

        public PmRow SelectedRow { get { return selected >= 0 && selected < Rows.Count ? Rows[selected] : null; } }

        public static int HeightOf(PmRow r) { return r.Header ? Tok.HeaderRowHeight : Tok.RowHeight; }

        public int ContentHeight { get { int h = 0; foreach (var r in Rows) h += HeightOf(r); return h; } }

        public void SetRows(IEnumerable<PmRow> rows, int select)
        {
            Rows.Clear();
            Rows.AddRange(rows);
            scroll = 0;
            selected = -1;
            if (select >= 0 && select < Rows.Count && !Rows[select].Header) selected = select;
            else selected = NextSelectable(-1, 1);
            EnsureVisible();
            Invalidate();
            RaiseSelection();
        }

        int NextSelectable(int from, int dir)
        {
            for (int i = from + dir; i >= 0 && i < Rows.Count; i += dir) if (!Rows[i].Header) return i;
            return -1;
        }

        public void Move(int delta)
        {
            if (Rows.Count == 0) return;
            int i = selected;
            int step = delta > 0 ? 1 : -1;
            for (int n = 0; n < Math.Abs(delta); n++)
            {
                int next = NextSelectable(i, step);
                if (next < 0) break;
                i = next;
            }
            if (i == selected) return;
            selected = i;
            EnsureVisible();
            Invalidate();
            RaiseSelection();
        }

        public void MoveTo(bool end)
        {
            if (Rows.Count == 0) return;
            int i = end ? NextSelectable(Rows.Count, -1) : NextSelectable(-1, 1);
            if (i < 0 || i == selected) return;
            selected = i;
            EnsureVisible();
            Invalidate();
            RaiseSelection();
        }

        void RaiseSelection() { if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); }

        int RowTop(int index)
        {
            int y = 0;
            for (int i = 0; i < index; i++) y += HeightOf(Rows[i]);
            return y;
        }

        public void EnsureVisible()
        {
            if (selected < 0) return;
            int top = RowTop(selected), h = HeightOf(Rows[selected]);
            int pad = selected > 0 && Rows[selected - 1].Header ? HeightOf(Rows[selected - 1]) : 0;
            if (top - pad < scroll) scroll = top - pad;
            else if (top + h > scroll + Height) scroll = top + h - Height;
            ClampScroll();
        }

        void ClampScroll()
        {
            int max = Math.Max(0, ContentHeight - Height);
            if (scroll > max) scroll = max;
            if (scroll < 0) scroll = 0;
        }

        int IndexAt(int y)
        {
            int pos = -scroll;
            for (int i = 0; i < Rows.Count; i++)
            {
                int h = HeightOf(Rows[i]);
                if (y >= pos && y < pos + h) return i;
                pos += h;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = IndexAt(e.Y);
            if (i >= 0 && Rows[i].Header) i = -1;
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
            if (i < 0 || Rows[i].Header) return;
            selected = i;
            Invalidate();
            RaiseSelection();
            if (e.Button == MouseButtons.Right) { if (RowRightClicked != null) RowRightClicked(this, e); return; }
            if (Activated != null) Activated(this, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll -= Math.Sign(e.Delta) * Tok.RowHeight * 3;
            ClampScroll();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClampScroll();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Tok.SurfaceAlt);
            int y = -scroll;
            int inset = Tok.S1;
            var digitRows = ShowDigits ? VisibleSelectable().Take(9).ToList() : new List<int>();
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                int h = HeightOf(row);
                if (y + h < 0) { y += h; continue; }
                if (y > Height) break;
                var rect = new Rectangle(inset, y, Width - inset * 2, h);
                if (row.Header)
                {
                    FGfx.Text(g, row.Title, Tok.Semibold, new Rectangle(rect.X + Tok.S3, rect.Y + Tok.S1, rect.Width - Tok.S3, rect.Height - Tok.S1), Tok.TextSecondary, FGfx.Line);
                    y += h;
                    continue;
                }
                var inner = new Rectangle(rect.X, rect.Y + 1, rect.Width, rect.Height - 2);
                if (i == selected)
                {
                    FGfx.FillRound(g, inner, Tok.RadiusSmall, Tok.Selected);
                    var pill = new Rectangle(inner.X, inner.Y + (inner.Height - UI.S(16)) / 2, UI.S(3), UI.S(16));
                    FGfx.FillRound(g, pill, 1, Tok.Accent);
                }
                else if (i == hover) FGfx.FillRound(g, inner, Tok.RadiusSmall, Tok.Hover);

                int right = inner.Right - Tok.S3;
                int digitAt = digitRows.IndexOf(i);
                if (digitAt >= 0)
                {
                    FGfx.Text(g, (digitAt + 1).ToString(), Tok.Caption, new Rectangle(right - UI.S(12), inner.Y, UI.S(12), inner.Height), Tok.TextDisabled, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                    right -= UI.S(20);
                }
                if (row.Folder)
                {
                    using (var f = UI.Icons(9f))
                        TextRenderer.DrawText(g, "\uE76C", f, new Rectangle(right - UI.S(14), inner.Y, UI.S(14), inner.Height), Tok.TextSecondary, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                    right -= UI.S(22);
                }
                if (row.Favorite)
                {
                    using (var f = UI.Icons(10f))
                        TextRenderer.DrawText(g, "\uE735", f, new Rectangle(right - UI.S(16), inner.Y, UI.S(16), inner.Height), Tok.Accent, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                    right -= UI.S(22);
                }
                if (row.Risk != Risk.Safe || row.Admin)
                {
                    int d = UI.S(8);
                    var dot = new Rectangle(right - d, inner.Y + (inner.Height - d) / 2, d, d);
                    Color c = row.Risk == Risk.Destructive ? Tok.RiskDestructive : Tok.RiskModifies;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var b = new SolidBrush(c)) g.FillEllipse(b, dot);
                    if (row.Admin)
                        using (var p = new Pen(c, 1.5f)) g.DrawEllipse(p, dot.X - 3, dot.Y - 3, dot.Width + 6, dot.Height + 6);
                    right -= d + UI.S(14);
                }
                if (row.Extended)
                {
                    var tag = new Rectangle(right - UI.S(44), inner.Y + (inner.Height - UI.S(18)) / 2, UI.S(40), UI.S(18));
                    FGfx.StrokeRound(g, tag, Tok.RadiusSmall, Tok.Stroke);
                    FGfx.Text(g, "Shift", Tok.Caption, tag, Tok.TextSecondary, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    right -= UI.S(50);
                }
                int textLeft = inner.X + Tok.S3 + UI.S(4);
                if (row.Glyph.Length > 0)
                {
                    using (var f = UI.Icons(10f))
                        TextRenderer.DrawText(g, row.Glyph, f, new Rectangle(textLeft - UI.S(4), inner.Y, UI.S(20), inner.Height), Tok.TextSecondary, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                    textLeft += UI.S(26);
                }
                if (row.Secondary.Length > 0)
                {
                    int secW = Math.Min(UI.S(150), Math.Max(UI.S(60), (right - textLeft) / 3));
                    FGfx.Text(g, row.Secondary, Tok.Caption, new Rectangle(right - secW, inner.Y, secW, inner.Height), Tok.TextSecondary, FGfx.Line | TextFormatFlags.Right);
                    right -= secW + Tok.S2;
                }
                FGfx.Text(g, row.Title, Tok.Body, new Rectangle(textLeft, inner.Y, Math.Max(10, right - textLeft), inner.Height), Tok.Text, FGfx.Line);
                y += h;
            }
            int total = ContentHeight;
            if (total > Height)
            {
                int trackH = Height - Tok.S2 * 2;
                int thumb = Math.Max(UI.S(24), trackH * Height / total);
                int top = Tok.S2 + (trackH - thumb) * scroll / Math.Max(1, total - Height);
                FGfx.FillRound(g, new Rectangle(Width - UI.S(5), top, UI.S(3), thumb), 1, Tok.StrokeStrong);
            }
        }
    }

    class FBreadcrumb : Control
    {
        public string Trail = "";
        public List<string> Parts = new List<string>();
        readonly List<Rectangle> hits = new List<Rectangle>();
        public event Action<int> LevelClicked;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            for (int i = 0; i < hits.Count; i++)
                if (hits[i].Contains(e.Location) && LevelClicked != null) { LevelClicked(i); return; }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = hits.Any(h => h.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        }

        public FBreadcrumb()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = UI.S(28);
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Tok.SurfaceAlt);
            hits.Clear();
            int x = Tok.S3;
            var all = new List<string> { "Menu" };
            all.AddRange(Parts);
            for (int i = 0; i < all.Count; i++)
            {
                var size = TextRenderer.MeasureText(g, all[i], Tok.Caption, new Size(int.MaxValue, 0), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                var r = new Rectangle(x, 0, size.Width, Height);
                bool last = i == all.Count - 1;
                FGfx.Text(g, all[i], Tok.Caption, r, last ? Tok.Text : Tok.Accent, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                hits.Add(new Rectangle(r.X - 2, 0, r.Width + 4, Height));
                x += size.Width;
                if (!last)
                {
                    FGfx.Text(g, "  \u203A  ", Tok.Caption, new Rectangle(x, 0, UI.S(30), Height), Tok.TextSecondary, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    x += TextRenderer.MeasureText(g, "  \u203A  ", Tok.Caption, new Size(int.MaxValue, 0), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
                }
            }
            using (var p = new Pen(Tok.Stroke)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }
}
