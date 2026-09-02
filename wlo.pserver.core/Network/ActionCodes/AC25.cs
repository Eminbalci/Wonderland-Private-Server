using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.PlayerRelated;

namespace Network.ActionCodes
{
    public class AC25 : AC
    {
        public override int ID { get { return 25; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null) return;

            byte subAction = r.B ?? 0;
            DebugSystem.Write($"[AC25] Player {p.CharName} sent Trade SubAction {subAction}");

            switch (subAction)
            {
                case 1: // Request Trade with Target
                    try
                    {
                        uint targetCharId = r.Unpack32();
                        Player target = null;
                        if (p.CurMap is GameMap curMap)
                        {
                            target = curMap.PlayersList.FirstOrDefault(x => x.CharID == targetCharId);
                        }

                        if (target != null)
                        {
                            TradeSystem.RequestTrade(p, target);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC25:1] Exception requesting trade: {ex.Message}");
                    }
                    break;

                case 2: // Trade Response (Accept / Decline / Cancel)
                    try
                    {
                        byte code = r.Unpack8();
                        if (code == 1) // Accept request
                        {
                            TradeSystem.AcceptTradeRequest(p);
                        }
                        else // Cancel / Decline
                        {
                            TradeSystem.CancelTrade(p);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC25:2] Exception responding to trade: {ex.Message}");
                    }
                    break;

                case 3: // Set Offer (Gold & Items)
                    try
                    {
                        uint gold = r.Unpack32();
                        byte count = r.Unpack8();
                        var items = new List<TradeOfferItem>();

                        for (int i = 0; i < count; i++)
                        {
                            byte slot = r.Unpack8();
                            ushort itemId = r.Unpack16();
                            byte itCount = r.Unpack8();
                            items.Add(new TradeOfferItem(slot, itemId, itCount));
                        }

                        TradeSystem.SetOffer(p, gold, items);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC25:3] Exception setting trade offer: {ex.Message}");
                    }
                    break;

                case 4: // Lock / Confirm Trade
                    try
                    {
                        TradeSystem.FinalizeTrade(p);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC25:4] Exception finalizing trade: {ex.Message}");
                    }
                    break;

                default:
                    DebugSystem.Write($"[AC25] Unhandled Trade subaction {subAction}");
                    break;
            }
        }
    }
}
