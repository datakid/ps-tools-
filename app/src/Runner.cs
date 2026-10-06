// Runs an action on a background thread with a small progress window, then shows the result.
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace PSTools
{
    static class Runner
    {
        public static void RunWithUI(Action a, string path, IWin32Window owner)
        {
            if (!System.IO.Directory.Exists(path)) { Program.Msg("Folder not found:\n" + path); return; }

            // Actions that are dialogs or instant, not background jobs.
            switch (a.Id)
            {
                case "rename": ShowForm(new RenameForm(path), owner); return;
                case "copy-names":
                    string names = Actions.NamesText(path);
                    if (names.Length == 0) { Toast(owner, "Folder is empty."); return; }
                    Clipboard.SetText(names); Toast(owner, "File names copied."); return;
                case "copy-tree": Clipboard.SetText(Actions.TreeText(path)); Toast(owner, "Folder tree copied."); return;
                case "library": ShowForm(new LibraryForm(path), owner); return;
            }

            if (a.Confirm != null && !Program.Ask(string.Format(a.Confirm, path))) return;

            Result result = null; Exception error = null;
            var ctx = new Ctx();
            using (var dlg = new ProgressForm(a.Label, ctx))
            {
                ctx.Report = s => { try { dlg.BeginInvoke((MethodInvoker)(() => dlg.Status = s)); } catch { } };
                var t = new Thread(() =>
                {
                    try { result = a.Run(path, ctx); }
                    catch (Exception ex) { error = ex; }
                    try { dlg.BeginInvoke((MethodInvoker)dlg.Close); } catch { }
                });
                t.IsBackground = true;
                dlg.Shown += (s, e) => t.Start();
                dlg.ShowDialog(owner);
                t.Join();
            }

            if (error is OperationCanceledException) { Program.Msg("Cancelled. Changes made so far can be reverted with Undo last."); return; }
            if (error != null) { MessageBox.Show(error.Message, "PS Tools - error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (result.Rows != null && result.Rows.Count > 0) ShowForm(new ResultsForm(a.Label + "  -  " + path, result), owner);
            else Program.Msg(result.Summary);
        }

        static void ShowForm(Form f, IWin32Window owner)
        {
            if (owner == null) Application.Run(f);   // launched from Explorer: this is the only window
            else f.Show(owner);
        }

        static void Toast(IWin32Window owner, string text)
        {
            if (owner == null) Program.Msg(text);
            else { var m = owner as MainForm; if (m != null) m.SetStatus(text); }
        }
    }

    class ProgressForm : Form
    {
        readonly Label status = new Label { Dock = DockStyle.Top, Height = 28, Text = "Working...", AutoEllipsis = true };
        public string Status { set { status.Text = value; } }

        public ProgressForm(string title, Ctx ctx)
        {
            Text = title; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(380, 96); Font = UI.Font;
            Padding = new Padding(14); ShowInTaskbar = true;
            var bar = new ProgressBar { Dock = DockStyle.Top, Height = 14, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25 };
            var cancel = new Button { Text = "Cancel", Width = 88, Height = 28, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Location = new Point(278, 58) };
            cancel.Click += (s, e) => { ctx.Cancel = true; cancel.Enabled = false; status.Text = "Cancelling..."; };
            Controls.Add(cancel); Controls.Add(bar); Controls.Add(status);
            FormClosing += (s, e) => { ctx.Cancel = true; };
        }
    }

    static class UI
    {
        public static readonly Font Font = new Font("Segoe UI", 9f);
        public static readonly Font Bold = new Font("Segoe UI Semibold", 9f);
        public static readonly Font Head = new Font("Segoe UI Semibold", 11f);
        public static readonly Color Accent = Color.FromArgb(0, 103, 192);
        public static readonly Color Muted = Color.FromArgb(96, 96, 96);
        public static readonly Color Panel = Color.FromArgb(243, 243, 243);
    }
}
