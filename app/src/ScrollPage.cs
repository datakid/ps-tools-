using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PSTools
{
    class ScrollThumb : Control
    {
        public int Offset, Content;

        public ScrollThumb()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Tag = "overlay";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Tok.Window : Parent.BackColor);
            if (Content <= Height) return;
            int track = Height - Tok.S2 * 2;
            int thumb = Math.Max(UI.S(32), track * Height / Content);
            int top = Tok.S2 + (track - thumb) * Offset / Math.Max(1, Content - Height);
            FGfx.FillRound(e.Graphics, new Rectangle(Width - UI.S(5), top, UI.S(3), thumb), 1, Tok.StrokeStrong);
        }
    }

    class ScrollPage : Panel, IMessageFilter
    {
        protected readonly Panel canvas = new Panel();
        readonly ScrollThumb thumb = new ScrollThumb();
        readonly HashSet<Control> hooked = new HashSet<Control>();
        int offset, content;
        bool filtering;

        public ScrollPage()
        {
            Controls.Add(canvas);
            Controls.Add(thumb);
            thumb.BringToFront();
            canvas.ControlAdded += (s, e) => Hook(e.Control);
        }

        public static int ThumbWidth { get { return UI.S(12); } }

        protected int ContentWidth { get { return Math.Max(UI.S(160), ClientSize.Width - ThumbWidth); } }

        public int ScrollOffset { get { return offset; } }

        protected void SetContentHeight(int total)
        {
            content = total;
            Clamp();
            Place();
        }

        void Clamp()
        {
            int max = Math.Max(0, content - Height);
            if (offset > max) offset = max;
            if (offset < 0) offset = 0;
        }

        void Place()
        {
            canvas.Location = new Point(0, -offset);
            thumb.SetBounds(Width - ThumbWidth, 0, ThumbWidth, Height);
            thumb.Offset = offset;
            thumb.Content = content;
            thumb.Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Clamp();
            Place();
        }

        void Hook(Control c)
        {
            if (!hooked.Add(c)) return;
            c.Enter += (s, e) => ScrollIntoView(c);
            c.ControlAdded += (s, e) => Hook(e.Control);
            foreach (Control child in c.Controls) Hook(child);
        }

        public void ScrollIntoView(Control c)
        {
            var r = RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
            if (r.Top < 0) offset += r.Top - Tok.S2;
            else if (r.Bottom > Height) offset += r.Bottom - Height + Tok.S2;
            Clamp();
            Place();
        }

        public void ScrollBy(int pixels)
        {
            offset += pixels;
            Clamp();
            Place();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            bool want = Visible && IsHandleCreated;
            if (want && !filtering) { Application.AddMessageFilter(this); filtering = true; }
            else if (!want && filtering) { Application.RemoveMessageFilter(this); filtering = false; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            OnVisibleChanged(EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (filtering) { Application.RemoveMessageFilter(this); filtering = false; }
            base.Dispose(disposing);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != 0x020A || !Visible || content <= Height) return false;
            var screen = Cursor.Position;
            if (!RectangleToScreen(ClientRectangle).Contains(screen)) return false;
            var form = FindForm();
            if (form == null || form != Form.ActiveForm) return false;
            var under = Control.FromHandle(m.HWnd);
            if (under is RichTextBox) return false;
            int delta = (short)(((long)m.WParam >> 16) & 0xffff);
            ScrollBy(-Math.Sign(delta) * UI.S(72));
            return true;
        }
    }
}
