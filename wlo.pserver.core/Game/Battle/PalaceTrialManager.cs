using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.Battle
{
    public class PalaceStage
    {
        public byte StageNumber { get; set; }
        public string ZodiacName { get; set; }
        public ushort GuardianNpcId { get; set; }
        public int BossHp { get; set; }
        public int BossAtk { get; set; }
        public ushort RewardChestItemId { get; set; }

        public PalaceStage(byte stage, string name, ushort npcId, int hp, int atk, ushort reward)
        {
            StageNumber = stage;
            ZodiacName = name;
            GuardianNpcId = npcId;
            BossHp = hp;
            BossAtk = atk;
            RewardChestItemId = reward;
        }
    }

    public static class PalaceTrialManager
    {
        private static readonly List<PalaceStage> _palaces = new List<PalaceStage>();

        static PalaceTrialManager()
        {
            _palaces.Add(new PalaceStage(1, "Aries Palace (Koç)", 1001, 15000, 450, 48030));
            _palaces.Add(new PalaceStage(2, "Taurus Palace (Boğa)", 1002, 22000, 520, 48030));
            _palaces.Add(new PalaceStage(3, "Gemini Palace (İkizler)", 1003, 30000, 600, 48030));
            _palaces.Add(new PalaceStage(4, "Cancer Palace (Yengeç)", 1004, 38000, 680, 48031));
            _palaces.Add(new PalaceStage(5, "Leo Palace (Aslan)", 1005, 48000, 780, 48031));
            _palaces.Add(new PalaceStage(6, "Virgo Palace (Başak)", 1006, 58000, 850, 48031));
            _palaces.Add(new PalaceStage(7, "Libra Palace (Terazi)", 1007, 70000, 950, 48032));
            _palaces.Add(new PalaceStage(8, "Scorpio Palace (Akrep)", 1008, 85000, 1050, 48032));
            _palaces.Add(new PalaceStage(9, "Sagittarius Palace (Yay)", 1009, 100000, 1200, 48032));
            _palaces.Add(new PalaceStage(10, "Capricorn Palace (Oğlak)", 1010, 120000, 1350, 48033));
            _palaces.Add(new PalaceStage(11, "Aquarius Palace (Kova)", 1011, 150000, 1500, 48033));
            _palaces.Add(new PalaceStage(12, "Pisces Palace (Balık)", 1012, 200000, 1800, 48033));
        }

        public static bool ChallengeStage(Player player, byte stage) => EnterPalaceTrial(player, stage);

        public static bool EnterPalaceTrial(Player player, byte stage)
        {
            if (player == null) return false;

            var palace = _palaces.FirstOrDefault(p => p.StageNumber == stage);
            if (palace == null)
            {
                SendSystemMsg(player, "Invalid Palace stage! Choose between Stage 1 to 12.");
                return false;
            }

            // Start Zodiac Boss Encounter
            SendPacket pAnnounce = new SendPacket();
            pAnnounce.Pack8(23);
            pAnnounce.Pack8(57);
            pAnnounce.Pack8(0);
            pAnnounce.PackString($"[12 Palaces Trial] Entered {palace.ZodiacName}! Guardian Boss awaits!");
            player.Send(pAnnounce);

            // Trigger Victory / Trial Reward
            player.Inv.AddItem(palace.RewardChestItemId, 1);
            SendSystemMsg(player, $"[Trial Victory] Cleared {palace.ZodiacName}! Received Zodiac Trial Chest #{palace.RewardChestItemId}!");

            DebugSystem.Write($"[PalaceTrial] Player {player.CharName} entered {palace.ZodiacName} (Stage {stage}).");
            return true;
        }

        private static void SendSystemMsg(Player p, string msg)
        {
            if (p == null || string.IsNullOrEmpty(msg)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(msg);
            p.Send(s);
        }
    }
}
