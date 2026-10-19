$repoRoot = "D:\GitHub\Wonderland-Private-Server"

$phoenixDataDll = "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$phoenixCoreDll = "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
$wloCoreDll = "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
$rcLibraryDll = "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
$pserverCoreDll = "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$serverExe = "$repoRoot\bin\Debug\Wonderland Private Server.exe"
$serverDll = "$repoRoot\bin\Debug\WonderlandPrivateServer.dll"
if (Test-Path $serverExe) {
    Copy-Item $serverExe $serverDll -Force
}

Add-Type -Path $phoenixDataDll
Add-Type -Path $phoenixCoreDll
Add-Type -Path $wloCoreDll
Add-Type -Path $rcLibraryDll
Add-Type -Path $pserverCoreDll
Add-Type -Path $serverDll

$code = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using Network;
using Game;
using Game.PlayerRelated;
using Game.QuestRelated;
using Game.Maps;
using Game.Maps.Code;
using Game.DataFiles;
using DataFiles;
using RCLibrary.Core.Networking;
using Wonderland_Private_Server.ActionCodes;

public class PacketCaptureSocket : SocketClient {
    public List<byte[]> SentBuffers = new List<byte[]>();
    public override void SendPacket(IPacket p) {
        if (p != null && p.Buffer != null) {
            SentBuffers.Add(p.Buffer.ToArray());
        }
    }
}

public static class NarrativeCutsceneTests {
    public static void RunAll(string repoRoot) {
        Console.WriteLine("[TEST SUITE] Executing Narrative & Cutscene Verification Tests...");

        // Setup Eve and Item data
        var itemDat = new PhxItemDat();
        itemDat.Load(repoRoot + @"\Data\itemDat.wpdat").Wait();
        DataBase.GameDataBase.GlobalInstance = new DataBase.GameDataBase();
        DataBase.GameDataBase.GlobalInstance.EveDat = new EveManager();
        DataBase.GameDataBase.GlobalInstance.EveDat.LoadFile(repoRoot + @"\Data\eve.Emg");

        Test1_CaptainUiRestoration(itemDat);
        Test2_MonkeyCgMovieTrigger(repoRoot, itemDat);
        Test3_RobinsonBeachArrivalSequence(repoRoot, itemDat);
        Test4_ShipStormCutsceneTiming(repoRoot, itemDat);
        Test5_BeachArrivalCutsceneHandshake(repoRoot, itemDat);
        Test6_RobinsonDuplicateDialoguePrevention(repoRoot, itemDat);

        Console.WriteLine("[TEST SUITE] ALL TESTS PASSED!");
    }

    private static Player CreateMockPlayer(PhxItemDat itemDat, out PacketCaptureSocket sock) {
        sock = (PacketCaptureSocket)FormatterServices.GetUninitializedObject(typeof(PacketCaptureSocket));
        sock.m_IncomingPackets = new ConcurrentQueue<IPacket>();
        sock.SentBuffers = new List<byte[]>();
        var player = new Player(sock, itemDat);
        player.CharName = "TestPlayer";
        player.CharID = 12345;
        player.Quests = new Dictionary<uint, PlayerQuest>();
        player.PlayerPets = new Dictionary<byte, Player.PlayerPetData>();
        return player;
    }

