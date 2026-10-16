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

namespace PSTools
{
    class PinChoice : Form
    {
        public enum Result { Keep, UsePreset, Cancel }

        Result answer = Result.Cancel;

        PinChoice()
        {
            Text = "PS Tools"; Font = UI.Font; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            int w = UI.S(420);
            var table = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(UI.S(24), UI.S(20), UI.S(24), UI.S(20)), Dock = DockStyle.Fill };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = "Keep your pinned actions?", Font = UI.Head, AutoSize = true, MaximumSize = new Size(w, 0), Margin = new Padding(0, 0, 0, UI.S(8)) }, 0, 0);
            table.Controls.Add(new Label
            {
                Text = "Keep pins leaves the actions you pinned at the top of the menu and adds them to the preset. Use preset pins replaces them with the preset's own pins.",
                ForeColor = UI.Muted, AutoSize = true, MaximumSize = new Size(w, 0), Margin = new Padding(0, 0, 0, UI.S(14))
            }, 0, 1);
            var keep = new FButton { Text = "Keep pins", Width = UI.S(120), Height = UI.S(34), Margin = new Padding(0, 0, UI.S(10), 0) };
            UI.Primary(keep);
            keep.Click += (s, e) => { answer = Result.Keep; Close(); };
            var use = new FButton { Text = "Use preset pins", Width = UI.S(140), Height = UI.S(34), Margin = new Padding(0, 0, UI.S(10), 0) };
            use.Click += (s, e) => { answer = Result.UsePreset; Close(); };
            var cancel = new FButton { Text = "Cancel", Width = UI.S(100), Height = UI.S(34), Margin = new Padding(0) };
            cancel.Click += (s, e) => { answer = Result.Cancel; Close(); };
            var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
            row.Controls.Add(keep); row.Controls.Add(use); row.Controls.Add(cancel);
            table.Controls.Add(row, 0, 2);
            Controls.Add(table);
            AcceptButton = keep; CancelButton = cancel;
            UI.Apply(this, false);
        }

        public static Result Ask(IWin32Window owner)
        {
            using (var d = new PinChoice())
            {
                d.ShowDialog(owner);
                return d.answer;
            }
        }
    }
}
