using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace Game.Battle
{
    public static class CriticalHitManager
    {
        public class Entry
        {
            public ushort item_id { get; set; }
            public int chance_percent { get; set; }
        }
        public class Configuration
        {
            public double damage_multiplier { get; set; }
            public Entry[] items { get; set; }
        }

        private static Dictionary<ushort, int> chances = new Dictionary<ushort, int>();
        private static double multiplier = 1.5;

        public static void Load(string path)
        {
            var loaded = new Dictionary<ushort, int>();
            try
            {
                var config = new JavaScriptSerializer().Deserialize<Configuration>(File.ReadAllText(path));
                if (config?.items == null || config.items.Length == 0 ||
                    double.IsNaN(config.damage_multiplier) || double.IsInfinity(config.damage_multiplier) ||
                    config.damage_multiplier < 1 || config.damage_multiplier > 10)
                    throw new InvalidDataException("Invalid critical-hit configuration");
                foreach (var entry in config.items)
                {
                    if (entry == null || entry.item_id == 0 || entry.chance_percent < 1 || entry.chance_percent > 520 || loaded.ContainsKey(entry.item_id))
                        throw new InvalidDataException("Invalid critical-hit item");
                    loaded.Add(entry.item_id, Math.Min(100, entry.chance_percent));
                }
                multiplier = config.damage_multiplier;
            }
            catch (Exception ex)
            {
                loaded.Clear();
                DebugSystem.Write("[CriticalHit] Disabled invalid configuration: " + ex.Message);
            }
            chances = loaded;
        }

        public static int GetItemChance(ushort itemId)
        {
            int chance;
            return chances.TryGetValue(itemId, out chance) ? chance : 0;
        }

        public static int GetChance(BattleFighter actor)
        {
            if (actor == null) return 0;
            // Pets use their own equipment, never their owner's equipment.
            if (actor.FighterType == BattleFighterType.Pet)
            {
                var pet = actor.PetRef;
                if (pet == null) return 0;
                return Math.Min(100, GetItemChance(pet.Eq_Head) + GetItemChance(pet.Eq_Body) +
                    GetItemChance(pet.Eq_Weapon) + GetItemChance(pet.Eq_Wrist) +
                    GetItemChance(pet.Eq_Shoes) + GetItemChance(pet.Eq_Special));
            }
            return actor.FighterType == BattleFighterType.Player ? actor.PlayerRef?.Eqs?.Crit ?? 0 : 0;
        }

        internal static int ApplyDamage(int damage, BattleFighter actor, string actionType, int roll)
        {
            bool isCritical;
            return CalculateDamage(damage, actor, actionType, roll, out isCritical);
        }

        internal static int CalculateDamage(int damage, BattleFighter actor, string actionType, int roll, out bool isCritical)
        {
            // Preserve the roll result for the packet, even if rounding or a 1x multiplier
            // makes critical damage numerically equal to an ordinary hit.
            isCritical = damage > 0 && actionType == "attack" && roll >= 0 && roll < 100 && roll < GetChance(actor);
            return isCritical ? (int)Math.Min(int.MaxValue, Math.Floor(damage * multiplier)) : damage;
        }
    }
}
