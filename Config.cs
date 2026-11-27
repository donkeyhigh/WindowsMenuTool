// Built-in items, menu.json (load/save, merging new built-ins), letters and hotkey text.
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
                // A built-in still under a name this program used to give it gets the current name.
                foreach (string[] r in RenamedDefaults)
                {
                    if (s.Id == r[0] && s.Title == r[1]) s.Title = d.Title;
                }
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

    // Built-in names that changed: { id, old name }. Users who kept the old name get the new one.
    // Names end with the Run command (Win+R) in parentheses when there is a short one.
    static readonly string[][] RenamedDefaults =
    {
        new string[] { "sysdm", "Systemegenskaper (sysdm.cpl)" },
        new string[] { "firewall", "firewall.cpl" },
        new string[] { "appwiz", "Add remove programs" },
        new string[] { "ncpa", "Advanced network settings" },
        new string[] { "taskmgr", "Task Manager" },
        new string[] { "devmgmt", "Device Manager" },
        new string[] { "compmgmt", "Computer Management" },
        new string[] { "mmsys", "Sounds" },
        new string[] { "notepad", "notepad" },
        new string[] { "npp", "notepad++" },
        new string[] { "startup", "Shell: startup" },
        new string[] { "sandbox", "Windows Sandbox" },
        new string[] { "about", "System, About" },
        new string[] { "bluetooth", "Add Bluetooth Device" },
        new string[] { "reliability", "Reliability Monitor" },
    };

    public static List<Entry> Defaults()
    {
        List<Entry> l = new List<Entry>();
        l.Add(D("firewall", "Firewall (firewall.cpl)", "{system}\\control.exe", "firewall.cpl", "F", false));
        l.Add(D("appwiz", "Add or remove programs (appwiz.cpl)", "{system}\\control.exe", "appwiz.cpl", "R", false));
        l.Add(D("ncpa", "Advanced network settings (ncpa.cpl)", "{system}\\control.exe", "ncpa.cpl", "W", false));
        l.Add(D("taskmgr", "Task Manager (taskmgr)", "{system}\\taskmgr.exe", "", "T", false));
        l.Add(D("devmgmt", "Device Manager (devmgmt.msc)", "{system}\\devmgmt.msc", "", "G", false));
        l.Add(D("compmgmt", "Computer Management (compmgmt.msc)", "{system}\\compmgmt.msc", "", "M", false));
        l.Add(D("mmsys", "Sounds (mmsys.cpl)", "{system}\\control.exe", "mmsys.cpl", "S", false));
        l.Add(D("notepad", "Notepad (notepad)", "{system}\\notepad.exe", "", "E", false));
        l.Add(D("npp", "Notepad++ (notepad++)", "{programfiles}\\Notepad++\\notepad++.exe", "", "A", false));
        l.Add(D("appdata", "%appdata%", "{windows}\\explorer.exe", "\"{appdata}\"", "D", false));
        l.Add(D("localappdata", "%localappdata%", "{windows}\\explorer.exe", "\"{localappdata}\"", "C", false));
        l.Add(D("startup", "Startup folder (shell:startup)", "{windows}\\explorer.exe", "shell:startup", "U", false));
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
        l.Add(D("sandbox", "Windows Sandbox (WindowsSandbox)", "WindowsSandbox.exe", "", "X", false));
        l.Add(D("about", "System, About (ms-settings:about)", "ms-settings:about", "", "Y", false));
        l.Add(D("sysdm", "System Properties (sysdm.cpl)", "{system}\\control.exe", "sysdm.cpl", "K", false));
        l.Add(D("bluetooth", "Add Bluetooth Device (DevicePairingWizard)", "{system}\\DevicePairingWizard.exe", "", "B", false));
        l.Add(D("reliability", "Reliability Monitor (perfmon /rel)", "{system}\\perfmon.exe", "/rel", "L", false));
        l.Add(D("resmon", "Resource Monitor (resmon)", "{system}\\resmon.exe", "", "V", false));
        l.Add(D("control", "Classic Control Panel (control)", "{system}\\control.exe", "", "Z", false));
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
