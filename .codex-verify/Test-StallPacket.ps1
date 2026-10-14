param([string]$BuildDirectory="D:\GitHub\Wonderland-Private-Server\bin\Debug")
$ErrorActionPreference = 'Stop'

$refs = @('RCLibrary.dll', 'Phoenix.Core.dll', 'PhoenixData.dll', 'Wlo.Core.dll', 'wlo.pserver.maps.dll', 'wlo.pserver.core.dll', 'Wonderland Private Server.exe') | ForEach-Object {
    Join-Path $BuildDirectory $_
}

foreach ($a in $refs) {
    if (Test-Path $a) {
        [void][Reflection.Assembly]::LoadFrom($a)
    }
}

$csharpCode = @"
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Game;
using Game.Code;
using Game.Experimental;
using Network;
using RCLibrary.Core;
using RCLibrary.Core.Networking;

public class TestSocket : SocketClient
{
    public List<byte[]> SentPackets = new List<byte[]>();
    public override void SendPacket(IPacket p)
    {
        SentPackets.Add(p.Buffer.ToArray());
    }
}

public static class StallVerificationSuite
{
    public static void RunTests()
    {
        Console.WriteLine("[TEST] Starting Persistent Custom Stall & Bazaar Verification Suite...");

        // 1. Initialize Player and Mock Socket
        var socket = (TestSocket)FormatterServices.GetUninitializedObject(typeof(TestSocket));
        socket.SentPackets = new List<byte[]>();
        socket.m_IncomingPackets = new ConcurrentQueue<IPacket>();

        var player = new Player(socket, null);
        player.CharID = 99991;
        player.CharName = "purnaci";
        player.CurX = 1200;
        player.CurY = 800;

        // Ensure Experimental Features Enabled
        ExperimentalFeaturesManager.Instance.EnablePlayerStallBazaar = true;
        var stallEngine = ExperimentalFeaturesManager.Instance.PlayerStallBazaar;
        stallEngine.InitializeDatabase();

        // -------------------------------------------------------------
        // PART 1: Native AC 23:30 / 23:31 Personal Stall Stance Flow
        // -------------------------------------------------------------
        Console.WriteLine("[TEST] Part 1: Native AC 23:30 / 23:31 personal stall flow...");
        byte[] pcapPacket = new byte[] {
            0xF4, 0x44, 0x15, 0x00, 0x17, 0x1E, 0x0C, 0x50, 0x72, 0x69, 0x76, 0x61, 0x74, 0x65, 0x20, 0x53, 0x68, 0x6F, 0x70,
            0x01, 0x09, 0x01, 0x00, 0x00, 0x00, 0x00
        };

        var ac23Type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("Network.ActionCodes.AC23"))
            .First(t => t != null);
        var ac23 = Activator.CreateInstance(ac23Type);
        var rxPkt = new RecievePacket(pcapPacket);

        ac23Type.GetMethod("ProcessPkt").Invoke(ac23, new object[] { player, rxPkt });

        var personalStall = stallEngine.GetStall(player.CharID);
        if (personalStall == null || personalStall.StallName != "Private Shop")
        {
            throw new Exception("FAIL: Personal stall was not registered!");
        }
        Console.WriteLine("[PASS] Personal stall registered (Name: " + personalStall.StallName + ").");

        if (player.Emote != 1)
        {
            throw new Exception("FAIL: Personal stall player emote expected 1, got " + player.Emote);
        }
        Console.WriteLine("[PASS] Personal stall put player into sitting pose (Emote = 1).");

        // Close personal stall
        byte[] closePacket = new byte[] { 0xF4, 0x44, 0x02, 0x00, 0x17, 0x1F };
        ac23Type.GetMethod("ProcessPkt").Invoke(ac23, new object[] { player, new RecievePacket(closePacket) });
        if (stallEngine.GetStall(player.CharID) != null || player.Emote != 0)
        {
            throw new Exception("FAIL: Personal stall was not closed or player emote not reset!");
        }
        Console.WriteLine("[PASS] Personal stall closed and player posture reset to 0.");

