$repoRoot = "D:\GitHub\Wonderland-Private-Server"
$phoenixDataDll = "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$phoenixCoreDll = "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
$wloCoreDll = "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
$rcLibraryDll = "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
$pserverCoreDll = "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"

Add-Type -Path $phoenixDataDll
Add-Type -Path $phoenixCoreDll
Add-Type -Path $wloCoreDll
Add-Type -Path $rcLibraryDll
Add-Type -Path $pserverCoreDll

$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg") | Out-Null
$mapData = $loader.GetMapData(10035)
$npc1 = $mapData.Npclist | Where-Object { $_.clickId -eq 1 }
Write-Host "NPC 1 clickId: $($npc1.clickId) npcId: $($npc1.npcId) Events: $($npc1.Events -join ', ')"

$npc7 = $mapData.Npclist | Where-Object { $_.clickId -eq 7 }
Write-Host "NPC 7 clickId: $($npc7.clickId) npcId: $($npc7.npcId) Events: $($npc7.Events -join ', ')"
