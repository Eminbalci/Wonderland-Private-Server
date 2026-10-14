using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public enum SiegeWarState
    {
        Idle,
        Preparation,
        ActiveWar,
        Concluded
    }

    public class SiegeTerritory
    {
        public int TerritoryId { get; set; }
        public string TerritoryName { get; set; }
        public ushort MapId { get; set; }
        public uint OccupyingGuildId { get; set; }
        public string OccupyingGuildName { get; set; }
        public int CrystalHp { get; set; }
        public int MaxCrystalHp { get; set; }
        public int TaxRatePercent { get; set; }
        public long AccumulatedTaxGold { get; set; }
        public SiegeWarState State { get; set; }
        public DateTime SiegeEndTime { get; set; }
        public ConcurrentDictionary<uint, long> GuildDamageScores { get; } = new ConcurrentDictionary<uint, long>();

        public SiegeTerritory(int id, string name, ushort mapId, int maxHp)
        {
            TerritoryId = id;
            TerritoryName = name;
            MapId = mapId;
            MaxCrystalHp = maxHp;
            CrystalHp = maxHp;
            OccupyingGuildId = 0;
            OccupyingGuildName = "Neutral Keepers";
            TaxRatePercent = 5;
            AccumulatedTaxGold = 50000;
            State = SiegeWarState.Idle;
        }
    }

    /// <summary>
    /// Guild Siege Warfare and Castle Territory Control Engine.
    /// Manages fortress crystal health, battle state transitions, territory taxes, and AC 184 vouchers.
    /// </summary>
    public class GuildSiegeEngine
    {
        private readonly List<SiegeTerritory> m_territories = new List<SiegeTerritory>();
        private readonly object m_siegeLock = new object();

        public IReadOnlyList<SiegeTerritory> Territories => m_territories.AsReadOnly();

        public GuildSiegeEngine()
        {
            m_territories.Add(new SiegeTerritory(1, "Kelan Fortress", 10001, 10000));
            m_territories.Add(new SiegeTerritory(2, "Holy City Bastion", 11001, 15000));
            m_territories.Add(new SiegeTerritory(3, "Kyoto Citadel", 12001, 20000));
        }

        public SiegeTerritory GetTerritory(int id)
        {
            return m_territories.FirstOrDefault(t => t.TerritoryId == id);
        }

        public bool StartSiege(int territoryId, int durationMinutes = 30)
        {
            lock (m_siegeLock)
            {
                var terr = GetTerritory(territoryId);
                if (terr == null) return false;

                terr.State = SiegeWarState.ActiveWar;
                terr.CrystalHp = terr.MaxCrystalHp;
                terr.SiegeEndTime = DateTime.UtcNow.AddMinutes(durationMinutes);
                terr.GuildDamageScores.Clear();

                DebugSystem.Write($"[GuildSiegeEngine] SIEGE WAR DECLARED! {terr.TerritoryName} (Map {terr.MapId}) Crystal HP: {terr.CrystalHp}. Ends at {terr.SiegeEndTime:HH:mm:ss}");
                return true;
            }
        }

        public void EndSiege(int territoryId)
        {
            lock (m_siegeLock)
            {
                var terr = GetTerritory(territoryId);
                if (terr == null || terr.State != SiegeWarState.ActiveWar) return;

                terr.State = SiegeWarState.Concluded;

                // Award territory to the guild that dealt the highest damage to the fortress crystal
                if (terr.GuildDamageScores.Count > 0)
                {
                    var winner = terr.GuildDamageScores.OrderByDescending(kvp => kvp.Value).First();
                    terr.OccupyingGuildId = winner.Key;
                    terr.OccupyingGuildName = $"Guild #{winner.Key}";
                    DebugSystem.Write($"[GuildSiegeEngine] Siege concluded! {terr.TerritoryName} claimed by {terr.OccupyingGuildName} with {winner.Value} total siege damage!");
                }
                else
                {
                    DebugSystem.Write($"[GuildSiegeEngine] Siege concluded on {terr.TerritoryName}! No attackers registered; retained by {terr.OccupyingGuildName}.");
                }
            }
        }

        public bool DamageCrystal(int territoryId, Player attacker, uint attackerGuildId, int damage)
        {
            if (attacker == null || attackerGuildId == 0) return false;

            lock (m_siegeLock)
            {
                var terr = GetTerritory(territoryId);
                if (terr == null || terr.State != SiegeWarState.ActiveWar) return false;

                // Cannot damage own crystal if defending
                if (attackerGuildId == terr.OccupyingGuildId)
                {
                    return false;
                }

                terr.CrystalHp = Math.Max(0, terr.CrystalHp - damage);
                terr.GuildDamageScores.AddOrUpdate(attackerGuildId, damage, (k, old) => old + damage);

                DebugSystem.Write($"[GuildSiegeEngine] Crystal in {terr.TerritoryName} struck for {damage} dmg by {attacker.CharName} (Guild {attackerGuildId}). HP: {terr.CrystalHp}/{terr.MaxCrystalHp}");

                if (terr.CrystalHp == 0)
                {
                    DebugSystem.Write($"[GuildSiegeEngine] CRYSTAL SHATTERED in {terr.TerritoryName}!");
                    EndSiege(territoryId);
                }
                return true;
            }
        }

        public long CollectTaxes(int territoryId, Player guildLeader, uint guildId)
        {
            if (guildLeader == null || guildId == 0) return 0;

            lock (m_siegeLock)
            {
                var terr = GetTerritory(territoryId);
                if (terr == null || terr.OccupyingGuildId != guildId) return 0;

                long gold = terr.AccumulatedTaxGold;
                terr.AccumulatedTaxGold = 0;
                DebugSystem.Write($"[GuildSiegeEngine] Guild Leader {guildLeader.CharName} collected {gold} gold in territory taxes from {terr.TerritoryName}");
                return gold;
            }
        }

        public int GetGuildBuffBonus(uint guildId)
        {
            if (guildId == 0) return 0;
            // 5% bonus stats for each territory held by guild
            int count = m_territories.Count(t => t.OccupyingGuildId == guildId);
            return count * 5;
        }
    }
}
