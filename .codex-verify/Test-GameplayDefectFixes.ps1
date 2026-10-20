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
        Test6_EveEventMerchantDialogueAndSaleLiquidation(itemDat);
        Test7_PostShopInteractionUnlockAndMovementCleanup(itemDat);

        Console.WriteLine("[TEST SUITE] ALL 7 GAMEPLAY DEFECT TESTS PASSED SUCCESSFULLY!");
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

        var respAc27 = sock.SentBuffers.FirstOrDefault(b => b.Length >= 7 && b[4] == 27 && b[5] == 1);
        if (respAc27 == null) {
            throw new Exception("FAIL: AC 27:1 did NOT reply with purchase confirmation!");
        }
        if (respAc27[6] != 0) {
            throw new Exception("FAIL: AC 27:1 raw ItemID purchase failed with error code: " + respAc27[6]);
        }

        // Test Authentic Client Wire Framing: [count, slot, ammt] (e.g. Map 12000 Props Shop: slot 1 = Bread 32011)
        player.CurrentShopCatalogId = 0x0001FB85u; // Props Shop
        uint goldBeforeBread = player.Eqs.Gold;
        sock.SentBuffers.Clear();
        // Authentic client buy packet: [count: 1, slot: 1, ammt: 1] -> Hex: F4 44 05 00 1B 01 01 01 01
        var clientBuyPkt = new byte[] { 0xF4, 0x44, 0x05, 0x00, 27, 1, 0x01, 0x01, 0x01 };
        var recvClientBuy = new RecievePacket(clientBuyPkt);
        recvClientBuy.SetPtr(5);
        ac27.ProcessPkt(player, recvClientBuy);

        var clientResp = sock.SentBuffers.FirstOrDefault(b => b.Length >= 7 && b[4] == 27 && b[5] == 1);
        if (clientResp == null) {
            throw new Exception("FAIL: AC 27:1 did NOT reply to authentic client buy packet!");
        }
        if (clientResp[6] != 0) {
            throw new Exception("FAIL: Authentic client catalog purchase returned error code: " + clientResp[6]);
        }

        uint goldAfterBread = player.Eqs.Gold;
        if (goldBeforeBread - goldAfterBread != 10) {
            throw new Exception("FAIL: Authentic BuyPrice deduction mismatch for Bread #32011! Expected 10 Gold, got: " + (goldBeforeBread - goldAfterBread));
        }

        bool hasBread = false;
        for (byte s = 1; s <= 50; s++) {
            var item = player.Inv[s];
            if (item != null && item.ItemID == 32011) {
                hasBread = true;
                break;
            }
        }
        if (!hasBread) {
            throw new Exception("FAIL: Slot 1 (Bread #32011) was not resolved or added to inventory!");
        }

        // Test Map 12000 Props Shop Slot 2 (Rice Wine #32002) resolved directly from EveData InteractiveInfo
        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player.CurMap, (uint)12000);
        uint goldBeforeWine = player.Eqs.Gold;
        sock.SentBuffers.Clear();
        // [count: 1, slot: 2, ammt: 1] -> Hex: F4 44 05 00 1B 01 01 02 01
        var buyRiceWinePkt = new byte[] { 0xF4, 0x44, 0x05, 0x00, 27, 1, 0x01, 0x02, 0x01 };
        var recvRiceWine = new RecievePacket(buyRiceWinePkt);
        recvRiceWine.SetPtr(5);
        ac27.ProcessPkt(player, recvRiceWine);

        var wineResp = sock.SentBuffers.FirstOrDefault(b => b.Length >= 7 && b[4] == 27 && b[5] == 1);
        if (wineResp == null || wineResp[6] != 0) {
            throw new Exception("FAIL: Map 12000 slot 2 purchase failed with code: " + (wineResp != null ? wineResp[6].ToString() : "null"));
        }

        uint goldAfterWine = player.Eqs.Gold;
        if (goldBeforeWine - goldAfterWine != 50) {
            throw new Exception("FAIL: Authentic BuyPrice deduction mismatch for Rice Wine #32002! Expected 50 Gold, got: " + (goldBeforeWine - goldAfterWine));
        }

        bool hasWine = false;
        for (byte s = 1; s <= 50; s++) {
            var item = player.Inv[s];
            if (item != null && item.ItemID == 32002) {
                hasWine = true;
                break;
            }
        }
        if (!hasWine) {
            throw new Exception("FAIL: Map 12000 Slot 2 (Rice Wine #32002) was not resolved from InteractiveInfo!");
        }

        Console.WriteLine("    [PASS] NPC shop purchase verified: Both raw ItemID and authentic 1-based catalog slot purchasing (fallback + Map InteractiveInfo) succeeded byte-for-byte.");

        Console.WriteLine("--> Test 5: NPC Shop Selling Protocol (AC 27:2 & SellToNpc)");

        // 1. Test OpenNpcSale opcodes (Mode 0 -> AC 27:4 Form_SaleEquip_1, Mode 1 -> AC 27:3 Form_SaleThing_1)
        sock.SentBuffers.Clear();
        player.OpenNpcSale(0);
        var openEquip = sock.SentBuffers.FirstOrDefault(b => b.Length >= 6 && b[4] == 27 && b[5] == 4);
        if (openEquip == null) throw new Exception("FAIL: OpenNpcSale(0) did NOT send AC 27:4 for Form_SaleEquip_1!");

        sock.SentBuffers.Clear();
        player.OpenNpcSale(1);
        var openThing = sock.SentBuffers.FirstOrDefault(b => b.Length >= 6 && b[4] == 27 && b[5] == 3);
        if (openThing == null) throw new Exception("FAIL: OpenNpcSale(1) did NOT send AC 27:3 for Form_SaleThing_1!");

        // 2. Test Selling General Items (Mode 1): Sell slot with Bread #32011
        byte breadSlot = 0;
        for (byte s = 1; s <= 50; s++) {
            if (player.Inv[s] != null && player.Inv[s].ItemID == 32011) {
                breadSlot = s;
                break;
            }
        }
        if (breadSlot == 0) throw new Exception("FAIL: Bread #32011 not found in inventory for sale test!");

        uint goldBeforeSell = player.Eqs.Gold;
        sock.SentBuffers.Clear();
        // Client sale packet: [27, 2, slot, mode=1] -> F4 44 04 00 1B 02 <breadSlot> 01
        var sellThingPkt = new byte[] { 0xF4, 0x44, 0x04, 0x00, 27, 2, breadSlot, 1 };
        var recvSellThing = new RecievePacket(sellThingPkt);
        recvSellThing.SetPtr(5);
        ac27.ProcessPkt(player, recvSellThing);

        // Check response: official AC 27:2 with result 0 (Success) at VA 0x002FE0B6
        var sellResp = sock.SentBuffers.FirstOrDefault(b => b.Length >= 7 && b[4] == 27 && b[5] == 2);
        if (sellResp == null) throw new Exception("FAIL: AC 27:2 did NOT reply with AC 27:2 sell response!");
        if (sellResp[6] != 0) throw new Exception("FAIL: Selling commodity failed with error code: " + sellResp[6]);

        if (player.Inv[breadSlot] != null && player.Inv[breadSlot].ItemID != 0) {
            throw new Exception("FAIL: Sold item was not removed from inventory slot " + breadSlot);
        }
        if (player.Eqs.Gold <= goldBeforeSell) {
            throw new Exception("FAIL: Gold was not awarded upon liquidating commodity!");
        }

        // 3. Test Selling Equipment (Mode 0): Add weapon, sell via mode 0
        player.Inv.AddItem(17064, 1); // Iron Sword (Equippos=3)
        byte swordSlot = 0;
        for (byte s = 1; s <= 50; s++) {
            if (player.Inv[s] != null && player.Inv[s].ItemID == 17064) {
                swordSlot = s;
                break;
            }
        }
        if (swordSlot == 0) throw new Exception("FAIL: Iron Sword #17064 not found in inventory for equipment sale test!");

        uint goldBeforeSword = player.Eqs.Gold;
        sock.SentBuffers.Clear();
        // Client sale packet: [27, 2, swordSlot, mode=0]
        var sellEquipPkt = new byte[] { 0xF4, 0x44, 0x04, 0x00, 27, 2, swordSlot, 0 };
        var recvSellEquip = new RecievePacket(sellEquipPkt);
        recvSellEquip.SetPtr(5);
        ac27.ProcessPkt(player, recvSellEquip);

        var equipResp = sock.SentBuffers.FirstOrDefault(b => b.Length >= 7 && b[4] == 27 && b[5] == 2);
        if (equipResp == null || equipResp[6] != 0) {
            throw new Exception("FAIL: Selling equipment failed with error code: " + (equipResp != null ? equipResp[6].ToString() : "null"));
        }
        if (player.Inv[swordSlot] != null && player.Inv[swordSlot].ItemID != 0) {
            throw new Exception("FAIL: Sold equipment was not removed from inventory slot " + swordSlot);
        }
        if (player.Eqs.Gold <= goldBeforeSword) {
            throw new Exception("FAIL: Gold was not awarded upon liquidating equipment!");
        }

        // 4. Test Category mismatch rejection (attempting to sell equipment in mode 1)
        player.Inv.AddItem(17064, 1);
        byte mismatchSlot = 0;
        for (byte s = 1; s <= 50; s++) {
            if (player.Inv[s] != null && player.Inv[s].ItemID == 17064) {
                mismatchSlot = s;
                break;
            }
        }
        sock.SentBuffers.Clear();
        var mismatchPkt = new byte[] { 0xF4, 0x44, 0x04, 0x00, 27, 2, mismatchSlot, 1 };
        var recvMismatch = new RecievePacket(mismatchPkt);
        recvMismatch.SetPtr(5);
        ac27.ProcessPkt(player, recvMismatch);

        var mismatchResp = sock.SentBuffers.FirstOrDefault(b => b.Length >= 7 && b[4] == 27 && b[5] == 2);
        if (mismatchResp == null || mismatchResp[6] != 4) {
            throw new Exception("FAIL: Mode mismatch (equipment in mode 1) was not rejected with code 4!");
        }

        Console.WriteLine("    [PASS] NPC shop liquidation verified: AC 27:4/5 window opening, commodity liquidation, equipment liquidation, and category validation succeeded byte-for-byte.");
    }

    private static void Test6_EveEventMerchantDialogueAndSaleLiquidation(PhxItemDat itemDat) {
        Console.WriteLine("--> Test 6: Eve Event Merchant Interception & Liquidation Window Invocation");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player.CurMap, (uint)12000);

        // Scenario A: Native Eve Event 31 (Map 12000 ClickID 23 'Props Shop' TID 13007)
        // Question 5 dispatched -> player chooses answer 31 ("Sell goods")
        sock.SentBuffers.Clear();
        bool executed = EveEventInterpreter.TryExecute(player, player.CurMap as GameMap, 23);
        if (!executed) throw new Exception("FAIL: TryExecute failed for Map 12000 ClickID 23!");

        if (player.OnDialogueChoice == null) throw new Exception("FAIL: OnDialogueChoice callback was not registered for Question 5!");

        // Simulate client sending answer 31 ("Sell goods" / 0x1F) for Question 5
        sock.SentBuffers.Clear();
        player.OnDialogueChoice(31);

        // Verify AC 27:3 was sent (Form_SaleThing_1 for Props Shop)
        var openSaleThing = sock.SentBuffers.FirstOrDefault(b => b.Length >= 6 && b[4] == 27 && b[5] == 3);
        if (openSaleThing == null) throw new Exception("FAIL: Answer 31 to Question 5 did NOT open Form_SaleThing_1 (AC 27:3)!");

        // Scenario B: Native Eve Event 34 (Map 12000 ClickID 24 'Weapon Shop' TID 13006)
        // Question 9 dispatched -> player chooses answer 31 ("Sell equipment")
        sock.SentBuffers.Clear();
        bool execWeapon = EveEventInterpreter.TryExecute(player, player.CurMap as GameMap, 24);
        if (!execWeapon) throw new Exception("FAIL: TryExecute failed for Map 12000 ClickID 24!");

        if (player.OnDialogueChoice == null) throw new Exception("FAIL: OnDialogueChoice callback was not registered for Question 9!");

        sock.SentBuffers.Clear();
        player.OnDialogueChoice(31);

        // Verify AC 27:4 was sent (Form_SaleEquip_1 for Weapon Shop)
        var openSaleEquip = sock.SentBuffers.FirstOrDefault(b => b.Length >= 6 && b[4] == 27 && b[5] == 4);
        if (openSaleEquip == null) throw new Exception("FAIL: Answer 31 to Question 9 did NOT open Form_SaleEquip_1 (AC 27:4)!");

        // Scenario C: Native Question 6 Interception (Merchant Choice without authored branches in Eve.emg)
        // Answer 31 ("Sell goods") -> Open liquidation Form_SaleThing_1 (AC 27:3)
        sock.SentBuffers.Clear();
        Action finishAction = () => { player.CancelInteraction(); };
        var handledSell = (bool)typeof(EveEventInterpreter).GetMethod("TryHandleMerchantChoice", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { player, player.CurMap as GameMap, (ushort)23, (ushort)6, (ushort)31, finishAction });
        if (!handledSell) throw new Exception("FAIL: TryHandleMerchantChoice returned false for Question 6 Answer 31!");

        var openSaleQ6 = sock.SentBuffers.FirstOrDefault(b => b.Length >= 6 && b[4] == 27 && b[5] == 3);
        if (openSaleQ6 == null) throw new Exception("FAIL: TryHandleMerchantChoice Q6 A31 did NOT open AC 27:3!");

        // Scenario D: Native Question 6 Interception Answer 30 ("Buy goods") -> Open Buy catalog
        sock.SentBuffers.Clear();
        var handledBuy = (bool)typeof(EveEventInterpreter).GetMethod("TryHandleMerchantChoice", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { player, player.CurMap as GameMap, (ushort)23, (ushort)6, (ushort)30, finishAction });
        if (!handledBuy) throw new Exception("FAIL: TryHandleMerchantChoice returned false for Question 6 Answer 30!");

        var openBuyQ6 = sock.SentBuffers.FirstOrDefault(b => b.Length >= 10 && b[4] == 35 && b[5] == 12);
        if (openBuyQ6 == null) throw new Exception("FAIL: TryHandleMerchantChoice Q6 A30 did NOT send AC 35:12 Buy Catalog!");

        Console.WriteLine("    [PASS] Eve Event Merchant Interception & Liquidation: Question 5/9 Opcode 7 sell redirection and Question 6/8 Buy/Sell fallback verified byte-for-byte.");
    }

    private static void Test7_PostShopInteractionUnlockAndMovementCleanup(PhxItemDat itemDat) {
        Console.WriteLine("--> Test 7: Post-Shop Interaction Unlock & Movement Cleanup (AC 20:6 -> AC 20:8, AC 06:1 -> Reset)");
        PacketCaptureSocket sock;
        var player = CreateMockPlayer(itemDat, out sock);

        // Case A: Player closes shop modal/catalog (client sends AC 20:6)
        player.NpcSaleMode = 1;
        player.CurrentShopCatalogId = 0x0001FB85u;
        sock.SentBuffers.Clear();

        var ac20 = AC.GetAction(20);
        if (ac20 == null) throw new Exception("FAIL: AC20 handler not found!");

        var closeShopPkt = new byte[] { 0xF4, 0x44, 0x02, 0x00, 20, 6 };
        var recvClose = new RecievePacket(closeShopPkt);
        recvClose.SetPtr(5);
        ac20.ProcessPkt(player, recvClose);

        if (player.NpcSaleMode != null) throw new Exception("FAIL: NpcSaleMode was not reset to null after closing shop via AC 20:6!");
        if (player.CurrentShopCatalogId != 0) throw new Exception("FAIL: CurrentShopCatalogId was not reset to 0 after closing shop via AC 20:6!");

        var unlockPosture = sock.SentBuffers.FirstOrDefault(b => b.Length >= 7 && b[4] == 6 && b[5] == 2 && b[6] == 0);
        if (unlockPosture == null) throw new Exception("FAIL: AC 20:6 did not reply with AC 6:2 [0] posture unlock!");

        var unlockInteraction = sock.SentBuffers.FirstOrDefault(b => b.Length >= 6 && b[4] == 20 && b[5] == 8);
        if (unlockInteraction == null) throw new Exception("FAIL: AC 20:6 did not reply with AC 20:8 interaction unlock!");

        var unlockHud = sock.SentBuffers.FirstOrDefault(b => b.Length >= 6 && b[4] == 5 && b[5] == 4);
        if (unlockHud == null) throw new Exception("FAIL: AC 20:6 did not reply with AC 5:4 scene gate/HUD unlock!");

        // Case B: Player walks away while vendor session was active (client sends AC 6:1)
        player.NpcSaleMode = 0;
        player.CurrentShopCatalogId = 0x0001FB84u;
        player.CurX = 100;
        player.CurY = 100;
        sock.SentBuffers.Clear();

        var ac06 = AC.GetAction(6);
        if (ac06 == null) throw new Exception("FAIL: AC06 handler not found!");

        var movePkt = new byte[] { 0xF4, 0x44, 0x06, 0x00, 6, 1, 0, 150, 0, 150, 0 };
        var recvMove = new RecievePacket(movePkt);
        recvMove.SetPtr(5);
        ac06.ProcessPkt(player, recvMove);

        if (player.NpcSaleMode != null) throw new Exception("FAIL: NpcSaleMode was not reset to null after movement via AC 06:1!");
        if (player.CurrentShopCatalogId != 0) throw new Exception("FAIL: CurrentShopCatalogId was not reset to 0 after movement via AC 06:1!");

        var moveUnlockInteraction = sock.SentBuffers.FirstOrDefault(b => b.Length >= 6 && b[4] == 20 && b[5] == 8);
        if (moveUnlockInteraction == null) throw new Exception("FAIL: Movement during active shop did not dispatch AC 20:8 interaction unlock!");

        var moveUnlockHud = sock.SentBuffers.FirstOrDefault(b => b.Length >= 6 && b[4] == 5 && b[5] == 4);
        if (moveUnlockHud == null) throw new Exception("FAIL: Movement during active shop did not dispatch AC 5:4 HUD unlock!");

        Console.WriteLine("    [PASS] Post-shop interaction unlock and movement cleanup verified: AC 20:6 and AC 06:1 cleanly reset vendor sessions and emit AC 20:8 + AC 5:4 byte-for-byte.");
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
