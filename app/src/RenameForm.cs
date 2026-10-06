// Batch rename with live preview. Two-phase rename (via temp names) so swaps and case-only
// changes work. Journaled for Undo.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PSTools
{
    class RenameForm : Form
    {
        readonly string root;
        readonly TextBox find = new TextBox(), repl = new TextBox(), prefix = new TextBox(), suffix = new TextBox(), only = new TextBox();
        readonly CheckBox regex = new CheckBox { Text = "Regex", AutoSize = true }, number = new CheckBox { Text = "Number", AutoSize = true },
                          spaces = new CheckBox { Text = "Spaces to _", AutoSize = true }, keepExt = new CheckBox { Text = "Keep extension", AutoSize = true, Checked = true },
                          dateP = new CheckBox { Text = "Date prefix", AutoSize = true }, folders = new CheckBox { Text = "Include folders", AutoSize = true };
        readonly ComboBox casing = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        readonly NumericUpDown start = new NumericUpDown { Minimum = 0, Maximum = 999999, Value = 1, Width = 64 },
                               digits = new NumericUpDown { Minimum = 1, Maximum = 8, Value = 3, Width = 44 };
        readonly ListView lv = new ListView();
        readonly Label info = new Label { AutoSize = true, ForeColor = UI.Muted, Margin = new Padding(0, 8, 12, 0) };
        readonly Button apply = new Button { Text = "Rename", Width = 110, Height = 30 };
        List<KeyValuePair<string, string>> plan = new List<KeyValuePair<string, string>>();

        public RenameForm(string root)
        {
            this.root = root;
            Text = "Batch rename  -  " + root; Font = UI.Font; ClientSize = new Size(920, 600); StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var opts = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(10, 10, 10, 4), BackColor = UI.Panel };
            for (int i = 0; i < 4; i++) opts.ColumnStyles.Add(new ColumnStyle(i % 2 == 0 ? SizeType.AutoSize : SizeType.Percent, 50));
            foreach (var t in new[] { find, repl, prefix, suffix, only }) { t.Dock = DockStyle.Fill; t.TextChanged += (s, e) => Preview(); }
            Row(opts, 0, "Find", find, "Replace with", repl);
            Row(opts, 1, "Prefix", prefix, "Suffix", suffix);
            casing.Items.AddRange(new object[] { "Keep case", "lowercase", "UPPERCASE", "Title Case" }); casing.SelectedIndex = 0;
            var flags = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            foreach (Control c in new Control[] { regex, spaces, keepExt, dateP, folders, casing, number, new Label { Text = "from", AutoSize = true, Margin = new Padding(0, 6, 0, 0) }, start, new Label { Text = "digits", AutoSize = true, Margin = new Padding(0, 6, 0, 0) }, digits })
            { flags.Controls.Add(c); var cb = c as CheckBox; if (cb != null) cb.CheckedChanged += (s, e) => Preview(); }
            casing.SelectedIndexChanged += (s, e) => Preview(); start.ValueChanged += (s, e) => Preview(); digits.ValueChanged += (s, e) => Preview();
            opts.Controls.Add(new Label { Text = "Options", AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, 0, 2);
            opts.Controls.Add(flags, 1, 2); opts.SetColumnSpan(flags, 3);
            opts.Controls.Add(new Label { Text = "Only names matching", AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, 0, 3);
            opts.Controls.Add(only, 1, 3);
            ResultsForm.SetCue(only, "e.g. *.jpg  (empty = all)");
            ResultsForm.SetCue(find, "text to find"); ResultsForm.SetCue(prefix, "{n} = number, {date} = modified date");

            lv.Dock = DockStyle.Fill; lv.View = View.Details; lv.FullRowSelect = true; lv.BorderStyle = BorderStyle.None;
            lv.Columns.Add("Current name", 400); lv.Columns.Add("New name", 440);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 7, 8, 4), BackColor = UI.Panel };
            apply.BackColor = UI.Accent; apply.ForeColor = Color.White; apply.FlatStyle = FlatStyle.Flat; apply.FlatAppearance.BorderSize = 0; apply.Font = UI.Bold;
            apply.Click += (s, e) => Apply();
            var close = new Button { Text = "Close", Width = 80, Height = 30 }; close.Click += (s, e) => Close();
            bottom.Controls.Add(apply); bottom.Controls.Add(close); bottom.Controls.Add(info);

            Controls.Add(lv); Controls.Add(opts); Controls.Add(bottom);
            Preview();
        }

        static void Row(TableLayoutPanel t, int r, string l1, Control c1, string l2, Control c2)
        {
            t.Controls.Add(new Label { Text = l1, AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, 0, r); t.Controls.Add(c1, 1, r);
            t.Controls.Add(new Label { Text = l2, AutoSize = true, Margin = new Padding(12, 6, 8, 0) }, 2, r); t.Controls.Add(c2, 3, r);
        }

        IEnumerable<string> Items()
        {
            IEnumerable<string> items = Directory.GetFiles(root);
            if (folders.Checked) items = Directory.GetDirectories(root).Concat(items);
            string pat = only.Text.Trim();
            if (pat.Length > 0)
            {
                var rx = new Regex("^" + Regex.Escape(pat).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
                items = items.Where(p => rx.IsMatch(Path.GetFileName(p)));
            }
            return items.OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
        }

        string NewName(string path, int n)
        {
            string name = Path.GetFileName(path);
            bool isDir = Directory.Exists(path);
            string ext = keepExt.Checked && !isDir ? Path.GetExtension(name) : "";
            string bas = name.Substring(0, name.Length - ext.Length);
            string num = n.ToString(new string('0', (int)digits.Value));
            string date = File.GetLastWriteTime(path).ToString("yyyy-MM-dd");

            if (find.Text.Length > 0)
                bas = regex.Checked ? Regex.Replace(bas, find.Text, repl.Text) : ReplaceCI(bas, find.Text, repl.Text);
            if (spaces.Checked) bas = bas.Replace(' ', '_');
            switch (casing.SelectedIndex)
            {
                case 1: bas = bas.ToLowerInvariant(); ext = ext.ToLowerInvariant(); break;
                case 2: bas = bas.ToUpperInvariant(); ext = ext.ToUpperInvariant(); break;
                case 3: bas = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(bas.ToLower()); break;
            }
            string pre = prefix.Text.Replace("{n}", num).Replace("{date}", date);
            string suf = suffix.Text.Replace("{n}", num).Replace("{date}", date);
            bas = pre + bas + suf;
            if (dateP.Checked) bas = date + "-" + bas;
            if (number.Checked && !prefix.Text.Contains("{n}") && !suffix.Text.Contains("{n}")) bas = bas + "_" + num;
            return bas + ext;
        }

        static string ReplaceCI(string s, string find, string repl)
        {
            return Regex.Replace(s, Regex.Escape(find), repl.Replace("$", "$$"), RegexOptions.IgnoreCase);
        }

        void Preview()
        {
            plan.Clear();
            string error = null;
            int n = (int)start.Value, changed = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            lv.BeginUpdate(); lv.Items.Clear();
            try
            {
                foreach (var p in Items())
                {
                    string nn = NewName(p, n++);
                    string old = Path.GetFileName(p);
                    var it = new ListViewItem(new[] { old, nn });
                    if (nn.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || nn.Trim().Length == 0) { it.ForeColor = Color.Firebrick; error = "Invalid characters in a new name."; }
                    else if (!seen.Add(nn)) { it.ForeColor = Color.Firebrick; error = "Two files would get the same name."; }
                    else if (nn != old) { it.ForeColor = UI.Accent; changed++; plan.Add(new KeyValuePair<string, string>(p, Path.Combine(root, nn))); }
                    lv.Items.Add(it);
                }
            }
            catch (ArgumentException) { error = "Invalid regex."; }
            lv.EndUpdate();
            // Collision with an existing file that is not part of the rename.
            var sources = new HashSet<string>(plan.Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
            if (error == null && plan.Any(x => (File.Exists(x.Value) || Directory.Exists(x.Value)) && !sources.Contains(x.Value)))
                error = "A new name already exists in the folder.";
            info.Text = error ?? (changed + " of " + lv.Items.Count + " will be renamed");
            info.ForeColor = error != null ? Color.Firebrick : UI.Muted;
            apply.Enabled = error == null && changed > 0;
        }

        void Apply()
        {
            var j = new Journal("Rename in " + root);
            try
            {
                // Phase 1: move everything to unique temp names. Phase 2: temp -> final.
                var temps = new List<KeyValuePair<string, string>>();
                foreach (var x in plan)
                {
                    string tmp = Path.Combine(root, "~pst" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    j.Move(x.Key, tmp); temps.Add(new KeyValuePair<string, string>(tmp, x.Value));
                }
                foreach (var t in temps) j.Move(t.Key, t.Value);
                info.Text = "Renamed " + plan.Count + ". Undo is available in the main window.";
            }
            catch (Exception ex) { Program.Msg("Stopped: " + ex.Message + "\n\nUse Undo last to revert."); }
            finally { j.Save(); }
            find.Text = repl.Text = prefix.Text = suffix.Text = "";
            Preview();
        }
    }
}
