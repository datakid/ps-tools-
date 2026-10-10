using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace PSTools
{
    static class Runner
    {
        static string ClipText()
        {
            try
            {
                if (!Clipboard.ContainsText()) return "";
                string t = Clipboard.GetText();
                return t.Length > 5000 ? t.Substring(0, 5000) : t;
            }
            catch { return ""; }
        }

        public static void Execute(Action a, string[] paths, IWin32Window owner)
        {
            foreach (var p in paths)
                if (!Directory.Exists(p) && !File.Exists(p)) { Program.Msg("Not found:\n" + p); return; }

            if (a.Window != null) { ShowForm(a.Window(paths), owner); return; }

            string what = Actions.Describe(paths);

            if (a.Script != null)
            {
                string script;
                try { script = a.Script(paths); }
                catch (Exception ex) { Program.Msg(ex.Message); return; }
                if (!Program.Ask(a.Label + "\n\nThis runs in an elevated PowerShell window (Windows will ask for permission):\n\n" + script.Trim())) return;
                try { Launch.Console(script, true, false); }
                catch (Win32Exception) { }
                catch (Exception ex) { Program.Msg(ex.Message); }
                return;
            }

            if (a.Confirm != null && !Program.Ask(string.Format(a.Confirm, what))) return;

            Result result = null; Exception error = null;
            var ctx = new Ctx { ClipboardText = ClipText() };

            if (a.Fast)
            {
                try { result = a.Run(paths, ctx); }
                catch (Exception ex) { error = ex; }
            }
            else
            {
                using (var dlg = new ProgressForm(a.Label, ctx))
                {
                    ctx.Report = s => { try { dlg.BeginInvoke((MethodInvoker)(() => dlg.Status = s)); } catch { } };
                    var t = new Thread(() =>
                    {
                        try { result = a.Run(paths, ctx); }
                        catch (Exception ex) { error = ex; }
                        try { dlg.BeginInvoke((MethodInvoker)dlg.Close); } catch { }
                    });
                    t.IsBackground = true;
                    dlg.Shown += (s, e) => t.Start();
                    dlg.ShowDialog(owner);
                    t.Join();
                }
            }

            Finish(a, paths, result, error, owner);
        }

        static void Finish(Action a, string[] paths, Result result, Exception error, IWin32Window owner)
        {
            if (error is OperationCanceledException) { Program.Msg("Cancelled. Changes made so far can be reverted with Undo last."); return; }
            if (error != null) { MessageBox.Show(error.Message, "PS Tools - error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (result == null) return;

            if (result.ClipText != null)
            {
                Clip.Set(result.ClipText);
                Notify(owner, result.Summary);
                return;
            }
            if (result.Rows != null && result.Rows.Count > 0)
            {
                ShowForm(new ResultsForm(a.Label + "  -  " + Actions.Describe(paths), result), owner);
                return;
            }
            string s = result.Summary ?? "";
            if (a.Fast && s.IndexOf('\n') < 0 && s.Length < 160) Notify(owner, s);
            else Program.Msg(s);
        }

        public static void Notify(IWin32Window owner, string text)
        {
            var m = owner as MainForm;
            if (m != null) m.SetStatus(text);
            else Toast.Run(text);
        }

        public static void ShowForm(Form f, IWin32Window owner)
        {
            UI.Apply(f);
            if (owner == null) Application.Run(f);
            else f.Show(owner);
        }
    }

    class ProgressForm : Form
    {
        readonly Label status = new Label { Dock = DockStyle.Top, Height = 28, Text = "Working...", AutoEllipsis = true };
        public string Status { set { status.Text = value; } }

        public ProgressForm(string title, Ctx ctx)
        {
            Text = title; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(400, 100); Font = UI.Font;
            Padding = new Padding(14); ShowInTaskbar = true;
            var bar = new ProgressBar { Dock = DockStyle.Top, Height = 14, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25 };
            var cancel = new Button { Text = "Cancel", Width = 88, Height = 28, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Location = new Point(298, 62) };
            cancel.Click += (s, e) => { ctx.Cancel = true; cancel.Enabled = false; status.Text = "Cancelling..."; };
            Controls.Add(cancel); Controls.Add(bar); Controls.Add(status);
            FormClosing += (s, e) => { ctx.Cancel = true; };
            UI.Apply(this);
        }
    }
}
