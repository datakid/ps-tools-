using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class UndoHistoryForm : Form
    {
        readonly ListView entries = new ListView();
        readonly ListView details = new ListView();
        readonly Label note = new Label { Dock = DockStyle.Top, Height = 44, ForeColor = UI.Muted, Padding = new Padding(14, 8, 14, 0) };
        readonly Label usage = new Label { AutoSize = false, Dock = DockStyle.Left, Width = 330, ForeColor = UI.Muted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0) };
        readonly Button undo = new Button { Width = 250, Height = 32, Margin = new Padding(8, 0, 0, 0) };
        readonly Button clear = new Button { Text = "Clear history", Width = 120, Height = 32 };
        readonly Button close = new Button { Text = "Close", Width = 90, Height = 32 };
        List<JournalEntry> history = new List<JournalEntry>();

        JournalEntry Selected { get { return entries.SelectedItems.Count == 0 ? null : (JournalEntry)entries.SelectedItems[0].Tag; } }

        public UndoHistoryForm()
        {
            Text = "Undo history"; Font = UI.Font; ClientSize = new Size(860, 560); MinimumSize = new Size(700, 440); StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            note.Text = "Newest first. Undo always goes in order, so undoing an older entry also undoes everything above it. History keeps the last "
                + Journal.MaxEntries + " operations and up to " + Fs.Size(Journal.MaxBackupBytes) + " of backups.";

            entries.Dock = DockStyle.Fill; entries.View = View.Details; entries.FullRowSelect = true; entries.MultiSelect = false; entries.HideSelection = false; entries.BorderStyle = BorderStyle.None;
            entries.Columns.Add("When", 140); entries.Columns.Add("What", 520); entries.Columns.Add("Items", 70);
            entries.SmallImageList = new ImageList { ImageSize = new Size(1, UI.S(28)) };
            entries.SelectedIndexChanged += (s, e) => ShowDetails();
            entries.Resize += (s, e) => { if (entries.Columns.Count == 3) entries.Columns[1].Width = Math.Max(120, entries.ClientSize.Width - entries.Columns[0].Width - entries.Columns[2].Width); UI.HideHScroll(entries); };

            details.Dock = DockStyle.Fill; details.View = View.Details; details.HeaderStyle = ColumnHeaderStyle.None; details.FullRowSelect = true; details.BorderStyle = BorderStyle.None;
            details.Columns.Add("", 800); details.ShowItemToolTips = true;
            details.SmallImageList = new ImageList { ImageSize = new Size(1, UI.S(24)) };
            details.Resize += (s, e) => { if (details.Columns.Count == 1) details.Columns[0].Width = Math.Max(200, details.ClientSize.Width); UI.HideHScroll(details); };

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
            split.Panel1.Controls.Add(entries);
            var head = new Label { Text = "What undoing the selected entry will do, in order", Dock = DockStyle.Top, Height = 28, Font = UI.Bold, Padding = new Padding(14, 6, 0, 0) };
            split.Panel2.Controls.Add(details); split.Panel2.Controls.Add(head);

            undo.Click += (s, e) => DoUndo();
            clear.Click += (s, e) => DoClear();
            close.Click += (s, e) => Close();
            UI.Primary(undo);
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = UI.Panel };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 520, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 9, 14, 0) };
            buttons.Controls.Add(close); buttons.Controls.Add(clear); buttons.Controls.Add(undo);
            bar.Controls.Add(buttons); bar.Controls.Add(usage);

            Controls.Add(split); Controls.Add(note); Controls.Add(bar);
            CancelButton = close;
            Activated += (s, e) => Reload();
            Shown += (s, e) => { split.SplitterDistance = Math.Max(120, split.Height * 45 / 100); };
            UI.Apply(this);
            Reload();
        }

        void Reload()
        {
            string keep = Selected == null ? null : Selected.File;
            history = Journal.History();
            entries.BeginUpdate();
            entries.Items.Clear();
            foreach (var h in history)
            {
                var it = new ListViewItem(h.When) { Tag = h };
                it.SubItems.Add(h.Title);
                it.SubItems.Add(h.Items.ToString());
                entries.Items.Add(it);
            }
            entries.EndUpdate();
            usage.Text = history.Count == 0 ? "Nothing to undo." : history.Count + (history.Count == 1 ? " operation, " : " operations, ") + Fs.Size(history.Sum(h => h.BackupBytes)) + " of backups";
            if (entries.Items.Count > 0)
            {
                ListViewItem pick = entries.Items[0];
                foreach (ListViewItem it in entries.Items) if (((JournalEntry)it.Tag).File == keep) pick = it;
                pick.Selected = true;
            }
            else ShowDetails();
            clear.Enabled = history.Count > 0;
        }

        void ShowDetails()
        {
            var e = Selected;
            details.BeginUpdate();
            details.Items.Clear();
            if (e == null) undo.Text = "Undo";
            else
            {
                int index = entries.SelectedItems[0].Index;
                undo.Text = index == 0 ? "Undo this" : "Undo this and the " + index + " above it";
                var steps = Journal.Steps(e);
                foreach (string s in steps.Take(2000)) details.Items.Add(new ListViewItem(s) { ToolTipText = s });
                if (steps.Count > 2000) details.Items.Add(new ListViewItem("... and " + (steps.Count - 2000) + " more"));
                if (steps.Count == 0) details.Items.Add(new ListViewItem("Nothing to restore for this entry."));
            }
            details.EndUpdate();
            undo.Enabled = e != null;
        }

        void DoUndo()
        {
            var e = Selected;
            if (e == null) return;
            int index = entries.SelectedItems[0].Index;
            var titles = history.Take(index + 1).Select(h => "- " + h.Title + "  (" + h.When + ")").ToList();
            string ask = "Undo " + titles.Count + (titles.Count == 1 ? " operation" : " operations") + ", newest first?\n\n" + string.Join("\n", titles.Take(8).ToArray())
                + (titles.Count > 8 ? "\n... and " + (titles.Count - 8) + " more" : "");
            if (!Program.Ask(ask)) return;
            string msg;
            Cursor = Cursors.WaitCursor;
            try { msg = Journal.UndoThrough(e); }
            finally { Cursor = Cursors.Default; }
            Reload();
            foreach (Form f in Application.OpenForms)
            {
                var m = f as MainForm;
                if (m != null) m.AfterUndo(msg);
            }
            Program.Msg(msg);
        }

        void DoClear()
        {
            if (!Program.Ask("Delete the whole undo history and its backups?\n\nThe changes stay as they are, but they can no longer be undone.")) return;
            Journal.ClearHistory();
            Reload();
        }
    }
}
