using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PSTools
{
    class RunForm : Form
    {
        readonly FSearchBox dirBox = new FSearchBox("Working folder") { ShowGlyph = false };
        readonly FButton browse = new FButton { Text = "Browse..." };
        readonly FSegment shell = new FSegment();
        readonly FSearchBox editor = new FSearchBox("Type a command, for example Get-ChildItem -Recurse -Filter *.py") { ShowGlyph = false };
        readonly FButton run = new FButton { Text = "Run  Ctrl+Enter", Kind = FButton.Look.Accent };
        readonly FButton stop = new FButton { Text = "Stop", Visible = false };
        readonly FButton save = new FButton { Text = "Save as action..." };
        readonly FButton history = new FButton { Text = "History  Ctrl+R" };
        readonly FButton terminal = new FButton { Text = "Open in terminal" };
        readonly FInfoBar info = new FInfoBar();
        readonly FButton copyAll = new FButton { Text = "Copy all" };
        readonly FButton copyCmd = new FButton { Text = "Copy command + output" };
        readonly FButton asTable = new FButton { Text = "Show as table" };
        readonly FButton wrap = new FButton { Text = "Wrap" };
        readonly FButton find = new FButton { Text = "Find" };
        readonly FButton clear = new FButton { Text = "Clear" };
        readonly Label status = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, AccessibleName = "Run status" };
        readonly FSearchBox findBox = new FSearchBox("Find in output") { Visible = false };
        readonly FConsole output = new FConsole();
        readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer { Interval = 200 };
        readonly List<string> lines = new List<string>();
        readonly StringBuilder captured = new StringBuilder();
        readonly Form completeForm = new Form();
        readonly FTreeList completeList = new FTreeList();
        RunSession session;
        string lastCommand = "";
        int historyIndex = -1;
        bool tableMode;
        bool pendingApproval;
        string startContext = "background";

        public bool Running { get { return session != null && !session.Finished; } }

        public string OutputText { get { return output.Text; } }

        public RunForm(string dir, string initialCommand, string contextName)
        {
            startContext = contextName ?? "background";
            Text = "PS Tools Run console";
            AccessibleName = "PS Tools Run console";
            Font = UI.Font;
            ClientSize = new Size(UI.S(900), UI.S(620));
            MinimumSize = new Size(UI.S(700), UI.S(480));
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            dirBox.Input.Text = dir ?? Environment.CurrentDirectory;
            dirBox.AccessibleName = "Working folder";
            dirBox.Input.AccessibleName = "Working folder";
            shell.Items.AddRange(new[] { "PowerShell 5.1", "PowerShell 7", "cmd" });
            shell.AccessibleName = "Shell";
            if (RunEngine.FindPwsh() == null) shell.Disabled.Add(1);
            shell.SelectedIndex = RunHistory.LastShell == "cmd" ? 2 : RunHistory.LastShell == "pwsh" && RunEngine.FindPwsh() != null ? 1 : 0;
            shell.SelectedChanged += (s, e) => { RunHistory.LastShell = ShellKey(); RunHistory.Save(); UpdateButtons(); };

            editor.Input.Multiline = true;
            editor.Input.AcceptsReturn = true;
            editor.Input.Font = Tok.Mono;
            editor.Input.WordWrap = true;
            editor.Input.ScrollBars = ScrollBars.None;
            editor.Input.AccessibleName = "Command editor";
            editor.AccessibleName = "Command editor";
            editor.Input.Text = initialCommand ?? "";
            editor.Input.TextChanged += (s, e) => { UpdateButtons(); LayoutAll(); ShowComplete(); };
            editor.Input.KeyDown += OnEditorKeyDown;
            editor.Input.LostFocus += (s, e) => { if (!completeForm.ContainsFocus) completeForm.Hide(); };

            run.AccessibleName = "Run";
            stop.AccessibleName = "Stop";
            run.Click += (s, e) => StartRun();
            stop.Click += (s, e) => StopRun();
            save.Click += (s, e) => OpenSave();
            history.Click += (s, e) => OpenHistory();
            terminal.Click += (s, e) => OpenTerminal();
            browse.Click += (s, e) => PickFolder();
            copyAll.Click += (s, e) => { if (output.TextLength > 0) Clip.Set(output.Text); };
            copyCmd.Click += (s, e) => Clip.Set(lastCommand + "\n\n" + output.Text);
            asTable.Click += (s, e) => RunAsTable();
            wrap.Click += (s, e) => { output.WordWrap = !output.WordWrap; };
            find.Click += (s, e) => ToggleFind();
            clear.Click += (s, e) => output.ClearAll();
            findBox.Input.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { FindNext(); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Escape) { ToggleFind(); e.SuppressKeyPress = true; } };
            status.Font = Tok.Semibold;
            ticker.Tick += (s, e) => { if (Running && session.LooksIdle(10) && !info.Visible) ShowIdleBanner(); };
            clock.Tick += (s, e) => { if (Running) SetStatus("Running " + (session.Watch.ElapsedMilliseconds / 1000.0).ToString("0.0") + " s", Tok.TextSecondary); };

            completeForm.FormBorderStyle = FormBorderStyle.None;
            completeForm.ShowInTaskbar = false;
            completeForm.StartPosition = FormStartPosition.Manual;
            completeForm.TopMost = true;
            completeForm.Controls.Add(completeList);
            completeList.Dock = DockStyle.Fill;
            completeList.Activated += (s, e) => AcceptComplete();

            foreach (Control c in new Control[] { dirBox, browse, shell, editor, run, stop, save, history, terminal, info, copyAll, copyCmd, asTable, wrap, find, clear, status, findBox, output })
                Controls.Add(c);

            UI.Apply(this, false);
            BackColor = Tok.Window;
            dirBox.RefreshTheme();
            editor.RefreshTheme();
            findBox.RefreshTheme();
            output.RefreshTheme();
            Resize += (s, e) => LayoutAll();
            Shown += (s, e) => { LayoutAll(); editor.Input.Focus(); editor.Input.SelectionStart = editor.Input.TextLength; ticker.Start(); clock.Start(); };
            FormClosing += (s, e) => { if (Running) session.Stop(); completeForm.Dispose(); };
            UpdateButtons();
            LayoutAll();
        }

        string ShellKey() { return shell.SelectedIndex == 2 ? "cmd" : shell.SelectedIndex == 1 ? "pwsh" : "ps"; }

        ActionShell CurrentShell { get { return shell.SelectedIndex == 2 ? ActionShell.Cmd : shell.SelectedIndex == 1 ? ActionShell.Pwsh : ActionShell.PowerShell; } }

        public string Command { get { return editor.Input.Text; } set { editor.Input.Text = value; } }

        public string WorkDir { get { return dirBox.Input.Text.Trim(); } }

        public void SetShell(int index) { shell.SelectedIndex = index; }

        void UpdateButtons()
        {
            bool hasText = editor.Input.Text.Trim().Length > 0;
            run.Enabled = hasText && !Running;
            save.Enabled = hasText;
            asTable.Enabled = hasText && !Running && CurrentShell != ActionShell.Cmd;
            stop.Visible = Running;
            run.Visible = !Running;
        }

        void SetStatus(string text, Color c)
        {
            status.Text = text;
            status.ForeColor = c;
            status.AccessibleName = "Run status";
            status.AccessibleDescription = text;
        }

        void LayoutAll()
        {
            if (ClientSize.Width < 10) return;
            int pad = Tok.S3, w = ClientSize.Width - pad * 2, y = pad;
            int segW = UI.S(330);
            browse.SetBounds(ClientSize.Width - pad - segW - Tok.S2 - browse.GetPreferredSize(Size.Empty).Width, y, browse.GetPreferredSize(Size.Empty).Width, Tok.ButtonHeight);
            shell.SetBounds(ClientSize.Width - pad - segW, y, segW, Tok.ButtonHeight);
            dirBox.SetBounds(pad, y, Math.Max(100, browse.Left - Tok.S2 - pad), Tok.InputHeight);
            y += Tok.ButtonHeight + Tok.S2;
            int lineCount = Math.Max(3, Math.Min(10, editor.Input.GetLineFromCharIndex(Math.Max(0, editor.Input.TextLength - 1)) + 1));
            int editorH = lineCount * UI.S(19) + UI.S(18);
            editor.SetBounds(pad, y, w, editorH);
            y += editorH + Tok.S2;
            int x = pad;
            foreach (var b in new[] { run, stop, save, history, terminal })
            {
                if (!b.Visible && b != stop && b != run) continue;
                var size = b.GetPreferredSize(Size.Empty);
                if (b == stop && !b.Visible) continue;
                if (b == run && !b.Visible) continue;
                b.SetBounds(x, y, size.Width, Tok.ButtonHeight);
                x += size.Width + Tok.S2;
            }
            if (!run.Visible && stop.Visible) { var s0 = stop.GetPreferredSize(Size.Empty); stop.SetBounds(pad, y, s0.Width, Tok.ButtonHeight); }
            y += Tok.ButtonHeight + Tok.S2;
            if (info.Visible) { info.SetBounds(pad, y, w, UI.S(44)); y += UI.S(44) + Tok.S2; }
            int tx = pad;
            foreach (var b in new[] { copyAll, copyCmd, asTable, wrap, find, clear })
            {
                var size = b.GetPreferredSize(Size.Empty);
                b.SetBounds(tx, y, size.Width, UI.S(28));
                b.Height = UI.S(28);
                tx += size.Width + Tok.S1;
            }
            status.SetBounds(tx + Tok.S2, y, Math.Max(40, ClientSize.Width - pad - tx - Tok.S2), UI.S(28));
            y += UI.S(28) + Tok.S1;
            if (findBox.Visible) { findBox.SetBounds(pad, y, UI.S(300), Tok.InputHeight); y += Tok.InputHeight + Tok.S1; }
            output.SetBounds(pad, y, w, Math.Max(40, ClientSize.Height - y - pad));
        }

        void PickFolder()
        {
            using (var d = new FolderBrowserDialog { SelectedPath = WorkDir })
                if (d.ShowDialog(this) == DialogResult.OK) dirBox.Input.Text = d.SelectedPath;
        }

        void OnEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (completeForm.Visible)
            {
                if (e.KeyCode == Keys.Tab || (e.KeyCode == Keys.Enter && !e.Control)) { AcceptComplete(); e.SuppressKeyPress = true; return; }
                if (e.KeyCode == Keys.Down) { completeList.Move(1); e.SuppressKeyPress = true; return; }
                if (e.KeyCode == Keys.Up) { completeList.Move(-1); e.SuppressKeyPress = true; return; }
                if (e.KeyCode == Keys.Escape) { completeForm.Hide(); e.SuppressKeyPress = true; return; }
            }
            if (e.KeyCode == Keys.Enter && e.Control) { StartRun(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape && Running) { StopRun(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.R && e.Control) { OpenHistory(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F && e.Control) { ToggleFind(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Up && !e.Control && editor.Input.GetLineFromCharIndex(editor.Input.SelectionStart) == 0) { BrowseHistory(1); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Down && !e.Control && editor.Input.GetLineFromCharIndex(editor.Input.SelectionStart) >= editor.Input.GetLineFromCharIndex(Math.Max(0, editor.Input.TextLength - 1)) && historyIndex >= 0) { BrowseHistory(-1); e.SuppressKeyPress = true; }
        }

        void BrowseHistory(int dir)
        {
            var all = RunHistory.All;
            if (all.Count == 0) return;
            historyIndex = Math.Max(-1, Math.Min(all.Count - 1, historyIndex + dir));
            if (historyIndex >= 0) { editor.Input.Text = all[historyIndex].Command; editor.Input.SelectionStart = editor.Input.TextLength; }
            else editor.Input.Text = "";
        }

        void ShowComplete()
        {
            if (!editor.Input.Focused) return;
            var list = RunComplete.Suggest(editor.Input.Text, editor.Input.SelectionStart, WorkDir);
            if (list.Count == 0) { completeForm.Hide(); return; }
            completeList.SetRows(list.Select(t => new PmRow { Title = t }), 0);
            int caret = editor.Input.SelectionStart;
            var pt = editor.Input.GetPositionFromCharIndex(Math.Max(0, Math.Min(editor.Input.TextLength, RunComplete.TokenStart(editor.Input.Text, caret))));
            var screen = editor.Input.PointToScreen(new Point(pt.X, pt.Y + UI.S(22)));
            completeForm.Size = new Size(UI.S(320), Math.Min(8, list.Count) * Tok.RowHeight + 2);
            completeForm.Location = screen;
            completeForm.BackColor = Tok.Stroke;
            completeForm.Padding = new Padding(1);
            if (!completeForm.Visible) { completeForm.Show(this); editor.Input.Focus(); }
        }

        void AcceptComplete()
        {
            var row = completeList.SelectedRow;
            if (row == null) return;
            string text = editor.Input.Text;
            int caret = editor.Input.SelectionStart;
            int start = RunComplete.TokenStart(text, caret);
            editor.Input.Text = text.Substring(0, start) + row.Title + text.Substring(caret);
            editor.Input.SelectionStart = start + row.Title.Length;
            completeForm.Hide();
            editor.Input.Focus();
        }

        void ToggleFind()
        {
            findBox.Visible = !findBox.Visible;
            LayoutAll();
            if (findBox.Visible) findBox.Input.Focus(); else editor.Input.Focus();
        }

        void FindNext()
        {
            string t = findBox.Input.Text;
            if (t.Length == 0) return;
            int from = output.SelectionStart + output.SelectionLength;
            int i = output.Find(t, from < output.TextLength ? from : 0, RichTextBoxFinds.None);
            if (i < 0) i = output.Find(t, 0, RichTextBoxFinds.None);
        }

        public void StartRun()
        {
            if (Running) return;
            string script = editor.Input.Text;
            if (script.Trim().Length == 0) return;
            string dir = WorkDir;
            if (dir.Length > 0 && !Directory.Exists(dir)) { info.Show("The working folder does not exist: " + dir, true); LayoutAll(); return; }
            var risk = RiskScan.Classify(script, CurrentShell);
            string approvalKey = ShellKey() + "\n" + script;
            if (risk.Risk == Risk.Destructive)
            {
                string msg = "This command can destroy data:\n\n" + script.Trim() + "\n\n" + string.Join("\n", risk.Reasons.ToArray()) + "\n\nWorking folder: " + dir;
                if (!Program.Ask(msg)) { SetStatus("Not run", Tok.TextSecondary); return; }
            }
            else if (risk.Risk == Risk.Modifies && !RunHistory.Approved.Contains(approvalKey))
            {
                string verbs = string.Join(", ", risk.Verbs.ToArray());
                pendingApproval = true;
                info.Show("This command changes files: " + verbs + ".", true,
                    new KeyValuePair<string, System.Action>("Run", () => { RunHistory.Approved.Add(approvalKey); pendingApproval = false; info.Hide2(); LayoutAll(); Begin(script, dir, false); }),
                    new KeyValuePair<string, System.Action>("Run once", () => { pendingApproval = false; info.Hide2(); LayoutAll(); Begin(script, dir, false); }),
                    new KeyValuePair<string, System.Action>("Cancel", () => { pendingApproval = false; info.Hide2(); LayoutAll(); SetStatus("Not run", Tok.TextSecondary); }));
                LayoutAll();
                return;
            }
            Begin(script, dir, false);
        }

        void Begin(string script, string dir, bool table)
        {
            tableMode = table;
            lastCommand = script;
            historyIndex = -1;
            captured.Length = 0;
            if (!table) { output.ClearAll(); info.Hide2(); }
            var req = new RunRequest { Shell = CurrentShell, Script = script, Dir = dir, AsTable = table };
            SetStatus("Running...", Tok.TextSecondary);
            UpdateButtons();
            LayoutAll();
            string shellKey = ShellKey();
            session = RunEngine.Start(req,
                (line, err) => BeginInvoke(new System.Action(() => OnLine(line, err))),
                (code, ms, stopped) => BeginInvoke(new System.Action(() => OnExit(script, shellKey, dir, code, ms, stopped))));
            UpdateButtons();
            LayoutAll();
        }

        void OnLine(string line, bool err)
        {
            if (tableMode) { captured.AppendLine(line); return; }
            output.AppendLine(line, err);
        }

        void OnExit(string script, string shellKey, string dir, int code, long ms, bool stopped)
        {
            string time = (ms / 1000.0).ToString("0.00") + " s";
            if (tableMode)
            {
                string text;
                var table = RunEngine.ParseTable(captured.ToString(), out text);
                if (table != null) Executor.ShowForm(new ResultsForm("Result of: " + (script.Length > 60 ? script.Substring(0, 57) + "..." : script), table));
                else output.AppendLine(text ?? "", false);
            }
            if (stopped) SetStatus("Stopped", Tok.RiskModifies);
            else if (code == 0) SetStatus("\u25CF Exit 0 \u00B7 " + time, UI.Dark ? Color.FromArgb(108, 203, 95) : Color.FromArgb(15, 123, 15));
            else SetStatus("\u25CF Exit " + code + " \u00B7 " + time, Tok.RiskDestructive);
            if (!tableMode) RunHistory.Add(script, shellKey, dir, code);
            info.Hide2();
            session.Finished = true;
            UpdateButtons();
            LayoutAll();
        }

        public void StopRun()
        {
            if (Running) session.Stop();
        }

        void RunAsTable()
        {
            if (Running) return;
            string script = editor.Input.Text;
            if (script.Trim().Length == 0 || CurrentShell == ActionShell.Cmd) return;
            Begin(script, WorkDir, true);
        }

        void ShowIdleBanner()
        {
            info.Show("This command may be waiting for input.", true,
                new KeyValuePair<string, System.Action>("Open in a real terminal", () => { StopRun(); OpenTerminal(); }));
            LayoutAll();
        }

        void OpenTerminal()
        {
            string command = editor.Input.Text;
            if (command.Trim().Length > 0) Clip.Set(command);
            string dir = Directory.Exists(WorkDir) ? WorkDir : Environment.CurrentDirectory;
            try
            {
                string wt = Fs.Which("wt");
                if (wt != null) Process.Start(new ProcessStartInfo(wt, "-d \"" + dir + "\"") { UseShellExecute = false });
                else Process.Start(new ProcessStartInfo(CurrentShell == ActionShell.Cmd ? "cmd.exe" : "powershell.exe", CurrentShell == ActionShell.Cmd ? "/k" : "-NoExit") { WorkingDirectory = dir, UseShellExecute = false });
                info.Show("The command is on the clipboard. Paste it in the terminal.", false);
                LayoutAll();
            }
            catch (Exception ex) { Program.Fail("Could not open a terminal: " + ex.Message); }
        }

        void OpenHistory()
        {
            using (var picker = new HistoryPicker())
                if (picker.ShowDialog(this) == DialogResult.OK && picker.Picked != null)
                {
                    editor.Input.Text = picker.Picked;
                    editor.Input.Focus();
                    editor.Input.SelectionStart = editor.Input.TextLength;
                }
        }

        void OpenSave()
        {
            string dir = WorkDir;
            using (var f = new SaveActionForm(editor.Input.Text.Trim(), CurrentShell, dir, startContext))
                f.ShowDialog(this);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && Running) { StopRun(); return true; }
            if (keyData == (Keys.Control | Keys.Enter)) { StartRun(); return true; }
            if (keyData == (Keys.Control | Keys.S) && editor.Input.Text.Trim().Length > 0) { OpenSave(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    class HistoryPicker : Form
    {
        readonly FSearchBox box = new FSearchBox("Search history");
        readonly FTreeList list = new FTreeList();
        readonly FButton import = new FButton { Text = "Import PowerShell history..." };
        readonly FButton close = new FButton { Text = "Close" };
        List<string> source = new List<string>();
        public string Picked;

        public HistoryPicker()
        {
            Text = "Run history";
            AccessibleName = "Run history";
            Font = UI.Font;
            ClientSize = new Size(UI.S(620), UI.S(480));
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            source = RunHistory.All.Select(e => e.Command).ToList();
            box.Input.TextChanged += (s, e) => Fill();
            box.Input.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down) { list.Move(1); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Up) { list.Move(-1); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { Pick(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); e.SuppressKeyPress = true; }
            };
            list.Activated += (s, e) => Pick();
            import.Click += (s, e) =>
            {
                var imported = RunHistory.ImportPsReadLine(null);
                if (imported.Count == 0) { Program.Msg("No PowerShell history files were found in your PSReadLine folder."); return; }
                source = imported;
                Fill();
            };
            close.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.AddRange(new Control[] { box, list, import, close });
            UI.Apply(this, false);
            BackColor = Tok.Window;
            box.RefreshTheme();
            Resize += (s, e) => Place();
            Shown += (s, e) => { Place(); box.Input.Focus(); };
            Fill();
            Place();
        }

        void Place()
        {
            int pad = Tok.S3;
            box.SetBounds(pad, pad, ClientSize.Width - pad * 2, Tok.InputHeight);
            var iz = import.GetPreferredSize(Size.Empty);
            var cz = close.GetPreferredSize(Size.Empty);
            close.SetBounds(ClientSize.Width - pad - cz.Width, ClientSize.Height - pad - Tok.ButtonHeight, cz.Width, Tok.ButtonHeight);
            import.SetBounds(pad, ClientSize.Height - pad - Tok.ButtonHeight, iz.Width, Tok.ButtonHeight);
            list.SetBounds(pad, pad * 2 + Tok.InputHeight, ClientSize.Width - pad * 2, ClientSize.Height - pad * 4 - Tok.InputHeight - Tok.ButtonHeight);
        }

        void Fill()
        {
            string q = box.Input.Text.Trim().ToLowerInvariant();
            var rows = source.Where(c => q.Length == 0 || c.ToLowerInvariant().Contains(q)).Take(300)
                .Select(c => new PmRow { Title = c.Replace("\r", " ").Replace("\n", " "), Secondary = "", Node = null, Glyph = "\uE81C" }).ToList();
            list.SetRows(rows, 0);
            filtered = source.Where(c => q.Length == 0 || c.ToLowerInvariant().Contains(q)).Take(300).ToList();
        }

        List<string> filtered = new List<string>();

        void Pick()
        {
            int i = list.Selected;
            if (i < 0 || i >= filtered.Count) return;
            Picked = filtered[i];
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
