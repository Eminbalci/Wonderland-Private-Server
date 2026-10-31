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

Write-Host "Scanning Bangkok maps around 12601..."
for ($m = 12600; $m -le 12650; $m++) {
    $md = $mgr.GetMapData($m)
    if ($md -ne $null) {
        $npcCount = if ($md.Npclist -ne $null) { $md.Npclist.Count } else { 0 }
        $evCount = if ($md.Events -ne $null) { $md.Events.Count } else { 0 }
        Write-Host ("Map {0}: NPCs={1}, Events={2}, PreEvents={3}" -f $m, $npcCount, $evCount, $md.PreEvents.Count)
    }
}
