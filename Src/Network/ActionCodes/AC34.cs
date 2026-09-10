using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 34: In-Game Shopping Cart Checkout & Points Synchronization Protocol
    /// Ported and enhanced from authentic live packet captures and client decompilation:
    /// - AC 34 Sub 1 [0]: Initial points balance query when opening cart/mall/minigame
    /// - AC 34 Sub 1 [mode >= 1]: Cart checkout for slot/row mode
    /// - AC 34 Sub 2: Direct Item Mall Purchase [34, 2, ItemID(uint16), Quantity(uint8)]
    /// </summary>
    public class AC34 : AC
    {
        public override int ID { get { return 34; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            byte subcode = p.Unpack8();
            switch (subcode)
            {
                case 1:
                    Recv1(r, p);
                    break;
                case 2:
                    Recv2(r, p);
                    break;
                default:
                    DebugSystem.Write($"[AC34] Subcode {subcode} not handled");
                    break;
            }
        }

        void Recv1(Player p, RecievePacket r)
        {
            try
            {
                byte mode = 0;
                try { mode = r.Unpack8(); } catch { mode = 0; }

                uint points = (uint)ItemMallManager.GetUserPoints(p);

                if (mode == 0)
                {
                    // Balance query on Mall open / Minigame open
                    SendPacket resp = new SendPacket();
                    resp.Pack8(34);
                    resp.Pack8(1);
                    resp.Pack32(points);
                    p.Send(resp);

                    ItemMallManager.SendPointBalance(p);
                    ItemMallManager.SendCatalog(p, isBonus: false);
                    DebugSystem.Write($"[AC34.Recv1] Synced IM points ({points} IM) and sent catalog for {p.CharName}");
                    return;
                }

                // mode >= 1: Shopping Cart Checkout (Confirm button in Form_Cart)
                var catalog = ItemMallManager.GetCatalog(isBonus: false);
                MallItemEntry itemToBuy = null;

                int catIndex = mode - 1;
                if (catIndex >= 0 && catIndex < catalog.Count)
                {
                    itemToBuy = catalog[catIndex];
                }
                else if (catalog.Count > 0)
                {
                    itemToBuy = catalog[0];
                }

                if (itemToBuy != null)
                {
                    bool success = ItemMallManager.PurchaseItem(p, itemToBuy.ItemID, 1, isBonus: false);
                    uint remPoints = (uint)ItemMallManager.GetUserPoints(p);

                    // 1. S->C AC 34 Sub 1: [RemainingPoints(4B)] -> Triggers client banner "WLO Point Remain: %04d Pts"
                    SendPacket resp = new SendPacket();
                    resp.Pack8(34);
                    resp.Pack8(1);
                    resp.Pack32(remPoints);
                    p.Send(resp);

                    // 2. S->C AC 75 Sub 3: 13-byte dual balance sync
                    ItemMallManager.SendPointBalance(p);

                    // 3. S->C AC 35 Sub 4: [16 zero bytes] -> Authentic pcap packet #142 Cart Purchase confirmation
                    SendPacket pCart = new SendPacket();
                    pCart.Pack8(35);
                    pCart.Pack8(4);
                    for (int i = 0; i < 16; i++) pCart.Pack8(0);
                    p.Send(pCart);

                    // 4. Send updated catalog (AC 75 Sub 1)
                    ItemMallManager.SendCatalog(p, isBonus: false);

                    DebugSystem.Write($"[AC34.Recv1] Cart Checkout Slot #{mode} -> {itemToBuy.ItemName} (#{itemToBuy.ItemID}) by {p.CharName}: {(success ? "Success" : "Failed")}");
                }
                else
                {
                    DebugSystem.Write($"[AC34.Recv1] No item found for Cart Slot #{mode}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC34.Recv1] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void Recv2(Player p, RecievePacket r)
        {
            try
            {
                int rem = r.Buffer.Length - r.GetPtr();
                if (rem >= 2)
                {
                    ushort itemId = r.Unpack16();
                    byte quantity = 1;
                    if (r.Buffer.Length - r.GetPtr() >= 1)
                    {
                        try { quantity = r.Unpack8(); } catch { quantity = 1; }
                    }
                    if (quantity <= 0) quantity = 1;

                    DebugSystem.Write($"[AC34.Recv2] Direct Mall Purchase #{itemId} x{quantity} by {p.CharName}");
                    bool success = ItemMallManager.PurchaseItem(p, itemId, quantity, isBonus: false);
                    uint remPoints = (uint)ItemMallManager.GetUserPoints(p);

                    // Sync points (AC 34:1 and AC 75:3)
                    SendPacket resp = new SendPacket();
                    resp.Pack8(34);
                    resp.Pack8(1);
                    resp.Pack32(remPoints);
                    p.Send(resp);

                    ItemMallManager.SendPointBalance(p);

                    // Cart clearance ACK
                    SendPacket pCart = new SendPacket();
                    pCart.Pack8(35);
                    pCart.Pack8(4);
                    for (int i = 0; i < 16; i++) pCart.Pack8(0);
                    p.Send(pCart);

                    ItemMallManager.SendCatalog(p, isBonus: false);
                }
                else
                {
                    DebugSystem.Write($"[AC34.Recv2] Malformed AC 34 Sub 2 packet from {p.CharName}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC34.Recv2] Error: {ex.Message}");
            }
        }
    }
}
