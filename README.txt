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
  starts it, and adds it to Startup so it runs when you sign in. Nothing is downloaded.

BUILT-IN SHORTCUTS (press the letter shown underlined)
  F  Firewall                       R  Add or remove programs
  W  Advanced network settings      T  Task Manager
  G  Device Manager                 M  Computer Management
  S  Sounds                         E  Notepad
  A  Notepad++ (if installed)       D  %appdata% folder
  C  %localappdata% folder          U  Startup folder (shell:startup)
  O  Open CMD in the current folder
  P  Open PowerShell in the current folder
  Q  God Mode (all settings in one folder)
  X  Windows Sandbox (Pro/Enterprise/Education, the feature must be turned on)
  Y  System, About (Settings > System > About)
  K  Systemegenskaper (sysdm.cpl)
  B  Add Bluetooth Device (the Add a device wizard)
  If one of your own items already uses one of these letters, the new item
  gets another free letter.

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
  - Hide, rename, move up or down, and remove items.
  - Reset everything to the built-in list.
  - Change the hotkey: click "Menu hotkey", press the new combination (it must
    include Win, Ctrl, Alt or Shift), then Save. "Default" puts back Win+Alt+X.
  New items get a letter automatically. Save to keep your changes.

  Your settings are stored in %LOCALAPPDATA%\WinTools\menu.json.

UPDATES
  The menu checks GitHub for a newer version when it starts, once a day, and
  when you open it. It never checks more than once a minute. If there is a
  newer version, the footer shows "Update to <version>". Click it to update;
  the menu restarts by itself. "Skip this version" hides that version.

UNINSTALL
  Double-click uninstall.cmd. It stops the menu and removes it from Startup.
  It asks before deleting your settings file.

Startup can also be turned off in Settings > Apps > Startup (WinTools Menu).
