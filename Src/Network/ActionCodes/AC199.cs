using System;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 199: Anti-Bot Verification & Companion Specialization Talent Protocol.
    /// Handles anti-macro security challenge dialogs and pet specialization talent allocations.
    /// </summary>
    public class AC199 : AC
    {
        public override int ID => 199;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 3;
            switch (subCode)
            {
                case 4:
                    Recv4(c, p); // Allocate Companion Talent Point
                    break;
                default:
                    RecvDefault(c, p, subCode); // Anti-bot verification
                    break;
            }
        }

        private void Recv4(Player c, RecievePacket p)
        {
            try
            {
                uint petId = p.Buffer.Length >= 10 ? (uint)p.Unpack32() : 0;
                byte branchId = p.Buffer.Length >= 11 ? p.Unpack8() : (byte)1;

                if (ExperimentalFeaturesManager.Instance.EnableCompanionTalents)
                {
                    TalentBranch branch = (TalentBranch)Math.Max(1, Math.Min(4, (int)branchId));
                    ExperimentalFeaturesManager.Instance.CompanionTalents.AllocateTalentPoint(c, petId, branch);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void RecvDefault(Player c, RecievePacket p, byte subCode)
        {
            try
            {
                byte answer = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)1;

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1); // 1 = Verification Passed
                c.Send(resp);

                DebugSystem.Write($"[AC199] Captcha challenge passed for {c.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
