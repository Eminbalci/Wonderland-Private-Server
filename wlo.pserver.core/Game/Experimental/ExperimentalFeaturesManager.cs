using System;
using System.IO;
using RCLibrary.Core;

namespace Game.Experimental
{
    /// <summary>
    /// Master Experimental Features Management Controller.
    /// Coordinates all 17 bleeding-edge gameplay engines, live feature toggles, and persistence.
    /// </summary>
    public class ExperimentalFeaturesManager
    {
        private static readonly Lazy<ExperimentalFeaturesManager> s_instance =
            new Lazy<ExperimentalFeaturesManager>(() => new ExperimentalFeaturesManager());

        public static ExperimentalFeaturesManager Instance => s_instance.Value;

        // Wave 1 Sub-Engines
        public InstanceDungeonEngine InstanceDungeons { get; }
        public PvPArenaEngine PvPArena { get; }
        public GuildSiegeEngine GuildSiege { get; }
        public ArcadeMinigameEngine ArcadeMinigames { get; }
        public FestivalRaceEngine FestivalRaces { get; }
        public MapMechanismEngine MapMechanisms { get; }
        public CompanionTalentsEngine CompanionTalents { get; }

        // Wave 2 Extended Sub-Engines
        public CommunityBulletinEngine CommunityBulletin { get; }
        public ChurchCharityEngine ChurchCharity { get; }
        public EquipmentForgeEngine EquipmentForge { get; }
        public GobangWagerEngine GobangWager { get; }
        public AquacultureFishingEngine AquacultureFishing { get; }
        public HotSpringBathingEngine HotSpringBathing { get; }
        public SecondaryPinVaultEngine SecondaryPinVault { get; }
        public PlayerStallBazaarEngine PlayerStallBazaar { get; }
        public DynamicWeatherLightingEngine DynamicWeather { get; }
        public VehicleTransportEngine VehicleTransport { get; }

        // Wave 3 Sub-Engines
        public AtmBankingEngine AtmBanking { get; }
        public PlayerTitleAchievementEngine PlayerTitles { get; }
        public TreasureExcavationEngine TreasureExcavation { get; }
        public LotteryGachaEngine LotteryGacha { get; }
        public TentMaidButlerEngine TentMaidButler { get; }
        public CombatStatusConditionEngine CombatStatus { get; }

        // Live Feature Toggles (Wave 1)
        public bool EnableInstanceDungeons { get; set; } = true;
        public bool EnablePvPArena { get; set; } = true;
        public bool EnableGuildSiege { get; set; } = true;
        public bool EnableArcadeMinigames { get; set; } = true;
        public bool EnableFestivalRaces { get; set; } = true;
        public bool EnableMapMechanisms { get; set; } = true;
        public bool EnableCompanionTalents { get; set; } = true;

        // Live Feature Toggles (Wave 2)
        public bool EnableCommunityBulletin { get; set; } = true;
        public bool EnableChurchCharity { get; set; } = true;
        public bool EnableEquipmentForge { get; set; } = true;
        public bool EnableGobangWager { get; set; } = true;
        public bool EnableAquacultureFishing { get; set; } = true;
        public bool EnableHotSpringBathing { get; set; } = true;
        public bool EnableSecondaryPinVault { get; set; } = true;
        public bool EnablePlayerStallBazaar { get; set; } = true;
        public bool EnableDynamicWeather { get; set; } = true;
        public bool EnableVehicleTransport { get; set; } = true;

        // Live Feature Toggles (Wave 3)
        public bool EnableAtmBanking { get; set; } = true;
        public bool EnablePlayerTitles { get; set; } = true;
        public bool EnableTreasureExcavation { get; set; } = true;
        public bool EnableLotteryGacha { get; set; } = true;
        public bool EnableTentMaidButler { get; set; } = true;
        public bool EnableCombatStatus { get; set; } = true;

        private ExperimentalFeaturesManager()
        {
            // Initialize Wave 1
            InstanceDungeons = new InstanceDungeonEngine();
            PvPArena = new PvPArenaEngine();
            GuildSiege = new GuildSiegeEngine();
            ArcadeMinigames = new ArcadeMinigameEngine();
            FestivalRaces = new FestivalRaceEngine();
            MapMechanisms = new MapMechanismEngine();
            CompanionTalents = new CompanionTalentsEngine();

            // Initialize Wave 2
            CommunityBulletin = new CommunityBulletinEngine();
            ChurchCharity = new ChurchCharityEngine();
            EquipmentForge = new EquipmentForgeEngine();
            GobangWager = new GobangWagerEngine();
            AquacultureFishing = new AquacultureFishingEngine();
            HotSpringBathing = new HotSpringBathingEngine();
            SecondaryPinVault = new SecondaryPinVaultEngine();
            PlayerStallBazaar = new PlayerStallBazaarEngine();
            DynamicWeather = new DynamicWeatherLightingEngine();
            VehicleTransport = new VehicleTransportEngine();

            // Initialize Wave 3
            AtmBanking = new AtmBankingEngine();
            PlayerTitles = new PlayerTitleAchievementEngine();
            TreasureExcavation = new TreasureExcavationEngine();
            LotteryGacha = new LotteryGachaEngine();
            TentMaidButler = new TentMaidButlerEngine();
            CombatStatus = new CombatStatusConditionEngine();

            LoadSettings();
        }

