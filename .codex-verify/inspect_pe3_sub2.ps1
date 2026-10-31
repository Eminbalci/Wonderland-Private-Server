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
$pe3 = $md.PreEvents[2]

for ($i = 0; $i -lt $pe3.subentry1.Count; $i++) {
    $s = $pe3.subentry1[$i]
    $arr = $s.unknown.ToArray()
    $hex = ($arr | ForEach-Object { $_.ToString("X2") }) -join ' '
    $q = [BitConverter]::ToUInt16($arr, 1)
    $w2 = [BitConverter]::ToUInt16($arr, 3)
    $w3 = [BitConverter]::ToUInt16($arr, 5)
    $w4 = [BitConverter]::ToUInt16($arr, 7)
    $w5 = [BitConverter]::ToUInt16($arr, 9)
    $w6 = [BitConverter]::ToUInt16($arr, 11)
    Write-Host "PE3 Sub[$i]: q=$q w2=$w2 w3=$w3 w4=0x$($w4.ToString('X4')) w5=$w5 w6=0x$($w6.ToString('X4')) acts=$(if($s.subentry2){$s.subentry2.Count}else{0})"
    Write-Host "   Raw: $hex"
}
