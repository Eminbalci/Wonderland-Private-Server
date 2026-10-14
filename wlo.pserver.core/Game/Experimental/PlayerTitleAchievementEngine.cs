using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class TitleDefinition
    {
        public ushort TitleId { get; set; }
        public string TitleName { get; set; }
        public string Description { get; set; }
        public int AtkBonus { get; set; }
        public int DefBonus { get; set; }
        public int SpdBonus { get; set; }
        public int HpBonus { get; set; }

        public TitleDefinition(ushort id, string name, string desc, int atk = 0, int def = 0, int spd = 0, int hp = 0)
        {
            TitleId = id;
            TitleName = name;
            Description = desc;
            AtkBonus = atk;
            DefBonus = def;
            SpdBonus = spd;
            HpBonus = hp;
        }
    }

    public class PlayerTitleProfile
    {
        public uint CharId { get; set; }
        public string CharName { get; set; }
        public ushort EquippedTitleId { get; set; }
        public HashSet<ushort> UnlockedTitleIds { get; } = new HashSet<ushort>();

        public PlayerTitleProfile(uint charId, string name)
        {
            CharId = charId;
            CharName = name ?? "Adventurer";
            EquippedTitleId = 0;
            // Novice title unlocked by default
            UnlockedTitleIds.Add(1);
        }
    }

    /// <summary>
    /// Player Title Management, Achievement Milestones, and Prestige Honor Engine (AC 40 / Spec 59).
    /// Tracks milestone achievements, displays visual player titles above character heads, and grants passive attribute perks.
    /// </summary>
    public class PlayerTitleAchievementEngine
    {
        private readonly Dictionary<ushort, TitleDefinition> m_catalog = new Dictionary<ushort, TitleDefinition>();
        private readonly ConcurrentDictionary<uint, PlayerTitleProfile> m_profiles = new ConcurrentDictionary<uint, PlayerTitleProfile>();
        private readonly object m_titleLock = new object();

        public int CatalogCount => m_catalog.Count;
        public int TotalPlayerProfiles => m_profiles.Count;

        public PlayerTitleAchievementEngine()
        {
            // Seed canonical title definitions
            RegisterTitle(new TitleDefinition(1, "Novice Wanderer", "Begin your epic journey on Kelan Island", 0, 0, 0, 50));
            RegisterTitle(new TitleDefinition(2, "Beast Vanquisher", "Defeat 500 wilderness creatures", 15, 0, 5, 100));
            RegisterTitle(new TitleDefinition(3, "Iron Bastion", "Attain elite defensive mastery", 0, 30, 0, 250));
            RegisterTitle(new TitleDefinition(4, "Gale Strider", "Achieve exceptional agility and swift movement", 0, 0, 25, 100));
            RegisterTitle(new TitleDefinition(5, "Bazaar Merchant King", "Accumulate prominent commercial wealth", 10, 10, 10, 500));
            RegisterTitle(new TitleDefinition(6, "Grand Gladiator", "Triumph in PvP Arena tournaments", 35, 15, 15, 300));
            RegisterTitle(new TitleDefinition(7, "Celestial Ascendant", "Undergo Rebirth and awaken primal power", 40, 40, 30, 800));
        }

        public void RegisterTitle(TitleDefinition def)
        {
            if (def != null) m_catalog[def.TitleId] = def;
        }

        public IReadOnlyCollection<TitleDefinition> GetCatalog() => m_catalog.Values.ToList().AsReadOnly();

        public PlayerTitleProfile GetOrCreateProfile(Player player)
        {
            if (player == null) return null;
            return m_profiles.GetOrAdd(player.CharID, id => new PlayerTitleProfile(id, player.CharName));
        }

        public bool UnlockTitle(Player player, ushort titleId)
        {
            if (player == null || !m_catalog.TryGetValue(titleId, out var def)) return false;

            try
            {
                lock (m_titleLock)
                {
                    var profile = GetOrCreateProfile(player);
                    if (profile.UnlockedTitleIds.Contains(titleId)) return false;

                    profile.UnlockedTitleIds.Add(titleId);
                    DebugSystem.Write($"[PlayerTitleAchievementEngine] {player.CharName} unlocked Title #{titleId} '{def.TitleName}'!");

                    // S->C AC 40:6 Title Unlocked Frame
                    SendPacket p = new SendPacket();
                    p.Pack8(40);
                    p.Pack8(6);
                    p.Pack16(titleId);
                    p.PackString(def.TitleName);
                    player.Send(p);

                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerTitleAchievementEngine] UnlockTitle error: {ex.Message}");
                return false;
            }
        }

        public bool EquipTitle(Player player, ushort titleId)
        {
            if (player == null) return false;

            try
            {
                lock (m_titleLock)
                {
                    var profile = GetOrCreateProfile(player);
                    if (titleId != 0 && !profile.UnlockedTitleIds.Contains(titleId))
                    {
                        player.SendSystemMessage("You have not unlocked this title yet.");
                        return false;
                    }

                    profile.EquippedTitleId = titleId;
                    string titleName = (titleId != 0 && m_catalog.TryGetValue(titleId, out var def)) ? def.TitleName : "None";
                    DebugSystem.Write($"[PlayerTitleAchievementEngine] {player.CharName} equipped Title #{titleId} '{titleName}'");

                    // S->C AC 40:7 Equip Title ACK to player
                    SendPacket p = new SendPacket();
                    p.Pack8(40);
                    p.Pack8(7);
                    p.Pack16(titleId);
                    player.Send(p);

                    // Broadcast visual title banner to map peers
                    SendPacket bcast = new SendPacket();
                    bcast.Pack8(40);
                    bcast.Pack8(8);
                    bcast.Pack32(player.CharID);
                    bcast.Pack16(titleId);
                    player.CurMap?.Broadcast(bcast);

                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerTitleAchievementEngine] EquipTitle error: {ex.Message}");
                return false;
            }
        }
    }
}
