param([string]$BuildDirectory = (Join-Path $PSScriptRoot 'main-bin'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
foreach ($assembly in @('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory $assembly))
}
$script:checks = 0
function Check($condition, [string]$message) {
    if (!$condition) { throw $message }
    $script:checks++
}
function New-TestPlayer {
    $p = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Game.Player])
    for ($type=[Game.Player]; $null -ne $type; $type=$type.BaseType) {
        $lockField=$type.GetField('mlock',[Reflection.BindingFlags]'Instance,NonPublic,DeclaredOnly')
        if ($null -ne $lockField) { $lockField.SetValue($p,[object]::new()) }
    }
    $p.PlayerPets = [Collections.Generic.Dictionary[byte,Game.Player+PlayerPetData]]::new()
    [Game.Character].GetField('c_lock',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($p,[object]::new())
    [Game.Player].GetField('m_useracc', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($p, [Game.Code.User]::new())
    return $p
}
function New-Pet([byte]$slot, [uint32]$id, [string]$name) {
    $pet = [Game.Player+PlayerPetData]::new()
    $pet.Slot=$slot; $pet.PetID=$id; $pet.PetName=$name
    return $pet
}
$p = New-TestPlayer
$robin = New-Pet 1 12032 'Robinson1'
$xao = New-Pet 2 14156 'Xaolan'
$grape = New-Pet 3 17003 'Grape Mons'
$duplicate = New-Pet 4 17003 'Grape duplicate'
foreach ($pet in @($robin,$xao,$grape,$duplicate)) { $p.PlayerPets.Add($pet.Slot,$pet) }
Check ($p.RegisterClientPet($robin)) 'Robinson allocation failed'
Check ($p.RegisterClientPet($xao)) 'Xaolan allocation failed'
Check ($p.RegisterClientPet($grape)) 'Grape allocation failed'
Check (!$p.RegisterClientPet($duplicate)) 'Native-rejected duplicate consumed client slot'
Check ($duplicate.ClientSlot -eq 0 -and $p.PlayerPets.Count -eq 4) 'Duplicate stored pet was deleted'
Check (!$p.RegisterClientPet($robin)) 'Repeat roster event was allowed'
Check ($p.GetClientPet(3) -eq $grape) 'Client slot3 is not Grape'
Check ($null -eq $p.GetClientPet(4) -and $null -eq $p.GetClientPet(0)) 'Empty slot resolves a pet'

$p = New-TestPlayer
$robin = New-Pet 2 12032 'Robinson1'
$xao = New-Pet 4 14156 'Xaolan'
$p.PlayerPets.Add(2,$robin); $p.PlayerPets.Add(4,$xao)
[void]$p.RegisterClientPet($robin); [void]$p.RegisterClientPet($xao)
Check ($robin.ClientSlot -eq 1 -and $xao.ClientSlot -eq 2) 'DB gaps leaked into client slots'
Check ($robin.Slot -eq 2 -and $xao.Slot -eq 4) 'DB identities were rewritten'
[void]$p.PlayerPets.Remove(2); $robin.ClientSlot=0
$grape = New-Pet 1 17003 'Grape Mons'
$p.PlayerPets.Add(1,$grape); [void]$p.RegisterClientPet($grape)
Check ($grape.ClientSlot -eq 1 -and $xao.ClientSlot -eq 2) 'Hole reuse shifted existing pet'
$alias = New-Pet 3 12178 'Robinson alias'
$p.PlayerPets.Add(2,$robin); [void]$p.RegisterClientPet($robin)
$p.PlayerPets.Add(3,$alias)
Check (!$p.RegisterClientPet($alias)) 'Companion alias duplicate was not rejected'

# Layout read independently from the native parser at 0x409820.
# Check actual compiled serializers, not a fixture that copies their implementation.
foreach ($level in @(1,2,6,50)) {
    $pkt = [Game.QuestRelated.QuestManager]::CreatePetPacket($p,12032,2,123,221,45,147,60,$level,15,20,0,8,12,3,$false,0)
    [byte[]]$b = @($pkt.Buffer)[4..($pkt.Buffer.Length-1)]
    Check ($b.Length -eq 54) 'AC15:1 must contain 54 payload bytes'
    Check ($b[0] -eq 15 -and $b[1] -eq 1) 'Recruit opcode changed'
    Check ([BitConverter]::ToUInt32($b,2) -eq $p.CharID) 'Recruit owner misaligned'
    Check ([BitConverter]::ToUInt32($b,6) -eq 12178 -and $b[10] -eq 1) 'Recruit identity/type misaligned'
    Check ([BitConverter]::ToUInt16($b,11) -eq 15 -and [BitConverter]::ToUInt16($b,15) -eq 0) 'Stats/valid zero not preserved'
    Check ($b[21] -eq $level) 'Level must be one byte at offset21'
    Check ([BitConverter]::ToUInt32($b,22) -eq [Game.Player+PlayerPetData]::GetClientTotalExp($level,3)) 'EXP is not cumulative or is misaligned'
    Check ($b[26] -eq 1 -and $b[31] -eq 1 -and $b[36] -eq 0 -and [BitConverter]::ToUInt32($b,27) -eq 0 -and [BitConverter]::ToUInt32($b,32) -eq 0 -and [BitConverter]::ToUInt32($b,37) -eq 0) 'Native skill progress misaligned'
    Check ($b[41] -eq 60 -and $b[45] -eq 0 -and $b[46] -eq 0) 'Amity/reborn/job misaligned'
}
$robin.PetName='Robinson1'
$name = [Game.QuestRelated.QuestManager]::CreatePetNamePacket($p,$robin)
[byte[]]$b=@($name.Buffer)[4..($name.Buffer.Length-1)]
Check ($b[0] -eq 15 -and $b[1] -eq 9 -and $b[6] -eq $robin.ClientSlot) 'Rename opcode/internal slot mismatch'
Check ([Text.Encoding]::ASCII.GetString($b,7,$b.Length-7) -eq 'Robinson1') 'Rename has length prefix/trailing zero'
$map = $p.CreatePetMapPacket(12178,'Robinson1')
[byte[]]$b=@($map.Buffer)[4..($map.Buffer.Length-1)]
Check ($b.Length -eq 13+9+8 -and $b[12] -eq 9) 'Remote appearance trailing fields/name layout wrong'

# Execute the exact combat serializer block from production source with a fake fighter.
$source = Get-Content (Join-Path $repo 'wlo.pserver.core\Game\Battle\PvEBattleManager.cs') -Raw
$start = $source.IndexOf('SendPacket pPet = new SendPacket();')
$end = $source.IndexOf('p.Send(pPet);',$start)
$block = $source.Substring($start,$end-$start)
$harness = @"
using System;
using Network;
using Game.Battle;
public static class NativePetCombatFixture {
    public static byte[] Build() {
        var pet = new BattleFighter { ID=12178,OwnerID=4510001,GridX=3,GridY=2,MaxHP=221,CurHP=123,MaxSP=147,CurSP=45,Level=2 };
        byte petElem=1;
        $block
        return pPet.Buffer;
    }
}
"@
Add-Type -TypeDefinition $harness -ReferencedAssemblies @((Join-Path $BuildDirectory 'wlo.pserver.core.dll'),(Join-Path $BuildDirectory 'RCLibrary.dll'))
[byte[]]$b=[NativePetCombatFixture]::Build()
Check ($b.Length -eq 4+3+31) 'Combat native parser would not read one 31-byte fighter'
Check ($b[7] -eq 4 -and [BitConverter]::ToUInt32($b,8) -eq 12178) 'Combat pet identity/type wrong'
Check ([BitConverter]::ToUInt32($b,14) -eq 4510001 -and $b[18] -eq 3 -and $b[19] -eq 2) 'Combat owner/position wrong'
Check ([BitConverter]::ToUInt32($b,26) -eq 123 -and [BitConverter]::ToUInt16($b,30) -eq 45) 'Combat HP/SP misaligned'
"PASS: $script:checks native layout and session identity checks. No DB or live client modified. Rendering still requires client replay."
