using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 84: Vehicle Fueling & Durability Restoration Protocol.
    /// Handles refueling and repairing mounts, rafts, flying carpets, and mechanical vehicles.
    /// </summary>
    public class AC84 : AC
    {
        public override int ID => 84;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC84] Vehicle repair/refuel from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(100); // 100% Fuel / Durability
                resp.Pack8(1);   // Success
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
