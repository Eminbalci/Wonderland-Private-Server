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
$ev8 = $md.Events | Where-Object { $_.clickID -eq 8 }

$talkIds = @()
foreach ($sub in $ev8.SubEntry) {
    if ($sub.SubEntry) {
        foreach ($op in $sub.SubEntry) {
            if ($op.DialogPtr -eq 2 -and $op.dialog3 -gt 10000) {
                $talkIds += $op.dialog3
            }
            if ($op.DialogPtr -eq 1 -and $op.dialog2 -gt 10000) {
                $talkIds += $op.dialog2
            }
        }
    }
}
$talkIds = $talkIds | Select-Object -Unique
Write-Host "Talk IDs: $($talkIds -join ', ')"

# Let's inspect Talk.dat or SceneDataManager for these texts
foreach ($t in $talkIds) {
    $txt = [Game.DataFiles.SceneDataManager]::GetTalk(12601, [uint32]$t)
    Write-Host "Talk $t : $txt"
}
