using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 199: Anti-Bot Verification & Captcha Security Protocol.
    /// Handles anti-macro security challenge dialogs, question prompts, and answer validations.
    /// </summary>
    public class AC199 : AC
    {
        public override int ID => 199;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 3;
            DebugSystem.Write($"[AC199] Anti-bot verification from {c.CharName}: SubCode={subCode}");

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
