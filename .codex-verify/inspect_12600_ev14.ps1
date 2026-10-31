$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$refs = @(
    (Join-Path $repoRoot 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repoRoot 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repoRoot 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repoRoot 'Data\eve.Emg'))
$talk = New-Object DataFiles.PhxTalkDat("$repoRoot\Data\Talk.DAT")

$md = $mgr.GetMapData(12600)
$ev14 = $md.Events | Where-Object { $_.clickID -eq 14 }
Write-Host "Map 12600 Event 14 SubEntries: $($ev14.SubEntry.Count)"
for ($i = 0; $i -lt $ev14.SubEntry.Count; $i++) {
    $s = $ev14.SubEntry[$i]
    if ($s.SubEntry -ne $null -and $s.SubEntry.Count -gt 0) {
        $count = $s.unknownword6 -shr 8
        Write-Host ("--- Event 14 Branch at Sub " + $i + " (Condition count = " + $count + ") ---")
        for ($k = 0; $k -lt $count; $k++) {
            $c = $ev14.SubEntry[$i + $k]
            $cmp = $c.unknownword4 -band 255
            $val = ($c.unknownword4 -shr 8) -bor ($c.unknownword5 -shl 8)
            Write-Host ("    Cond " + $k + ": b1=" + $c.unknownbyte1 + " w1=" + $c.unknownword1 + " w2=" + $c.unknownword2 + " cmp=" + $cmp + " val=" + $val)
        }
        foreach ($op in $s.SubEntry) {
            $t = ""
            if ($op.dialog3 -gt 10000) { $t = $talk.GetDialogue([uint32]$op.dialog3) }
            elseif ($op.dialog2 -gt 10000) { $t = $talk.GetDialogue([uint32]$op.dialog2) }
            Write-Host ("    Op " + $op.DialogPtr + " d1=" + $op.dialog1 + " d2=" + $op.dialog2 + " d3=" + $op.dialog3 + " d4=" + $op.dialog4 + " :: " + $t)
        }
    }
}