        // -------------------------------------------------------------
        // PART 2: Synthetic Vendor Spawning (:stall Chat Command)
        // -------------------------------------------------------------
        Console.WriteLine("[TEST] Part 2: Synthetic Vendor Chat Command & Player Freedom...");
        player.Emote = 0;
        socket.SentPackets.Clear();

        // Command: :stall Rare Alchemy infinite 10001:500:99 10002:150:5
        Player.HandleStallChatCommand(player, ":stall Rare Alchemy infinite 10001:500:99 10002:150:5");

        // Verify player remains mobile and standing
        if (player.Emote != 0)
        {
            throw new Exception("FAIL: Calling player emote changed to " + player.Emote + "! Calling player must remain standing (Emote = 0) and mobile!");
        }
        Console.WriteLine("[PASS] Calling player posture remained standing (Emote = 0) and free.");

        // Find the spawned synthetic stall
        var synthStall = stallEngine.GetStalls().FirstOrDefault(s => s.StallName == "Rare Alchemy");
        if (synthStall == null)
        {
            throw new Exception("FAIL: Synthetic stall 'Rare Alchemy' not found in engine!");
        }

        if (synthStall.VendorCharId == player.CharID)
        {
            throw new Exception("FAIL: Synthetic stall vendor ID collides with calling player!");
        }

        if (synthStall.VendorCharId < 8900001)
        {
            throw new Exception("FAIL: Synthetic vendor ID expected >= 8900001, got " + synthStall.VendorCharId);
        }
        Console.WriteLine("[PASS] Synthetic vendor created with non-colliding ID " + synthStall.VendorCharId + ".");

        if (!synthStall.IsInfinite)
        {
            throw new Exception("FAIL: Synthetic stall expected IsInfinite = true!");
        }
        Console.WriteLine("[PASS] Stall stock mode set to Infinite.");

        if (synthStall.Items.Count != 2)
        {
            throw new Exception("FAIL: Expected 2 catalog items, got " + synthStall.Items.Count);
        }

        var item1 = synthStall.Items[0];
        var item2 = synthStall.Items[1];
        if (item1.ItemId != 10001 || item1.PriceGold != 500 || item1.Quantity != 99 || !item1.IsInfinite)
        {
            throw new Exception("FAIL: Item 1 parsed incorrectly: ID=" + item1.ItemId + ", Price=" + item1.PriceGold + ", Qty=" + item1.Quantity + ", Infinite=" + item1.IsInfinite);
        }
        if (item2.ItemId != 10002 || item2.PriceGold != 150 || item2.Quantity != 5 || !item2.IsInfinite)
        {
            throw new Exception("FAIL: Item 2 parsed incorrectly: ID=" + item2.ItemId + ", Price=" + item2.PriceGold + ", Qty=" + item2.Quantity + ", Infinite=" + item2.IsInfinite);
        }
        Console.WriteLine("[PASS] Catalog items correctly parsed: 10001:500:99 and 10002:150:5.");

        // -------------------------------------------------------------
        // PART 3: Infinite Stock Purchase Semantics
        // -------------------------------------------------------------
        Console.WriteLine("[TEST] Part 3: Infinite Stock Purchase Semantics...");
        var buyerSocket = (TestSocket)FormatterServices.GetUninitializedObject(typeof(TestSocket));
        buyerSocket.SentPackets = new List<byte[]>();
        buyerSocket.m_IncomingPackets = new ConcurrentQueue<IPacket>();

        var buyer = new Player(buyerSocket, null);
        buyer.CharID = 88882;
        buyer.CharName = "BuyerAlice";
        buyer.Gold = 100000;

        bool buySuccess = stallEngine.PurchaseItem(buyer, synthStall.VendorCharId, 0, 20);
        if (!buySuccess)
        {
            throw new Exception("FAIL: PurchaseItem returned false for infinite stock item!");
        }

        if (buyer.Gold != 90000) // 100000 - (20 * 500) = 90000
        {
            throw new Exception("FAIL: Buyer gold expected 90000, got " + buyer.Gold);
        }

