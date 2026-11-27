# WinTools Menu: notes for Claude

A keyboard menu for Windows 11 (Win+Alt+X). One C# file, compiled on the user's PC by install.ps1.
The owner writes in Norwegian; answer in Norwegian. Code, comments, commit messages and README are in English.

## Files
- `WinToolsMenu.cs`: the whole program. Built-in items are in `Config.Defaults()`.
- `install.ps1` / `install.cmd`: compile with csc.exe and install to `%LOCALAPPDATA%\WinToolsMenu`, add to Startup.
- `update.ps1` / `update.cmd`: manual update (copied into the install folder).
- `uninstall.ps1` / `uninstall.cmd`, `WinTools.ico`, `VERSION`, `README.txt`.
- Settings: `%LOCALAPPDATA%\WinTools\menu.json`.

## Rules
- **C# 5 only.** Windows compiles with csc.exe from .NET Framework 4 (no `$"..."`, `?.`, `nameof`, expression bodies,
  `out var`, etc.). References: System.Windows.Forms, System.Drawing, System.Web.Extensions, System.Management.
- Check before every commit (Mono, installed with `apt-get install -y mono-devel`):
  `mcs -langversion:5 -nologo -target:winexe -out:/tmp/t.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Web.Extensions.dll -r:System.Management.dll WinToolsMenu.cs`
  It compiles but cannot run the program; say that the change is untested on Windows.
- `WinToolsMenu.cs` has a UTF-8 BOM and CRLF line endings: keep both when editing.
- **Every pushed change is a release.** Bump the version in both `VERSION` (no trailing newline) and
  `AppVersion` in `WinToolsMenu.cs`. Installed menus see `VERSION` on main and offer the update.
  Commit message style: what changed, then `Version x.y.z`. Push straight to `main`.
- raw.githubusercontent.com caches `main/` for up to 5 minutes (query strings do not bypass it), so a new version
  can take a few minutes to appear. Updates download files at the exact commit of main for that reason.
- A new built-in item: give it a unique `Id` and a free letter. `Config.Load` keeps the user's title, letter, order
  and hidden state for built-ins, takes target/args from the code, and appends new built-ins (with another free
  letter if theirs is taken).
- If the update file list changes, change it in both `MenuHost.StartUpdate` and `update.ps1`. Old versions only
  download the files their own list names, so install.ps1 must cope with a file that is missing.

## Launching
- `MenuHost.Launch` keeps the menu in the foreground while it starts the item (and calls AllowSetForegroundWindow),
  so new windows come to the front; if nothing took the focus after 3 s, it goes back to the previous window.
- `ms-settings:` pages: `BringSettingsForward` restores an already open, minimized Settings window.
- Folder items (`NeedsFolder`) use `{folder}` / `{folderps}` in Args; `Admin` items start with the `runas` verb.
- Missing Notepad++ / Windows Sandbox: the `Installers` class offers winget installs, enabling the Sandbox feature,
  or Sandboxie-Plus on Windows Home.
