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

$md12601 = $mgr.GetMapData(12601)
Write-Host "12601 in AllMaps: $($mgr.AllMaps.ContainsKey(12601))"
$mdFromAll = $mgr.AllMaps[12601]
Write-Host "mdFromAll PreEvents: $($mdFromAll.PreEvents.Count)"
foreach ($k in $mgr.AllMaps.Keys) {
    $item = $mgr.AllMaps[$k]
    if ($item.PreEvents -ne $null -and $item.PreEvents.Count -gt 0) {
        Write-Host "Found PreEvents on Map $k : $($item.PreEvents.Count)"
        break
    }
}
