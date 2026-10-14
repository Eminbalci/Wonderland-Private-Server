using System;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 76: Church Charity, Tithe Donations & Karma Blessing Protocol.
    /// Handles gold tithes, accumulating character karma, and granting divine blessings.
    /// </summary>
    public class AC76 : AC
    {
        public override int ID => 76;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC76] Church charity / karma from {c.CharName}: SubCode={subCode}");

            try
            {
                if (!ExperimentalFeaturesManager.Instance.EnableChurchCharity)
                {
                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(subCode);
                    resp.Pack8(0); // Disabled
                    c.Send(resp);
                    return;
                }

                switch (subCode)
                {
                    case 1: // Query Karma Status
                        var record = ExperimentalFeaturesManager.Instance.ChurchCharity.GetKarmaRecord(c.CharID);
                        SendPacket resp1 = new SendPacket();
                        resp1.Pack8((byte)ID);
                        resp1.Pack8(1);
                        resp1.Pack32((uint)record.KarmaPoints);
                        resp1.Pack32((uint)record.TotalDonatedGold);
                        c.Send(resp1);
                        break;

                    case 2: // Tithe Gold Donation
                        int amount = 1000;
                        if (p.Buffer != null && p.Buffer.Length >= 10)
                        {
                            amount = (int)p.Unpack32();
                        }
                        var donationResult = ExperimentalFeaturesManager.Instance.ChurchCharity.Donate(c, amount);
                        SendPacket resp2 = new SendPacket();
                        resp2.Pack8((byte)ID);
                        resp2.Pack8(2);
                        resp2.Pack8(1); // 1 = Donation Accepted
                        resp2.Pack32((uint)donationResult.KarmaPoints);
                        c.Send(resp2);
                        break;

                    case 3: // Request Divine Karma Blessing
                        var blessing = ExperimentalFeaturesManager.Instance.ChurchCharity.GetBlessing(c);
                        SendPacket resp3 = new SendPacket();
                        resp3.Pack8((byte)ID);
                        resp3.Pack8(3);
                        resp3.Pack8(blessing.IsActive ? (byte)1 : (byte)0);
                        resp3.Pack16(blessing.MaxHpBonus);
                        resp3.Pack16((ushort)(blessing.ExpBonusMultiplier * 100));
                        c.Send(resp3);
                        break;

                    default:
                        SendPacket respDef = new SendPacket();
                        respDef.Pack8((byte)ID);
                        respDef.Pack8(subCode);
                        respDef.Pack8(1);
                        c.Send(respDef);
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
