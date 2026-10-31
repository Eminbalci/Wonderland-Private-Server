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
using Game.Maps;
using Network;
using RCLibrary.Core.Networking;

public class ReinforcementSocket : SocketClient
{
    public List<byte[]> Packets = new List<byte[]>();
    public ReinforcementSocket()
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

public static class BattleReinforcementVerifier
{
    public static string RunTest()
    {
        // 1. Create player
        var socket = new ReinforcementSocket();
        var items = new global::DataFiles.PhxItemDat();
        var player = new Player(socket, items);
        player.CharID = 998877;
        player.CharName = "TestWarrior";
        player.CurHP = 2000;
        player.CurSP = 1000;
        player.CurMap = new GameMap();

        // 2. Setup formation with 3 monsters across 2 slots:
        // Slot 2 has 20014 initially, and 20016 as reinforcement!
        // Slot 5 has 20015 initially (no reinforcement).
        var spawnEntries = new List<BattleSpawnEntry>();
        var s1 = new BattleSpawnEntry(); s1.MonsterTid = 20014; s1.Slot = 2; spawnEntries.Add(s1);
        var s2 = new BattleSpawnEntry(); s2.MonsterTid = 20015; s2.Slot = 5; spawnEntries.Add(s2);
        var s3 = new BattleSpawnEntry(); s3.MonsterTid = 20016; s3.Slot = 2; spawnEntries.Add(s3);

        bool victoryCalled = false;
        var qCtx = new QuestBattleContext();
        qCtx.QuestID = 9999;
        qCtx.Step = 1;
        qCtx.OnVictory = delegate { victoryCalled = true; };

        PvEBattleManager.StartPvEBattle(
            player,
            10,
            "TestBoss",
            30,
            500,
            20014,
            qCtx,
            new uint[] { 20014, 20015, 20016 },
            spawnEntries
        );

        var battle = PvEBattleManager.GetBattle(player);
        if (battle == null) return "FAIL: Battle was not registered in active battles.";

        // Check initial state
        if (battle.Defenders.Count != 2)
            return string.Format("FAIL: Expected 2 initial defenders on field, got {0}.", battle.Defenders.Count);

        var defSlot2 = battle.Defenders.FirstOrDefault(f => f.GridX == 2 && f.GridY == 2);
        if (defSlot2 == null || defSlot2.ID != 20014)
            return string.Format("FAIL: Expected initial monster 20014 at Slot 2 (2, 2), got {0}.", defSlot2 != null ? defSlot2.ID : 0);

        var defSlot5 = battle.Defenders.FirstOrDefault(f => f.GridX == 1 && f.GridY == 1);
        if (defSlot5 == null || defSlot5.ID != 20015)
            return string.Format("FAIL: Expected initial monster 20015 at Slot 5 (1, 1), got {0}.", defSlot5 != null ? defSlot5.ID : 0);

        if (!battle.HasPendingReinforcements)
            return "FAIL: Expected HasPendingReinforcements to be true.";

        if (!battle.ReinforcementQueues.ContainsKey(2) || battle.ReinforcementQueues[2].Count != 1)
            return "FAIL: Expected 1 pending reinforcement in Slot 2 queue.";

        // Step 3: Simulate killing monster in Slot 2 (20014) in Round 1
        defSlot2.CurHP = 0;
        if (battle.IsFinished)
            return "FAIL: Battle marked finished prematurely.";

        // Step 4: Submit player action and trigger turn execution
        var rp1 = new RecievePacket(new byte[] { 4, 2, 2, 2, 0x11, 0x27 });
        rp1.SetPtr(0);
        PvEBattleManager.HandleBattleAction(player, 1, rp1);
        int wait1 = 0;
        while (battle.IsTurnProcessing && wait1++ < 50)
        {
            System.Threading.Thread.Sleep(100);
        }
        System.Threading.Thread.Sleep(200);

        // Step 5: Verify reinforcement 20016 was spawned into Slot 2!
        var newDefSlot2 = battle.Defenders.FirstOrDefault(f => f.GridX == 2 && f.GridY == 2);
        if (newDefSlot2 == null || newDefSlot2.ID != 20016)
            return string.Format("FAIL: Expected reinforcement 20016 at Slot 2 (2, 2), got {0}.", newDefSlot2 != null ? newDefSlot2.ID : 0);

        if (newDefSlot2.CurHP <= 0)
            return string.Format("FAIL: Reinforcement spawned with 0 HP ({0}).", newDefSlot2 != null ? newDefSlot2.CurHP : 0);

        if (battle.HasPendingReinforcements)
            return "FAIL: Expected HasPendingReinforcements to be false after spawning all.";

        if (battle.IsFinished)
            return "FAIL: Battle finished while reinforcement 20016 and 20015 are still alive.";

        // Step 6: Kill remaining monsters (20016 in Slot 2 and 20015 in Slot 5) in Round 2
        newDefSlot2.CurHP = 0;
        defSlot5.CurHP = 0;

        var rp2 = new RecievePacket(new byte[] { 4, 2, 2, 2, 0x11, 0x27 });
        rp2.SetPtr(0);
        PvEBattleManager.HandleBattleAction(player, 1, rp2);
        int wait2 = 0;
        while ((battle.IsTurnProcessing || !battle.IsFinished) && wait2++ < 50)
        {
            System.Threading.Thread.Sleep(100);
        }
        System.Threading.Thread.Sleep(200);

        if (!battle.IsFinished)
            return "FAIL: Battle did not finish after defeating all monsters and reinforcements.";

        if (!victoryCalled)
            return "FAIL: QuestBattleContext.OnVictory was not invoked.";

        if (battle.Monsters.Count != 3)
            return string.Format("FAIL: Expected 3 total monsters recorded in battle.Monsters, got {0}.", battle.Monsters.Count);

        return "SUCCESS: Battle reinforcements spawned correctly and battle concluded with victory.";
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core', 'System.Data'))
$result = [BattleReinforcementVerifier]::RunTest()
Write-Host $result
if (-not $result.StartsWith("SUCCESS")) {
    exit 1
}
exit 0
