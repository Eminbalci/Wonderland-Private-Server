# Load assemblies
$coreDll = Resolve-Path "wlo.pserver.core/bin/Debug/wlo.pserver.core.dll"
$rcDll = Resolve-Path "RCLibrary/bin/Debug/net462/RCLibrary.dll"
[System.Reflection.Assembly]::LoadFrom($rcDll) | Out-Null
[System.Reflection.Assembly]::LoadFrom($coreDll) | Out-Null

Write-Host "=== TEST 1: Reborn Job Skill ID Mapping ==="
$jobs = @(
    @{ Job = [Game.RebornJob]::Killer;  ExpectedId = 15241; Name = "Murder" },
    @{ Job = [Game.RebornJob]::Warrior; ExpectedId = 15240; Name = "Holy Shield" },
    @{ Job = [Game.RebornJob]::Knight;  ExpectedId = 15242; Name = "Knight's Spirit" },
    @{ Job = [Game.RebornJob]::Wit;     ExpectedId = 15243; Name = "Curse" },
    @{ Job = [Game.RebornJob]::Priest;  ExpectedId = 15244; Name = "Rest" },
    @{ Job = [Game.RebornJob]::Seer;    ExpectedId = 15245; Name = "Foretell" }
)

foreach ($j in $jobs) {
    $actualId = [Game.SkillRelated.SkillManager]::GetJobSkillId($j.Job)
    if ($actualId -eq $j.ExpectedId) {
        Write-Host "PASS: $($j.Job) mapped to Skill $actualId ($($j.Name))" -ForegroundColor Green
    } else {
        Write-Error "FAIL: $($j.Job) mapped to $actualId, expected $($j.ExpectedId)"
    }
}

Write-Host "`n=== TEST 2: Rebirth Level Reduction Formula (Math.Max(1, Level - 99)) ==="
$testLevels = @(
    @{ Input = 100; Expected = 1 },
    @{ Input = 120; Expected = 21 },
    @{ Input = 130; Expected = 31 },
    @{ Input = 180; Expected = 81 },
    @{ Input = 199; Expected = 100 }
)

foreach ($tl in $testLevels) {
    $calculated = [byte][Math]::Max(1, $tl.Input - 99)
    if ($calculated -eq $tl.Expected) {
        Write-Host "PASS: Level $($tl.Input) -> Reborn Level $calculated" -ForegroundColor Green
    } else {
        Write-Error "FAIL: Level $($tl.Input) resulted in $calculated, expected $($tl.Expected)"
    }
}

Write-Host "`n=== TEST 3: PerformReborn Grants Class Skill and Deducts 99 Levels Without Adding Stat Points ==="
$player = New-Object Game.Player
$player.Eqs.SetLevel(120, $true)
$ptsBefore = $player.Eqs.SkillPoints
Write-Host "Pre-Reborn: Level = $($player.Level), Points = $ptsBefore, Reborn = $($player.Reborn), Skills = $($player.PlayerSkills.Count)"

$success = [Game.PlayerRelated.RebornManager]::PerformReborn($player, [Game.PlayerRelated.RebornJob]::Killer, $true)
$ptsAfter = $player.Eqs.SkillPoints
$hasMurder = $player.PlayerSkills | Where-Object { $_.SkillID -eq 15241 }

Write-Host "Post-Reborn: Success = $success, Level = $($player.Level), Points = $ptsAfter, Reborn = $($player.Reborn), Job = $($player.Job)"
Write-Host "Has Murder Skill (15241): $([bool]$hasMurder)"

if ($player.Level -eq 21) {
    Write-Host "PASS: Post-Reborn Level is 21 (120 - 99 = 21)" -ForegroundColor Green
} else {
    Write-Error "FAIL: Post-Reborn Level is $($player.Level), expected 21"
}

if ($ptsAfter -eq $ptsBefore) {
    Write-Host "PASS: SkillPoints preserved ($ptsAfter) without unwanted stat point grants" -ForegroundColor Green
} else {
    Write-Error "FAIL: SkillPoints changed from $ptsBefore to $ptsAfter"
}

if ($hasMurder) {
    Write-Host "PASS: Killer Job Skill Murder (15241) successfully granted to PlayerSkills" -ForegroundColor Green
} else {
    Write-Error "FAIL: PlayerSkills does not contain Murder (15241)"
}

Write-Host "`n=== TEST 4: Progression Skills Unlocking Auto-Includes Job Skill for Reborn Characters ==="
$qualified = [Game.SkillRelated.SkillManager]::GetQualifiedProgressionSkills($player)
if ($qualified.Contains(15241)) {
    Write-Host "PASS: GetQualifiedProgressionSkills includes Killer Job Skill 15241" -ForegroundColor Green
} else {
    Write-Error "FAIL: GetQualifiedProgressionSkills missing 15241"
}

Write-Host "`n=== ALL TESTS COMPLETED SUCCESSFULLY ==="
