$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$refs = @(
    (Join-Path $repoRoot 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repoRoot 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repoRoot 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repoRoot 'Data\eve.Emg'))

$md = $mgr.GetMapData(10003)
$pe1 = $md.PreEvents[0]
Write-Host "Map 10003 PE1 (ClickID $($pe1.clickID)) SubEntries count: $($pe1.subentry1.Count)"
for ($i = 0; $i -lt $pe1.subentry1.Count; $i++) {
    $s = $pe1.subentry1[$i]
    $arr = $s.unknown.ToArray()
    $q = [BitConverter]::ToUInt16($arr, 1)
    $w2 = [BitConverter]::ToUInt16($arr, 3)
    $w4 = [BitConverter]::ToUInt16($arr, 7)
    $acts = if ($s.subentry2) { $s.subentry2.Count } else { 0 }
    Write-Host "  Sub[$i]: q=$q w2=$w2 w4=0x$($w4.ToString('X4')) acts=$acts"
    if ($acts -gt 0) {
        foreach ($act in $s.subentry2) {
            $aArr = $act.unknown.ToArray()
            Write-Host "    Act: targetClick=$([BitConverter]::ToUInt16($aArr, 1)) type=$([BitConverter]::ToUInt16($aArr, 3))"
        }
    }
}

$ev1 = $md.Events | Where-Object { $_.clickID -eq 1 }
Write-Host "Map 10003 Ev 1 Sub0 Op5:"
foreach ($op in $ev1.SubEntry[0].SubEntry) {
    if ($op.DialogPtr -eq 5) {
        Write-Host "  Op 5: q=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4)"
    }
}
