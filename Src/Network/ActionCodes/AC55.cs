using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    public class AC55 : AC
    {
        public override int ID => 55;
        public override void ProcessPkt(Player c, RecievePacket p)
        {
            // Native AC55 is a separate UI operation, not an inventory-slot reward packet.
            // Gacha uses the standard AC23 item-use path and inventory delta packets.
            DebugSystem.Write("[AC55] Unsupported request; inventory unchanged.");
        }
    }
}
