using System;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 24: Security PIN & Trade/Stall Protection Protocol.
    /// Handles setting, verifying, and unlocking secondary security PINs for player transactions.
    /// </summary>
    public class AC24 : AC
    {
        public override int ID => 24;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 5;
            DebugSystem.Write($"[AC24] Security PIN action from {c.CharName}: SubCode={subCode}");

            try
            {
                if (ExperimentalFeaturesManager.Instance.EnableSecondaryPinVault)
                {
                    switch (subCode)
                    {
                        case 1: // Set PIN
                            string pinToSet = "123456";
                            if (p.Buffer != null && p.Buffer.Length >= 8)
                            {
                                pinToSet = p.UnpackString();
                            }
                            ExperimentalFeaturesManager.Instance.SecondaryPinVault.SetPin(c, pinToSet);
                            break;

                        case 2: // Verify PIN
                            string pinToVerify = "123456";
                            if (p.Buffer != null && p.Buffer.Length >= 8)
                            {
                                pinToVerify = p.UnpackString();
                            }
                            ExperimentalFeaturesManager.Instance.SecondaryPinVault.VerifyPin(c, pinToVerify);
                            break;

                        case 5: // Unlock Security Lock (default acknowledge)
                            SendPacket resp5 = new SendPacket();
                            resp5.Pack8((byte)ID);
                            resp5.Pack8(5);
                            resp5.Pack8(1); // 1 = Unlocked
                            c.Send(resp5);
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
                else
                {
                    // Fallback basic unlock response
                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(subCode);
                    resp.Pack8(1); // 1 = Success / Unlocked
                    c.Send(resp);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
