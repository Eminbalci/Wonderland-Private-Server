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
$npc5 = $md.Npclist | Where-Object { $_.clickId -eq 5 }
$npc7 = $md.Npclist | Where-Object { $_.clickId -eq 7 }

Write-Host "NPC 5: $($npc5.x), $($npc5.y)"
Write-Host "NPC 7: $($npc7.x), $($npc7.y)"
Write-Host "Abs diff X: $([Math]::Abs($npc5.x - $npc7.x))"
Write-Host "Abs diff Y: $([Math]::Abs($npc5.y - $npc7.y))"
Write-Host "Condition: $([Math]::Abs($npc5.x - $npc7.x) -le 30 -and [Math]::Abs($npc5.y - $npc7.y) -le 30)"
