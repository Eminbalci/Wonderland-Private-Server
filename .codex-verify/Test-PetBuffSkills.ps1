param([string]$BuildDirectory = "bin/Debug")
$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path

$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$code = @"
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using Game;
using Game.Battle;
using Game.SkillRelated;
using Game.Maps;
using Network;
using RCLibrary.Core.Networking;

public class PetBuffSocket : SocketClient
{
    public List<byte[]> Packets = new List<byte[]>();
    public PetBuffSocket()
    {
        m_IncomingPackets = new System.Collections.Concurrent.ConcurrentQueue<IPacket>();
    }
    public override void SendPacket(IPacket packet)
    {
        if (packet != null && packet.Buffer != null)
        {
            Packets.Add(packet.Buffer);
        }
    }
}

public static class PetBuffVerifier
{
    public static string RunTest()
    {
        // 1. Check Robinson Unload Wall IsShield / IsBuff classification
        var unloadWall = SkillManager.GetSkill(12046);
        if (unloadWall == null) return "FAIL: Skill 12046 (Unload Wall) not found in database.";
        if (!unloadWall.IsShield) return "FAIL: Skill 12046 IsShield should be true (Effect 101 Wall).";
        if (!unloadWall.IsBuff) return "FAIL: Skill 12046 IsBuff should be true.";
        if (!unloadWall.IsPureSupport) return "FAIL: Skill 12046 IsPureSupport should be true.";

        // 2. Setup Player and Pet
        var socket = new PetBuffSocket();
        var items = new global::DataFiles.PhxItemDat();
        var player = new Player(socket, items);
        player.CharID = 100200;
        player.CharName = "BuffTester";
        player.CurHP = 1500;
        player.CurSP = 500;
        player.CurMap = new GameMap();

        // Create active battle pet with multiple buff skills
        var pet = new Player.PlayerPetData();
        pet.Slot = 1;
        pet.PetID = 15657; // Victoria
        pet.PetName = "Victoria";
        pet.Level = 25;
        pet.HP = 800;
        pet.MaxHP = 800;
        pet.SP = 300;
        pet.MaxSP = 300;
        pet.IsBattle = true;

        // Victoria learns Fiery Attack (11072) at Grade 5, Speed Up (11052) at Grade 3, Vanish (15190) at Grade 2, Recovery (11043) at Grade 4
        pet.Skills.Clear();
        var skFiery = new PlayerSkill(11072); skFiery.Grade = 5; pet.Skills.Add(skFiery);
        var skSpeed = new PlayerSkill(11052); skSpeed.Grade = 3; pet.Skills.Add(skSpeed);
        var skVanish = new PlayerSkill(15190); skVanish.Grade = 2; pet.Skills.Add(skVanish);
        var skRecov = new PlayerSkill(11043); skRecov.Grade = 4; pet.Skills.Add(skRecov);

        player.PlayerPets = new Dictionary<byte, Player.PlayerPetData>();
        player.PlayerPets[1] = pet;
        player.ActivePetID = pet.PetID;

        // Start battle
        PvEBattleManager.StartPvEBattle(player, 1, "WildWolf", 10, 200, 20014);
        var battle = PvEBattleManager.GetBattle(player);
        if (battle == null) return "FAIL: Active battle was not created.";

        var playerFighter = battle.Attackers.FirstOrDefault(f => f.PlayerRef == player);
        var petFighter = battle.Attackers.FirstOrDefault(f => f.PetRef == pet);
        var monsterFighter = battle.Defenders.FirstOrDefault(f => f.MonsterRef != null);

        if (playerFighter == null) return "FAIL: Player fighter not found.";
        if (petFighter == null) return "FAIL: Pet fighter not found.";
        if (monsterFighter == null) return "FAIL: Monster fighter not found.";

        // 3. Verify GetFighterSkillGrade for Pet
        var methodGrade = typeof(PvEBattleManager).GetMethod("GetFighterSkillGrade", BindingFlags.NonPublic | BindingFlags.Static);
        if (methodGrade == null) return "FAIL: GetFighterSkillGrade method not found.";

        byte gradeFiery = (byte)methodGrade.Invoke(null, new object[] { petFighter, (ushort)11072 });
        if (gradeFiery != 5) return string.Format("FAIL: Expected Pet Fiery Attack grade 5, got {0}.", gradeFiery);

        byte gradeSpeed = (byte)methodGrade.Invoke(null, new object[] { petFighter, (ushort)11052 });
        if (gradeSpeed != 3) return string.Format("FAIL: Expected Pet Speed Up grade 3, got {0}.", gradeSpeed);

        // 4. Test Pet casts Fiery Attack (11072) on Player
        // Player at (4, 2), Pet at (3, 2), Monster at (2, 2)
        int initialPetSp = petFighter.CurSP;
        int fieryCost = SkillManager.GetSkill(11072).SP;

        // Player defends, Pet casts Fiery Attack targeting Player
        var rpDefend = new RecievePacket();
        rpDefend.Pack8(playerFighter.GridX); rpDefend.Pack8(playerFighter.GridY);
        rpDefend.Pack8(playerFighter.GridX); rpDefend.Pack8(playerFighter.GridY);
        rpDefend.Pack16(60021);
        rpDefend.SetPtr(0);
        PvEBattleManager.HandleBattleAction(player, 4, rpDefend);

        var rpFiery = new RecievePacket();
        rpFiery.Pack8(petFighter.GridX); rpFiery.Pack8(petFighter.GridY);
        rpFiery.Pack8(playerFighter.GridX); rpFiery.Pack8(playerFighter.GridY);
        rpFiery.Pack16(11072);
        rpFiery.SetPtr(0);
        PvEBattleManager.HandleBattleAction(player, 1, rpFiery);

        // Wait for round 1 to complete and turn processing to open
        for (int i = 0; i < 50 && battle.IsTurnProcessing; i++)
            System.Threading.Thread.Sleep(200);

        if (!playerFighter.HasStatus(FighterStatusType.HotBlooded))
            return "FAIL: Player should have HotBlooded status after Pet Fiery Attack.";

        int hotMult = playerFighter.GetStatusValue(FighterStatusType.HotBlooded, 0);
        if (hotMult != 3)
            return string.Format("FAIL: Expected HotBlooded multiplier 3 from Skill 11072, got {0}.", hotMult);

        if (petFighter.CurSP != (initialPetSp - fieryCost))
            return string.Format("FAIL: Expected Pet SP to be {0}, got {1}.", initialPetSp - fieryCost, petFighter.CurSP);

        // Verify AC 50:1 packet emitted with 0xE1 flame overlay
        bool foundFlameOverlay = socket.Packets.Any(p => p.Length >= 7 && p[4] == 50 && p[5] == 1 && p.Contains((byte)0xE1));
        if (!foundFlameOverlay)
            return "FAIL: AC 50:1 with 0xE1 HotBlooded visual overlay not found in broadcast packets.";

        // 5. Test Pet casts Vanish (15190) on itself
        var rpPlayerDef = new RecievePacket();
        rpPlayerDef.Pack8(playerFighter.GridX); rpPlayerDef.Pack8(playerFighter.GridY);
        rpPlayerDef.Pack8(playerFighter.GridX); rpPlayerDef.Pack8(playerFighter.GridY);
        rpPlayerDef.Pack16(60021);
        rpPlayerDef.SetPtr(0);
        PvEBattleManager.HandleBattleAction(player, 4, rpPlayerDef);

        var rpVanish = new RecievePacket();
        rpVanish.Pack8(petFighter.GridX); rpVanish.Pack8(petFighter.GridY);
        rpVanish.Pack8(petFighter.GridX); rpVanish.Pack8(petFighter.GridY);
        rpVanish.Pack16(15190);
        rpVanish.SetPtr(0);
        PvEBattleManager.HandleBattleAction(player, 1, rpVanish);

        for (int i = 0; i < 50 && battle.IsTurnProcessing; i++)
            System.Threading.Thread.Sleep(200);

        if (!petFighter.HasStatus(FighterStatusType.Vanish))
            return "FAIL: Pet should have Vanish status after casting Skill 15190.";

        bool foundVanishOverlay = socket.Packets.Any(p => p.Length >= 7 && p[4] == 50 && p[5] == 1 && p.Contains((byte)0xE6));
        if (!foundVanishOverlay)
            return "FAIL: AC 50:1 with 0xE6 Vanish visual overlay not found in broadcast packets.";

        // 6. Test Speed Up +30 and expiration
        int baseSpd = petFighter.Spd;
        petFighter.AddStatus(FighterStatusType.SpeedUp, 2, 30);
        petFighter.Spd += 30;
        if (petFighter.Spd != baseSpd + 30)
            return string.Format("FAIL: Expected Spd {0}, got {1}.", baseSpd + 30, petFighter.Spd);

        // Advance 2 rounds to expire SpeedUp
        var st = petFighter.ActiveStatuses.FirstOrDefault(s => s.StatusType == FighterStatusType.SpeedUp);
        if (st != null) st.RemainingTurns = 1;

        for (int i = 0; i < 50 && battle.IsTurnProcessing; i++)
            System.Threading.Thread.Sleep(200);

        // Player & pet defend to trigger round end status tick
        var rpP1 = new RecievePacket();
        rpP1.Pack8(playerFighter.GridX); rpP1.Pack8(playerFighter.GridY);
        rpP1.Pack8(playerFighter.GridX); rpP1.Pack8(playerFighter.GridY);
        rpP1.Pack16(60021); rpP1.SetPtr(0);
        PvEBattleManager.HandleBattleAction(player, 4, rpP1);

        var rpP2 = new RecievePacket();
        rpP2.Pack8(petFighter.GridX); rpP2.Pack8(petFighter.GridY);
        rpP2.Pack8(petFighter.GridX); rpP2.Pack8(petFighter.GridY);
        rpP2.Pack16(60021); rpP2.SetPtr(0);
        PvEBattleManager.HandleBattleAction(player, 4, rpP2);

        for (int i = 0; i < 50 && battle.IsTurnProcessing; i++)
            System.Threading.Thread.Sleep(200);

        if (petFighter.HasStatus(FighterStatusType.SpeedUp))
            return "FAIL: SpeedUp should have expired at round end.";

        if (petFighter.Spd != baseSpd)
            return string.Format("FAIL: Expected Pet Spd to revert to {0}, got {1}.", baseSpd, petFighter.Spd);

        // Force clean battle cleanup
        PvEBattleManager.ForceWinBattle(player.CharID);

        return "SUCCESS";
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core', 'System.Data'))
$res = [PetBuffVerifier]::RunTest()
if ($res -eq "SUCCESS") {
    Write-Host "SUCCESS: All pet buff skill tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "FAILED: $res" -ForegroundColor Red
    exit 1
}
