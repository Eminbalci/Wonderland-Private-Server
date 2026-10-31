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

# Branch 15 (Sub 14):
Write-Host "Branch 15 (Sub 14):"
$s14 = $ev8.SubEntry[14]
for ($k = 0; $k -lt ($s14.unknownword6 -shr 8); $k++) {
    $c = $ev8.SubEntry[14 + $k]
    Write-Host "  Cond $k : b1=$($c.unknownbyte1) w1=$($c.unknownword1) w2=$($c.unknownword2) w3=$($c.unknownword3) w4=0x$($c.unknownword4.ToString('X4'))"
}
foreach ($op in $s14.SubEntry) {
    Write-Host "  Op $($op.DialogPtr) d1=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4)"
}
