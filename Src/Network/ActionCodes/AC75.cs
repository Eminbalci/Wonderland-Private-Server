using System;
using System.Collections.Generic;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    public class AC75 : AC
    {
        public override int ID { get { return 75; } }

        public override void ProcessPkt(Player player, RecievePacket packet)
        {
            byte sub = packet.Unpack8();
            int remaining = packet.Buffer.Length - packet.GetPtr();
            switch (sub)
            {
                case 1: // Native cart: row count, then item ID, category, quantity, order index.
                case 5: // Bonus cart uses the same row layout.
                    if (remaining < 1) return;
                    byte rows = packet.Unpack8();
                    if (rows < 1 || rows > 7 || remaining != 1 + rows * 6) return;
                    var cart = new List<MallPurchaseRequest>();
                    for (int i = 0; i < rows; i++)
                        cart.Add(new MallPurchaseRequest {
                            ItemID = packet.Unpack16(), CategoryID = packet.Unpack8(),
                            Quantity = packet.Unpack8(), OrderIndex = packet.Unpack16()
                        });
                    bool success = ItemMallManager.PurchaseCart(player, cart, sub == 5);
                    foreach (var row in cart)
                    {
                        // Native 75:4 removes exactly the confirmed row from the cart.
                        var reply = new SendPacket();
                        reply.Pack8(75); reply.Pack8(4);
                        reply.Pack16(row.ItemID); reply.Pack8(row.CategoryID);
                        reply.Pack8(row.Quantity); reply.Pack16(row.OrderIndex);
                        reply.Pack8((byte)(success ? 1 : 0));
                        player.Send(reply);
                    }
                    break;
                case 2: // Open/refresh catalog.
                    if (remaining != 0) return;
                    ItemMallManager.SendCatalog(player, false);
                    ItemMallManager.SendCatalog(player, true);
                    ItemMallManager.SendPointBalance(player);
                    break;
                case 3: // Native Forging request carries exactly one inventory slot.
                    if (remaining != 1) return;
                    MallForgingManager.Forge(player, packet.Unpack8());
                    break;
                case 4: // Minigame category switch, never an item purchase.
                    if (remaining != 1) return;
                    byte category = packet.Unpack8();
                    player.Send(Tools.FromFormat("bbbbbb", 57, 1, category, 0, 0, 0));
                    ItemMallManager.SendPointBalance(player);
                    break;
            }
        }
    }
}
