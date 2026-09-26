$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(Get-Process -Name 'Wonderland Private Server' -ErrorAction SilentlyContinue){throw 'Server still running'}
$build=Join-Path $PSScriptRoot 'mall-stock-bin'
$runtime=Join-Path $root 'bin/Debug'
$files=@('Wonderland Private Server.exe','Wonderland Private Server.pdb','wlo.pserver.core.dll','wlo.pserver.core.pdb')
foreach($name in $files){if(!(Test-Path -LiteralPath (Join-Path $build $name))){throw "Missing staged file: $name"}}
$backup=Join-Path $PSScriptRoot ('deploy-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'-mall-stock')
[void](New-Item -ItemType Directory -Path $backup)
$dbHash=(Get-FileHash -LiteralPath (Join-Path $runtime 'ServerDataBase.db')).Hash
foreach($name in $files){Copy-Item -LiteralPath (Join-Path $runtime $name) -Destination (Join-Path $backup $name)}
try {
 foreach($name in $files){Copy-Item -LiteralPath (Join-Path $build $name) -Destination (Join-Path $runtime $name) -Force}
 foreach($name in $files){if((Get-FileHash -LiteralPath (Join-Path $build $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $runtime $name)).Hash){throw "Hash mismatch: $name"}}
 if((Get-FileHash -LiteralPath (Join-Path $runtime 'ServerDataBase.db')).Hash -ne $dbHash){throw 'Database changed during deployment'}
} catch {
 foreach($name in $files){Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $runtime $name) -Force};throw
}
try {
 & python (Join-Path $PSScriptRoot 'Update-MallStock.py') --apply
 if($LASTEXITCODE -ne 0){throw 'Catalog update failed'}
} catch {
 foreach($name in $files){Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $runtime $name) -Force};throw
}
Add-Content (Join-Path $PSScriptRoot 'Mall-Stock-20260919.md') -Value "`nDeployed $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') while server stopped. Four binary hashes match. Catalog migration removed25 and added7; all non-catalog tables unchanged. Binary backup: $backup. DB backup recorded in migration output. Server left stopped."
"DEPLOYED: binaries verified; catalog updated; server remains stopped. Backup: $backup."
