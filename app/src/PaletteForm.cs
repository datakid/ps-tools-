using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class PaletteForm : Form
    {
        readonly Form owner;
        readonly Action<Action> run;
        readonly TextBox box = new TextBox();
        readonly ListView list = new ListView();
        readonly List<Suggestion> suggestions;
        bool closing;

        public PaletteForm(Form ownerForm, Action<Action> runAction, List<Suggestion> suggested)
        {
            owner = ownerForm; run = runAction; suggestions = suggested ?? new List<Suggestion>();
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            KeyPreview = true; Font = UI.Font; ClientSize = new Size(560, 392); Padding = new Padding(1);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = UI.Back };
            var wrap = new Panel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(12, 12, 12, 8) };
            box.Dock = DockStyle.Fill; box.Font = new Font("Segoe UI", 11f);
            ResultsForm.SetCue(box, "Type an action...");
            box.TextChanged += (s, e) => Fill();
            box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down) { Move(1); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Up) { Move(-1); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { Choose(); e.SuppressKeyPress = true; }
            };
            wrap.Controls.Add(box);

            list.Dock = DockStyle.Fill; list.View = View.Details; list.HeaderStyle = ColumnHeaderStyle.None; list.FullRowSelect = true;
            list.MultiSelect = false; list.HideSelection = false; list.BorderStyle = BorderStyle.None; list.Font = new Font("Segoe UI", 9.5f);
            list.Columns.Add("", 340); list.Columns.Add("", 180);
            list.SmallImageList = new ImageList { ImageSize = new Size(1, UI.S(30)) };
            list.DoubleClick += (s, e) => Choose();
            list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Choose(); e.SuppressKeyPress = true; } };
            list.Resize += (s, e) => SizeColumns();

            body.Controls.Add(list); body.Controls.Add(wrap);
            Controls.Add(body);

            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { Shut(); e.SuppressKeyPress = true; } };
            Deactivate += (s, e) => Shut();
            Shown += (s, e) => { box.Focus(); SizeColumns(); };
            UI.Round(this);
            UI.Apply(this);
            BackColor = UI.Border;
            Location = new Point(owner.Left + (owner.Width - Width) / 2, owner.Top + Math.Max(24, owner.Height / 8));
            Fill();
        }

        void SizeColumns()
        {
            if (list.Columns.Count < 2) return;
            list.Columns[1].Width = UI.S(180);
            list.Columns[0].Width = Math.Max(UI.S(120), list.ClientSize.Width - UI.S(180));
            UI.HideHScroll(list);
        }

        void Shut()
        {
            if (closing) return;
            closing = true;
            Close();
        }

        void Move(int d)
        {
            if (list.Items.Count == 0) return;
            int i = list.SelectedItems.Count == 0 ? -1 : list.SelectedItems[0].Index;
            i = Math.Max(0, Math.Min(list.Items.Count - 1, i + d));
            list.Items[i].Selected = true;
            list.Items[i].EnsureVisible();
        }

        void Choose()
        {
            if (list.SelectedItems.Count == 0) return;
            var a = (Action)list.SelectedItems[0].Tag;
            Shut();
            owner.BeginInvoke((MethodInvoker)(() => run(a)));
        }

        static int Score(Action a, string q)
        {
            if (q.Length == 0) return 5;
            string l = a.Label;
            if (l.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 0;
            if (l.IndexOf(" " + q, StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            if (l.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            int pos = 0;
            foreach (char ch in q.Replace(" ", ""))
            {
                pos = l.IndexOf(ch.ToString(), pos, StringComparison.OrdinalIgnoreCase);
                if (pos < 0) break;
                pos++;
            }
            if (pos >= 0 && q.Replace(" ", "").Length > 1) return 3;
            if ((a.Hint + " " + a.Group + " " + a.Id).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return 4;
            return -1;
        }

        void Fill()
        {
            string q = box.Text.Trim();
            var recent = Settings.Recent;
            var rows = Actions.All.Where(a => a.Usable).Select(a => new { A = a, S = Score(a, q) }).Where(x => x.S >= 0)
                .OrderBy(x => x.S).ThenBy(x => { int i = recent.IndexOf(x.A.Id); return i < 0 ? 99 : i; })
                .ThenBy(x => Modules.Order(x.A.Group)).ThenBy(x => x.A.Label).Take(60).ToList();
            list.BeginUpdate(); list.Items.Clear();
            var shown = new HashSet<string>();
            if (q.Length == 0)
                foreach (var sg in suggestions)
                {
                    shown.Add(sg.ActionId);
                    var si = new ListViewItem("   " + sg.Action.Label.TrimEnd('.') + (sg.Action.Tier == Tier.Elevated ? "   (admin)" : "")) { Tag = sg.Action, UseItemStyleForSubItems = false };
                    si.SubItems.Add("Suggested: " + sg.Reason);
                    si.SubItems[1].ForeColor = UI.Muted;
                    list.Items.Add(si);
                }
            foreach (var x in rows)
            {
                if (shown.Contains(x.A.Id)) continue;
                bool isRecent = recent.Contains(x.A.Id) && q.Length == 0;
                var it = new ListViewItem((isRecent ? "\u21BB  " : "   ") + x.A.Label.TrimEnd('.') + (x.A.Tier == Tier.Elevated ? "   (admin)" : "")) { Tag = x.A, UseItemStyleForSubItems = false };
                it.SubItems.Add(x.A.Group);
                it.SubItems[1].ForeColor = UI.Muted;
                list.Items.Add(it);
            }
            list.EndUpdate();
            if (list.Items.Count > 0) list.Items[0].Selected = true;
        }
    }
}
