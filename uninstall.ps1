# Removes WinTools Menu: stops it, removes the Startup shortcut and the program.
# Your menu settings (menu.json) are kept unless you say yes.
$installDir = Join-Path $env:LOCALAPPDATA 'WinToolsMenu'
$settings = Join-Path $env:LOCALAPPDATA 'WinTools\menu.json'

Get-Process -Name WinToolsMenu -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath('Startup')) 'WinToolsMenu.lnk') -Force -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $installDir) { Remove-Item -LiteralPath $installDir -Recurse -Force }

if (Test-Path -LiteralPath $settings) {
    $answer = Read-Host 'Also delete your menu settings (menu.json)? [y/N]'
    if ($answer -match '^(y|yes)$') { Remove-Item -LiteralPath $settings -Force }
}
Write-Host 'WinTools Menu is uninstalled.'
