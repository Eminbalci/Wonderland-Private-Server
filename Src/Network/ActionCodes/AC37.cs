using System;
using Game;
using Game.Crafting;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 37: Gem Socketing & Spar Forging Subsystem Protocol.
    /// Handles embedding Diamonds, Crystals, and Attribute Spars (+ATK, +DEF, +MATK, +MDEF, +SPD) into equipment.
    /// </summary>
    public class AC37 : AC
    {
        public override int ID => 37;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            byte equipSlot = 0;
            byte gemSlot = 0;

            if (p.Buffer.Length >= 8)
            {
                equipSlot = p.Unpack8();
                gemSlot = p.Unpack8();
            }

            DebugSystem.Write($"[AC37] Gem socketing request from {c.CharName}: EqSlot={equipSlot}, GemSlot={gemSlot}");

            try
            {
                bool success = ForgingManager.ForgeGem(c, equipSlot, gemSlot);

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(equipSlot);
                resp.Pack8(success ? (byte)1 : (byte)0);
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
