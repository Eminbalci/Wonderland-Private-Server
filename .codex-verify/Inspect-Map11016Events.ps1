$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
Add-Type -Path "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg")
$mapData = $loader.GetMapData(11016)
Write-Host "Map 11016 Events:"
foreach ($ev in $mapData.Events) {
    Write-Host "Event clickID=$($ev.clickID), Name=$($ev.Name), SubCount=$($ev.SubEntry.Count)"
    for ($i = 0; $i -lt $ev.SubEntry.Count; $i++) {
        $sub = $ev.SubEntry[$i]
        Write-Host "  Sub $i (subIndex=$($sub.subIndex)): cond(b1=$($sub.unknownbyte1), w1=$($sub.unknownword1), w2=$($sub.unknownword2), w3=$($sub.unknownword3), w4=$($sub.unknownword4), w5=$($sub.unknownword5), w6=$($sub.unknownword6)) SubSubCount: $($sub.SubEntry.Count)"
        foreach ($op in $sub.SubEntry) {
            Write-Host "    Opcode $($op.DialogPtr): d1=$($op.dialog1), d2=$($op.dialog2), d3=$($op.dialog3), d4=$($op.dialog4) dw1=$($op.unknowndword1) dw2=$($op.unknowndword2) dw3=$($op.unknowndword3)"
        }
    }
}
