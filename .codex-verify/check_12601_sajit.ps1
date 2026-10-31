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

$md = $mgr.GetMapData(12601)
Write-Host "=== Map 12601 NPCs ==="
foreach ($n in $md.Npclist) {
    Write-Host ("  ClickID {0}: Template {1} ('{2}') at ({3},{4}) byte1=0x{5:X2}" -f $n.clickID, $n.npcID, [Game.DataFiles.SceneDataManager]::GetNpcName($n.npcID), $n.x, $n.y, $n.unknownbyte1)
}

Write-Host "=== Map 12601 PreEvents ==="
foreach ($pe in $md.PreEvents) {
    Write-Host ("PreEvent ClickID {0}:" -f $pe.clickID)
    foreach ($c in $pe.conditions) {
        Write-Host ("   Cond: op={0} w1={1} w2={2} w3={3} w4={4} val={5}" -f $c.operator, $c.unknownword1, $c.unknownword2, $c.unknownword3, $c.unknownword4, $c.value)
    }
    foreach ($act in $pe.actions) {
        Write-Host ("   Act: op={0} b1={1} b2={2} b3={3} b4={4}" -f $act.actionOp, $act.actionByte1, $act.actionByte2, $act.actionByte3, $act.actionByte4)
    }
}

Write-Host "=== Map 12601 ClickID 5 & 7 Events ==="
foreach ($ev in $md.Events) {
    if ($ev.clickID -eq 5 -or $ev.clickID -eq 7 -or $ev.clickID -eq 8) {
        Write-Host ("Event ClickID {0}:" -f $ev.clickID)
        foreach ($sub in $ev.SubEntry) {
            Write-Host ("  Branch {0} (w1={1}, w2={2}):" -f $sub.subIndex, $sub.unknownword1, $sub.unknownword2)
            foreach ($op in $sub.SubEntry) {
                Write-Host ("     Op {0}: d1={1} d2={2} d3={3} d4={4} dw1={5}" -f $op.DialogPtr, $op.dialog1, $op.dialog2, $op.dialog3, $op.dialog4, $op.unknowndword1)
            }
        }
    }
}
