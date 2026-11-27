# Installs WinTools Menu for the current user only.
# Compiles the .cs files on this PC with csc.exe (part of Windows) and starts the menu, with a Startup shortcut
# so it starts with Windows. Nothing is downloaded when all files are here (install.cmd from a full copy).
# When files are missing, they are fetched from GitHub: that happens when an older version updates itself,
# because it only downloads the files it knew about, and when update.ps1 runs (it downloads this script only).
param([string]$Commit = '')
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$installDir = Join-Path $env:LOCALAPPDATA 'WinToolsMenu'
$exe = Join-Path $installDir 'WinToolsMenu.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$repo = 'donkeyhigh/WindowsMenuTool'

# Every source file. A new .cs file must be added here.
$sources = 'WinToolsMenu.cs', 'Config.cs', 'Installers.cs', 'Dialogs.cs', 'MenuHost.cs', 'MenuHost.Launch.cs',
           'MenuHost.Update.cs', 'Steam.cs', 'SettingsForm.cs'
$others = 'WinTools.ico', 'uninstall.ps1', 'update.ps1', 'update.cmd'

if (-not (Test-Path -LiteralPath $csc)) { throw "csc.exe not found at $csc. This Windows has no .NET Framework 4." }

$missing = @($sources + $others | Where-Object { -not (Test-Path -LiteralPath (Join-Path $here $_)) })
if ($missing.Count -gt 0) {
  # All files from one commit, so they fit together. raw.githubusercontent.com caches main/ for up to 5 minutes;
  # files at the exact commit are never stale.
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  if (-not $Commit) {
    try { $Commit = (Invoke-RestMethod -UseBasicParsing -UserAgent 'WinToolsMenu' -Uri "https://api.github.com/repos/$repo/commits/main").sha } catch { }
  }
  if (-not $Commit) { $Commit = 'main' }
  Write-Host "Downloading the program files from GitHub ($Commit)..."
  foreach ($f in $sources + $others) {
    Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/$repo/$Commit/$f" -OutFile (Join-Path $here $f)
  }
}

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Get-Process -Name WinToolsMenu -ErrorAction SilentlyContinue | Stop-Process -Force

$ico = Join-Path $here 'WinTools.ico'
$src = @($sources | ForEach-Object { Join-Path $here $_ })
& $csc /nologo /target:winexe /optimize+ "/out:$exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Management.dll "/win32icon:$ico" "/resource:$ico,WinTools.ico" $src
if ($LASTEXITCODE -ne 0) { throw 'Compile failed. Nothing was installed.' }

# Keep the scripts next to the program: "Uninstall program", updates and update.cmd use them.
foreach ($f in 'install.ps1', 'uninstall.ps1', 'update.ps1', 'update.cmd') {
  Copy-Item -LiteralPath (Join-Path $here $f) -Destination $installDir -Force
}

$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'WinToolsMenu.lnk'
$shell = New-Object -ComObject WScript.Shell
$s = $shell.CreateShortcut($lnk)
$s.TargetPath = $exe
$s.WorkingDirectory = $installDir
$s.Save()

Start-Process -FilePath $exe
Write-Host 'Installed. Press Win+Alt+X to open the menu. Press Win+Alt+X again to open Custom Shortcuts.'
