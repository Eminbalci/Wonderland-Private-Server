using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 87: Hot Springs, Bath Rest & Stamina Recovery Protocol.
    /// Handles soaking in hot spring waters and bath tubs to rapidly restore character HP and SP.
    /// </summary>
    public class AC87 : AC
    {
        public override int ID => 87;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC87] Hot spring bath action from {c.CharName}: SubCode={subCode}");

            try
            {
                // Restore character HP/SP
                c.Eqs.CurHP = c.Eqs.FullHP;
                c.Eqs.CurSP = c.Eqs.FullSP;

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1); // 1 = Bathing Active
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
