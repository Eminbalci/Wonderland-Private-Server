using System;

namespace Game.PlayerRelated
{
    public static class PotentialRule
    {
        public const int Maximum = 12;
        public const ushort PotentialPill = 34269;
        public const ushort GoldenPotentialPill = 34289;
        public const ushort SuperPotentialPill = 34350;
        public const int GoldenMinimum = 10;
        public const int ChanceScale = 100;

        private static readonly ushort[] BonusTable = { 0, 1, 2, 3, 4, 6, 9, 13, 18, 24, 31, 39, 48 };
        private static readonly int[] Chances = { 100, 100, 100, 70, 60, 55, 55, 50, 35, 20, 18, 15 };

        public static ushort Bonus(ushort level)
        {
            return BonusTable[Math.Min((int)level, Maximum)];
        }

        public static bool TrySuccessPercent(ushort itemId, ushort level, out int chance, out string error)
        {
            if (itemId != PotentialPill && itemId != GoldenPotentialPill && itemId != SuperPotentialPill)
            {
                chance = 0;
                error = "invalid potential pill or target";
                return false;
            }

            if (level >= Maximum)
            {
                chance = 0;
                error = "potential is already at its maximum";
                return false;
            }

            if (itemId == GoldenPotentialPill && level < GoldenMinimum)
            {
                chance = 0;
                error = "golden potential pills require potential level 10";
                return false;
            }

            if (itemId == SuperPotentialPill)
            {
                chance = 100;
                error = null;
                return true;
            }

            chance = Chances[level];
            error = null;
            return true;
        }

        public static bool TryNext(ushort itemId, ushort level, int roll, out ushort next, out string error)
        {
            next = level;
            if (!TrySuccessPercent(itemId, level, out int chance, out error))
            {
                return false;
            }

            if (roll < 0 || roll >= ChanceScale)
            {
                error = "invalid potential pill probability roll";
                return false;
            }

            if (roll < chance)
            {
                next = (ushort)(level + 1);
            }
            else if (itemId == PotentialPill)
            {
                next = (ushort)(level > 0 ? level - 1 : 0);
            }
            else
            {
                next = level;
            }

            error = null;
            return true;
        }
    }
}
