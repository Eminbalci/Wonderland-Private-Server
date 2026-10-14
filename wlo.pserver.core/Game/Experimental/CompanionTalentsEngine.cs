using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public enum TalentBranch : byte
    {
        Combat = 1,   // Increased ATK, Crit Chance
        Guardian = 2, // Increased DEF, Max HP, Damage Reduction
        Mystic = 3,   // Increased MATK, Max SP, SP Regen
        Swift = 4     // Increased SPD, Evasion
    }

    public class CompanionTalentSheet
    {
        public uint PetId { get; set; }
        public string PetName { get; set; }
        public int AvailablePoints { get; set; }
        public int CombatPoints { get; set; }
        public int GuardianPoints { get; set; }
        public int MysticPoints { get; set; }
        public int SwiftPoints { get; set; }

        public CompanionTalentSheet(uint id, string name)
        {
            PetId = id;
            PetName = name;
            AvailablePoints = 5;
            CombatPoints = 0;
            GuardianPoints = 0;
            MysticPoints = 0;
            SwiftPoints = 0;
        }
    }

    /// <summary>
    /// Companion Pet Specialization and Talent Tree Engine.
    /// Manages talent point allocation across Combat, Guardian, Mystic, and Swift paths.
    /// </summary>
    public class CompanionTalentsEngine
    {
        private readonly ConcurrentDictionary<uint, CompanionTalentSheet> m_sheets = new ConcurrentDictionary<uint, CompanionTalentSheet>();

        public CompanionTalentSheet GetOrAddSheet(uint petId, string petName)
        {
            return m_sheets.GetOrAdd(petId, id => new CompanionTalentSheet(id, petName));
        }

        public bool AllocateTalentPoint(Player player, uint petId, TalentBranch branch)
        {
            if (player == null) return false;

            var sheet = GetOrAddSheet(petId, $"Pet #{petId}");
            lock (sheet)
            {
                if (sheet.AvailablePoints <= 0) return false;

                sheet.AvailablePoints--;
                switch (branch)
                {
                    case TalentBranch.Combat: sheet.CombatPoints++; break;
                    case TalentBranch.Guardian: sheet.GuardianPoints++; break;
                    case TalentBranch.Mystic: sheet.MysticPoints++; break;
                    case TalentBranch.Swift: sheet.SwiftPoints++; break;
                }

                DebugSystem.Write($"[CompanionTalentsEngine] Pet {sheet.PetName} invested point in {branch}. Available: {sheet.AvailablePoints}");

                // S->C AC 199:4 Talent update frame
                SendPacket p = new SendPacket();
                p.Pack8(199);
                p.Pack8(4);
                p.Pack32(petId);
                p.Pack8((byte)branch);
                p.Pack8((byte)sheet.AvailablePoints);
                player.Send(p);

                return true;
            }
        }
    }
}
