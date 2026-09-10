using System;
using Game;
using Game.Battle;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 77: 12 Zodiac Palaces Challenge & Boss Trial Protocol.
    /// Handles entering the Zodiac Palace trial stages (Aries to Pisces), boss combat encounters, and rewards.
    /// </summary>
    public class AC77 : AC
    {
        public override int ID => 77;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            byte stageNumber = 1;
            if (p.Buffer.Length >= 7)
            {
                stageNumber = p.Unpack8();
            }

            DebugSystem.Write($"[AC77] 12 Palaces challenge from {c.CharName}: SubCode={subCode}, Stage={stageNumber}");

            try
            {
                bool success = PalaceTrialManager.ChallengeStage(c, stageNumber);

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(stageNumber);
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
