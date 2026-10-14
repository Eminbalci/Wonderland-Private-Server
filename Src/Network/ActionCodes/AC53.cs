using System;
using System.Collections.Generic;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 53: Overworld Player Stall Vending & Bazaar Protocol.
    /// Handles player storefront kiosks, item sales listings, and player-to-player trade purchases.
    /// </summary>
    public class AC53 : AC
    {
        public override int ID => 53;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC53] Stall/Bazaar action from {c.CharName}: SubCode={subCode}");

            try
            {
                if (ExperimentalFeaturesManager.Instance.EnablePlayerStallBazaar)
                {
                    switch (subCode)
                    {
                        case 1: // Open Stall
                            string stallTitle = "Player Stall";
                            if (p.Buffer.Length >= 8)
                            {
                                stallTitle = p.UnpackString();
                            }

                            // Collect inventory items into stall listing
                            var stallItems = new List<StallItem>();
                            stallItems.Add(new StallItem(0, 10001, "Healing Potion", 10, 150));

                            var stall = ExperimentalFeaturesManager.Instance.PlayerStallBazaar.OpenStall(c, stallTitle, stallItems);

                            SendPacket respOpen = new SendPacket();
                            respOpen.Pack8((byte)ID);
                            respOpen.Pack8(1);
                            respOpen.Pack8(1); // 1 = Open OK
                            respOpen.PackString(stall != null ? stall.StallName : stallTitle);
                            c.Send(respOpen);
                            break;

                        case 2: // Purchase Item from Stall
                            if (p.Buffer.Length >= 10)
                            {
                                uint vendorCharId = p.Unpack32();
                                byte slotIndex = p.Unpack8();
                                int qty = p.Buffer.Length >= 12 ? p.Unpack16() : 1;

                                bool bought = ExperimentalFeaturesManager.Instance.PlayerStallBazaar.PurchaseItem(c, vendorCharId, slotIndex, qty);

                                SendPacket respBuy = new SendPacket();
                                respBuy.Pack8((byte)ID);
                                respBuy.Pack8(2);
                                respBuy.Pack8(bought ? (byte)1 : (byte)0);
                                respBuy.Pack32(vendorCharId);
                                respBuy.Pack8(slotIndex);
                                c.Send(respBuy);
                            }
                            break;

                        case 3: // Close Stall
                            ExperimentalFeaturesManager.Instance.PlayerStallBazaar.CloseStall(c);
                            break;

                        case 4: // Query Stalls
                            var stalls = ExperimentalFeaturesManager.Instance.PlayerStallBazaar.GetStalls();

                            SendPacket respQuery = new SendPacket();
                            respQuery.Pack8((byte)ID);
                            respQuery.Pack8(4);
                            respQuery.Pack16((ushort)stalls.Count);
                            c.Send(respQuery);
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
                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(subCode);
                    resp.Pack8(1);
                    c.Send(resp);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
