# Downloads the newest WinTools Menu from GitHub and installs it: the same as "Update" in the menu.
# Run update.cmd in %LOCALAPPDATA%\WinToolsMenu, or paste this into PowerShell:
#   irm https://raw.githubusercontent.com/donkeyhigh/WindowsMenuTool/main/update.ps1 | iex
$ErrorActionPreference = 'Stop'
try {
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  $repo = 'donkeyhigh/WindowsMenuTool'
  # raw.githubusercontent.com caches main/ for up to 5 minutes; files at the exact newest commit are never stale.
  $base = "https://raw.githubusercontent.com/$repo/main/"
  try {
    $sha = (Invoke-RestMethod -UseBasicParsing -UserAgent 'WinToolsMenu' -Uri "https://api.github.com/repos/$repo/commits/main").sha
    if ($sha) { $base = "https://raw.githubusercontent.com/$repo/$sha/" }
  } catch { }
  $tmp = Join-Path $env:TEMP ('WindowsMenuTool-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
  New-Item -ItemType Directory -Force -Path $tmp | Out-Null
  foreach ($f in 'WinToolsMenu.cs', 'install.ps1', 'uninstall.ps1', 'update.ps1', 'update.cmd', 'WinTools.ico') {
    Invoke-WebRequest -UseBasicParsing -Uri ($base + $f) -OutFile (Join-Path $tmp $f)
  }
  & (Join-Path $tmp 'install.ps1')
} catch {
  Write-Host ('Update failed: ' + $_.Exception.Message)
  Read-Host 'Press Enter to close'
}
