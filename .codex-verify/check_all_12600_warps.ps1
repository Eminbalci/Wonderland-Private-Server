$rcLib = [System.IO.File]::ReadAllBytes("D:\GitHub\Wonderland-Private-Server\DLLS\RCLibrary.dll")
[System.Reflection.Assembly]::Load([byte[]]$rcLib) | Out-Null
$bytes = [System.IO.File]::ReadAllBytes("D:\GitHub\Wonderland-Private-Server\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll")
[System.Reflection.Assembly]::Load([byte[]]$bytes) | Out-Null

$eveMgr = New-Object Game.DataFiles.EveManager
$eveMgr.LoadFile("D:\GitHub\Wonderland-Private-Server\Data\Eve.emg") | Out-Null

$m = $eveMgr.GetMapData(12600)
foreach ($w in $m.WarpLoc) {
    Write-Host "12600 Warp: clickID=$($w.clickID) dstMap=$($w.mapID) dstXY=($($w.x),$($w.y))"
    $dst = $eveMgr.GetMapData($w.mapID)
    if ($dst -ne $null) {
        foreach ($rw in $dst.WarpLoc) {
            if ($rw.mapID -eq 12600) {
                Write-Host "   -> Reverse to 12600 coords: ($($rw.x), $($rw.y))"
            }
        }
    }
}
