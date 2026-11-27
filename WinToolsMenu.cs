// WinTools menu (Win+Alt+X). Items, letters, hidden entries and order come from %LOCALAPPDATA%\WinTools\menu.json.
// Win+Alt+X again while the menu is open opens Custom Shortcuts: untick to hide, rename, move, add files.
// Two columns; a plain text pad on the right. RegisterHotKey (no keyboard hook).
// Esc, a click outside (focus leaves the window), or 60 seconds without a key closes the menu.
// C# 5 (csc.exe from .NET Framework 4). References: System.Windows.Forms, System.Drawing, System.Web.Extensions, System.Management.
// Resource: WinTools.ico (the program icon, also used for the tray and windows).
// Source files (all compiled together by install.ps1):
//   WinToolsMenu.cs      start-up and the data types (Entry, MenuFile, FooterItem)
//   Config.cs            built-in items, menu.json, letters, hotkey parsing
//   Installers.cs        offers to install Notepad++, Windows Sandbox, Sandboxie-Plus
//   Dialogs.cs           small dialogs (text prompt, letter picker) and the program icon
//   MenuHost.cs          the menu window: hotkey, layout, open/close, keys, mouse, drawing
//   MenuHost.Launch.cs   running items: folder under the mouse, admin, Sandbox, Settings window
//   MenuHost.Update.cs   update checks, updating, GitHub, uninstall
//   Steam.cs             Steam library and game picker
//   SettingsForm.cs      the Custom Shortcuts window
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
