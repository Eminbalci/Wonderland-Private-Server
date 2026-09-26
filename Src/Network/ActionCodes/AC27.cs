using System;
using System.Linq;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 27: Native NPC shop protocol.
    /// Handles selected-stack sales and the client sale result.
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
                // Native sale request: AC27:2, selected inventory slots, trailing sale mode.
                // The client sells complete selected stacks; the last byte is NOT a count.
                if (subCode == 2)
                {
                    byte[] data = p.Buffer.Skip(6).ToArray();
                    var result = data.Length >= 2 && data[data.Length - 1] <= 1
                        ? c.Inv.SellToNpc(data.Take(data.Length - 1).ToArray(), data[data.Length - 1])
                        : Game.Code.NpcSaleResult.Rejected;
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
