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
[Game.DataFiles.SceneDataManager]::Initialize((Join-Path $repo 'Data'))

$md = $mgr.GetMapData(12603)
Write-Host "=== Map 12603 PreEvents ==="
foreach ($pe in $md.PreEvents) {
    Write-Host ("PreEvent: ClickID={0}, CondCount={1}, ActionCount={2}" -f $pe.clickID, $pe.conditions.Count, $pe.actions.Count)
    foreach ($c in $pe.conditions) {
        Write-Host ("   Cond: op={0} val={1} cmp={2} w1={3} w2={4}" -f $c.operator, $c.value, $c.comparison, $c.unknownword1, $c.unknownword2)
    }
    foreach ($act in $pe.actions) {
        Write-Host ("   Act: op={0} b1={1} b2={2} b3={3} b4={4}" -f $act.actionOp, $act.actionByte1, $act.actionByte2, $act.actionByte3, $act.actionByte4)
    }
}

$ev15 = $md.Events | Where-Object { $_.clickID -eq 15 -or $_.clickID -eq 16 -or $_.clickID -eq 17 -or $_.clickID -eq 18 }
foreach ($e in $ev15) {
    Write-Host ("Event ClickID {0}:" -f $e.clickID)
    foreach ($sub in $e.SubEntry) {
        Write-Host ("  Branch {0} (w1={1}, w2={2}):" -f $sub.subIndex, $sub.unknownword1, $sub.unknownword2)
        foreach ($op in $sub.SubEntry) {
            Write-Host ("     Op {0}: d1={1} d2={2} d3={3} d4={4} dw1={5}" -f $op.DialogPtr, $op.dialog1, $op.dialog2, $op.dialog3, $op.dialog4, $op.unknowndword1)
        }
    }
}
