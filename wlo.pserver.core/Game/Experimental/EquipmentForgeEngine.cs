using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class WearReport
    {
        public byte Slot { get; set; }
        public ushort ItemId { get; set; }
        public int CurrentDurability { get; set; }
        public int MaxDurability { get; set; }
        public bool IsBroken { get; set; }

        public WearReport(byte slot, ushort id, int cur, int max)
        {
            Slot = slot;
            ItemId = id;
            CurrentDurability = cur;
            MaxDurability = max;
            IsBroken = cur <= 0;
        }
    }

    public enum ForgeOutcome
    {
        Success,
        FailureNoLoss,
        FailureDowngrade,
        Broken
    }

    public class ForgeResult
    {
        public ForgeOutcome Outcome { get; set; }
        public byte TargetGrade { get; set; }
        public byte NewTier => TargetGrade;
        public string Message { get; set; }

        public ForgeResult(ForgeOutcome outcome, byte grade, string msg)
        {
            Outcome = outcome;
            TargetGrade = grade;
            Message = msg;
        }
    }

    /// <summary>
    /// Equipment Durability Wear, Repair Hammers & Potential Forge Engine (AC 40 / Spec 17 & 40).
    /// Simulates combat equipment wear, durability restoration, and forging progression.
    /// </summary>
    public class EquipmentForgeEngine
    {
        private readonly ConcurrentDictionary<string, int> m_equipmentDurability = new ConcurrentDictionary<string, int>();
        private readonly Random m_rnd = new Random();

        public int GetDurability(uint charId, byte slot, int defaultMax = 100)
        {
            string key = $"{charId}:{slot}";
            return m_equipmentDurability.GetOrAdd(key, defaultMax);
        }

        public void SetDurability(uint charId, byte slot, int val)
        {
            string key = $"{charId}:{slot}";
            m_equipmentDurability[key] = Math.Max(0, Math.Min(100, val));
        }

        public List<WearReport> ApplyCombatWear(Player player, byte wearAmount = 1)
        {
            var reports = new List<WearReport>();
            if (player == null) return reports;

            // Slots 0..5 (Head, Body, Weapon, Shield, Shoes, Ring)
            for (byte s = 0; s < 6; s++)
            {
                int cur = GetDurability(player.CharID, s);
                if (cur > 0)
                {
                    int next = Math.Max(0, cur - wearAmount);
                    SetDurability(player.CharID, s, next);
                    reports.Add(new WearReport(s, (ushort)(20000 + s), next, 100));

                    if (next <= 0)
                    {
                        DebugSystem.Write($"[EquipmentForgeEngine] WARNING: Player {player.CharName} equipment in Slot {s} BROKE (Durability: 0)");
                        player.SendSystemMessage($"Your equipment in slot {s} is broken and needs repair!");
                    }
                }
            }
            return reports;
        }

        public bool RepairEquipment(Player player, byte slot, ushort hammerItemId = 25001)
        {
            if (player == null) return false;

            int cur = GetDurability(player.CharID, slot);
            if (cur >= 100)
            {
                player.SendSystemMessage("This equipment is already at 100% durability.");
                return false;
            }

            SetDurability(player.CharID, slot, 100);
            DebugSystem.Write($"[EquipmentForgeEngine] Player {player.CharName} repaired Slot {slot} to 100% durability with Repair Hammer #{hammerItemId}");

            // S->C AC 40:4 Repair Frame
            SendPacket p = new SendPacket();
            p.Pack8(40);
            p.Pack8(4);
            p.Pack8(slot);
            p.Pack16(100); // 100% Durability
            player.Send(p);

            return true;
        }

        public ForgeResult ForgeUpgrade(Player player, byte slot, byte currentGrade, bool useCatalyst = false)
        {
            if (player == null) return new ForgeResult(ForgeOutcome.FailureNoLoss, currentGrade, "Invalid player");

            if (currentGrade >= 10)
            {
                return new ForgeResult(ForgeOutcome.FailureNoLoss, currentGrade, "Equipment is already at maximum Forge Grade (+10)!");
            }

            // Success rates: +1 (95%) down to +10 (15%)
            int baseChance = Math.Max(15, 100 - (currentGrade * 10));
            if (useCatalyst) baseChance = Math.Min(100, baseChance + 20);

            int roll = m_rnd.Next(1, 101);
            if (roll <= baseChance)
            {
                byte newGrade = (byte)(currentGrade + 1);
                DebugSystem.Write($"[EquipmentForgeEngine] FORGE SUCCESS! {player.CharName} upgraded equipment in slot {slot} to +{newGrade} (Roll: {roll} <= {baseChance})");

                // S->C AC 40:5 Forge Success Frame
                SendPacket p = new SendPacket();
                p.Pack8(40);
                p.Pack8(5);
                p.Pack8(slot);
                p.Pack8(newGrade);
                p.Pack8(1); // 1 = Success
                player.Send(p);

                return new ForgeResult(ForgeOutcome.Success, newGrade, $"Forge Succeeded! Equipment upgraded to +{newGrade}");
            }
            else
            {
                // On high-tier failure, risk of downgrade
                bool downgrade = currentGrade >= 5 && roll > 90;
                byte newGrade = downgrade ? (byte)(currentGrade - 1) : currentGrade;

                DebugSystem.Write($"[EquipmentForgeEngine] FORGE FAILED for {player.CharName} (Roll: {roll} > {baseChance}). Grade: +{newGrade}");

                SendPacket p = new SendPacket();
                p.Pack8(40);
                p.Pack8(5);
                p.Pack8(slot);
                p.Pack8(newGrade);
                p.Pack8(0); // 0 = Failed
                player.Send(p);

                return new ForgeResult(
                    downgrade ? ForgeOutcome.FailureDowngrade : ForgeOutcome.FailureNoLoss,
                    newGrade,
                    downgrade ? $"Forge Failed! Equipment downgraded to +{newGrade}" : "Forge Failed! Materials consumed, grade preserved."
                );
            }
        }

        public ForgeResult ForgeUpgrade(Player player, byte slot, ushort catalystItemId)
        {
            byte currentGrade = 0;
            bool useCatalyst = catalystItemId > 0;
            return ForgeUpgrade(player, slot, currentGrade, useCatalyst);
        }
    }
}
