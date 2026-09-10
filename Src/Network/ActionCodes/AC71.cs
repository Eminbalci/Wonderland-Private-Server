using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 71: Carnival Arcade Minigames Protocol.
    /// Handles arcade machine interactions (Pachinko, Whack-a-Mole, High-Low Card Game) and gameplay states.
    /// </summary>
    public class AC71 : AC
    {
        public override int ID => 71;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 6;
            DebugSystem.Write($"[AC71] Arcade minigame action from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1); // 1 = Minigame Game State Active
                resp.Pack16(100); // Current Score
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
