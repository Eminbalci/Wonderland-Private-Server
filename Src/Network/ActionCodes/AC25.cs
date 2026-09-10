using System;
using System.Linq;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 25: Secure Peer-to-Peer Player Trading System Protocol.
    /// Handles trade invitations, item/gold placement, mutual lock states, and atomic confirmations.
    /// </summary>
    public class AC25 : AC
    {
        public override int ID => 25;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subAction = p.B ?? 1;
            DebugSystem.Write($"[AC25] Player {c.CharName} sent Trade SubAction={subAction}");

            switch (subAction)
            {
                case 1: // Trade Invitation Request
                    HandleTradeRequest(c, p);
                    break;
                case 2: // Accept / Decline Invitation
                    HandleTradeResponse(c, p);
                    break;
                case 3: // Add / Update Item in Trade Window
                    HandleTradeItem(c, p);
                    break;
                case 4: // Set Gold in Trade Window
                    HandleTradeGold(c, p);
                    break;
                case 5: // Lock Trade Window
                    HandleLockTrade(c);
                    break;
                case 6: // Final Confirm Trade Transaction
                    HandleConfirmTrade(c);
                    break;
                case 7: // Cancel / Close Trade
                    HandleCancelTrade(c);
                    break;
                default:
                    DebugSystem.Write($"[AC25] Unhandled Trade SubAction: {subAction}");
                    break;
            }
        }

        private void HandleTradeRequest(Player c, RecievePacket p)
        {
            try
            {
                uint targetCharId = p.Unpack32();
                if (c.CurMap is GameMap curMap)
                {
                    Player target = curMap.PlayersList.FirstOrDefault(x => x.CharID == targetCharId);
                    if (target != null)
                    {
                        TradeSystem.RequestTrade(c, target);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void HandleTradeResponse(Player c, RecievePacket p)
        {
            try
            {
                byte code = p.Unpack8();
                if (code == 1)
                {
                    TradeSystem.AcceptTradeRequest(c);
                }
                else
                {
                    TradeSystem.CancelTrade(c);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void HandleTradeItem(Player c, RecievePacket p)
        {
            try
            {
                byte tradeSlot = p.Unpack8();
                byte invSlot = p.Unpack8();
                byte count = p.Unpack8();
                TradeSystem.SetTradeItem(c, tradeSlot, invSlot, count);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void HandleTradeGold(Player c, RecievePacket p)
        {
            try
            {
                uint gold = p.Unpack32();
                TradeSystem.SetTradeGold(c, gold);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void HandleLockTrade(Player c)
        {
            try
            {
                TradeSystem.LockTrade(c);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void HandleConfirmTrade(Player c)
        {
            try
            {
                TradeSystem.ConfirmTrade(c);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void HandleCancelTrade(Player c)
        {
            try
            {
                TradeSystem.CancelTrade(c);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
