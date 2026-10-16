using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class RecipesForm : Form
    {
        readonly List<Recipe> items = new List<Recipe>();
        readonly ListView recipeList = new ListView();
        readonly TextBox name = new TextBox();
        readonly TextBox desc = new TextBox { Multiline = true };
        readonly CheckBox inMenu = new FCheck { Text = "Show in the right-click menu (Recipes submenu)", AutoSize = true };
        readonly ListView steps = new ListView();
        readonly Label info = new Label { Dock = DockStyle.Bottom, Height = 78, ForeColor = UI.Muted };
        readonly Button addStep = Btn("Add steps...", 120), removeStep = Btn("Remove", 120), up = Btn("Move up", 120), down = Btn("Move down", 120);
        readonly Button copy = Btn("Copy", 62), delete = Btn("Delete", 66);
        string menuBefore;
        bool loading, dirty;

        Recipe Current { get { return recipeList.SelectedItems.Count == 0 ? null : (Recipe)recipeList.SelectedItems[0].Tag; } }

        static Button Btn(string text, int width) { return new FButton { Text = text, Width = width, Height = 30, Margin = new Padding(0, 0, 8, 6) }; }

        static string MenuKey(IEnumerable<Recipe> list)
        {
            return string.Join("|", list.Where(r => r.Menu).Select(r => r.Id + ":" + r.Name).OrderBy(x => x).ToArray());
        }

        public RecipesForm()
        {
            Text = "Recipes"; Font = UI.Font; ClientSize = new Size(960, 620); MinimumSize = new Size(820, 520); StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            foreach (var r in RecipeData.All) items.Add(r.Copy());
            menuBefore = MenuKey(items);
            if (RecipeData.LoadError != null) Program.Msg("Could not read recipes.json:\n" + RecipeData.LoadError + "\n\nIt will be replaced the next time you save.");

            recipeList.Dock = DockStyle.Fill; recipeList.View = View.Details; recipeList.HeaderStyle = ColumnHeaderStyle.None; recipeList.FullRowSelect = true;
            recipeList.MultiSelect = false; recipeList.HideSelection = false; recipeList.BorderStyle = BorderStyle.None; recipeList.Columns.Add("", 230);
            recipeList.SmallImageList = new ImageList { ImageSize = new Size(1, UI.S(30)) };
            recipeList.SelectedIndexChanged += (s, e) => Bind();
            recipeList.Resize += (s, e) => { if (recipeList.Columns.Count > 0) recipeList.Columns[0].Width = Math.Max(120, recipeList.ClientSize.Width); UI.HideHScroll(recipeList); };

            var newBtn = Btn("New recipe", 92);
            newBtn.Click += (s, e) => NewRecipe();
            copy.Click += (s, e) => CopyRecipe();
            delete.Click += (s, e) => DeleteRecipe();
            var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(10, 8, 0, 0) };
            leftButtons.Controls.Add(newBtn); leftButtons.Controls.Add(copy); leftButtons.Controls.Add(delete);

            var left = new Panel { Dock = DockStyle.Left, Width = 270 };
            left.Controls.Add(recipeList); left.Controls.Add(leftButtons);

            name.Dock = DockStyle.Top; name.Font = new Font("Segoe UI", 11f);
            name.TextChanged += (s, e) => { if (loading || Current == null) return; Current.Name = name.Text; recipeList.SelectedItems[0].Text = name.Text; MarkDirty(); };
            desc.Dock = DockStyle.Top; desc.Height = 62; desc.ScrollBars = ScrollBars.Vertical;
            desc.TextChanged += (s, e) => { if (loading || Current == null) return; Current.Desc = desc.Text; MarkDirty(); };
            inMenu.Dock = DockStyle.Top; inMenu.Height = 30; inMenu.Padding = new Padding(0, 6, 0, 0);
            inMenu.CheckedChanged += (s, e) => { if (loading || Current == null) return; Current.Menu = inMenu.Checked; MarkDirty(); };

            steps.Dock = DockStyle.Fill; steps.View = View.Details; steps.FullRowSelect = true; steps.MultiSelect = false; steps.HideSelection = false; steps.BorderStyle = BorderStyle.None;
            steps.Columns.Add("#", 40); steps.Columns.Add("Step", 330); steps.Columns.Add("Group", 110);
            steps.SmallImageList = new ImageList { ImageSize = new Size(1, UI.S(28)) };
            steps.SelectedIndexChanged += (s, e) => UpdateButtons();
            steps.Resize += (s, e) => { if (steps.Columns.Count == 3) steps.Columns[1].Width = Math.Max(120, steps.ClientSize.Width - steps.Columns[0].Width - steps.Columns[2].Width); UI.HideHScroll(steps); };
            steps.DoubleClick += (s, e) => AddSteps();

            addStep.Click += (s, e) => AddSteps();
            removeStep.Click += (s, e) => RemoveStep();
            up.Click += (s, e) => MoveStep(-1);
            down.Click += (s, e) => MoveStep(1);
            var stepButtons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 138, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10, 0, 0, 0) };
            stepButtons.Controls.Add(addStep); stepButtons.Controls.Add(removeStep); stepButtons.Controls.Add(up); stepButtons.Controls.Add(down);

            var stepsHost = new Panel { Dock = DockStyle.Fill };
            stepsHost.Controls.Add(steps); stepsHost.Controls.Add(stepButtons);

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 6) };
            right.Resize += (s, e) => FitInfo();
            right.Controls.Add(stepsHost);
            right.Controls.Add(info);
            right.Controls.Add(new Label { Text = "Steps, run in this order on the same selection", Dock = DockStyle.Top, Height = 26, Font = UI.Bold, Padding = new Padding(0, 6, 0, 0) });
            right.Controls.Add(inMenu);
            right.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
            right.Controls.Add(desc);
            right.Controls.Add(new Label { Text = "Description (optional)", Dock = DockStyle.Top, Height = 22, ForeColor = UI.Muted });
            right.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
            right.Controls.Add(name);
            right.Controls.Add(new Label { Text = "Name", Dock = DockStyle.Top, Height = 22, ForeColor = UI.Muted });

            var examples = Btn("Add examples", 120); examples.Click += (s, e) => AddExamples();
            var import = Btn("Import...", 90); import.Click += (s, e) => Import();
            var export = Btn("Export selected...", 140); export.Click += (s, e) => Export();
            var save = Btn("Save", 90); UI.Primary(save); save.Click += (s, e) => Save();
            var close = Btn("Close", 90); close.Click += (s, e) => Close();
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = UI.Panel };
            var bl = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 400, Padding = new Padding(14, 10, 0, 0) };
            bl.Controls.Add(examples); bl.Controls.Add(import); bl.Controls.Add(export);
            var br = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 230, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 14, 0) };
            br.Controls.Add(close); br.Controls.Add(save);
            bottom.Controls.Add(bl); bottom.Controls.Add(br);

            Controls.Add(right);
            Controls.Add(new Panel { Dock = DockStyle.Left, Width = 1, BackColor = UI.Border });
            Controls.Add(left);
            Controls.Add(bottom);

            FormClosing += (s, e) =>
            {
                if (dirty && !Program.Ask("Close without saving your recipe changes?")) e.Cancel = true;
            };
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.S) { Save(); e.SuppressKeyPress = true; } };

            UI.Apply(this);
            Fill(null);
        }

        void MarkDirty()
        {
            dirty = true;
            Text = "Recipes *";
            RefreshInfo();
        }

        void Fill(Recipe select)
        {
            loading = true;
            recipeList.BeginUpdate();
            recipeList.Items.Clear();
            foreach (var r in items) recipeList.Items.Add(new ListViewItem(r.Name) { Tag = r });
            recipeList.EndUpdate();
            loading = false;
            if (recipeList.Items.Count > 0)
            {
                ListViewItem pick = recipeList.Items[0];
                foreach (ListViewItem it in recipeList.Items) if (it.Tag == select) pick = it;
                pick.Selected = true;
            }
            else Bind();
        }

        void Bind()
        {
            loading = true;
            var r = Current;
            bool has = r != null;
            name.Enabled = desc.Enabled = inMenu.Enabled = steps.Enabled = has;
            name.Text = has ? r.Name : "";
            desc.Text = has ? r.Desc : "";
            inMenu.Checked = has && r.Menu;
            FillSteps(-1);
            loading = false;
            copy.Enabled = delete.Enabled = has;
            UpdateButtons();
            RefreshInfo();
        }

        void FillSteps(int select)
        {
            steps.BeginUpdate();
            steps.Items.Clear();
            var r = Current;
            if (r != null)
                for (int i = 0; i < r.Steps.Count; i++)
                {
                    var a = Actions.Find(r.Steps[i]);
                    bool ok = a != null && RecipeRunner.Eligible(a);
                    var it = new ListViewItem((i + 1).ToString()) { UseItemStyleForSubItems = false };
                    it.SubItems.Add(a != null ? a.Label.TrimEnd('.') : "(missing: " + r.Steps[i] + ")");
                    it.SubItems.Add(a != null ? a.Group : "");
                    if (!ok) it.SubItems[1].ForeColor = UI.Danger;
                    steps.Items.Add(it);
                }
            steps.EndUpdate();
            if (select >= 0 && select < steps.Items.Count) steps.Items[select].Selected = true;
            UpdateButtons();
        }

        void UpdateButtons()
        {
            var r = Current;
            int i = steps.SelectedItems.Count == 0 ? -1 : steps.SelectedItems[0].Index;
            addStep.Enabled = r != null;
            removeStep.Enabled = i >= 0;
            up.Enabled = i > 0;
            down.Enabled = r != null && i >= 0 && i < r.Steps.Count - 1;
        }

        void FitInfo()
        {
            int h = UI.TextHeight(info, 40);
            if (info.Height != h) info.Height = h;
        }

        void RefreshInfo()
        {
            RefreshInfoText();
            FitInfo();
        }

        void RefreshInfoText()
        {
            var r = Current;
            if (r == null) { info.ForeColor = UI.Muted; info.Text = "Create a recipe with New recipe, or load a few with Add examples."; return; }
            Target t; bool multi; string applies;
            string problem = RecipeRunner.Check(r, Actions.All, out t, out multi, out applies);
            if (problem != null) { info.ForeColor = UI.Danger; info.Text = "Cannot run yet: " + problem; return; }
            info.ForeColor = UI.Muted;
            info.Text = "Works on: " + RecipeRunner.Works(t) + (multi ? " (several at once)" : "") + ".\n"
                + "Every step runs on the items you pick. A step does not receive files an earlier step created, but it does see changes the earlier steps made to them.\n"
                + "One confirmation covers the whole recipe, and Undo last reverts all of it.";
        }

        void NewRecipe()
        {
            var r = new Recipe { Name = "New recipe" };
            r.Id = RecipeData.UniqueId(r.Name, items);
            items.Add(r);
            MarkDirty();
            Fill(r);
            name.Focus(); name.SelectAll();
        }

        void CopyRecipe()
        {
            var r = Current; if (r == null) return;
            var c = r.Copy();
            c.Name = r.Name + " copy";
            c.Id = RecipeData.UniqueId(c.Name, items);
            items.Add(c);
            MarkDirty();
            Fill(c);
        }

        void DeleteRecipe()
        {
            var r = Current; if (r == null) return;
            if (!Program.Ask("Delete the recipe \"" + r.Name + "\"?")) return;
            items.Remove(r);
            MarkDirty();
            Fill(null);
        }

        void AddSteps()
        {
            var r = Current; if (r == null) return;
            using (var dlg = new StepPickerForm())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Chosen.Count == 0) return;
                int at = steps.SelectedItems.Count == 0 ? r.Steps.Count : steps.SelectedItems[0].Index + 1;
                r.Steps.InsertRange(at, dlg.Chosen);
                MarkDirty();
                FillSteps(at + dlg.Chosen.Count - 1);
            }
        }

        void RemoveStep()
        {
            var r = Current;
            if (r == null || steps.SelectedItems.Count == 0) return;
            int i = steps.SelectedItems[0].Index;
            r.Steps.RemoveAt(i);
            MarkDirty();
            FillSteps(Math.Min(i, r.Steps.Count - 1));
        }

        void MoveStep(int d)
        {
            var r = Current;
            if (r == null || steps.SelectedItems.Count == 0) return;
            int i = steps.SelectedItems[0].Index, j = i + d;
            if (j < 0 || j >= r.Steps.Count) return;
            string tmp = r.Steps[i]; r.Steps[i] = r.Steps[j]; r.Steps[j] = tmp;
            MarkDirty();
            FillSteps(j);
        }

        void AddExamples()
        {
            var made = new List<Recipe>();
            Action<string, string, string[]> add = (n, d, ids) =>
            {
                if (items.Any(x => x.Name == n)) return;
                var r = new Recipe { Name = n, Desc = d, Steps = new List<string>(ids) };
                r.Id = RecipeData.UniqueId(n, items);
                items.Add(r); made.Add(r);
            };
            add("Sort a messy folder", "Sorts files into Images, Documents, Archives and so on, then removes the folders that end up empty.", new[] { "organize-type", "delete-empty" });
            add("Zip a project and copy where it is", "Creates the zip next to the folder, leaving out .git, node_modules, bin and obj, then copies the folder that holds the zip.", new[] { "zip-smart", "copy-parent" });
            add("Normalize text files", "Converts the selected text files to LF line endings and removes the byte order mark.", new[] { "eol-lf", "bom-remove" });
            if (made.Count == 0) { Program.Msg("The three examples are already in your list."); return; }
            MarkDirty();
            Fill(made[0]);
        }

        void Import()
        {
            using (var d = new OpenFileDialog { Filter = "Recipes (*.json)|*.json|All files|*.*", Title = "Import recipes" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string err;
                var list = RecipeData.Read(d.FileName, out err);
                if (err != null) { Program.Fail("Could not read that file:\n" + err); return; }
                if (list.Count == 0) { Program.Msg("That file has no recipes."); return; }
                Recipe first = null;
                foreach (var r in list)
                {
                    if (items.Any(x => x.Name == r.Name)) r.Name += " (imported)";
                    r.Id = RecipeData.UniqueId(r.Id, items);
                    items.Add(r);
                    if (first == null) first = r;
                }
                MarkDirty();
                Fill(first);
                Program.Msg("Imported " + list.Count + (list.Count == 1 ? " recipe." : " recipes.") + "\n\nRecipes that use actions this version does not have are marked in red until you fix their steps.");
            }
        }

        void Export()
        {
            var r = Current;
            if (r == null) { Program.Msg("Select a recipe to export."); return; }
            using (var d = new SaveFileDialog { Filter = "Recipes (*.json)|*.json", FileName = r.Id + ".recipe.json", Title = "Export recipe" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(d.FileName, RecipeData.Serialize(new[] { r }), new System.Text.UTF8Encoding(false)); }
                catch (Exception ex) { Program.Fail("Could not write the file:\n" + ex.Message); }
            }
        }

        void Save()
        {
            foreach (var r in items)
                if (r.Name.Trim().Length == 0) { Program.Msg("Every recipe needs a name."); return; }
            foreach (var r in items) r.Name = r.Name.Trim();
            try { RecipeData.Save(items); }
            catch (Exception ex)
            {
                Program.Fail("Could not save recipes.json next to PSTools.exe:\n" + ex.Message + "\n\nIf PSTools.exe is in a protected folder such as Program Files, move it to a folder you can write to.");
                return;
            }
            dirty = false;
            Text = "Recipes";
            string after = MenuKey(items);
            bool menuChanged = after != menuBefore;
            menuBefore = after;
            string note = menuChanged && Shell.IsInstalled()
                ? "Recipes saved. Press Apply to Explorer menu to update the right-click menu."
                : "Recipes saved.";
            foreach (Form f in Application.OpenForms)
            {
                var m = f as MainForm;
                if (m != null) m.RefreshRecipes(note);
            }
            RefreshInfo();
            Fill(Current);
        }
    }

    class StepPickerForm : Form
    {
        public readonly List<string> Chosen = new List<string>();
        readonly TextBox search = new TextBox();
        readonly ListView lv = new ListView();
        readonly Label hint = new Label { Dock = DockStyle.Bottom, Height = 70, ForeColor = UI.Muted, Padding = new Padding(14, 8, 14, 0) };

        public StepPickerForm()
        {
            Text = "Add steps"; Font = UI.Font; ClientSize = new Size(640, 520); MinimumSize = new Size(520, 400);
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MaximizeBox = false;

            var top = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(12, 9, 12, 4), BackColor = UI.Panel };
            search.Dock = DockStyle.Fill; ResultsForm.SetCue(search, "Search actions...");
            search.TextChanged += (s, e) => Fill();
            top.Controls.Add(search);

            lv.Dock = DockStyle.Fill; lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = true; lv.BorderStyle = BorderStyle.None;
            lv.Columns.Add("Action", 330); lv.Columns.Add("Group", 90); lv.Columns.Add("Works on", 150);
            lv.SmallImageList = new ImageList { ImageSize = new Size(1, UI.S(26)) };
            lv.SelectedIndexChanged += (s, e) => hint.Text = lv.SelectedItems.Count == 1 ? ((Action)lv.SelectedItems[0].Tag).Hint : "";
            lv.DoubleClick += (s, e) => Accept();
            lv.Resize += (s, e) => { if (lv.Columns.Count == 3) lv.Columns[0].Width = Math.Max(120, lv.ClientSize.Width - lv.Columns[1].Width - lv.Columns[2].Width); UI.HideHScroll(lv); };

            var add = new FButton { Text = "Add", Width = 90, Height = 30 }; UI.Primary(add); add.Click += (s, e) => Accept();
            var cancel = new FButton { Text = "Cancel", Width = 90, Height = 30 }; cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 9, 10, 0), BackColor = UI.Panel };
            bar.Controls.Add(cancel); bar.Controls.Add(add);

            Controls.Add(lv); Controls.Add(hint); Controls.Add(bar); Controls.Add(top);
            AcceptButton = add; CancelButton = cancel;
            Shown += (s, e) => search.Focus();
            UI.Apply(this);
            Fill();
        }

        void Fill()
        {
            string q = search.Text.Trim();
            lv.BeginUpdate();
            lv.Items.Clear();
            foreach (var a in Actions.All.Where(RecipeRunner.Eligible).Where(a => MainForm.Matches(a, q)).OrderBy(a => Modules.Order(a.Group)).ThenBy(a => a.Label))
            {
                var it = new ListViewItem(a.Label.TrimEnd('.') + (a.Confirm != null ? "   (asks first)" : "")) { Tag = a, UseItemStyleForSubItems = false };
                it.SubItems.Add(a.Group);
                it.SubItems.Add(RecipeRunner.Works(a.Targets));
                it.SubItems[1].ForeColor = UI.Muted; it.SubItems[2].ForeColor = UI.Muted;
                lv.Items.Add(it);
            }
            lv.EndUpdate();
            if (lv.Items.Count > 0) lv.Items[0].Selected = true; else hint.Text = "";
        }

        void Accept()
        {
            foreach (ListViewItem it in lv.SelectedItems) Chosen.Add(((Action)it.Tag).Id);
            if (Chosen.Count > 0) DialogResult = DialogResult.OK;
        }
    }
}
