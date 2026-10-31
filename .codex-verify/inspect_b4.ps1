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

$md = $mgr.GetMapData(12601)
$ev8 = $md.Events | Where-Object { $_.clickID -eq 8 }

# Branch 4 (Sub 3):
$s3 = $ev8.SubEntry[3]
Write-Host "Branch 4 (Sub 3) condition 0: b1=$($s3.unknownbyte1) w1=$($s3.unknownword1) w2=$($s3.unknownword2) w4=0x$($s3.unknownword4.ToString('X4'))"
for ($opIdx = 0; $opIdx -lt $s3.SubEntry.Count; $opIdx++) {
    $op = $s3.SubEntry[$opIdx]
    $t = ""
    if ($op.dialog3 -gt 10000) { $t = $talk.GetDialogue([uint32]$op.dialog3) }
    elseif ($op.dialog2 -gt 10000) { $t = $talk.GetDialogue([uint32]$op.dialog2) }
    Write-Host "  Op $($op.DialogPtr) d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) :: $t"
}
