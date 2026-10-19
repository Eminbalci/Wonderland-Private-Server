$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
Add-Type -Path "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg")

# Find events that contain Opcode 8 or Opcode 2 + Opcode 8 together
$count = 0
foreach ($m in $loader.AllMaps.Values) {
    if ($m.Events -eq $null) { continue }
    foreach ($ev in $m.Events) {
        foreach ($sub in $ev.SubEntry) {
            $hasOp8 = $false
            $hasOp2 = $false
            foreach ($op in $sub.SubEntry) {
                if ($op.DialogPtr -eq 8) { $hasOp8 = $true }
                if ($op.DialogPtr -eq 2) { $hasOp2 = $true }
            }
            if ($hasOp8 -and $hasOp2) {
                $count++
                if ($count -le 10) {
                    Write-Host "Map $($m.mapID) Ev $($ev.clickID) Sub $($sub.subIndex) Name='$($ev.Name.Trim())':"
                    foreach ($op in $sub.SubEntry) {
                        Write-Host "   Op $($op.DialogPtr): d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4) dw1=$($op.unknowndword1)"
                    }
                }
            }
        }
    }
}
Write-Host "Total events with Op8 and Op2: $count"
