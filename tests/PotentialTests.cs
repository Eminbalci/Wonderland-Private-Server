using System;
using Game.PlayerRelated;

internal static class PotentialTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static int Main(string[] args)
    {
        try
        {
            // Bonus tests
            Check(PotentialRule.Bonus(0) == 0, "Bonus(0) should be 0");
            Check(PotentialRule.Bonus(5) == 6, "Bonus(5) should be 6");
            Check(PotentialRule.Bonus(12) == 48, "Bonus(12) should be 48");
            Check(PotentialRule.Bonus(100) == 48, "Bonus(100) should be 48");

            // Chance tests for PotentialPill at levels 0, 3, 8, 11
            int chance;
            string error;
            Check(PotentialRule.TrySuccessPercent(PotentialRule.PotentialPill, 0, out chance, out error) && chance == 100, "PotentialPill at level 0 should have 100% chance");
            Check(PotentialRule.TrySuccessPercent(PotentialRule.PotentialPill, 3, out chance, out error) && chance == 70, "PotentialPill at level 3 should have 70% chance");
            Check(PotentialRule.TrySuccessPercent(PotentialRule.PotentialPill, 8, out chance, out error) && chance == 35, "PotentialPill at level 8 should have 35% chance");
            Check(PotentialRule.TrySuccessPercent(PotentialRule.PotentialPill, 11, out chance, out error) && chance == 15, "PotentialPill at level 11 should have 15% chance");

            // Level 12 fails with the maximum error
            Check(!PotentialRule.TrySuccessPercent(PotentialRule.PotentialPill, 12, out chance, out error) && error == "potential is already at its maximum", "Level 12 should fail with 'potential is already at its maximum'");

            // Golden pill tests
            Check(!PotentialRule.TrySuccessPercent(PotentialRule.GoldenPotentialPill, 9, out chance, out error) && error == "golden potential pills require potential level 10", "Golden at level 9 should fail with 'golden potential pills require potential level 10'");
            Check(PotentialRule.TrySuccessPercent(PotentialRule.GoldenPotentialPill, 10, out chance, out error) && chance == 18, "Golden at level 10 should give 18% chance");

            // Super pill test
            Check(PotentialRule.TrySuccessPercent(PotentialRule.SuperPotentialPill, 11, out chance, out error) && chance == 100, "Super at level 11 should give 100% chance");

            // Unknown item 1 fails
            Check(!PotentialRule.TrySuccessPercent(1, 0, out chance, out error) && error == "invalid potential pill or target", "Unknown item 1 should fail with 'invalid potential pill or target'");

            // TryNext normal pill level 5 roll 54 -> 6, roll 55 -> 4
            ushort next;
            Check(PotentialRule.TryNext(PotentialRule.PotentialPill, 5, 54, out next, out error) && next == 6, "Normal pill level 5 roll 54 should result in level 6");
            Check(PotentialRule.TryNext(PotentialRule.PotentialPill, 5, 55, out next, out error) && next == 4, "Normal pill level 5 roll 55 should result in level 4");

            // Golden level 10 roll 99 -> 10
            Check(PotentialRule.TryNext(PotentialRule.GoldenPotentialPill, 10, 99, out next, out error) && next == 10, "Golden pill level 10 roll 99 should result in level 10");

            // Super level 11 roll 99 -> 12
            Check(PotentialRule.TryNext(PotentialRule.SuperPotentialPill, 11, 99, out next, out error) && next == 12, "Super pill level 11 roll 99 should result in level 12");

            // Roll 100 and roll -1 fail
            Check(!PotentialRule.TryNext(PotentialRule.PotentialPill, 5, 100, out next, out error) && error == "invalid potential pill probability roll", "Roll 100 should fail with 'invalid potential pill probability roll'");
            Check(!PotentialRule.TryNext(PotentialRule.PotentialPill, 5, -1, out next, out error) && error == "invalid potential pill probability roll", "Roll -1 should fail with 'invalid potential pill probability roll'");

            Console.WriteLine("PotentialTests passed");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
