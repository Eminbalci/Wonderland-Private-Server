using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.PlayerRelated;

namespace Network.ActionCodes
{
    public class AC56 : AC
    {
        public override int ID { get { return 56; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null) return;

            byte subAction = r.B ?? 0;
            DebugSystem.Write($"[AC56] Player {p.CharName} sent Stall SubAction {subAction}");

            switch (subAction)
            {
                case 1: // Open Stall
                    try
                    {
                        string shopTitle = r.UnpackString();
                        byte count = r.Unpack8();
                        var items = new List<StallItem>();

                        for (int i = 0; i < count; i++)
                        {
                            byte slot = r.Unpack8();
                            ushort itemId = r.Unpack16();
                            uint price = r.Unpack32();
                            byte itCount = r.Unpack8();
                            items.Add(new StallItem(slot, itemId, price, itCount));
                        }

                        StallManager.OpenStall(p, shopTitle, items);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC56:1] Exception opening stall: {ex.Message}");
                    }
                    break;

                case 2: // Close Stall
                    try
                    {
                        StallManager.CloseStall(p);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC56:2] Exception closing stall: {ex.Message}");
                    }
                    break;

                case 3: // View Stall
                    try
                    {
                        uint sellerCharId = r.Unpack32();
                        StallManager.ViewStall(p, sellerCharId);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC56:3] Exception viewing stall: {ex.Message}");
                    }
                    break;

                case 4: // Buy Item from Stall
                    try
                    {
                        uint sellerCharId = r.Unpack32();
                        byte slot = r.Unpack8();
                        byte count = r.Unpack8();
                        StallManager.BuyItem(p, sellerCharId, slot, count);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC56:4] Exception buying from stall: {ex.Message}");
                    }
                    break;

                default:
                    DebugSystem.Write($"[AC56] Unhandled Stall subaction {subAction}");
                    break;
            }
        }
    }
}
