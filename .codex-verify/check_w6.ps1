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
    $b = $sub.unknown.ToArray()
    $w6 = [BitConverter]::ToUInt16($b, 11)
    $acts = if ($sub.subentry2) { $sub.subentry2.Count } else { 0 }
    Write-Host ("Sub $i : acts=$acts w6=0x$($w6.ToString('X4')) (hi=$($w6 -shr 8) lo=$($w6 -band 255))")
}
