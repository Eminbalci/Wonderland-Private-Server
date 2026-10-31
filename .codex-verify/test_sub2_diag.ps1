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
$sub2 = $pe3.subentry1[2]
Write-Host "sub2 unknown count: $($sub2.unknown.Count)"
Write-Host "sub2 unknown[0]: $($sub2.unknown[0])"
$arr = $sub2.unknown.ToArray()
Write-Host "arr length: $($arr.Length)"
Write-Host "w1: $([BitConverter]::ToUInt16($arr, 1))"
Write-Host "w2: $([BitConverter]::ToUInt16($arr, 3))"
