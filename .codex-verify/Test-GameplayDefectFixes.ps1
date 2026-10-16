$repoRoot = "D:\GitHub\Wonderland-Private-Server"

$phoenixDataDll = "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$phoenixCoreDll = "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
$wloCoreDll = "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
$rcLibraryDll = "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
$pserverCoreDll = "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$serverDll = "$repoRoot\bin\Debug\WonderlandPrivateServer.dll"

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
using Network.ActionCodes;
using Game;
using Game.Code;
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

public static class GameplayDefectTests {
    public static void RunAll(string repoRoot) {
        Console.WriteLine("[TEST SUITE] Executing Gameplay Defect Verification Tests...");

        var itemDat = new PhxItemDat();
        itemDat.Load(repoRoot + @"\Data\itemDat.wpdat").Wait();
        DataBase.GameDataBase.GlobalInstance = new DataBase.GameDataBase();
        DataBase.GameDataBase.GlobalInstance.EveDat = new EveManager();
        DataBase.GameDataBase.GlobalInstance.EveDat.LoadFile(repoRoot + @"\Data\eve.Emg");

        Test1_PetHotelDepositAndWithdrawal(itemDat);
        Test2_TentEntryAndPointerReading(itemDat);
        Test3_InventoryItemUsageDispatching(itemDat);
        Test4_NpcShopPurchasing(itemDat);

        Console.WriteLine("[TEST SUITE] ALL 4 GAMEPLAY DEFECT TESTS PASSED SUCCESSFULLY!");
    }

    private static Player CreateMockPlayer(PhxItemDat itemDat, out PacketCaptureSocket sock) {
        sock = (PacketCaptureSocket)FormatterServices.GetUninitializedObject(typeof(PacketCaptureSocket));
        sock.m_IncomingPackets = new ConcurrentQueue<IPacket>();
        sock.SentBuffers = new List<byte[]>();
        var player = new Player(sock, itemDat);
        player.CharName = "GameplayTester";
        player.CharID = 5001;
        player.Quests = new Dictionary<uint, PlayerQuest>();
        player.PlayerPets = new Dictionary<byte, Player.PlayerPetData>();
        player.HotelPets = new Dictionary<byte, Player.PlayerPetData>();
        var map = new GameMap();
        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(map, (uint)10001);
        player.CurMap = map;
        player.Eqs.SetGold(50000);
        return player;
    }

