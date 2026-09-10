using System;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 66: Superior Class Rebirth Advancement Protocol.
    /// Handles unlocking rebirth superior job classes (Killer, Warrior, Knight, Mage, Priest, Wit) and stat awakening.
    /// </summary>
    public class AC66 : AC
    {
        public override int ID => 66;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 11;
            DebugSystem.Write($"[AC66] Rebirth advancement action from {c.CharName}: SubCode={subCode}");

            try
            {
                byte jobClass = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)1;
                c.RebornJob = jobClass;

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(jobClass);
                resp.Pack8(1); // Success
                c.Send(resp);

                DebugSystem.Write($"[AC66] {c.CharName} awakened as Rebirth Class #{jobClass}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
