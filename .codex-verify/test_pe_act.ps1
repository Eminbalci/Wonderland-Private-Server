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
$pe3 = $md.PreEvents | Where-Object { $_.clickID -eq 3 }
Write-Host "PE 3 found: $($pe3 -ne $null)"
if ($pe3) {
    Write-Host "SubEntries: $($pe3.subentry1.Count)"
    foreach ($s in $pe3.subentry1) {
        Write-Host "Sub acts: $($s.subentry2.Count)"
        foreach ($act in $s.subentry2) {
            $h = ($act.unknown | ForEach-Object { $_.ToString("X2") }) -join ' '
            Write-Host "  Act: $h"
        }
    }
}
