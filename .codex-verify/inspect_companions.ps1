$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
Add-Type -Path "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg")

# Look at Robinson on Map 10035
$m10035 = $loader.GetMapData(10035)
Write-Host "=== Map 10035 (Rhode Island Beach) Events ==="
foreach ($ev in $m10035.Events) {
    if ($ev.clickID -eq 8) {
        Write-Host "Event $($ev.clickID): $($ev.Name)"
        foreach ($sub in $ev.SubEntry) {
            Write-Host "  Sub $($sub.subIndex) (Cond: b1=$($sub.unknownbyte1), w1=$($sub.unknownword1), w2=$($sub.unknownword2)): count=$($sub.SubEntry.Count)"
            foreach ($op in $sub.SubEntry) {
                Write-Host "    Op $($op.DialogPtr): d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4) dw1=$($op.unknowndword1)"
            }
        }
    }
}

# Look at Roca on Map 11005
$m11005 = $loader.GetMapData(11005)
if ($m11005 -ne $null) {
    Write-Host "=== Map 11005 Events ==="
    foreach ($ev in $m11005.Events) {
        if ($ev.Name -like "*蘿卡*" -or $ev.Name -like "*Roca*" -or $ev.clickID -in 25, 26) {
            Write-Host "Event $($ev.clickID): $($ev.Name)"
            foreach ($sub in $ev.SubEntry) {
                Write-Host "  Sub $($sub.subIndex): count=$($sub.SubEntry.Count)"
                foreach ($op in $sub.SubEntry) {
                    Write-Host "    Op $($op.DialogPtr): d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4) dw1=$($op.unknowndword1)"
                }
            }
        }
    }
}
