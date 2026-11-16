using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    static class ActionLauncher
    {
        public static ActionSpec[] ResolveIds(IEnumerable<string> ids)
        {
            var list = new List<ActionSpec>();
            foreach (string id in ids)
            {
                var s = ActionCatalog.Current.Find(id);
                if (s != null && !s.UserHidden && !list.Contains(s)) list.Add(s);
            }
            return list.ToArray();
        }

        public static bool NeedsFile(ActionSpec s)
        {
            return (s.When & (ContextKind.Folder | ContextKind.Background | ContextKind.Drive)) == 0 && (s.When & ContextKind.File) != 0;
        }

        public static void Run(ActionSpec s, string folder, Form owner, Action<string> status)
        {
            if (s.Inputs.Count > 0)
            {
                var q = PowerMenuLauncher.QueryFor(ContextKind.Background, new string[0]);
                q.BackgroundFolder = folder;
                var opt = PowerMenuLauncher.Options(folder, "background");
                opt.Location = new Point(owner.Left + (owner.Width - UI.S(420)) / 2, owner.Top + Math.Max(24, owner.Height / 8));
                var pm = new PowerMenu(q, opt);
                pm.FormClosed += (s2, e2) => { if (pm.ToRun != null) owner.BeginInvoke(pm.ToRun); };
                pm.Show(owner);
                pm.ShowInputsFor(s);
                return;
            }
            string[] paths;
            if (NeedsFile(s))
            {
                using (var d = new OpenFileDialog { Multiselect = true, InitialDirectory = Directory.Exists(folder) ? folder : null })
                {
                    if (d.ShowDialog(owner) != DialogResult.OK) return;
                    paths = d.FileNames;
                }
            }
            else
            {
                if (!Directory.Exists(folder)) { status("Folder not found: " + folder); return; }
                paths = new[] { folder };
            }
            Settings.NoteUsed("background", s.Id);
            Executor.Run(s, paths);
        }

        public static void OpenConsole(Form owner, string folder, string command)
        {
            string dir = Directory.Exists(folder) ? folder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var f = new RunForm(dir, command ?? "", "background");
            f.Show(owner);
        }

        public static PmRow RowFor(ActionSpec s, bool favorite, string secondary)
        {
            return new PmRow { Title = s.Title, Node = null, Tag = s, Favorite = favorite, Risk = s.Risk, Admin = s.Admin, Secondary = secondary ?? "", Glyph = PowerMenu.GlyphFor(s), Extended = s.Extended };
        }
    }
}
