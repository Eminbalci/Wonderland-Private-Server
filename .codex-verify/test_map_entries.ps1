$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))

Write-Host "Count of MapEntries: $($mgr.MapEntries.Count)"
$firstKey = ($mgr.MapEntries.Keys | Select-Object -First 1)
Write-Host ("First key: " + $firstKey)
$md = $mgr.MapEntries[$firstKey]
Write-Host ("MapData for " + $firstKey + ": " + $md.MapID + ", PreEvents: " + $md.PreEvents.Count)
