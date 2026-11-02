using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    static class PowerMenuLauncher
    {
        public static MenuQuery QueryFor(ContextKind ctx, string[] paths)
        {
            Settings.EnsureLoaded();
            return new MenuQuery
            {
                Context = ctx, Paths = paths, Catalog = ActionCatalog.Current, Shift = Control.ModifierKeys == Keys.Shift,
                Favorites = Settings.Favorites, Recent = Settings.UsedIn(SpecRules.ContextName(ctx))
            };
        }

        public static PowerMenuOptions Options(string summary, string ctxName)
        {
            return new PowerMenuOptions
            {
                Summary = summary,
                ToggleFavorite = id => { bool now = Settings.ToggleFavorite(id); Shell.RefreshIfInstalled(); return now; },
                NoteUsed = id => Settings.NoteUsed(ctxName, id)
            };
        }

        public static int Run(bool background, string path)
        {
            string[] paths;
            if (background) paths = new string[0];
            else
            {
                paths = Collector.Gather("menu", path, 160);
                if (paths == null) return 0;
            }
            var ctx = MenuTree.ContextFor(paths, background);
            if (ctx == ContextKind.None) ctx = ContextKind.Folder;
            var q = QueryFor(ctx, paths);
            string summary = background ? path : null;
            var pm = new PowerMenu(q, Options(summary, SpecRules.ContextName(ctx == (ContextKind.File | ContextKind.Folder) ? ContextKind.File : ctx)));
            Application.Run(pm);
            if (pm.ToRun != null) pm.ToRun();
            return 0;
        }
    }
}
