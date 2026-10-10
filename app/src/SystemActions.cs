using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using Microsoft.Win32;

namespace PSTools
{
    static partial class Actions
    {
        static bool? wingetFound;
        static bool HasWinget { get { if (wingetFound == null) wingetFound = Fs.Which("winget") != null; return wingetFound.Value; } }

        static string Reg(string path, string name)
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(path)) { object v = k == null ? null : k.GetValue(name); return v == null ? "" : v.ToString(); }
            }
            catch { return ""; }
        }

        static string SysInfo()
        {
            string nt = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string os = Reg(nt, "ProductName");
            string ver = Reg(nt, "DisplayVersion");
            string build = Reg(nt, "CurrentBuild") + "." + Reg(nt, "UBR");
            string cpu = Reg(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString").Trim();
            double ram = 0;
            try { ram = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / (1024.0 * 1024 * 1024); } catch { }
            var sb = new StringBuilder();
            sb.AppendLine("Computer: " + Environment.MachineName);
            sb.AppendLine("User: " + Environment.UserName);
            sb.AppendLine("OS: " + os + " " + ver + " (build " + build + ")");
            sb.AppendLine("Architecture: " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"));
            sb.AppendLine("CPU: " + cpu + " (" + Environment.ProcessorCount + " logical processors)");
            sb.AppendLine("Memory: " + ram.ToString("0.0") + " GB");
            return sb.ToString().TrimEnd();
        }

        static Result RestartExplorer(bool clearCaches)
        {
            foreach (var p in Process.GetProcessesByName("explorer")) { try { p.Kill(); p.WaitForExit(3000); } catch { } }
            int removed = 0;
            if (clearCaches)
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var targets = new List<string> { Path.Combine(local, "IconCache.db") };
                string ex = Path.Combine(local, @"Microsoft\Windows\Explorer");
                try
                {
                    targets.AddRange(Directory.GetFiles(ex, "iconcache_*.db"));
                    targets.AddRange(Directory.GetFiles(ex, "thumbcache_*.db"));
                }
                catch { }
                foreach (var t in targets) try { if (File.Exists(t)) { File.Delete(t); removed++; } } catch { }
            }
            Process.Start("explorer.exe");
            return new Result { Summary = clearCaches ? "Explorer restarted, " + removed + " cache files removed. Icons rebuild as you browse." : "Explorer restarted." };
        }

        static IEnumerable<Action> SystemModule()
        {
            yield return new Action
            {
                Id = "update-all", Group = "System", Label = "Update everything...", Targets = Target.None,
                Hint = "Updates winget apps, and optionally scoop, npm globals, WSL and PowerShell modules, in a PowerShell window you can watch. Pick what to include; preview first.",
                Window = p => new UpdateForm()
            };
            yield return new Action
            {
                Id = "apps-export", Group = "System", Label = "Export installed apps list", Targets = Target.None, Fast = true,
                Available = () => HasWinget,
                Hint = "Saves everything winget knows about to a JSON file so a new PC can be set up with Import.",
                Run = (paths, c) =>
                {
                    using (var d = new SaveFileDialog { Filter = "JSON|*.json", FileName = "apps-" + DateTime.Now.ToString("yyyy-MM-dd") + ".json" })
                    {
                        if (d.ShowDialog() != DialogResult.OK) return null;
                        Launch.Console("winget export -o '" + d.FileName.Replace("'", "''") + "' --accept-source-agreements", false, false);
                    }
                    return new Result { Summary = "Exporting in the PowerShell window." };
                }
            };
            yield return new Action
            {
                Id = "apps-import", Group = "System", Label = "Install apps from a list", Targets = Target.None, Fast = true,
                Available = () => HasWinget,
                Hint = "Installs every app in a list made by Export installed apps list.",
                Run = (paths, c) =>
                {
                    using (var d = new OpenFileDialog { Filter = "JSON|*.json" })
                    {
                        if (d.ShowDialog() != DialogResult.OK) return null;
                        if (!Program.Ask("Install all apps listed in\n" + d.FileName + " ?")) return null;
                        Launch.Console("winget import -i '" + d.FileName.Replace("'", "''") + "' --accept-package-agreements --accept-source-agreements", false, false);
                    }
                    return new Result { Summary = "Installing in the PowerShell window." };
                }
            };
            yield return new Action
            {
                Id = "restart-explorer", Group = "System", Label = "Restart Explorer", Targets = Target.Background, Tier = Tier.Confirm,
                Hint = "Restarts the Windows shell (taskbar and File Explorer windows) when it hangs or after changing the menu style.",
                Confirm = "Restart Windows Explorer now? Open File Explorer windows will close.",
                Run = (paths, c) => RestartExplorer(false)
            };
            yield return new Action
            {
                Id = "clear-icon-cache", Group = "System", Label = "Rebuild icon and thumbnail cache", Targets = Target.Background, Tier = Tier.Confirm,
                Hint = "Fixes wrong, blank or stale icons and thumbnails: restarts Explorer and deletes its icon and thumbnail cache files.",
                Confirm = "Restart Explorer and delete the icon and thumbnail cache?\nOpen File Explorer windows will close.",
                Run = (paths, c) => RestartExplorer(true)
            };
            yield return new Action
            {
                Id = "toggle-hidden", Group = "System", Label = "Toggle hidden files", Targets = Target.Background, Fast = true,
                Hint = "Shows or hides hidden files in Explorer. Reverted by a full uninstall.",
                Run = (paths, c) => new Result { Summary = Tweaks.ToggleHidden() }
            };
            yield return new Action
            {
                Id = "toggle-ext", Group = "System", Label = "Toggle file extensions", Targets = Target.Background, Fast = true,
                Hint = "Shows or hides file name extensions in Explorer. Reverted by a full uninstall.",
                Run = (paths, c) => new Result { Summary = Tweaks.ToggleExt() }
            };
            yield return new Action
            {
                Id = "toggle-classic", Group = "System", Label = "Toggle classic right-click menu (Windows 11)", Targets = Target.Background, Fast = true,
                Hint = "Makes every right-click show the full menu instead of \"Show more options\" (HKCU only). Use Restart Explorer afterwards. Reverted by a full uninstall.",
                Run = (paths, c) => new Result { Summary = Tweaks.ToggleClassic() }
            };
            yield return new Action
            {
                Id = "copy-sysinfo", Group = "System", Label = "Copy system summary", Targets = Target.None, Fast = true,
                Hint = "Computer name, Windows version and build, CPU and memory, ready to paste into a support request.",
                Run = (paths, c) => new Result { ClipText = SysInfo(), Summary = "Copied system summary" }
            };
            yield return new Action
            {
                Id = "port-who", Group = "System", Label = "What is using a port...", Targets = Target.None, Fast = true,
                Hint = "Asks for a port number and shows which process owns it, with a button to end that process.",
                Run = (paths, c) =>
                {
                    string port = Interaction.InputBox("Port number:", "What is using a port", "3000");
                    port = (port ?? "").Trim();
                    if (port.Length == 0) return null;
                    int exit;
                    string o = Fs.Cmd("netstat -ano -p tcp", 20000, out exit);
                    var rows = new List<string[]>();
                    foreach (var line in o.Replace("\r", "").Split('\n'))
                    {
                        var t = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (t.Length < 5 || t[0] != "TCP") continue;
                        int colon = t[1].LastIndexOf(':');
                        if (colon < 0 || t[1].Substring(colon + 1) != port) continue;
                        string name = "?";
                        try { name = Process.GetProcessById(int.Parse(t[4])).ProcessName; } catch { }
                        rows.Add(new[] { t[1], t[2], t[3], t[4], name });
                    }
                    if (rows.Count == 0) return new Result { Summary = "Nothing is using TCP port " + port + "." };
                    return new Result
                    {
                        Summary = "Processes using TCP port " + port,
                        Columns = new[] { "Local", "Remote", "State", "PID", "Process" }, Rows = rows,
                        RowActionText = "End process",
                        RowAction = row =>
                        {
                            try { Process.GetProcessById(int.Parse(row[3])).Kill(); return "Ended " + row[4] + " (" + row[3] + ")"; }
                            catch (Exception ex) { return "Could not end it: " + ex.Message; }
                        }
                    };
                }
            };
            yield return new Action
            {
                Id = "battery-report", Group = "System", Label = "Battery report", Targets = Target.None,
                Hint = "Creates Windows' battery health report and opens it in your browser.",
                Run = (paths, c) =>
                {
                    string f = Path.Combine(Path.GetTempPath(), "pstools-battery.html");
                    int exit;
                    string o = Fs.Cmd("powercfg /batteryreport /output \"" + f + "\"", 30000, out exit);
                    if (!File.Exists(f)) return new Result { Summary = o.Length > 0 ? o : "No report was created (is there a battery?)." };
                    Process.Start(f);
                    return new Result { Summary = "Opened the battery report." };
                }
            };
            yield return ElevatedCmd("flush-dns", "Flush DNS cache", "Clears Windows' DNS resolver cache.", "ipconfig /flushdns");
            yield return ElevatedCmd("reset-network", "Reset network stack", "Resets Winsock and TCP/IP and flushes DNS. Fixes broken connections; custom VPN or adapter settings can be lost and a restart is needed.",
                "netsh winsock reset\r\nnetsh int ip reset\r\nipconfig /flushdns\r\nWrite-Host 'Restart Windows to finish.' -ForegroundColor Yellow");
            yield return ElevatedCmd("power-requests", "What is keeping the PC awake", "Shows which processes and drivers block sleep (powercfg /requests).", "powercfg /requests");
            yield return ElevatedCmd("dism-cleanup", "Clean up Windows component store", "DISM StartComponentCleanup: removes superseded update files safely. Can take several minutes.", "Dism.exe /Online /Cleanup-Image /StartComponentCleanup");
            yield return ElevatedCmd("sfc-scan", "System file check (sfc /scannow)", "Scans and repairs protected Windows files. Takes a while.", "sfc /scannow");
        }

        static Action ElevatedCmd(string id, string label, string hint, string script)
        {
            return new Action
            {
                Id = id, Group = "System", Label = label, Hint = hint, Targets = Target.None, Tier = Tier.Elevated,
                Script = paths => script
            };
        }
    }
}
