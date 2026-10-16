$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
Add-Type -Path "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg")
$mapData = $loader.GetMapData(10035)
foreach ($ev in $mapData.Events) {
    Write-Host ("Event clickID={0}, Name={1}, SubCount={2}" -f $ev.clickID, $ev.Name, $ev.SubEntry.Count)
    for ($s = 0; $s -lt $ev.SubEntry.Count; $s++) {
        $sub = $ev.SubEntry[$s]
        Write-Host ("  Sub {0}: b1={1}, w1={2}, w2={3}, w3={4}, w4={5}, w5={6}, w6={7}, SubSubCount={8}" -f $s, $sub.unknownbyte1, $sub.unknownword1, $sub.unknownword2, $sub.unknownword3, $sub.unknownword4, $sub.unknownword5, $sub.unknownword6, $sub.SubEntry.Count)
        foreach ($op in $sub.SubEntry) {
            Write-Host ("    Opcode {0}: d1={1}, d2={2}, d3={3}, d4={4}, dw1={5}, dw2={6}, dw3={7}" -f $op.DialogPtr, $op.dialog1, $op.dialog2, $op.dialog3, $op.dialog4, $op.unknowndword1, $op.unknowndword2, $op.unknowndword3)
        }
    }
}