        // Verify stall item quantity is NOT reduced
        if (item1.Quantity != 99)
        {
            throw new Exception("FAIL: Infinite stock item quantity was modified to " + item1.Quantity + "! Expected 99.");
        }
        Console.WriteLine("[PASS] Infinite item purchased: Buyer charged 10,000G, stall stock remained unchanged at 99.");

        // -------------------------------------------------------------
        // PART 4: Limited Stock Purchase Semantics
        // -------------------------------------------------------------
        Console.WriteLine("[TEST] Part 4: Limited Stock Purchase Semantics...");
        var limitedItems = new List<StallItem> {
            new StallItem(0, 10003, "Limited Elixir", 5, 200, 0, false)
        };
        uint limitedVendorId = stallEngine.GenerateSyntheticVendorId();
        var limitedStall = stallEngine.SpawnCustomStall(limitedVendorId, "Limited Vendor", "Limited Market", 12000, 1500, 1600, limitedItems, false, true);

        // Buy 2 items
        bool limBuy1 = stallEngine.PurchaseItem(buyer, limitedVendorId, 0, 2);
        if (!limBuy1 || limitedStall.Items[0].Quantity != 3)
        {
            throw new Exception("FAIL: Limited item purchase did not decrease stock to 3!");
        }
        Console.WriteLine("[PASS] Limited stock decreased from 5 to 3 on partial purchase.");

        // Buy remaining 3 items -> slot should be depleted and removed
        bool limBuy2 = stallEngine.PurchaseItem(buyer, limitedVendorId, 0, 3);
        if (!limBuy2 || limitedStall.Items.Count != 0)
        {
            throw new Exception("FAIL: Depleted limited item was not removed from stall!");
        }
        Console.WriteLine("[PASS] Limited stock depleted to 0 and cleanly removed from catalog.");

        // -------------------------------------------------------------
        // PART 5: Database Persistence (Save, Query, Reload, Remove)
        // -------------------------------------------------------------
        Console.WriteLine("[TEST] Part 5: Database Persistence (SQLite & MySQL)...");
        var dt = RCLibrary.Core.DataBase.Query("SELECT vendor_id, vendor_name, stall_title, map_id, is_infinite, items_data FROM custom_stalls WHERE vendor_id = " + synthStall.VendorCharId + ";");
        if (dt == null || dt.Rows.Count == 0)
        {
            throw new Exception("FAIL: Custom stall was not found in custom_stalls database table!");
        }

        var row = dt.Rows[0];
        if (Convert.ToInt32(row["is_infinite"]) != 1 || row["stall_title"].ToString() != "Rare Alchemy")
        {
            throw new Exception("FAIL: Database row data mismatch: is_infinite=" + row["is_infinite"] + ", title=" + row["stall_title"]);
        }
        Console.WriteLine("[PASS] Custom stall verified in database table 'custom_stalls'.");

        // Test Deserialization and Reload from DB
        uint savedVendorId = synthStall.VendorCharId;
        stallEngine.RemoveCustomStall(limitedVendorId);

        // Clear memory without dropping DB
        var memoryStallsField = typeof(PlayerStallBazaarEngine).GetField("m_stalls", BindingFlags.NonPublic | BindingFlags.Instance);
        var memoryStalls = (ConcurrentDictionary<uint, PlayerStall>)memoryStallsField.GetValue(stallEngine);
        memoryStalls.Clear();

        if (stallEngine.GetStall(savedVendorId) != null)
        {
            throw new Exception("FAIL: Memory stalls failed to clear for reload test!");
        }

        // Call LoadStallsFromDatabase
        stallEngine.LoadStallsFromDatabase();
        var reloadedStall = stallEngine.GetStall(savedVendorId);
        if (reloadedStall == null || reloadedStall.StallName != "Rare Alchemy" || !reloadedStall.IsInfinite || reloadedStall.Items.Count != 2)
        {
            throw new Exception("FAIL: Stall was not correctly restored from database on server startup reload!");
        }
        Console.WriteLine("[PASS] Successfully restored stall from database with exact catalog and infinite status.");

