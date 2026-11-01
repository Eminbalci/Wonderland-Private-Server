$rcLib = [System.IO.File]::ReadAllBytes("bin\Debug\RCLibrary.dll")
[System.Reflection.Assembly]::Load($rcLib) | Out-Null
$bytes = [System.IO.File]::ReadAllBytes("bin\Debug\wlo.pserver.core.dll")
[System.Reflection.Assembly]::Load($bytes) | Out-Null

$eveMgr = New-Object Game.DataFiles.EveManager
$ok = $eveMgr.LoadFile("Data\Eve.emg")
Write-Host "LoadFile result: $ok"
$m = $eveMgr.GetMapData(12600)
Write-Host "Map 12600 Warps:"
foreach ($w in $m.WarpLoc) {
    Write-Host "  clickID: $($w.clickID) mapID: $($w.mapID) x: $($w.x) y: $($w.y)"
}
Write-Host "Map 12612 Warps:"
$m12 = $eveMgr.GetMapData(12612)
if ($m12 -ne $null) {
    foreach ($w in $m12.WarpLoc) {
        Write-Host "  clickID: $($w.clickID) mapID: $($w.mapID) x: $($w.x) y: $($w.y)"
    }
}
