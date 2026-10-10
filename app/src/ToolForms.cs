using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PSTools
{
    class ServeForm : Form
    {
        readonly string root;
        readonly Label url = new Label { AutoSize = false, Dock = DockStyle.Top, Height = 34, Font = UI.Head };
        readonly ListBox log = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };
        HttpListener listener;
        int port;

        public ServeForm(string folder)
        {
            root = Fs.Full(folder);
            Text = "Serve  -  " + root; Font = UI.Font; ClientSize = new Size(640, 380); StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Padding = new Padding(16);

            var note = new Label { Dock = DockStyle.Top, Height = 40, ForeColor = UI.Muted, Text = "Serving only on this computer (localhost). Closing this window stops the server." };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 8, 0, 0) };
            var open = new Button { Text = "Open in browser", Width = 130, Height = 30 };
            UI.Primary(open);
            open.Click += (s, e) => { try { Process.Start("http://localhost:" + port + "/"); } catch { } };
            var copy = new Button { Text = "Copy URL", Width = 100, Height = 30 };
            copy.Click += (s, e) => Clip.Set("http://localhost:" + port + "/");
            var stop = new Button { Text = "Stop and close", Width = 120, Height = 30 };
            stop.Click += (s, e) => Close();
            bar.Controls.Add(open); bar.Controls.Add(copy); bar.Controls.Add(stop);

            Controls.Add(log); Controls.Add(bar); Controls.Add(note); Controls.Add(url);
            UI.Apply(this);
            Shown += (s, e) => StartServer();
            FormClosing += (s, e) => { try { if (listener != null) listener.Close(); } catch { } };
        }

        void StartServer()
        {
            for (int p = 8080; p < 8130; p++)
            {
                if (!PortFree(p)) continue;
                var l = new HttpListener();
                l.Prefixes.Add("http://localhost:" + p + "/");
                l.Prefixes.Add("http://127.0.0.1:" + p + "/");
                try { l.Start(); }
                catch
                {
                    l.Close();
                    l = new HttpListener();
                    l.Prefixes.Add("http://localhost:" + p + "/");
                    try { l.Start(); } catch { l.Close(); continue; }
                }
                listener = l; port = p;
                url.Text = "http://localhost:" + p + "/";
                var t = new Thread(Loop) { IsBackground = true };
                t.Start();
                try { Process.Start(url.Text); } catch { }
                return;
            }
            Program.Msg("Could not start a server (ports 8080-8129 are busy or blocked).");
            Close();
        }

        static bool PortFree(int p)
        {
            try { var t = new TcpListener(IPAddress.Loopback, p); t.Start(); t.Stop(); return true; }
            catch { return false; }
        }

        void Loop()
        {
            var l = listener;
            while (l.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = l.GetContext(); } catch { return; }
                ThreadPool.QueueUserWorkItem(x => Serve(ctx));
            }
        }

        void Note(string text)
        {
            try
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    log.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + text);
                    while (log.Items.Count > 300) log.Items.RemoveAt(log.Items.Count - 1);
                }));
            }
            catch { }
        }

        void Serve(HttpListenerContext ctx)
        {
            string raw = "";
            int code = 200;
            try
            {
                raw = ctx.Request.Url.AbsolutePath;
                string rel = Uri.UnescapeDataString(raw).TrimStart('/').Replace('/', '\\');
                string full = Path.GetFullPath(Path.Combine(root, rel));
                if (!Fs.IsInside(full, root) && !string.Equals(full.TrimEnd('\\'), root, StringComparison.OrdinalIgnoreCase)) { code = 403; Send(ctx, 403, "text/plain", "Forbidden"); return; }
                if (Directory.Exists(full))
                {
                    string idx = new[] { "index.html", "index.htm" }.Select(n => Path.Combine(full, n)).FirstOrDefault(File.Exists);
                    if (idx != null) full = idx;
                    else { Send(ctx, 200, "text/html; charset=utf-8", Listing(full, raw)); return; }
                }
                if (!File.Exists(full)) { code = 404; Send(ctx, 404, "text/plain", "Not found"); return; }
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = Fs.Mime(Path.GetExtension(full));
                ctx.Response.AddHeader("Cache-Control", "no-store");
                using (var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    ctx.Response.ContentLength64 = fs.Length;
                    if (ctx.Request.HttpMethod != "HEAD") fs.CopyTo(ctx.Response.OutputStream);
                }
                ctx.Response.OutputStream.Close();
            }
            catch { code = 500; try { ctx.Response.Abort(); } catch { } }
            Note(code + "  " + raw);
        }

        static void Send(HttpListenerContext ctx, int code, string type, string body)
        {
            var b = Encoding.UTF8.GetBytes(body);
            ctx.Response.StatusCode = code;
            ctx.Response.ContentType = type;
            ctx.Response.ContentLength64 = b.Length;
            ctx.Response.OutputStream.Write(b, 0, b.Length);
            ctx.Response.OutputStream.Close();
        }

        string Listing(string dir, string urlPath)
        {
            var sb = new StringBuilder("<!doctype html><meta charset=utf-8><title>Index</title><body style=\"font:15px Segoe UI,sans-serif;margin:2em\"><h3>");
            sb.Append(WebUtility.HtmlEncode(urlPath)).Append("</h3><ul style=\"list-style:none;padding:0;line-height:1.8\">");
            if (urlPath.Length > 1) sb.Append("<li><a href=\"../\">../</a></li>");
            string baseUrl = urlPath.EndsWith("/") ? urlPath : urlPath + "/";
            foreach (var d in Directory.GetDirectories(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                sb.Append("<li><a href=\"").Append(baseUrl).Append(Uri.EscapeDataString(Path.GetFileName(d))).Append("/\">").Append(WebUtility.HtmlEncode(Path.GetFileName(d))).Append("/</a></li>");
            foreach (var f in Directory.GetFiles(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                sb.Append("<li><a href=\"").Append(baseUrl).Append(Uri.EscapeDataString(Path.GetFileName(f))).Append("\">").Append(WebUtility.HtmlEncode(Path.GetFileName(f))).Append("</a></li>");
            return sb.Append("</ul></body>").ToString();
        }
    }

    class UpdateForm : Form
    {
        readonly CheckBox winget = new CheckBox { Text = "winget apps (includes Microsoft Store apps winget can see)", AutoSize = true, Checked = true };
        readonly CheckBox unknown = new CheckBox { Text = "Also update apps whose installed version winget cannot read", AutoSize = true };
        readonly CheckBox silent = new CheckBox { Text = "Silent installers (no installer windows)", AutoSize = true, Checked = true };
        readonly CheckBox scoop = new CheckBox { Text = "scoop", AutoSize = true };
        readonly CheckBox npm = new CheckBox { Text = "npm global packages", AutoSize = true };
        readonly CheckBox wsl = new CheckBox { Text = "WSL kernel (wsl --update)", AutoSize = true };
        readonly CheckBox mods = new CheckBox { Text = "PowerShell modules from the gallery", AutoSize = true };

        public UpdateForm()
        {
            Text = "Update everything"; Font = UI.Font; ClientSize = new Size(560, 400); StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var head = new Label { Text = "Update everything", Font = UI.Title, Left = 20, Top = 16, AutoSize = true };
            var sub = new Label { Text = "Runs in a PowerShell window so you see every step and can stop with Ctrl+C. Only tools found on this PC are enabled.", Left = 22, Top = 52, Width = 510, Height = 36, ForeColor = UI.Muted };
            var box = new FlowLayoutPanel { Left = 22, Top = 96, Width = 520, Height = 220, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            foreach (var c in new CheckBox[] { winget, unknown, silent, scoop, npm, wsl, mods }) { c.Margin = new Padding(0, 4, 0, 4); box.Controls.Add(c); }
            unknown.Margin = new Padding(22, 4, 0, 4); silent.Margin = new Padding(22, 4, 0, 4);

            bool hasWinget = Fs.Which("winget") != null;
            winget.Enabled = hasWinget; winget.Checked = hasWinget; unknown.Enabled = silent.Enabled = hasWinget;
            scoop.Enabled = Fs.Which("scoop") != null;
            npm.Enabled = Fs.Which("npm") != null;
            wsl.Enabled = File.Exists(Path.Combine(Environment.SystemDirectory, "wsl.exe"));

            var preview = new Button { Text = "Preview what is outdated", Left = 22, Top = 340, Width = 190, Height = 34 };
            preview.Click += (s, e) => Go(false);
            var run = new Button { Text = "Update now", Left = 224, Top = 340, Width = 140, Height = 34 };
            UI.Primary(run);
            run.Click += (s, e) => Go(true);
            var close = new Button { Text = "Close", Left = 452, Top = 340, Width = 90, Height = 34 };
            close.Click += (s, e) => Close();
            Controls.AddRange(new Control[] { head, sub, box, preview, run, close });
            UI.Apply(this);
        }

        void Go(bool apply)
        {
            var sb = new StringBuilder();
            Action<string> title = t => sb.AppendLine("Write-Host \"`n=== " + t + " ===\" -ForegroundColor Cyan");
            if (winget.Checked && winget.Enabled)
            {
                title("winget");
                if (!apply) sb.AppendLine("winget upgrade" + (unknown.Checked ? " --include-unknown" : ""));
                else sb.AppendLine("winget upgrade --all --accept-source-agreements --accept-package-agreements" + (silent.Checked ? " --silent" : "") + (unknown.Checked ? " --include-unknown" : ""));
            }
            if (scoop.Checked && scoop.Enabled) { title("scoop"); sb.AppendLine(apply ? "scoop update *" : "scoop status"); }
            if (npm.Checked && npm.Enabled) { title("npm global packages"); sb.AppendLine(apply ? "npm update -g" : "npm outdated -g"); }
            if (wsl.Checked && wsl.Enabled) { title("WSL"); sb.AppendLine(apply ? "wsl --update" : "wsl --version"); }
            if (mods.Checked)
            {
                title("PowerShell modules");
                sb.AppendLine(apply ? "Get-InstalledModule | ForEach-Object { Write-Host $_.Name; Update-Module -Name $_.Name -ErrorAction Continue }" : "Get-InstalledModule | Select-Object Name, Version | Format-Table -AutoSize");
            }
            if (sb.Length == 0) { Program.Msg("Nothing is selected."); return; }
            try { Launch.Console(sb.ToString(), false, false); } catch (Exception ex) { Program.Msg(ex.Message); }
        }
    }
}
