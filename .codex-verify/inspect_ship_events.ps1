$ErrorActionPreference = "Stop"
$repoRoot = "D:\GitHub\Wonderland-Private-Server"
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"

$talkDat = New-Object DataFiles.PhxTalkDat("Data/Talk.dat")
[DataFiles.TalkResolver]::Initialize($talkDat)

$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg")

foreach ($mapId in 10014..10018) {
    $m = $loader.GetMapData($mapId)
    if ($m -ne $null) {
        Write-Host ("=== Map {0}: NPCs={1}, Events={2} ===" -f $mapId, $m.NpcList.Count, $m.Events.Count)
        foreach ($ev in $m.Events) {
            Write-Host ("  Event {0}: '{1}' (Subs: {2})" -f $ev.clickID, $ev.Name.Trim(), $ev.SubEntry.Count)
            foreach ($sub in $ev.SubEntry) {
                foreach ($op in $sub.SubEntry) {
                    $talk = ""
                    if ($op.DialogPtr -eq 1 -or $op.DialogPtr -eq 2) {
                        $tid = if ($op.dialog2 -ge 10000) { $op.dialog2 } else { $op.dialog3 }
                        if ($tid -ge 10000) {
                            $tRes = [DataFiles.TalkResolver]::ResolveDetailed([uint32]$tid)
                            if ($tRes.Success) { $talk = $tRes.RawText }
                        }
                    }
                    Write-Host ("    Op {0}: d1={1} d2={2} d3={3} d4={4} dw1={5} {6}" -f $op.DialogPtr, $op.dialog1, $op.dialog2, $op.dialog3, $op.dialog4, $op.unknowndword1, $talk)
                }
            }
        }
    }
}
