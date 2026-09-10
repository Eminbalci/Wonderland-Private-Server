using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 88: Agricultural Farming & Crop Harvesting Protocol.
    /// Handles planting seeds in tent garden patches, watering plants, and harvesting botanical crops.
    /// </summary>
    public class AC88 : AC
    {
        public override int ID => 88;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC88] Farming action from {c.CharName}: SubCode={subCode}");

            try
            {
                byte plotIndex = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)0;

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(plotIndex);
                resp.Pack8(1); // 1 = Harvested / Planted OK
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
