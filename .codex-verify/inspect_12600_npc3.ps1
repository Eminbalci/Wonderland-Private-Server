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

$md = $mgr.GetMapData(12600)
$npc3 = $md.Npclist | Where-Object { $_.clickId -eq 3 }
Write-Host "Map 12600 NPC 3: TID=$($npc3.npcId), Name='$($npc3.Name)', pos=($($npc3.x),$($npc3.y)), byte1=0x$($npc3.unknownbyte1.ToString('X2')), Events=$($npc3.Events -join ',')"

Write-Host "`nAll PreEvents on Map 12600:"
for ($i = 0; $i -lt $md.PreEvents.Count; $i++) {
    $pe = $md.PreEvents[$i]
    Write-Host "PE $i (ClickID $($pe.clickID)):"
    for ($j = 0; $j -lt $pe.subentry1.Count; $j++) {
        $sub = $pe.subentry1[$j]
        $acts = if ($sub.subentry2) { $sub.subentry2.Count } else { 0 }
        $arr = $sub.unknown.ToArray()
        $q = [BitConverter]::ToUInt16($arr, 1)
        $w2 = [BitConverter]::ToUInt16($arr, 3)
        $w4 = [BitConverter]::ToUInt16($arr, 7)
        Write-Host "  Sub[$j]: b1=$($arr[0]) q=$q w2=$w2 w4=0x$($w4.ToString('X4')) acts=$acts"
        if ($acts -gt 0) {
            foreach ($act in $sub.subentry2) {
                $aArr = $act.unknown.ToArray()
                $tClick = [BitConverter]::ToUInt16($aArr, 1)
                $tType = [BitConverter]::ToUInt16($aArr, 3)
                Write-Host "    Action: type=$tType targetClick=$tClick"
            }
        }
    }
}
