$repoRoot = "D:\GitHub\Wonderland-Private-Server"
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
Add-Type -Path "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"

[Game.DataFiles.SceneDataManager]::LoadNpcBaseStats("$repoRoot\Data\Npc.dat")
$broadcastId = [Game.Player]::GetCompanionBroadcastId(12178)
$stats = [Game.DataFiles.SceneDataManager]::GetNpcBaseStats($broadcastId)

Write-Host "broadcastId for 12178: $broadcastId"
Write-Host "stats is null: $($stats -eq $null)"
if ($stats -ne $null) {
    Write-Host "stats Name: $($stats.Name), Level: $($stats.Level)"
}
