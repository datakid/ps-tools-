using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class PreviewForm : Form
    {
        readonly SettingsData state;
        readonly TreeView tree = new TreeView();
        readonly FDrop where = new FDrop { Width = 180 };
        readonly FToggle shift = new FCheck { Text = "Holding Shift", AutoSize = true, Margin = new Padding(16, 5, 0, 0) };
        readonly Label count = new Label { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(14, 8, 14, 10), ForeColor = UI.Muted };
        static readonly Target[] kinds = { Target.Folder, Target.Background, Target.Drive, Target.File };

        public PreviewForm(HashSet<string> enabledIds, HashSet<string> favIds) : this(FromSets(enabledIds, favIds)) { }

        static SettingsData FromSets(HashSet<string> enabledIds, HashSet<string> favIds)
        {
            var d = new SettingsData();
            d.Enabled = new HashSet<string>(enabledIds);
            foreach (var t in DefaultPins.Contexts)
                foreach (var a in Actions.All)
                    if ((a.Targets & t) != 0 && favIds.Contains(a.Id) && d.ContextPins[t].Count < Settings.MaxPins) d.ContextPins[t].Add(a.Id);
            return d;
        }

        public PreviewForm(SettingsData data)
        {
            state = data;
            Text = "Right-click menu preview"; Font = UI.Font; ClientSize = new Size(480, 560); MinimumSize = new Size(380, 360);
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;

            var bar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3, RowCount = 1, Padding = new Padding(14, 10, 14, 6), BackColor = UI.Panel };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = new Label { Text = "Right-click on", AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
            where.Margin = new Padding(0, 2, 0, 0);
            where.Items.AddRange(new[] { "a folder", "folder background", "a drive", "a file" });
            where.SelectedIndex = 0;
            where.SelectedIndexChanged += (s, e) => Build();
            shift.CheckedChanged += (s, e) => Build();
            bar.Controls.Add(label, 0, 0); bar.Controls.Add(where, 1, 0); bar.Controls.Add(shift, 2, 0);

            tree.Dock = DockStyle.Fill; tree.ShowLines = false; tree.FullRowSelect = true; tree.HideSelection = true; tree.ItemHeight = UI.S(26); tree.ShowRootLines = true;

            Controls.Add(tree); Controls.Add(count); Controls.Add(bar);
            Resize += (s, e) => FitNote();
            UI.Apply(this);
            tree.ItemHeight = UI.S(26);
            Build();
        }

        void FitNote()
        {
            count.MaximumSize = new Size(Math.Max(100, ClientSize.Width), 0);
        }

        internal static string Qualifier(Action a)
        {
            if (string.IsNullOrEmpty(a.AppliesTo)) return "";
            if (a.AppliesTo == Modules.ImageQuery) return "   (pictures only)";
            if (a.AppliesTo == Modules.ZipQuery) return "   (zip files only)";
            return "   (matching files only)";
        }

        public static string Render(TreeView tree, MenuPlan plan)
        {
            var open = new HashSet<string>();
            foreach (TreeNode top in tree.Nodes)
                foreach (TreeNode n in top.Nodes)
                    if (n.IsExpanded && n.Tag is string) open.Add((string)n.Tag);
            tree.Font = UI.Font;
            tree.BeginUpdate();
            tree.Nodes.Clear();
            var root = new TreeNode("PS Tools");
            if (plan.OpenEntry) root.Nodes.Add(new TreeNode("Open in PS Tools"));
            foreach (var a in plan.Pinned) root.Nodes.Add(new TreeNode("\u2605 " + a.Label.TrimEnd('.') + Qualifier(a)));
            foreach (var g in plan.Groups)
            {
                var mn = new TreeNode((g.Pinned ? "\u2605 " : "") + g.Name + "  (" + g.Items.Count + ")") { Tag = g.Name };
                foreach (var a in g.Items) mn.Nodes.Add(new TreeNode(a.Label.TrimEnd('.') + Qualifier(a) + (a.Tier == Tier.Elevated ? "   (admin)" : "")));
                root.Nodes.Add(mn);
            }
            tree.Nodes.Add(root);
            root.Expand();
            foreach (TreeNode n in root.Nodes)
                if (n.Tag is string && open.Contains((string)n.Tag)) n.Expand();
            tree.EndUpdate();
            UI.HideHScroll(tree);
            int total = plan.ActionCount;
            string note = total == 0 ? "Nothing is enabled for this kind of item, so PS Tools would not appear." : total + " actions, " + plan.TopLevelCount + " entries at the top level of the PS Tools menu.";
            if (Tweaks.IsWindows11 && !Tweaks.Classic) note += "\nWindows 11 shows this under Show more options until the classic menu is on.";
            return note;
        }

        void Build()
        {
            Target t = kinds[Math.Max(0, where.SelectedIndex)];
            count.Text = Render(tree, MenuPlan.Build(t, shift.Checked, state));
            FitNote();
        }
    }
}
