using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Code;
using Game.Maps.Code;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 27: Native NPC shop protocol.
    /// Handles purchasing from vendor catalogs (subcode 1) and selected-stack sales (subcode 2).
    /// </summary>
    public class AC27 : AC
    {
        public override int ID => 27;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;

            try
            {
                // Native buy request: AC27:1, shopping cart item records
                if (subCode == 1)
                {
                    byte[] data = p.Buffer.Skip(6).ToArray();
                    var cart = new List<ShoppingCart>();

                    // 1. Official client format: [count: byte], [[slot: byte], [ammt: byte]] * count
                    if (data.Length >= 3 && (data.Length - 1) % 2 == 0 && data[0] == (data.Length - 1) / 2)
                    {
                        for (int i = 1; i + 1 < data.Length; i += 2)
                        {
                            cart.Add(new ShoppingCart { slot = data[i], ammt = data[i + 1] });
                        }
                    }
                    // 2. Count-prefixed 3-byte records: [count: byte], [[itemId: ushort], [ammt: byte]] * count
                    else if (data.Length >= 4 && (data.Length - 1) % 3 == 0 && data[0] == (data.Length - 1) / 3)
                    {
                        for (int i = 1; i + 2 < data.Length; i += 3)
                        {
                            ushort id = BitConverter.ToUInt16(data, i);
                            byte ammt = data[i + 2];
                            cart.Add(new ShoppingCart { slot = id, ammt = ammt });
                        }
                    }
                    // 3. Unprefixed 3-byte records: [[itemId: ushort], [ammt: byte]] * N
                    else if (data.Length >= 3 && data.Length % 3 == 0)
                    {
                        for (int i = 0; i + 2 < data.Length; i += 3)
                        {
                            ushort id = BitConverter.ToUInt16(data, i);
                            byte ammt = data[i + 2];
                            cart.Add(new ShoppingCart { slot = id, ammt = ammt });
                        }
                    }
                    // 4. Unprefixed 2-byte records: [[slot: byte], [ammt: byte]] * N
                    else if (data.Length >= 2 && data.Length % 2 == 0)
                    {
                        for (int i = 0; i + 1 < data.Length; i += 2)
                        {
                            cart.Add(new ShoppingCart { slot = data[i], ammt = data[i + 1] });
                        }
                    }

                    var result = cart.Count > 0
                        ? c.Inv.BuyFromNpc(cart.ToArray())
                        : NpcBuyResult.Rejected;

                    DebugSystem.Write($"[AC27:1] Player {c.CharName} purchased {cart.Count} items. Result={result} (0x{(byte)result:X2})");
                    c.Send(Tools.FromFormat("bbb", 27, 1, (byte)result));
                    c.Send(Tools.FromFormat("bbb", 27, 2, (byte)result));
                    return;
                }

                // Native sale request: AC27:2, selected inventory slots, trailing sale mode.
                // The client sells complete selected stacks; the last byte is NOT a count.
                if (subCode == 2)
                {
                    byte[] data = p.Buffer.Skip(6).ToArray();
                    var result = data.Length >= 2 && data[data.Length - 1] <= 1
                        ? c.Inv.SellToNpc(data.Take(data.Length - 1).ToArray(), data[data.Length - 1])
                        : NpcSaleResult.Rejected;
                    DebugSystem.Write($"[AC27:2] Player {c.CharName} sold {data.Length - 1} slots in mode {data[data.Length - 1]}. Result={result} (0x{(byte)result:X2})");
                    // Send official client liquidation ACK (Subcode 2 at VA 0x002FE0B6)
                    c.Send(Tools.FromFormat("bbb", 27, 2, (byte)result));
                    return;
                }
                return;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