        // -------------------------------------------------------------
        // PART 6: Removal and Cleanup Commands
        // -------------------------------------------------------------
        Console.WriteLine("[TEST] Part 6: Removal and Cleanup Commands...");
        Player.HandleRemoveStallChatCommand(player, ":removestall " + savedVendorId);
        if (stallEngine.GetStall(savedVendorId) != null)
        {
            throw new Exception("FAIL: :removestall command failed to despawn stall!");
        }

        var dtCheck = RCLibrary.Core.DataBase.Query("SELECT COUNT(*) as cnt FROM custom_stalls WHERE vendor_id = " + savedVendorId + ";");
        if (dtCheck != null && Convert.ToInt32(dtCheck.Rows[0]["cnt"]) != 0)
        {
            throw new Exception("FAIL: Stall record was not deleted from database on :removestall!");
        }
        Console.WriteLine("[PASS] :removestall removed vendor from memory and purged record from database.");

        // -------------------------------------------------------------
        // PART 7: Visual Stall Table (AC 23:31), Despawn (AC 23:32), Form Closure (AC 23:165), and Clothed Daniel
        // -------------------------------------------------------------
        Console.WriteLine("[TEST] Part 7: Visual Stall Table (AC 23:31), Despawn (AC 23:32), Form Close (AC 23:165), Clothed Daniel...");
        var testStall = new PlayerStall(null, "Test Market")
        {
            VendorCharId = 8900005,
            VendorName = "Market Vendor",
            MapId = 12000,
            X = 1200,
            Y = 1500
        };

        // SendPacket has 4-byte transport header [0xF4, 0x44, len_lo, len_hi], payload starts at offset 4
        // 1. Verify AC 23:31 (3D Table and Floating Signboard Banner)
        var mapStatePkt = PlayerStallBazaarEngine.BuildStallMapStatePacket(testStall);
        var mapStateBytes = mapStatePkt.Buffer.ToArray();
        // Wire: [0xF4, 0x44, len, len] [AC: 23 (0x17), Sub: 31 (0x1F), VendorCharId: 4B, TitleLen: 1B (11), Title: "Test Market", StallType: 1B (1)]
        if (mapStateBytes[4] != 23 || mapStateBytes[5] != 31)
        {
            throw new Exception("FAIL: BuildStallMapStatePacket opcode mismatch: AC=" + mapStateBytes[4] + ", Sub=" + mapStateBytes[5]);
        }
        uint parsedVendorId = BitConverter.ToUInt32(mapStateBytes, 6);
        if (parsedVendorId != 8900005)
        {
            throw new Exception("FAIL: BuildStallMapStatePacket vendor ID expected 8900005, got " + parsedVendorId);
        }
        byte titleLen = mapStateBytes[10];
        string titleStr = System.Text.Encoding.ASCII.GetString(mapStateBytes, 11, titleLen);
        if (titleStr != "Test Market")
        {
            throw new Exception("FAIL: BuildStallMapStatePacket title mismatch: " + titleStr);
        }
        byte stallType = mapStateBytes[11 + titleLen];
        if (stallType != 1)
        {
            throw new Exception("FAIL: BuildStallMapStatePacket stallType expected 1, got " + stallType);
        }
        Console.WriteLine("[PASS] AC 23:31 authentic wire packet verified (Vendor: " + parsedVendorId + ", Title: '" + titleStr + "', Type: " + stallType + ").");

        // 2. Verify AC 23:32 (Stall Despawn)
        var despawnPkt = PlayerStallBazaarEngine.BuildStallDespawnPacket(8900005);
        var despawnBytes = despawnPkt.Buffer.ToArray();
        if (despawnBytes[4] != 23 || despawnBytes[5] != 32 || BitConverter.ToUInt32(despawnBytes, 6) != 8900005)
        {
            throw new Exception("FAIL: BuildStallDespawnPacket mismatch!");
        }
        Console.WriteLine("[PASS] AC 23:32 authentic stall despawn wire packet verified.");

