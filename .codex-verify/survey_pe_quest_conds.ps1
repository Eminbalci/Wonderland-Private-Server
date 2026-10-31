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

$samples = 0
for ($m = 0; $m -lt 1119; $m++) {
    $md = $mgr.GetMapData($m)
    if ($md -eq $null -or $md.PreEvents -eq $null) { continue }
    foreach ($pe in $md.PreEvents) {
        if ($pe.subentry1 -eq $null) { continue }
        for ($i = 0; $i -lt $pe.subentry1.Count; $i++) {
            $sub = $pe.subentry1[$i]
            if ($sub.unknown -ne $null -and $sub.unknown.Count -ge 13) {
                $b1 = $sub.unknown[0]
                if ($b1 -eq 5) { # quest condition
                    $qId = [BitConverter]::ToUInt16($sub.unknown.ToArray(), 1)
                    $w2 = [BitConverter]::ToUInt16($sub.unknown.ToArray(), 3)
                    $w3 = [BitConverter]::ToUInt16($sub.unknown.ToArray(), 5)
                    $w4 = [BitConverter]::ToUInt16($sub.unknown.ToArray(), 7)
                    $w5 = [BitConverter]::ToUInt16($sub.unknown.ToArray(), 9)
                    $w6 = [BitConverter]::ToUInt16($sub.unknown.ToArray(), 11)
                    $cmp = $w4 -band 255
                    $val = ($w4 -shr 8) -bor ($w5 -shl 8) -bor (($w6 -band 255) -shl 24)
                    $acts = if ($sub.subentry2) { $sub.subentry2.Count } else { 0 }
                    Write-Host "Map $m PE $($pe.clickID) Sub[$i]: Quest=$qId w2=$w2 w3=$w3 w4=0x$($w4.ToString('X4')) (cmp=$cmp, val=$val) w6=0x$($w6.ToString('X4')) acts=$acts"
                    $samples++
                    if ($samples -ge 30) { break }
                }
            }
        }
        if ($samples -ge 30) { break }
    }
    if ($samples -ge 30) { break }
}
