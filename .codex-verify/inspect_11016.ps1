$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg")
$m = $loader.GetMapData(11016)
Write-Host "Map 11016 NPCs count: $($m.NpcList.Count), Events count: $($m.Events.Count)"
foreach ($npc in $m.NpcList) {
    Write-Host "NPC clickID: $($npc.clickID), TID: $($npc.NpcID), Name: $($npc.Name)"
}
foreach ($ev in $m.Events) {
    Write-Host "Event clickID: $($ev.clickID), Name: $($ev.Name), SubEntries: $($ev.SubEntry.Count)"
    foreach ($sub in $ev.SubEntry) {
        Write-Host "  Sub $($sub.subIndex): cond(b1=$($sub.unknownbyte1), w1=$($sub.unknownword1), w2=$($sub.unknownword2), w3=$($sub.unknownword3), w4=$($sub.unknownword4), w5=$($sub.unknownword5), w6=$($sub.unknownword6)) SubSub count: $($sub.SubEntry.Count)"
        foreach ($op in $sub.SubEntry) {
            Write-Host "    Op $($op.DialogPtr): d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4) dw1=$($op.unknowndword1)"
        }
    }
}
