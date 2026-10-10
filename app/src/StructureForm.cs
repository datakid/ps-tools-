using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PSTools
{
    class StructureForm : Form
    {
        readonly string root;
        readonly ComboBox format = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        readonly CheckBox files = new CheckBox { Text = "Include files", AutoSize = true };
        readonly CheckBox hidden = new CheckBox { Text = "Hidden folders", AutoSize = true };
        readonly CheckBox counts = new CheckBox { Text = "File counts", AutoSize = true };
        readonly NumericUpDown depth = new NumericUpDown { Minimum = 0, Maximum = 50, Value = 0, Width = 52 };
        readonly TextBox preview = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = UI.Mono, Tag = "code" };
        readonly Label info = new Label { AutoSize = true, ForeColor = UI.Muted, Margin = new Padding(12, 8, 0, 0) };
        string text = "";

        public StructureForm(string folder)
        {
            root = Fs.Full(folder);
            Text = "Folder structure  -  " + root; Font = UI.Font; ClientSize = new Size(860, 600); StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var opts = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(12, 10, 12, 4), BackColor = UI.Panel };
            format.Items.AddRange(Structure.Formats); format.SelectedIndex = 0;
            opts.Controls.Add(new Label { Text = "Format", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
            opts.Controls.Add(format);
            opts.Controls.Add(new Label { Text = "Depth (0 = all)", AutoSize = true, Margin = new Padding(14, 6, 6, 0) });
            opts.Controls.Add(depth);
            foreach (var c in new Control[] { files, hidden, counts }) { c.Margin = new Padding(14, 5, 0, 0); opts.Controls.Add(c); }

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(10, 8, 8, 4), BackColor = UI.Panel, FlowDirection = FlowDirection.RightToLeft };
            var close = new Button { Text = "Close", Width = 80, Height = 30 }; close.Click += (s, e) => Close();
            var save = new Button { Text = "Save as .txt...", Width = 120, Height = 30 }; save.Click += (s, e) => Save();
            var copy = new Button { Text = "Copy", Width = 110, Height = 30 }; UI.Primary(copy);
            copy.Click += (s, e) => { Clip.Set(text); info.Text = "Copied."; };
            bar.Controls.Add(close); bar.Controls.Add(save); bar.Controls.Add(copy); bar.Controls.Add(info);

            format.SelectedIndexChanged += (s, e) => Rebuild();
            depth.ValueChanged += (s, e) => Rebuild();
            files.CheckedChanged += (s, e) => Rebuild();
            hidden.CheckedChanged += (s, e) => Rebuild();
            counts.CheckedChanged += (s, e) => Rebuild();

            Controls.Add(preview); Controls.Add(opts); Controls.Add(bar);
            UI.Apply(this);
            preview.BackColor = UI.Field; preview.ForeColor = UI.Text;
            Shown += (s, e) => Rebuild();
        }

        void Rebuild()
        {
            var o = new StructureOptions { Files = files.Checked, Hidden = hidden.Checked, Counts = counts.Checked, Depth = (int)depth.Value, Format = (string)format.SelectedItem };
            Cursor = Cursors.WaitCursor;
            try
            {
                text = Structure.Build(root, o, null);
                preview.Text = text;
                info.Text = (text.Split('\n').Length - 1).ToString("N0") + " lines";
            }
            catch (Exception ex) { info.Text = ex.Message; }
            finally { Cursor = Cursors.Default; }
        }

        void Save()
        {
            using (var d = new SaveFileDialog { Filter = "Text|*.txt", FileName = (Path.GetFileName(root).Length > 0 ? Path.GetFileName(root) : "drive") + "-structure.txt" })
                if (d.ShowDialog(this) == DialogResult.OK) File.WriteAllText(d.FileName, text, new UTF8Encoding(true));
        }
    }
}