    private static void Test1_CaptainUiRestoration(PhxItemDat itemDat) {
        Console.WriteLine("--> Test 1: Captain Storyline UI Restoration (AC 5:4 in CancelInteraction)");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        player.CancelInteraction();

        bool hasAc5_4 = sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 5 && b[5] == 4);
        bool hasAc6_2 = sock.SentBuffers.Any(b => b.Length >= 7 && b[4] == 6 && b[5] == 2 && b[6] == 0);
        bool hasAc20_8 = sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 20 && b[5] == 8);

        if (!hasAc5_4) throw new Exception("FAIL: CancelInteraction did NOT send AC 5:4 (HUD unfreeze)!");
        if (!hasAc6_2) throw new Exception("FAIL: CancelInteraction did NOT send AC 6:2 [0] (default stance)!");
        if (!hasAc20_8) throw new Exception("FAIL: CancelInteraction did NOT send AC 20:8 (movement unlock)!");

        Console.WriteLine("    [PASS] CancelInteraction properly emits AC 5:4, AC 6:2 [0], and AC 20:8.");
    }

    private static void Test2_MonkeyCgMovieTrigger(string repoRoot, PhxItemDat itemDat) {
        Console.WriteLine("--> Test 2: Monkey Storyline CG Cutscene 12000 Trigger");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        var gmap = new GameMap();
        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gmap, (uint)11016);
        player.CurMap = gmap;

        // Execute S. Monkey event (Map 11016, clickId 1)
        bool executed = EveEventInterpreter.TryExecute(player, gmap, 1);
        if (!executed) throw new Exception("FAIL: S.Monkey TryExecute returned false!");

        // Execute OnInteractionComplete callback (cutscene finish)
        if (player.OnInteractionComplete == null) throw new Exception("FAIL: OnInteractionComplete was not set!");
        player.OnInteractionComplete.Invoke();

        // Complete subsequent dialogue sequence if interactive dialogue follows cutscene
        if (player.OnInteractionComplete != null) {
            player.OnInteractionComplete.Invoke();
        }

        bool hasAc186_12_12000 = sock.SentBuffers.Any(b => b.Length >= 8 && b[4] == 186 && b[5] == 12 &&
            b[6] == (12000 & 0xFF) && b[7] == ((12000 >> 8) & 0xFF));

        bool hasAc20_1_Cutscene12000 = sock.SentBuffers.Any(b => b.Length >= 15 && b[4] == 20 && b[5] == 1 &&
            b[10] == 5 && BitConverter.ToUInt32(b, 15) == 12000);

        bool hasAc186_12_11011 = sock.SentBuffers.Any(b => b.Length >= 8 && b[4] == 186 && b[5] == 12 &&
            b[6] == (11011 & 0xFF) && b[7] == ((11011 >> 8) & 0xFF));

        bool hasAc20_1_Cutscene11011 = sock.SentBuffers.Any(b => b.Length >= 15 && b[4] == 20 && b[5] == 1 &&
            b[10] == 5 && BitConverter.ToUInt32(b, 15) == 11011);

        bool hasStar29 = sock.SentBuffers.Any(b => b.Length >= 8 && b[4] == 15 && b[5] == 20 && b[6] == 29 && b[7] == 0);

        bool hasRecruited = player.PlayerPets.Values.Any(p => p != null && p.PetID == 17162);
        bool hasAc5_4 = sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 5 && b[5] == 4);

        if (!hasAc186_12_12000) throw new Exception("FAIL: AC 186:12 Cutscene 12000 was NOT emitted!");
        if (!hasAc20_1_Cutscene12000) throw new Exception("FAIL: AC 20:1 Type 5 Cutscene 12000 was NOT emitted!");
        if (!hasAc186_12_11011) throw new Exception("FAIL: AC 186:12 Cutscene 11011 (CG Photo) was NOT emitted!");
        if (!hasAc20_1_Cutscene11011) throw new Exception("FAIL: AC 20:1 Type 5 Cutscene 11011 (CG Photo) was NOT emitted!");
        if (!hasStar29) throw new Exception("FAIL: AC 15:20 Star 29 (Story Album CG) was NOT emitted!");
        if (!hasRecruited) throw new Exception("FAIL: S.Monkey (TID 17162) was not recruited!");
        if (!hasAc5_4) throw new Exception("FAIL: AC 5:4 was NOT emitted on S.Monkey completion!");

        Console.WriteLine("    [PASS] S.Monkey storyline authentically dispatches Cutscene 12000, Story CG Cutscene 11011 (JRole_5_3), Star 29, and AC 5:4.");
    }

    private static void Test3_RobinsonBeachArrivalSequence(string repoRoot, PhxItemDat itemDat) {
        Console.WriteLine("--> Test 3: Robinson Beach Arrival & Dialogue Sequence");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        var gmap = new GameMap();
        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gmap, (uint)10035);
        player.CurMap = gmap;
        player.BeachCutsceneActive = true;
        player.BeachCutsceneStep = 2;

        // Step 2: AC 24:1 + AC 20:10
        sock.SentBuffers.Clear();
        AC20.AdvanceBeachCutscene(player);
        if (player.BeachCutsceneStep != 3) throw new Exception("FAIL: Step did not advance to 3!");
        if (player.Quests.ContainsKey(12040) && player.Quests[12040].State == QuestState.InProgress)
            throw new Exception("FAIL: Quest 12040 was prematurely set to InProgress in Step 2!");
        if (!sock.SentBuffers.Any(b => b.Length >= 9 && b[4] == 24 && b[5] == 1))
            throw new Exception("FAIL: AC 24:1 was not sent in Step 2!");

        // Step 3: AC 20:10
        sock.SentBuffers.Clear();
        AC20.AdvanceBeachCutscene(player);
        if (player.BeachCutsceneStep != 4) throw new Exception("FAIL: Step did not advance to 4!");

        // Step 4: Camera pan AC 24:5 + AC 20:10
        sock.SentBuffers.Clear();
        AC20.AdvanceBeachCutscene(player);
        if (player.BeachCutsceneStep != 5) throw new Exception("FAIL: Step did not advance to 5!");
        if (!sock.SentBuffers.Any(b => b.Length >= 9 && b[4] == 24 && b[5] == 5))
            throw new Exception("FAIL: Camera pan AC 24:5 was not sent in Step 4!");

        // Step 5: Robinson walking AC 22:12 + AC 20:10
        sock.SentBuffers.Clear();
        AC20.AdvanceBeachCutscene(player);
        if (player.BeachCutsceneStep != 6) throw new Exception("FAIL: Step did not advance to 6!");
        if (!sock.SentBuffers.Any(b => b.Length >= 10 && b[4] == 22 && b[5] == 12))
            throw new Exception("FAIL: Robinson walking AC 22:12 was not sent in Step 5!");

        // Step 6: Mobilize player, send AC 5:30 [0], AC 20:8, AC 6:2 [0], AC 5:4, and trigger Robinson Event 8
        sock.SentBuffers.Clear();
        AC20.AdvanceBeachCutscene(player);
        if (player.BeachCutsceneActive) throw new Exception("FAIL: BeachCutsceneActive was not cleared!");
        if (!sock.SentBuffers.Any(b => b.Length >= 9 && b[4] == 5 && b[5] == 30 && b[6] == 0))
            throw new Exception("FAIL: AC 5:30 [0] immobilize release was not sent in Step 6!");
        if (!sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 5 && b[5] == 4))
            throw new Exception("FAIL: AC 5:4 unfreeze was not sent in Step 6!");

        // Step 6 mobilizes player without auto-dialogue: ensure Robinson dialogue is NOT automatically dispatched
        bool hasRobinsonDialogue = sock.SentBuffers.Any(b => b.Length >= 22 && b[4] == 20 && b[5] == 1 &&
            (b[19] | (b[20] << 8)) == 20304);
        if (hasRobinsonDialogue)
            throw new Exception("FAIL: Robinson dialogue was prematurely auto-started in Step 6!");

        // Manual player click on Robinson (Click ID 1) triggers Event 8 Sub 0
        sock.SentBuffers.Clear();
        EveEventInterpreter.TryExecute(player, gmap, 1);
        bool hasRobinsonManualDialogue = sock.SentBuffers.Any(b => b.Length >= 22 && b[4] == 20 && b[5] == 1 &&
            (b[19] | (b[20] << 8)) == 20304);
        if (!hasRobinsonManualDialogue)
            throw new Exception("FAIL: Robinson dialogue TalkID 20304 was NOT started on manual click!");

        Console.WriteLine("    [PASS] Beach sequence advanced through Steps 2-5, Step 6 mobilized player without forced dialogue, and manual click on Robinson correctly started Event 8.");
    }

    private static void Test4_ShipStormCutsceneTiming(string repoRoot, PhxItemDat itemDat) {
        Console.WriteLine("--> Test 4: Ship Storm Cutscene Packet State Handshake (AC 186:9 -> AC 20:6)");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        var gmap = new GameMap();
        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gmap, (uint)10017);
        player.CurMap = gmap;

        // Simulate Storm Cutscene initialization (as dispatched by Opcode 8 in Captain Event 11)
        player.PlayingStormCutscene = true;
        player.StormCutsceneAckReceived = false;

        var ac20 = new AC20();
        var ac186 = new Network.ActionCodes.AC186();
        var rxAck20_6 = new RecievePacket(new byte[] { 0xF4, 0x44, 2, 0, 20, 6 });
        var rxAck186_9 = new RecievePacket(new byte[] { 0xF4, 0x44, 4, 0, 186, 9, 1, 0 });

        // 1. Premature AC 20:6 before client AC 186:9 handshake: must be absorbed
        sock.SentBuffers.Clear();
        ac20.ProcessPkt(player, rxAck20_6);

        if (!player.PlayingStormCutscene)
            throw new Exception("FAIL: Premature AC 20:6 before AC 186:9 handshake terminated PlayingStormCutscene prematurely!");
        if (player.PendingBeachCutscene)
            throw new Exception("FAIL: Premature AC 20:6 set PendingBeachCutscene prematurely!");
        if (sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 20 && b[5] == 7))
            throw new Exception("FAIL: Premature AC 20:6 emitted AC 20:7 warp out prematurely!");

        // 2. Client returns AC 186:9 (Cutscene #1 acknowledged by client)
        sock.SentBuffers.Clear();
        ac186.ProcessPkt(player, rxAck186_9);

        if (!player.StormCutsceneAckReceived)
            throw new Exception("FAIL: AC 186:9 did not set StormCutsceneAckReceived!");
        bool hasAc186_9_Resp = sock.SentBuffers.Any(b => b.Length >= 9 && b[4] == 186 && b[5] == 9 && b[6] == 1 && b[7] == 0 && b[8] == 1);
        if (!hasAc186_9_Resp)
            throw new Exception("FAIL: Server did not respond to AC 186:9 with active status 1!");

        // 3. Client finishes cutscene playback and sends AC 20:6
        sock.SentBuffers.Clear();
        ac20.ProcessPkt(player, rxAck20_6);

        if (player.PlayingStormCutscene)
            throw new Exception("FAIL: Valid AC 20:6 after AC 186:9 did not clear PlayingStormCutscene!");
        if (!player.PendingBeachCutscene)
            throw new Exception("FAIL: Valid AC 20:6 after AC 186:9 did not set PendingBeachCutscene!");
        if (!sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 20 && b[5] == 7))
            throw new Exception("FAIL: Valid AC 20:6 did not emit AC 20:7 warp out!");

        Console.WriteLine("    [PASS] Premature AC 20:6 was absorbed; AC 186:9 confirmed handshake; post-cutscene AC 20:6 transitioned to Beach.");
    }

    private static void Test5_BeachArrivalCutsceneHandshake(string repoRoot, PhxItemDat itemDat) {
        Console.WriteLine("--> Test 5: Beach Arrival Pure Packet Handshake (AC 12:1 -> AC 20:6 -> Cutscene 12008)");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        var gmap = new GameMap();
        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gmap, (uint)10035);
        player.CurMap = gmap;
        player.Flags.Add(PlayerFlag.Warping);
        player.PendingBeachCutscene = true;

        var ac12 = new AC12();
        var ac20 = new AC20();
        var rxAc12_1 = new RecievePacket(new byte[] { 0xF4, 0x44, 2, 0, 12, 1 });
        var rxAck20_6 = new RecievePacket(new byte[] { 0xF4, 0x44, 2, 0, 20, 6 });

        // 1. Client completes Map 10035 load and sends AC 12:1
        sock.SentBuffers.Clear();
        ac12.ProcessPkt(player, rxAc12_1);

        if (!player.BeachCutsceneActive)
            throw new Exception("FAIL: AC 12:1 did not activate BeachCutsceneActive!");
        if (player.BeachCutsceneStep != 2)
            throw new Exception("FAIL: AC 12:1 did not set BeachCutsceneStep to 2!");
        if (player.PendingBeachCutscene)
            throw new Exception("FAIL: PendingBeachCutscene was not cleared!");
        if (player.Emote != 9)
            throw new Exception("FAIL: Player emote 9 (sand lie down) was not set!");

        bool hasEmote9Pkt = sock.SentBuffers.Any(b => b.Length >= 11 && b[4] == 32 && b[5] == 2 && b[10] == 9);
        bool hasImmobilize = sock.SentBuffers.Any(b => b.Length >= 7 && b[4] == 5 && b[5] == 30 && b[6] == 1);
        bool hasRobinsonWalk = sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 22 && b[5] == 11);
        bool hasCutscene12008 = sock.SentBuffers.Any(b => b.Length >= 15 && b[4] == 20 && b[5] == 1 &&
            b[10] == 5 && BitConverter.ToUInt32(b, 15) == 12008);

        if (!hasEmote9Pkt) throw new Exception("FAIL: Emote 9 packet was not sent on map load!");
        if (!hasImmobilize) throw new Exception("FAIL: AC 5:30 [1] immobilize was not sent on map load!");
        if (!hasRobinsonWalk) throw new Exception("FAIL: Robinson walk AC 22:11 was not sent on map load!");
        if (!hasCutscene12008) throw new Exception("FAIL: Cutscene 12008 (beach wakeup) was not dispatched on map load!");

        // 2. Client finishes Cutscene 12008 animation and sends AC 20:6
        sock.SentBuffers.Clear();
        ac20.ProcessPkt(player, rxAck20_6);

        if (player.BeachCutsceneStep != 3)
            throw new Exception("FAIL: AC 20:6 did not advance BeachCutsceneStep to 3!");

        bool hasAc24_1 = sock.SentBuffers.Any(b => b.Length >= 9 && b[4] == 24 && b[5] == 1);
        if (!hasAc24_1)
            throw new Exception("FAIL: AC 24:1 progress packet was not sent on Cutscene 12008 completion!");

        Console.WriteLine("    [PASS] Beach arrival strictly packet-driven: AC 12:1 -> Dispatches Cutscene 12008 -> Client AC 20:6 -> Step 3.");
    }

    private static void Test6_RobinsonDuplicateDialoguePrevention(string repoRoot, PhxItemDat itemDat) {
        Console.WriteLine("--> Test 6: Robinson Duplicate Dialogue Prevention & Debounce Verification");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        var gmap = new GameMap();
        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gmap, (uint)10035);
        var robinsonNpc = new QuestNpc {
            CickID = 1,
            TemplateID = 12032,
            Name = "Robinson",
            MapID = 10035,
            X = 1038,
            Y = 2235
        };
        gmap.NpcList.Add(robinsonNpc);
        player.CurMap = gmap;
        player.CurX = 1038;
        player.CurY = 2235;

        var ac20 = new AC20();
        // AC 20:1 with ClickID = 1 (Robinson)
        var rxClickRobinson = new RecievePacket(new byte[] { 0xF4, 0x44, 4, 0, 20, 1, 1, 0 });

        // Subtest 1: Click during BeachCutsceneActive must be dropped with AC 20:8
        player.BeachCutsceneActive = true;
        sock.SentBuffers.Clear();
        ac20.ProcessPkt(player, rxClickRobinson);
        if (player.NativeEventActive)
            throw new Exception("FAIL: AC 20:1 during BeachCutsceneActive started NativeEventActive dialogue!");
        if (!sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 20 && b[5] == 8))
            throw new Exception("FAIL: AC 20:1 during BeachCutsceneActive did not reply with AC 20:8!");

        // Subtest 2: Click during PlayingStormCutscene must be dropped with AC 20:8
        player.BeachCutsceneActive = false;
        player.PlayingStormCutscene = true;
        sock.SentBuffers.Clear();
        ac20.ProcessPkt(player, rxClickRobinson);
        if (player.NativeEventActive)
            throw new Exception("FAIL: AC 20:1 during PlayingStormCutscene started NativeEventActive dialogue!");
        if (!sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 20 && b[5] == 8))
            throw new Exception("FAIL: AC 20:1 during PlayingStormCutscene did not reply with AC 20:8!");
        player.PlayingStormCutscene = false;

        // Subtest 3: AdvanceBeachCutscene Step 6 mobilizes player without starting dialogue
        player.BeachCutsceneActive = true;
        player.BeachCutsceneStep = 6;
        sock.SentBuffers.Clear();
        AC20.AdvanceBeachCutscene(player);
        if (player.BeachCutsceneActive)
            throw new Exception("FAIL: Step 6 did not deactivate BeachCutsceneActive!");
        if (player.NativeEventActive)
            throw new Exception("FAIL: Step 6 unexpectedly started NativeEventActive dialogue!");

        // Manual player click on Robinson starts dialogue and sets debounce
        sock.SentBuffers.Clear();
        ac20.ProcessPkt(player, rxClickRobinson);
        if (player.LastNpcClick != 1)
            throw new Exception("FAIL: Manual click on Robinson did not set LastNpcClick = 1!");
        if (player.LastNpcMap != 10035)
            throw new Exception("FAIL: Manual click on Robinson did not set LastNpcMap = 10035!");
        if (!player.NativeEventActive)
            throw new Exception("FAIL: Manual click on Robinson did not start Robinson NativeEventActive dialogue!");

        // Subtest 4: Trailing click during active dialogue must be dropped with AC 20:8
        sock.SentBuffers.Clear();
        ac20.ProcessPkt(player, rxClickRobinson);
        if (!sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 20 && b[5] == 8))
            throw new Exception("FAIL: Click during active dialogue did not reply with AC 20:8!");

        // Subtest 5: Dialogue completes -> CancelInteraction sets debounce. Trailing click within debounce window must be dropped!
        player.CancelInteraction();
        if (player.NpcClickResumeAt <= DateTime.UtcNow)
            throw new Exception("FAIL: CancelInteraction did not set NpcClickResumeAt in the future!");
        
        sock.SentBuffers.Clear();
        ac20.ProcessPkt(player, rxClickRobinson);
        if (player.NativeEventActive)
            throw new Exception("FAIL: Trailing AC 20:1 within debounce window restarted dialogue prematurely!");
        if (!sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 20 && b[5] == 8))
            throw new Exception("FAIL: Trailing AC 20:1 within debounce window did not reply with AC 20:8!");

        // Subtest 6: After debounce window expires, clicking Robinson is permitted
        player.NpcClickResumeAt = DateTime.UtcNow.AddSeconds(-1);
        sock.SentBuffers.Clear();
        ac20.ProcessPkt(player, rxClickRobinson);
        if (!player.NativeEventActive)
            throw new Exception("FAIL: Legitimate click on Robinson after debounce window did NOT start dialogue!");

        player.CancelInteraction();
        Console.WriteLine("    [PASS] Robinson duplicate dialogue strictly prevented across cutscenes, active dialogues, and trailing click debouncing.");
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies @(
    "System.Core",
    "System.Runtime.Serialization",
    $phoenixDataDll,
    $phoenixCoreDll,
    $wloCoreDll,
    $rcLibraryDll,
    $pserverCoreDll,
    $serverDll
)

[NarrativeCutsceneTests]::RunAll($repoRoot)
