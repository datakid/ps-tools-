using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PSTools
{
    class FDialog : Form
    {
        public enum Kind { Info, Question, Error }

        readonly Kind kind;

        FDialog(string text, string title, Kind k, bool cancel)
        {
            kind = k;
            Text = title; Font = UI.Font; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            const int textW = 380, maxText = 300;
            int h = UI.MeasureHeight(text, UI.Font, textW) + 6;
            int shown = Math.Min(h, maxText);

            var icon = new Control { Location = new Point(26, 28), Size = new Size(40, 40) };
            icon.Paint += (s, e) => PaintIcon(e.Graphics);
            Controls.Add(icon);

            var label = new Label { Text = text, Location = new Point(0, 0), Size = new Size(textW, h), ForeColor = UI.Text };
            if (h > maxText)
            {
                var scroll = new Panel { Location = new Point(86, 26), Size = new Size(textW + 20, shown), AutoScroll = true };
                scroll.Controls.Add(label);
                Controls.Add(scroll);
            }
            else
            {
                label.Location = new Point(86, 26);
                Controls.Add(label);
            }

            int y = 26 + Math.Max(44, shown) + 26;
            var ok = new FButton { Text = "OK", Width = 112, Height = 34, DialogResult = DialogResult.OK };
            UI.Primary(ok);
            ok.Location = new Point(480 - 26 - 112, y);
            Controls.Add(ok);
            AcceptButton = ok;
            if (cancel)
            {
                var no = new FButton { Text = "Cancel", Width = 112, Height = 34, DialogResult = DialogResult.Cancel };
                no.Location = new Point(480 - 26 - 112 - 10 - 112, y);
                Controls.Add(no);
                CancelButton = no;
            }
            else CancelButton = ok;
            ClientSize = new Size(480, y + 34 + 24);
            UI.Apply(this);
            label.ForeColor = UI.Text;
        }

        void PaintIcon(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Color fill = kind == Kind.Error ? UI.Danger : UI.Accent;
            using (var b = new SolidBrush(fill)) g.FillEllipse(b, 1, 1, 37, 37);
            string mark = kind == Kind.Error ? "!" : kind == Kind.Question ? "?" : "i";
            using (var f = new Font("Segoe UI Semibold", 16f))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(UI.OnAccent))
                g.DrawString(mark, f, b, new RectangleF(1, 1, 38, 38), fmt);
        }

        public static bool Show(string text, string title, Kind kind, bool cancel)
        {
            IWin32Window owner = null;
            try
            {
                Form f = Form.ActiveForm;
                if (f != null && f.Visible && !f.InvokeRequired) owner = f;
            }
            catch { }
            using (var d = new FDialog(text, title, kind, cancel))
            {
                d.StartPosition = owner != null ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
                return d.ShowDialog(owner) == DialogResult.OK;
            }
        }
    }
}
