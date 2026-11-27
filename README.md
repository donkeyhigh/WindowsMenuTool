# WinTools Menu

A small keyboard menu for Windows 11 with shortcuts to Windows tools and your own programs, files and games.
Press **Win+Alt+X** to open it, then press the underlined letter of an item to start it.

I made this for myself. You are welcome to use it if you want to, but:

> **USE IT AT YOUR OWN RISK.** It changes Windows settings and runs the programs you add. It comes with no
> warranty of any kind. I am not responsible for any damage to your PC, your data or anything else, including if
> your PC overheats, catches fire, or burns down your house or anything else because of it.
> Read the scripts before you run them.

## Install

1. Download the ZIP file from the newest release on the
   [Releases page](https://github.com/donkeyhigh/WindowsMenuTool/releases/latest) and unzip it.
2. Double-click `install.cmd`. If Windows warns about a file from the internet, choose to run it anyway
   (or right-click the ZIP before unzipping, choose Properties and tick **Unblock**).

`install.cmd` compiles the program on your PC (csc.exe comes with Windows), starts it, and adds it to Startup so it
runs when you sign in. Nothing is downloaded, except when you answer Yes to install a missing tool
(see [Missing tools](#missing-tools)).

## Use

| Key | What it does |
|---|---|
| Win+Alt+X | Open the menu |
| A letter | Run the item with that underlined letter |
| Esc / click away | Close |
| Mouse | Click an item to run it |
| Win+Alt+X again | Open Custom Shortcuts while the menu is open |

## Built-in shortcuts

The Run command (Win+R) is shown in parentheses after the name.

**Shown in the menu**

| Key | Item |
|---|---|
| F | Firewall (firewall.cpl) |
| R | Add or remove programs (appwiz.cpl) |
| W | Advanced network settings (ncpa.cpl) |
| B | Add Bluetooth Device (DevicePairingWizard) |
| S | Sounds (mmsys.cpl) |
| N | Open CMD here (admin) |
| H | Open PowerShell here (admin) |
| I | Open Terminal here (admin) |
| A | Notepad++ (notepad++), offers to install it if it is missing |
| Q | God Mode (all settings in one folder) |
| Z | Classic Control Panel (control) |
| X | Windows Sandbox (WindowsSandbox), needs Pro/Enterprise/Education with the feature turned on |
| L | Reliability Monitor (perfmon /rel) |
| V | Resource Monitor (resmon) |
| G | Device Manager (devmgmt.msc) |
| U | Startup folder (shell:startup) |
| D | %appdata% folder |
| C | %localappdata% folder |

**In Custom Shortcuts, unticked** (tick them to show them in the menu)

| Key | Item |
|---|---|
| E | Notepad (notepad) |
| K | System Properties (sysdm.cpl) |
| Y | System, About (ms-settings:about) |
| T | Task Manager (taskmgr) |
| O | Open CMD here |
| P | Open PowerShell here |
| M | Computer Management (compmgmt.msc) |
| J | Open Terminal here |

"Here" is the Explorer folder under the mouse (or the desktop). The (admin) versions run as administrator;
Windows asks first. If one of your own items already uses one of these letters, the new item gets another free
letter.

## Missing tools

- **Notepad++:** if it is not installed, the menu asks if you want it. Yes downloads and installs it automatically
  (winget, or the official installer from GitHub if winget is missing). Windows asks for permission.
- **Windows Sandbox:** if it is off, the menu offers to turn it on (Windows asks for permission, then the PC must
  restart). Windows Home has no Windows Sandbox. If Sandboxie (Plus or classic) is already installed in the usual
  folder, the menu says so, and the shortcut is renamed to Sandboxie / Sandboxie Plus and opens it. Otherwise the
  menu offers to install Sandboxie-Plus with winget. If virtualization is off in the BIOS/UEFI, the menu says so.

Of course you can install these yourself if you would rather do that, and add them (or any other program) to the
list in Custom Shortcuts.

## Custom Shortcuts

Open the menu and press Win+Alt+X again. There you can:

- **Add your own items:** .exe, .bat, .cmd, .ps1, .lnk, web addresses (URLs) and files or folders. Drag them into
  the window, or use "Add files...".
- **Add Steam games:** "Add Steam game..." lists the games installed in your Steam library (Steam must be installed).
- **Hide, rename and remove** items.
- **Change the order:** drag a row to where you want it, or select it and use Up / Down (or Alt+Up / Alt+Down).
  The menu shows the items in this order.
- **Change letters:** select an item and click "Change letter...", or double-click its letter. Press the new letter.
  If another item has it, the two swap. Delete removes the letter.
- **Change the hotkey:** click "Menu hotkey", press the new combination (it must include Win, Ctrl, Alt or Shift),
  then Save. "Default" puts back Win+Alt+X.
- **Reset** everything to the built-in list.

New items get a letter automatically. Save to keep your changes.
Your settings are stored in `%LOCALAPPDATA%\WinTools\menu.json`.

## Updates

The menu checks GitHub for a newer version when it starts, once a day, and when you open it (never more than once a
minute). If there is a newer version, the menu shows "Update to &lt;version&gt;" at the bottom. Click it to update;
the menu restarts by itself. "Skip this version" hides that version.

"Check for updates" (in Custom Shortcuts, or in the tray icon's menu) checks right away, also finds a version you
skipped, and offers to install it. The installed version is shown in the Custom Shortcuts title bar and when you
hover over the tray icon.

To update by hand, run `update.cmd` in `%LOCALAPPDATA%\WinToolsMenu`, or paste this into PowerShell:

```powershell
irm https://raw.githubusercontent.com/donkeyhigh/WindowsMenuTool/main/update.ps1 | iex
```

## Uninstall

Click "Uninstall program..." in Custom Shortcuts, or double-click `uninstall.cmd`. It stops the menu and removes it
from Startup, and asks before deleting your settings file.

Startup can also be turned off in Settings > Apps > Startup (WinTools Menu).

## License

[MIT](LICENSE)
