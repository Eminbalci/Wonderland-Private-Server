using System;
using Game;
using Game.Crafting;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 59: Tent Workbench Crafting & Manufacture Protocol.
    /// Handles manufacturing recipes across workbenches (Forge, Anvil, Loom, Sewing Machine, Kiln).
    /// </summary>
    public class AC59 : AC
    {
        public override int ID => 59;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC59] Tent manufacture requested by {c.CharName}: SubCode={subCode}");

            try
            {
                string bench = "Forge";
                ushort in1 = 0;
                byte c1 = 1;
                ushort in2 = 0;
                byte c2 = 1;

                if (p.Buffer.Length >= 12)
                {
                    in1 = p.Unpack16();
                    c1 = p.Unpack8();
                    in2 = p.Unpack16();
                    c2 = p.Unpack8();
                }

                bool success = TentManufactureManager.Manufacture(c, bench, in1, c1, in2, c2);

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(success ? (byte)1 : (byte)0);
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
