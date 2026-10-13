using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class PreviewForm : Form
    {
        readonly HashSet<string> enabled;
        readonly HashSet<string> favs;
        readonly TreeView tree = new TreeView();
        readonly ComboBox where = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        readonly CheckBox shift = new CheckBox { Text = "Holding Shift", AutoSize = true, Margin = new Padding(16, 5, 0, 0) };
        readonly Label count = new Label { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(14, 8, 14, 0), ForeColor = UI.Muted };
        static readonly Target[] kinds = { Target.Folder, Target.Background, Target.Drive, Target.File };

        public PreviewForm(HashSet<string> enabledIds, HashSet<string> favIds)
        {
            enabled = enabledIds; favs = favIds;
            Text = "Right-click menu preview"; Font = UI.Font; ClientSize = new Size(480, 560); MinimumSize = new Size(380, 360);
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(14, 10, 14, 0), BackColor = UI.Panel };
            bar.Controls.Add(new Label { Text = "Right-click on", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
            where.Items.AddRange(new object[] { "a folder", "folder background", "a drive", "a file" });
            where.SelectedIndex = 0;
            where.SelectedIndexChanged += (s, e) => Build();
            shift.CheckedChanged += (s, e) => Build();
            bar.Controls.Add(where); bar.Controls.Add(shift);

            tree.Dock = DockStyle.Fill; tree.ShowLines = false; tree.FullRowSelect = true; tree.HideSelection = true; tree.ItemHeight = UI.S(26); tree.ShowRootLines = true;

            Controls.Add(tree); Controls.Add(count); Controls.Add(bar);
            UI.Apply(this);
            tree.ItemHeight = UI.S(26);
            Build();
        }

        bool On(Action a)
        {
            return a.Id.StartsWith("lib-") ? Settings.IsEnabled(a.Id) : enabled.Contains(a.Id);
        }

        static string Qualifier(Action a)
        {
            if (string.IsNullOrEmpty(a.AppliesTo)) return "";
            if (a.AppliesTo == Modules.ImageQuery) return "   (pictures only)";
            if (a.AppliesTo == Modules.ZipQuery) return "   (zip files only)";
            return "   (matching files only)";
        }

        void Build()
        {
            Target t = kinds[Math.Max(0, where.SelectedIndex)];
            bool dirs = t != Target.File;
            bool shiftDown = shift.Checked;
            var acts = Actions.All.Where(a => (a.Targets & t) != 0 && a.Usable && On(a) && (shiftDown || !a.Extended)).ToList();

            tree.BeginUpdate();
            tree.Nodes.Clear();
            var root = new TreeNode("PS Tools") { NodeFont = UI.Bold };
            int top = 0, total = 0;
            if (dirs) { root.Nodes.Add(new TreeNode("Open in PS Tools")); top++; }
            foreach (var a in acts.Where(a => favs.Contains(a.Id)))
            {
                root.Nodes.Add(new TreeNode("\u2605 " + a.Label.TrimEnd('.') + Qualifier(a)));
                top++; total++;
            }
            foreach (var g in acts.Where(a => !favs.Contains(a.Id)).GroupBy(a => a.Group).OrderBy(g => Modules.Order(g.Key)))
            {
                var items = g.ToList();
                var mn = new TreeNode(g.Key + "  (" + items.Count + ")") { NodeFont = UI.Bold };
                foreach (var a in items) mn.Nodes.Add(new TreeNode(a.Label.TrimEnd('.') + Qualifier(a) + (a.Tier == Tier.Elevated ? "   (admin)" : "")));
                root.Nodes.Add(mn);
                top++; total += items.Count;
            }
            tree.Nodes.Add(root);
            root.Expand();
            tree.EndUpdate();

            string note = total == 0 ? "Nothing is enabled for this kind of item, so PS Tools would not appear." : total + " actions, " + top + " entries at the top level of the PS Tools menu.";
            if (Tweaks.IsWindows11 && !Tweaks.Classic) note += "\nWindows 11 shows this under Show more options until the classic menu is on.";
            count.Text = note;
        }
    }
}
