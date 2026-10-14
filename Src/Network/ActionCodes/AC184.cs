using System;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 184: Promotional Gift Exchange & Guild Siege Territory Voucher Protocol.
    /// Handles redeeming special promotional event gift tokens, territory siege rewards, and castle vouchers.
    /// </summary>
    public class AC184 : AC
    {
        public override int ID => 184;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC184] Voucher exchange from {c.CharName}: SubCode={subCode}");

            try
            {
                if (ExperimentalFeaturesManager.Instance.EnableGuildSiege && subCode == 10)
                {
                    // Siege reward voucher redemption
                    var terr = ExperimentalFeaturesManager.Instance.GuildSiege.GetTerritory(1);
                    long collected = ExperimentalFeaturesManager.Instance.GuildSiege.CollectTaxes(1, c, c.GuildID);

                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(subCode);
                    resp.Pack8(1); // 1 = Exchange Successful
                    resp.Pack32((uint)collected);
                    c.Send(resp);
                    return;
                }

                // Standard promotional gift token exchange
                SendPacket standardResp = new SendPacket();
                standardResp.Pack8((byte)ID);
                standardResp.Pack8(subCode);
                standardResp.Pack8(1); // 1 = Exchange Successful
                standardResp.Pack16(23001); // Reward Item ID
                c.Send(standardResp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