        // 3. Verify AC 23:165 (Native Form Close)
        var formClosePkt = PlayerStallBazaarEngine.BuildStallFormClosePacket(1);
        var formCloseBytes = formClosePkt.Buffer.ToArray();
        if (formCloseBytes[4] != 23 || formCloseBytes[5] != 165 || formCloseBytes[6] != 1)
        {
            throw new Exception("FAIL: BuildStallFormClosePacket mismatch: [23, 165, 1] expected, got " + BitConverter.ToString(formCloseBytes));
        }
        Console.WriteLine("[PASS] AC 23:165 1 authentic Form_PrivateShop close wire packet verified.");

        // 4. Verify Clothed Daniel Synthetic Vendor (AC 4 and AC 5:0)
        var spawnPkt = PlayerStallBazaarEngine.BuildActorSpawnPacket(testStall);
        var spawnBytes = spawnPkt.Buffer.ToArray();
        // Body at offset 9 (BodyStyle.Big_Male = 3)
        if (spawnBytes[9] != 3)
        {
            throw new Exception("FAIL: Actor body style expected 3 (Big_Male), got " + spawnBytes[9]);
        }
        // Head at offset 19 (Daniel = 0)
        if (spawnBytes[19] != 0)
        {
            throw new Exception("FAIL: Actor head style expected 0 (Daniel), got " + spawnBytes[19]);
        }
        // WornCount at offset 29 (2)
        if (spawnBytes[29] != 2)
        {
            throw new Exception("FAIL: Actor WornCount expected 2, got " + spawnBytes[29]);
        }
        ushort equip1 = BitConverter.ToUInt16(spawnBytes, 30);
        ushort equip2 = BitConverter.ToUInt16(spawnBytes, 32);
        if (equip1 != 21004 || equip2 != 24004)
        {
            throw new Exception("FAIL: Actor equips expected [21004, 24004], got [" + equip1 + ", " + equip2 + "]");
        }
        Console.WriteLine("[PASS] AC 4 synthetic vendor avatar verified as clothed Daniel (Body: 3, Head: 0, Equips: 21004, 24004).");

        var equipPkt = PlayerStallBazaarEngine.BuildActorEquipPacket(testStall);
        var equipBytes = equipPkt.Buffer.ToArray();
        if (equipBytes[4] != 5 || equipBytes[5] != 0 || BitConverter.ToUInt16(equipBytes, 10) != 21004 || BitConverter.ToUInt16(equipBytes, 12) != 24004)
        {
            throw new Exception("FAIL: BuildActorEquipPacket mismatch!");
        }
        Console.WriteLine("[PASS] AC 5:0 synthetic vendor equipment layers verified.");

        // -------------------------------------------------------------
        // PART 8: Native AC 23:32 Browse Request and AC 23:33 Shop Form & Purchase Protocol
        // -------------------------------------------------------------
        Console.WriteLine("[TEST] Part 8: Native AC 23:32 Browse and AC 23:33 Shop Form & Purchase Protocol...");

        var itemsList = PlayerStallBazaarEngine.ParseItemList("10001:120:50,10002:300:25", false);
        var activeBrowseStall = stallEngine.SpawnCustomStall(0, "Test Merchant", "Browse Test Shop", 12000, 1200, 1500, itemsList, false, false);
        if (activeBrowseStall == null) throw new Exception("FAIL: SpawnCustomStall returned null in Part 8!");

        // 1. Verify BuildStallBrowse33Packet Wire Structure
        var browse33Pkt = PlayerStallBazaarEngine.BuildStallBrowse33Packet(activeBrowseStall);
        if (browse33Pkt == null) throw new Exception("FAIL: BuildStallBrowse33Packet returned null!");
        var b33Bytes = browse33Pkt.Buffer.ToArray();

