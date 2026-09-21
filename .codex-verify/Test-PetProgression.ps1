param([string]$BuildDirectory = (Join-Path $PSScriptRoot 'main-bin'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
foreach ($assembly in @('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory $assembly))
}
[Game.DataFiles.SceneDataManager]::Initialize((Join-Path $repo 'Data'))
$script:checks = 0
function Check($condition, [string]$message) {
    if (!$condition) { throw $message }
    $script:checks++
}
function StatSum($pet) { [int]$pet.Str + $pet.Con + $pet.Int + $pet.Wis + $pet.Agi }

# Fixed external fixture: verified matching records in server and client Npc.dat.
foreach ($fixture in @(
    @{ID=12032; Stats='15,20,5,8,12'},
    @{ID=14156; Stats='3,6,5,4,9'},
    @{ID=17003; Stats='0,1,1,0,1'}
)) {
    $pet = [Game.Player+PlayerPetData]::new()
    $pet.PetID = $fixture.ID
    $pet.InitializeBaseStats()
    Check ((@($pet.Str,$pet.Con,$pet.Int,$pet.Wis,$pet.Agi) -join ',') -eq $fixture.Stats) "Wrong template for $($fixture.ID)"
}

# Every level boundary conserves cumulative EXP, and awards exactly one stat.
for ($level=1; $level -lt 199; $level++) {
    $pet = [Game.Player+PlayerPetData]::new()
    $pet.PetID = 12032
    $pet.InitializeBaseStats()
    $pet.Level = $level
    $pet.Exp = [Game.Player+PlayerPetData]::GetRequiredExpForNextLevel($level) - 1
    $beforeExp = $pet.ClientTotalExp
    $beforeStats = StatSum $pet
    $gained = $pet.GainExp(1)
    Check ($gained -eq 1 -and $pet.Level -eq ($level+1) -and $pet.Exp -eq 0) "Level boundary $level"
    Check (([long]$pet.ClientTotalExp - $beforeExp) -eq 1) "Phantom EXP at level $level"
    Check ((StatSum $pet) -eq ($beforeStats+1) -and $pet.SkillPoints -eq 0) "Wrong growth at level $level"
    Check ($pet.Int -eq 5 -and $pet.Wis -eq 8) 'Robinson grew outside template top-three tendency'
    $sum = StatSum $pet
    $pet.NormalizeExpForLevel()
    Check ((StatSum $pet) -eq $sum) 'Repeated synchronization awarded another stat'
}

$pet = [Game.Player+PlayerPetData]::new()
$pet.PetID = 14156
$pet.InitializeBaseStats()
$beforeStats = StatSum $pet
$beforeExp = $pet.ClientTotalExp
$levels = $pet.GainExp(120) # 14 + 35 EXP consumed; 71 EXP remains at LV3.
Check ($levels -eq 2 -and $pet.Level -eq 3 -and $pet.Exp -eq 71) 'Multi-level reward failed'
Check ((StatSum $pet) -eq ($beforeStats+2)) 'Multi-level stats failed'
Check (([long]$pet.ClientTotalExp - $beforeExp) -eq 120) 'Multi-level EXP conservation failed'
Check ($pet.Str -eq 3 -and $pet.Wis -eq 4) 'Xaolan grew outside template top-three tendency'
Check ($pet.GainExp(0) -eq 0 -and (StatSum $pet) -eq ($beforeStats+2)) 'Zero reward changed stats'

$pet = [Game.Player+PlayerPetData]::new()
$pet.PetID = 12032; $pet.Level = 5; $pet.Exp = 260
Check ($pet.ClientTotalExp -eq 546) 'Robinson LV5 EXP fixture failed'
$pet.NormalizeExpForLevel()
Check ($pet.Level -eq 5 -and $pet.Exp -eq 260 -and (StatSum $pet) -eq 50) 'Valid saved pet was reset'
Check ($pet.GainExp(3) -eq 1 -and $pet.Level -eq 6 -and $pet.Exp -eq 0) 'Robinson threshold failed'
Check ($pet.ClientTotalExp -eq 549 -and (StatSum $pet) -eq 51) 'Robinson reward not conserved'

$pet.Level=199; $pet.Exp=0
$beforeStats=StatSum $pet
Check ($pet.GainExp(100) -eq 0 -and (StatSum $pet) -eq $beforeStats) 'Level cap awarded extra stats'
$pet = [Game.Player+PlayerPetData]::new()
$pet.PetID=999999
$pet.InitializeBaseStats()
Check ((StatSum $pet) -eq 50) 'Unknown template erased existing stats'
$pet.Str=65535; $pet.Con=65535; $pet.Int=65535; $pet.Wis=65535; $pet.Agi=65535
[void]$pet.GainExp(14)
Check ((StatSum $pet) -eq (5*65535)) 'Stat overflow wrapped'

# Source integration checks complement the actual compiled progression tests.
$allocation = Get-Content -LiteralPath (Join-Path $repo 'Src\Network\ActionCodes\AC08.cs') -Raw
Check ($allocation.Contains('SendPetProgression(r, targetPet)')) 'Allocation does not use canonical pet sync'
Check (!$allocation.Contains('"bbbbdd", 8, 2')) 'Allocation still uses malformed stat packet'
$quest = Get-Content -LiteralPath (Join-Path $repo 'wlo.pserver.core\Game\QuestRelated\QuestManager.cs') -Raw
$start = $quest.IndexOf('public static SendPacket CreatePetPacket(')
$end = $quest.IndexOf('public static SendPacket CreatePetListPacket(', $start)
$recruit = $quest.Substring($start, $end-$start)
Check ($recruit -notmatch 'Pack16\([^\r\n]*> 0') 'Recruit rewrites valid zero stats'
$combat = Get-Content -LiteralPath (Join-Path $repo 'wlo.pserver.core\Game\Battle\PvEBattleManager.cs') -Raw
Check ($combat.Contains('capturedPet.InitializeBaseStats()')) 'Captured pet misses template stats'
Check ($combat.Contains('SendPetProgression(ca.Player, capturedPet)')) 'Captured pet misses progression sync'
"PASS: $script:checks checks using compiled pet progression plus source integration guards. Native-client rendering is not tested."
