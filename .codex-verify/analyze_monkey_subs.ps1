$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
Add-Type -Path "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg")
$talk = New-Object DataFiles.PhxTalkDat("$repoRoot\Data\Talk.DAT")

$m = $loader.GetMapData(11016)
$ev = $m.Events.Find({param($e) $e.clickID -eq 1})
Write-Host "Event 1 on Map 11016: SubEntries count = $($ev.SubEntry.Count)"

for ($i = 0; $i -lt $ev.SubEntry.Count; $i++) {
    $sub = $ev.SubEntry[$i]
    Write-Host "`n--- SubEntry $($i+1) (subIndex=$($sub.subIndex)) ---"
    Write-Host "  Conditions: b1=$($sub.unknownbyte1), w1=$($sub.unknownword1), w2=$($sub.unknownword2), w3=$($sub.unknownword3), w4=$($sub.unknownword4), w5=$($sub.unknownword5), w6=$($sub.unknownword6)"
    Write-Host "  Opcodes count: $($sub.SubEntry.Count)"
    foreach ($op in $sub.SubEntry) {
        $val = ($op.dialog4 -shr 8) -bor ($op.unknowndword1 -shl 8)
        $talkText = ""
        if ($op.DialogPtr -eq 1 -and $op.dialog2 -gt 10000) {
            $talkText = $talk.GetDialogue([uint32]$op.dialog2)
        } elseif ($op.DialogPtr -eq 2 -and $op.dialog3 -gt 10000) {
            $talkText = $talk.GetDialogue([uint32]$op.dialog3)
        }
        Write-Host "    Opcode $($op.DialogPtr): d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4) dw1=$($op.unknowndword1) (DecodedVal=$val) Text='$talkText'"
    }
}
