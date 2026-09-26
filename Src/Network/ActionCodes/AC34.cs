using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    // AC34:1 asks for the account balance before the client submits its cart.
    public class AC34 : AC
    {
        public override int ID { get { return 34; } }

        public override void ProcessPkt(Player player, RecievePacket packet)
        {
            if (packet.Buffer.Length - packet.GetPtr() != 2 || packet.Unpack8() != 1) return;
            byte mode = packet.Unpack8();
            if (mode > 1) return;

            ItemMallManager.SendPointBalance(player);
            // Native 35:4 sums these first two DWORDs, then resumes cart checkout.
            // A catalog refresh here would erase the client's pending cart.
            var reply = new SendPacket();
            reply.Pack8(35);
            reply.Pack8(4);
            reply.Pack32((uint)ItemMallManager.GetUserPoints(player));
            reply.Pack32(0);
            reply.PackArray(new byte[8]);
            player.Send(reply);
        }
    }
}
