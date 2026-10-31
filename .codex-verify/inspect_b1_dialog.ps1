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

# Branch 1 (Sub 0):
$s0 = $ev8.SubEntry[0]
Write-Host "Branch 1 (Sub 0):"
for ($k = 0; $k -lt ($s0.unknownword6 -shr 8); $k++) {
    $c = $ev8.SubEntry[$k]
    $cmp = $c.unknownword4 -band 255
    $val = ($c.unknownword4 -shr 8) -bor ($c.unknownword5 -shl 8)
    Write-Host "  Cond $k : b1=$($c.unknownbyte1) w1=$($c.unknownword1) w2=$($c.unknownword2) w3=$($c.unknownword3) cmp=$cmp val=$val"
}
foreach ($op in $s0.SubEntry) {
    $t = ""
    if ($op.dialog3 -gt 10000) { $t = $talk.GetDialogue([uint32]$op.dialog3) }
    elseif ($op.dialog2 -gt 10000) { $t = $talk.GetDialogue([uint32]$op.dialog2) }
    Write-Host "  Op $($op.DialogPtr) d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) :: $t"
}
