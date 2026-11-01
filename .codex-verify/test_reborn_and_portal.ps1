$deps = @(
    "D:\GitHub\Wonderland-Private-Server\bin\Debug\RCLibrary.dll",
    "D:\GitHub\Wonderland-Private-Server\bin\Debug\PhoenixData.dll",
    "D:\GitHub\Wonderland-Private-Server\bin\Debug\Phoenix.Core.dll",
    "D:\GitHub\Wonderland-Private-Server\bin\Debug\Wlo.Core.dll",
    "D:\GitHub\Wonderland-Private-Server\bin\Debug\wlo.pserver.maps.dll",
    "D:\GitHub\Wonderland-Private-Server\bin\Debug\wlo.pserver.core.dll"
)

foreach ($d in $deps) {
    if (Test-Path $d) {
        [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($d)) | Out-Null
    }
}

$eveMgr = New-Object Game.DataFiles.EveManager
$eveMgr.LoadFile("D:\GitHub\Wonderland-Private-Server\Data\Eve.emg") | Out-Null
[DataBase.GameDataBase]::GlobalInstance = New-Object DataBase.GameDataBase
[DataBase.GameDataBase]::GlobalInstance.EveDat = $eveMgr

$map = New-Object Game.GameMap
$map.MapID = 12600

$dstMap = 0; $dstX = 0; $dstY = 0;
$res = $map.LookupPortal(11, 442, 1455, [ref]$dstMap, [ref]$dstX, [ref]$dstY)

Write-Host "LookupPortal result: $res, dstMap=$dstMap, dstX=$dstX, dstY=$dstY"

if ($dstMap -eq 12613) {
    Write-Host "TEST 1 PASSED: Portal 11 correctly leads to Bangkok (Map 12613) instead of 12612!"
} else {
    Write-Host "TEST 1 FAILED: Expected 12613, got $dstMap"
}

# Test 2: Character Rebirth Level Reduction
# Create mock player
$p = New-Object Game.Player
$p.CharID = 99999
$p.CharName = "TestPlayer"
$p.SetLevel(120)
$initialPoints = $p.SkillPoints
Write-Host "Initial Level: $($p.Level), SkillPoints: $($p.SkillPoints)"

[Game.PlayerRelated.RebornManager]::PerformReborn($p, [Game.PlayerRelated.RebornJob]::Killer, $true) | Out-Null

Write-Host "Post-Reborn Level: $($p.Level), Reborn: $($p.Reborn), Job: $($p.Job), SkillPoints: $($p.SkillPoints)"

if ($p.Level -eq 20 -and $p.SkillPoints -eq $initialPoints -and $p.Reborn -eq $true -and $p.Job -eq [Game.PlayerRelated.RebornJob]::Killer) {
    Write-Host "TEST 2 PASSED: Level reduced by 100 (120 -> 20), points preserved ($initialPoints), Reborn=True, Job=Killer!"
} else {
    Write-Host "TEST 2 FAILED!"
}

# Test 3: Reborn at Lv 100 -> Lv 1
$p2 = New-Object Game.Player
$p2.CharID = 99998
$p2.CharName = "TestPlayer2"
$p2.SetLevel(100)
$initialPoints2 = $p2.SkillPoints
[Game.PlayerRelated.RebornManager]::PerformReborn($p2, [Game.PlayerRelated.RebornJob]::Wit, $true) | Out-Null
Write-Host "Lv100 -> Lv$($p2.Level), SkillPoints: $($p2.SkillPoints)"
if ($p2.Level -eq 1 -and $p2.SkillPoints -eq $initialPoints2) {
    Write-Host "TEST 3 PASSED: Level reduced by 100 (100 -> 1), points preserved!"
} else {
    Write-Host "TEST 3 FAILED!"
}
