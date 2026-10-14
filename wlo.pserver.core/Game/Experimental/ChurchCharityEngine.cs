using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class KarmaRecord
    {
        public uint CharId { get; set; }
        public string CharName { get; set; }
        public int KarmaPoints { get; set; }
        public long TotalDonatedGold { get; set; }
        public DateTime BlessingExpiryTime { get; set; }
        public bool HasActiveBlessing => DateTime.UtcNow < BlessingExpiryTime;

        public KarmaRecord(uint id, string name)
        {
            CharId = id;
            CharName = name;
            KarmaPoints = 0;
            TotalDonatedGold = 0;
            BlessingExpiryTime = DateTime.MinValue;
        }
    }

    public class BlessingStatus
    {
        public bool IsActive { get; set; }
        public ushort MaxHpBonus { get; set; }
        public double ExpBonusMultiplier { get; set; }
        public TimeSpan RemainingTime { get; set; }

        public BlessingStatus(bool active, ushort hp, double exp, TimeSpan remaining)
        {
            IsActive = active;
            MaxHpBonus = hp;
            ExpBonusMultiplier = exp;
            RemainingTime = remaining;
        }
    }

    /// <summary>
    /// Church Charity Tithe, Divine Blessings & Karma Engine (AC 76 / Spec 39 & 76).
    /// Tracks player tithes, accumulates charitable karma, and disburses divine buffs.
    /// </summary>
    public class ChurchCharityEngine
    {
        private readonly ConcurrentDictionary<uint, KarmaRecord> m_karmaRecords = new ConcurrentDictionary<uint, KarmaRecord>();
        private long m_totalCharityVaultGold = 150000;
        private readonly object m_titheLock = new object();

        public long TotalCharityVaultGold => m_totalCharityVaultGold;
        public int TotalDonorsCount => m_karmaRecords.Count;

        public KarmaRecord GetOrAddRecord(Player player)
        {
            if (player == null) return new KarmaRecord(0, "Pilgrim");
            return m_karmaRecords.GetOrAdd(player.CharID, id => new KarmaRecord(id, player.CharName));
        }

        public KarmaRecord GetKarmaRecord(uint charId)
        {
            if (m_karmaRecords.TryGetValue(charId, out var rec))
            {
                return rec;
            }
            return new KarmaRecord(charId, "Unknown");
        }

        public IReadOnlyList<KarmaRecord> GetTopDonors()
        {
            return m_karmaRecords.Values
                .OrderByDescending(r => r.TotalDonatedGold)
                .Take(20)
                .ToList()
                .AsReadOnly();
        }

        public KarmaRecord Donate(Player player, int goldAmount)
        {
            if (player == null || goldAmount <= 0) return null;

            lock (m_titheLock)
            {
                if (player.Gold < goldAmount)
                {
                    DebugSystem.Write($"[ChurchCharityEngine] {player.CharName} has insufficient gold to donate {goldAmount:N0}G");
                    return null;
                }

                player.Gold -= (uint)goldAmount;
                m_totalCharityVaultGold += goldAmount;

                var rec = GetOrAddRecord(player);
                rec.TotalDonatedGold += goldAmount;

                // +1 Karma per 1,000G donated
                int gainedKarma = Math.Max(1, goldAmount / 1000);
                rec.KarmaPoints += gainedKarma;

                DebugSystem.Write($"[ChurchCharityEngine] {player.CharName} donated {goldAmount:N0}G (+{gainedKarma} Karma -> Total: {rec.KarmaPoints}). Vault: {m_totalCharityVaultGold:N0}G");

                // S->C AC 76:2 Donation ACK
                SendPacket p = new SendPacket();
                p.Pack8(76);
                p.Pack8(2); // Donation Success
                p.Pack32((uint)goldAmount);
                p.Pack32((uint)rec.KarmaPoints);
                p.Pack32(player.Gold);
                player.Send(p);

                return rec;
            }
        }

        public BlessingStatus GetBlessing(Player player)
        {
            if (player == null) return new BlessingStatus(false, 0, 1.0, TimeSpan.Zero);

            var rec = GetOrAddRecord(player);
            if (rec.HasActiveBlessing)
            {
                var remaining = rec.BlessingExpiryTime - DateTime.UtcNow;
                return new BlessingStatus(true, 500, 1.10, remaining);
            }

            if (rec.KarmaPoints >= 10)
            {
                rec.KarmaPoints -= 10;
                rec.BlessingExpiryTime = DateTime.UtcNow.AddMinutes(60);
                DebugSystem.Write($"[ChurchCharityEngine] Divine Blessing bestowed upon {player.CharName} for 60 minutes!");
                return new BlessingStatus(true, 500, 1.10, TimeSpan.FromMinutes(60));
            }

            return new BlessingStatus(false, 0, 1.0, TimeSpan.Zero);
        }

        public bool GrantDivineBlessing(Player player)
        {
            var blessing = GetBlessing(player);
            if (blessing.IsActive)
            {
                SendPacket p = new SendPacket();
                p.Pack8(76);
                p.Pack8(3); // Blessing Active
                p.Pack16((ushort)blessing.RemainingTime.TotalMinutes);
                p.Pack16(10); // +10% EXP boost
                p.Pack16(blessing.MaxHpBonus);
                player.Send(p);
                return true;
            }
            return false;
        }
    }
}
