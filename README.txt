WinTools Menu
=============

A small keyboard menu for Windows 11 with shortcuts to Windows tools and
your own programs, files and games. Press Win+Alt+X to open it, then press
the underlined letter of an item to start it.

I made this for myself. You are welcome to use it if you want to, but:

  USE IT AT YOUR OWN RISK. It changes Windows settings and runs the programs
  you add. It comes with no warranty of any kind. I am not responsible for any
  damage to your PC, your data or anything else, including if your PC overheats,
  catches fire, or burns down your house or anything else because of it.
  Read the scripts before you run them.

INSTALL
  Double-click install.cmd. It compiles the program on your PC (csc.exe comes with Windows),
  starts it, and adds it to Startup so it runs when you sign in. Nothing is downloaded,
  except when you answer Yes to install a missing tool (see MISSING TOOLS).

BUILT-IN SHORTCUTS (press the letter shown underlined)
  The Run command (Win+R) is shown in parentheses after the name.
  F  Firewall (firewall.cpl)
  R  Add or remove programs (appwiz.cpl)
  W  Advanced network settings (ncpa.cpl)
  T  Task Manager (taskmgr)
  G  Device Manager (devmgmt.msc)
  M  Computer Management (compmgmt.msc)
  S  Sounds (mmsys.cpl)
  E  Notepad (notepad)
  A  Notepad++ (notepad++) - offers to install it if it is missing
  D  %appdata% folder
  C  %localappdata% folder
  U  Startup folder (shell:startup)
  O  Open CMD here (admin)              N  Open CMD here
  P  Open PowerShell here (admin)       H  Open PowerShell here
  I  Open Terminal here (admin)         J  Open Terminal here
     "Here" is the Explorer folder under the mouse (or the desktop). The
     administrator versions are shown; the normal ones are in Custom Shortcuts,
     unticked - tick them to show them too.
  Q  God Mode (all settings in one folder)
  X  Windows Sandbox (WindowsSandbox) - Pro/Enterprise/Education, turned on
  Y  System, About (ms-settings:about)
  K  System Properties (sysdm.cpl)
  B  Add Bluetooth Device (DevicePairingWizard)
  L  Reliability Monitor (perfmon /rel)
  V  Resource Monitor (resmon)
  Z  Classic Control Panel (control)
  If one of your own items already uses one of these letters, the new item
  gets another free letter.

MISSING TOOLS
  Notepad++   If it is not installed, the menu asks if you want it. Yes downloads
              and installs it automatically (winget, or the official installer
              from GitHub if winget is missing). Windows asks for permission.
  Sandbox     If Windows Sandbox is off, the menu offers to turn it on (Windows
              asks for permission, then the PC must restart). Windows Home has no
              Windows Sandbox. If Sandboxie (Plus or classic) is already installed
              in the usual folder, the menu says so and the shortcut is renamed to
              Sandboxie / Sandboxie Plus and opens it. Otherwise the menu offers to
              install Sandboxie-Plus with winget. If virtualization is off in the
              BIOS/UEFI, the menu says so.
  Of course you can install these yourself if you would rather do that, and add
  them (or any other program) to the list in Custom Shortcuts.

USE
  Win+Alt+X          open the menu
  letter             run the item with that underlined letter
  Esc / click away   close
  Mouse              click an item to run it
  Win+Alt+X again    open Custom Shortcuts while the menu is open

CUSTOM SHORTCUTS
  Open the menu and press Win+Alt+X again. There you can:
  - Add your own items: .exe, .bat, .cmd, .ps1, .lnk, web addresses (URLs)
    and files or folders. Drag them into the window, or use "Add files...".
  - Add Steam games: "Add Steam game..." lists the games installed in your
    Steam library (Steam must be installed).
  - Hide, rename and remove items.
  - Change the order: drag a row to where you want it, or select it and use
    Up / Down (or Alt+Up / Alt+Down). The menu shows the items in this order.
  - Reset everything to the built-in list.
  - Change the hotkey: click "Menu hotkey", press the new combination (it must
    include Win, Ctrl, Alt or Shift), then Save. "Default" puts back Win+Alt+X.
  - Change letters: select an item and click "Change letter...", or double-click
    its letter. Press the new letter. If another item has it, the two swap.
    Delete removes the letter.
  New items get a letter automatically. Save to keep your changes.

  Your settings are stored in %LOCALAPPDATA%\WinTools\menu.json.

UPDATES
  The menu checks GitHub for a newer version when it starts, once a day, and
  when you open it. It never checks more than once a minute. If there is a
  newer version, the footer shows "Update to <version>". Click it to update;
  the menu restarts by itself. "Skip this version" hides that version.
  "Check for updates" (in Custom Shortcuts, or in the tray icon's menu) checks
  right away, also finds a version you skipped, and offers to install it.
  The installed version is shown in the Custom Shortcuts title bar and when you
  hover over the tray icon.

  To update by hand, run update.cmd in %LOCALAPPDATA%\WinToolsMenu, or paste
  this into PowerShell:
    irm https://raw.githubusercontent.com/donkeyhigh/WindowsMenuTool/main/update.ps1 | iex

UNINSTALL
  Double-click uninstall.cmd. It stops the menu and removes it from Startup.
  It asks before deleting your settings file.

Startup can also be turned off in Settings > Apps > Startup (WinTools Menu).
