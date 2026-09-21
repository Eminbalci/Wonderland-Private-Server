$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$runtime=Join-Path $root 'bin/Debug'
$stage=Join-Path $PSScriptRoot 'native-gacha-bin'
$client='D:\Game Private\WLRI\aLogin.exe'
$clientStage=Join-Path $PSScriptRoot 'gacha-client/aLogin.exe'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'gacha-client/manifest.json') -Raw | ConvertFrom-Json
$active=Get-Process -Name 'aLogin','Wonderland Private Server' -ErrorAction SilentlyContinue
if($active){throw 'Close server and game before deployment'}
if((Get-FileHash -LiteralPath $client).Hash -ne $manifest.source_sha256){throw 'Client source changed since validation'}
if((Get-FileHash -LiteralPath $clientStage).Hash -ne $manifest.patched_sha256){throw 'Staged client hash mismatch'}
$files=@('Wonderland Private Server.exe','Wonderland Private Server.pdb','wlo.pserver.core.dll','wlo.pserver.core.pdb')
foreach($name in $files){if(!(Test-Path -LiteralPath (Join-Path $stage $name))){throw "Missing stage: $name"}}
$backup=Join-Path $PSScriptRoot ('deploy-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'-native-gacha')
[void](New-Item -ItemType Directory -Path $backup)
$db=Join-Path $runtime 'ServerDataBase.db'
$dbHash=(Get-FileHash -LiteralPath $db).Hash
foreach($name in $files){Copy-Item -LiteralPath (Join-Path $runtime $name) -Destination (Join-Path $backup $name)}
Copy-Item -LiteralPath $client -Destination (Join-Path $backup 'aLogin.exe')
try {
 foreach($name in $files){Copy-Item -LiteralPath (Join-Path $stage $name) -Destination (Join-Path $runtime $name) -Force}
 Copy-Item -LiteralPath $clientStage -Destination $client -Force
 foreach($name in $files){if((Get-FileHash -LiteralPath (Join-Path $runtime $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $stage $name)).Hash){throw "Hash mismatch: $name"}}
 if((Get-FileHash -LiteralPath $client).Hash -ne $manifest.patched_sha256){throw 'Deployed client hash mismatch'}
 if((Get-FileHash -LiteralPath $db).Hash -ne $dbHash){throw 'Database changed during deploy'}
} catch {
 foreach($name in $files){Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $runtime $name) -Force}
 Copy-Item -LiteralPath (Join-Path $backup 'aLogin.exe') -Destination $client -Force
 throw
}
Add-Content -LiteralPath (Join-Path $PSScriptRoot 'Native-Gacha-20260919.md') -Value "`nDeployed $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') with server and client stopped. Five runtime hashes match stage; database unchanged. Backup: $backup. Server and game left stopped for user restart."
"DEPLOYED server and client. Five hashes match, database unchanged. Backup: $backup. Both remain stopped."
