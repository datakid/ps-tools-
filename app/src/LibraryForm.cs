// PowerShell command library: browses commands.json (next to the exe), copies a snippet or
// opens PowerShell in the target folder with the snippet ready to review (never auto-runs).
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
    class LibraryForm : Form
    {
        class Cmd { public string Id, CatId, CatLabel, Tags, Title, Desc, Code, Risk, Note, Requires; public bool Admin; }

        readonly string folder;
        readonly List<Cmd> cmds = new List<Cmd>();
        readonly ListView lv = new ListView();
        readonly TextBox search = new TextBox(), code = new TextBox();
        readonly Label desc = new Label();

        public LibraryForm(string folder)
        {
            this.folder = folder;
            Text = "Command library  -  " + folder; Font = UI.Font; ClientSize = new Size(980, 600); StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Load_();

            var top = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(10, 8, 10, 4), BackColor = UI.Panel };
            search.Dock = DockStyle.Fill; ResultsForm.SetCue(search, "Search commands, tags, categories...");
            search.TextChanged += (s, e) => Fill(); top.Controls.Add(search);

            var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
            Load += (s, e) => { try { split.SplitterDistance = 430; } catch { } };
            lv.Dock = DockStyle.Fill; lv.View = View.Details; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false; lv.BorderStyle = BorderStyle.None;
            lv.Columns.Add("Command", 290); lv.Columns.Add("Risk", 80); lv.Columns.Add("", 40);
            lv.SelectedIndexChanged += (s, e) => Show_();
            lv.DoubleClick += (s, e) => OpenShell();
            split.Panel1.Controls.Add(lv);

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 10, 14, 10) };
            desc.Dock = DockStyle.Top; desc.Height = 110; desc.ForeColor = Color.FromArgb(40, 40, 40);
            code.Dock = DockStyle.Fill; code.Multiline = true; code.ScrollBars = ScrollBars.Both; code.WordWrap = false;
            code.Font = new Font("Consolas", 10f); code.BackColor = Color.FromArgb(30, 30, 30); code.ForeColor = Color.FromArgb(220, 220, 220);
            var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 8, 0, 0) };
            var copy = new Button { Text = "Copy", Width = 90, Height = 30 }; copy.Click += (s, e) => { if (code.Text.Length > 0) Clipboard.SetText(code.Text); };
            var shell = new Button { Text = "Open PowerShell here", Width = 170, Height = 30, BackColor = UI.Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = UI.Bold };
            shell.FlatAppearance.BorderSize = 0; shell.Click += (s, e) => OpenShell();
            var edit = new Button { Text = "Edit commands.json", Width = 150, Height = 30 };
            edit.Click += (s, e) => { try { Process.Start("notepad.exe", "\"" + JsonPath + "\""); } catch { } };
            btns.Controls.Add(shell); btns.Controls.Add(copy); btns.Controls.Add(edit);
            var hint = new Label { Dock = DockStyle.Bottom, Height = 34, ForeColor = UI.Muted, Text = "You can edit the code above before running. \"Open PowerShell here\" pastes nothing and runs nothing: the command is on your clipboard, so press Ctrl+V, review, then Enter." };
            right.Controls.Add(code); right.Controls.Add(desc); right.Controls.Add(hint); right.Controls.Add(btns);
            split.Panel2.Controls.Add(right);

            Controls.Add(split); Controls.Add(top);
            Fill();
        }

        static string JsonPath { get { return Path.Combine(Program.ExeDir, "commands.json"); } }

        void Load_()
        {
            if (!File.Exists(JsonPath)) return;
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
                    cmds.Add(new Cmd
                    {
                        Id = S(c, "id"), CatId = cat, CatLabel = cats.TryGetValue(cat, out label) ? label : cat, Tags = tags, Title = S(c, "title"),
                        Desc = S(c, "description"), Code = S(c, "command"), Risk = S(c, "risk"), Note = S(c, "note"), Requires = S(c, "requires"),
                        Admin = c.ContainsKey("admin") && c["admin"] is bool && (bool)c["admin"]
                    });
                }
            }
            catch (Exception ex) { Program.Msg("Could not read commands.json:\n" + ex.Message); }
        }

        static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? v.ToString() : ""; }

        void Fill()
        {
            string q = search.Text.Trim();
            lv.BeginUpdate(); lv.Items.Clear(); lv.Groups.Clear();
            foreach (var c in cmds)
            {
                if (q.Length > 0 && (c.Title + " " + c.Desc + " " + c.CatLabel + " " + c.Tags + " " + c.Code).IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var grp = lv.Groups[c.CatId] ?? lv.Groups.Add(c.CatId, c.CatLabel);
                var it = new ListViewItem(new[] { c.Title, c.Risk, c.Admin ? "admin" : "" }, grp) { Tag = c };
                if (c.Risk == "destructive") it.SubItems[1].ForeColor = Color.Firebrick;
                it.UseItemStyleForSubItems = false;
                lv.Items.Add(it);
            }
            lv.EndUpdate();
            if (lv.Items.Count > 0) lv.Items[0].Selected = true; else { code.Text = ""; desc.Text = "No match."; }
        }

        void Show_()
        {
            if (lv.SelectedItems.Count == 0) return;
            var c = (Cmd)lv.SelectedItems[0].Tag;
            var sb = new StringBuilder();
            sb.Append(c.Desc);
            if (c.Note.Length > 0) sb.Append("\n\nNote: ").Append(c.Note);
            if (c.Requires.Length > 0) sb.Append("\nRequires: ").Append(c.Requires);
            if (c.Admin) sb.Append("\nNeeds an elevated (admin) PowerShell.");
            desc.Text = sb.ToString();
            code.Text = c.Code.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        void OpenShell()
        {
            if (code.Text.Length == 0) return;
            Clipboard.SetText(code.Text);
            var c = lv.SelectedItems.Count > 0 ? (Cmd)lv.SelectedItems[0].Tag : null;
            // Elevated shells ignore WorkingDirectory, so cd explicitly.
            string intro = "Set-Location -LiteralPath '" + folder.Replace("'", "''") + "'; Write-Host 'Command copied. Press Ctrl+V to paste it, review, then Enter.' -ForegroundColor Cyan";
            if (c != null && c.Risk == "destructive") intro += "; Write-Host 'This command deletes or overwrites data. Read it first.' -ForegroundColor Yellow";
            string enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(intro));
            var psi = new ProcessStartInfo("powershell.exe", "-NoExit -NoProfile -EncodedCommand " + enc) { WorkingDirectory = folder, UseShellExecute = true };
            if (c != null && c.Admin) psi.Verb = "runas";
            try { Process.Start(psi); } catch { }
        }
    }
}
