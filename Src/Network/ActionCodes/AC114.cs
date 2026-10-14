using System;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 114: Combat Status Condition, Elemental Afflictions and Buff/Debuff Protocol.
    /// Handles combat crowd control (Freeze, Petrify, Sleep, Seal) and buff decay synchronization.
    /// </summary>
    public class AC114 : AC
    {
        public override int ID => 114;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC114] Combat Status Action from {c.CharName}: SubCode={subCode}");

            try
            {
                var engine = ExperimentalFeaturesManager.Instance.CombatStatus;
                switch (subCode)
                {
                    case 1: // Afflict Status (GM or Test Battle)
                        byte statusByte = p.B ?? 1;
                        byte turns = p.B ?? 3;
                        var cond = (StatusConditionType)Math.Min((byte)9, Math.Max((byte)1, statusByte));
                        engine.AfflictStatus(c, cond, turns);
                        break;
                    case 2: // Turn Decay Tick
                        engine.ProcessTurnDecay(c);
                        break;
                    case 3: // Dispel / Cleanse Status
                        byte dispelByte = p.B ?? 1;
                        var dispelCond = (StatusConditionType)Math.Min((byte)9, Math.Max((byte)1, dispelByte));
                        engine.Dispel(c, dispelCond);
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
