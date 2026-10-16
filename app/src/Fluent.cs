using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PSTools
{
    static class Fx
    {
        public const string FolderOpen = "\uE838", Folder = "\uE8B7", Play = "\uE768", Settings = "\uE713", Search = "\uE721", Undo = "\uE7A7",
            History = "\uE81C", Copy = "\uE8C8", Add = "\uE710", Delete = "\uE74D", Image = "\uE91B", Package = "\uE7B8", Share = "\uE72D",
            Link = "\uE71B", Code = "\uE943", List = "\uE8FD", Edit = "\uE70F", OpenWindow = "\uE8A7", Accept = "\uE8FB", Up = "\uE74A",
            Refresh = "\uE72C", Warning = "\uE7BA", Pin = "\uE718", Save = "\uE74E", Download = "\uE896", Cancel = "\uE711", Help = "\uE897";

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        static extern uint GetGlyphIndicesW(IntPtr hdc, string text, int count, ushort[] indices, uint flags);

        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr obj);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        static readonly Dictionary<string, bool> glyphCache = new Dictionary<string, bool>();

        public static bool GlyphOk(string glyph)
        {
            if (string.IsNullOrEmpty(glyph) || UI.IconFamily == null) return false;
            bool ok;
            if (glyphCache.TryGetValue(glyph, out ok)) return ok;
            try
            {
                using (var f = UI.Icons(12f))
                {
                    IntPtr dc = GetDC(IntPtr.Zero);
                    IntPtr hf = f.ToHfont();
                    IntPtr old = SelectObject(dc, hf);
                    var idx = new ushort[1];
                    uint r = GetGlyphIndicesW(dc, glyph, 1, idx, 1);
                    SelectObject(dc, old);
                    DeleteObject(hf);
                    ReleaseDC(IntPtr.Zero, dc);
                    ok = r != 0xFFFFFFFF && idx[0] != 0xFFFF;
                }
            }
            catch { ok = true; }
            glyphCache[glyph] = ok;
            return ok;
        }

        public static void DrawGlyph(Graphics g, string glyph, float size, Color color, RectangleF box)
        {
            if (!GlyphOk(glyph)) return;
            using (var f = UI.Icons(size))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            using (var b = new SolidBrush(color))
            {
                var old = g.TextRenderingHint;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(glyph, f, b, box, fmt);
                g.TextRenderingHint = old;
            }
        }

        public static void DrawCheckBox(Graphics g, RectangleF box, int state, bool hot)
        {
            bool on = state > 0;
            Color target = UI.Dark ? Color.White : Color.Black;
            Color fill = on ? UI.Blend(UI.Accent, target, hot ? 0.08f : 0f) : (hot ? UI.CtrlHover : UI.Field);
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Round(box, UI.S(3)))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (!on) using (var p = new Pen(UI.Blend(UI.Back, UI.Text, 0.55f))) g.DrawPath(p, path);
            }
            if (on)
                using (var p = new Pen(UI.OnAccent, Math.Max(1.6f, UI.S(2) * 0.9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    if (state == 2) g.DrawLine(p, box.X + box.Width * 0.28f, box.Y + box.Height / 2f, box.X + box.Width * 0.72f, box.Y + box.Height / 2f);
                    else g.DrawLines(p, new[]
                    {
                        new PointF(box.X + box.Width * 0.26f, box.Y + box.Height * 0.54f),
                        new PointF(box.X + box.Width * 0.43f, box.Y + box.Height * 0.70f),
                        new PointF(box.X + box.Width * 0.75f, box.Y + box.Height * 0.32f)
                    });
                }
            g.SmoothingMode = old;
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2f;
            if (radius <= 0f || r.Width < d || r.Height < d) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void ThemeMenu(ContextMenuStrip m)
        {
            m.Renderer = new FluentMenuRenderer();
            m.BackColor = UI.Card;
            m.ForeColor = UI.Text;
            m.Font = UI.Font;
        }

        public static Control Host(Control c)
        {
            return c.Parent is FCard ? c.Parent : c;
        }

        public static void Wrap(Control c, bool plate)
        {
            Control parent = c.Parent;
            if (parent == null || parent is FCard) return;
            var tb = c as TextBox;
            bool single = tb != null && !tb.Multiline;
            var card = new FCard { Plate = plate };
            card.Dock = c.Dock;
            card.Anchor = c.Anchor;
            card.Margin = c.Margin;
            card.Location = c.Location;
            card.Size = single && c.Dock != DockStyle.Fill ? new Size(c.Width, c.Height + UI.S(10)) : c.Size;
            var tlp = parent as TableLayoutPanel;
            int col = 0, row = 0, cs = 1, rs = 1;
            if (tlp != null) { col = tlp.GetColumn(c); row = tlp.GetRow(c); cs = tlp.GetColumnSpan(c); rs = tlp.GetRowSpan(c); }
            if (card.Dock == DockStyle.Fill && !plate && parent is Form && parent.Padding == Padding.Empty) parent.Padding = new Padding(UI.S(12), 0, UI.S(12), 0);
            int idx = parent.Controls.GetChildIndex(c);
            parent.SuspendLayout();
            parent.Controls.Remove(c);
            c.Margin = Padding.Empty;
            card.Adopt(c);
            if (tlp != null)
            {
                tlp.Controls.Add(card, col, row);
                tlp.SetColumnSpan(card, cs);
                tlp.SetRowSpan(card, rs);
            }
            else
            {
                parent.Controls.Add(card);
                parent.Controls.SetChildIndex(card, idx);
            }
            parent.ResumeLayout(true);
        }
    }

    class FCard : Panel
    {
        public bool Plate;
        public int Radius = 8;
        bool focused;
        Control inner;

        public FCard()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = UI.Card;
        }

        public void Adopt(Control c)
        {
            inner = c;
            if (Plate) { BackColor = UI.Field; Radius = 4; } else BackColor = UI.Card;
            var tb = c as TextBox;
            if (tb != null && !tb.Multiline)
            {
                Controls.Add(c);
                c.Dock = DockStyle.None;
                c.BackColor = UI.Field;
                PositionInner();
            }
            else
            {
                int pad = Plate ? UI.S(8) : UI.S(5);
                Padding = new Padding(pad);
                c.Dock = DockStyle.Fill;
                Controls.Add(c);
                if (Plate) c.BackColor = UI.Field;
            }
            if (Plate)
            {
                c.Enter += (s, e) => { focused = true; Invalidate(); };
                c.Leave += (s, e) => { focused = false; Invalidate(); };
            }
        }

        void PositionInner()
        {
            var tb = inner as TextBox;
            if (tb == null || tb.Multiline) return;
            int pad = UI.S(10);
            tb.SetBounds(pad, Math.Max(0, (Height - tb.Height) / 2), Math.Max(10, Width - pad * 2), tb.Height);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            PositionInner();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : UI.Back);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = Fx.Round(r, UI.S(Radius)))
            {
                using (var b = new SolidBrush(Plate ? UI.Field : UI.Card)) g.FillPath(b, path);
                using (var p = new Pen(Plate ? UI.CtrlStroke : UI.Border)) g.DrawPath(p, path);
                if (Plate && focused)
                {
                    g.SetClip(path);
                    using (var b = new SolidBrush(UI.Accent)) g.FillRectangle(b, 0, Height - UI.S(2), Width, UI.S(2));
                    g.ResetClip();
                }
            }
        }
    }

    class FButton : Button
    {
        public enum Look { Standard, Accent, Subtle }

        public Look Kind = Look.Standard;
        public string Glyph;
        float hover;
        bool over, down;
        readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer { Interval = 15 };

        public FButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Height = 32;
            Font = UI.Font;
            anim.Tick += (s, e) =>
            {
                float target = over ? 1f : 0f;
                hover += (target - hover) * 0.45f;
                if (Math.Abs(target - hover) < 0.03f) { hover = target; anim.Stop(); }
                Invalidate();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); over = true; anim.Start(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); over = false; down = false; anim.Start(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : UI.Back);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size t = TextRenderer.MeasureText(Text ?? "", Font, new Size(10000, 100), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            int w = t.Width + UI.S(28) + (Fx.GlyphOk(Glyph) ? UI.S(24) : 0);
            return new Size(Math.Max(w, UI.S(64)), Math.Max(Height, UI.S(32)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color fill, stroke = Color.Transparent, fore = UI.Text;
            Color target = UI.Dark ? Color.White : Color.Black;
            if (Kind == Look.Accent)
            {
                fill = Enabled ? UI.Blend(UI.Blend(UI.Accent, target, 0.10f), UI.Accent, 1f - hover) : UI.Blend(UI.Back, UI.Text, 0.16f);
                if (Enabled && down) fill = UI.Blend(UI.Accent, target, 0.18f);
                fore = Enabled ? UI.OnAccent : UI.Blend(UI.Back, UI.Text, 0.55f);
            }
            else if (Kind == Look.Subtle)
            {
                Color rest = UI.Back, high = UI.ListHover;
                fill = UI.Blend(rest, high, hover);
                if (Enabled && down) fill = UI.ListSelected;
                if (!Enabled) { fill = rest; fore = UI.Blend(UI.Back, UI.Text, 0.4f); }
            }
            else
            {
                fill = UI.Blend(UI.CtrlFill, UI.CtrlHover, hover);
                if (Enabled && down) fill = UI.CtrlDown;
                stroke = UI.CtrlStroke;
                if (!Enabled) { fill = UI.Blend(UI.Back, UI.CtrlFill, 0.5f); fore = UI.Blend(UI.Back, UI.Text, 0.4f); stroke = UI.Border; }
                else if (ForeColor == UI.Danger) fore = UI.Danger;
            }
            using (var path = Fx.Round(r, UI.S(4)))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (stroke != Color.Transparent) using (var p = new Pen(stroke)) g.DrawPath(p, path);
            }

            bool glyph = Fx.GlyphOk(Glyph);
            Size ts = TextRenderer.MeasureText(g, Text ?? "", Font, new Size(10000, 100), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            int gw = glyph ? UI.S(22) : 0;
            int total = gw + (string.IsNullOrEmpty(Text) ? 0 : ts.Width);
            int x = Math.Max(UI.S(6), (Width - total) / 2);
            if (glyph) Fx.DrawGlyph(g, Glyph, 11f, fore, new RectangleF(x - UI.S(2), 0, UI.S(22), Height));
            if (!string.IsNullOrEmpty(Text))
            {
                var tr = new Rectangle(x + gw, 0, Math.Max(10, Width - x - gw - UI.S(6)), Height);
                TextRenderer.DrawText(g, Text, Font, tr, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
            if (Focused && ShowFocusCues)
            {
                var fr = new RectangleF(1.5f, 1.5f, Width - 3f, Height - 3f);
                using (var path = Fx.Round(fr, UI.S(4))) using (var p = new Pen(UI.Text, 1.6f)) g.DrawPath(p, path);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) anim.Dispose();
            base.Dispose(disposing);
        }
    }

    class FToggle : Control
    {
        const int BoxSize = 18;

        bool over, down, isRadio, threeState;
        CheckState state = CheckState.Unchecked;
        string groupName = "";

        public event EventHandler CheckedChanged;

        public FToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.StandardDoubleClick, false);
            TabStop = true;
            Font = UI.Font;
        }

        public bool IsRadio
        {
            get { return isRadio; }
            set { isRadio = value; Invalidate(); }
        }

        public string GroupName
        {
            get { return groupName; }
            set { groupName = value ?? ""; }
        }

        public bool ThreeState
        {
            get { return threeState; }
            set { threeState = value; }
        }

        public bool Checked
        {
            get { return state != CheckState.Unchecked; }
            set { CheckState = value ? CheckState.Checked : CheckState.Unchecked; }
        }

        public CheckState CheckState
        {
            get { return state; }
            set
            {
                if (isRadio && value == CheckState.Indeterminate) value = CheckState.Checked;
                if (state == value) return;
                bool wasChecked = Checked;
                state = value;
                if (isRadio && Checked) ClearSiblings();
                Invalidate();
                if (wasChecked != Checked || !isRadio) RaiseChanged();
            }
        }

        void RaiseChanged()
        {
            var handler = CheckedChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        IEnumerable<FToggle> Siblings()
        {
            if (Parent == null) yield break;
            foreach (Control c in Parent.Controls)
            {
                var t = c as FToggle;
                if (t != null && t != this && t.isRadio && t.groupName == groupName) yield return t;
            }
        }

        void ClearSiblings()
        {
            foreach (var t in Siblings().ToList()) if (t.state != CheckState.Unchecked) t.CheckState = CheckState.Unchecked;
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (isRadio && Checked) ClearSiblings();
        }

        public void Toggle()
        {
            if (!Enabled) return;
            if (isRadio)
            {
                if (!Checked) CheckState = CheckState.Checked;
                return;
            }
            if (threeState) CheckState = state == CheckState.Unchecked ? CheckState.Checked : state == CheckState.Checked ? CheckState.Indeterminate : CheckState.Unchecked;
            else CheckState = state == CheckState.Unchecked ? CheckState.Checked : CheckState.Unchecked;
        }

        protected override void OnClick(EventArgs e)
        {
            Toggle();
            base.OnClick(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (isRadio && (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down)) return true;
            if (k == Keys.Enter) { var f = FindForm(); return f == null || f.AcceptButton == null; }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || (e.KeyCode == Keys.Enter && IsInputKey(Keys.Enter)))
            {
                Toggle();
                e.Handled = true;
            }
            else if (isRadio && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                MoveInGroup(e.KeyCode == Keys.Left || e.KeyCode == Keys.Up ? -1 : 1);
                e.Handled = true;
            }
        }

        void MoveInGroup(int step)
        {
            if (Parent == null) return;
            var group = new List<FToggle>();
            foreach (Control c in Parent.Controls)
            {
                var t = c as FToggle;
                if (t != null && t.isRadio && t.groupName == groupName && t.Enabled && t.Visible) group.Add(t);
            }
            group.Sort((x, y) => x.TabIndex != y.TabIndex ? x.TabIndex.CompareTo(y.TabIndex) : Parent.Controls.GetChildIndex(y).CompareTo(Parent.Controls.GetChildIndex(x)));
            int i = group.IndexOf(this);
            if (i < 0 || group.Count < 2) return;
            var next = group[(i + step + group.Count) % group.Count];
            next.Focus();
            next.CheckState = CheckState.Checked;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); over = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); over = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; if (CanFocus) Focus(); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); if (AutoSize) { var p = Parent; if (p != null) p.PerformLayout(this, "Text"); } }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Invalidate(); }

        int TextLeft { get { return UI.S(BoxSize) + UI.S(10); } }

        int AvailableTextWidth(Size proposed)
        {
            int w = proposed.Width > 0 && proposed.Width < 10000 ? proposed.Width : 10000;
            if (MaximumSize.Width > 0) w = Math.Min(w, MaximumSize.Width);
            return Math.Max(20, w - TextLeft - UI.S(4));
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int avail = AvailableTextWidth(proposedSize);
            string text = Text ?? "";
            Size one = TextRenderer.MeasureText(text, Font, new Size(10000, 10000), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            Size t = one.Width <= avail ? one : TextRenderer.MeasureText(text, Font, new Size(avail, 10000), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
            int w = TextLeft + Math.Min(t.Width, avail) + UI.S(4);
            return new Size(w, Math.Max(UI.S(26), t.Height + UI.S(8)));
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : UI.Back);
        }

        protected virtual Color Rest { get { return UI.Blend(UI.Back, UI.Text, 0.55f); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : UI.Back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int bs = UI.S(BoxSize);
            string text = Text ?? "";
            int tx = TextLeft;
            int avail = Math.Max(10, Width - tx);
            Size one = TextRenderer.MeasureText(g, text, Font, new Size(10000, 10000), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            bool wrap = one.Width > avail;
            float boxY;
            Rectangle tr;
            TextFormatFlags f;
            if (wrap)
            {
                int top = UI.S(4);
                boxY = top + (one.Height - bs) / 2f + 0.5f;
                tr = new Rectangle(tx, top, avail, Math.Max(10, Height - top));
                f = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
            }
            else
            {
                boxY = (Height - bs) / 2f + 0.5f;
                tr = new Rectangle(tx, 0, avail, Height);
                f = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            }
            var box = new RectangleF(0.5f, boxY, bs - 1f, bs - 1f);
            if (isRadio) DrawRadio(g, box); else DrawCheck(g, box);
            if (Focused && ShowFocusCues)
            {
                var fr = RectangleF.Inflate(box, 3f, 3f);
                if (isRadio) using (var p = new Pen(UI.Text, 1.4f)) g.DrawEllipse(p, fr);
                else using (var path = Fx.Round(fr, UI.S(6))) using (var p = new Pen(UI.Text, 1.4f)) g.DrawPath(p, path);
            }
            Color fore = Enabled ? ForeColor : UI.Blend(UI.Back, UI.Text, 0.4f);
            TextRenderer.DrawText(g, text, Font, tr, fore, f);
        }

        void DrawCheck(Graphics g, RectangleF box)
        {
            bool on = state != CheckState.Unchecked;
            Color target = UI.Dark ? Color.White : Color.Black;
            Color fill = on
                ? (Enabled ? UI.Blend(UI.Accent, target, down ? 0.18f : over ? 0.08f : 0f) : UI.Blend(UI.Back, UI.Text, 0.2f))
                : (Enabled ? (down ? UI.CtrlDown : over ? UI.CtrlHover : UI.Field) : UI.Blend(UI.Back, UI.Field, 0.5f));
            using (var path = Fx.Round(box, UI.S(4)))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (!on) using (var p = new Pen(Enabled ? Rest : UI.Border)) g.DrawPath(p, path);
            }
            if (!on) return;
            Color mark = Enabled ? UI.OnAccent : UI.Blend(UI.Back, UI.Text, 0.55f);
            using (var p = new Pen(mark, Math.Max(1.6f, UI.S(2) * 0.9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (state == CheckState.Indeterminate)
                    g.DrawLine(p, box.X + box.Width * 0.28f, box.Y + box.Height / 2f, box.X + box.Width * 0.72f, box.Y + box.Height / 2f);
                else
                    g.DrawLines(p, new[]
                    {
                        new PointF(box.X + box.Width * 0.26f, box.Y + box.Height * 0.54f),
                        new PointF(box.X + box.Width * 0.43f, box.Y + box.Height * 0.70f),
                        new PointF(box.X + box.Width * 0.75f, box.Y + box.Height * 0.32f)
                    });
            }
        }

        void DrawRadio(Graphics g, RectangleF box)
        {
            Color target = UI.Dark ? Color.White : Color.Black;
            if (Checked)
            {
                Color fill = Enabled ? UI.Blend(UI.Accent, target, down ? 0.18f : over ? 0.08f : 0f) : UI.Blend(UI.Back, UI.Text, 0.2f);
                using (var b = new SolidBrush(fill)) g.FillEllipse(b, box);
                float d = box.Width * (down ? 0.34f : over ? 0.46f : 0.40f);
                using (var b = new SolidBrush(Enabled ? UI.OnAccent : UI.Blend(UI.Back, UI.Text, 0.55f)))
                    g.FillEllipse(b, box.X + (box.Width - d) / 2f, box.Y + (box.Height - d) / 2f, d, d);
            }
            else
            {
                using (var b = new SolidBrush(Enabled ? (down ? UI.CtrlDown : over ? UI.CtrlHover : UI.Field) : UI.Blend(UI.Back, UI.Field, 0.5f))) g.FillEllipse(b, box);
                using (var p = new Pen(Enabled ? Rest : UI.Border)) g.DrawEllipse(p, box);
            }
        }
    }

    class FCheck : FToggle
    {
        public FCheck() { IsRadio = false; }
    }

    class FRadio : FToggle
    {
        public FRadio() { IsRadio = true; }
    }

    class FluentColors : ProfessionalColorTable
    {
        public FluentColors() { UseSystemColors = false; }
        public override Color ToolStripDropDownBackground { get { return UI.Card; } }
        public override Color ImageMarginGradientBegin { get { return UI.Card; } }
        public override Color ImageMarginGradientMiddle { get { return UI.Card; } }
        public override Color ImageMarginGradientEnd { get { return UI.Card; } }
        public override Color MenuBorder { get { return UI.Border; } }
        public override Color MenuItemBorder { get { return UI.ListHover; } }
        public override Color MenuItemSelected { get { return UI.ListHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return UI.ListHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return UI.ListHover; } }
        public override Color MenuItemPressedGradientBegin { get { return UI.ListSelected; } }
        public override Color MenuItemPressedGradientEnd { get { return UI.ListSelected; } }
        public override Color SeparatorDark { get { return UI.Border; } }
        public override Color SeparatorLight { get { return UI.Card; } }
    }

    class FluentMenuRenderer : ToolStripProfessionalRenderer
    {
        public FluentMenuRenderer() : base(new FluentColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? UI.Text : UI.Blend(UI.Card, UI.Text, 0.45f);
            base.OnRenderItemText(e);
        }
    }

    class FProgress : Control
    {
        readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer { Interval = 16 };
        float pos = -0.35f;

        public FProgress()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 8;
            tick.Tick += (s, e) =>
            {
                pos += 0.011f + Math.Abs(pos - 0.35f) * 0.012f;
                if (pos > 1.05f) pos = -0.35f;
                Invalidate();
            };
            tick.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : UI.Back);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int th = UI.S(4);
            var track = new RectangleF(0, (Height - th) / 2f, Width, th);
            using (var path = Fx.Round(track, th / 2f))
            {
                using (var b = new SolidBrush(UI.Blend(UI.Back, UI.Text, 0.14f))) g.FillPath(b, path);
                g.SetClip(path);
                float w = Width * 0.35f;
                using (var b = new SolidBrush(UI.Accent)) g.FillRectangle(b, pos * Width, track.Y, w, th);
                g.ResetClip();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tick.Dispose();
            base.Dispose(disposing);
        }
    }

    class NavRail : Control
    {
        public class Item
        {
            public string Text, Glyph;
            public bool Page;
            public bool Divider;
        }

        public readonly List<Item> Items = new List<Item>();
        public string Heading = "";
        public string Footer = "";
        int selected = -1, hot = -1;
        public event Action<int> ItemClicked;

        public NavRail()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                int d = e.KeyCode == Keys.Down ? 1 : -1;
                int i = hot >= 0 ? hot : (selected >= 0 ? selected : (d > 0 ? -1 : Items.Count));
                do { i += d; } while (i >= 0 && i < Items.Count && Items[i].Divider);
                if (i >= 0 && i < Items.Count) { hot = i; Invalidate(); }
                e.Handled = true;
            }
            else if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) && hot >= 0 && ItemClicked != null)
            {
                ItemClicked(hot);
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        public int Selected
        {
            get { return selected; }
            set { selected = value; Invalidate(); }
        }

        int Row { get { return UI.S(40); } }
        int Top0 { get { return UI.S(64); } }

        Rectangle RectOf(int i)
        {
            int y = Top0;
            for (int k = 0; k < i; k++) y += Items[k].Divider ? UI.S(14) : Row;
            return new Rectangle(UI.S(8), y, Width - UI.S(16), Items[i].Divider ? UI.S(14) : Row - UI.S(4));
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < Items.Count; i++) if (!Items[i].Divider && RectOf(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h != hot) { hot = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int h = HitTest(e.Location);
            if (h >= 0 && ItemClicked != null) ItemClicked(h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(UI.Back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            TextRenderer.DrawText(g, Heading, UI.Head, new Rectangle(UI.S(20), UI.S(14), Width - UI.S(28), UI.S(36)), UI.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                var r = RectOf(i);
                if (it.Divider)
                {
                    using (var p = new Pen(UI.Border)) g.DrawLine(p, r.X + UI.S(8), r.Y + r.Height / 2, r.Right - UI.S(8), r.Y + r.Height / 2);
                    continue;
                }
                bool sel = it.Page && i == selected;
                if (sel || i == hot)
                {
                    using (var path = Fx.Round(r, UI.S(4))) using (var b = new SolidBrush(sel ? UI.ListSelected : UI.ListHover)) g.FillPath(b, path);
                }
                if (sel)
                {
                    int bh = UI.S(16);
                    using (var path = Fx.Round(new RectangleF(r.X + 1, r.Y + (r.Height - bh) / 2f, UI.S(3), bh), 1.5f)) using (var b = new SolidBrush(UI.Accent)) g.FillPath(b, path);
                }
                int tx = r.X + UI.S(14);
                if (Fx.GlyphOk(it.Glyph))
                {
                    Fx.DrawGlyph(g, it.Glyph, 12f, sel ? UI.Accent : UI.Text, new RectangleF(r.X + UI.S(10), r.Y, UI.S(26), r.Height));
                    tx = r.X + UI.S(42);
                }
                TextRenderer.DrawText(g, it.Text, sel ? UI.Bold : UI.Font, new Rectangle(tx, r.Y, r.Right - tx - UI.S(6), r.Height), UI.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            if (Focused && hot >= 0 && hot < Items.Count && !Items[hot].Divider)
                using (var path = Fx.Round(RectOf(hot), UI.S(4))) using (var p = new Pen(UI.Text, 1.6f)) g.DrawPath(p, path);
            TextRenderer.DrawText(g, Footer, UI.Small, new Rectangle(UI.S(20), Height - UI.S(34), Width - UI.S(28), UI.S(24)), UI.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    class HeaderRow
    {
        public string Name;
    }

    class RowDeco
    {
        public Func<ListViewItem, int> Check;
        public Action<ListViewItem> OnCheck;
        public Func<ListViewItem, int> Pin;
        public Action<ListViewItem> OnPin;
    }

    static class FList
    {
        static readonly ConditionalWeakTable<ListView, object> done = new ConditionalWeakTable<ListView, object>();
        static readonly ConditionalWeakTable<ListView, RowDeco> decos = new ConditionalWeakTable<ListView, RowDeco>();

        public static void Decorate(ListView lv, RowDeco d)
        {
            RowDeco old;
            if (decos.TryGetValue(lv, out old)) decos.Remove(lv);
            decos.Add(lv, d);
        }

        static RowDeco DecoOf(ListView lv)
        {
            RowDeco d;
            return decos.TryGetValue(lv, out d) ? d : null;
        }

        static Rectangle RowRect(ListView lv, ListViewItem item)
        {
            Rectangle row = item.GetBounds(ItemBoundsPortion.Entire);
            return new Rectangle(row.X, row.Y, Math.Max(row.Width, lv.ClientSize.Width), row.Height);
        }

        static RectangleF CheckRect(Rectangle row)
        {
            int s = UI.S(16);
            return new RectangleF(row.X + UI.S(16), row.Y + (row.Height - s) / 2f, s, s);
        }

        static Rectangle PinRect(Rectangle row)
        {
            return new Rectangle(row.Right - UI.S(40), row.Y, UI.S(32), row.Height);
        }

        public static bool IsHeader(ListViewItem it) { return it != null && it.Tag is HeaderRow; }

        public static ListViewItem Header(string text)
        {
            return new ListViewItem(text) { Tag = new HeaderRow { Name = text } };
        }

        public static void Attach(ListView lv, int rowHeight, Func<ListViewItem, string> glyphOf)
        {
            Attach(lv, rowHeight, glyphOf, null);
        }

        public static void Attach(ListView lv, int rowHeight, Func<ListViewItem, string> glyphOf, Func<ListViewItem, string> secondOf)
        {
            object seen;
            if (done.TryGetValue(lv, out seen)) return;
            done.Add(lv, new object());
            lv.OwnerDraw = true;
            lv.SmallImageList = new ImageList { ImageSize = new Size(1, UI.S(rowHeight)) };
            int hot = -1;
            ListViewItem last = null;
            bool fixing = false;
            lv.MouseMove += (s, e) =>
            {
                var it = lv.GetItemAt(e.X, e.Y);
                int i = it == null || IsHeader(it) ? -1 : it.Index;
                if (i == hot) return;
                int old = hot; hot = i;
                Redraw(lv, old); Redraw(lv, i);
            };
            lv.MouseLeave += (s, e) => { int old = hot; hot = -1; Redraw(lv, old); };
            lv.Resize += (s, e) => Stretch(lv);
            lv.HandleCreated += (s, e) => Stretch(lv);
            lv.DrawColumnHeader += (s, e) => ColumnHead(lv, e);
            lv.DrawItem += (s, e) => { if (lv.View != View.Details) e.DrawDefault = true; };
            lv.DrawSubItem += (s, e) => Cell(lv, e, hot == e.ItemIndex, glyphOf, secondOf);
            lv.MouseDown += (s, e) =>
            {
                var deco = DecoOf(lv);
                if (deco == null || e.Button != MouseButtons.Left) return;
                var it = lv.GetItemAt(Math.Min(e.X, Math.Max(0, lv.ClientSize.Width - 2)), e.Y);
                if (it == null) return;
                var row = RowRect(lv, it);
                if (deco.Check != null && deco.OnCheck != null && deco.Check(it) >= 0)
                {
                    var cr = RectangleF.Inflate(CheckRect(row), UI.S(6), UI.S(6));
                    if (cr.Contains(e.X, e.Y)) { deco.OnCheck(it); return; }
                }
                if (deco.Pin != null && deco.OnPin != null && deco.Pin(it) >= 0 && PinRect(row).Contains(e.X, e.Y)) deco.OnPin(it);
            };
            lv.ItemSelectionChanged += (s, e) =>
            {
                if (fixing) return;
                if (!e.IsSelected) return;
                if (!IsHeader(e.Item)) { last = e.Item; return; }
                fixing = true;
                try
                {
                    int from = e.Item.Index;
                    int dir = last != null && last.ListView == lv && last.Index > from ? -1 : 1;
                    e.Item.Selected = false;
                    var target = NextRow(lv, from, dir) ?? NextRow(lv, from, -dir);
                    if (target != null) { target.Selected = true; target.Focused = true; target.EnsureVisible(); last = target; }
                }
                finally { fixing = false; }
            };
            lv.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Up && e.KeyCode != Keys.Down) return;
                if (lv.Items.Count == 0) return;
                int cur = lv.SelectedIndices.Count == 0 ? -1 : lv.SelectedIndices[0];
                int dir = e.KeyCode == Keys.Down ? 1 : -1;
                var target = NextRow(lv, cur < 0 ? (dir > 0 ? -1 : lv.Items.Count) : cur, dir);
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (target == null) return;
                fixing = true;
                try
                {
                    foreach (ListViewItem x in lv.SelectedItems) x.Selected = false;
                    target.Selected = true; target.Focused = true; target.EnsureVisible(); last = target;
                }
                finally { fixing = false; }
            };
        }

        static void DrawStar(Graphics g, Rectangle box, bool on, bool header, int top)
        {
            string star = on ? "\u2605" : "\u2606";
            Color c = on ? UI.Accent : UI.Muted;
            TextRenderer.DrawText(g, star, UI.Font, new Rectangle(box.X, top - UI.S(3), box.Width, UI.S(24)), c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        public static ListViewItem NextRow(ListView lv, int from, int dir)
        {
            for (int i = from + dir; i >= 0 && i < lv.Items.Count; i += dir)
                if (!IsHeader(lv.Items[i])) return lv.Items[i];
            return null;
        }

        public static ListViewItem FirstRow(ListView lv)
        {
            return NextRow(lv, -1, 1);
        }

        static void Stretch(ListView lv)
        {
            if (lv.View != View.Details || lv.Columns.Count == 0 || lv.ClientSize.Width < 50) return;
            int sum = 0;
            foreach (ColumnHeader h in lv.Columns) sum += h.Width;
            int diff = lv.ClientSize.Width - sum;
            var last = lv.Columns[lv.Columns.Count - 1];
            if (diff > 0) last.Width += diff;
            else if (diff >= -UI.S(60) && last.Width + diff >= UI.S(60)) last.Width += diff;
        }

        static void Redraw(ListView lv, int i)
        {
            if (i >= 0 && i < lv.Items.Count) lv.RedrawItems(i, i, false);
        }

        static void ColumnHead(ListView lv, DrawListViewColumnHeaderEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(lv.BackColor)) g.FillRectangle(b, e.Bounds);
            using (var p = new Pen(UI.Border)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            var tr = new Rectangle(e.Bounds.X + UI.S(12), e.Bounds.Y, Math.Max(4, e.Bounds.Width - UI.S(16)), e.Bounds.Height);
            TextRenderer.DrawText(g, e.Header.Text, UI.Small, tr, UI.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        static void Cell(ListView lv, DrawListViewSubItemEventArgs e, bool hot, Func<ListViewItem, string> glyphOf, Func<ListViewItem, string> secondOf)
        {
            var g = e.Graphics;
            var item = e.Item;
            bool sel = item.Selected;
            Rectangle row = item.GetBounds(ItemBoundsPortion.Entire);
            int rowW = Math.Max(row.Width, lv.ClientSize.Width);
            if (IsHeader(item))
            {
                if (e.ColumnIndex == 0)
                {
                    using (var b = new SolidBrush(lv.BackColor)) g.FillRectangle(b, row.X, row.Y, rowW, row.Height);
                    using (var p = new Pen(UI.Border)) g.DrawLine(p, row.X + UI.S(8), row.Bottom - 1, row.X + rowW - UI.S(8), row.Bottom - 1);
                    int hl = UI.S(16);
                    var hd = DecoOf(lv);
                    Rectangle rr = new Rectangle(row.X, row.Y, rowW, row.Height);
                    if (hd != null && hd.Check != null && hd.Check(item) >= 0)
                    {
                        var cb = CheckRect(rr);
                        cb.Y = rr.Bottom - UI.S(6) - cb.Height;
                        Fx.DrawCheckBox(g, cb, hd.Check(item), false);
                        hl = UI.S(42);
                    }
                    if (hd != null && hd.Pin != null && hd.Pin(item) >= 0) DrawStar(g, PinRect(rr), hd.Pin(item) > 0, true, rr.Bottom - UI.S(6) - UI.S(16));
                    var hr = new Rectangle(e.Bounds.X + hl, row.Y, Math.Max(4, e.Bounds.Width - hl - UI.S(44)), row.Height - UI.S(2));
                    TextRenderer.DrawText(g, item.Text, UI.SmallBold, hr, UI.Muted, TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                }
                return;
            }
            if (e.ColumnIndex == 0)
            {
                using (var b = new SolidBrush(lv.BackColor)) g.FillRectangle(b, row.X, row.Y, rowW, row.Height);
                if (sel || hot)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    var pill = new RectangleF(row.X + UI.S(4), row.Y + UI.S(2), rowW - UI.S(8), row.Height - UI.S(4));
                    using (var path = Fx.Round(pill, UI.S(4))) using (var b = new SolidBrush(sel ? UI.ListSelected : UI.ListHover)) g.FillPath(b, path);
                    if (sel)
                    {
                        int bh = Math.Min(UI.S(16), row.Height - UI.S(10));
                        using (var path = Fx.Round(new RectangleF(pill.X + 1, pill.Y + (pill.Height - bh) / 2f, UI.S(3), bh), 1.5f)) using (var b = new SolidBrush(UI.Accent)) g.FillPath(b, path);
                    }
                    g.SmoothingMode = SmoothingMode.Default;
                }
            }
            int left = e.ColumnIndex == 0 ? UI.S(16) : UI.S(8);
            int rightPad = UI.S(6);
            var deco = e.ColumnIndex == 0 ? DecoOf(lv) : null;
            if (deco != null)
            {
                var rr = new Rectangle(row.X, row.Y, rowW, row.Height);
                int cs = deco.Check == null ? -1 : deco.Check(item);
                if (cs >= 0) { Fx.DrawCheckBox(g, CheckRect(rr), cs, false); left = UI.S(42); }
                int ps = deco.Pin == null ? -1 : deco.Pin(item);
                if (ps >= 0)
                {
                    rightPad = UI.S(44);
                    if (ps > 0 || sel || hot) DrawStar(g, PinRect(rr), ps > 0, false, rr.Y + (rr.Height - UI.S(16)) / 2);
                }
            }
            if (e.ColumnIndex == 0 && glyphOf != null)
            {
                string gl = glyphOf(item);
                if (Fx.GlyphOk(gl))
                {
                    Fx.DrawGlyph(g, gl, 11f, UI.Muted, new RectangleF(e.Bounds.X + UI.S(12), e.Bounds.Y, UI.S(22), e.Bounds.Height));
                    left = UI.S(44);
                }
            }
            Color fore = item.UseItemStyleForSubItems ? item.ForeColor : e.SubItem.ForeColor;
            if (fore.IsEmpty || fore == SystemColors.WindowText) fore = UI.Text;
            var tr = new Rectangle(e.Bounds.X + left, e.Bounds.Y, Math.Max(4, e.Bounds.Width - left - rightPad), e.Bounds.Height);
            string second = e.ColumnIndex == 0 && secondOf != null ? secondOf(item) : null;
            if (!string.IsNullOrEmpty(second))
            {
                int half = e.Bounds.Height / 2;
                var top = new Rectangle(tr.X, tr.Y + UI.S(1), tr.Width, half);
                var bottom = new Rectangle(tr.X, tr.Y + half - UI.S(1), tr.Width, e.Bounds.Height - half);
                TextRenderer.DrawText(g, e.SubItem.Text, item.Font, top, fore, TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(g, second, UI.Small, bottom, UI.Muted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                return;
            }
            TextRenderer.DrawText(g, e.SubItem.Text, item.Font, tr, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }
}
