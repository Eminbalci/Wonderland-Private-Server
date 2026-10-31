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

# Search all other events for 15266
foreach ($ev in $md.Events) {
    if ($ev.clickID -eq 8) { continue }
    foreach ($sub in $ev.SubEntry) {
        if ($sub.SubEntry) {
            foreach ($op in $sub.SubEntry) {
                if ($op.DialogPtr -eq 5 -and $op.dialog1 -eq 15266) {
                    Write-Host "Event $($ev.clickID) Sub $($sub.subIndex): Op 5 q=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4)"
                }
            }
        }
    }
}
