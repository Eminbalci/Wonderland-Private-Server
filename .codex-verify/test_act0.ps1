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
$sub0 = $pe3.subentry1[0]
$act0 = $sub0.subentry2[1]
Write-Host "TargetClick: $([BitConverter]::ToUInt16($act0.unknown.ToArray(), 1))"
Write-Host "ActType: $([BitConverter]::ToUInt16($act0.unknown.ToArray(), 3))"
Write-Host "act.unknown[0]: $($act0.unknown[0])"
