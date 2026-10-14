using System;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 90: Authentic Fishing & Angling Catch System Protocol.
    /// Handles casting fishing rods into water shorelines, fish biting hooks, and harvesting catches.
    /// </summary>
    public class AC90 : AC
    {
        public override int ID => 90;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC90] Fishing action from {c.CharName}: SubCode={subCode}");

            try
            {
                if (ExperimentalFeaturesManager.Instance.EnableAquacultureFishing)
                {
                    switch (subCode)
                    {
                        case 1: // Cast Fishing Rod / Start Fishing
                            ushort rodId = 26101;
                            ushort baitId = 26201;
                            if (p.Buffer.Length >= 10)
                            {
                                rodId = p.Unpack16();
                            }
                            ExperimentalFeaturesManager.Instance.AquacultureFishing.StartFishing(c, rodId, baitId);
                            break;

                        case 2: // Reel In / Catch Fish
                            ExperimentalFeaturesManager.Instance.AquacultureFishing.CatchFish(c);
                            break;

                        case 3: // Cancel / Stop Fishing
                            ExperimentalFeaturesManager.Instance.AquacultureFishing.StopFishing(c);
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
                else
                {
                    // Fallback basic handler
                    switch (subCode)
                    {
                        case 1:
                            c.Fishing = true;
                            SendPacket resp1 = new SendPacket();
                            resp1.Pack8((byte)ID);
                            resp1.Pack8(1);
                            resp1.Pack8(1);
                            c.Send(resp1);
                            break;
                        case 2:
                            c.Fishing = false;
                            ushort caughtItemId = 26001;
                            c.Inv.AddItem(caughtItemId, 1);
                            SendPacket resp2 = new SendPacket();
                            resp2.Pack8((byte)ID);
                            resp2.Pack8(2);
                            resp2.Pack16(caughtItemId);
                            resp2.Pack8(1);
                            c.Send(resp2);
                            break;
                        case 3:
                            c.Fishing = false;
                            SendPacket resp3 = new SendPacket();
                            resp3.Pack8((byte)ID);
                            resp3.Pack8(3);
                            resp3.Pack8(0);
                            c.Send(resp3);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
