$rcLib = [System.IO.File]::ReadAllBytes("D:\GitHub\Wonderland-Private-Server\DLLS\RCLibrary.dll")
[System.Reflection.Assembly]::Load([byte[]]$rcLib) | Out-Null
$bytes = [System.IO.File]::ReadAllBytes("D:\GitHub\Wonderland-Private-Server\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll")
[System.Reflection.Assembly]::Load([byte[]]$bytes) | Out-Null

$eveMgr = New-Object Game.DataFiles.EveManager
$eveMgr.LoadFile("D:\GitHub\Wonderland-Private-Server\Data\Eve.emg") | Out-Null

$m13 = $eveMgr.GetMapData(12613)
Write-Host "Map 12613 Warps:"
foreach ($w in $m13.WarpLoc) {
    Write-Host "  clickID: $($w.clickID) mapID: $($w.mapID) x: $($w.x) y: $($w.y)"
}
