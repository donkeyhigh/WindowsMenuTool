# WinTools Menu: notes for Claude

A keyboard menu for Windows 11 (Win+Alt+X). C# split into files by area, compiled together on the user's PC by install.ps1.
The owner writes in Norwegian; answer in Norwegian. Code, comments, commit messages and README are in English.
**Everything the program shows is in English**: item names, messages, buttons. When the owner gives a name or text
in Norwegian, that is a slip: translate it to English (e.g. "Systemegenskaper" -> "System Properties").
If a built-in item's default name changes, add the old name to `Config.RenamedDefaults` so saved settings follow.
Released (first public release v1.0.31). Other people may have any version from 1.0.31 on, so updates from those
must keep working.
Installed menus update from `donkeyhigh/WindowsMenuTool` on GitHub without signing in: the repo must stay public
and keep its name and owner, or every installed copy stops updating. Releases are only for downloading; updates
do not use them.

## Files
Read only the file for the area you change; the list below says what is where.

| File | What is in it |
|---|---|
| `WinToolsMenu.cs` | `Program.Main`, data types `Entry`, `MenuFile`, `FooterItem` |
| `Config.cs` | Built-in items (`Config.Defaults()`), menu.json load/save, new built-ins merging, letters, hotkey parsing |
| `Installers.cs` | Offers to install Notepad++ / Sandboxie-Plus (winget), turn on Windows Sandbox, Sandboxie detection |
| `Dialogs.cs` | Text prompt, letter picker, program icon (`Dialogs.AppIcon`) |
| `MenuHost.cs` | The menu window (`partial`): fields, constants, `AppVersion`, hotkey, layout, open/close, keys, mouse, drawing |
| `MenuHost.Launch.cs` | Running items: folder under the mouse, admin, Sandbox/Sandboxie, Settings window to front, `Start` |
| `MenuHost.Update.cs` | Update checks, `StartUpdate`, Check for updates, View on GitHub, uninstall |
| `Steam.cs` | Steam library scan and game picker |
| `SettingsForm.cs` | Custom Shortcuts window (list, letters, reorder, hotkey, buttons) |
| `install.ps1` / `install.cmd` | Compile all `.cs` files, install to `%LOCALAPPDATA%\WinToolsMenu`, Startup shortcut |
| `update.ps1` / `update.cmd` | Manual update; copied into the install folder |
| `uninstall.ps1` / `uninstall.cmd`, `WinTools.ico`, `VERSION`, `README.md`, `LICENSE` (MIT) | |

Settings live in `%LOCALAPPDATA%\WinTools\menu.json`.

## Rules
- **C# 5 only.** Windows compiles with csc.exe from .NET Framework 4 (no `$"..."`, `?.`, `nameof`, expression bodies,
  `out var`, etc.). References: System.Windows.Forms, System.Drawing, System.Web.Extensions, System.Management.
- Check before every commit (Mono, installed with `apt-get install -y mono-devel`):
  `mcs -langversion:5 -nologo -target:winexe -out:/tmp/t.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Web.Extensions.dll -r:System.Management.dll *.cs`
  It compiles but cannot run the program; say that the change is untested on Windows.
- The `.cs` files have a UTF-8 BOM and CRLF line endings: keep both when editing.
- **A new `.cs` file must be added to `$sources` in `install.ps1`**, or it is not compiled and not downloaded.
  Never delete or rename `WinToolsMenu.cs` or `install.ps1`: older versions download those two by name when they
  update. `install.ps1` downloads any missing files itself, all from one commit.
- **Every pushed change is a release.** Bump the version in both `VERSION` (no trailing newline) and
  `AppVersion` in `MenuHost.cs`. Installed menus see `VERSION` on main and offer the update. Commit message style: what changed, then `Version x.y.z`. Push straight to `main`.
- raw.githubusercontent.com caches `main/` for up to 5 minutes (query strings do not bypass it), so a new version
  can take a few minutes to appear. Updates download files at the exact commit of main for that reason.
- A new built-in item: give it a unique `Id` and a free letter. `Config.Load` keeps the user's title, letter, order
  and hidden state for built-ins, takes target/args from the code, and appends new built-ins (with another free
  letter if theirs is taken).
- Updates (`StartUpdate`, `update.ps1`) download only `install.ps1` from the newest commit and run it with
  `-Commit <sha>`; `install.ps1` fetches every missing file from that commit. The file list is only in `install.ps1`.

## Launching
- `MenuHost.Launch` keeps the menu in the foreground while it starts the item (and calls AllowSetForegroundWindow),
  so new windows come to the front; if nothing took the focus after 3 s, it goes back to the previous window.
- `ms-settings:` pages: `BringSettingsForward` restores an already open, minimized Settings window.
- Folder items (`NeedsFolder`) use `{folder}` / `{folderps}` in Args; `Admin` items start with the `runas` verb.
- Missing Notepad++ / Windows Sandbox: the `Installers` class offers winget installs, enabling the Sandbox feature,
  or Sandboxie-Plus on Windows Home. An installed Sandboxie (Plus or classic) is used instead, and the shortcut renamed.

## Releases
- Keep `README.md` in step with what the program does (built-in list, buttons, behaviour).
- A GitHub Release is made for bigger versions only, when the owner asks: tag `vX.Y.Z` on the commit, attach
  `WinToolsMenu-X.Y.Z.zip` with the files needed to install (all `.cs`, `*.ps1`, `*.cmd`, `WinTools.ico`, `VERSION`,
  `README.md`, `LICENSE`; not `CLAUDE.md`). Installed menus update from `main` either way.
