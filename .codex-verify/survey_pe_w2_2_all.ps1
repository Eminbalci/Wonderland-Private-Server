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

$samples = @()
foreach ($k in $mgr.AllMaps.Keys) {
    $md = $mgr.AllMaps[$k]
    if ($md.PreEvents -eq $null) { continue }
    foreach ($pe in $md.PreEvents) {
        if ($pe.subentry1 -eq $null) { continue }
        foreach ($sub in $pe.subentry1) {
            if ($sub.unknown -ne $null -and $sub.unknown.Count -ge 5 -and $sub.unknown[0] -eq 5 -and [BitConverter]::ToUInt16($sub.unknown.ToArray(), 3) -eq 2) {
                $arr = $sub.unknown.ToArray()
                $q = [BitConverter]::ToUInt16($arr, 1)
                $w4 = [BitConverter]::ToUInt16($arr, 7)
                $acts = if ($sub.subentry2) { $sub.subentry2.Count } else { 0 }
                $samples += "Map $k PE $($pe.clickID): Quest $q w2=2 w4=0x$($w4.ToString('X4')) acts=$acts"
                if ($samples.Count -ge 20) { break }
            }
        }
        if ($samples.Count -ge 20) { break }
    }
    if ($samples.Count -ge 20) { break }
}
foreach ($s in $samples) { Write-Host $s }
