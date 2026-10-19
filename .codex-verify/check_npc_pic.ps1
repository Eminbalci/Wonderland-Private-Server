$ErrorActionPreference = "Stop"
Add-Type -Path "PhoenixData/bin/Debug/net462/PhoenixData.dll"
Add-Type -Path "wlo.pserver.core/bin/Debug/wlo.pserver.core.dll"
$npcDat = New-Object DataFiles.PhxNpcDat
$npcDat.Load("Data/Npc.dat").Wait()
Write-Host ("NpcList Count=" + $npcDat.NpcList.Count)
if ($npcDat.NpcList.Count -gt 0) {
    $first = $npcDat.NpcList[0]
    Write-Host ("First NpcID=" + $first.NpcID)
}
$match = $null
foreach ($item in $npcDat.NpcList) {
    if ($item.NpcID -eq 17162) { $match = $item; break }
}
if ($match -ne $null) {
    Write-Host ("Found: NpcID=" + $match.NpcID + ", ImageNum=" + $match.ImageNum)
} else {
    Write-Host "Not found by NpcID == 17162"
}
