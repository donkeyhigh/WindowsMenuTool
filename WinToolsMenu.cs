// WinTools menu (Win+Alt+X). Items, letters, hidden entries and order come from %LOCALAPPDATA%\WinTools\menu.json.
// Win+Alt+X again while the menu is open opens Custom Shortcuts: untick to hide, rename, move, add files.
// Two columns; a plain text pad on the right. RegisterHotKey (no keyboard hook).
// Esc, a click outside (focus leaves the window), or 60 seconds without a key closes the menu.
// C# 5 (csc.exe from .NET Framework 4). References: System.Windows.Forms, System.Drawing, System.Web.Extensions, System.Management.
// Resource: WinTools.ico (the program icon, also used for the tray and windows).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        bool created;
        using (Mutex m = new Mutex(true, "WinToolsMenuSingleInstance", out created))
        {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MenuHost());
        }
    }
}

// One line in the menu. Built-in entries and custom entries look the same in the file.
public sealed class Entry
{
    public string Id { get; set; }
    public bool Custom { get; set; }
    public string Title { get; set; }
    public string Target { get; set; }
    public string Args { get; set; }
    public string Letter { get; set; }
    public bool Hidden { get; set; }
    public bool NeedsFolder { get; set; }
    public bool Admin { get; set; }      // run as administrator (Windows asks first)

    public Entry Clone()
    {
        return new Entry { Id = Id, Custom = Custom, Title = Title, Target = Target, Args = Args, Letter = Letter, Hidden = Hidden, NeedsFolder = NeedsFolder, Admin = Admin };
    }
}

public sealed class MenuFile
{
    public List<Entry> Entries { get; set; }
    public string SkipVersion { get; set; }
    public string Hotkey { get; set; }
}

public sealed class FooterItem
{
    public readonly string Label;
    public readonly Action Run;
    public FooterItem(string label, Action run) { Label = label; Run = run; }
}

static class Config
{
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinTools");
    public static readonly string FilePath = Path.Combine(Dir, "menu.json");
    // The update the user chose to skip ("Skip this version"). Kept in menu.json.
    public static string Skipped;
    // The hotkey that opens the menu, as text like "Win+Alt+X". Kept in menu.json.
    public static string Hotkey = "Win+Alt+X";

    // Reads menu.json. A broken file is kept as menu.json.bad and the built-in list is used.
    // Built-in entries that the file does not know yet are added at the end.
    public static List<Entry> Load()
    {
        List<Entry> saved = null;
        if (File.Exists(FilePath))
        {
            try
            {
                MenuFile f = new JavaScriptSerializer().Deserialize<MenuFile>(File.ReadAllText(FilePath, Encoding.UTF8));
                if (f != null) Skipped = f.SkipVersion;
                if (f != null && !string.IsNullOrEmpty(f.Hotkey)) Hotkey = f.Hotkey;
                if (f != null && f.Entries != null) saved = f.Entries;
            }
            catch (Exception)
            {
                try { File.Move(FilePath, FilePath + ".bad"); } catch (Exception) { }
            }
        }
        if (saved == null) return Defaults();
        List<Entry> result = new List<Entry>(saved);
        foreach (Entry d in Defaults())
        {
            bool known = false;
            foreach (Entry s in result)
            {
                if (s.Id != d.Id || s.Custom) continue;
                // Built-in entries always run what this version says, so fixed paths reach old settings files.
                // The user's title, letter, order and hidden state are kept.
                s.Target = d.Target;
                s.Args = d.Args;
                s.NeedsFolder = d.NeedsFolder;
                s.Admin = d.Admin;
                known = true;
                break;
            }
            if (!known)
            {
                // A new built-in must not take a letter the user's items already use.
                if (LetterUsed(result, d.Letter)) d.Letter = FreeLetter(result, d.Title);
                result.Add(d);
            }
        }
        return result;
    }

