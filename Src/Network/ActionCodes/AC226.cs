using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 226: Seasonal Festival Events, Track Races and Item Mall Dynamic Matrix.
    /// Handles festival track race checkpoints, speed verification, and seasonal catalogs.
    /// </summary>
    public class AC226 : AC
    {
        public override int ID => 226;

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null || r == null) return;

            byte subCode = r.B ?? 0;
            switch (subCode)
            {
                case 1:
                    Recv1(p, r); // Start Festival Race
                    break;
                case 2:
                    Recv2(p, r); // Checkpoint Crossed
                    break;
                case 255:
                    Recv255(p, r); // Item Mall Matrix Sync
                    break;
                default:
                    DebugSystem.Write($"[AC226] SubAction: {subCode} from {p.CharName}");
                    break;
            }
        }

        private void Recv1(Player p, RecievePacket r)
        {
            try
            {
                int trackId = r.Buffer.Length >= 8 ? r.Unpack16() : 1;
                if (ExperimentalFeaturesManager.Instance.EnableFestivalRaces)
                {
                    ExperimentalFeaturesManager.Instance.FestivalRaces.StartRaceForPlayer(p, trackId);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv2(Player p, RecievePacket r)
        {
            try
            {
                int trackId = r.Buffer.Length >= 8 ? r.Unpack16() : 1;
                int checkpointIndex = r.Buffer.Length >= 9 ? r.Unpack8() : 0;

                if (ExperimentalFeaturesManager.Instance.EnableFestivalRaces)
                {
                    ExperimentalFeaturesManager.Instance.FestivalRaces.PassCheckpoint(p, trackId, checkpointIndex);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv255(Player p, RecievePacket r)
        {
            try
            {
                // Authentic WLO Catalog Matrix (Packet #124, #141, #155 in itemmall.pcapng)
                SendPacket s1 = new SendPacket();
                s1.Pack8(238);
                s1.Pack8(183);
                s1.Pack8(0);
                s1.Pack8(255);
                s1.Pack16(27);
                s1.Pack8(1);
                s1.Pack16(29);
                s1.Pack8(2);
                s1.Pack16(24);
                s1.Pack8(0);
                p.Send(s1);

                // Authentic WLO Mall State / Claim Sync
                SendPacket s2 = new SendPacket();
                s2.Pack8(225);
                s2.Pack8(252);
                s2.PackArray(new byte[10]);
                p.Send(s2);

                DebugSystem.Write($"[AC226.Recv255] Dispatched Item Mall Matrix (238:183) & State (225:252) to {p.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC226.Recv255] Error: {ex.Message}");
            }
        }
    }
}
