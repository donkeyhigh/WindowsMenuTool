# Installs WinTools Menu for the current user only.
# Compiles WinToolsMenu.cs on this PC with csc.exe (part of Windows). Nothing is downloaded, except the icon when an update from an old version did not bring it.
# Starts the menu and adds a Startup shortcut so it starts with Windows.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$installDir = Join-Path $env:LOCALAPPDATA 'WinToolsMenu'
$exe = Join-Path $installDir 'WinToolsMenu.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src = Join-Path $here 'WinToolsMenu.cs'
$ico = Join-Path $here 'WinTools.ico'

if (-not (Test-Path -LiteralPath $csc)) { throw "csc.exe not found at $csc. This Windows has no .NET Framework 4." }
if (-not (Test-Path -LiteralPath $src)) { throw 'WinToolsMenu.cs must be next to this script.' }

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Get-Process -Name WinToolsMenu -ErrorAction SilentlyContinue | Stop-Process -Force

# Updates from version 1.0.16 and older do not bring the icon along; fetch it then. Without it the standard icon is used.
if (-not (Test-Path -LiteralPath $ico)) {
  try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri 'https://raw.githubusercontent.com/donkeyhigh/WindowsMenuTool/main/WinTools.ico' -OutFile $ico
  } catch { }
}
$iconArgs = @()
if (Test-Path -LiteralPath $ico) { $iconArgs = @("/win32icon:$ico", "/resource:$ico,WinTools.ico") }

& $csc /nologo /target:winexe /optimize+ "/out:$exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Management.dll @iconArgs $src
if ($LASTEXITCODE -ne 0) { throw 'Compile failed. Nothing was installed.' }

# Keep the scripts next to the program: the menu's "Uninstall program" and updates use them.
Copy-Item -LiteralPath (Join-Path $here 'uninstall.ps1') -Destination $installDir -Force
Copy-Item -LiteralPath (Join-Path $here 'install.ps1') -Destination $installDir -Force
# update.cmd / update.ps1 there update by hand. Updates from 1.0.22 and older do not bring them along.
foreach ($f in 'update.ps1', 'update.cmd') {
  $p = Join-Path $here $f
  if (Test-Path -LiteralPath $p) { Copy-Item -LiteralPath $p -Destination $installDir -Force }
}

$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'WinToolsMenu.lnk'
$shell = New-Object -ComObject WScript.Shell
$s = $shell.CreateShortcut($lnk)
$s.TargetPath = $exe
$s.WorkingDirectory = $installDir
$s.Save()

Start-Process -FilePath $exe
Write-Host 'Installed. Press Win+Alt+X to open the menu. Press Win+Alt+X again to open Custom Shortcuts.'
