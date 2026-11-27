# Downloads the newest WinTools Menu from GitHub and installs it: the same as "Update" in the menu.
# Run update.cmd in %LOCALAPPDATA%\WinToolsMenu, or paste this into PowerShell:
#   irm https://raw.githubusercontent.com/donkeyhigh/WindowsMenuTool/main/update.ps1 | iex
# It downloads install.ps1 from the newest commit; install.ps1 then fetches the other files from that same commit.
$ErrorActionPreference = 'Stop'
try {
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  $repo = 'donkeyhigh/WindowsMenuTool'
  $sha = ''
  try { $sha = (Invoke-RestMethod -UseBasicParsing -UserAgent 'WinToolsMenu' -Uri "https://api.github.com/repos/$repo/commits/main").sha } catch { }
  $ref = if ($sha) { $sha } else { 'main' }
  $tmp = Join-Path $env:TEMP ('WindowsMenuTool-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
  New-Item -ItemType Directory -Force -Path $tmp | Out-Null
  $installer = Join-Path $tmp 'install.ps1'
  Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/$repo/$ref/install.ps1" -OutFile $installer
  & $installer -Commit $ref
} catch {
  Write-Host ('Update failed: ' + $_.Exception.Message)
  Read-Host 'Press Enter to close'
}
