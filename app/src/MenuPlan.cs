using System;
using System.Collections.Generic;
using System.Linq;

namespace PSTools
{
    class PlanGroup
    {
        public string Name;
        public bool Pinned;
        public List<Action> Items = new List<Action>();
        public string CommonAppliesTo;
        public bool AllExtended;
    }

    class MenuPlan
    {
        public Target Context;
        public bool Shift;
        public bool OpenEntry;
        public List<Action> Pinned = new List<Action>();
        public List<PlanGroup> Groups = new List<PlanGroup>();

        public int TopLevelCount { get { return (OpenEntry ? 1 : 0) + Pinned.Count + Groups.Count; } }

        public int ActionCount { get { return Pinned.Count + Groups.Sum(g => g.Items.Count); } }

        public IEnumerable<Action> AllActions { get { return Pinned.Concat(Groups.SelectMany(g => g.Items)); } }

        public static MenuPlan Build(Target t, bool shift)
        {
            return Build(t, shift, Settings.Snapshot());
        }

        public static MenuPlan Build(Target t, bool shift, SettingsData state)
        {
            var plan = new MenuPlan { Context = t, Shift = shift, OpenEntry = t != Target.File };
            var acts = Actions.All.Where(a => (a.Targets & t) != 0 && a.Usable && state.IsEnabled(a.Id) && (shift || !a.Extended)).ToList();
            List<string> pinIds;
            if (!state.ContextPins.TryGetValue(t, out pinIds)) pinIds = new List<string>();
            foreach (var id in pinIds)
            {
                var a = acts.FirstOrDefault(x => x.Id == id);
                if (a != null && !plan.Pinned.Contains(a)) plan.Pinned.Add(a);
            }
            var rest = acts.Where(a => !plan.Pinned.Contains(a));
            foreach (var g in rest.GroupBy(a => a.Group).OrderBy(g => state.GroupPins.Contains(g.Key) ? 0 : 1).ThenBy(g => Modules.Order(g.Key)))
            {
                var pg = new PlanGroup { Name = g.Key, Pinned = state.GroupPins.Contains(g.Key), Items = g.ToList() };
                string common = pg.Items[0].AppliesTo;
                pg.CommonAppliesTo = !string.IsNullOrEmpty(common) && pg.Items.All(a => a.AppliesTo == common) ? common : null;
                pg.AllExtended = pg.Items.All(a => a.Extended);
                plan.Groups.Add(pg);
            }
            return plan;
        }
    }
}
