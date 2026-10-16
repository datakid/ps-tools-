using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace PSTools
{
    class SearchRun
    {
        public volatile bool Stop;
        public volatile bool Done;
        public volatile bool Capped;
    }

    class SearchForm : Form
    {
        const int Max = 2000;
        const long MaxTextBytes = 5 * 1024 * 1024;

        readonly string root;
        readonly TextBox query = new TextBox();
        readonly FToggle inside = new FCheck { Text = "Also search inside text files", AutoSize = true, Checked = true };
        readonly ListView list = new ListView();
        readonly Label state = new Label { AutoSize = true, ForeColor = UI.Muted };
        readonly System.Windows.Forms.Timer pump = new System.Windows.Forms.Timer { Interval = 120 };
        readonly List<string[]> pending = new List<string[]>();
        SearchRun current;
        int shown;

        public SearchForm(string folder)
        {
            root = folder;
            Text = "Search  -  " + folder; Font = UI.Font; ClientSize = new Size(980, 600); StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, ColumnCount = 3, Padding = new Padding(12, 11, 12, 8), BackColor = UI.Panel };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            query.Dock = DockStyle.Fill; query.Font = new Font("Segoe UI", 10f);
            ResultsForm.SetCue(query, "Words to look for (all must match)");
            inside.Margin = new Padding(12, 6, 12, 0);
            var go = new FButton { Text = "Search", Width = 100, Height = 30 };
            UI.Primary(go);
            go.Click += (s, e) => Start();
            top.Controls.Add(query, 0, 0); top.Controls.Add(inside, 1, 0); top.Controls.Add(go, 2, 0);
            AcceptButton = go;

            list.Dock = DockStyle.Fill; list.View = View.Details; list.FullRowSelect = true; list.HideSelection = false; list.BorderStyle = BorderStyle.None;
            list.SmallImageList = new ImageList { ImageSize = new Size(1, UI.S(24)) };
            list.Columns.Add("Name", 260); list.Columns.Add("Found", 320); list.Columns.Add("Folder", 380);
            list.DoubleClick += (s, e) => OpenSel(false);
            list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) OpenSel(false); };

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8, 7, 8, 4), BackColor = UI.Panel };
            Action<string, int, System.Action> add = (text, w, click) =>
            {
                var b = new FButton { Text = text, Width = w, Height = 30, Margin = new Padding(0, 0, 6, 0) };
                b.Click += (s, e) => click();
                bar.Controls.Add(b);
            };
            add("Open", 80, () => OpenSel(false));
            add("Show in folder", 120, () => OpenSel(true));
            add("Copy path", 100, CopyPath);
            state.Margin = new Padding(10, 8, 0, 0);
            bar.Controls.Add(state);

            Controls.Add(list); Controls.Add(top); Controls.Add(bar);
            UI.Apply(this);
            state.ForeColor = UI.Muted;

            pump.Tick += (s, e) => Pump();
            pump.Start();
            Shown += (s, e) => query.Focus();
            FormClosing += (s, e) => { pump.Stop(); if (current != null) current.Stop = true; };
        }

        void Start()
        {
            string[] terms = query.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0) return;
            if (current != null) current.Stop = true;
            var run = new SearchRun();
            lock (pending) pending.Clear();
            list.Items.Clear();
            shown = 0;
            current = run;
            state.Text = "Searching...";
            bool contents = inside.Checked;
            var t = new Thread(() => Work(run, terms, contents)) { IsBackground = true };
            t.Start();
        }

        static bool AllIn(string text, string[] terms)
        {
            foreach (var t in terms) if (text.IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0) return false;
            return true;
        }

        void Push(SearchRun run, string name, string found, string dir, string full)
        {
            lock (pending) { if (!run.Stop) pending.Add(new[] { name, found, dir, full }); }
        }

        static string FindInFile(string file, string[] terms, SearchRun run)
        {
            try
            {
                if (new FileInfo(file).Length > MaxTextBytes || !Fs.LooksText(file)) return null;
                int n = 0;
                foreach (var line in File.ReadLines(file))
                {
                    n++;
                    if ((n & 1023) == 0 && run.Stop) return null;
                    if (AllIn(line, terms))
                    {
                        string t = line.Trim();
                        if (t.Length > 140) t = t.Substring(0, 140) + "...";
                        return "line " + n + ": " + t;
                    }
                }
            }
            catch { }
            return null;
        }

        void Work(SearchRun run, string[] terms, bool contents)
        {
            int count = 0;
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0 && !run.Stop && count < Max)
            {
                string dir = stack.Pop();
                string[] files = null, subs = null;
                try { files = Directory.GetFiles(dir); } catch { }
                try { subs = Directory.GetDirectories(dir); } catch { }
                if (files != null)
                    foreach (var f in files)
                    {
                        if (run.Stop || count >= Max) break;
                        string name = Path.GetFileName(f);
                        string found = AllIn(name, terms) ? "name" : contents ? FindInFile(f, terms, run) : null;
                        if (found != null) { Push(run, name, found, dir, f); count++; }
                    }
                if (subs != null)
                    foreach (var s in subs)
                    {
                        string name = Path.GetFileName(s);
                        if (Fs.IsLink(s) || Actions.IsSkippedDir(name)) continue;
                        if (AllIn(name, terms) && count < Max) { Push(run, name, "folder name", dir, s); count++; }
                        stack.Push(s);
                    }
            }
            run.Capped = count >= Max;
            run.Done = true;
        }

        void Pump()
        {
            if (current == null) return;
            string[][] batch;
            lock (pending) { batch = pending.ToArray(); pending.Clear(); }
            if (batch.Length > 0)
            {
                list.BeginUpdate();
                foreach (var r in batch)
                {
                    var it = new ListViewItem(new[] { r[0], r[1], r[2] }) { Tag = r[3] };
                    list.Items.Add(it);
                    shown++;
                }
                list.EndUpdate();
                if (list.SelectedItems.Count == 0 && list.Items.Count > 0) list.Items[0].Selected = true;
            }
            if (current.Done)
                state.Text = shown == 0 ? "No matches." : shown + " results" + (current.Capped ? " (stopped at " + Max + ", narrow the search)" : "");
            else
                state.Text = "Searching...  " + shown + " so far";
        }

        string SelectedPath { get { return list.SelectedItems.Count == 0 ? null : (string)list.SelectedItems[0].Tag; } }

        void OpenSel(bool reveal)
        {
            string p = SelectedPath;
            if (p == null) return;
            try
            {
                if (reveal) Process.Start("explorer.exe", "/select,\"" + p + "\"");
                else Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
            }
            catch (Exception ex) { Program.Msg(ex.Message); }
        }

        void CopyPath()
        {
            var paths = list.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag).ToArray();
            if (paths.Length > 0) Clip.Set(string.Join("\r\n", paths));
        }
    }
}
