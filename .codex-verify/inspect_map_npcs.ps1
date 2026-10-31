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

function InspectNpcs($mapId) {
    $m = $mgr.GetMapData($mapId)
    Write-Host "=== Map $mapId NPCs ==="
    foreach ($npc in $m.Npclist) {
        Write-Host ("  ClickID {0}: TemplateID {1} Pos ({2},{3})" -f $npc.clickID, $npc.npcID, $npc.x, $npc.y)
    }
}

InspectNpcs 10005
InspectNpcs 12250
InspectNpcs 12251
InspectNpcs 12257