    static bool LetterUsed(List<Entry> entries, string letter)
    {
        if (string.IsNullOrEmpty(letter)) return false;
        foreach (Entry e in entries)
        {
            if (string.Equals(e.Letter, letter, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // First letter of the title that is free, otherwise the first free letter A-Z, otherwise none.
    static string FreeLetter(List<Entry> entries, string title)
    {
        foreach (char ch in (title ?? "") + "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        {
            char c = char.ToUpperInvariant(ch);
            if (c >= 'A' && c <= 'Z' && !LetterUsed(entries, c.ToString())) return c.ToString();
        }
        return "";
    }

    public static void Save(List<Entry> entries)
    {
        Directory.CreateDirectory(Dir);
        string json = new JavaScriptSerializer().Serialize(new MenuFile { Entries = entries, SkipVersion = Skipped, Hotkey = Hotkey });
        File.WriteAllText(FilePath, json, Encoding.UTF8);
    }

    // Parses text like "Win+Alt+X" (modifiers Win, Ctrl, Alt, Shift, then one key).
    // At least one modifier is required, so the hotkey cannot be a plain letter.
    public static bool TryParseHotkey(string text, out uint mods, out uint vk)
    {
        mods = 0;
        vk = 0;
        if (string.IsNullOrEmpty(text)) return false;
        string[] parts = text.Split('+');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (i < parts.Length - 1)
            {
                if (string.Equals(p, "Win", StringComparison.OrdinalIgnoreCase)) mods |= 0x0008;
                else if (string.Equals(p, "Ctrl", StringComparison.OrdinalIgnoreCase)) mods |= 0x0002;
                else if (string.Equals(p, "Alt", StringComparison.OrdinalIgnoreCase)) mods |= 0x0001;
                else if (string.Equals(p, "Shift", StringComparison.OrdinalIgnoreCase)) mods |= 0x0004;
                else return false;
            }
            else
            {
                Keys k;
                if (!Enum.TryParse(p, true, out k)) return false;
                Keys key = k & Keys.KeyCode;
                if (key == Keys.None || key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu
                    || key == Keys.LWin || key == Keys.RWin) return false;
                vk = (uint)key;
            }
        }
        return mods != 0 && vk != 0;
    }

    static Entry D(string id, string title, string target, string args, string letter, bool folder)
    {
        return new Entry { Id = id, Title = title, Target = target, Args = args, Letter = letter, NeedsFolder = folder };
    }

    static Entry ConsoleEntry(string id, string title, string target, string args, string letter, bool admin, bool hidden)
    {
        Entry e = D(id, title, target, args, letter, true);
        e.Admin = admin;
        e.Hidden = hidden;
        return e;
    }

    public static List<Entry> Defaults()
    {
        List<Entry> l = new List<Entry>();
        l.Add(D("firewall", "firewall.cpl", "{system}\\control.exe", "firewall.cpl", "F", false));
        l.Add(D("appwiz", "Add remove programs", "{system}\\control.exe", "appwiz.cpl", "R", false));
        l.Add(D("ncpa", "Advanced network settings", "{system}\\control.exe", "ncpa.cpl", "W", false));
        l.Add(D("taskmgr", "Task Manager", "{system}\\taskmgr.exe", "", "T", false));
        l.Add(D("devmgmt", "Device Manager", "{system}\\devmgmt.msc", "", "G", false));
        l.Add(D("compmgmt", "Computer Management", "{system}\\compmgmt.msc", "", "M", false));
        l.Add(D("mmsys", "Sounds", "{system}\\control.exe", "mmsys.cpl", "S", false));
        l.Add(D("notepad", "notepad", "{system}\\notepad.exe", "", "E", false));
        l.Add(D("npp", "notepad++", "{programfiles}\\Notepad++\\notepad++.exe", "", "A", false));
        l.Add(D("appdata", "%appdata%", "{windows}\\explorer.exe", "\"{appdata}\"", "D", false));
        l.Add(D("localappdata", "%localappdata%", "{windows}\\explorer.exe", "\"{localappdata}\"", "C", false));
        l.Add(D("startup", "Shell: startup", "{windows}\\explorer.exe", "shell:startup", "U", false));
        // Consoles in the folder under the mouse. {folder} is that folder, {folderps} the same for a PowerShell '...' string.
        // The folder goes in the arguments: an elevated program ignores the working directory it is given.
        // The admin versions are shown by default; the normal ones are in the list, unticked.
        const string CmdArgs = "/k cd /d \"{folder}\"";
        const string PsArgs = "-NoExit -Command \"Set-Location -LiteralPath '{folderps}'\"";
        const string WtArgs = "-d \"{folder}\"";
        string cmd = "{system}\\cmd.exe", ps = "{system}\\WindowsPowerShell\\v1.0\\powershell.exe", wt = "{localappdata}\\Microsoft\\WindowsApps\\wt.exe";
        l.Add(ConsoleEntry("cmdadmin", "Open CMD here (admin)", cmd, CmdArgs, "O", true, false));
        l.Add(ConsoleEntry("cmd", "Open CMD here", cmd, CmdArgs, "N", false, true));
        l.Add(ConsoleEntry("pwshadmin", "Open PowerShell here (admin)", ps, PsArgs, "P", true, false));
        l.Add(ConsoleEntry("pwsh", "Open PowerShell here", ps, PsArgs, "H", false, true));
        l.Add(ConsoleEntry("wtadmin", "Open Terminal here (admin)", wt, WtArgs, "I", true, false));
        l.Add(ConsoleEntry("wt", "Open Terminal here", wt, WtArgs, "J", false, true));
        l.Add(D("godmode", "God Mode", "{windows}\\explorer.exe", "shell:::{ED7BA470-8E54-465E-825C-99712043E01C}", "Q", false));
        // Bare name: ShellExecute finds it in System32 or, on newer Windows 11, as an app alias.
        l.Add(D("sandbox", "Windows Sandbox", "WindowsSandbox.exe", "", "X", false));
        l.Add(D("about", "System, About", "ms-settings:about", "", "Y", false));
        l.Add(D("sysdm", "Systemegenskaper (sysdm.cpl)", "{system}\\control.exe", "sysdm.cpl", "K", false));
        l.Add(D("bluetooth", "Add Bluetooth Device", "{system}\\DevicePairingWizard.exe", "", "B", false));
        l.Add(D("reliability", "Reliability Monitor", "{system}\\perfmon.exe", "/rel", "L", false));
        return l;
    }

    public static string Expand(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("{system}", Environment.GetFolderPath(Environment.SpecialFolder.System))
                .Replace("{windows}", Environment.GetFolderPath(Environment.SpecialFolder.Windows))
                .Replace("{appdata}", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
                .Replace("{localappdata}", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
                .Replace("{programfiles}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))
                .Replace("{programfiles86}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
    }
}

// Offers to install missing tools: Notepad++ and Sandboxie-Plus with winget, Windows Sandbox as a Windows feature.
// Nothing is downloaded or changed unless the user answers Yes. Long work runs on a background thread.
static class Installers
{
    const string Caption = "WinTools";

    static string WingetPath()
    {
        string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\WindowsApps\\winget.exe");
        return File.Exists(p) ? p : null;
    }

    static bool Ask(string text)
    {
        return MessageBox.Show(text, Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    static void Tell(string text, MessageBoxIcon icon)
    {
        MessageBox.Show(text, Caption, MessageBoxButtons.OK, icon);
    }

    static void Open(string fileOrUrl, string args)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(fileOrUrl, args ?? "");
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        catch (Exception ex) { Tell(ex.Message, MessageBoxIcon.Warning); }
    }

    // Runs a program and waits for it. Returns its exit code, or -1 if it could not start (for example UAC was refused).
    static int RunAndWait(string file, string args, bool elevate)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args);
            psi.UseShellExecute = true;
            if (elevate) psi.Verb = "runas";
            using (Process p = Process.Start(psi))
            {
                if (p == null) return -1;
                p.WaitForExit();
                return p.ExitCode;
            }
        }
        catch (Exception) { return -1; }
    }

    static void InBackground(ThreadStart work)
    {
        Thread t = new Thread(delegate()
        {
            try { work(); }
            catch (Exception ex) { Tell(ex.Message, MessageBoxIcon.Warning); }
        });
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }

    // winget asks for admin rights itself when the installer needs them, and shows its progress in a console window.
    static int Winget(string id)
    {
        return RunAndWait(WingetPath(), "install --id " + id + " -e --source winget --silent"
            + " --accept-package-agreements --accept-source-agreements", false);
    }

    // ---- Notepad++ ----

    public static string NotepadPPPath()
    {
        foreach (Environment.SpecialFolder f in new Environment.SpecialFolder[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            string p = Path.Combine(Environment.GetFolderPath(f), "Notepad++\\notepad++.exe");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public static void OfferNotepadPP()
    {
        if (!Ask("Notepad++ is not installed.\n\nInstall it now? It is downloaded from the official source and installed "
            + "automatically. Windows asks for permission first.")) return;
        InBackground(delegate
        {
            if (WingetPath() != null) Winget("Notepad++.Notepad++");
            if (NotepadPPPath() == null) InstallNotepadPPFromGitHub();
            string exe = NotepadPPPath();
            if (exe != null)
            {
                Open(exe, "");
                return;
            }
            if (Ask("Notepad++ was not installed.\n\nOpen the Notepad++ download page instead?"))
                Open("https://notepad-plus-plus.org/downloads/", "");
        });
    }

    // Without winget: the newest installer from the official GitHub releases, run silently (/S).
    static void InstallNotepadPPFromGitHub()
    {
        System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072; // TLS 1.2
        string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
        string ending = string.Equals(arch, "ARM64", StringComparison.OrdinalIgnoreCase) ? ".Installer.arm64.exe"
            : Environment.Is64BitOperatingSystem ? ".Installer.x64.exe" : ".Installer.exe";
        string url = null;
        using (System.Net.WebClient wc = new System.Net.WebClient())
        {
            wc.Headers.Add("User-Agent", "WinToolsMenu");
            string json = wc.DownloadString("https://api.github.com/repos/notepad-plus-plus/notepad-plus-plus/releases/latest");
            Dictionary<string, object> release = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            System.Collections.IEnumerable assets = release.ContainsKey("assets") ? release["assets"] as System.Collections.IEnumerable : null;
            if (assets == null) return;
            foreach (object a in assets)
            {
                Dictionary<string, object> asset = a as Dictionary<string, object>;
                if (asset == null) continue;
                string name = asset["name"] as string;
                if (name != null && name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
                {
                    url = asset["browser_download_url"] as string;
                    break;
                }
            }
            if (url == null) return;
            string file = Path.Combine(Path.GetTempPath(), "npp-installer.exe");
            wc.DownloadFile(url, file);
            RunAndWait(file, "/S", true);
            try { File.Delete(file); } catch (Exception) { }
        }
    }

    // ---- Windows Sandbox / Sandboxie-Plus ----

    public static string SandboxiePath()
    {
        string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sandboxie-Plus\\SandMan.exe");
        return File.Exists(p) ? p : null;
    }

    // Windows Home is "Core..." in EditionID. Home has no Windows Sandbox.
    static bool IsHomeEdition()
    {
        try
        {
            object v = Microsoft.Win32.Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "EditionID", null);
            string id = v as string;
            return id != null && id.StartsWith("Core", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    // InstallState of the Windows Sandbox feature: 1 on, 2 off, 3 not available in this Windows, 0 unknown.
    static int SandboxFeatureState()
    {
        try
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                "SELECT InstallState FROM Win32_OptionalFeature WHERE Name='Containers-DisposableClientVM'"))
            {
                foreach (ManagementObject o in s.Get()) return Convert.ToInt32(o["InstallState"]);
            }
            return 3;
        }
        catch (Exception) { return 0; }
    }

    // True if virtualization is on in the BIOS/UEFI, or a hypervisor already runs (then the CPU reports it as off).
    static bool VirtualizationOn()
    {
        try
        {
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT HypervisorPresent FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject o in s.Get()) if (o["HypervisorPresent"] is bool && (bool)o["HypervisorPresent"]) return true;
            }
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT VirtualizationFirmwareEnabled FROM Win32_Processor"))
            {
                foreach (ManagementObject o in s.Get()) if (o["VirtualizationFirmwareEnabled"] is bool && (bool)o["VirtualizationFirmwareEnabled"]) return true;
            }
            return false;
        }
        catch (Exception) { return true; }
    }

    static void OfferRestart(string text)
    {
        if (Ask(text + "\n\nRestart the PC now? Save your work first.")) Open("shutdown.exe", "/r /t 0");
    }

    // Windows Sandbox was not found. Explains why and offers what fits this PC.
    public static void SandboxMissing()
    {
        Cursor.Current = Cursors.WaitCursor;
        bool home = IsHomeEdition();
        int state = home ? 3 : SandboxFeatureState();
        Cursor.Current = Cursors.Default;

        if (home || state == 3)
        {
            string why = home ? "Your Windows is a Home edition, which does not support Windows Sandbox."
                : "This Windows does not support Windows Sandbox.";
            if (!Ask(why + " It needs Windows 11 Pro, Enterprise or Education.\n\n"
                + "Install Sandboxie-Plus instead? It is free and runs programs in an isolated box. "
                + "It is downloaded and installed automatically, and this shortcut then opens it.")) return;
            if (WingetPath() == null)
            {
                Tell("winget (App Installer) is missing, so Sandboxie-Plus cannot be installed automatically. "
                    + "The download page opens now.", MessageBoxIcon.Information);
                Open("https://sandboxie-plus.com/downloads/", "");
                return;
            }
            InBackground(delegate
            {
                Winget("Sandboxie.Plus");
                string exe = SandboxiePath();
                if (exe != null)
                {
                    Open(exe, "");
                    return;
                }
                if (Ask("Sandboxie-Plus was not installed.\n\nOpen the Sandboxie-Plus download page instead?"))
                    Open("https://sandboxie-plus.com/downloads/", "");
            });
            return;
        }
        if (state == 1)
        {
            OfferRestart("Windows Sandbox is turned on, but Windows finishes setting it up when the PC restarts.");
            return;
        }
        if (!VirtualizationOn())
        {
            Tell("Windows Sandbox needs virtualization, which is turned off in this PC's BIOS/UEFI.\n\n"
                + "Restart into the BIOS/UEFI setup and turn on Intel VT-x (Intel Virtualization Technology) or AMD SVM. "
                + "Then use this shortcut again.", MessageBoxIcon.Information);
            return;
        }
        if (!Ask("Windows Sandbox is not turned on.\n\nTurn it on now? Windows asks for permission, then installs it. "
            + "The PC must restart afterwards.")) return;
        InBackground(delegate
        {
            string dism = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe");
            int code = RunAndWait(dism, "/online /enable-feature /featurename:Containers-DisposableClientVM /all /norestart", true);
            if (code == 0 || code == 3010) OfferRestart("Windows Sandbox is turned on.");
            else if (code == -1) Tell("Windows Sandbox was not turned on (permission was not given).", MessageBoxIcon.Information);
            else Tell("Windows Sandbox could not be turned on (DISM error " + code + ").\n\n"
                + "Try \"Turn Windows features on or off\" (optionalfeatures.exe) instead.", MessageBoxIcon.Warning);
        });
    }
}

static class Dialogs
{
    // The program icon (embedded WinTools.ico) at the given size, or the standard one if it is missing.
    public static Icon AppIcon(Size size)
    {
        try
        {
            using (Stream st = typeof(Dialogs).Assembly.GetManifestResourceStream("WinTools.ico"))
            {
                if (st != null) return new Icon(st, size);
            }
        }
        catch (Exception) { }
        return SystemIcons.Application;
    }

    // Waits for one letter key for the entry. Returns "A".."Z", "" for no letter (Delete), or null if cancelled.
    public static string PickLetter(IWin32Window owner, string title, string current)
    {
        using (Form f = new Form())
        {
            string result = null;
            f.Text = "Change letter";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.KeyPreview = true;
            f.ClientSize = new Size(380, 110);
            Label l = new Label();
            l.Text = "Press the new letter for \"" + title + "\" (now: " + (string.IsNullOrEmpty(current) ? "none" : current) + ").\n"
                + "If another item uses it, the two swap letters.\n"
                + "Delete = no letter. Esc = cancel.";
            l.Location = new Point(12, 12);
            l.Size = new Size(356, 60);
            f.Controls.Add(l);
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(288, 76);
            cancel.Width = 80;
            f.Controls.Add(cancel);
            f.CancelButton = cancel;
            f.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Control || e.Alt) return;
                if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
                {
                    result = ((char)('A' + (int)(e.KeyCode - Keys.A))).ToString();
                }
                else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
                {
                    result = "";
                }
                else return;
                e.SuppressKeyPress = true;
                f.DialogResult = DialogResult.OK;
            };
            return f.ShowDialog(owner) == DialogResult.OK ? result : null;
        }
    }

    // A small text prompt. Returns the new text, or null if cancelled.
    public static string Prompt(string caption, string value)
    {
        using (Form f = new Form())
        {
            f.Text = caption;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ClientSize = new Size(360, 100);
            TextBox tb = new TextBox();
            tb.Text = value;
            tb.Location = new Point(12, 14);
            tb.Width = 336;
            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(176, 56);
            ok.Width = 80;
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(268, 56);
            cancel.Width = 80;
            f.Controls.Add(tb);
            f.Controls.Add(ok);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            return f.ShowDialog() == DialogResult.OK ? tb.Text : null;
        }
    }
}

sealed class MenuHost : Form
{
    string latestVersion;                       // newer version on GitHub, or null
    int footerHover = -1;
    readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer();
    bool checkingUpdates;
    DateTime lastUpdateCheck = DateTime.MinValue;

    const int WM_HOTKEY = 0x0312;
    const int HOTKEY_ID = 1;
    const uint MOD_ALT = 0x0001;
    const uint MOD_WIN = 0x0008;
    const int VK_X = 0x58;
    const byte VK_NOP = 0xE8;
    int listWidth = 200;       // set in LayoutMenu to fit the longest name
    int ListWidth { get { return listWidth; } }
    const int ListGap = 50;        // space between the list and the text pad
    const int MaxRows = 18;      // rows shown at once; the rest scroll
    const int PadWidth = 440;
    const int HeaderHeight = 34;   // top of the list, below the hint
    const int PadTop = 34;         // top of the text pad (its title sits above it)
    const int RowHeight = 22;
    const int MinHeight = 340;
    const string AppVersion = "1.0.23";
    public static string Version { get { return AppVersion; } }
    const string RepoOwner = "donkeyhigh";
    const string RepoName = "WindowsMenuTool";
    const string RawRoot = "https://raw.githubusercontent.com/" + RepoOwner + "/" + RepoName + "/";
    const string RawBase = RawRoot + "main/";
    const string ApiCommit = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/commits/main";
    uint hotkeyMods, hotkeyKey;   // the menu hotkey as it is registered now
    bool hotkeyOn;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int dwProcessId);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(System.Drawing.Point p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")] static extern uint GetWindowPid(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);

    static readonly Point Parked = new Point(-32000, -32000);

    List<Entry> entries;                       // everything, including hidden entries, in order
    readonly List<Entry> shown = new List<Entry>();
    string folderUnderMouse;                   // folder under the mouse when the menu opened
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly NotifyIcon tray = new NotifyIcon();
    readonly Font font = new Font("Segoe UI", 9f);
    readonly Font headFont = new Font("Segoe UI", 8.5f);
    readonly Label title = new Label();
    readonly TextBox pad = new TextBox();
    readonly Label padStatus = new Label();
    SettingsForm settings;
    bool open;
    int hover = -1;
    int top;                   // index of the first item shown (the list scrolls)
    DateTime lastKey;
    IntPtr previousForeground = IntPtr.Zero;

    public MenuHost()
    {
        Text = "WinTools Menu";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(17, 24, 39);
        ForeColor = Color.White;
        DoubleBuffered = true;
        KeyPreview = true;

        entries = Config.Load();
        Rebuild();
        BuildPad();
        LayoutMenu();
        updateTimer.Interval = 24 * 60 * 60 * 1000;
        updateTimer.Tick += delegate { CheckForUpdates(); };
        updateTimer.Start();

        timer.Interval = 1000;
        timer.Tick += delegate { OnTimer(); };

        tray.Icon = Dialogs.AppIcon(SystemInformation.SmallIconSize);
        tray.Text = "WinTools Menu " + AppVersion + " (" + Config.Hotkey + ")";
        tray.Visible = true;
        ContextMenuStrip cm = new ContextMenuStrip();
        cm.Items.Add("Custom shortcuts (" + Config.Hotkey + " twice)", null, delegate { ShowSettings(); });
        cm.Items.Add("Check for updates", null, delegate { CheckNowIgnoringSkip(); });
        cm.Items.Add("Exit WinTools Menu", null, delegate { Application.Exit(); });
        tray.ContextMenuStrip = cm;

        // Parked off-screen while closed. Hiding the window made Windows stop delivering the hotkey.
        Location = Parked;
        if (!Config.TryParseHotkey(Config.Hotkey, out hotkeyMods, out hotkeyKey))
        {
            Config.Hotkey = "Win+Alt+X";
            Config.TryParseHotkey(Config.Hotkey, out hotkeyMods, out hotkeyKey);
        }
        if (!RegisterOwnHotkey(hotkeyMods, hotkeyKey))
        {
            tray.Text = "WinTools Menu: " + Config.Hotkey + " is used by another program";
        }
        CheckForUpdates();
    }

    bool UnregisterOwnHotkey()
    {
        if (hotkeyOn) UnregisterHotKey(Handle, HOTKEY_ID);
        hotkeyOn = false;
        return true;
    }

    bool RegisterOwnHotkey(uint mods, uint key)
    {
        UnregisterOwnHotkey();
        hotkeyOn = RegisterHotKey(Handle, HOTKEY_ID, mods, key);
        return hotkeyOn;
    }

    // While the settings window records a new combination, the old hotkey must not fire.
    public void SuspendHotkey() { UnregisterOwnHotkey(); }
    public void ResumeHotkey() { if (!hotkeyOn) RegisterOwnHotkey(hotkeyMods, hotkeyKey); }

    // Registers a new hotkey text such as "Ctrl+Alt+M". Returns false and keeps the old one if Windows refuses it.
    public bool ChangeHotkey(string text)
    {
        uint mods, key;
        if (!Config.TryParseHotkey(text, out mods, out key)) return false;
        if (RegisterOwnHotkey(mods, key))
        {
            hotkeyMods = mods;
            hotkeyKey = key;
            tray.Text = "WinTools Menu " + AppVersion + " (" + text + ")";
            return true;
        }
        RegisterOwnHotkey(hotkeyMods, hotkeyKey);
        return false;
    }

    // Called by the settings window after Save.
    public void Reload(List<Entry> newEntries)
    {
        entries = newEntries;
        Rebuild();
        hover = -1;
        LayoutMenu();
        Invalidate();
    }

    void Rebuild()
    {
        shown.Clear();
        foreach (Entry e in entries)
        {
            if (!e.Hidden) shown.Add(e);
        }
    }

    int VisibleRows() { return Math.Max(1, Math.Min(shown.Count, MaxRows)); }
    int MaxTop() { return Math.Max(0, shown.Count - VisibleRows()); }

    // The list is only as wide as its longest name (and footer item), so the gap to the text pad is measured from the text.
    int ComputeListWidth()
    {
        int widest = 0;
        foreach (Entry e in shown) widest = Math.Max(widest, TextRenderer.MeasureText(e.Title ?? "", font).Width);
        foreach (FooterItem f in Footer()) widest = Math.Max(widest, TextRenderer.MeasureText(f.Label, font).Width);
        widest = Math.Max(widest, TextRenderer.MeasureText(Config.Hotkey + " again: customise", headFont).Width);
        return Math.Max(120, widest + 16);
    }

    void LayoutMenu()
    {
        top = Math.Min(top, MaxTop());
        listWidth = ComputeListWidth();
        int listW = ListWidth;
        int height = Math.Max(FooterTop(VisibleRows()) + Footer().Count * RowHeight + 8, MinHeight);
        ClientSize = new Size(listW + ListGap + PadWidth + 8, height);
        int px = listW + ListGap;
        title.Location = new Point(px, 8);
        pad.Location = new Point(px, PadTop);
        pad.Size = new Size(PadWidth, height - PadTop - 34);
        padStatus.Location = new Point(px, height - 26);
    }

    void BuildPad()
    {
        title.Text = "Plain text: paste here, then copy with Ctrl+C";
        title.Font = headFont;
        title.ForeColor = Color.FromArgb(156, 163, 175);
        title.AutoSize = true;
        Controls.Add(title);

        pad.Multiline = true;
        pad.WordWrap = true;
        pad.ScrollBars = ScrollBars.Vertical;
        pad.AcceptsReturn = true;
        pad.BorderStyle = BorderStyle.FixedSingle;
        pad.BackColor = Color.FromArgb(31, 41, 55);
        pad.ForeColor = Color.White;
        pad.Font = new Font("Segoe UI", 10f);
        pad.TextChanged += delegate
        {
            lastKey = DateTime.Now;
            padStatus.Text = pad.TextLength + " characters";
        };
        Controls.Add(pad);

        padStatus.Text = "0 characters";
        padStatus.ForeColor = Color.FromArgb(156, 163, 175);
        padStatus.Font = headFont;
        padStatus.AutoSize = true;
        Controls.Add(padStatus);

    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            // Tap an unused key so Windows does not open Start when Win is released.
            keybd_event(VK_NOP, 0, 0, UIntPtr.Zero);
            keybd_event(VK_NOP, 0, 2, UIntPtr.Zero);
            // Win+Alt+X opens the menu. Pressed again while the menu is open, it opens Custom Shortcuts.
            if (open) ShowSettings();
            else OpenMenu();
            return;
        }
        base.WndProc(ref m);
    }

    bool ItemEnabled(Entry e) { return !e.NeedsFolder || folderUnderMouse != null; }

    // The folder shown by the Explorer window under the mouse, or the desktop. Null if it is not a folder.
    static string FolderUnderCursor()
    {
        try
        {
            IntPtr top = GetAncestor(WindowFromPoint(Cursor.Position), 2); // GA_ROOT
            StringBuilder cls = new StringBuilder(256);
            GetClassName(top, cls, 256);
            string c = cls.ToString();
            if (c == "Progman" || c == "WorkerW") return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (c != "CabinetWClass") return null;
            return ExplorerLocation(top);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Asks Explorer (Shell.Application) which folder the window with this handle shows.
    static string ExplorerLocation(IntPtr hwnd)
    {
        Type t = Type.GetTypeFromProgID("Shell.Application");
        if (t == null) return null;
        object shell = Activator.CreateInstance(t);
        object windows = t.InvokeMember("Windows", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
        int count = (int)windows.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, windows, null);
        for (int i = 0; i < count; i++)
        {
            object w = windows.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, windows, new object[] { i });
            if (w == null) continue;
            long handle = Convert.ToInt64(w.GetType().InvokeMember("HWND", System.Reflection.BindingFlags.GetProperty, null, w, null));
            if (handle != hwnd.ToInt64()) continue;
            string url = (string)w.GetType().InvokeMember("LocationURL", System.Reflection.BindingFlags.GetProperty, null, w, null);
            if (!string.IsNullOrEmpty(url) && url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(url).LocalPath;
            }
            return null;
        }
        return null;
    }

    static string Quote(string path) { return "\"" + path + "\""; }

    // Closes the menu and runs the entry while the menu still has the foreground, so Windows lets the new
    // window come to the front. Handing the focus back first made Settings and control panels open behind
    // other windows (only flashing in the taskbar). If nothing has taken the focus after a few seconds,
    // it goes back to the window that had it before the menu opened.
    void Launch(Entry en)
    {
        string folder = folderUnderMouse;
        IntPtr previous = previousForeground;
        CloseMenu(false);
        AllowSetForegroundWindow(-1); // ASFW_ANY: also programs started through a broker (Settings, Store apps)
        RunEntry(en, folder);
        System.Windows.Forms.Timer giveBack = new System.Windows.Forms.Timer();
        giveBack.Interval = 3000;
        giveBack.Tick += delegate
        {
            giveBack.Stop();
            giveBack.Dispose();
            if (!open && GetForegroundWindow() == Handle) GiveFocusTo(previous);
        };
        giveBack.Start();
    }

    // Windows Sandbox is WindowsSandbox.exe in System32 on older Windows 11, and an app on newer builds.
    static void RunSandbox()
    {
        string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsSandbox.exe");
        if (File.Exists(exe))
        {
            Start(exe, "", null, true);
            return;
        }
        string app = null;
        try { app = FindApp("WindowsSandbox", "Windows Sandbox"); } catch (Exception) { }
        if (app != null)
        {
            Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "shell:AppsFolder\\" + app, null, true);
            return;
        }
        string sandboxie = Installers.SandboxiePath();
        if (sandboxie != null)
        {
            Start(sandboxie, "", null, true);
            return;
        }
        Installers.SandboxMissing();
    }

    // The launch id (shell:AppsFolder\<id>) of an installed app whose id contains idPart or whose name is name.
    static string FindApp(string idPart, string name)
    {
        Type t = Type.GetTypeFromProgID("Shell.Application");
        if (t == null) return null;
        object shell = Activator.CreateInstance(t);
        object folder = t.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { "shell:AppsFolder" });
        if (folder == null) return null;
        object items = folder.GetType().InvokeMember("Items", System.Reflection.BindingFlags.InvokeMethod, null, folder, null);
        int count = (int)items.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, items, null);
        for (int i = 0; i < count; i++)
        {
            object it = items.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, items, new object[] { i });
            if (it == null) continue;
            string path = it.GetType().InvokeMember("Path", System.Reflection.BindingFlags.GetProperty, null, it, null) as string;
            string title = it.GetType().InvokeMember("Name", System.Reflection.BindingFlags.GetProperty, null, it, null) as string;
            if (string.IsNullOrEmpty(path)) continue;
            if (path.IndexOf(idPart, StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(title, name, StringComparison.OrdinalIgnoreCase)) return path;
        }
        return null;
    }

    // Runs one entry. Folder entries run in the folder under the mouse. Scripts run through their host.
    static void RunEntry(Entry e, string folder)
    {
        if (!e.Custom && e.Id == "sandbox")
        {
            RunSandbox();
            return;
        }
        string target = Config.Expand(e.Target);
        string args = Config.Expand(e.Args);
        if (e.NeedsFolder)
        {
            if (folder == null) return;
            // "C:\" would end in \" and escape the closing quote, so a folder never ends in a backslash here.
            string f = folder.TrimEnd('\\');
            if (f.EndsWith(":")) f += "\\.";
            args = (args ?? "").Replace("{folderps}", f.Replace("'", "''")).Replace("{folder}", f);
            if (e.Admin) StartAdmin(target, args);
            else Start(target, args, folder, true);
            return;
        }
        // Built-in Notepad++ may live under the 32-bit folder instead.
        if (!File.Exists(target) && e.Target != null && e.Target.IndexOf("{programfiles}", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            string alt = Config.Expand(e.Target.Replace("{programfiles}", "{programfiles86}"));
            if (File.Exists(alt)) target = alt;
        }
        if (!e.Custom && e.Id == "npp" && !File.Exists(target))
        {
            Installers.OfferNotepadPP();
            return;
        }
        string ext = Path.GetExtension(target).ToLowerInvariant();
        if (ext == ".ps1")
        {
            string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
            Start(ps, "-NoProfile -ExecutionPolicy Bypass -File " + Quote(target) + " " + args, Path.GetDirectoryName(target), false);
        }
        else if (ext == ".bat" || ext == ".cmd")
        {
            string cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            Start(cmd, "/c \"" + Quote(target) + " " + args + "\"", Path.GetDirectoryName(target), false);
        }
        else
        {
            string dir = File.Exists(target) ? Path.GetDirectoryName(target) : null;
            Start(target, args, dir, true);
            if (target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)) BringSettingsForward();
        }
    }

    // A Settings window that is already open but minimized stays minimized when a settings page is opened
    // in it. For a few seconds after starting a settings page, restore the Settings window and bring it forward.
    static void BringSettingsForward()
    {
        int ticks = 0;
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        t.Interval = 250;
        t.Tick += delegate
        {
            ticks++;
            IntPtr w = IntPtr.Zero;
            try { w = SettingsWindow(); } catch (Exception) { }
            if (w != IntPtr.Zero)
            {
                if (IsIconic(w)) ShowWindow(w, 9); // SW_RESTORE
                if (GetForegroundWindow() != w) ForceForeground(w);
            }
            bool done = w != IntPtr.Zero && !IsIconic(w) && GetForegroundWindow() == w;
            if (done || ticks >= 20)
            {
                t.Stop();
                t.Dispose();
            }
        };
        t.Start();
    }

    static void ForceForeground(IntPtr hwnd)
    {
        uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        SetForegroundWindow(hwnd);
        if (attached) AttachThreadInput(me, fgThread, false);
    }

    // The frame window of the Settings app (SystemSettings.exe), or zero. Its content window (CoreWindow) is a child
    // of the frame while shown, and a separate top-level window with the same title while minimized.
    static IntPtr SettingsWindow()
    {
        Process[] ps = Process.GetProcessesByName("SystemSettings");
        if (ps.Length == 0) return IntPtr.Zero;
        List<uint> pids = new List<uint>();
        foreach (Process p in ps) { pids.Add((uint)p.Id); p.Dispose(); }

        List<IntPtr> frames = new List<IntPtr>();
        List<string> titles = new List<string>();
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            StringBuilder cls = new StringBuilder(256);
            GetClassName(h, cls, 256);
            string c = cls.ToString();
            if (c == "ApplicationFrameWindow") frames.Add(h);
            else if (c == "Windows.UI.Core.CoreWindow")
            {
                uint pid;
                GetWindowPid(h, out pid);
                if (pids.Contains(pid))
                {
                    StringBuilder text = new StringBuilder(256);
                    GetWindowText(h, text, 256);
                    if (text.Length > 0) titles.Add(text.ToString());
                }
            }
            return true;
        }, IntPtr.Zero);

