Add-Type -Path 'bin/Debug/PhoenixData.dll'
Add-Type -Path 'bin/Debug/wlo.pserver.core.dll'
$eve = New-Object PhoenixData.DataFiles.PhxEveDat
$eve.Load('Data/eve.Emg')
$m = $eve.GetMapData(10035)
foreach ($n in $m.Npclist) {
    if ($n.clickId -eq 1) {
        Write-Output ("NPC 1: Name={0}, TID={1}, Events={2}" -f $n.Name, $n.npcId, ($n.Events -join ','))
    }
}
foreach ($ev in $m.Events) {
    Write-Output ("Event clickID={0}, Name={1}, SubCount={2}" -f $ev.clickID, $ev.Name, $ev.SubEntry.Count)
}
