using System;
using Game;
using Game.Experimental;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 41: In-Game Postal Delivery & Gobang Chess Wager Protocol.
    /// Handles sending/collecting postal packages and 15x15 Gobang board game moves with ante gold wagers.
    /// </summary>
    public class AC41 : AC
    {
        public override int ID => 41;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC41] Postal/Gobang action from {c.CharName}: SubCode={subCode}");

            try
            {
                switch (subCode)
                {
                    case 1: // Check Postal Inbox
                        SendPacket resp1 = new SendPacket();
                        resp1.Pack8((byte)ID);
                        resp1.Pack8(1);
                        resp1.Pack8(0); // 0 unread parcels
                        c.Send(resp1);
                        break;

                    case 2: // Collect Parcel Item
                        SendPacket resp2 = new SendPacket();
                        resp2.Pack8((byte)ID);
                        resp2.Pack8(2);
                        resp2.Pack8(1); // Collected OK
                        c.Send(resp2);
                        break;

                    case 10: // Create Gobang Match
                        if (ExperimentalFeaturesManager.Instance.EnableGobangWager)
                        {
                            var match = ExperimentalFeaturesManager.Instance.GobangWager.CreateMatch(c, c, 1000);
                            SendPacket resp10 = new SendPacket();
                            resp10.Pack8((byte)ID);
                            resp10.Pack8(10);
                            resp10.Pack32(match.MatchId);
                            resp10.Pack8(1); // Ready
                            c.Send(resp10);
                        }
                        break;

                    case 11: // Place Gobang Stone
                        if (ExperimentalFeaturesManager.Instance.EnableGobangWager && p.Buffer.Length >= 10)
                        {
                            uint matchId = p.Unpack32();
                            int x = p.Unpack8();
                            int y = p.Unpack8();

                            bool placed = ExperimentalFeaturesManager.Instance.GobangWager.PlaceStone(matchId, c, x, y);
                            SendPacket resp11 = new SendPacket();
                            resp11.Pack8((byte)ID);
                            resp11.Pack8(11);
                            resp11.Pack32(matchId);
                            resp11.Pack8(placed ? (byte)1 : (byte)0);
                            c.Send(resp11);
                        }
                        break;

                    default:
                        SendPacket respDef = new SendPacket();
                        respDef.Pack8((byte)ID);
                        respDef.Pack8(subCode);
                        respDef.Pack8(1);
                        c.Send(respDef);
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