        // Length: 4B header + 18B header + (2 * 39B items) = 100 bytes
        if (b33Bytes[4] != 23 || b33Bytes[5] != 33)
        {
            throw new Exception("FAIL: BuildStallBrowse33Packet opcode expected [23, 33], got [" + b33Bytes[4] + ", " + b33Bytes[5] + "]");
        }
        uint b33VendorId = BitConverter.ToUInt32(b33Bytes, 6);
        if (b33VendorId != activeBrowseStall.VendorCharId)
        {
            throw new Exception("FAIL: BuildStallBrowse33Packet VendorCharId expected " + activeBrowseStall.VendorCharId + ", got " + b33VendorId);
        }
        uint pointsCardItem = BitConverter.ToUInt32(b33Bytes, 18);
        if (pointsCardItem != 0)
        {
            throw new Exception("FAIL: PointsCardItem expected 0 for item stall, got " + pointsCardItem);
        }
        int expectedPayloadLen = 18 + activeBrowseStall.Items.Count * 39;
        int actualPayloadLen = b33Bytes.Length - 4;
        if (actualPayloadLen != expectedPayloadLen)
        {
            throw new Exception("FAIL: AC 23:33 payload len expected " + expectedPayloadLen + ", got " + actualPayloadLen);
        }
        Console.WriteLine("[PASS] BuildStallBrowse33Packet wire format verified (" + actualPayloadLen + " bytes payload, 39B per item record).");

        // 2. Test C -> S AC 23:32 (Client clicking on stall actor)
        socket.SentPackets.Clear();
        byte[] browseReq = new byte[10];
        browseReq[0] = 0xF4; browseReq[1] = 0x44;
        browseReq[2] = 0x06; browseReq[3] = 0x00; // Len 6
        browseReq[4] = 0x17; // AC 23
        browseReq[5] = 0x20; // Sub 32 (0x20)
        Array.Copy(BitConverter.GetBytes(activeBrowseStall.VendorCharId), 0, browseReq, 6, 4);

        ac23Type.GetMethod("ProcessPkt").Invoke(ac23, new object[] { player, new RecievePacket(browseReq) });

        // Verify that socket received AC 23:33 browse packet
        bool received33 = socket.SentPackets.Any(p => p.Length >= 6 && p[4] == 23 && p[5] == 33);
        if (!received33)
        {
            throw new Exception("FAIL: Player did not receive AC 23:33 browse packet upon sending AC 23:32!");
        }
        Console.WriteLine("[PASS] C -> S AC 23:32 triggered S -> C AC 23:33 Form_BuyItemStall_1 open packet.");

        // 3. Test C -> S AC 23:33 (Client clicking Buy button inside Form_BuyItemStall_1)
        player.Gold = 50000;
        uint initialGold = player.Gold;
        byte[] buyPkt = new byte[18];
        buyPkt[0] = 0xF4; buyPkt[1] = 0x44;
        buyPkt[2] = 0x0E; buyPkt[3] = 0x00; // Len 14 (Opcode 2B + VendorId 4B + Item 8B)
        buyPkt[4] = 0x17; // AC 23
        buyPkt[5] = 0x21; // Sub 33 (0x21)
        Array.Copy(BitConverter.GetBytes(activeBrowseStall.VendorCharId), 0, buyPkt, 6, 4);
        buyPkt[10] = 0; // SlotIndex = 0
        buyPkt[11] = 2; // Quantity = 2
        Array.Copy(BitConverter.GetBytes((ushort)10001), 0, buyPkt, 12, 2); // ItemId = 10001
        Array.Copy(BitConverter.GetBytes((uint)120), 0, buyPkt, 14, 4); // UnitPrice = 120

        ac23Type.GetMethod("ProcessPkt").Invoke(ac23, new object[] { player, new RecievePacket(buyPkt) });

        if (player.Gold != initialGold - 240)
        {
            throw new Exception("FAIL: Player gold expected " + (initialGold - 240) + ", got " + player.Gold);
        }
        Console.WriteLine("[PASS] C -> S AC 23:33 purchase processed successfully (Deducted 240G for 2 items).");

        // Clean up
        stallEngine.RemoveCustomStall(activeBrowseStall.VendorCharId);

        Console.WriteLine("[SUCCESS] All persistent stall, synthetic vendor, and infinite catalog tests passed successfully!");
    }
}
"@

$dllRefs = @($refs | Where-Object { $_ -notlike '*.exe' -and (Test-Path $_) }) + @('System.Core.dll', 'System.Data.dll', 'System.Xml.dll')
Add-Type -TypeDefinition $csharpCode -ReferencedAssemblies $dllRefs
[StallVerificationSuite]::RunTests()
