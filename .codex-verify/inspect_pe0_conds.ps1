$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }
$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
$m = $mgr.GetMapData(12600)
$pe0 = $m.PreEvents[0]
for ($i = 0; $i -lt $pe0.subentry1.Count; $i++) {
    $sub = $pe0.subentry1[$i]
    $bytes = $sub.unknown.ToArray()
    $b1 = $bytes[0]
    $w1 = [BitConverter]::ToUInt16($bytes, 1)
    $w2 = [BitConverter]::ToUInt16($bytes, 3)
    $w3 = [BitConverter]::ToUInt16($bytes, 5)
    $w4 = [BitConverter]::ToUInt16($bytes, 7)
    $w5 = [BitConverter]::ToUInt16($bytes, 9)
    $w6 = [BitConverter]::ToUInt16($bytes, 11)
    $cmp = $w4 -band 255
    $val = ($w4 -shr 8) -bor ($w5 -shl 8)
    Write-Host ("Sub $i : b1=$b1 w1=$w1 w2=$w2 w3=$w3 cmp=$cmp val=$val")
}