        foreach (IntPtr f in frames)
        {
            IntPtr core = FindWindowEx(f, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
            if (core == IntPtr.Zero) continue;
            uint pid;
            GetWindowPid(core, out pid);
            if (pids.Contains(pid)) return f;
        }
        foreach (IntPtr f in frames)
        {
            StringBuilder text = new StringBuilder(256);
            GetWindowText(f, text, 256);
            if (text.Length > 0 && titles.Contains(text.ToString())) return f;
        }
        return IntPtr.Zero;
    }

    // Runs as administrator. Windows asks first; answering No is not an error.
    static void StartAdmin(string file, string args)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args ?? "");
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            if (ex.NativeErrorCode != 1223) MessageBox.Show(ex.Message, "WinTools"); // 1223: the user said No
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "WinTools");
        }
    }

    static void Start(string file, string args, string dir, bool shell)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args ?? "");
            psi.UseShellExecute = shell;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) psi.WorkingDirectory = dir;
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "WinTools");
        }
    }

    void OpenMenu()
    {
        if (open) return;
        open = true;
        lastKey = DateTime.Now;
        folderUnderMouse = FolderUnderCursor();
        Point p = Cursor.Position;
        Rectangle wa = Screen.FromPoint(p).WorkingArea;
        int x = Math.Max(wa.Left, Math.Min(p.X, wa.Right - Width));
        int y = Math.Max(wa.Top, Math.Min(p.Y, wa.Bottom - Height));
        Location = new Point(x, y);
        timer.Start();
        TakeFocus();
        Invalidate();
        // Look for a new version each time the menu opens (at most once a minute).
        if (DateTime.Now - lastUpdateCheck > TimeSpan.FromMinutes(1)) CheckForUpdates();
    }

    // Windows only lets a window take focus if it is allowed to. Attaching to the current foreground
    // thread for a moment makes SetForegroundWindow work, so the letter keys reach this window.
    void TakeFocus()
    {
        IntPtr fg = GetForegroundWindow();
        if (fg != Handle) previousForeground = fg;
        uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        SetForegroundWindow(Handle);
        if (attached) AttachThreadInput(me, fgThread, false);
        Activate();
        // Opening must not leave the text pad focused, or the letters would be typed into it.
        ActiveControl = null;
        Focus();
    }

    // giveBackFocus false: the menu keeps the foreground so a program started right after can take it over.
    void CloseMenu(bool giveBackFocus = true)
    {
        if (!open) return;
        open = false;
        hover = -1;
        top = 0;
        timer.Stop();
        Location = Parked;
        // The parked window would otherwise keep the keyboard. Give it back to the window that had it.
        if (giveBackFocus) GiveFocusTo(previousForeground);
        previousForeground = IntPtr.Zero;
    }

    void GiveFocusTo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || hwnd == Handle) return;
        uint fgThread = GetWindowThreadProcessId(hwnd, IntPtr.Zero);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        SetForegroundWindow(hwnd);
        if (attached) AttachThreadInput(me, fgThread, false);
    }

    void ShowSettings()
    {
        CloseMenu();
        if (settings == null || settings.IsDisposed)
        {
            List<Entry> copy = new List<Entry>();
            foreach (Entry e in entries) copy.Add(e.Clone());
            settings = new SettingsForm(this, copy);
        }
        settings.Show();
        settings.Activate();
    }

    // Clicking outside (or switching to another window) takes focus away and closes the menu.
    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        CloseMenu();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!open) return;
        lastKey = DateTime.Now;
        if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; CloseMenu(); return; }
        // Typing in the pad: letters are text, not shortcuts.
        if (pad.Focused) return;
        if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; ScrollBy(1); return; }
        if (e.KeyCode == Keys.Up) { e.SuppressKeyPress = true; ScrollBy(-1); return; }
        if (e.KeyCode == Keys.PageDown) { e.SuppressKeyPress = true; ScrollBy(VisibleRows()); return; }
        if (e.KeyCode == Keys.PageUp) { e.SuppressKeyPress = true; ScrollBy(-VisibleRows()); return; }
        if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
        {
            char c = (char)('A' + (int)(e.KeyCode - Keys.A));
            foreach (Entry en in shown)
            {
                if (en.Letter == c.ToString() && ItemEnabled(en))
                {
                    e.SuppressKeyPress = true;
                    Launch(en);
                    return;
                }
            }
        }
    }

    void OnTimer()
    {
        if ((DateTime.Now - lastKey).TotalSeconds > 60) CloseMenu();
    }

    // The item on the row under (x, y), counting from the scroll position. -1 if there is none.
    int ItemAt(int x, int y)
    {
        if (x < 0 || x >= ListWidth || y < HeaderHeight) return -1;
        int row = (y - HeaderHeight) / RowHeight;
        if (row >= VisibleRows()) return -1;
        int index = top + row;
        return index < shown.Count ? index : -1;
    }

    // Moves the list by whole rows. Used by the mouse wheel and the arrow, Page Up and Page Down keys.
    void ScrollBy(int rows)
    {
        int next = Math.Max(0, Math.Min(MaxTop(), top + rows));
        if (next == top) return;
        top = next;
        hover = -1;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollBy(e.Delta > 0 ? -3 : 3);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = ItemAt(e.X, e.Y);
        int fh = FooterAt(e.X, e.Y);
        if (h != hover || fh != footerHover)
        {
            hover = h;
            footerHover = fh;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!open || e.Button != MouseButtons.Left) return;
        int fi = FooterAt(e.X, e.Y);
        if (fi >= 0)
        {
            FooterItem footerItem = Footer()[fi];
            CloseMenu();
            BeginInvoke(new Action(footerItem.Run));
            return;
        }
        int i = ItemAt(e.X, e.Y);
        if (i < 0 || !ItemEnabled(shown[i])) return;
        Launch(shown[i]);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        // Hint above the list, in the same style as the "Plain text" title above the pad.
        TextRenderer.DrawText(g, Config.Hotkey + " again: customise", headFont,
            new Rectangle(10, 8, ListWidth - 10, 14), Color.FromArgb(156, 163, 175), TextFormatFlags.NoPadding);
        int visible = VisibleRows();
        for (int row = 0; row < visible && top + row < shown.Count; row++)
        {
            int i = top + row;
            Rectangle r = new Rectangle(0, HeaderHeight + row * RowHeight, ListWidth, RowHeight);
            if (i == hover)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(55, 65, 81)))
                {
                    g.FillRectangle(b, r);
                }
            }
            DrawItem(g, shown[i], r);
        }
        DrawFooter(g);
    }

    int FooterTop(int rows) { return HeaderHeight + rows * RowHeight + 10; }

    List<FooterItem> Footer()
    {
        List<FooterItem> f = new List<FooterItem>();
        bool updateWaiting = latestVersion != null && latestVersion != Config.Skipped;
        if (updateWaiting)
        {
            f.Add(new FooterItem("Update to " + latestVersion, delegate { StartUpdate(); }));
            f.Add(new FooterItem("Skip this version", delegate { SkipCurrentUpdate(); }));
        }
        return f;
    }

    int FooterAt(int x, int y)
    {
        if (x < 0 || x >= ListWidth) return -1;
        int footTop = FooterTop(VisibleRows());
        if (y < footTop) return -1;
        int i = (y - footTop) / RowHeight;
        return i < Footer().Count ? i : -1;
    }

    void DrawFooter(Graphics g)
    {
        if (Footer().Count == 0) return;
        int footTop = FooterTop(VisibleRows());
        using (Pen sep = new Pen(Color.FromArgb(55, 65, 81)))
        {
            g.DrawLine(sep, 10, footTop - 5, ListWidth - 10, footTop - 5);
        }
        List<FooterItem> f = Footer();
        for (int i = 0; i < f.Count; i++)
        {
            Rectangle r = new Rectangle(0, footTop + i * RowHeight, ListWidth, RowHeight);
            if (i == footerHover)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(55, 65, 81))) { g.FillRectangle(b, r); }
            }
            TextRenderer.DrawText(g, f[i].Label, font, new Rectangle(r.X + 8, r.Y, r.Width - 8, r.Height),
                Color.FromArgb(209, 213, 219), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
        }
    }

    // ----- updates (read from the public repo over HTTPS; nothing to install or log in to) -----

    static string PowerShellExe()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
    }

    static bool IsNewer(string latest, string current)
    {
        try { return new Version(latest) > new Version(current); }
        catch (Exception) { return false; }
    }

    // Reads a small text file from GitHub. Throws if it cannot be reached.
    static string FetchText(string url)
    {
        System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072; // TLS 1.2
        System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
        req.Timeout = 15000;
        req.UserAgent = "WinToolsMenu";
        using (System.Net.HttpWebResponse res = (System.Net.HttpWebResponse)req.GetResponse())
        using (StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
        {
            return sr.ReadToEnd();
        }
    }

    // Runs in the background. Asks GitHub for the VERSION file.
    void CheckForUpdates()
    {
        if (checkingUpdates) return;
        checkingUpdates = true;
        lastUpdateCheck = DateTime.Now;
        ThreadPool.QueueUserWorkItem(delegate
        {
            string latest = null;
            try
            {
                string v = FetchText(RawBase + "VERSION");
                if (v != null) latest = v.Trim();
            }
            catch (Exception)
            {
                // Offline or GitHub unreachable: the menu just shows no update.
            }
            string foundLatest = latest;
            try { BeginInvoke(new Action(delegate { ApplyCheck(foundLatest); })); }
            catch (Exception) { }
        });
    }

    void ApplyCheck(string latest)
    {
        checkingUpdates = false;
        latestVersion = (latest != null && IsNewer(latest, AppVersion)) ? latest : null;
        footerHover = -1;
        LayoutMenu();
        Invalidate();
    }

    // From the tray menu and Custom Shortcuts: forgets "Skip this version", checks GitHub now and offers the update.
    public void CheckNowIgnoringSkip()
    {
        Config.Skipped = null;
        try { Config.Save(entries); } catch (Exception) { }
        ThreadPool.QueueUserWorkItem(delegate
        {
            string latest = null;
            try { latest = FetchText(RawBase + "VERSION").Trim(); } catch (Exception) { }
            string found = latest;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    ApplyCheck(found);
                    if (found == null)
                    {
                        MessageBox.Show("Could not reach GitHub. Check the internet connection and try again.", "WinTools");
                    }
                    else if (latestVersion == null)
                    {
                        MessageBox.Show("WinTools Menu " + AppVersion + " is the newest version.", "WinTools");
                    }
                    else if (MessageBox.Show("Version " + latestVersion + " is available (you have " + AppVersion + ").\n\nUpdate now?",
                        "WinTools", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        StartUpdate();
                    }
                }));
            }
            catch (Exception) { }
        });
    }

    void SkipCurrentUpdate()
    {
        Config.Skipped = latestVersion;
        try { Config.Save(entries); } catch (Exception) { }
        footerHover = -1;
        LayoutMenu();
        Invalidate();
    }

    // Downloads the program's files from the repo's main branch into a temp folder and runs its install.ps1,
    // which replaces this program and starts the new one. The menu closes and reopens by itself.
    void StartUpdate()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "WindowsMenuTool-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        string ps1 = Path.Combine(Path.GetTempPath(), "WindowsMenuTool-update.ps1");
        File.WriteAllText(ps1,
            "$ErrorActionPreference = 'Stop'\r\n" +
            "try {\r\n" +
            "  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12\r\n" +
            "  New-Item -ItemType Directory -Force -Path '" + tmp + "' | Out-Null\r\n" +
            // raw.githubusercontent.com caches main/ for up to 5 minutes, so files right after a push can be a mix
            // of old and new. Files at the exact commit of main are never stale. If GitHub's API fails, use main/.
            "  $base = '" + RawBase + "'\r\n" +
            "  try {\r\n" +
            "    $sha = (Invoke-RestMethod -UseBasicParsing -UserAgent 'WinToolsMenu' -Uri '" + ApiCommit + "').sha\r\n" +
            "    if ($sha) { $base = '" + RawRoot + "' + $sha + '/' }\r\n" +
            "  } catch { }\r\n" +
            "  foreach ($f in 'WinToolsMenu.cs', 'install.ps1', 'uninstall.ps1', 'update.ps1', 'update.cmd', 'WinTools.ico') {\r\n" +
            "    Invoke-WebRequest -UseBasicParsing -Uri ($base + $f) -OutFile (Join-Path '" + tmp + "' $f)\r\n" +
            "  }\r\n" +
            "  & (Join-Path '" + tmp + "' 'install.ps1')\r\n" +
            "} catch {\r\n" +
            "  Write-Host ('Update failed: ' + $_.Exception.Message)\r\n" +
            "  Read-Host 'Press Enter to close'\r\n" +
            "}\r\n", Encoding.UTF8);
        Start(PowerShellExe(), "-NoProfile -ExecutionPolicy Bypass -File \"" + ps1 + "\"", null, true);
    }

    // Started from the View on GitHub button in Custom Shortcuts.
    public static void OpenGitHub()
    {
        Start("https://github.com/" + RepoOwner + "/" + RepoName, "", null, true);
    }

    // Started from the Uninstall button in Custom Shortcuts.
    public static void UninstallProgram()
    {
        if (MessageBox.Show("Uninstall WinTools Menu? You will be asked separately about your settings.", "WinTools",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
        string script = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinToolsMenu", "uninstall.ps1");
        Start(PowerShellExe(), "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"", null, true);
    }

    void DrawItem(Graphics g, Entry en, Rectangle r)
    {
        TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        Rectangle textRect = new Rectangle(r.X + 8, r.Y, r.Width - 8, r.Height);
        Color textColor = ItemEnabled(en) ? Color.White : Color.FromArgb(107, 114, 128);
        string label = en.Title ?? "";
        TextRenderer.DrawText(g, label, font, textRect, textColor, flags);

        // Underline the letter in the name. If the name no longer has it, show it after the name.
        char key = string.IsNullOrEmpty(en.Letter) ? '\0' : en.Letter[0];
        if (key == '\0') return;
        int idx = -1;
        for (int i = 0; i < label.Length; i++)
        {
            if (char.ToUpperInvariant(label[i]) == key) { idx = i; break; }
        }
        Size big = new Size(int.MaxValue, int.MaxValue);
        if (idx < 0)
        {
            string tag = "(" + en.Letter + ")";
            int tx = textRect.X + TextRenderer.MeasureText(g, label, font, big, flags).Width + 6;
            TextRenderer.DrawText(g, tag, font, new Rectangle(tx, r.Y, r.Width, r.Height), Color.FromArgb(251, 191, 36), flags);
            return;
        }
        string pre = label.Substring(0, idx);
        string letter = label.Substring(idx, 1);
        int x0 = textRect.X + (pre.Length == 0 ? 0 : TextRenderer.MeasureText(g, pre, font, big, flags).Width);
        int w = TextRenderer.MeasureText(g, letter, font, big, flags).Width;
        int underlineY = r.Y + r.Height - 6;
        using (Pen pen = new Pen(Color.FromArgb(251, 191, 36), 2f))
        {
            g.DrawLine(pen, x0, underlineY, x0 + w, underlineY);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        UnregisterOwnHotkey();
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosed(e);
    }
}

public sealed class SteamGame
{
    public string AppId;
    public string Name;
}

// Finds installed Steam games by reading Steam's own files (libraryfolders.vdf and appmanifest_*.acf).
static class Steam
{
    public static string SteamRoot()
    {
        string p = null;
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                if (k != null) p = k.GetValue("SteamPath") as string;
            }
        }
        catch (Exception) { }
        if (string.IsNullOrEmpty(p))
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (k != null) p = k.GetValue("InstallPath") as string;
                }
            }
            catch (Exception) { }
        }
        if (string.IsNullOrEmpty(p)) return null;
        string full = Path.GetFullPath(p.Replace('/', '\\'));
        return Directory.Exists(full) ? full : null;
    }

    // The main Steam folder plus every extra library listed in libraryfolders.vdf.
    public static List<string> LibraryFolders(string root)
    {
        List<string> libs = new List<string>();
        libs.Add(root);
        string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) return libs;
        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
        {
            string p = m.Groups[1].Value.Replace("\\\\", "\\");
            if (!Directory.Exists(p)) continue;
            bool known = false;
            foreach (string l in libs)
            {
                if (string.Equals(l, p, StringComparison.OrdinalIgnoreCase)) { known = true; break; }
            }
            if (!known) libs.Add(p);
        }
        return libs;
    }

    public static List<SteamGame> Installed()
    {
        List<SteamGame> games = new List<SteamGame>();
        string root = SteamRoot();
        if (root == null) return games;
        foreach (string lib in LibraryFolders(root))
        {
            string apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (string f in Directory.GetFiles(apps, "appmanifest_*.acf"))
            {
                string text = File.ReadAllText(f);
                Match id = Regex.Match(text, "\"appid\"\\s+\"(\\d+)\"");
                Match name = Regex.Match(text, "\"name\"\\s+\"([^\"]+)\"");
                if (!id.Success || !name.Success) continue;
                string appId = id.Groups[1].Value;
                string title = name.Groups[1].Value;
                // Steam's own tools are not games. Skip them, and skip an app listed twice.
                if (title.StartsWith("Proton", StringComparison.OrdinalIgnoreCase)) continue;
                if (title.StartsWith("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase)) continue;
                if (title.StartsWith("Steamworks Common", StringComparison.OrdinalIgnoreCase)) continue;
                bool seen = false;
                foreach (SteamGame g in games)
                {
                    if (g.AppId == appId) { seen = true; break; }
                }
                if (!seen) games.Add(new SteamGame { AppId = appId, Name = title });
            }
        }
        games.Sort(delegate(SteamGame a, SteamGame b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
        return games;
    }
}

static class SteamPicker
{
    // Shows the installed games. Returns the picked ones, or an empty list.
    public static List<SteamGame> Pick(IWin32Window owner)
    {
        List<SteamGame> games = Steam.Installed();
        List<SteamGame> result = new List<SteamGame>();
        using (Form f = new Form())
        {
            f.Text = "Add a Steam game";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ClientSize = new Size(420, 420);

            Label info = new Label();
            info.Text = games.Count == 0
                ? "No installed Steam games were found. Is Steam installed here?"
                : "Pick one or more games (Ctrl+click). They start through Steam.";
            info.Location = new Point(12, 10);
            info.AutoSize = true;
            f.Controls.Add(info);

            ListView lv = new ListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.MultiSelect = true;
            lv.HideSelection = false;
            lv.Location = new Point(12, 36);
            lv.Size = new Size(396, 330);
            lv.Columns.Add("Game", 300);
            lv.Columns.Add("App ID", 80);
            foreach (SteamGame g in games)
            {
                ListViewItem it = new ListViewItem(g.Name);
                it.SubItems.Add(g.AppId);
                it.Tag = g;
                lv.Items.Add(it);
            }
            f.Controls.Add(lv);

            Button ok = new Button();
            ok.Text = "Add";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(236, 376);
            ok.Size = new Size(80, 30);
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(328, 376);
            cancel.Size = new Size(80, 30);
            f.Controls.Add(ok);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;

            if (f.ShowDialog(owner) == DialogResult.OK)
            {
                foreach (ListViewItem it in lv.SelectedItems) result.Add((SteamGame)it.Tag);
            }
        }
        return result;
    }
}

// Custom Shortcuts: untick to hide, rename, move, add files (or drag them in). Save writes menu.json.
sealed class SettingsForm : Form
{
    readonly MenuHost host;
    readonly List<Entry> work;
    readonly ListView list = new ListView();
    readonly Label hint = new Label();
    readonly Label hotkeyLabel = new Label();
    readonly TextBox hotkeyBox = new TextBox();
    string hotkeyText;   // the hotkey this window will save

    public SettingsForm(MenuHost host, List<Entry> entries)
    {
        this.host = host;
        work = entries;
        Text = "WinTools Custom Shortcuts - version " + MenuHost.Version;
        Icon = Dialogs.AppIcon(new Size(32, 32));
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(920, 540);
        KeyPreview = true;
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        hint.Text = "Untick to hide an entry. Drag files here to add them. Double-click the Key column to change a letter.";
        hint.Location = new Point(12, 10);
        hint.AutoSize = true;
        Controls.Add(hint);

        list.View = View.Details;
        list.FullRowSelect = true;
        list.CheckBoxes = true;
        list.HideSelection = false;
        list.Location = new Point(12, 36);
        list.Size = new Size(896, 400);
        list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        list.Columns.Add("Key", 50);
        list.Columns.Add("Name", 240);
        list.Columns.Add("Target", 400);
        list.Columns.Add("Type", 80);
        list.AllowDrop = true;
        list.DragEnter += OnDragEnter;
        list.DragDrop += OnDragDrop;
        list.ItemChecked += delegate(object s, ItemCheckedEventArgs e) { ((Entry)e.Item.Tag).Hidden = !e.Item.Checked; };
        list.MouseDoubleClick += delegate(object s, MouseEventArgs e)
        {
            ListViewHitTestInfo hit = list.HitTest(e.Location);
            if (hit.Item != null && hit.Item.SubItems.IndexOf(hit.SubItem) == 0) ChangeLetter();
        };
        Controls.Add(list);

        AddButton("Up", 12, delegate { MoveSelected(-1); });
        AddButton("Down", 96, delegate { MoveSelected(1); });
        AddButton("Rename", 180, delegate { Rename(); });
        AddButton("Add files...", 264, delegate { AddWithDialog(); });
        AddButton("Remove custom", 372, delegate { RemoveCustom(); });
        AddButton("Reset to built-in", 500, delegate { ResetAll(); });
        AddButton("Change letter...", 628, delegate { ChangeLetter(); });
        AddButton("Cancel", 750, delegate { Close(); });
        AddButton("Save", 834, delegate { Save(); });

        Button steamBtn = new Button();
        steamBtn.Text = "Add Steam game...";
        steamBtn.Location = new Point(12, 492);
        steamBtn.Size = new Size(160, 30);
        steamBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        steamBtn.Click += delegate { AddSteam(); };
        Controls.Add(steamBtn);

        Button uninstallBtn = new Button();
        uninstallBtn.Text = "Uninstall program...";
        uninstallBtn.Location = new Point(748, 492);
        uninstallBtn.Size = new Size(160, 30);
        uninstallBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        uninstallBtn.Click += delegate { MenuHost.UninstallProgram(); };
        Controls.Add(uninstallBtn);

        Button updateBtn = new Button();
        updateBtn.Text = "Check for updates...";
        updateBtn.Location = new Point(578, 492);
        updateBtn.Size = new Size(160, 30);
        updateBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        updateBtn.Click += delegate { host.CheckNowIgnoringSkip(); };
        Controls.Add(updateBtn);

        Button githubBtn = new Button();
        githubBtn.Text = "View on GitHub";
        githubBtn.Location = new Point(746, 4);
        githubBtn.Size = new Size(162, 26);
        githubBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        githubBtn.Click += delegate { MenuHost.OpenGitHub(); };
        Controls.Add(githubBtn);

        hotkeyText = Config.Hotkey;
        hotkeyLabel.Text = "Menu hotkey:";
        hotkeyLabel.AutoSize = true;
        hotkeyLabel.Location = new Point(190, 498);
        hotkeyLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(hotkeyLabel);

        hotkeyBox.Text = hotkeyText;
        hotkeyBox.ReadOnly = true;
        hotkeyBox.Location = new Point(275, 494);
        hotkeyBox.Size = new Size(190, 24);
        hotkeyBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        hotkeyBox.KeyDown += OnHotkeyKey;
        hotkeyBox.GotFocus += delegate { host.SuspendHotkey(); hotkeyBox.Text = "Press the new keys..."; };
        hotkeyBox.LostFocus += delegate { hotkeyBox.Text = hotkeyText; host.ResumeHotkey(); };
        Controls.Add(hotkeyBox);

        Button hotkeyDefault = new Button();
        hotkeyDefault.Text = "Default";
        hotkeyDefault.Location = new Point(475, 493);
        hotkeyDefault.Size = new Size(80, 26);
        hotkeyDefault.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        hotkeyDefault.Click += delegate { hotkeyText = "Win+Alt+X"; hotkeyBox.Text = hotkeyText; };
        Controls.Add(hotkeyDefault);

        Refresh(-1);
    }

    // Lists the installed Steam games and adds the picked ones as steam://rungameid/<id> entries.
    void AddSteam()
    {
        List<SteamGame> picked = SteamPicker.Pick(this);
        bool added = false;
        foreach (SteamGame g in picked)
        {
            string target = "steam://rungameid/" + g.AppId;
            bool exists = false;
            foreach (Entry e in work)
            {
                if (string.Equals(e.Target, target, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
            }
            if (exists) continue;
            work.Add(new Entry
            {
                Id = "steam" + g.AppId,
                Custom = true,
                Title = g.Name,
                Target = target,
                Args = "",
                Letter = AutoLetter(g.Name),
                Hidden = false,
                NeedsFolder = false
            });
            added = true;
        }
        if (added) Refresh(work.Count - 1);
    }

    void AddButton(string text, int x, EventHandler click)
    {
        Button b = new Button();
        b.Text = text;
        b.Location = new Point(x, 452);
        b.Size = new Size(text == "Remove custom" || text == "Reset to built-in" || text == "Change letter..." ? 116 : 80, 30);
        b.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        b.Click += click;
        Controls.Add(b);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) Close();
    }

    int Selected()
    {
        return list.SelectedIndices.Count > 0 ? list.SelectedIndices[0] : -1;
    }

    void Refresh(int select)
    {
        list.BeginUpdate();
        list.Items.Clear();
        foreach (Entry e in work)
        {
            ListViewItem it = new ListViewItem(string.IsNullOrEmpty(e.Letter) ? "-" : e.Letter);
            it.Tag = e;
            it.Checked = !e.Hidden;
            it.SubItems.Add(e.Title ?? "");
            it.SubItems.Add(e.Custom ? (e.Target ?? "") : (Config.Expand(e.Target) + " " + Config.Expand(e.Args)).Trim());
            it.SubItems.Add(e.Custom ? "Custom" : "Built-in");
            list.Items.Add(it);
        }
        list.EndUpdate();
        if (select >= 0 && select < list.Items.Count)
        {
            list.Items[select].Selected = true;
            list.Items[select].Focused = true;
            list.EnsureVisible(select);
        }
    }

    void MoveSelected(int delta)
    {
        int i = Selected();
        int j = i + delta;
        if (i < 0 || j < 0 || j >= work.Count) return;
        Entry tmp = work[i];
        work[i] = work[j];
        work[j] = tmp;
        Refresh(j);
    }

    // Picks a new letter for the selected entry. If another entry has it, the two swap letters.
    void ChangeLetter()
    {
        int i = Selected();
        if (i < 0) return;
        string v = Dialogs.PickLetter(this, work[i].Title, work[i].Letter);
        if (v == null) return;
        if (v.Length > 0)
        {
            foreach (Entry other in work)
            {
                if (other != work[i] && string.Equals(other.Letter, v, StringComparison.OrdinalIgnoreCase)) other.Letter = work[i].Letter;
            }
        }
        work[i].Letter = v;
        Refresh(i);
    }

    void Rename()
    {
        int i = Selected();
        if (i < 0) return;
        string v = Dialogs.Prompt("Rename", work[i].Title);
        if (v != null && v.Trim().Length > 0)
        {
            work[i].Title = v.Trim();
            Refresh(i);
        }
    }

    void AddWithDialog()
    {
        using (OpenFileDialog dlg = new OpenFileDialog())
        {
            dlg.Title = "Add programs, scripts or shortcuts";
            dlg.Filter = "Programs and scripts|*.exe;*.bat;*.cmd;*.ps1;*.lnk;*.url;*.msc;*.cpl|All files|*.*";
            dlg.Multiselect = true;
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                foreach (string f in dlg.FileNames) AddFile(f);
                Refresh(work.Count - 1);
            }
        }
    }

    void AddFile(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        work.Add(new Entry
        {
            Id = "c" + Guid.NewGuid().ToString("N"),
            Custom = true,
            Title = name,
            Target = path,
            Args = "",
            Letter = AutoLetter(name),
            Hidden = false,
            NeedsFolder = false
        });
    }

    // First letter of the name that no entry uses yet, otherwise the first free letter A-Z.
    string AutoLetter(string name)
    {
        bool[] used = new bool[26];
        foreach (Entry e in work)
        {
            if (!string.IsNullOrEmpty(e.Letter))
            {
                char c = char.ToUpperInvariant(e.Letter[0]);
                if (c >= 'A' && c <= 'Z') used[c - 'A'] = true;
            }
        }
        foreach (char ch in name)
        {
            char c = char.ToUpperInvariant(ch);
            if (c >= 'A' && c <= 'Z' && !used[c - 'A']) return c.ToString();
        }
        for (int i = 0; i < 26; i++)
        {
            if (!used[i]) return ((char)('A' + i)).ToString();
        }
        return "";
    }

    void RemoveCustom()
    {
        int i = Selected();
        if (i < 0 || !work[i].Custom) return;
        work.RemoveAt(i);
        Refresh(Math.Min(i, work.Count - 1));
    }

    void ResetAll()
    {
        if (MessageBox.Show(this, "Put the built-in entries back as they were? Custom entries are removed.", "WinTools",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
        work.Clear();
        work.AddRange(Config.Defaults());
        Refresh(-1);
    }

    void Save()
    {
        if (!host.ChangeHotkey(hotkeyText))
        {
            MessageBox.Show(this, hotkeyText + " cannot be used. Another program may be using it, or Windows reserves it. Pick another one.",
                "WinTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Config.Hotkey = hotkeyText;
        Config.Save(work);
        host.Reload(work);
        Close();
    }

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

    // Records the next key combination. Needs Win, Ctrl, Alt or Shift together with a key.
    void OnHotkeyKey(object sender, KeyEventArgs e)
    {
        // Escape and Tab keep their usual meaning (close, move on).
        if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Tab) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        Keys k = e.KeyCode;
        if (k == Keys.None || k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin) return;
        bool win = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
        string combo = "";
        if (win) combo += "Win+";
        if (e.Control) combo += "Ctrl+";
        if (e.Alt) combo += "Alt+";
        if (e.Shift) combo += "Shift+";
        if (combo.Length == 0)
        {
            hotkeyBox.Text = "Hold Win, Ctrl, Alt or Shift too";
            return;
        }
        string keyName = k.ToString();
        if (keyName.Length == 2 && keyName[0] == 'D' && char.IsDigit(keyName[1])) keyName = keyName.Substring(1);
        hotkeyText = combo + keyName;
        hotkeyBox.Text = hotkeyText;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        host.ResumeHotkey();
        base.OnFormClosed(e);
    }

    void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void OnDragDrop(object sender, DragEventArgs e)
    {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;
        foreach (string f in files) AddFile(f);
        Refresh(work.Count - 1);
    }
}
