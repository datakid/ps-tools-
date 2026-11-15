using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class V4Card : Panel
    {
        public string Title = "";
        public bool ShowTitle = true;
        readonly Label caption = new Label { AutoSize = false };

        public V4Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public static int HeaderHeight { get { return UI.S(48); } }

        public Rectangle Inner
        {
            get
            {
                int top = ShowTitle ? HeaderHeight : Tok.S3;
                return new Rectangle(Tok.S3, top, Math.Max(10, Width - Tok.S3 * 2), Math.Max(10, Height - top - Tok.S3));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent == null ? Tok.Window : Parent.BackColor);
            var frame = new Rectangle(0, 0, Width - 1, Height - 1);
            FGfx.FillRound(g, frame, Tok.RadiusLarge, Tok.Surface);
            FGfx.StrokeRound(g, frame, Tok.RadiusLarge, Tok.Stroke);
            if (ShowTitle) FGfx.Text(g, Title, Tok.Semibold, new Rectangle(Tok.S3, Tok.S2, Width - Tok.S3 * 2 - UI.S(140), UI.S(32)), Tok.Text, FGfx.Line);
        }
    }

    class RowCard : V4Card
    {
        public readonly FTreeList List = new FTreeList { ClickActivates = false };
        readonly Label empty = new Label { AutoSize = false };
        public int MaxRows = 6;
        public string EmptyText = "";
        public event Action<PmRow> RowActivated;

        public RowCard(string title, string emptyText)
        {
            Title = title;
            EmptyText = emptyText;
            List.AccessibleName = title;
            AccessibleName = title;
            empty.Font = Tok.Caption;
            empty.AccessibleName = title + " empty";
            List.Activated += (s, e) => { var r = List.SelectedRow; if (r != null && RowActivated != null) RowActivated(r); };
            List.TabStop = true;
            Controls.Add(List);
            Controls.Add(empty);
            empty.ForeColor = Tok.TextSecondary;
        }

        public void SetRows(List<PmRow> rows)
        {
            List.SetRows(rows, rows.Count > 0 ? 0 : -1);
            empty.Text = rows.Count == 0 ? EmptyText : "";
            empty.Visible = rows.Count == 0;
            List.Visible = rows.Count > 0;
            Height = Needed;
            Relayout();
        }

        public int Needed
        {
            get
            {
                int n = Math.Min(MaxRows, Math.Max(List.Rows.Count, 0));
                int body = List.Rows.Count == 0 ? UI.S(40) : n * Tok.RowHeight;
                return HeaderHeight + body + Tok.S3;
            }
        }

        public void Relayout()
        {
            var r = Inner;
            int n = Math.Min(MaxRows, List.Rows.Count);
            List.SetBounds(Tok.S1, HeaderHeight, Math.Max(10, Width - Tok.S1 * 2), Math.Max(1, n * Tok.RowHeight));
            empty.SetBounds(Tok.S3, HeaderHeight, Math.Max(10, Width - Tok.S3 * 2), UI.S(28));
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }
    }
}