    private static void Test1_PetHotelDepositAndWithdrawal(PhxItemDat itemDat) {
        Console.WriteLine("--> Test 1: Pet Hotel Protocol (AC 31:5, 31:4, 31:8, 31:12)");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        // Setup 1 pet in team and 1 pet in hotel
        var pet1 = new Player.PlayerPetData();
        pet1.PetID = 1001;
        pet1.PetName = "TeamPet";
        pet1.Slot = 1;
        pet1.ClientSlot = 1;
        player.PlayerPets[1] = pet1;

        var pet2 = new Player.PlayerPetData();
        pet2.PetID = 1002;
        pet2.PetName = "HotelPet";
        pet2.Slot = 1;
        pet2.ClientSlot = 0;
        player.HotelPets[1] = pet2;

        var ac31 = AC.GetAction(31);
        if (ac31 == null) throw new Exception("FAIL: AC31 handler not found!");

        // 1. Deposit Pet: Client sends [31, 3, slot=1]
        byte[] depositRaw = new byte[] { 0xF4, 0x44, 0x03, 0x00, 31, 3, 1 };
        var recvDeposit = new RecievePacket(depositRaw);
        recvDeposit.SetPtr(5); // Point after AC 31
        ac31.ProcessPkt(player, recvDeposit);

        bool hasUnlock8 = sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 31 && b[5] == 8);
        bool hasUnlock12 = sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 31 && b[5] == 12);

        if (!hasUnlock8) throw new Exception("FAIL: Pet deposit did NOT emit AC 31:8 storage unlock!");
        if (!hasUnlock12) throw new Exception("FAIL: Pet deposit did NOT emit AC 31:12 modal unlock!");

        // 2. Withdraw Pet: Client sends [31, 2, hotelSlot=1]
        sock.SentBuffers.Clear();
        byte[] withdrawRaw = new byte[] { 0xF4, 0x44, 0x03, 0x00, 31, 2, 1 };
        var recvWithdraw = new RecievePacket(withdrawRaw);
        recvWithdraw.SetPtr(5); // Point after AC 31
        ac31.ProcessPkt(player, recvWithdraw);

        int idx31_5 = sock.SentBuffers.FindIndex(b => b.Length >= 7 && b[4] == 31 && b[5] == 5);
        int idx31_4 = sock.SentBuffers.FindIndex(b => b.Length >= 7 && b[4] == 31 && b[5] == 4);
        bool withdrawUnlock12 = sock.SentBuffers.Any(b => b.Length >= 6 && b[4] == 31 && b[5] == 12);

        if (idx31_5 == -1) throw new Exception("FAIL: Pet withdrawal did NOT emit AC 31:5 copy packet!");
        if (idx31_4 == -1) throw new Exception("FAIL: Pet withdrawal did NOT emit AC 31:4 slot clear packet!");
        if (idx31_5 >= idx31_4) throw new Exception("FAIL: AC 31:4 slot clear was emitted BEFORE AC 31:5 copy packet!");
        if (!withdrawUnlock12) throw new Exception("FAIL: Pet withdrawal did NOT emit AC 31:12 modal unlock!");

        Console.WriteLine("    [PASS] Pet Hotel correctly emitted AC 31:5 copy before AC 31:4 clear, with AC 31:8 and AC 31:12 modal unlocks.");
    }

    private static void Test2_TentEntryAndPointerReading(PhxItemDat itemDat) {
        Console.WriteLine("--> Test 2: Tent Entry Pointer and Virtual Map ID Resolution");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        var tent = player.Tent;

        if (tent.Owner == null || tent.Owner.CharID != 5001) {
            throw new Exception("FAIL: Tent.Owner property failed to return valid player!");
        }

        var map = (GameMap)player.CurMap;
        var ac65 = AC.GetAction(65);
        if (ac65 == null) throw new Exception("FAIL: AC65 handler not found!");

        // Client packet: [65, 1, CharID: 5001 (0x1389)] -> [0xF4, 0x44, 0x06, 0x00, 65, 1, 0x89, 0x13, 0x00, 0x00]
        byte[] tentEntryRaw = new byte[] { 0xF4, 0x44, 0x06, 0x00, 65, 1, 0x89, 0x13, 0x00, 0x00 };
        var recvTent = new RecievePacket(tentEntryRaw);
        recvTent.SetPtr(5); // Point after AC 65
        ac65.ProcessPkt(player, recvTent);

        // Verify return location was saved
        if (player.TentReturnMap == null || player.TentReturnMap.DstMap != 10001) {
            throw new Exception("FAIL: Tent entry did not save valid return location!");
        }

        Console.WriteLine("    [PASS] Tent entry correctly read Owner CharID via ptr 6 and configured virtual map return location.");
    }

    private static void Test3_InventoryItemUsageDispatching(PhxItemDat itemDat) {
        Console.WriteLine("--> Test 3: Inventory Item Usage Dispatching (Bamboo Dragonfly #34169)");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        // Add Bamboo Dragonfly #34169 to slot 1
        player.Inv.AddItem(34169, 1, false);

        var ac23 = AC.GetAction(23);
        if (ac23 == null) throw new Exception("FAIL: AC23 handler not found!");

        // Packet for AC 23:15, slot 1: [0xF4, 0x44, 0x05, 0x00, 23, 15, 1, 1, 0, 0]
        byte[] useRaw = new byte[] { 0xF4, 0x44, 0x05, 0x00, 23, 15, 1, 1, 0, 0 };
        var recvUse = new RecievePacket(useRaw);
        recvUse.SetPtr(5); // Point after AC 23
        ac23.ProcessPkt(player, recvUse);

        if (player.ActiveVehicleID != 34169) {
            throw new Exception("FAIL: ActiveVehicleID expected 34169 but was " + player.ActiveVehicleID);
        }

        // Use again to dismount
        sock.SentBuffers.Clear();
        var recvDismount = new RecievePacket(useRaw);
        recvDismount.SetPtr(5);
        ac23.ProcessPkt(player, recvDismount);

        if (player.ActiveVehicleID != 0) {
            throw new Exception("FAIL: ActiveVehicleID expected 0 after dismount but was " + player.ActiveVehicleID);
        }

        Console.WriteLine("    [PASS] Bamboo Dragonfly #34169 successfully mounted and dismounted via AC 23:15.");
    }

    private static void Test4_NpcShopPurchasing(PhxItemDat itemDat) {
        Console.WriteLine("--> Test 4: NPC Shop Purchasing Protocol (AC 27:1 & BuyFromNpc)");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        uint initialGold = player.Eqs.Gold;

        // Buy 5x Item 10001 (Apple / Red Potion)
        var cart = new ShoppingCart[] {
            new ShoppingCart { slot = 10001, ammt = 5 }
        };

        var result = player.Inv.BuyFromNpc(cart);
        if (result != NpcBuyResult.Success) {
            throw new Exception("FAIL: BuyFromNpc returned error: " + result);
        }

        if (player.Eqs.Gold >= initialGold) {
            throw new Exception("FAIL: Gold was not deducted after purchasing!");
        }

        int totalCount = 0;
        for (byte s = 1; s <= 50; s++) {
            var item = player.Inv[s];
            if (item != null && item.ItemID == 10001) {
                totalCount += item.Ammt;
            }
        }
        if (totalCount < 5) throw new Exception("FAIL: Purchased item 10001 was not added to inventory! (Total count: " + totalCount + ")");

        // Test AC 27:1 packet handler
        var ac27 = AC.GetAction(27);
        if (ac27 == null) throw new Exception("FAIL: AC27 handler not found!");

        sock.SentBuffers.Clear();
        // AC 27:1 packet buying 1x Item 10001: [27, 1, 0x11, 0x27, 0x01] (3-byte record: itemId 10001, count 1)
        var buyPktRaw = new byte[] { 0xF4, 0x44, 0x05, 0x00, 27, 1, 0x11, 0x27, 0x01 };
        var recvBuy = new RecievePacket(buyPktRaw);
        recvBuy.SetPtr(5);
        ac27.ProcessPkt(player, recvBuy);

        bool hasAc27Response = sock.SentBuffers.Any(b => b.Length >= 7 && b[4] == 27 && b[5] == 1);
        if (!hasAc27Response) {
            throw new Exception("FAIL: AC 27:1 did NOT reply with purchase confirmation!");
        }

        Console.WriteLine("    [PASS] NPC shop purchase verified: BuyFromNpc deducted gold, granted items, and AC 27:1 emitted protocol confirmation.");
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

[GameplayDefectTests]::RunAll($repoRoot)
