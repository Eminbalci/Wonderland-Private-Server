using System;
using Game.Battle;

internal static class OverkillTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static int Main(string[] args)
    {
        try
        {
            // OverkillRule calculation checks
            Check(!OverkillRule.Exceeds(150, 100), "Exceeds(150, 100) should be false");
            Check(OverkillRule.Exceeds(151, 100), "Exceeds(151, 100) should be true");
            Check(!OverkillRule.Exceeds(1000, 0), "Exceeds(1000, 0) should be false");
            Check(!OverkillRule.Exceeds(151, 101), "Exceeds(151, 101) should be false");
            Check(OverkillRule.Exceeds(152, 101), "Exceeds(152, 101) should be true");

            // BattleFighter damage and overkill checks
            var fighter = new BattleFighter { MaxHP = 100, CurHP = 100 };
            Check(!fighter.TakeRoundDamage(100), "TakeRoundDamage(100) should return false");
            Check(!fighter.IsOverkilled, "IsOverkilled should be false after taking 100");
            Check(fighter.TakeRoundDamage(51), "Second TakeRoundDamage(51) should return true");
            Check(fighter.IsOverkilled, "IsOverkilled should be true after overkill");
            Check(fighter.CurHP == 0, "CurHP should be 0 after overkill");
            Check(!fighter.TakeRoundDamage(10), "Further TakeRoundDamage(10) should return false when already overkilled");

            // Zero and negative damage
            var fresh = new BattleFighter { MaxHP = 100, CurHP = 100 };
            Check(!fresh.TakeRoundDamage(0), "TakeRoundDamage(0) should return false");
            Check(!fresh.TakeRoundDamage(-10), "TakeRoundDamage(negative) should return false");
            Check(fresh.RoundDamage == 0, "RoundDamage should remain 0");

            // Per-round reset
            var resetFighter = new BattleFighter { MaxHP = 100, CurHP = 100 };
            Check(!resetFighter.TakeRoundDamage(100), "Initial TakeRoundDamage(100) should return false");
            resetFighter.RoundDamage = 0;
            Check(!resetFighter.TakeRoundDamage(100), "Second TakeRoundDamage(100) after reset should return false");
            Check(!resetFighter.IsOverkilled, "Fighter should not be overkilled after reset");

            // Withdrawn state reports dead
            var withdrawn = new BattleFighter { CurHP = 50, IsWithdrawn = true };
            Check(withdrawn.IsDead, "Withdrawn fighter with CurHP 50 must report IsDead true");

            Console.WriteLine("OverkillTests passed");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
