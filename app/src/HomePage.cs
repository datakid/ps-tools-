using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class HomePage : Panel
    {
        readonly MainForm main;
        readonly Panel canvas = new Panel();
        readonly Label title = new Label { Text = "Home", AutoSize = false, AccessibleName = "Home title" };
        readonly V4Card folderCard = new V4Card { Title = "Working folder", AccessibleName = "Working folder" };
        readonly FSearchBox folderField = new FSearchBox("Folder path") { ShowGlyph = false, AccessibleName = "Working folder path" };
        public TextBox FolderBox { get { return folderField.Input; } }
        readonly FButton browse = new FButton { Text = "Browse...", AccessibleName = "Browse" };
        readonly FButton openExplorer = new FButton { Text = "Open", AccessibleName = "Open in Explorer" };
        readonly Label dropHint = new Label { Text = "You can also drop a folder anywhere on this window.", AutoSize = false };
        readonly RowCard suggested = new RowCard("Suggested here", "Nothing to suggest for this folder.");
        readonly RowCard favorites = new RowCard("Favorites", "No favorites yet. Press Ctrl+D on any action to add it.");
        readonly RowCard recent = new RowCard("Recent", "Nothing run yet.");
        readonly RowCard start = new RowCard("Get started", "") { MaxRows = 4 };
        readonly FButton dismiss = new FButton { Text = "Dismiss", AccessibleName = "Dismiss get started" };
        readonly RowCard tools = new RowCard("More tools", "") { MaxRows = 6 };
        List<Suggestion> suggestions = new List<Suggestion>();

        public HomePage(MainForm owner)
        {
            main = owner;
            Dock = DockStyle.Fill;
            AutoScroll = true;
            AccessibleName = "Home";
            title.Font = UI.Title;
            canvas.Location = Point.Empty;
            Controls.Add(canvas);
            canvas.Controls.Add(title);
            folderField.Input.AccessibleName = "Working folder path";
            folderCard.Controls.Add(folderField);
            folderCard.Controls.Add(browse);
            folderCard.Controls.Add(openExplorer);
            folderCard.Controls.Add(dropHint);
            canvas.Controls.Add(folderCard);
            foreach (var c in new Control[] { suggested, favorites, recent, start, tools }) canvas.Controls.Add(c);
            start.Controls.Add(dismiss);
            dropHint.Font = Tok.Caption;
            browse.Click += (s, e) => main.BrowseFolder();
            openExplorer.Click += (s, e) => { if (Directory.Exists(main.Folder)) System.Diagnostics.Process.Start("explorer.exe", "\"" + main.Folder + "\""); };
            FolderBox.TextChanged += (s, e) => main.SetFolderFromBox(FolderBox.Text);
            FolderBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            FolderBox.AutoCompleteSource = AutoCompleteSource.FileSystemDirectories;
            suggested.RowActivated += r => RunRow(r);
            favorites.RowActivated += r => RunRow(r);
            recent.RowActivated += r => RunRow(r);
            start.RowActivated += r => { var a = r.Tag as System.Action; if (a != null) a(); };
            tools.RowActivated += r => { var a = r.Tag as System.Action; if (a != null) a(); };
            dismiss.Click += (s, e) => { Settings.StartedDismissed = true; Fill(); DoLayout(); };
            Resize += (s, e) => DoLayout();
        }

        void RunRow(PmRow r)
        {
            var spec = r.Tag as ActionSpec;
            if (spec != null) { ActionLauncher.Run(spec, main.Folder, main, main.SetStatus); main.AfterRun(); return; }
            var entry = r.Tag as RunEntry;
            if (entry != null) ActionLauncher.OpenConsole(main, main.Folder, entry.Command);
        }

        public void SetFolderText(string text)
        {
            if (FolderBox.Text != text) FolderBox.Text = text;
        }

        public void SetSuggestions(List<Suggestion> list)
        {
            suggestions = list;
            Fill();
            DoLayout();
        }

        public void Fill()
        {
            var sug = new List<PmRow>();
            foreach (var s in suggestions.Take(6))
            {
                var spec = ActionCatalog.Current.Find(s.ActionId);
                if (spec == null || spec.UserHidden) continue;
                sug.Add(ActionLauncher.RowFor(spec, Settings.IsFavorite(spec.Id), s.Reason));
            }
            suggested.SetRows(sug);

            var fav = ActionLauncher.ResolveIds(Settings.Favorites).Select(s => ActionLauncher.RowFor(s, true, "")).ToList();
            favorites.MaxRows = 8;
            favorites.SetRows(fav);

            var rec = new List<PmRow>();
            var seen = new HashSet<string>();
            foreach (var s in ActionLauncher.ResolveIds(Settings.UsedIn("background").Concat(Settings.UsedIn("folder"))).Take(4))
                if (seen.Add(s.Id)) rec.Add(ActionLauncher.RowFor(s, Settings.IsFavorite(s.Id), "Action"));
            foreach (var e in RunHistory.All.Take(8 - rec.Count))
                rec.Add(new PmRow { Title = e.Command.Replace("\r", " ").Replace("\n", " "), Tag = e, Secondary = "Run console", Glyph = "\uE756" });
            recent.MaxRows = 8;
            recent.SetRows(rec);

            var steps = new List<PmRow>();
            bool dismissed = Settings.StartedDismissed;
            if (!dismissed)
            {
                steps.Add(new PmRow { Title = "1. Try the Power menu", Secondary = "Ctrl+K", Glyph = "\uE721", Tag = (System.Action)(() => main.OpenPalette()) });
                steps.Add(new PmRow { Title = "2. Save a command as an action", Secondary = "Run console", Glyph = "\uE74E", Tag = (System.Action)(() => ActionLauncher.OpenConsole(main, main.Folder, "")) });
                if (!Shell.IsInstalled()) steps.Add(new PmRow { Title = "3. Add PS Tools to the Explorer menu", Secondary = "Settings", Glyph = "\uE713", Tag = (System.Action)(() => main.ShowPage(MainForm.PageSettings)) });
            }
            start.Visible = steps.Count > 0;
            dismiss.Visible = steps.Count > 0;
            start.SetRows(steps);

            var more = new List<PmRow>
            {
                new PmRow { Title = "Recipes", Secondary = "Several steps in one click", Glyph = "\uE8FD", Tag = (System.Action)(() => new RecipesForm().Show(main)) },
                new PmRow { Title = "Space map", Secondary = "What takes room in this folder", Glyph = "\uE8B7", Tag = (System.Action)(() => main.OpenSpaceMap()) },
                new PmRow { Title = "Undo history", Secondary = "Everything PS Tools changed", Glyph = "\uE81C", Tag = (System.Action)(() => new UndoHistoryForm().Show(main)) },
                new PmRow { Title = "Setup guide", Secondary = "Explorer menu and Windows 11", Glyph = "\uE897", Tag = (System.Action)(() => new FirstRunForm(true).Show(main)) }
            };
            tools.SetRows(more);
        }

        public void DoLayout()
        {
            int pad = Tok.S4;
            int viewW = Math.Max(UI.S(300), ClientSize.Width - (VerticalScroll.Visible ? 0 : 0));
            int w = Math.Max(UI.S(280), viewW - pad);
            int gap = Tok.S3;
            title.SetBounds(0, 0, w, UI.S(40));
            int y = UI.S(48);
            folderCard.SetBounds(0, y, w, UI.S(132));
            var fi = folderCard.Inner;
            int btnW = UI.S(104);
            folderField.SetBounds(fi.X, V4Card.HeaderHeight, Math.Max(100, fi.Width - btnW * 2 - Tok.S2 * 2), Tok.InputHeight);
            browse.SetBounds(folderField.Right + Tok.S2, V4Card.HeaderHeight - 1, btnW, UI.S(32));
            openExplorer.SetBounds(browse.Right + Tok.S2, V4Card.HeaderHeight - 1, btnW, UI.S(32));
            dropHint.SetBounds(fi.X, V4Card.HeaderHeight + UI.S(40), fi.Width, UI.S(20));
            y += folderCard.Height + gap;
            var cards = new List<RowCard> { suggested, favorites, recent, start, tools }.Where(c => c.Visible).ToList();
            bool two = main.ClientSize.Width >= UI.S(1000) && w >= UI.S(560);
            int cols = two ? 2 : 1;
            int colW = (w - gap * (cols - 1)) / cols;
            var heights = new int[cols];
            for (int i = 0; i < cols; i++) heights[i] = y;
            foreach (var c in cards)
            {
                int col = 0;
                for (int i = 1; i < cols; i++) if (heights[i] < heights[col]) col = i;
                c.Width = colW;
                c.Relayout();
                c.Left = col * (colW + gap);
                c.Top = heights[col];
                c.Height = c.Needed;
                c.Relayout();
                heights[col] += c.Height + gap;
            }
            if (start.Visible) dismiss.SetBounds(start.Width - UI.S(96) - Tok.S3, UI.S(8), UI.S(96), UI.S(32));
            folderCard.Skin();
            foreach (var c in cards) c.Skin();
            start.Skin();
            int total = heights.Max();
            canvas.SetBounds(0, 0, w, total);
            AutoScrollMinSize = new Size(0, total + Tok.S3);
        }
    }
}