        public void SaveSettings()
        {
            try
            {
                string path = Path.Combine(PathHelper.DataDirectory, "experimental_settings.ini");
                using (var sw = new StreamWriter(path, false))
                {
                    sw.WriteLine("[ExperimentalFeatures]");
                    // Wave 1
                    sw.WriteLine($"EnableInstanceDungeons={EnableInstanceDungeons}");
                    sw.WriteLine($"EnablePvPArena={EnablePvPArena}");
                    sw.WriteLine($"EnableGuildSiege={EnableGuildSiege}");
                    sw.WriteLine($"EnableArcadeMinigames={EnableArcadeMinigames}");
                    sw.WriteLine($"EnableFestivalRaces={EnableFestivalRaces}");
                    sw.WriteLine($"EnableMapMechanisms={EnableMapMechanisms}");
                    sw.WriteLine($"EnableCompanionTalents={EnableCompanionTalents}");

                    // Wave 2
                    sw.WriteLine($"EnableCommunityBulletin={EnableCommunityBulletin}");
                    sw.WriteLine($"EnableChurchCharity={EnableChurchCharity}");
                    sw.WriteLine($"EnableEquipmentForge={EnableEquipmentForge}");
                    sw.WriteLine($"EnableGobangWager={EnableGobangWager}");
                    sw.WriteLine($"EnableAquacultureFishing={EnableAquacultureFishing}");
                    sw.WriteLine($"EnableHotSpringBathing={EnableHotSpringBathing}");
                    sw.WriteLine($"EnableSecondaryPinVault={EnableSecondaryPinVault}");
                    sw.WriteLine($"EnablePlayerStallBazaar={EnablePlayerStallBazaar}");
                    sw.WriteLine($"EnableDynamicWeather={EnableDynamicWeather}");
                    sw.WriteLine($"EnableVehicleTransport={EnableVehicleTransport}");

                    // Wave 3
                    sw.WriteLine($"EnableAtmBanking={EnableAtmBanking}");
                    sw.WriteLine($"EnablePlayerTitles={EnablePlayerTitles}");
                    sw.WriteLine($"EnableTreasureExcavation={EnableTreasureExcavation}");
                    sw.WriteLine($"EnableLotteryGacha={EnableLotteryGacha}");
                    sw.WriteLine($"EnableTentMaidButler={EnableTentMaidButler}");
                    sw.WriteLine($"EnableCombatStatus={EnableCombatStatus}");
                }
                DebugSystem.Write("[ExperimentalFeaturesManager] Settings saved successfully to experimental_settings.ini");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ExperimentalFeaturesManager] SaveSettings error: {ex.Message}");
            }
        }

        public void LoadSettings()
        {
            try
            {
                string path = Path.Combine(PathHelper.DataDirectory, "experimental_settings.ini");
                if (!File.Exists(path)) return;

                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("[")) continue;

                    var parts = trimmed.Split('=');
                    if (parts.Length != 2) continue;

                    string key = parts[0].Trim();
                    bool val = bool.TryParse(parts[1].Trim(), out var parsed) && parsed;

                    switch (key)
                    {
                        // Wave 1
                        case "EnableInstanceDungeons": EnableInstanceDungeons = val; break;
                        case "EnablePvPArena": EnablePvPArena = val; break;
                        case "EnableGuildSiege": EnableGuildSiege = val; break;
                        case "EnableArcadeMinigames": EnableArcadeMinigames = val; break;
                        case "EnableFestivalRaces": EnableFestivalRaces = val; break;
                        case "EnableMapMechanisms": EnableMapMechanisms = val; break;
                        case "EnableCompanionTalents": EnableCompanionTalents = val; break;

                        // Wave 2
                        case "EnableCommunityBulletin": EnableCommunityBulletin = val; break;
                        case "EnableChurchCharity": EnableChurchCharity = val; break;
                        case "EnableEquipmentForge": EnableEquipmentForge = val; break;
                        case "EnableGobangWager": EnableGobangWager = val; break;
                        case "EnableAquacultureFishing": EnableAquacultureFishing = val; break;
                        case "EnableHotSpringBathing": EnableHotSpringBathing = val; break;
                        case "EnableSecondaryPinVault": EnableSecondaryPinVault = val; break;
                        case "EnablePlayerStallBazaar": EnablePlayerStallBazaar = val; break;
                        case "EnableDynamicWeather": EnableDynamicWeather = val; break;
                        case "EnableVehicleTransport": EnableVehicleTransport = val; break;

                        // Wave 3
                        case "EnableAtmBanking": EnableAtmBanking = val; break;
                        case "EnablePlayerTitles": EnablePlayerTitles = val; break;
                        case "EnableTreasureExcavation": EnableTreasureExcavation = val; break;
                        case "EnableLotteryGacha": EnableLotteryGacha = val; break;
                        case "EnableTentMaidButler": EnableTentMaidButler = val; break;
                        case "EnableCombatStatus": EnableCombatStatus = val; break;
                    }
                }
                DebugSystem.Write("[ExperimentalFeaturesManager] Settings loaded successfully from experimental_settings.ini");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ExperimentalFeaturesManager] LoadSettings error: {ex.Message}");
            }
        }
    }
}
