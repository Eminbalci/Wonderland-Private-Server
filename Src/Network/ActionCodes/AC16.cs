using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 16: Client Preferences, Settings & Privacy Toggles Protocol.
    /// Handles in-game gameplay toggles (PK Flag, Trade Lock, Team Invite Rejection, Walk/Run speed mode).
    /// </summary>
    public class AC16 : AC
    {
        public override int ID => 16;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            byte settingVal = 1;
            if (p.Buffer.Length >= 7)
            {
                settingVal = p.Unpack8();
            }

            DebugSystem.Write($"[AC16] Setting toggle from {c.CharName}: SubCode={subCode}, Value={settingVal}");

            try
            {
                switch (subCode)
                {
                    case 1: // PK Allowed / Forbidden Toggle
                        c.AllowPK = settingVal == 1;
                        break;
                    case 2: // Trade Request Lock Toggle
                        c.TradeLock = settingVal == 1;
                        break;
                    case 3: // Auto-Reject Team Requests
                        c.RejectTeam = settingVal == 1;
                        break;
                    case 4: // Walk / Run Mode Toggle
                        c.WalkMode = settingVal;
                        break;
                    default:
                        break;
                }

                // Acknowledge setting state
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(settingVal);
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
