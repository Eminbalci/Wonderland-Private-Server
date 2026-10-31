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

$md = $mgr.GetMapData(12601)
Write-Host "Map 12601 Events count: $($md.Events.Count)"
foreach ($ev in $md.Events) {
    foreach ($sub in $ev.SubEntry) {
        if ($sub.SubEntry) {
            foreach ($op in $sub.SubEntry) {
                if ($op.DialogPtr -eq 5) {
                    Write-Host "Ev $($ev.clickID) Sub $($sub.subIndex): Op 5: q=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4)"
                }
            }
        }
    }
}
