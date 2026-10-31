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

$md = $mgr.GetMapData(10003)
$ev1 = $md.Events | Where-Object { $_.clickID -eq 1 }

Write-Host "Map 10003 Event 1:"
for ($i = 0; $i -lt $ev1.SubEntry.Count; $i++) {
    $s = $ev1.SubEntry[$i]
    if ($s.SubEntry -ne $null -and $s.SubEntry.Count -gt 0) {
        $condCount = $s.unknownword6 -shr 8
        Write-Host "--- Event 1 Branch at Sub $i (Condition count = $condCount) ---"
        for ($k = 0; $k -lt $condCount; $k++) {
            $c = $ev1.SubEntry[$i + $k]
            Write-Host "    Cond $k (Sub $($i+$k)): b1=$($c.unknownbyte1) w1=$($c.unknownword1) w2=$($c.unknownword2) w3=$($c.unknownword3)"
        }
        foreach ($op in $s.SubEntry) {
            $txt = ""
            if ($op.dialog3 -gt 10000 -and $op.dialog3 -lt 65000) { $txt = $talk.GetDialogue([uint32]$op.dialog3) }
            elseif ($op.dialog2 -gt 10000 -and $op.dialog2 -lt 65000) { $txt = $talk.GetDialogue([uint32]$op.dialog2) }
            Write-Host "    Action: Op $($op.DialogPtr) d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) :: $txt"
        }
    }
}
