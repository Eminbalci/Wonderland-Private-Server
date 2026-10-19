$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
Add-Type -Path "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg")
foreach ($m in $loader.AllMaps.Values) {
    if ($m.Events -eq $null) { continue }
    foreach ($ev in $m.Events) {
        foreach ($sub in $ev.SubEntry) {
            foreach ($op in $sub.SubEntry) {
                if ($op.dialog2 -ge 20038 -and $op.dialog2 -le 20053) {
                    Write-Host "Map $($m.mapID) Ev $($ev.clickID) Sub $($sub.subIndex): d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4)"
                }
                if ($op.dialog3 -ge 20038 -and $op.dialog3 -le 20053) {
                    Write-Host "Map $($m.mapID) Ev $($ev.clickID) Sub $($sub.subIndex): d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4)"
                }
            }
        }
    }
}
