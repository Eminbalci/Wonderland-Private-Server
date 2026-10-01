$repoRoot = "D:\GitHub\Wonderland-Private-Server"
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
Add-Type -Path "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"

$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg")
$mapData = $loader.GetMapData(10035)

Write-Host "Map 10035 NPCs:"
$ev8 = $mapData.Events.Find({param($e) $e.clickID -eq 8})
Write-Host "Event 8 SubEntries: $($ev8.SubEntry.Count)"
for ($i = 0; $i -lt $ev8.SubEntry.Count; $i++) {
    $sub = $ev8.SubEntry[$i]
    Write-Host "  Sub $i (subIndex=$($sub.subIndex)): cond(b1=$($sub.unknownbyte1), w1=$($sub.unknownword1), w2=$($sub.unknownword2), w3=$($sub.unknownword3), w4=$($sub.unknownword4), w5=$($sub.unknownword5), w6=$($sub.unknownword6)) SubSubCount: $($sub.SubEntry.Count)"
    foreach ($op in $sub.SubEntry) {
        Write-Host "    Opcode $($op.DialogPtr): d1=$($op.dialog1), d2=$($op.dialog2), d3=$($op.dialog3), d4=$($op.dialog4) dw1=$($op.unknowndword1) dw2=$($op.unknowndword2) dw3=$($op.unknowndword3)"
    }
}


