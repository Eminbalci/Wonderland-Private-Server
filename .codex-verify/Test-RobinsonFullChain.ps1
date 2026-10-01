$repoRoot = "D:\GitHub\Wonderland-Private-Server"

$phoenixDataDll = "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$phoenixCoreDll = "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
$wloCoreDll = "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
$rcLibraryDll = "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
$pserverCoreDll = "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"

Add-Type -Path $phoenixDataDll
Add-Type -Path $phoenixCoreDll
Add-Type -Path $wloCoreDll
Add-Type -Path $rcLibraryDll
Add-Type -Path $pserverCoreDll

$code = @"
using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Network;
using Game;
using Game.PlayerRelated;
using Game.QuestRelated;
using Game.Maps;
using Game.DataFiles;
using RCLibrary.Core.Networking;

public class MockSocketClient : SocketClient {
    public override void SendPacket(IPacket p) {}
}

public static class TestRobinsonVerification {
    public static void Run(string repoRoot) {
        try {
            if (DataBase.GameDataBase.GlobalInstance == null) {
                new DataBase.GameDataBase();
            }
            var loader = new EveManager();
            loader.LoadFile(repoRoot + @"\Data\eve.Emg");
            DataBase.GameDataBase.GlobalInstance.EveDat = loader;
        var mapData = loader.GetMapData(10035);
        var ev19 = mapData.Events.Find(e => e.clickID == 19);

        var socket = (MockSocketClient)FormatterServices.GetUninitializedObject(typeof(MockSocketClient));
        socket.m_IncomingPackets = new ConcurrentQueue<IPacket>();

        var dat = new DataFiles.PhxItemDat();
        dat.Load(repoRoot + @"\Data\itemDat.wpdat").Wait();
        var player = new Player(socket, dat);
        player.CharName = "biroduzbir";
        player.Quests = new Dictionary<uint, PlayerQuest>();
        player.PlayerPets = new Dictionary<byte, Player.PlayerPetData>();

        var gmap = new GameMap();
        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gmap, (uint)10035);
        player.CurMap = gmap;

        var startSessionMethod = typeof(EveEventInterpreter).GetMethod("StartSession", BindingFlags.Static | BindingFlags.NonPublic);
        var findBranchMethod = typeof(EveEventInterpreter).GetMethod("FindBranch", BindingFlags.Static | BindingFlags.NonPublic);

        Console.WriteLine("--- Test Case 1: Fresh Player opens Chest (Event 19 Sub 1) ---");
        var branch1 = findBranchMethod.Invoke(null, new object[] { player, gmap, ev19, (byte)0, (ushort)0, (ushort)0, null }) as EventSubEntry;
        if (branch1 == null || branch1.subIndex != 1) {
            throw new Exception("Expected branch 1, got: " + (branch1 != null ? branch1.subIndex.ToString() : "NULL"));
        }
        Console.WriteLine("Branch 1 found. Starting session for Branch 1...");
        startSessionMethod.Invoke(null, new object[] { player, gmap, (ushort)7, ev19, branch1, true });

        // Branch 1 has 3 synchronous opcodes (chest open, give raft, set Quest 12046 = 1).
        // Then line 978 transitions to Branch 3 (dialogue)!
        // In Branch 3, the first opcode is speech, so it sets player.OnInteractionComplete!
        Console.WriteLine("After Branch 1 execution, Quest 12046: " + (player.Quests.ContainsKey(12046) ? player.Quests[12046].State.ToString() : "NONE"));
        Console.WriteLine("Dialogue active (OnInteractionComplete != null): " + (player.OnInteractionComplete != null));

        Console.WriteLine("--- Test Case 2: Player advances through dialogue steps ---");
        int step = 0;
        while (player.OnInteractionComplete != null && step++ < 20) {
            var cb = player.OnInteractionComplete;
            player.OnInteractionComplete = null;
            cb();
        }
        Console.WriteLine("Dialogue steps completed: " + step);
        Console.WriteLine("Quest 12046 state: " + (player.Quests.ContainsKey(12046) ? player.Quests[12046].State.ToString() : "NONE"));
        Console.WriteLine("Quest 12047 state: " + (player.Quests.ContainsKey(12047) ? player.Quests[12047].State.ToString() : "NONE"));

        Console.WriteLine("--- Test Case 3: Verify Robinson in Party ---");
        bool hasRobinson = player.PlayerPets.Values.Any(p => p != null && Player.IsSamePetOrCompanion(p.PetID, 12178));
        Console.WriteLine("Has Robinson in Party: " + hasRobinson);
        if (!hasRobinson) {
            throw new Exception("Robinson was NOT recruited into party after dialogue completed!");
        }

        var robinsonPet = player.PlayerPets.Values.First(p => p != null && Player.IsSamePetOrCompanion(p.PetID, 12178));
        Console.WriteLine("Robinson PetID: " + robinsonPet.PetID + ", Name: " + robinsonPet.PetName + ", Slot: " + robinsonPet.Slot);

        Console.WriteLine("--- Test Case 4: Verify NPC Visibility ---");
        bool robinsonVisible = PreEventInterpreter.ShouldNpcBeVisible(player, 10035, 1);
        Console.WriteLine("Robinson NPC #1 visible on beach: " + robinsonVisible);
        if (robinsonVisible) {
            throw new Exception("Robinson should be hidden on beach after recruitment!");
        }

        Console.WriteLine("--- Test Case 5: Verify Fallback when talking to Robinson NPC #1 with Quest 12047 active ---");
        var player2 = new Player(socket, dat);
        player2.CharName = "fallbackPlayer";
        player2.Quests = new Dictionary<uint, PlayerQuest>();
        player2.Quests[12046] = new PlayerQuest(12046, QuestState.Completed, 1);
        player2.Quests[12047] = new PlayerQuest(12047, QuestState.InProgress, 1);
        player2.PlayerPets = new Dictionary<byte, Player.PlayerPetData>();
        player2.CurMap = gmap;

        bool interactResult = EveEventInterpreter.TryExecute(player2, gmap, 1);
        Console.WriteLine("Interact with NPC #1 result: " + interactResult);
        bool hasRobinsonFallback = player2.PlayerPets.Values.Any(p => p != null && Player.IsSamePetOrCompanion(p.PetID, 12178));
        Console.WriteLine("Has Robinson via NPC #1 fallback: " + hasRobinsonFallback);
        if (!hasRobinsonFallback) {
            throw new Exception("Robinson was NOT recruited via NPC #1 fallback!");
        }

        Console.WriteLine("ALL ROBINSON RECRUITMENT TESTS PASSED SUCCESSFULLY!");
        } catch (Exception ex) {
            Console.WriteLine("EXCEPTION: " + ex.ToString());
            throw;
        }
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies @(
    $phoenixDataDll,
    $phoenixCoreDll,
    $wloCoreDll,
    $rcLibraryDll,
    $pserverCoreDll,
    "System.Core.dll"
)

[TestRobinsonVerification]::Run($repoRoot)
