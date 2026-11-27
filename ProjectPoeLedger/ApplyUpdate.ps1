param([Parameter(Mandatory=$true)][int]$ParentId,[Parameter(Mandatory=$true)][string]$Installer,[Parameter(Mandatory=$true)][string]$Sha256)
$ErrorActionPreference='Stop'
try {
 $p=Get-Process -Id $ParentId -ErrorAction SilentlyContinue
 if ($p) { if (!$p.WaitForExit(60000)) { throw 'Poe Ledger did not close. Update cancelled.' } }
 if (Get-Process -Name ProjectPoeLedger -ErrorAction SilentlyContinue) { throw 'Close all Poe Ledger windows before installing.' }
 if ((Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash -ne $Sha256) { throw 'Installer checksum mismatch.' }
 $log=Join-Path (Split-Path -Parent $Installer) 'installation.log'
 $result=Start-Process -FilePath $Installer -ArgumentList ('/LOG="'+$log+'"') -Wait -PassThru
 if ($result.ExitCode -ne 0) { throw "Installer exited with code $($result.ExitCode). Your database backup remains available." }
} catch { Write-Host $_ -ForegroundColor Red; Read-Host 'Press Enter to close'; exit 1 }
