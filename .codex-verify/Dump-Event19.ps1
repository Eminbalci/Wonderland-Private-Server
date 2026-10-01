$repoRoot = "D:\GitHub\Wonderland-Private-Server"

$phoenixDataDll = "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$phoenixCoreDll = "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
$wloCoreDll = "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
$rcLibraryDll = "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
$pserverCoreDll = "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"

Add-Type -Path $phoenixDataDll
Add-Type -Path $phoenixCoreDll
Add-Type -Path $wloCoreDll
Add-Type -Path $rcLibraryDll
Add-Type -Path $pserverCoreDll

$eve = New-Object Game.DataFiles.EveManager
$eve.LoadFile("$repoRoot\Data\eve.Emg") | Out-Null
$map = $eve.GetMapData(10035)
Write-Host "Map 10035 events count: $($map.Events.Count)"

foreach ($ev in $map.Events) {
    if ($ev.clickID -eq 19 -or $ev.EventName -match '老魯' -or $ev.clickID -eq 7) {
        Write-Host "========================================="
        Write-Host "Event clickID: $($ev.clickID) Name: $($ev.EventName) SubEntries: $($ev.SubEntry.Count)"
        for ($i = 0; $i -lt $ev.SubEntry.Count; $i++) {
            $sub = $ev.SubEntry[$i]
            Write-Host "  Sub $i (subIndex=$($sub.subIndex)): cond(b1=$($sub.unknownbyte1), w1=$($sub.unknownword1), w2=$($sub.unknownword2), w3=$($sub.unknownword3), w4=$($sub.unknownword4), w5=$($sub.unknownword5), w6=$($sub.unknownword6)) SubSubCount: $($sub.SubEntry.Count)"
            foreach ($op in $sub.SubEntry) {
                Write-Host "    Opcode $($op.DialogPtr): d1=$($op.dialog1), d2=$($op.dialog2), d3=$($op.dialog3), d4=$($op.dialog4) dw1=$($op.unknowndword1) dw2=$($op.unknowndword2) dw3=$($op.unknowndword3)"
            }
        }
    }
}
