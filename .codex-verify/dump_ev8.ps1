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
$ev8 = $md.Events | Where-Object { $_.clickID -eq 8 }

Write-Host "Event 8: SubEntries count = $($ev8.SubEntry.Count)"
for ($i = 0; $i -lt $ev8.SubEntry.Count; $i++) {
    $sub = $ev8.SubEntry[$i]
    $ops = if ($sub.SubEntry) { $sub.SubEntry.Count } else { 0 }
    $hex = "{0:X2} {1:X4} {2:X4} {3:X4} {4:X4} {5:X4} {6:X4}" -f $sub.unknownbyte1, $sub.unknownword1, $sub.unknownword2, $sub.unknownword3, $sub.unknownword4, $sub.unknownword5, $sub.unknownword6
    Write-Host "Branch $($i+1) (idx $i): b1=$($sub.unknownbyte1) w1=$($sub.unknownword1) w2=$($sub.unknownword2) w3=$($sub.unknownword3) w4=$($sub.unknownword4) w5=$($sub.unknownword5) w6=$($sub.unknownword6) [Ops=$ops]"
    Write-Host "   Raw: $hex"
    if ($ops -gt 0) {
        foreach ($op in $sub.SubEntry) {
            Write-Host "     Op $($op.DialogPtr): d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4) dw1=$($op.unknowndword1)"
        }
    }
}
