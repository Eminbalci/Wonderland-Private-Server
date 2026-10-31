$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

[Game.DataFiles.SceneDataManager]::Initialize((Join-Path $repo 'Data'))

$tids = @(13, 17277, 17367, 17568, 17342, 17264, 14531, 14506, 14509, 14523, 14590, 14593, 14594)
foreach ($tid in $tids) {
    $name = [Game.DataFiles.SceneDataManager]::GetNpcName($tid)
    Write-Host ("TID {0}: Name='{1}'" -f $tid, $name)
}
