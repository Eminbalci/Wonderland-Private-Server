$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))

$md = $mgr.GetMapData(12601)
$ev8 = $md.Events | Where-Object { $_.clickID -eq 8 }

# Branch 1 (Sub 0): condition
Write-Host "Branch 1 (Sub 0):"
$s0 = $ev8.SubEntry[0]
Write-Host "  b1=$($s0.unknownbyte1) w1=$($s0.unknownword1) w2=$($s0.unknownword2) w3=$($s0.unknownword3) w4=0x$($s0.unknownword4.ToString('X4')) w5=$($s0.unknownword5) w6=0x$($s0.unknownword6.ToString('X4'))"

# Branch 6 (Sub 5):
Write-Host "Branch 6 (Sub 5):"
$s5 = $ev8.SubEntry[5]
Write-Host "  b1=$($s5.unknownbyte1) w1=$($s5.unknownword1) w2=$($s5.unknownword2) w3=$($s5.unknownword3) w4=0x$($s5.unknownword4.ToString('X4')) w5=$($s5.unknownword5) w6=0x$($s5.unknownword6.ToString('X4'))"
for ($k = 0; $k -lt ($s5.unknownword6 -shr 8); $k++) {
    $c = $ev8.SubEntry[5 + $k]
    $cmp = $c.unknownword4 -band 255
    $val = ($c.unknownword4 -shr 8) -bor ($c.unknownword5 -shl 8)
    Write-Host "    Cond $k (Sub $(5+$k)): b1=$($c.unknownbyte1) w1=$($c.unknownword1) w2=$($c.unknownword2) cmp=$cmp val=$val"
}
