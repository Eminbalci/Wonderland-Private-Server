$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }
$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
$m = $mgr.GetMapData(12600)
$ev20 = $m.Events | Where-Object { $_.EventID -eq 20 }
Write-Host "=== Map 12600 Event 20 ==="
for ($i = 0; $i -lt $ev20.SubEntries.Count; $i++) {
    $sub = $ev20.SubEntries[$i]
    Write-Host ("Sub[$i] (Index=" + $sub.subIndex + "): Conds=" + $sub.Conditions.Count + " Acts=" + $sub.Actions.Count)
    foreach ($c in $sub.Conditions) {
        Write-Host ("  Cond: b1=" + $c.unknownbyte1 + " w1=" + $c.unknownword1 + " w2=" + $c.unknownword2 + " cmp=" + $c.comparison + " val=" + $c.value)
    }
    foreach ($act in $sub.Actions) {
        Write-Host ("  Act: Op=" + $act.opcode + " d1=" + $act.dialog1 + " d2=" + $act.dialog2 + " d3=" + $act.dialog3 + " d4=" + $act.dialog4 + " dw1=" + $act.unknowndword1)
    }
}
