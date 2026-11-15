using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    class SettingsPage : ScrollPage
    {
        readonly MainForm main;
        readonly Label title = new Label { Text = "Settings", AutoSize = false, AccessibleName = "Settings title" };

        readonly V4Card appearance = new V4Card { Title = "Appearance", AccessibleName = "Appearance" };
        readonly FSegment themeSeg = new FSegment { AccessibleName = "Theme" };
        readonly Label themeNote = new Label { AutoSize = false };

        readonly V4Card menu = new V4Card { Title = "Explorer menu", AccessibleName = "Explorer menu" };
        readonly Label menuState = new Label { AutoSize = false, AccessibleName = "Explorer menu state" };
        readonly FButton install = new FButton { Text = "Install Explorer menu", Kind = FButton.Look.Accent, AccessibleName = "Install Explorer menu" };
        readonly FButton remove = new FButton { Text = "Remove Explorer menu", AccessibleName = "Remove Explorer menu" };
        readonly Label favCount = new Label { AutoSize = false, AccessibleName = "Favorites count" };
        readonly FTreeList favList = new FTreeList { ClickActivates = false, AccessibleName = "Favorites in menu" };
        readonly FButton favUp = new FButton { Text = "Move up", AccessibleName = "Move favorite up" };
        readonly FButton favDown = new FButton { Text = "Move down", AccessibleName = "Move favorite down" };
        readonly FButton favRemove = new FButton { Text = "Remove", AccessibleName = "Remove favorite" };
        readonly FSegment previewSeg = new FSegment { AccessibleName = "Preview for" };
        readonly FConsole preview = new FConsole { AccessibleName = "Menu preview" };

        readonly V4Card folderCard = new V4Card { Title = "Actions folder", AccessibleName = "Actions folder" };
        readonly Label folderPath = new Label { AutoSize = false, AccessibleName = "Actions folder path" };
        readonly FButton openFolder = new FButton { Text = "Open folder", AccessibleName = "Open actions folder" };
        readonly FButton reload = new FButton { Text = "Reload", AccessibleName = "Reload actions" };
        readonly FButton unhide = new FButton { Text = "Show hidden actions", AccessibleName = "Show hidden actions" };
        readonly Label problemsHead = new Label { AutoSize = false, AccessibleName = "Problems summary" };
        readonly FConsole problems = new FConsole { AccessibleName = "Problems" };

        readonly V4Card runCard = new V4Card { Title = "Run console", AccessibleName = "Run console settings" };
        readonly Label shellLabel = new Label { Text = "Default shell", AutoSize = false };
        readonly FSegment shellSeg = new FSegment { AccessibleName = "Default shell" };
        readonly FCheck confirm = new FCheck { Text = "Ask before running commands that change files", AccessibleName = "Confirm commands that change files" };
        readonly Label historyNote = new Label { AutoSize = false, AccessibleName = "History size" };
        readonly FButton importHistory = new FButton { Text = "Import PowerShell history", AccessibleName = "Import PowerShell history" };
        readonly FButton clearHistory = new FButton { Text = "Clear run history", AccessibleName = "Clear run history" };

        readonly V4Card data = new V4Card { Title = "Data and privacy", AccessibleName = "Data and privacy" };
        readonly Label dataPath = new Label { AutoSize = false, AccessibleName = "Data folder" };
        readonly Label dataNote = new Label { AutoSize = false };
        readonly FButton openData = new FButton { Text = "Open data folder", AccessibleName = "Open data folder" };
        readonly FButton uninstall = new FButton { Text = "Full uninstall...", AccessibleName = "Full uninstall" };

        readonly V4Card about = new V4Card { Title = "About", AccessibleName = "About" };
        readonly Label aboutText = new Label { AutoSize = false, AccessibleName = "About text" };
        readonly FButton github = new FButton { Text = "Open GitHub page", AccessibleName = "Open GitHub page" };
        readonly FButton setup = new FButton { Text = "Setup guide", AccessibleName = "Setup guide" };

        readonly V4Card[] cards;
        bool loading;
        List<string> favIds = new List<string>();

        public SettingsPage(MainForm owner)
        {
            main = owner;
            Dock = DockStyle.Fill;
            AccessibleName = "Settings";
            title.Font = UI.Title;
            canvas.Controls.Add(title);
            cards = new[] { appearance, menu, folderCard, runCard, data, about };
            foreach (var c in cards) canvas.Controls.Add(c);

            themeSeg.Items.AddRange(new[] { "System", "Light", "Dark" });
            appearance.Controls.Add(themeSeg);
            appearance.Controls.Add(themeNote);
            themeNote.Text = "Changes apply the next time PS Tools starts. The accent colour follows Windows.";

            foreach (var c in new Control[] { menuState, install, remove, favCount, favList, favUp, favDown, favRemove, previewSeg, preview }) menu.Controls.Add(c);
            previewSeg.Items.AddRange(new[] { "Folder", "Empty space", "File" });
            preview.ReadOnly = true;
            foreach (var c in new Control[] { folderPath, openFolder, reload, unhide, problemsHead, problems }) folderCard.Controls.Add(c);
            problems.ReadOnly = true;
            foreach (var c in new Control[] { shellLabel, shellSeg, confirm, historyNote, importHistory, clearHistory }) runCard.Controls.Add(c);
            shellSeg.Items.AddRange(new[] { "PowerShell 5.1", "PowerShell 7", "cmd" });
            foreach (var c in new Control[] { dataPath, dataNote, openData, uninstall }) data.Controls.Add(c);
            foreach (var c in new Control[] { aboutText, github, setup }) about.Controls.Add(c);

            themeSeg.SelectedChanged += (s, e) =>
            {
                if (loading) return;
                Settings.SetTheme(themeSeg.SelectedIndex == 1 ? "light" : themeSeg.SelectedIndex == 2 ? "dark" : "auto");
                main.SetStatus("Theme saved. It applies the next time PS Tools starts.");
            };
            install.Click += (s, e) => InstallNow();
            remove.Click += (s, e) => RemoveNow();
            favList.SelectionChanged += (s, e) => UpdateFavButtons();
            favUp.Click += (s, e) => MoveFav(-1);
            favDown.Click += (s, e) => MoveFav(1);
            favRemove.Click += (s, e) => RemoveFav();
            previewSeg.SelectedChanged += (s, e) => { if (!loading) FillPreview(); };
            openFolder.Click += (s, e) => OpenDir(Path.Combine(Journal.Dir, "actions"), true);
            reload.Click += (s, e) => { ActionCatalog.Reset(); main.FavoritesChanged(); Refresh2(); main.SetStatus("Actions reloaded."); };
            unhide.Click += (s, e) => { foreach (string id in Settings.HiddenIds) Settings.SetHidden(id, false); Shell.RefreshIfInstalled(); main.FavoritesChanged(); Refresh2(); main.SetStatus("Hidden actions are visible again."); };
            shellSeg.SelectedChanged += (s, e) =>
            {
                if (loading) return;
                RunHistory.LastShell = shellSeg.SelectedIndex == 2 ? "cmd" : shellSeg.SelectedIndex == 1 ? "pwsh" : "ps";
                RunHistory.Save();
            };
            confirm.CheckedChanged += (s, e) => { if (!loading) Settings.ConfirmModifies = confirm.Checked; };
            importHistory.Click += (s, e) => ImportHistory();
            clearHistory.Click += (s, e) => { if (Program.Ask("Clear the Run console history?")) { RunHistory.Clear(); Refresh2(); main.FavoritesChanged(); } };
            openData.Click += (s, e) => OpenDir(Journal.Dir, true);
            uninstall.Click += (s, e) => FullUninstall();
            github.Click += (s, e) => { try { Process.Start("https://github.com/datakid/ps-tools-"); } catch { } };
            setup.Click += (s, e) => new FirstRunForm(true).Show(main);
            Resize += (s, e) => DoLayout();
        }

        static void OpenDir(string dir, bool create)
        {
            try
            {
                if (create && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch { }
        }

        public void Refresh2()
        {
            loading = true;
            string theme = Settings.Theme;
            themeSeg.SelectedIndex = theme == "light" ? 1 : theme == "dark" ? 2 : 0;
            bool installed = Shell.IsInstalled();
            menuState.Text = installed ? "The Explorer menu is installed. Favorite changes apply right away." : "The Explorer menu is not installed.";
            install.Visible = !installed;
            remove.Visible = installed;
            favIds = Settings.Favorites;
            var specs = ActionLauncher.ResolveIds(favIds);
            var rows = specs.Select(s => ActionLauncher.RowFor(s, true, "")).ToList();
            favList.SetRows(rows, rows.Count > 0 ? 0 : -1);
            int shown = Math.Min(rows.Count, Shell.MaxFavoritesPerContext);
            favCount.Text = rows.Count == 0 ? "No favorites yet. Press Ctrl+D on any action to add it." : "Favorites in menu: " + shown + " of " + Shell.MaxFavoritesPerContext + (rows.Count > shown ? "  (" + (rows.Count - shown) + " more are kept but not shown)" : "");
            folderPath.Text = Path.Combine(Journal.Dir, "actions");
            var cat = ActionCatalog.Current;
            var probs = cat.UserProblems.ToList();
            int hidden = Settings.HiddenIds.Count;
            problemsHead.Text = (probs.Count == 0 ? "No problems in your actions." : "Problems (" + probs.Count + ")") + (hidden > 0 ? "     Hidden actions: " + hidden : "");
            unhide.Enabled = hidden > 0;
            problems.Text = probs.Count == 0 ? "" : string.Join("\n", probs.Select(p => p.File + ":" + p.Line + " - " + p.Reason).ToArray());
            shellSeg.SelectedIndex = RunHistory.LastShell == "cmd" ? 2 : RunHistory.LastShell == "pwsh" ? 1 : 0;
            confirm.Checked = Settings.ConfirmModifies;
            historyNote.Text = "History keeps the last " + RunHistory.Max + " commands (" + RunHistory.All.Count + " saved). Ctrl+R in the console searches it.";
            dataPath.Text = Journal.Dir;
            string pending = Tweaks.Pending();
            dataNote.Text = "Settings, undo data and run history stay on this PC. Nothing is sent anywhere." + (pending.Length > 0 ? " Windows settings changed: " + pending + "." : "");
            aboutText.Text = "PS Tools " + UI.Version + "\nPSTools.exe: " + Application.ExecutablePath + "\n" + ActionCatalog.Current.All.Count + " actions loaded.";
            if (previewSeg.SelectedIndex < 0) previewSeg.SelectedIndex = 0;
            loading = false;
            FillPreview();
            UpdateFavButtons();
            DoLayout();
        }

        void UpdateFavButtons()
        {
            int i = favList.Selected;
            favUp.Enabled = i > 0;
            favDown.Enabled = i >= 0 && i < favList.Rows.Count - 1;
            favRemove.Enabled = i >= 0;
        }

        string SelectedFavId()
        {
            var r = favList.SelectedRow;
            var s = r == null ? null : r.Tag as ActionSpec;
            return s == null ? null : s.Id;
        }

        void MoveFav(int delta)
        {
            string id = SelectedFavId();
            if (id == null) return;
            Settings.MoveFavorite(id, delta);
            Shell.RefreshIfInstalled();
            int keep = favList.Selected + delta;
            Refresh2();
            favList.Select(Math.Max(0, Math.Min(favList.Rows.Count - 1, keep)));
            main.FavoritesChanged();
        }

        void RemoveFav()
        {
            string id = SelectedFavId();
            if (id == null) return;
            Settings.ToggleFavorite(id);
            Shell.RefreshIfInstalled();
            Refresh2();
            main.FavoritesChanged();
        }

        void FillPreview()
        {
            try
            {
                ContextKind kind = previewSeg.SelectedIndex == 1 ? ContextKind.Background : previewSeg.SelectedIndex == 2 ? ContextKind.File : ContextKind.Folder;
                var paths = kind == ContextKind.Background ? new string[0] : kind == ContextKind.File ? new[] { Path.Combine(Environment.CurrentDirectory, "example.txt") } : new[] { Environment.CurrentDirectory };
                var q = PowerMenuLauncher.QueryFor(kind, paths);
                q.Shift = false;
                if (kind == ContextKind.Background) q.BackgroundFolder = main.Folder;
                var tree = MenuTree.Build(q);
                preview.Text = MenuTree.Dump(tree);
            }
            catch (Exception ex) { preview.Text = "The preview could not be built: " + ex.Message; }
        }

        void InstallNow()
        {
            try
            {
                Settings.MarkConfigured();
                Shell.Install();
                main.SetStatus("Explorer menu installed.");
            }
            catch (Exception ex) { Program.Msg("Could not install the menu:\n" + ex.Message); }
            Refresh2();
            main.FavoritesChanged();
        }

        void RemoveNow()
        {
            try { Shell.RemoveMenu(); main.SetStatus("Explorer menu removed."); }
            catch (Exception ex) { Program.Msg("Could not remove the menu:\n" + ex.Message); }
            Refresh2();
            main.FavoritesChanged();
        }

        void ImportHistory()
        {
            var lines = RunHistory.ImportPsReadLine(Directory.Exists(main.Folder) ? main.Folder : Environment.CurrentDirectory);
            main.SetStatus(lines.Count == 0 ? "No PowerShell history was found to import." : "Imported " + lines.Count + " commands.");
            Refresh2();
            main.FavoritesChanged();
        }

        void FullUninstall()
        {
            if (!Program.Ask("Remove the Explorer menu, delete PS Tools' settings, undo data and temporary files, and restore any Windows settings it changed?\n\nPSTools.exe itself stays until you delete it.")) return;
            Program.Msg(Shell.UninstallAll());
            Refresh2();
        }

        static int Block(Control c, int x, int y, int w, int h)
        {
            c.SetBounds(x, y, w, h);
            return y + h;
        }

        static void Line(int x, int y, int gap, params Control[] items)
        {
            foreach (var c in items)
            {
                if (!c.Visible) continue;
                int w = Math.Max(c.GetPreferredSize(Size.Empty).Width, UI.S(96));
                c.SetBounds(x, y, w, Tok.ButtonHeight);
                x += w + gap;
            }
        }

        public void DoLayout()
        {
            int w = Math.Max(UI.S(300), ContentWidth - Tok.S2);
            int gap = Tok.S3;
            int pad = Tok.S3;
            int hh = V4Card.HeaderHeight;
            int inner = w - pad * 2;
            int cap = UI.S(22);
            int row = Tok.ButtonHeight;
            title.SetBounds(0, 0, w, UI.S(40));
            int y = UI.S(48);

            appearance.SetBounds(0, y, w, hh + row + Tok.S2 + cap * 2 + pad);
            themeSeg.SetBounds(pad, hh, Math.Min(inner, UI.S(320)), row);
            themeNote.SetBounds(pad, hh + row + Tok.S2, inner, cap * 2);
            y += appearance.Height + gap;

            int favRows = Math.Max(1, Math.Min(Shell.MaxFavoritesPerContext, favList.Rows.Count));
            int favH = favList.Rows.Count == 0 ? 0 : favRows * Tok.RowHeight;
            int previewH = UI.S(190);
            int ym = hh;
            menuState.SetBounds(pad, ym, inner, cap);
            ym += cap + Tok.S1;
            Line(pad, ym, Tok.S2, install, remove);
            ym += row + Tok.S3;
            favCount.SetBounds(pad, ym, inner, cap);
            ym += cap + Tok.S1;
            favList.Visible = favH > 0;
            if (favH > 0) { favList.SetBounds(pad, ym, inner, favH); ym += favH + Tok.S2; }
            Line(pad, ym, Tok.S2, favUp, favDown, favRemove);
            ym += row + Tok.S3;
            previewSeg.SetBounds(pad, ym, Math.Min(inner, UI.S(360)), row);
            ym += row + Tok.S2;
            preview.SetBounds(pad, ym, inner, previewH);
            ym += previewH + pad;
            menu.SetBounds(0, y, w, ym);
            y += menu.Height + gap;

            int yf = hh;
            folderPath.SetBounds(pad, yf, inner, cap);
            yf += cap + Tok.S2;
            Line(pad, yf, Tok.S2, openFolder, reload, unhide);
            yf += row + Tok.S2;
            problemsHead.SetBounds(pad, yf, inner, cap);
            yf += cap + Tok.S1;
            bool hasProblems = problems.Text.Length > 0;
            problems.Visible = hasProblems;
            if (hasProblems) { problems.SetBounds(pad, yf, inner, UI.S(90)); yf += UI.S(90) + Tok.S1; }
            folderCard.SetBounds(0, y, w, yf + pad);
            y += folderCard.Height + gap;

            int yr = hh;
            shellLabel.SetBounds(pad, yr + UI.S(4), UI.S(120), cap);
            shellSeg.SetBounds(pad + UI.S(128), yr, Math.Min(inner - UI.S(128), UI.S(380)), row);
            yr += row + Tok.S3;
            confirm.SetBounds(pad, yr, inner, UI.S(28));
            yr += UI.S(28) + Tok.S2;
            historyNote.SetBounds(pad, yr, inner, cap * 2);
            yr += cap * 2 + Tok.S1;
            Line(pad, yr, Tok.S2, importHistory, clearHistory);
            yr += row + pad;
            runCard.SetBounds(0, y, w, yr);
            y += runCard.Height + gap;

            int yd = hh;
            dataPath.SetBounds(pad, yd, inner, cap);
            yd += cap + Tok.S1;
            dataNote.SetBounds(pad, yd, inner, cap * 2);
            yd += cap * 2 + Tok.S1;
            Line(pad, yd, Tok.S2, openData, uninstall);
            yd += row + pad;
            data.SetBounds(0, y, w, yd);
            y += data.Height + gap;

            int ya = hh;
            aboutText.SetBounds(pad, ya, inner, cap * 3);
            ya += cap * 3 + Tok.S2;
            Line(pad, ya, Tok.S2, github, setup);
            ya += row + pad;
            about.SetBounds(0, y, w, ya);
            y += about.Height + gap;

            foreach (var c in cards) c.Skin();
            canvas.SetBounds(0, -ScrollOffset, w, y);
            SetContentHeight(y + Tok.S3);
            foreach (var l in new[] { themeNote, menuState, favCount, folderPath, problemsHead, shellLabel, historyNote, dataPath, dataNote, aboutText })
            {
                l.Font = Tok.Body;
                l.ForeColor = (l == themeNote || l == historyNote || l == dataNote) ? Tok.TextSecondary : Tok.Text;
            }
        }
    }
}
