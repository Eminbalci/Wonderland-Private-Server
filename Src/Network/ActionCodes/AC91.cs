using Game;
using Game.PlayerRelated;

namespace Network.ActionCodes
{
    // Native pack-content preview. This protocol never buys or claims a reward.
    public class AC91 : AC
    {
        public override int ID { get { return 91; } }

        public override void ProcessPkt(Player player, RecievePacket packet)
        {
            if (player == null || packet == null || packet.B != 1) return;
            packet.SetPtr(6);
            if (packet.Buffer.Length - packet.GetPtr() != 3) return;
            ushort packId = packet.Unpack16();
            packet.Unpack8(); // Client cached version; return the authoritative list.
            GachaManager.SendContents(player, packId);
        }
    }
}
