$repoRoot = "D:\GitHub\Wonderland-Private-Server"
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
Add-Type -Path "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
Add-Type -Path "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
Add-Type -Path "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
Add-Type -Path "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"

$loader = New-Object Game.DataFiles.EveManager
$loader.LoadFile("$repoRoot\Data\eve.Emg")
$map10039 = $loader.GetMapData(10039)
if ($map10039 -ne $null) {
    Write-Host "Map 10039 exists. Events: $($map10039.Events.Count)"
    $ev19 = $map10039.Events.Find({param($e) $e.clickID -eq 19})
    Write-Host "Event 19 exists on Map 10039: $($ev19 -ne $null)"
} else {
    Write-Host "Map 10039 does not exist."
}
