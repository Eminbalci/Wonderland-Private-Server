using System;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 74: Teleport Scroll (verified in aLogin.exe 0x13e3d2 / 0x45b0b9).
    ///   C->S [74,1,slot,point] record current position; [74,2,slot,point] teleport.
    ///   S->C [74,1,point,map,x,y] saved point for the client world map.
    /// </summary>
    public class AC74 : AC
    {
        public override int ID => 74;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;
            try
            {
                if (p.Buffer.Length < 8) return;
                p.SetPtr(6); // header(4) + AC + sub; the sub code is p.B
                byte slot = p.Unpack8();
                byte idx = p.Unpack8();
                if (p.B == 1) TeleportScrollManager.Record(c, slot, idx);
                else if (p.B == 2) TeleportScrollManager.TeleportTo(c, slot, idx);
                else DebugSystem.Write($"[AC74] Unhandled teleport sub {p.B} from {c.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write("[AC74] Error: " + ex.Message);
            }
        }
    }
}
