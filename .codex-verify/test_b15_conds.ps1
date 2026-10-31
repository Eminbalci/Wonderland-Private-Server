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

$md = $mgr.GetMapData(12601)
$ev8 = $md.Events | Where-Object { $_.clickID -eq 8 }

# Branch 15 (Sub 14) condition count:
$s14 = $ev8.SubEntry[14]
$count = $s14.unknownword6 -shr 8
Write-Host "Branch 15 conditions count = $count"
for ($k = 0; $k -lt $count; $k++) {
    $c = $ev8.SubEntry[14 + $k]
    $cmp = $c.unknownword4 -band 255
    $val = ($c.unknownword4 -shr 8) -bor ($c.unknownword5 -shl 8)
    Write-Host "  Cond $k : b1=$($c.unknownbyte1) w1=$($c.unknownword1) w2=$($c.unknownword2) w3=$($c.unknownword3) cmp=$cmp val=$val"
}
