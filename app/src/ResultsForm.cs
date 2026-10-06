// Generic sortable/filterable results table with Open / Show in folder / Recycle / Export CSV.
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
    class ResultsForm : Form
    {
        readonly ListView lv = new ListView();
        readonly TextBox filter = new TextBox();
        readonly Result r;
        int sortCol = -1; bool sortDesc;
        List<string[]> rows;

        public ResultsForm(string caption, Result result)
        {
            r = result; rows = result.Rows;
            Text = caption; Font = UI.Font; ClientSize = new Size(900, 540); StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var head = new Label { Dock = DockStyle.Top, Height = 34, Padding = new Padding(10, 9, 10, 0), Text = r.Summary, BackColor = UI.Panel, AutoEllipsis = true };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8, 7, 8, 4), BackColor = UI.Panel };
            filter.Width = 220; filter.Margin = new Padding(0, 3, 12, 0);
            SetCue(filter, "Filter...");
            filter.TextChanged += (s, e) => Fill();
            bar.Controls.Add(filter);

            lv.Dock = DockStyle.Fill; lv.View = View.Details; lv.FullRowSelect = true; lv.GridLines = false;
            lv.CheckBoxes = r.Checkable; lv.BorderStyle = BorderStyle.None;
            int skip = r.Columns[0] == "" ? 1 : 0;   // first column "" = initial check state
            for (int i = skip; i < r.Columns.Length; i++)
                lv.Columns.Add(r.Columns[i], i == r.PathColumn ? 520 : 110);
            lv.ColumnClick += (s, e) => { int c = e.Column + skip; sortDesc = sortCol == c && !sortDesc; sortCol = c; Fill(); };

            if (r.PathColumn >= 0)
            {
                lv.DoubleClick += (s, e) => OpenSel(false);
                AddBtn(bar, "Open", () => OpenSel(false));
                AddBtn(bar, "Show in folder", () => OpenSel(true));
                if (r.Checkable)
                {
                    AddBtn(bar, "Tick selected", () => { foreach (ListViewItem i in lv.SelectedItems) i.Checked = true; });
                    AddBtn(bar, "Untick all", () => { foreach (ListViewItem i in lv.Items) i.Checked = false; });
                    var rec = AddBtn(bar, "Recycle ticked", RecycleChecked); rec.ForeColor = Color.FromArgb(170, 30, 30);
                }
                var cm = new ContextMenuStrip();
                cm.Items.Add("Open", null, (s, e) => OpenSel(false));
                cm.Items.Add("Show in folder", null, (s, e) => OpenSel(true));
                cm.Items.Add("Copy path", null, (s, e) => { var p = SelPaths(); if (p.Any()) Clipboard.SetText(string.Join("\r\n", p)); });
                lv.ContextMenuStrip = cm;
            }
            AddBtn(bar, "Export CSV...", ExportCsv);

            Controls.Add(lv); Controls.Add(head); Controls.Add(bar);
            Fill();
        }

        static Button AddBtn(Control parent, string text, System.Action onClick)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(0, 0, 6, 0) };
            b.Click += (s, e) => onClick();
            parent.Controls.Add(b); return b;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        public static void SetCue(TextBox t, string cue) { t.HandleCreated += (s, e) => SendMessage(t.Handle, 0x1501, (IntPtr)1, cue); }

        void Fill()
        {
            string f = filter.Text.Trim();
            IEnumerable<string[]> q = rows;
            if (f.Length > 0) q = q.Where(row => row.Any(c => c.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0));
            if (sortCol >= 0)
            {
                int col = sortCol;
                q = sortDesc ? q.OrderByDescending(row => row[col], Cmp.Instance) : q.OrderBy(row => row[col], Cmp.Instance);
            }
            int skip = r.Columns[0] == "" ? 1 : 0;
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var row in q.Take(5000))
            {
                var it = new ListViewItem(row.Skip(skip).ToArray()) { Tag = row };
                if (skip == 1) it.Checked = row[0] == "x";
                lv.Items.Add(it);
            }
            lv.EndUpdate();
            lv.ItemChecked -= OnChecked; lv.ItemChecked += OnChecked;
        }

        // Remember tick state in the row so filtering/sorting keeps it.
        void OnChecked(object s, ItemCheckedEventArgs e) { var row = (string[])e.Item.Tag; if (r.Columns[0] == "") row[0] = e.Item.Checked ? "x" : ""; }

        // Sorts sizes ("1.2 MB"), numbers ("1,234", "#3") numerically, everything else as text.
        class Cmp : IComparer<string>
        {
            public static readonly Cmp Instance = new Cmp();
            static readonly string[] U = { " B", " KB", " MB", " GB", " TB" };
            static bool Num(string v, out double d)
            {
                for (int i = U.Length - 1; i >= 0; i--)
                    if (v.EndsWith(U[i]) && double.TryParse(v.Substring(0, v.Length - U[i].Length), out d)) { d *= Math.Pow(1024, i); return true; }
                return double.TryParse(v.Replace(",", "").TrimStart('#'), out d);
            }
            public int Compare(string a, string b)
            {
                double x, y; bool nx = Num(a, out x), ny = Num(b, out y);
                if (nx && ny) return x.CompareTo(y);
                if (nx != ny) return nx ? -1 : 1;
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }

        IEnumerable<string> SelPaths() { return lv.SelectedItems.Cast<ListViewItem>().Select(i => ((string[])i.Tag)[r.PathColumn]); }

        void OpenSel(bool reveal)
        {
            foreach (var p in SelPaths().Take(10))
                try
                {
                    if (reveal) Process.Start("explorer.exe", "/select,\"" + p + "\"");
                    else Process.Start(p);
                }
                catch (Exception ex) { Program.Msg(ex.Message); }
        }

        void RecycleChecked()
        {
            var ticked = rows.Where(row => row[0] == "x").ToList();
            if (ticked.Count == 0) { Program.Msg("Nothing ticked."); return; }
            if (!Program.Ask("Send " + ticked.Count + " files to the Recycle Bin?")) return;
            int ok = 0;
            foreach (var row in ticked)
                try { Fs.Recycle(row[r.PathColumn]); rows.Remove(row); ok++; } catch { }
            Fill();
            Program.Msg(ok + " files sent to the Recycle Bin." + (ok < ticked.Count ? "\n" + (ticked.Count - ok) + " could not be removed." : ""));
        }

        void ExportCsv()
        {
            using (var d = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "results.csv" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var sb = new StringBuilder();
                int skip = r.Columns[0] == "" ? 1 : 0;
                sb.AppendLine(string.Join(",", r.Columns.Skip(skip).Select(Q)));
                foreach (var row in rows) sb.AppendLine(string.Join(",", row.Skip(skip).Select(Q)));
                File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true));
            }
        }

        static string Q(string s) { return "\"" + s.Replace("\"", "\"\"") + "\""; }
    }
}
