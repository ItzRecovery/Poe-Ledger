$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'Desktop'
if (!(Test-Path (Join-Path $source 'ProjectPoeLedger.exe'))) { throw 'Run Build Desktop.bat successfully first.' }
if (Get-Process -Name ProjectPoeLedger -ErrorAction SilentlyContinue) { throw 'Close Poe Ledger first.' }
$dest = Join-Path $env:LOCALAPPDATA 'Programs\ProjectPoeLedger'
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $dest -Recurse -Force
$menu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut((Join-Path $menu 'Poe Ledger.lnk'))
$link.TargetPath = Join-Path $dest 'ProjectPoeLedger.exe'
$link.WorkingDirectory = $dest
$link.IconLocation = (Join-Path $dest 'ProjectPoeLedger.exe') + ",0"
$link.Save()
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
$desktopLink = $shell.CreateShortcut((Join-Path $desktop 'Poe Ledger.lnk'))
$desktopLink.TargetPath = $link.TargetPath
$desktopLink.WorkingDirectory = $dest
$desktopLink.IconLocation = $link.IconLocation
$desktopLink.Save()
Write-Host "Installed to $dest. Existing application data was not changed."
