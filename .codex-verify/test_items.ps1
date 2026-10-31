$repo = (Get-Location).Path
Add-Type -Path (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll')
Add-Type -Path (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
[Game.DataFiles.SceneDataManager]::Initialize((Join-Path $repo 'Data'))
Write-Host ('Item 30404: ' + [Game.Battle.MonsterDropManager]::ResolveItemName(30404))
Write-Host ('Item 34146: ' + [Game.Battle.MonsterDropManager]::ResolveItemName(34146))
