using System;
using System.Collections.Generic;

namespace PSTools
{
    static partial class Actions
    {
        static IEnumerable<Action> MapActions()
        {
            yield return new Action
            {
                Id = "space-map", Group = "Inspect", Label = "Folder space map...", Targets = Target.Dirs,
                Hint = "Scans the folder and draws what takes up space as a map of colored tiles. Click a folder to open it, right-click a tile to open, show in Explorer, copy the path or move it to the Recycle Bin. Nothing is changed unless you choose the Recycle Bin.",
                Window = paths => new SpaceForm(paths[0])
            };

            yield return new Action
            {
                Id = "undo-history", Group = "Create", Label = "Undo history...", Targets = Target.Background,
                Hint = "Shows the last operations PS Tools made, what undoing each one will do, and lets you undo several in order. Recipes count as one operation.",
                Window = paths => new UndoHistoryForm()
            };
        }
    }
}
