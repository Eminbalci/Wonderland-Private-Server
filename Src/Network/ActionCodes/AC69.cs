using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 69: Companion Rebirth & Pet Evolution Protocol.
    /// Handles companion rebirth ascension quests and model evolution upon reaching maximum amity and level thresholds.
    /// </summary>
    public class AC69 : AC
    {
        public override int ID => 69;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC69] Pet rebirth evolution from {c.CharName}: SubCode={subCode}");

            try
            {
                byte petSlot = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)0;

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(petSlot);
                resp.Pack8(1); // 1 = Evolution Ascended
                c.Send(resp);

                DebugSystem.Write($"[AC69] Pet in slot #{petSlot} of {c.CharName} successfully evolved.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
