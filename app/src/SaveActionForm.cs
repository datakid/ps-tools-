using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PSTools
{
    class SaveActionForm : Form
    {
        readonly FSearchBox title = new FSearchBox("Title") { ShowGlyph = false };
        readonly ComboBox menu = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
        readonly FToggle forFile = new FToggle { Text = "A file" };
        readonly FToggle forFolder = new FToggle { Text = "A folder" };
        readonly FToggle forBackground = new FToggle { Text = "Inside a folder (empty space)" };
        readonly FToggle forDrive = new FToggle { Text = "A drive" };
        readonly FSearchBox extensions = new FSearchBox("For example png, jpg (optional)") { ShowGlyph = false };
        readonly ComboBox result = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly ComboBox risk = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly FSearchBox command = new FSearchBox("") { ShowGlyph = false };
        readonly FInfoBar suggestion = new FInfoBar();
        readonly Label preview = new Label { AutoSize = false, Font = Tok.Mono };
        readonly FConsole testOutput = new FConsole();
        readonly FToggle favorite = new FToggle { Text = "Add to the Explorer right-click menu" };
        readonly FButton test = new FButton { Text = "Test run" };
        readonly FButton cancel = new FButton { Text = "Cancel" };
        readonly FButton saveButton = new FButton { Text = "Save", Kind = FButton.Look.Accent };
        readonly List<Label> labels = new List<Label>();
        readonly ActionShell shell;
        readonly string dir;
        string replacementFrom;

        public string SavedPath;
        public string SavedId;

        static readonly string[] ResultNames = { "Show output", "Copy output to the clipboard", "Show as a table", "Just run" };
        static readonly ActionOutput[] ResultValues = { ActionOutput.Console, ActionOutput.Clipboard, ActionOutput.Grid, ActionOutput.Toast };

        public SaveActionForm(string commandText, ActionShell shellKind, string workDir, string contextName)
        {
            shell = shellKind;
            dir = workDir ?? "";
            Text = "Save as action";
            AccessibleName = "Save as action";
            Font = UI.Font;
            ClientSize = new Size(UI.S(640), UI.S(600));
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;

            string first = (commandText ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            title.Input.Text = first.Length > 32 ? first.Substring(0, 32).TrimEnd() : first;
            title.AccessibleName = "Action title";
            title.Input.AccessibleName = "Action title";

            var menus = new List<string> { MenuTree.MyActions };
            foreach (var s in ActionCatalog.Current.All.Where(x => x.Source != SpecSource.Library && x.Menu.Length > 0).Select(x => x.Menu).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                if (!menus.Contains(s)) menus.Add(s);
            menu.Items.AddRange(menus.ToArray());
            menu.Text = MenuTree.MyActions;
            menu.AccessibleName = "Menu location";

            forFile.Checked = contextName == "file";
            forFolder.Checked = contextName == "folder";
            forBackground.Checked = true;
            forDrive.Checked = contextName == "drive";

            result.Items.AddRange(ResultNames);
            result.SelectedIndex = 0;
            result.AccessibleName = "Result";
            var auto = RiskScan.Classify(commandText ?? "", shell);
            risk.Items.AddRange(new[] { "Safe", "Changes files", "Destructive" });
            risk.SelectedIndex = auto.Risk == Risk.Safe ? 0 : auto.Risk == Risk.Modifies ? 1 : 2;
            risk.AccessibleName = "Risk";

            command.Input.Multiline = true;
            command.Input.AcceptsReturn = true;
            command.Input.Font = Tok.Mono;
            command.Input.WordWrap = true;
            command.Input.Text = commandText ?? "";
            command.Input.TextChanged += (s, e) => { RefreshSuggestion(); RefreshPreview(); };
            command.Input.AccessibleName = "Command";
            command.AccessibleName = "Command";
            preview.AccessibleName = "Preview";
            testOutput.Font = Tok.Mono;
            testOutput.WordWrap = true;
            testOutput.AccessibleName = "Test output";
            favorite.Checked = false;

            test.Click += (s, e) => TestRun();
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            saveButton.Click += (s, e) => Save();
            title.Input.TextChanged += (s, e) => saveButton.Enabled = title.Input.Text.Trim().Length > 0 && command.Input.Text.Trim().Length > 0;

            foreach (var t in new[] { "Title", "Menu location", "Shows for", "Only for extensions", "Result", "Risk", "Command" })
                labels.Add(new Label { Text = t, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = Tok.Body });
            foreach (var l in labels) Controls.Add(l);
            foreach (Control c in new Control[] { title, menu, forFile, forFolder, forBackground, forDrive, extensions, result, risk, command, suggestion, preview, testOutput, favorite, test, cancel, saveButton })
                Controls.Add(c);

            UI.Apply(this, false);
            BackColor = Tok.Window;
            foreach (var b in new[] { title, extensions, command }) b.RefreshTheme();
            testOutput.RefreshTheme();
            preview.ForeColor = Tok.TextSecondary;
            Resize += (s, e) => Place();
            Shown += (s, e) => { Place(); title.Input.Focus(); title.Input.SelectAll(); };
            RefreshSuggestion();
            RefreshPreview();
            saveButton.Enabled = title.Input.Text.Trim().Length > 0 && command.Input.Text.Trim().Length > 0;
            Place();
        }

        public void SetTitle(string text) { title.Input.Text = text; }

        public void SetMenu(string text) { menu.Text = text; }

        public void SetResult(int index) { result.SelectedIndex = index; }

        public void SetContexts(bool file, bool folder, bool background, bool drive)
        {
            forFile.Checked = file; forFolder.Checked = folder; forBackground.Checked = background; forDrive.Checked = drive;
        }

        void Place()
        {
            int pad = Tok.S4, labelW = UI.S(140), x = pad + labelW, w = ClientSize.Width - x - pad, y = pad;
            int row = UI.S(40);
            Action<int, Control, int> put = (i, c, h) => { labels[i].SetBounds(pad, y, labelW, h); c.SetBounds(x, y, w, h); };
            put(0, title, Tok.InputHeight); y += row;
            put(1, menu, Tok.InputHeight); y += row;
            labels[2].SetBounds(pad, y, labelW, UI.S(28));
            int half = (w - Tok.S2) / 2;
            forFile.SetBounds(x, y, half, UI.S(28)); forFolder.SetBounds(x + half + Tok.S2, y, half, UI.S(28)); y += UI.S(32);
            forBackground.SetBounds(x, y, half, UI.S(28)); forDrive.SetBounds(x + half + Tok.S2, y, half, UI.S(28)); y += UI.S(40);
            put(3, extensions, Tok.InputHeight); y += row;
            put(4, result, Tok.InputHeight); y += row;
            put(5, risk, Tok.InputHeight); y += row;
            labels[6].SetBounds(pad, y, labelW, UI.S(28));
            command.SetBounds(x, y, w, UI.S(96)); y += UI.S(100);
            suggestion.SetBounds(x, y, w, suggestion.Visible ? UI.S(44) : 0); y += suggestion.Visible ? UI.S(48) : 0;
            preview.SetBounds(x, y, w, UI.S(52)); y += UI.S(56);
            int bottom = ClientSize.Height - pad - Tok.ButtonHeight;
            favorite.SetBounds(x, bottom - UI.S(34), w, UI.S(28));
            testOutput.SetBounds(x, y, w, Math.Max(30, bottom - UI.S(40) - y));
            var save = saveButton.GetPreferredSize(Size.Empty); var can = cancel.GetPreferredSize(Size.Empty); var tst = test.GetPreferredSize(Size.Empty);
            saveButton.SetBounds(ClientSize.Width - pad - save.Width, bottom, save.Width, Tok.ButtonHeight);
            cancel.SetBounds(saveButton.Left - Tok.S2 - can.Width, bottom, can.Width, Tok.ButtonHeight);
            test.SetBounds(cancel.Left - Tok.S2 - tst.Width, bottom, tst.Width, Tok.ButtonHeight);
        }

        public static string Generalize(string commandText, string workDir, out string from)
        {
            from = null;
            if (string.IsNullOrEmpty(workDir) || string.IsNullOrEmpty(commandText)) return commandText;
            string trimmed = workDir.TrimEnd('\\');
            if (trimmed.Length < 3) return commandText;
            foreach (string pattern in new[] { "'" + trimmed + "'", "\"" + trimmed + "\"", trimmed })
            {
                if (commandText.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    from = trimmed;
                    return Replace(commandText, pattern, "{dir}");
                }
            }
            return commandText;
        }

        static string Replace(string text, string find, string with)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (true)
            {
                int j = text.IndexOf(find, i, StringComparison.OrdinalIgnoreCase);
                if (j < 0) { sb.Append(text.Substring(i)); break; }
                sb.Append(text.Substring(i, j - i)).Append(with);
                i = j + find.Length;
            }
            return sb.ToString();
        }

        void RefreshSuggestion()
        {
            string from;
            Generalize(command.Input.Text, dir, out from);
            replacementFrom = from;
            if (from != null)
            {
                suggestion.Show("Replace " + from + " with {dir} so it works anywhere?", false,
                    new KeyValuePair<string, System.Action>("Replace", () => { string f; command.Input.Text = Generalize(command.Input.Text, dir, out f); }));
            }
            else suggestion.Hide2();
            if (IsHandleCreated) Place();
        }

        string CurrentScript() { return command.Input.Text; }

        void RefreshPreview()
        {
            try
            {
                string sample = string.IsNullOrEmpty(dir) ? @"D:\photos" : dir;
                string shown = Placeholders.Expand(CurrentScript(), shell, new PlaceholderContext { Paths = new[] { sample }, Dir = sample, Clip = "" });
                preview.Text = "On " + sample + " this runs:\n" + (shown.Length > 160 ? shown.Substring(0, 157) + "..." : shown);
            }
            catch (PlaceholderException ex) { preview.Text = ex.Message; }
        }

        void TestRun()
        {
            if (string.IsNullOrWhiteSpace(CurrentScript())) return;
            try
            {
                string script = Placeholders.Expand(CurrentScript(), shell, Executor.ContextFor(new[] { dir }));
                var risk2 = RiskScan.Classify(script, shell);
                if (risk2.Risk != Risk.Safe && !Program.Ask("Test run \"" + title.Input.Text + "\" in " + dir + "?\n\n" + string.Join("\n", risk2.Reasons.ToArray()))) return;
                testOutput.ClearAll();
                testOutput.AppendLine("Running...", false);
                bool table = ResultValues[result.SelectedIndex] == ActionOutput.Grid && shell != ActionShell.Cmd;
                var req = new RunRequest { Shell = shell, Script = script, Dir = dir, AsTable = table };
                var buffer = new StringBuilder();
                RunEngine.Start(req, (line, err) => { lock (buffer) buffer.AppendLine(line); },
                    (code, ms, stopped) => BeginInvoke(new System.Action(() =>
                    {
                        testOutput.ClearAll();
                        string text;
                        lock (buffer) text = buffer.ToString();
                        if (table)
                        {
                            string plain;
                            var parsed = RunEngine.ParseTable(text, out plain);
                            text = parsed == null ? plain : parsed.Summary + "\n" + string.Join("\t", parsed.Columns) + "\n" + string.Join("\n", parsed.Rows.Take(12).Select(r => string.Join("\t", r)).ToArray());
                        }
                        foreach (var l in text.TrimEnd().Split('\n')) testOutput.AppendLine(l.TrimEnd('\r'), false);
                        testOutput.AppendLine("Exit " + code + " (" + (ms / 1000.0).ToString("0.00") + " s)", code != 0);
                    })));
            }
            catch (PlaceholderException ex) { Program.Fail(ex.Message); }
        }

        static string Clean(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '-' : c);
            return sb.ToString().Trim().TrimEnd('.');
        }

        static string Slug(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s.ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            return sb.ToString().Trim('-');
        }

        public string Build(out string fileName)
        {
            string titleText = title.Input.Text.Trim();
            string menuText = menu.Text.Trim().Replace('\\', '/').Trim('/');
            var contexts = new List<string>();
            if (forFile.Checked) contexts.Add("file");
            if (forFolder.Checked) contexts.Add("folder");
            if (forBackground.Checked) contexts.Add("background");
            if (forDrive.Checked) contexts.Add("drive");
            if (contexts.Count == 0) contexts.Add("background");
            string riskText = risk.SelectedIndex == 0 ? "safe" : risk.SelectedIndex == 1 ? "modifies" : "destructive";
            string outputText = ResultValues[result.SelectedIndex].ToString().ToLowerInvariant();
            string firstLine = CurrentScript().Replace("\r", "").Split('\n')[0].Trim();
            string description = "Saved from the Run console: " + (firstLine.Length > 70 ? firstLine.Substring(0, 67) + "..." : firstLine);
            string id = "user." + Slug(menuText.Replace('/', ' ') + " " + titleText);
            var header = new StringBuilder();
            bool cmd = shell == ActionShell.Cmd;
            string open = cmd ? ":: " : "";
            if (!cmd) header.AppendLine("<#");
            header.AppendLine(open + "@id " + id);
            header.AppendLine(open + "@title " + titleText);
            header.AppendLine(open + "@menu " + menuText);
            header.AppendLine(open + "@when " + string.Join(", ", contexts.ToArray()));
            header.AppendLine(open + "@output " + outputText);
            header.AppendLine(open + "@risk " + riskText);
            if (extensions.Input.Text.Trim().Length > 0) header.AppendLine(open + "@ext " + extensions.Input.Text.Trim());
            if (shell == ActionShell.Pwsh) header.AppendLine(open + "@shell pwsh");
            header.AppendLine(open + "@description " + description);
            if (!cmd) header.AppendLine("#>");
            string body = CurrentScript().TrimEnd();
            fileName = Clean(titleText) + (cmd ? ".cmd" : ".ps1");
            SavedId = id;
            return header.ToString() + body + "\r\n";
        }

        void Save()
        {
            try
            {
                string fileName;
                string text = Build(out fileName);
                string baseId = SavedId;
                string menuText = menu.Text.Trim().Replace('/', '\\').Trim('\\');
                string folder = Path.Combine(Journal.Dir, "actions", menuText.Length == 0 ? MenuTree.MyActions : menuText);
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, fileName);
                int n = 2;
                while (File.Exists(path))
                {
                    path = Path.Combine(folder, Path.GetFileNameWithoutExtension(fileName) + " (" + n + ")" + Path.GetExtension(fileName));
                    n++;
                }
                if (n > 2)
                {
                    SavedId = baseId + "-" + (n - 1);
                    text = text.Replace("@id " + baseId, "@id " + SavedId);
                }
                File.WriteAllText(path, text, new UTF8Encoding(true));
                SavedPath = path;
                ActionCatalog.Reset();
                var spec = ActionCatalog.Current.Find(SavedId);
                if (spec != null && favorite.Checked) { Settings.ToggleFavorite(spec.Id); Shell.RefreshIfInstalled(); }
                Runner.Notify(this, "Saved. Right-click, PS Tools, Power menu: " + menu.Text.Trim().Replace("/", " > ") + " > " + title.Input.Text.Trim(), false);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { Program.Fail("Could not save the action: " + ex.Message); }
        }
    }
}
