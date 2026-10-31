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

function InspectEvent($mapId, $clickId, $branchIdx) {
    $m = $mgr.GetMapData($mapId)
    $ev = $m.Events | Where-Object { $_.clickID -eq $clickId }
    $br = $ev.SubEntry | Where-Object { $_.subIndex -eq $branchIdx }
    Write-Host "=== Map $mapId ClickID $clickId Branch $branchIdx (unknownwords: $($br.unknownword1),$($br.unknownword2),$($br.unknownword3),$($br.unknownword4)) ==="
    foreach ($op in $br.SubEntry) {
        Write-Host ("  Opcode {0}: d1={1} d2={2} d3={3} d4={4} dw1={5} dw2={6} dw3={7}" -f `
            $op.DialogPtr, $op.dialog1, $op.dialog2, $op.dialog3, $op.dialog4, `
            $op.unknowndword1, $op.unknowndword2, $op.unknowndword3)
    }
}

InspectEvent 10005 2 2
InspectEvent 12250 28 5
InspectEvent 12251 4 1
InspectEvent 12257 2 11
