# Installs WinTools Menu for the current user only.
# Compiles WinToolsMenu.cs on this PC with csc.exe (part of Windows). Nothing is downloaded.
# Starts the menu and adds a Startup shortcut so it starts with Windows.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$installDir = Join-Path $env:LOCALAPPDATA 'WinToolsMenu'
$exe = Join-Path $installDir 'WinToolsMenu.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src = Join-Path $here 'WinToolsMenu.cs'

if (-not (Test-Path -LiteralPath $csc)) { throw "csc.exe not found at $csc. This Windows has no .NET Framework 4." }
if (-not (Test-Path -LiteralPath $src)) { throw 'WinToolsMenu.cs must be next to this script.' }

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Get-Process -Name WinToolsMenu -ErrorAction SilentlyContinue | Stop-Process -Force

& $csc /nologo /target:winexe /optimize+ "/out:$exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll $src
if ($LASTEXITCODE -ne 0) { throw 'Compile failed. Nothing was installed.' }

$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'WinToolsMenu.lnk'
$shell = New-Object -ComObject WScript.Shell
$s = $shell.CreateShortcut($lnk)
$s.TargetPath = $exe
$s.WorkingDirectory = $installDir
$s.Save()

Start-Process -FilePath $exe
Write-Host 'Installed. Press Win+Alt+X to open the menu. Press Win+Alt+X again to open Custom Shortcuts.'
