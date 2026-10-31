$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }
[Game.DataFiles.SceneDataManager]::Initialize((Join-Path $repo 'Data'))

for ($id = 10000; $id -le 65000; $id++) {
    $name = [Game.DataFiles.SceneDataManager]::GetMapName($id)
    if ($name -ne $null -and ($name.ToLower().Contains('bang') -or $name.ToLower().Contains('thailand') -or $name.ToLower().Contains('siam'))) {
        Write-Host ("Map {0}: {1}" -f $id, $name)
    }
}
