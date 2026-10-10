using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PSTools
{
    class LibCmd
    {
        public string Id, CatId, CatLabel, Tags, Title, Desc, Code, Risk, Note, Requires;
        public bool Admin;
    }

    static class LibraryData
    {
        public static string JsonPath { get { return Path.Combine(Program.ExeDir, "commands.json"); } }

        static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? v.ToString() : ""; }

        public static List<LibCmd> Load(out string error)
        {
            error = null;
            var cmds = new List<LibCmd>();
            if (!File.Exists(JsonPath)) return cmds;
            try
            {
                var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = (Dictionary<string, object>)js.DeserializeObject(File.ReadAllText(JsonPath, Encoding.UTF8));
                var cats = new Dictionary<string, string>();
                object o;
                if (root.TryGetValue("categories", out o))
                    foreach (Dictionary<string, object> c in (IEnumerable)o) cats[S(c, "id")] = S(c, "label");
                foreach (Dictionary<string, object> c in (IEnumerable)root["commands"])
                {
                    string cat = S(c, "category"); string label;
                    var tags = c.ContainsKey("tags") ? string.Join(" ", ((IEnumerable)c["tags"]).Cast<object>()) : "";
                    cmds.Add(new LibCmd
                    {
                        Id = S(c, "id"), CatId = cat, CatLabel = cats.TryGetValue(cat, out label) ? label : cat, Tags = tags, Title = S(c, "title"),
                        Desc = S(c, "description"), Code = S(c, "command"), Risk = S(c, "risk"), Note = S(c, "note"), Requires = S(c, "requires"),
                        Admin = c.ContainsKey("admin") && c["admin"] is bool && (bool)c["admin"]
                    });
                }
            }
            catch (Exception ex) { error = ex.Message; }
            return cmds;
        }

        public static void OpenShell(string folder, LibCmd c)
        {
            string intro = "Write-Host 'Command copied. Press Ctrl+V to paste it, review, then Enter.' -ForegroundColor Cyan";
            if (c != null && c.Risk == "destructive") intro += "; Write-Host 'This command deletes or overwrites data. Read it first.' -ForegroundColor Yellow";
            try { Launch.PowerShell(folder, intro, c != null && c.Admin); } catch { }
        }
    }

    static partial class Actions
    {
        static IEnumerable<Action> PinnedModule()
        {
            if (Settings.Pins.Count == 0) yield break;
            string err;
            foreach (var c in LibraryData.Load(out err).Where(x => Settings.Pins.Contains(x.Id)))
            {
                var cmd = c;
                yield return new Action
                {
                    Id = "lib-" + cmd.Id, Group = "Library", Label = cmd.Title, Targets = Target.Dirs, Fast = true,
                    Tier = cmd.Admin ? Tier.Elevated : Tier.Instant,
                    Hint = cmd.Desc + "\n\nCopies the command and opens PowerShell in the folder. It never runs by itself: paste, review, press Enter.",
                    Run = (paths, ctx) =>
                    {
                        Clip.Set(cmd.Code);
                        LibraryData.OpenShell(DirOf(paths[0]), cmd);
                        return new Result { Summary = "Command copied. Paste it in the PowerShell window." };
                    }
                };
            }
        }
    }

    class LibraryForm : Form
    {
        readonly string folder;
        readonly List<LibCmd> cmds;
        readonly ListView lv = new ListView();
        readonly TextBox search = new TextBox(), code = new TextBox();
        readonly Label desc = new Label();
        readonly Button pin = new Button { Text = "Pin to menu", Width = 110, Height = 30 };

        public LibraryForm(string folder)
        {
            this.folder = folder;
            Text = "Command library  -  " + folder; Font = UI.Font; ClientSize = new Size(1000, 620); StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            string err;
            cmds = LibraryData.Load(out err);
            if (err != null) Program.Msg("Could not read commands.json:\n" + err);

            var top = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 9, 10, 4), BackColor = UI.Panel };
            search.Dock = DockStyle.Fill; ResultsForm.SetCue(search, "Search commands, tags, categories...");
            search.TextChanged += (s, e) => Fill(); top.Controls.Add(search);

            var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
            Load += (s, e) => { try { split.SplitterDistance = 440; } catch { } };
            lv.Dock = DockStyle.Fill; lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false; lv.BorderStyle = BorderStyle.None;
            lv.Columns.Add("Command", 290); lv.Columns.Add("Risk", 80); lv.Columns.Add("", 50);
            lv.SmallImageList = new ImageList { ImageSize = new Size(1, 26) };
            lv.SelectedIndexChanged += (s, e) => Show_();
            lv.DoubleClick += (s, e) => Open();
            split.Panel1.Controls.Add(lv);

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 10) };
            desc.Dock = DockStyle.Top; desc.Height = 110;
            code.Dock = DockStyle.Fill; code.Multiline = true; code.ScrollBars = ScrollBars.Both; code.WordWrap = false; code.Tag = "code";
            code.Font = UI.Mono; code.BackColor = Color.FromArgb(30, 30, 30); code.ForeColor = Color.FromArgb(225, 225, 225);
            var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(0, 8, 0, 0) };
            var copy = new Button { Text = "Copy", Width = 90, Height = 30 }; copy.Click += (s, e) => { if (code.Text.Length > 0) Clip.Set(code.Text); };
            var shell = new Button { Text = "Open PowerShell here", Width = 170, Height = 30 };
            UI.Primary(shell); shell.Click += (s, e) => Open();
            var edit = new Button { Text = "Edit commands.json", Width = 150, Height = 30 };
            edit.Click += (s, e) => { try { Process.Start("notepad.exe", "\"" + LibraryData.JsonPath + "\""); } catch { } };
            pin.Click += (s, e) => TogglePin();
            btns.Controls.Add(shell); btns.Controls.Add(copy); btns.Controls.Add(pin); btns.Controls.Add(edit);
            var hint = new Label { Dock = DockStyle.Bottom, Height = 36, ForeColor = UI.Muted, Text = "Edit the code above before running. Open PowerShell here runs nothing: the command is on your clipboard, so press Ctrl+V, review, then Enter." };
            right.Controls.Add(code); right.Controls.Add(desc); right.Controls.Add(hint); right.Controls.Add(btns);
            split.Panel2.Controls.Add(right);

            Controls.Add(split); Controls.Add(top);
            UI.Apply(this);
            code.BackColor = Color.FromArgb(30, 30, 30); code.ForeColor = Color.FromArgb(225, 225, 225);
            Fill();
        }

        LibCmd Current { get { return lv.SelectedItems.Count == 0 ? null : (LibCmd)lv.SelectedItems[0].Tag; } }

        void Fill()
        {
            string q = search.Text.Trim();
            lv.BeginUpdate(); lv.Items.Clear(); lv.Groups.Clear();
            foreach (var c in cmds)
            {
                if (q.Length > 0 && (c.Title + " " + c.Desc + " " + c.CatLabel + " " + c.Tags + " " + c.Code).IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var grp = lv.Groups[c.CatId] ?? lv.Groups.Add(c.CatId, c.CatLabel);
                var it = new ListViewItem(new[] { (Settings.Pins.Contains(c.Id) ? "\u2605 " : "") + c.Title, c.Risk, c.Admin ? "admin" : "" }, grp) { Tag = c };
                it.UseItemStyleForSubItems = false;
                if (c.Risk == "destructive") it.SubItems[1].ForeColor = UI.Danger;
                lv.Items.Add(it);
            }
            lv.EndUpdate();
            if (lv.Items.Count > 0) lv.Items[0].Selected = true; else { code.Text = ""; desc.Text = cmds.Count == 0 ? "commands.json was not found next to PSTools.exe." : "No match."; }
        }

        void Show_()
        {
            var c = Current;
            if (c == null) return;
            var sb = new StringBuilder();
            sb.Append(c.Desc);
            if (c.Note.Length > 0) sb.Append("\n\nNote: ").Append(c.Note);
            if (c.Requires.Length > 0) sb.Append("\nRequires: ").Append(c.Requires);
            if (c.Admin) sb.Append("\nNeeds an elevated (admin) PowerShell.");
            desc.Text = sb.ToString();
            code.Text = c.Code.Replace("\r\n", "\n").Replace("\n", "\r\n");
            pin.Text = Settings.Pins.Contains(c.Id) ? "Unpin from menu" : "Pin to menu";
        }

        void TogglePin()
        {
            var c = Current;
            if (c == null) return;
            Settings.TogglePin(c.Id);
            Show_();
            Fill();
            Program.Msg("Pinned commands appear in the Explorer menu after you press Apply on the Menu page (or run PSTools.exe --install).");
        }

        void Open()
        {
            if (code.Text.Length == 0) return;
            Clip.Set(code.Text);
            LibraryData.OpenShell(folder, Current);
        }
    }
}
