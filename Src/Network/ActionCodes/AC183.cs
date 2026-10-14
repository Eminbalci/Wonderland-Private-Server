using System;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 183: Ranked PvP Matchmaking, Arena Lobbies and Elo Rating Protocol.
    /// Handles competitive arena matchmaking, queue transitions, and rating synchronization.
    /// </summary>
    public class AC183 : AC
    {
        public override int ID => 183;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 0;
            switch (subCode)
            {
                case 1:
                    Recv1(c, p); // Join PvP Queue
                    break;
                case 2:
                    Recv2(c, p); // Cancel / Leave PvP Queue
                    break;
                case 17:
                    Recv17(c, p); // Heartbeat / Match status ACK
                    break;
                default:
                    DebugSystem.Write($"[AC183] Subcode {subCode} received from {c.CharName}");
                    break;
            }
        }

        private void Recv1(Player c, RecievePacket p)
        {
            try
            {
                byte mode = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)1; // Default 1v1
                if (ExperimentalFeaturesManager.Instance.EnablePvPArena)
                {
                    ExperimentalFeaturesManager.Instance.PvPArena.EnqueuePlayer(c, mode);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv2(Player c, RecievePacket p)
        {
            try
            {
                if (ExperimentalFeaturesManager.Instance.EnablePvPArena)
                {
                    ExperimentalFeaturesManager.Instance.PvPArena.DequeuePlayer(c);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv17(Player c, RecievePacket p)
        {
            try
            {
                // S->C AC 183:17 [183, 17, 0]
                SendPacket s183 = new SendPacket();
                s183.Pack8(183);
                s183.Pack8(17);
                s183.Pack8(0);
                c.Send(s183);

                // S->C AC 183:11 [183, 11, 9, 2] (Heartbeat status sync confirmed across all pcap captures)
                SendPacket s11 = new SendPacket();
                s11.Pack8(183);
                s11.Pack8(11);
                s11.Pack8(9);
                s11.Pack8(2);
                c.Send(s11);

                DebugSystem.Write($"[AC183.Recv17] Acknowledged AC 183:17 & AC 183:11 for {c.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
