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
[Game.DataFiles.SceneDataManager]::Initialize((Join-Path $repo 'Data'))

function ShowMapNpcs($mapId, $clickIds) {
    $md = $mgr.GetMapData($mapId)
    Write-Host "=== Map $mapId ==="
    foreach ($cId in $clickIds) {
        $n = $md.Npclist | Where-Object { $_.clickID -eq $cId }
        if ($n -ne $null) {
            $name = [Game.DataFiles.SceneDataManager]::GetNpcName($n.npcID)
            Write-Host ("  ClickID {0}: Template {1} ('{2}') at ({3},{4})" -f $cId, $n.npcID, $name, $n.x, $n.y)
        } else {
            Write-Host ("  ClickID {0}: not found in Npclist" -f $cId)
        }
    }
}

ShowMapNpcs 12601 @(5, 7, 8)
ShowMapNpcs 12603 @(1, 2, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18)
ShowMapNpcs 12650 @(21, 22, 23, 24, 29, 30, 31)
