namespace Game.Battle
{
    /// <summary>
    /// Overkill needs round damage strictly above 150% of max HP.
    /// </summary>
    public static class OverkillRule
    {
        /// <summary>
        /// Overkill needs round damage strictly above 150% of max HP.
        /// </summary>
        public static bool Exceeds(long roundDamage, int maxHp)
        {
            return maxHp > 0 && roundDamage > (long)maxHp + maxHp / 2;
        }
    }
}
