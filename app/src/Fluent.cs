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

    abstract class FToggleBase : CheckBox
    {
        protected bool over, down;
        protected const int BoxSize = 18;

        protected FToggleBase()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Font = UI.Font;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); over = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); over = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
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
            int textW = proposedSize.Width > 0 && proposedSize.Width < 10000
                ? Math.Max(20, proposedSize.Width - UI.S(BoxSize + 12))
                : 10000;
            Size t = TextRenderer.MeasureText(Text ?? "", Font, new Size(textW, 10000), TextFormatFlags.NoPrefix | (textW < 10000 ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine));
            return new Size(t.Width + UI.S(BoxSize + 14), Math.Max(UI.S(26), t.Height + UI.S(6)));
        }

        protected abstract void DrawBox(Graphics g, RectangleF box);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int bs = UI.S(BoxSize);
            var box = new RectangleF(0.5f, (Height - bs) / 2f + 0.5f, bs - 1f, bs - 1f);
            if (Height > UI.S(40) && !AutoSize) box.Y = UI.S(3) + 0.5f;
            DrawBox(g, box);
            if (Focused && ShowFocusCues)
            {
                var fr = RectangleF.Inflate(box, 3f, 3f);
                using (var path = Fx.Round(fr, UI.S(6))) using (var p = new Pen(UI.Text, 1.4f)) g.DrawPath(p, path);
            }
            int tx = bs + UI.S(10);
            var tr = new Rectangle(tx, 0, Math.Max(10, Width - tx), Height);
            Color fore = Enabled ? ForeColor : UI.Blend(UI.Back, UI.Text, 0.4f);
            TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.NoPrefix | (AutoSize ? TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine : TextFormatFlags.WordBreak);
            if (Height > UI.S(40) && !AutoSize) tr = new Rectangle(tx, UI.S(2), tr.Width, Height - UI.S(2));
            TextRenderer.DrawText(g, Text ?? "", Font, tr, fore, f);
        }

        protected Color Rest { get { return UI.Blend(UI.Back, UI.Text, 0.55f); } }
    }

    class FCheck : FToggleBase
    {
        protected override void DrawBox(Graphics g, RectangleF box)
        {
            bool on = CheckState != CheckState.Unchecked;
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
                if (CheckState == CheckState.Indeterminate)
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
    }

    class FRadio : RadioButton
    {
        bool over, down;

        public FRadio()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Font = UI.Font;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); over = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); over = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
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
            Size t = TextRenderer.MeasureText(Text ?? "", Font, new Size(10000, 10000), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            return new Size(t.Width + UI.S(32), Math.Max(UI.S(26), t.Height + UI.S(6)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int bs = UI.S(18);
            var box = new RectangleF(0.5f, (Height - bs) / 2f + 0.5f, bs - 1f, bs - 1f);
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
                using (var p = new Pen(Enabled ? UI.Blend(UI.Back, UI.Text, 0.55f) : UI.Border)) g.DrawEllipse(p, box);
            }
            if (Focused && ShowFocusCues)
            {
                var fr = RectangleF.Inflate(box, 3f, 3f);
                using (var p = new Pen(UI.Text, 1.4f)) g.DrawEllipse(p, fr);
            }
            int tx = bs + UI.S(10);
            Color fore = Enabled ? ForeColor : UI.Blend(UI.Back, UI.Text, 0.4f);
            TextRenderer.DrawText(g, Text ?? "", Font, new Rectangle(tx, 0, Math.Max(10, Width - tx), Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
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

    static class FList
    {
        static readonly ConditionalWeakTable<ListView, object> done = new ConditionalWeakTable<ListView, object>();

        public static void Attach(ListView lv, int rowHeight, Func<ListViewItem, string> glyphOf)
        {
            object seen;
            if (done.TryGetValue(lv, out seen)) return;
            done.Add(lv, new object());
            lv.OwnerDraw = true;
            lv.SmallImageList = new ImageList { ImageSize = new Size(1, UI.S(rowHeight)) };
            int hot = -1;
            lv.MouseMove += (s, e) =>
            {
                var it = lv.GetItemAt(e.X, e.Y);
                int i = it == null ? -1 : it.Index;
                if (i == hot) return;
                int old = hot; hot = i;
                Redraw(lv, old); Redraw(lv, i);
            };
            lv.MouseLeave += (s, e) => { int old = hot; hot = -1; Redraw(lv, old); };
            lv.Resize += (s, e) => Stretch(lv);
            lv.HandleCreated += (s, e) => Stretch(lv);
            lv.DrawColumnHeader += (s, e) => Header(lv, e);
            lv.DrawItem += (s, e) => { if (lv.View != View.Details) e.DrawDefault = true; };
            lv.DrawSubItem += (s, e) => Cell(lv, e, hot == e.ItemIndex, glyphOf);
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

        static void Header(ListView lv, DrawListViewColumnHeaderEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(lv.BackColor)) g.FillRectangle(b, e.Bounds);
            using (var p = new Pen(UI.Border)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            var tr = new Rectangle(e.Bounds.X + UI.S(12), e.Bounds.Y, Math.Max(4, e.Bounds.Width - UI.S(16)), e.Bounds.Height);
            TextRenderer.DrawText(g, e.Header.Text, UI.Small, tr, UI.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        static void Cell(ListView lv, DrawListViewSubItemEventArgs e, bool hot, Func<ListViewItem, string> glyphOf)
        {
            var g = e.Graphics;
            var item = e.Item;
            bool sel = item.Selected;
            Rectangle row = item.GetBounds(ItemBoundsPortion.Entire);
            int rowW = Math.Max(row.Width, lv.ClientSize.Width);
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
            var tr = new Rectangle(e.Bounds.X + left, e.Bounds.Y, Math.Max(4, e.Bounds.Width - left - UI.S(6)), e.Bounds.Height);
            TextRenderer.DrawText(g, e.SubItem.Text, item.Font, tr, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }
}
