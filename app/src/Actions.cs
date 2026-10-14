using System;
using System.Collections.Generic;
using System.Linq;

namespace PSTools
{
    static partial class Actions
    {
        public static List<Action> All = Build();

        public static void Rebuild() { All = Build(); }

        static List<Action> Build()
        {
            var l = new List<Action>();
            l.AddRange(CopyModule());
            l.AddRange(QuickModule());
            l.AddRange(CreateModule());
            l.AddRange(Classic());
            l.AddRange(FixModule());
            l.AddRange(LinksModule());
            l.AddRange(DevModule());
            l.AddRange(DiskModule());
            l.AddRange(SystemModule());
            l.AddRange(MapActions());
            l.AddRange(RecipeActions(l));
            l.AddRange(PinnedModule());
            return l.OrderBy(a => Modules.Order(a.Group)).ToList();
        }

        public static Action Find(string id) { return All.FirstOrDefault(a => a.Id == id); }

        public static string Describe(string[] paths)
        {
            return paths.Length == 1 ? paths[0] : paths.Length + " items";
        }
    }
}
