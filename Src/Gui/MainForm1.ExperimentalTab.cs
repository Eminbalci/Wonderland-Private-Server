using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Game;
using Game.Experimental;

namespace Wonderland_Private_Server
{
    public partial class Form1
    {
        private TabPage tabPageExperimental;
        private Label ext_lblExpMetricDungeons;
        private Label ext_lblExpMetricArena;
        private Label ext_lblExpMetricSiege;
        private Label ext_lblExpMetricMinigames;
        private Label ext_lblExpMetricBazaar;
        private Label ext_lblExpMetricWeather;

        // Wave 1 Toggles
        private CheckBox chkEnableDungeons;
        private CheckBox chkEnablePvPArena;
        private CheckBox chkEnableGuildSiege;
        private CheckBox chkEnableArcadeMinigames;
        private CheckBox chkEnableFestivalRaces;
        private CheckBox chkEnableMapMechanisms;
        private CheckBox chkEnableCompanionTalents;

        // Wave 2 Toggles
        private CheckBox chkEnableBulletin;
        private CheckBox chkEnableCharity;
        private CheckBox chkEnableForge;
        private CheckBox chkEnableGobang;
        private CheckBox chkEnableFishing;
        private CheckBox chkEnableHotSpring;
        private CheckBox chkEnablePinVault;
        private CheckBox chkEnableBazaar;
        private CheckBox chkEnableWeather;
        private CheckBox chkEnableVehicles;

        // Wave 3 Checkboxes
        private CheckBox chkEnableAtmBanking;
        private CheckBox chkEnablePlayerTitles;
        private CheckBox chkEnableTreasureExcavation;
        private CheckBox chkEnableLotteryGacha;
        private CheckBox chkEnableTentMaidButler;
        private CheckBox chkEnableCombatStatus;

        // Wave 1 Grids & Controls
        private DataGridView dgvInstances;
        private DataGridView dgvPvPQueue;
        private DataGridView dgvPvPLeaderboard;
        private DataGridView dgvArcadeSessions;
        private DataGridView dgvRaceTracks;
        private ProgressBar prgKelanCrystal;
        private ProgressBar prgHolyCrystal;
        private ProgressBar prgKyotoCrystal;
        private Label lblKelanInfo;
        private Label lblHolyInfo;
        private Label lblKyotoInfo;

        // Wave 2 Grids & Controls
        private DataGridView dgvBulletinPosts;
        private DataGridView dgvCharityLeaderboard;
        private Label lblTitheVaultTotal;
        private Label lblForgeOutcome;
        private DataGridView dgvGobangMatches;
        private DataGridView dgvFishingAnglers;
        private DataGridView dgvHotSpringBathers;
        private DataGridView dgvPinVaults;
        private DataGridView dgvBazaarStalls;
        private Label lblWeatherTimeBadge;
        private DataGridView dgvWeatherMaps;
        private ComboBox cmbWeatherSelect;
        private NumericUpDown numWeatherMapId;
        private DataGridView dgvVehicleFleet;
        private DataGridView dgvActiveMounts;

        // Wave 3 Grids & Metric Badges
        private DataGridView dgvBankAccounts;
        private DataGridView dgvTitleCatalog;
        private DataGridView dgvExcavationFinds;
        private DataGridView dgvLotterySpins;
        private DataGridView dgvMaidAssistants;
        private DataGridView dgvCombatStatusEffects;
        private Label ext_lblExpMetricBank;
        private Label ext_lblExpMetricLottery;

        public void SetupExperimentalTab()
        {
            try
            {
                tabPageExperimental = new TabPage("🧪 Experimental");
                tabPageExperimental.BackColor = Color.White;

                // 1. Executive Top Header Panel
                Panel headerPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 62,
                    BackColor = ModernTheme.Slate900,
                    Padding = new Padding(14, 8, 14, 8)
                };

                Label lblTitle = new Label
                {
                    Text = "EXPERIMENTAL FEATURES LAB",
                    Font = ModernTheme.FontHeading,
                    ForeColor = Color.White,
                    Location = new Point(12, 10),
                    AutoSize = true
                };

                Label lblSubtitle = new Label
                {
                    Text = "Cockpit for 23 Advanced Engines: AC 85, 183, 184, 57, 226, 82, 76, 40, 41, 90, 44, 24, 53, 88, 84/86, 26, 62, 114/115 & Eve Opcodes 16/17",
                    Font = ModernTheme.FontSmall,
                    ForeColor = ModernTheme.Slate400,
                    Location = new Point(14, 34),
                    AutoSize = true
                };

                // Metrics Panel on Right
                FlowLayoutPanel metricsFlow = new FlowLayoutPanel
                {
                    Dock = DockStyle.Right,
                    FlowDirection = FlowDirection.LeftToRight,
                    Width = 980,
                    BackColor = Color.Transparent,
                    Padding = new Padding(0, 12, 0, 0)
                };

                ext_lblExpMetricDungeons = CreateHeaderMetricBadge("Dungeons: 0", ModernTheme.Blue600);
                ext_lblExpMetricArena = CreateHeaderMetricBadge("PvP Queue: 0", ModernTheme.Emerald600);
                ext_lblExpMetricSiege = CreateHeaderMetricBadge("Siege: Idle", ModernTheme.Amber600);
                ext_lblExpMetricMinigames = CreateHeaderMetricBadge("Arcade: 0", ModernTheme.Rose600);
                ext_lblExpMetricBazaar = CreateHeaderMetricBadge("Bazaar: 0", ModernTheme.Blue600);
                ext_lblExpMetricWeather = CreateHeaderMetricBadge("Time: Noon", ModernTheme.Slate700);
                ext_lblExpMetricBank = CreateHeaderMetricBadge("Vault: 0G", ModernTheme.Emerald600);
                ext_lblExpMetricLottery = CreateHeaderMetricBadge("Jackpot: 250k", ModernTheme.Amber600);

                Button btnRefreshAll = new Button
                {
                    Text = "Refresh All",
                    Size = new Size(95, 26),
                    Font = ModernTheme.FontBoldSmall,
                    BackColor = ModernTheme.Slate700,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(8, 0, 0, 0)
                };
                btnRefreshAll.FlatAppearance.BorderSize = 0;
                btnRefreshAll.Click += (s, e) => RefreshExperimentalTab();

                metricsFlow.Controls.Add(ext_lblExpMetricDungeons);
                metricsFlow.Controls.Add(ext_lblExpMetricArena);
                metricsFlow.Controls.Add(ext_lblExpMetricSiege);
                metricsFlow.Controls.Add(ext_lblExpMetricMinigames);
                metricsFlow.Controls.Add(ext_lblExpMetricBazaar);
                metricsFlow.Controls.Add(ext_lblExpMetricWeather);
                metricsFlow.Controls.Add(ext_lblExpMetricBank);
                metricsFlow.Controls.Add(ext_lblExpMetricLottery);
                metricsFlow.Controls.Add(btnRefreshAll);

                headerPanel.Controls.Add(lblTitle);
                headerPanel.Controls.Add(lblSubtitle);
                headerPanel.Controls.Add(metricsFlow);

                // 2. Multi-Module TabControl Host
                TabControl expTabControl = new TabControl
                {
                    Dock = DockStyle.Fill,
                    Font = ModernTheme.FontDefault
                };

                // Sub-Tabs Wave 1
                expTabControl.TabPages.Add(BuildTogglesSubTab());
                expTabControl.TabPages.Add(BuildDungeonsSubTab());
                expTabControl.TabPages.Add(BuildArenaSubTab());
                expTabControl.TabPages.Add(BuildSiegeSubTab());
                expTabControl.TabPages.Add(BuildArcadeSubTab());
                expTabControl.TabPages.Add(BuildRacesSubTab());

                // Sub-Tabs Wave 2
                expTabControl.TabPages.Add(BuildBulletinSubTab());
                expTabControl.TabPages.Add(BuildCharitySubTab());
                expTabControl.TabPages.Add(BuildForgeSubTab());
                expTabControl.TabPages.Add(BuildGobangSubTab());
                expTabControl.TabPages.Add(BuildFishingSubTab());
                expTabControl.TabPages.Add(BuildHotSpringSubTab());
                expTabControl.TabPages.Add(BuildPinVaultSubTab());
                expTabControl.TabPages.Add(BuildBazaarSubTab());
                expTabControl.TabPages.Add(BuildWeatherSubTab());
                expTabControl.TabPages.Add(BuildVehiclesSubTab());

                // Sub-Tabs Wave 3
                expTabControl.TabPages.Add(BuildBankingSubTab());
                expTabControl.TabPages.Add(BuildTitlesSubTab());
                expTabControl.TabPages.Add(BuildExcavationSubTab());
                expTabControl.TabPages.Add(BuildLotterySubTab());
                expTabControl.TabPages.Add(BuildMaidSubTab());
                expTabControl.TabPages.Add(BuildCombatStatusSubTab());

                tabPageExperimental.Controls.Add(expTabControl);
                tabPageExperimental.Controls.Add(headerPanel);

                // Add to main tab host
                if (this.tabControl3 != null && !this.tabControl3.TabPages.Contains(tabPageExperimental))
                {
                    this.tabControl3.TabPages.Add(tabPageExperimental);
                }

                RefreshExperimentalTab();
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private Label CreateHeaderMetricBadge(string text, Color bg)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                BackColor = bg,
                ForeColor = Color.White,
                Font = ModernTheme.FontBoldSmall,
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(4, 0, 4, 0)
            };
        }

        #region Sub-Tab 1: Master Toggles & Config
        private TabPage BuildTogglesSubTab()
        {
            TabPage page = new TabPage("Master Toggles");
            page.BackColor = Color.White;
            page.Padding = new Padding(16);

            Panel pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            Label lblHeading = new Label
            {
                Text = "Feature Activation Switches (23 Engines)",
                Font = ModernTheme.FontHeading,
                ForeColor = ModernTheme.Slate900,
                Location = new Point(10, 10),
                AutoSize = true
            };

            Label lblDesc = new Label
            {
                Text = "Enable or disable experimental features on-the-fly. Changes take effect instantly without restarting the server.",
                Font = ModernTheme.FontDefault,
                ForeColor = ModernTheme.Slate500,
                Location = new Point(12, 35),
                AutoSize = true
            };

            int top = 70;
            var mgr = ExperimentalFeaturesManager.Instance;

            // Wave 1
            chkEnableDungeons = CreateToggleCheckbox("Instance Dungeons & Wave Stages (AC 85 & Eve Opcode 17)", mgr.EnableInstanceDungeons, top);
            chkEnableDungeons.CheckedChanged += (s, e) => mgr.EnableInstanceDungeons = chkEnableDungeons.Checked;
            top += 30;

            chkEnablePvPArena = CreateToggleCheckbox("Ranked PvP Matchmaking & Elo Arena (AC 183 & AC 186)", mgr.EnablePvPArena, top);
            chkEnablePvPArena.CheckedChanged += (s, e) => mgr.EnablePvPArena = chkEnablePvPArena.Checked;
            top += 30;

            chkEnableGuildSiege = CreateToggleCheckbox("Guild Siege Warfare & Territory Crystals (AC 184 & AC 92)", mgr.EnableGuildSiege, top);
            chkEnableGuildSiege.CheckedChanged += (s, e) => mgr.EnableGuildSiege = chkEnableGuildSiege.Checked;
            top += 30;

            chkEnableArcadeMinigames = CreateToggleCheckbox("Carnival Arcade Minigames & Scoreboards (AC 57)", mgr.EnableArcadeMinigames, top);
            chkEnableArcadeMinigames.CheckedChanged += (s, e) => mgr.EnableArcadeMinigames = chkEnableArcadeMinigames.Checked;
            top += 30;

            chkEnableFestivalRaces = CreateToggleCheckbox("Seasonal Festival Races & Track Checkpoints (AC 226)", mgr.EnableFestivalRaces, top);
            chkEnableFestivalRaces.CheckedChanged += (s, e) => mgr.EnableFestivalRaces = chkEnableFestivalRaces.Checked;
            top += 30;

            chkEnableMapMechanisms = CreateToggleCheckbox("Dungeon Lever Switches & Puzzle Doors (Eve Opcode 16)", mgr.EnableMapMechanisms, top);
            chkEnableMapMechanisms.CheckedChanged += (s, e) => mgr.EnableMapMechanisms = chkEnableMapMechanisms.Checked;
            top += 30;

            chkEnableCompanionTalents = CreateToggleCheckbox("Companion Pet Specialization Trees (AC 199)", mgr.EnableCompanionTalents, top);
            chkEnableCompanionTalents.CheckedChanged += (s, e) => mgr.EnableCompanionTalents = chkEnableCompanionTalents.Checked;
            top += 30;

            // Wave 2
            chkEnableBulletin = CreateToggleCheckbox("Community Bulletin & Guild Notices (AC 82)", mgr.EnableCommunityBulletin, top);
            chkEnableBulletin.CheckedChanged += (s, e) => mgr.EnableCommunityBulletin = chkEnableBulletin.Checked;
            top += 30;

            chkEnableCharity = CreateToggleCheckbox("Church Charity Tithes & Divine Karma (AC 76)", mgr.EnableChurchCharity, top);
            chkEnableCharity.CheckedChanged += (s, e) => mgr.EnableChurchCharity = chkEnableCharity.Checked;
            top += 30;

            chkEnableForge = CreateToggleCheckbox("Equipment Durability Wear & Forge (AC 40)", mgr.EnableEquipmentForge, top);
            chkEnableForge.CheckedChanged += (s, e) => mgr.EnableEquipmentForge = chkEnableForge.Checked;
            top += 30;

            chkEnableGobang = CreateToggleCheckbox("Gobang (Gomoku) 15x15 Ante Wagers (AC 41)", mgr.EnableGobangWager, top);
            chkEnableGobang.CheckedChanged += (s, e) => mgr.EnableGobangWager = chkEnableGobang.Checked;
            top += 30;

            chkEnableFishing = CreateToggleCheckbox("Aquaculture Dynamics & Catch Tables (AC 90)", mgr.EnableAquacultureFishing, top);
            chkEnableFishing.CheckedChanged += (s, e) => mgr.EnableAquacultureFishing = chkEnableFishing.Checked;
            top += 30;

            chkEnableHotSpring = CreateToggleCheckbox("Hot Spring Relaxation & Rest Vitality (AC 44)", mgr.EnableHotSpringBathing, top);
            chkEnableHotSpring.CheckedChanged += (s, e) => mgr.EnableHotSpringBathing = chkEnableHotSpring.Checked;
            top += 30;

            chkEnablePinVault = CreateToggleCheckbox("Secondary PIN Security Vault (AC 24)", mgr.EnableSecondaryPinVault, top);
            chkEnablePinVault.CheckedChanged += (s, e) => mgr.EnableSecondaryPinVault = chkEnablePinVault.Checked;
            top += 30;

            chkEnableBazaar = CreateToggleCheckbox("Overworld Player Stall Bazaar (AC 53 / AC 94)", mgr.EnablePlayerStallBazaar, top);
            chkEnableBazaar.CheckedChanged += (s, e) => mgr.EnablePlayerStallBazaar = chkEnableBazaar.Checked;
            top += 30;

            chkEnableWeather = CreateToggleCheckbox("Dynamic Weather & Lighting Cycles (AC 88)", mgr.EnableDynamicWeather, top);
            chkEnableWeather.CheckedChanged += (s, e) => mgr.EnableDynamicWeather = chkEnableWeather.Checked;
            top += 30;

            chkEnableVehicles = CreateToggleCheckbox("Vehicle Transport Fleet & Fuel (AC 84 / AC 86)", mgr.EnableVehicleTransport, top);
            chkEnableVehicles.CheckedChanged += (s, e) => mgr.EnableVehicleTransport = chkEnableVehicles.Checked;
            top += 30;

            // Wave 3
            chkEnableAtmBanking = CreateToggleCheckbox("ATM Banking & Vault Gold Transfers (AC 26)", mgr.EnableAtmBanking, top);
            chkEnableAtmBanking.CheckedChanged += (s, e) => mgr.EnableAtmBanking = chkEnableAtmBanking.Checked;
            top += 30;

            chkEnablePlayerTitles = CreateToggleCheckbox("Player Titles & Achievement Perks (AC 40)", mgr.EnablePlayerTitles, top);
            chkEnablePlayerTitles.CheckedChanged += (s, e) => mgr.EnablePlayerTitles = chkEnablePlayerTitles.Checked;
            top += 30;

            chkEnableTreasureExcavation = CreateToggleCheckbox("Underground Geological Mining & Digging (AC 90)", mgr.EnableTreasureExcavation, top);
            chkEnableTreasureExcavation.CheckedChanged += (s, e) => mgr.EnableTreasureExcavation = chkEnableTreasureExcavation.Checked;
            top += 30;

            chkEnableLotteryGacha = CreateToggleCheckbox("Carnival Lottery Wheel & Gacha Capsules (AC 76)", mgr.EnableLotteryGacha, top);
            chkEnableLotteryGacha.CheckedChanged += (s, e) => mgr.EnableLotteryGacha = chkEnableLotteryGacha.Checked;
            top += 30;

            chkEnableTentMaidButler = CreateToggleCheckbox("Tent Housekeeper & Meal Cooking (AC 62)", mgr.EnableTentMaidButler, top);
            chkEnableTentMaidButler.CheckedChanged += (s, e) => mgr.EnableTentMaidButler = chkEnableTentMaidButler.Checked;
            top += 30;

            chkEnableCombatStatus = CreateToggleCheckbox("Turn-Based Combat Status & Buff Decay (AC 114/115)", mgr.EnableCombatStatus, top);
            chkEnableCombatStatus.CheckedChanged += (s, e) => mgr.EnableCombatStatus = chkEnableCombatStatus.Checked;
            top += 45;

            Button btnSaveConfig = new Button
            {
                Text = "Save Configuration to Disk",
                Location = new Point(14, top),
                Size = new Size(200, 32),
                Font = ModernTheme.FontBold,
                BackColor = ModernTheme.Emerald600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSaveConfig.FlatAppearance.BorderSize = 0;
            btnSaveConfig.Click += (s, e) =>
            {
                mgr.SaveSettings();
                MessageBox.Show("Experimental feature configuration saved successfully to Data/experimental_settings.ini", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnReloadConfig = new Button
            {
                Text = "Reload from Disk",
                Location = new Point(225, top),
                Size = new Size(160, 32),
                Font = ModernTheme.FontBold,
                BackColor = ModernTheme.Slate700,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnReloadConfig.FlatAppearance.BorderSize = 0;
            btnReloadConfig.Click += (s, e) =>
            {
                mgr.LoadSettings();
                chkEnableDungeons.Checked = mgr.EnableInstanceDungeons;
                chkEnablePvPArena.Checked = mgr.EnablePvPArena;
                chkEnableGuildSiege.Checked = mgr.EnableGuildSiege;
                chkEnableArcadeMinigames.Checked = mgr.EnableArcadeMinigames;
                chkEnableFestivalRaces.Checked = mgr.EnableFestivalRaces;
                chkEnableMapMechanisms.Checked = mgr.EnableMapMechanisms;
                chkEnableCompanionTalents.Checked = mgr.EnableCompanionTalents;

                chkEnableBulletin.Checked = mgr.EnableCommunityBulletin;
                chkEnableCharity.Checked = mgr.EnableChurchCharity;
                chkEnableForge.Checked = mgr.EnableEquipmentForge;
                chkEnableGobang.Checked = mgr.EnableGobangWager;
                chkEnableFishing.Checked = mgr.EnableAquacultureFishing;
                chkEnableHotSpring.Checked = mgr.EnableHotSpringBathing;
                chkEnablePinVault.Checked = mgr.EnableSecondaryPinVault;
                chkEnableBazaar.Checked = mgr.EnablePlayerStallBazaar;
                chkEnableWeather.Checked = mgr.EnableDynamicWeather;
                chkEnableVehicles.Checked = mgr.EnableVehicleTransport;

                // Wave 3
                chkEnableAtmBanking.Checked = mgr.EnableAtmBanking;
                chkEnablePlayerTitles.Checked = mgr.EnablePlayerTitles;
                chkEnableTreasureExcavation.Checked = mgr.EnableTreasureExcavation;
                chkEnableLotteryGacha.Checked = mgr.EnableLotteryGacha;
                chkEnableTentMaidButler.Checked = mgr.EnableTentMaidButler;
                chkEnableCombatStatus.Checked = mgr.EnableCombatStatus;

                MessageBox.Show("Configuration reloaded from disk.", "Reloaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            pnl.Controls.Add(lblHeading);
            pnl.Controls.Add(lblDesc);

            // Wave 1
            pnl.Controls.Add(chkEnableDungeons);
            pnl.Controls.Add(chkEnablePvPArena);
            pnl.Controls.Add(chkEnableGuildSiege);
            pnl.Controls.Add(chkEnableArcadeMinigames);
            pnl.Controls.Add(chkEnableFestivalRaces);
            pnl.Controls.Add(chkEnableMapMechanisms);
            pnl.Controls.Add(chkEnableCompanionTalents);

            // Wave 2
            pnl.Controls.Add(chkEnableBulletin);
            pnl.Controls.Add(chkEnableCharity);
            pnl.Controls.Add(chkEnableForge);
            pnl.Controls.Add(chkEnableGobang);
            pnl.Controls.Add(chkEnableFishing);
            pnl.Controls.Add(chkEnableHotSpring);
            pnl.Controls.Add(chkEnablePinVault);
            pnl.Controls.Add(chkEnableBazaar);
            pnl.Controls.Add(chkEnableWeather);
            pnl.Controls.Add(chkEnableVehicles);

            // Wave 3
            pnl.Controls.Add(chkEnableAtmBanking);
            pnl.Controls.Add(chkEnablePlayerTitles);
            pnl.Controls.Add(chkEnableTreasureExcavation);
            pnl.Controls.Add(chkEnableLotteryGacha);
            pnl.Controls.Add(chkEnableTentMaidButler);
            pnl.Controls.Add(chkEnableCombatStatus);

            pnl.Controls.Add(btnSaveConfig);
            pnl.Controls.Add(btnReloadConfig);

            page.Controls.Add(pnl);
            return page;
        }

        private CheckBox CreateToggleCheckbox(string text, bool initial, int top)
        {
            return new CheckBox
            {
                Text = text,
                Checked = initial,
                Font = ModernTheme.FontDefault,
                ForeColor = ModernTheme.Slate800,
                Location = new Point(16, top),
                AutoSize = true
            };
        }
        #endregion

        #region Sub-Tab 2: Instance Dungeons (AC 85 & Eve 17)
        private TabPage BuildDungeonsSubTab()
        {
            TabPage page = new TabPage("Instance Dungeons (AC 85)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnCreateTest = new Button
            {
                Text = "Spawn Test Dungeon",
                Size = new Size(150, 28),
                BackColor = ModernTheme.Blue600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnCreateTest.FlatAppearance.BorderSize = 0;
            btnCreateTest.Click += (s, e) =>
            {
                var dummy = new Player { CharID = 1000, CharName = "Admin" };
                var inst = ExperimentalFeaturesManager.Instance.InstanceDungeons.CreateRoom(dummy, "Challenge Chamber", 30001);
                RefreshDungeonsGrid();
                MessageBox.Show($"Created Room #{inst.RoomId} for Map {inst.MapId} (Stages: {inst.MaxWaves})", "Dungeon Spawned", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            toolbar.Controls.Add(btnCreateTest);

            dgvInstances = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvInstances.Columns.Add("colId", "ID");
            dgvInstances.Columns.Add("colLeader", "Leader CharID");
            dgvInstances.Columns.Add("colMap", "Map ID");
            dgvInstances.Columns.Add("colStage", "Stage");
            dgvInstances.Columns.Add("colElapsed", "Elapsed Time");
            dgvInstances.Columns.Add("colPlayers", "Players");

            dgvInstances.Columns["colId"].Width = 60;
            dgvInstances.Columns["colLeader"].Width = 110;
            dgvInstances.Columns["colMap"].Width = 90;
            dgvInstances.Columns["colStage"].Width = 80;
            dgvInstances.Columns["colElapsed"].Width = 120;
            dgvInstances.Columns["colPlayers"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvInstances);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshDungeonsGrid()
        {
            if (dgvInstances == null) return;
            dgvInstances.Rows.Clear();

            var instances = ExperimentalFeaturesManager.Instance.InstanceDungeons.GetRooms();
            foreach (var inst in instances)
            {
                string elapsed = $"{(DateTime.UtcNow - inst.CreatedAt).TotalMinutes:F1} min";
                dgvInstances.Rows.Add(inst.RoomId, inst.LeaderCharId, inst.MapId, $"{inst.CurrentWave}/{inst.MaxWaves}", elapsed, inst.Members.Count);
            }

            if (ext_lblExpMetricDungeons != null)
            {
                ext_lblExpMetricDungeons.Text = $"Dungeons: {instances.Count}";
            }
        }
        #endregion

        #region Sub-Tab 3: PvP Arena (AC 183 & AC 186)
        private TabPage BuildArenaSubTab()
        {
            TabPage page = new TabPage("PvP Arena (AC 183)");
            page.BackColor = Color.White;

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 350
            };

            // Left: Matchmaking Queue
            Panel pnlLeft = new Panel { Dock = DockStyle.Fill };
            Label lblQueue = new Label { Text = "Matchmaking Queue (1v1)", Dock = DockStyle.Top, Height = 25, Font = ModernTheme.FontBold, ForeColor = ModernTheme.Slate800 };
            dgvPvPQueue = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvPvPQueue.Columns.Add("colQueueChar", "Char ID");
            dgvPvPQueue.Columns.Add("colQueueRating", "Elo Rating");
            dgvPvPQueue.Columns.Add("colQueueWait", "Waiting");
            dgvPvPQueue.Columns["colQueueWait"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            pnlLeft.Controls.Add(dgvPvPQueue);
            pnlLeft.Controls.Add(lblQueue);

            // Right: Elo Leaderboard
            Panel pnlRight = new Panel { Dock = DockStyle.Fill };
            Label lblBoard = new Label { Text = "Top Arena Gladiators", Dock = DockStyle.Top, Height = 25, Font = ModernTheme.FontBold, ForeColor = ModernTheme.Slate800 };
            dgvPvPLeaderboard = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvPvPLeaderboard.Columns.Add("colRank", "#");
            dgvPvPLeaderboard.Columns.Add("colName", "Gladiator");
            dgvPvPLeaderboard.Columns.Add("colRating", "Rating");
            dgvPvPLeaderboard.Columns.Add("colW", "W");
            dgvPvPLeaderboard.Columns.Add("colL", "L");
            dgvPvPLeaderboard.Columns.Add("colStreak", "Streak");

            dgvPvPLeaderboard.Columns["colRank"].Width = 45;
            dgvPvPLeaderboard.Columns["colName"].Width = 140;
            dgvPvPLeaderboard.Columns["colRating"].Width = 70;
            dgvPvPLeaderboard.Columns["colW"].Width = 45;
            dgvPvPLeaderboard.Columns["colL"].Width = 45;
            dgvPvPLeaderboard.Columns["colStreak"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            pnlRight.Controls.Add(dgvPvPLeaderboard);
            pnlRight.Controls.Add(lblBoard);

            split.Panel1.Controls.Add(pnlLeft);
            split.Panel2.Controls.Add(pnlRight);
            page.Controls.Add(split);
            return page;
        }

        private void RefreshArenaGrids()
        {
            if (dgvPvPQueue == null || dgvPvPLeaderboard == null) return;

            dgvPvPQueue.Rows.Clear();
            var queue = ExperimentalFeaturesManager.Instance.PvPArena.GetQueue();
            foreach (var q in queue)
            {
                string wait = $"{(DateTime.UtcNow - q.EnqueueTime).TotalSeconds:F0}s";
                dgvPvPQueue.Rows.Add(q.CharId, q.EloRating, wait);
            }

            dgvPvPLeaderboard.Rows.Clear();
            var top = ExperimentalFeaturesManager.Instance.PvPArena.GetLeaderboard();
            int rank = 1;
            foreach (var g in top)
            {
                dgvPvPLeaderboard.Rows.Add(rank++, g.CharName, g.EloRating, g.Wins, g.Losses, g.RankTitle);
            }

            if (ext_lblExpMetricArena != null)
            {
                ext_lblExpMetricArena.Text = $"PvP Queue: {queue.Count}";
            }
        }
        #endregion

        #region Sub-Tab 4: Guild Siege (AC 184 & AC 92)
        private TabPage BuildSiegeSubTab()
        {
            TabPage page = new TabPage("Guild Siege (AC 184)");
            page.BackColor = Color.White;
            page.Padding = new Padding(16);

            Panel pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            Label lblHeading = new Label { Text = "Territory Power Crystals", Font = ModernTheme.FontHeading, ForeColor = ModernTheme.Slate900, Location = new Point(10, 10), AutoSize = true };
            Label lblDesc = new Label { Text = "Real-time health of city nexus crystals contested during Sunday Guild Sieges.", Font = ModernTheme.FontDefault, ForeColor = ModernTheme.Slate500, Location = new Point(12, 35), AutoSize = true };

            int y = 75;

            // Kelan Crystal
            Label lblK = new Label { Text = "Kelan Village Nexus Crystal (Map 10000)", Font = ModernTheme.FontBold, ForeColor = ModernTheme.Slate800, Location = new Point(14, y), AutoSize = true };
            y += 24;
            prgKelanCrystal = new ProgressBar { Location = new Point(14, y), Size = new Size(400, 22), Maximum = 1000000 };
            lblKelanInfo = new Label { Location = new Point(425, y + 2), AutoSize = true, Font = ModernTheme.FontDefault, ForeColor = ModernTheme.Slate700 };
            y += 45;

            // Holy Village Crystal
            Label lblH = new Label { Text = "Holy Village Fortress Crystal (Map 11000)", Font = ModernTheme.FontBold, ForeColor = ModernTheme.Slate800, Location = new Point(14, y), AutoSize = true };
            y += 24;
            prgHolyCrystal = new ProgressBar { Location = new Point(14, y), Size = new Size(400, 22), Maximum = 1000000 };
            lblHolyInfo = new Label { Location = new Point(425, y + 2), AutoSize = true, Font = ModernTheme.FontDefault, ForeColor = ModernTheme.Slate700 };
            y += 45;

            // Kyoto Castle Crystal
            Label lblC = new Label { Text = "Kyoto Citadel Nexus Crystal (Map 12000)", Font = ModernTheme.FontBold, ForeColor = ModernTheme.Slate800, Location = new Point(14, y), AutoSize = true };
            y += 24;
            prgKyotoCrystal = new ProgressBar { Location = new Point(14, y), Size = new Size(400, 22), Maximum = 1000000 };
            lblKyotoInfo = new Label { Location = new Point(425, y + 2), AutoSize = true, Font = ModernTheme.FontDefault, ForeColor = ModernTheme.Slate700 };
            y += 50;

            Button btnStartSiege = new Button
            {
                Text = "Activate Warfare Mode",
                Location = new Point(14, y),
                Size = new Size(180, 32),
                Font = ModernTheme.FontBold,
                BackColor = ModernTheme.Rose600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnStartSiege.FlatAppearance.BorderSize = 0;
            btnStartSiege.Click += (s, e) =>
            {
                var siege = ExperimentalFeaturesManager.Instance.GuildSiege;
                siege.StartSiege(1);
                siege.StartSiege(2);
                siege.StartSiege(3);
                RefreshSiegeControls();
                MessageBox.Show("Guild Siege warfare mode activated across all three territories!", "Warfare Active", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };

            Button btnEndSiege = new Button
            {
                Text = "End Warfare & Tally",
                Location = new Point(205, y),
                Size = new Size(180, 32),
                Font = ModernTheme.FontBold,
                BackColor = ModernTheme.Slate700,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnEndSiege.FlatAppearance.BorderSize = 0;
            btnEndSiege.Click += (s, e) =>
            {
                var siege = ExperimentalFeaturesManager.Instance.GuildSiege;
                siege.EndSiege(1);
                siege.EndSiege(2);
                siege.EndSiege(3);
                RefreshSiegeControls();
                MessageBox.Show("Guild Siege ended. Territory controllers updated.", "Warfare Concluded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            pnl.Controls.Add(lblHeading);
            pnl.Controls.Add(lblDesc);
            pnl.Controls.Add(lblK);
            pnl.Controls.Add(prgKelanCrystal);
            pnl.Controls.Add(lblKelanInfo);
            pnl.Controls.Add(lblH);
            pnl.Controls.Add(prgHolyCrystal);
            pnl.Controls.Add(lblHolyInfo);
            pnl.Controls.Add(lblC);
            pnl.Controls.Add(prgKyotoCrystal);
            pnl.Controls.Add(lblKyotoInfo);
            pnl.Controls.Add(btnStartSiege);
            pnl.Controls.Add(btnEndSiege);

            page.Controls.Add(pnl);
            return page;
        }

        private void RefreshSiegeControls()
        {
            if (prgKelanCrystal == null) return;

            var siege = ExperimentalFeaturesManager.Instance.GuildSiege;
            var c1 = siege.GetTerritory(1);
            var c2 = siege.GetTerritory(2);
            var c3 = siege.GetTerritory(3);

            if (c1 != null)
            {
                prgKelanCrystal.Maximum = c1.MaxCrystalHp;
                prgKelanCrystal.Value = Math.Max(0, Math.Min(prgKelanCrystal.Maximum, c1.CrystalHp));
                lblKelanInfo.Text = $"{c1.CrystalHp:N0} / {c1.MaxCrystalHp:N0} HP | Owner: {c1.OccupyingGuildName}";
            }

            if (c2 != null)
            {
                prgHolyCrystal.Maximum = c2.MaxCrystalHp;
                prgHolyCrystal.Value = Math.Max(0, Math.Min(prgHolyCrystal.Maximum, c2.CrystalHp));
                lblHolyInfo.Text = $"{c2.CrystalHp:N0} / {c2.MaxCrystalHp:N0} HP | Owner: {c2.OccupyingGuildName}";
            }

            if (c3 != null)
            {
                prgKyotoCrystal.Maximum = c3.MaxCrystalHp;
                prgKyotoCrystal.Value = Math.Max(0, Math.Min(prgKyotoCrystal.Maximum, c3.CrystalHp));
                lblKyotoInfo.Text = $"{c3.CrystalHp:N0} / {c3.MaxCrystalHp:N0} HP | Owner: {c3.OccupyingGuildName}";
            }

            bool isWar = siege.Territories.Any(t => t.State == SiegeWarState.ActiveWar);
            if (ext_lblExpMetricSiege != null)
            {
                ext_lblExpMetricSiege.Text = isWar ? "Siege: ACTIVE" : "Siege: Idle";
                ext_lblExpMetricSiege.BackColor = isWar ? ModernTheme.Rose600 : ModernTheme.Amber600;
            }
        }
        #endregion

        #region Sub-Tab 5: Carnival Arcade (AC 57)
        private TabPage BuildArcadeSubTab()
        {
            TabPage page = new TabPage("Carnival Arcade (AC 57)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnTestArcade = new Button
            {
                Text = "Simulate Minigame Session",
                Size = new Size(180, 28),
                BackColor = ModernTheme.Emerald600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnTestArcade.FlatAppearance.BorderSize = 0;
            btnTestArcade.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 8801, CharName = "Arcade Hero" };
                    var session = ExperimentalFeaturesManager.Instance.ArcadeMinigames.StartSession(dummy, MinigameKind.WhackAMole);
                    if (session != null)
                    {
                        ExperimentalFeaturesManager.Instance.ArcadeMinigames.SubmitScore(dummy, session.SessionId, 450, true);
                        MessageBox.Show($"Minigame session #{session.SessionId} simulated with score 450!", "Arcade Score Recorded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    RefreshArcadeGrid();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Arcade simulation error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(btnTestArcade);

            dgvArcadeSessions = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvArcadeSessions.Columns.Add("colSession", "Session ID");
            dgvArcadeSessions.Columns.Add("colPlayer", "Player");
            dgvArcadeSessions.Columns.Add("colGame", "Minigame");
            dgvArcadeSessions.Columns.Add("colScore", "Score");
            dgvArcadeSessions.Columns.Add("colFinished", "Finished");
            dgvArcadeSessions.Columns.Add("colTime", "Time");

            dgvArcadeSessions.Columns["colSession"].Width = 90;
            dgvArcadeSessions.Columns["colPlayer"].Width = 140;
            dgvArcadeSessions.Columns["colGame"].Width = 160;
            dgvArcadeSessions.Columns["colScore"].Width = 90;
            dgvArcadeSessions.Columns["colFinished"].Width = 80;
            dgvArcadeSessions.Columns["colTime"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvArcadeSessions);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshArcadeGrid()
        {
            if (dgvArcadeSessions == null) return;
            dgvArcadeSessions.Rows.Clear();

            var sessions = ExperimentalFeaturesManager.Instance.ArcadeMinigames.GetRecentSessions();
            foreach (var s in sessions)
            {
                dgvArcadeSessions.Rows.Add(s.SessionId, s.CharName, s.GameKind, s.Score, s.IsFinished ? "Yes" : "No", $"{s.StartTime:HH:mm:ss}");
            }

            if (ext_lblExpMetricMinigames != null)
            {
                ext_lblExpMetricMinigames.Text = $"Arcade: {ExperimentalFeaturesManager.Instance.ArcadeMinigames.ActiveSessionsCount}";
            }
        }
        #endregion

        #region Sub-Tab 6: Festival Races (AC 226)
        private TabPage BuildRacesSubTab()
        {
            TabPage page = new TabPage("Festival Races (AC 226)");
            page.BackColor = Color.White;

            dgvRaceTracks = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvRaceTracks.Columns.Add("colTrkId", "Track ID");
            dgvRaceTracks.Columns.Add("colTrkName", "Circuit Name");
            dgvRaceTracks.Columns.Add("colTrkMap", "Map ID");
            dgvRaceTracks.Columns.Add("colTrkLaps", "Laps");
            dgvRaceTracks.Columns.Add("colTrkCP", "Checkpoints");
            dgvRaceTracks.Columns.Add("colTrkMin", "Min Lap Time");

            dgvRaceTracks.Columns["colTrkId"].Width = 80;
            dgvRaceTracks.Columns["colTrkName"].Width = 240;
            dgvRaceTracks.Columns["colTrkMap"].Width = 80;
            dgvRaceTracks.Columns["colTrkLaps"].Width = 70;
            dgvRaceTracks.Columns["colTrkCP"].Width = 100;
            dgvRaceTracks.Columns["colTrkMin"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvRaceTracks);
            return page;
        }

        private void RefreshRacesGrid()
        {
            if (dgvRaceTracks == null) return;
            dgvRaceTracks.Rows.Clear();

            var tracks = ExperimentalFeaturesManager.Instance.FestivalRaces.Tracks;
            foreach (var t in tracks)
            {
                dgvRaceTracks.Rows.Add(t.TrackId, t.TrackName, t.MapId, t.TotalLaps, $"{t.Checkpoints.Count} Checkpoints", $"{t.MinLapSeconds}s");
            }
        }
        #endregion

        #region Sub-Tab 7: Community Bulletin (AC 82)
        private TabPage BuildBulletinSubTab()
        {
            TabPage page = new TabPage("Community Bulletin (AC 82)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnBroadcast = new Button
            {
                Text = "Broadcast Global Notice",
                Size = new Size(180, 28),
                BackColor = ModernTheme.Blue600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnBroadcast.FlatAppearance.BorderSize = 0;
            btnBroadcast.Click += (s, e) =>
            {
                ExperimentalFeaturesManager.Instance.CommunityBulletin.BroadcastGlobalAnnouncement("Server Announcement", "Server event starting shortly in Kelan Village!");
                RefreshBulletinGrid();
                MessageBox.Show("Global notice dispatched to bulletin boards.", "Notice Dispatched", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            toolbar.Controls.Add(btnBroadcast);

            dgvBulletinPosts = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvBulletinPosts.Columns.Add("colPostId", "Post ID");
            dgvBulletinPosts.Columns.Add("colAuthor", "Author");
            dgvBulletinPosts.Columns.Add("colGuild", "Guild ID");
            dgvBulletinPosts.Columns.Add("colTitle", "Title");
            dgvBulletinPosts.Columns.Add("colContent", "Message Content");
            dgvBulletinPosts.Columns.Add("colDate", "Posted");

            dgvBulletinPosts.Columns["colPostId"].Width = 70;
            dgvBulletinPosts.Columns["colAuthor"].Width = 120;
            dgvBulletinPosts.Columns["colGuild"].Width = 80;
            dgvBulletinPosts.Columns["colTitle"].Width = 200;
            dgvBulletinPosts.Columns["colContent"].Width = 320;
            dgvBulletinPosts.Columns["colDate"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvBulletinPosts);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshBulletinGrid()
        {
            if (dgvBulletinPosts == null) return;
            dgvBulletinPosts.Rows.Clear();

            var posts = ExperimentalFeaturesManager.Instance.CommunityBulletin.GetPosts(0, true);
            foreach (var p in posts)
            {
                dgvBulletinPosts.Rows.Add(p.PostId, p.AuthorName, p.GuildId, p.Title, p.Content, $"{p.PostedAt:yyyy-MM-dd HH:mm}");
            }
        }
        #endregion

        #region Sub-Tab 8: Church Charity & Karma (AC 76)
        private TabPage BuildCharitySubTab()
        {
            TabPage page = new TabPage("Church Charity (AC 76)");
            page.BackColor = Color.White;

            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(12, 10, 12, 10) };

            lblTitheVaultTotal = new Label
            {
                Text = "Tithe Vault Reserves: 150,000G | Donors: 0",
                Font = ModernTheme.FontBold,
                ForeColor = ModernTheme.Slate800,
                Location = new Point(12, 12),
                AutoSize = true
            };

            Button btnTestDonation = new Button
            {
                Text = "Simulate 5,000G Tithe",
                Size = new Size(160, 26),
                BackColor = ModernTheme.Emerald600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(12, 32)
            };
            btnTestDonation.FlatAppearance.BorderSize = 0;
            btnTestDonation.Click += (s, e) =>
            {
                try
                {
                    var mgr = ExperimentalFeaturesManager.Instance.ChurchCharity;
                    var dummy = new Player { CharID = 9999, CharName = "Pilgrim John" };
                    dummy.Gold = 100000;
                    mgr.Donate(dummy, 5000);
                    RefreshCharityGrid();
                    MessageBox.Show("Simulated 5,000G donation from Pilgrim John to the Church Tithe Vault!", "Donation Recorded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Charity donation error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            topPanel.Controls.Add(lblTitheVaultTotal);
            topPanel.Controls.Add(btnTestDonation);

            dgvCharityLeaderboard = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvCharityLeaderboard.Columns.Add("colCharId", "Char ID");
            dgvCharityLeaderboard.Columns.Add("colCharName", "Donor Name");
            dgvCharityLeaderboard.Columns.Add("colKarma", "Karma Points");
            dgvCharityLeaderboard.Columns.Add("colTotalGold", "Total Gold Donated");
            dgvCharityLeaderboard.Columns.Add("colBlessing", "Divine Blessing Active");

            dgvCharityLeaderboard.Columns["colCharId"].Width = 90;
            dgvCharityLeaderboard.Columns["colCharName"].Width = 150;
            dgvCharityLeaderboard.Columns["colKarma"].Width = 110;
            dgvCharityLeaderboard.Columns["colTotalGold"].Width = 160;
            dgvCharityLeaderboard.Columns["colBlessing"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvCharityLeaderboard);
            page.Controls.Add(topPanel);
            return page;
        }

        private void RefreshCharityGrid()
        {
            if (dgvCharityLeaderboard == null) return;
            dgvCharityLeaderboard.Rows.Clear();

            var charity = ExperimentalFeaturesManager.Instance.ChurchCharity;
            var top = charity.GetTopDonors();

            foreach (var r in top)
            {
                dgvCharityLeaderboard.Rows.Add(r.CharId, r.CharName, $"{r.KarmaPoints} pts", $"{r.TotalDonatedGold:N0}G", r.HasActiveBlessing ? "ACTIVE" : "None");
            }

            if (lblTitheVaultTotal != null)
            {
                lblTitheVaultTotal.Text = $"Tithe Vault Reserves: {charity.TotalCharityVaultGold:N0}G | Active Donors: {charity.TotalDonorsCount}";
            }
        }
        #endregion

        #region Sub-Tab 9: Equipment Forge & Durability (AC 40)
        private TabPage BuildForgeSubTab()
        {
            TabPage page = new TabPage("Equipment Forge (AC 40)");
            page.BackColor = Color.White;
            page.Padding = new Padding(16);

            Panel pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            Label lblHead = new Label { Text = "Equipment Durability & Forging Bench", Font = ModernTheme.FontHeading, ForeColor = ModernTheme.Slate900, Location = new Point(10, 10), AutoSize = true };
            Label lblDesc = new Label { Text = "Monitor durability decay, test repair hammer restoration, and simulate forging upgrade success curves.", Font = ModernTheme.FontDefault, ForeColor = ModernTheme.Slate500, Location = new Point(12, 35), AutoSize = true };

            int y = 75;

            Label lblSlot = new Label { Text = "Target Equipment Slot (0=Head, 1=Body, 2=Weapon, 3=Shield, 4=Shoes, 5=Ring):", Font = ModernTheme.FontBold, ForeColor = ModernTheme.Slate800, Location = new Point(14, y), AutoSize = true };
            y += 24;

            NumericUpDown numSlot = new NumericUpDown { Location = new Point(16, y), Minimum = 0, Maximum = 5, Value = 2, Width = 80, Font = ModernTheme.FontDefault };
            y += 40;

            Button btnWear = new Button
            {
                Text = "Apply -10 Durability Wear",
                Location = new Point(16, y),
                Size = new Size(180, 30),
                BackColor = ModernTheme.Amber600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall
            };
            btnWear.FlatAppearance.BorderSize = 0;
            btnWear.Click += (s, e) =>
            {
                byte slot = (byte)numSlot.Value;
                int cur = ExperimentalFeaturesManager.Instance.EquipmentForge.GetDurability(1001, slot);
                int next = Math.Max(0, cur - 10);
                ExperimentalFeaturesManager.Instance.EquipmentForge.SetDurability(1001, slot, next);
                lblForgeOutcome.Text = $"Slot {slot} durability reduced to {next}/100";
            };

            Button btnRepair = new Button
            {
                Text = "Repair with Hammer (+100%)",
                Location = new Point(205, y),
                Size = new Size(200, 30),
                BackColor = ModernTheme.Emerald600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall
            };
            btnRepair.FlatAppearance.BorderSize = 0;
            btnRepair.Click += (s, e) =>
            {
                byte slot = (byte)numSlot.Value;
                ExperimentalFeaturesManager.Instance.EquipmentForge.SetDurability(1001, slot, 100);
                lblForgeOutcome.Text = $"Slot {slot} restored to 100/100 Durability with Repair Hammer";
            };
            y += 45;

            Button btnUpgrade = new Button
            {
                Text = "Attempt Forge Upgrade (+1)",
                Location = new Point(16, y),
                Size = new Size(220, 32),
                BackColor = ModernTheme.Blue600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall
            };
            btnUpgrade.FlatAppearance.BorderSize = 0;
            btnUpgrade.Click += (s, e) =>
            {
                byte slot = (byte)numSlot.Value;
                var res = ExperimentalFeaturesManager.Instance.EquipmentForge.ForgeUpgrade(null, slot, 27001);
                lblForgeOutcome.Text = $"Forge Result: {res.Outcome} (Grade: +{res.TargetGrade}) - {res.Message}";
            };
            y += 50;

            lblForgeOutcome = new Label
            {
                Text = "Bench Ready. Select a slot and action.",
                Font = ModernTheme.FontBold,
                ForeColor = ModernTheme.Slate700,
                Location = new Point(16, y),
                AutoSize = true
            };

            pnl.Controls.Add(lblHead);
            pnl.Controls.Add(lblDesc);
            pnl.Controls.Add(lblSlot);
            pnl.Controls.Add(numSlot);
            pnl.Controls.Add(btnWear);
            pnl.Controls.Add(btnRepair);
            pnl.Controls.Add(btnUpgrade);
            pnl.Controls.Add(lblForgeOutcome);

            page.Controls.Add(pnl);
            return page;
        }
        #endregion

        #region Sub-Tab 10: Gobang Chess (AC 41)
        private TabPage BuildGobangSubTab()
        {
            TabPage page = new TabPage("Gobang Chess (AC 41)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnTestMatch = new Button
            {
                Text = "Simulate 15x15 Match",
                Size = new Size(180, 28),
                BackColor = ModernTheme.Emerald600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnTestMatch.FlatAppearance.BorderSize = 0;
            btnTestMatch.Click += (s, e) =>
            {
                try
                {
                    var h = new Player { CharID = 8801, CharName = "Grandmaster Black" };
                    var c = new Player { CharID = 8802, CharName = "Challenger White" };
                    var match = ExperimentalFeaturesManager.Instance.GobangWager.CreateMatch(h, c, 5000);
                    RefreshGobangGrid();
                    if (match != null)
                        MessageBox.Show($"Created Gobang Match #{match.MatchId} between {match.PlayerBlackName} and {match.PlayerWhiteName} with 5,000G Ante!", "Match Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed match creation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(btnTestMatch);

            dgvGobangMatches = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvGobangMatches.Columns.Add("colMatchId", "Match ID");
            dgvGobangMatches.Columns.Add("colBlack", "Black Player");
            dgvGobangMatches.Columns.Add("colWhite", "White Player");
            dgvGobangMatches.Columns.Add("colWager", "Wager Ante");
            dgvGobangMatches.Columns.Add("colTurn", "Current Turn");
            dgvGobangMatches.Columns.Add("colStatus", "Status");

            dgvGobangMatches.Columns["colMatchId"].Width = 90;
            dgvGobangMatches.Columns["colBlack"].Width = 150;
            dgvGobangMatches.Columns["colWhite"].Width = 150;
            dgvGobangMatches.Columns["colWager"].Width = 110;
            dgvGobangMatches.Columns["colTurn"].Width = 100;
            dgvGobangMatches.Columns["colStatus"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvGobangMatches);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshGobangGrid()
        {
            if (dgvGobangMatches == null) return;
            dgvGobangMatches.Rows.Clear();

            var matches = ExperimentalFeaturesManager.Instance.GobangWager.GetActiveMatches();
            foreach (var m in matches)
            {
                string turn = m.CurrentTurn == 1 ? "Black" : "White";
                string status = m.IsFinished ? $"Finished (Winner: #{m.WinnerCharId})" : "In Progress";
                dgvGobangMatches.Rows.Add(m.MatchId, m.PlayerBlackName, m.PlayerWhiteName, $"{m.WagerGold:N0}G", turn, status);
            }
        }
        #endregion

        #region Sub-Tab 11: Aquaculture & Fishing (AC 90)
        private TabPage BuildFishingSubTab()
        {
            TabPage page = new TabPage("Aquaculture (AC 90)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnTestCatch = new Button
            {
                Text = "Simulate Angler Catch",
                Size = new Size(180, 28),
                BackColor = ModernTheme.Blue600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnTestCatch.FlatAppearance.BorderSize = 0;
            btnTestCatch.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 7701, CharName = "Old Fisherman" };
                    ExperimentalFeaturesManager.Instance.AquacultureFishing.StartFishing(dummy, 26101, 26201);
                    var catchRes = ExperimentalFeaturesManager.Instance.AquacultureFishing.CatchFish(dummy);
                    RefreshFishingGrid();
                    if (catchRes != null)
                        MessageBox.Show($"Caught: {catchRes.ItemName} ({catchRes.WeightKg} kg, +{catchRes.ExpGained} EXP)!", "Fish Landed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed fishing simulation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(btnTestCatch);

            dgvFishingAnglers = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvFishingAnglers.Columns.Add("colAnglerId", "Angler CharID");
            dgvFishingAnglers.Columns.Add("colStatus", "Status");
            dgvFishingAnglers.Columns.Add("colLootTier", "Active Catch Profile");

            dgvFishingAnglers.Columns["colAnglerId"].Width = 140;
            dgvFishingAnglers.Columns["colStatus"].Width = 160;
            dgvFishingAnglers.Columns["colLootTier"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvFishingAnglers);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshFishingGrid()
        {
            if (dgvFishingAnglers == null) return;
            dgvFishingAnglers.Rows.Clear();

            int anglers = ExperimentalFeaturesManager.Instance.AquacultureFishing.ActiveAnglerCount;
            dgvFishingAnglers.Rows.Add("System Angling Table", $"{anglers} Active Lines In Water", "Kelp (40%), Carp (30%), Tuna (20%), Golden Chest (5%)");
        }
        #endregion

        #region Sub-Tab 12: Hot Spring Recovery (AC 44)
        private TabPage BuildHotSpringSubTab()
        {
            TabPage page = new TabPage("Hot Spring (AC 44)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnTestBather = new Button
            {
                Text = "Simulate Bather Entry",
                Size = new Size(180, 28),
                BackColor = ModernTheme.Emerald600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnTestBather.FlatAppearance.BorderSize = 0;
            btnTestBather.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 6601, CharName = "Spa Guest" };
                    bool entered = ExperimentalFeaturesManager.Instance.HotSpringBathing.EnterHotSpring(dummy, 10000);
                    ExperimentalFeaturesManager.Instance.HotSpringBathing.PulseRestTick(dummy);
                    RefreshHotSpringGrid();
                    if (entered)
                        MessageBox.Show($"Bather '{dummy.CharName}' entered hot spring (Map 10000) and received rest pulse!", "Hot Spring Entered", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed hot spring simulation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(btnTestBather);

            dgvHotSpringBathers = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvHotSpringBathers.Columns.Add("colCharId", "Char ID");
            dgvHotSpringBathers.Columns.Add("colCharName", "Bather Name");
            dgvHotSpringBathers.Columns.Add("colMapId", "Map ID");
            dgvHotSpringBathers.Columns.Add("colEntered", "Entered At");
            dgvHotSpringBathers.Columns.Add("colTicks", "Rest Ticks");

            dgvHotSpringBathers.Columns["colCharId"].Width = 90;
            dgvHotSpringBathers.Columns["colCharName"].Width = 150;
            dgvHotSpringBathers.Columns["colMapId"].Width = 90;
            dgvHotSpringBathers.Columns["colEntered"].Width = 120;
            dgvHotSpringBathers.Columns["colTicks"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvHotSpringBathers);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshHotSpringGrid()
        {
            if (dgvHotSpringBathers == null) return;
            dgvHotSpringBathers.Rows.Clear();

            var bathers = ExperimentalFeaturesManager.Instance.HotSpringBathing.GetBathers();
            foreach (var b in bathers)
            {
                dgvHotSpringBathers.Rows.Add(b.CharId, b.CharName, b.MapId, $"{b.EnteredAt:HH:mm:ss}", $"{b.TotalTicks} ticks (+{b.TotalTicks * 5}% restored)");
            }
        }
        #endregion

        #region Sub-Tab 13: Secondary PIN Vault (AC 24)
        private TabPage BuildPinVaultSubTab()
        {
            TabPage page = new TabPage("PIN Vault (AC 24)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnResetLockout = new Button
            {
                Text = "Reset All Failed Lockouts",
                Size = new Size(180, 28),
                BackColor = ModernTheme.Emerald600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnResetLockout.FlatAppearance.BorderSize = 0;
            btnResetLockout.Click += (s, e) =>
            {
                ExperimentalFeaturesManager.Instance.SecondaryPinVault.ResetLockout(5501);
                RefreshPinVaultGrid();
                MessageBox.Show("Administrative PIN lockout reset successfully.", "Lockout Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            toolbar.Controls.Add(btnResetLockout);

            dgvPinVaults = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvPinVaults.Columns.Add("colCharId", "Char ID");
            dgvPinVaults.Columns.Add("colStatus", "PIN Protection");
            dgvPinVaults.Columns.Add("colFailed", "Failed Attempts");
            dgvPinVaults.Columns.Add("colLockout", "Lockout Status");

            dgvPinVaults.Columns["colCharId"].Width = 100;
            dgvPinVaults.Columns["colStatus"].Width = 140;
            dgvPinVaults.Columns["colFailed"].Width = 120;
            dgvPinVaults.Columns["colLockout"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvPinVaults);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshPinVaultGrid()
        {
            if (dgvPinVaults == null) return;
            dgvPinVaults.Rows.Clear();

            dgvPinVaults.Rows.Add(5501, "Active (6-digit PIN)", "0 / 3", "Unlocked (Normal Operation)");
        }
        #endregion

        #region Sub-Tab 14: Overworld Bazaar (AC 53)
        private TabPage BuildBazaarSubTab()
        {
            TabPage page = new TabPage("Overworld Bazaar (AC 53)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 6, 10, 6) };

            // Row 1: Coordinate, Title, Vendor, Infinite toggle, and Actions
            Label lblMap = new Label { Text = "Map ID:", Location = new Point(10, 10), AutoSize = true, Font = ModernTheme.FontBoldSmall };
            NumericUpDown numMap = new NumericUpDown { Location = new Point(62, 7), Width = 60, Minimum = 1, Maximum = 65535, Value = 12000, Font = ModernTheme.FontDefault };

            Label lblX = new Label { Text = "X:", Location = new Point(128, 10), AutoSize = true, Font = ModernTheme.FontBoldSmall };
            NumericUpDown numX = new NumericUpDown { Location = new Point(144, 7), Width = 55, Minimum = 0, Maximum = 10000, Value = 1200, Font = ModernTheme.FontDefault };

            Label lblY = new Label { Text = "Y:", Location = new Point(205, 10), AutoSize = true, Font = ModernTheme.FontBoldSmall };
            NumericUpDown numY = new NumericUpDown { Location = new Point(221, 7), Width = 55, Minimum = 0, Maximum = 10000, Value = 1500, Font = ModernTheme.FontDefault };

            Label lblTitle = new Label { Text = "Title:", Location = new Point(282, 10), AutoSize = true, Font = ModernTheme.FontBoldSmall };
            TextBox txtTitle = new TextBox { Location = new Point(318, 7), Width = 135, Text = "Rare Alchemy Supplies", Font = ModernTheme.FontDefault };

            Label lblVendor = new Label { Text = "Vendor:", Location = new Point(458, 10), AutoSize = true, Font = ModernTheme.FontBoldSmall };
            TextBox txtVendor = new TextBox { Location = new Point(510, 7), Width = 110, Text = "Bazaar Merchant", Font = ModernTheme.FontDefault };

            CheckBox chkInfinite = new CheckBox
            {
                Text = "Infinite Stock",
                Location = new Point(628, 8),
                AutoSize = true,
                Font = ModernTheme.FontBoldSmall,
                ForeColor = ModernTheme.Blue700,
                Checked = false
            };

            Button btnSpawnStall = new Button
            {
                Text = "Spawn Stall",
                Size = new Size(115, 26),
                BackColor = ModernTheme.Blue600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(745, 6)
            };
            btnSpawnStall.FlatAppearance.BorderSize = 0;

            Button btnDeleteSelected = new Button
            {
                Text = "Delete Selected",
                Size = new Size(115, 26),
                BackColor = ModernTheme.Rose600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(868, 6)
            };
            btnDeleteSelected.FlatAppearance.BorderSize = 0;

            Button btnClearStalls = new Button
            {
                Text = "Clear All",
                Size = new Size(85, 26),
                BackColor = ModernTheme.Rose700,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(991, 6)
            };
            btnClearStalls.FlatAppearance.BorderSize = 0;

            // Row 2: Items specification & Persistence note
            Label lblItems = new Label { Text = "Catalog Items (id:price[:qty]):", Location = new Point(10, 44), AutoSize = true, Font = ModernTheme.FontBoldSmall };
            TextBox txtItems = new TextBox
            {
                Location = new Point(185, 41),
                Width = 470,
                Text = "10001:120:50, 10002:300:25, 26315:2500:10, 27001:5000:5, 26399:15000:2",
                Font = ModernTheme.FontDefault
            };

            Label lblDbNote = new Label
            {
                Text = "Saved to DB (SQLite & MySQL) and restored on server reboot.",
                Location = new Point(665, 44),
                AutoSize = true,
                Font = ModernTheme.FontSmall,
                ForeColor = ModernTheme.Slate600
            };

            btnSpawnStall.Click += (s, e) =>
            {
                try
                {
                    ushort mapId = (ushort)numMap.Value;
                    int x = (int)numX.Value;
                    int y = (int)numY.Value;
                    string title = string.IsNullOrWhiteSpace(txtTitle.Text) ? "Custom Market Stall" : txtTitle.Text.Trim();
                    string vendor = string.IsNullOrWhiteSpace(txtVendor.Text) ? "Merchant" : txtVendor.Text.Trim();
                    bool isInf = chkInfinite.Checked;

                    var items = Game.Experimental.PlayerStallBazaarEngine.ParseItemList(txtItems.Text, isInf);
                    uint charId = ExperimentalFeaturesManager.Instance.PlayerStallBazaar.GenerateSyntheticVendorId();

                    var stall = ExperimentalFeaturesManager.Instance.PlayerStallBazaar.SpawnCustomStall(charId, vendor, title, mapId, x, y, items, isInf, true);
                    RefreshBazaarGrid();
                    if (stall != null)
                    {
                        string stockStr = isInf ? "Infinite Stock" : "Limited Stock";
                        MessageBox.Show($"Stall '{stall.StallName}' (ClickID: {stall.ClickId}, Vendor ID: {stall.VendorCharId}) spawned successfully for {stall.VendorName} at Map {stall.MapId} ({stall.X}, {stall.Y}) with {stall.Items.Count} items ({stockStr}) and saved to database.", "Stall Spawned", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to spawn stall: {ex.Message}", "Error Spawning Stall", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnDeleteSelected.Click += (s, e) =>
            {
                try
                {
                    if (dgvBazaarStalls.SelectedRows.Count == 0)
                    {
                        MessageBox.Show("Please select a stall in the table below to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var row = dgvBazaarStalls.SelectedRows[0];
                    if (row.Cells["colVendorId"].Value != null && uint.TryParse(row.Cells["colVendorId"].Value.ToString(), out uint vendorId))
                    {
                        bool deleted = ExperimentalFeaturesManager.Instance.PlayerStallBazaar.RemoveCustomStall(vendorId);
                        RefreshBazaarGrid();
                        if (deleted)
                        {
                            MessageBox.Show($"Stall (Vendor ID: {vendorId}) removed from map and database.", "Stall Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show($"Failed to remove stall {vendorId}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete stall: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnClearStalls.Click += (s, e) =>
            {
                try
                {
                    if (MessageBox.Show("Are you sure you want to delete ALL stalls from all maps and database?", "Confirm Clear All", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        ExperimentalFeaturesManager.Instance.PlayerStallBazaar.ClearAllStalls();
                        RefreshBazaarGrid();
                        MessageBox.Show("All overworld bazaar stalls have been cleared and purged from database.", "Bazaar Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to clear stalls: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(lblMap);
            toolbar.Controls.Add(numMap);
            toolbar.Controls.Add(lblX);
            toolbar.Controls.Add(numX);
            toolbar.Controls.Add(lblY);
            toolbar.Controls.Add(numY);
            toolbar.Controls.Add(lblTitle);
            toolbar.Controls.Add(txtTitle);
            toolbar.Controls.Add(lblVendor);
            toolbar.Controls.Add(txtVendor);
            toolbar.Controls.Add(chkInfinite);
            toolbar.Controls.Add(btnSpawnStall);
            toolbar.Controls.Add(btnDeleteSelected);
            toolbar.Controls.Add(btnClearStalls);

            toolbar.Controls.Add(lblItems);
            toolbar.Controls.Add(txtItems);
            toolbar.Controls.Add(lblDbNote);

            dgvBazaarStalls = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvBazaarStalls.Columns.Add("colVendorId", "Vendor ID");
            dgvBazaarStalls.Columns.Add("colVendorName", "Vendor");
            dgvBazaarStalls.Columns.Add("colStallName", "Stall Title");
            dgvBazaarStalls.Columns.Add("colMapId", "Map ID");
            dgvBazaarStalls.Columns.Add("colCoordX", "Coord X");
            dgvBazaarStalls.Columns.Add("colCoordY", "Coord Y");
            dgvBazaarStalls.Columns.Add("colStockType", "Stock Mode");
            dgvBazaarStalls.Columns.Add("colItems", "Catalog Size");
            dgvBazaarStalls.Columns.Add("colOpened", "Opened At");

            dgvBazaarStalls.Columns["colVendorId"].Width = 85;
            dgvBazaarStalls.Columns["colVendorName"].Width = 130;
            dgvBazaarStalls.Columns["colStallName"].Width = 180;
            dgvBazaarStalls.Columns["colMapId"].Width = 70;
            dgvBazaarStalls.Columns["colCoordX"].Width = 55;
            dgvBazaarStalls.Columns["colCoordY"].Width = 55;
            dgvBazaarStalls.Columns["colStockType"].Width = 90;
            dgvBazaarStalls.Columns["colItems"].Width = 95;
            dgvBazaarStalls.Columns["colOpened"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvBazaarStalls);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshBazaarGrid()
        {
            if (dgvBazaarStalls == null) return;
            dgvBazaarStalls.Rows.Clear();

            var stalls = ExperimentalFeaturesManager.Instance.PlayerStallBazaar.GetStalls();
            foreach (var s in stalls)
            {
                string stockMode = s.IsInfinite ? "Infinite" : "Limited";
                dgvBazaarStalls.Rows.Add(s.VendorCharId, s.VendorName, s.StallName, s.MapId, s.X, s.Y, stockMode, $"{s.Items.Count} items", $"{s.OpenedAt:HH:mm:ss}");
            }

            if (ext_lblExpMetricBazaar != null)
            {
                ext_lblExpMetricBazaar.Text = $"Bazaar: {stalls.Count}";
            }
        }
        #endregion

        #region Sub-Tab 15: Weather & Lighting (AC 88)
        private TabPage BuildWeatherSubTab()
        {
            TabPage page = new TabPage("Weather & Lighting (AC 88)");
            page.BackColor = Color.White;

            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(12, 10, 12, 10) };

            lblWeatherTimeBadge = new Label
            {
                Text = "Current Ambient Time: Noon (12:00)",
                Font = ModernTheme.FontBold,
                ForeColor = ModernTheme.Slate800,
                Location = new Point(12, 12),
                AutoSize = true
            };

            Button btnAdvanceTime = new Button
            {
                Text = "Advance Time of Day",
                Size = new Size(160, 26),
                BackColor = ModernTheme.Slate700,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(12, 32)
            };
            btnAdvanceTime.FlatAppearance.BorderSize = 0;
            btnAdvanceTime.Click += (s, e) =>
            {
                var next = ExperimentalFeaturesManager.Instance.DynamicWeather.AdvanceTimeOfDay();
                RefreshWeatherGrid();
            };

            Label lblMap = new Label { Text = "Map ID:", Location = new Point(200, 36), AutoSize = true, Font = ModernTheme.FontDefault };
            numWeatherMapId = new NumericUpDown { Location = new Point(255, 34), Minimum = 1000, Maximum = 99999, Value = 10000, Width = 80, Font = ModernTheme.FontDefault };

            cmbWeatherSelect = new ComboBox { Location = new Point(345, 33), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Font = ModernTheme.FontDefault };
            cmbWeatherSelect.Items.AddRange(new object[] { "Clear", "Rain", "Thunderstorm", "Snow", "Sandstorm", "Fog" });
            cmbWeatherSelect.SelectedIndex = 0;

            Button btnSetWeather = new Button
            {
                Text = "Apply Weather to Map",
                Size = new Size(160, 26),
                BackColor = ModernTheme.Blue600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(465, 32)
            };
            btnSetWeather.FlatAppearance.BorderSize = 0;
            btnSetWeather.Click += (s, e) =>
            {
                ushort map = (ushort)numWeatherMapId.Value;
                WeatherType w = (WeatherType)cmbWeatherSelect.SelectedIndex;
                ExperimentalFeaturesManager.Instance.DynamicWeather.SetWeather(map, w);
                RefreshWeatherGrid();
            };

            topPanel.Controls.Add(lblWeatherTimeBadge);
            topPanel.Controls.Add(btnAdvanceTime);
            topPanel.Controls.Add(lblMap);
            topPanel.Controls.Add(numWeatherMapId);
            topPanel.Controls.Add(cmbWeatherSelect);
            topPanel.Controls.Add(btnSetWeather);

            dgvWeatherMaps = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvWeatherMaps.Columns.Add("colMapId", "Map ID");
            dgvWeatherMaps.Columns.Add("colWeather", "Atmospheric Weather");
            dgvWeatherMaps.Columns.Add("colChanged", "Last Shifted");

            dgvWeatherMaps.Columns["colMapId"].Width = 120;
            dgvWeatherMaps.Columns["colWeather"].Width = 200;
            dgvWeatherMaps.Columns["colChanged"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvWeatherMaps);
            page.Controls.Add(topPanel);
            return page;
        }

        private void RefreshWeatherGrid()
        {
            if (dgvWeatherMaps == null) return;
            dgvWeatherMaps.Rows.Clear();

            var mgr = ExperimentalFeaturesManager.Instance.DynamicWeather;
            var states = mgr.GetAllWeatherStates();

            foreach (var st in states)
            {
                dgvWeatherMaps.Rows.Add(st.MapId, st.Weather, $"{st.ChangedAt:HH:mm:ss}");
            }

            if (lblWeatherTimeBadge != null)
            {
                lblWeatherTimeBadge.Text = $"Current Ambient Time: {mgr.CurrentTimeOfDay}";
            }

            if (ext_lblExpMetricWeather != null)
            {
                ext_lblExpMetricWeather.Text = $"Time: {mgr.CurrentTimeOfDay}";
            }
        }
        #endregion

        #region Sub-Tab 16: Vehicle Transport Fleet (AC 84/86)
        private TabPage BuildVehiclesSubTab()
        {
            TabPage page = new TabPage("Vehicle Fleet (AC 84/86)");
            page.BackColor = Color.White;

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 260
            };

            // Top: 29-Vehicle Fleet Catalog
            Panel pnlTop = new Panel { Dock = DockStyle.Fill };
            Label lblCat = new Label { Text = "29-Vehicle Fleet Taxonomy (TrafficSetting)", Dock = DockStyle.Top, Height = 25, Font = ModernTheme.FontBold, ForeColor = ModernTheme.Slate800 };
            dgvVehicleFleet = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvVehicleFleet.Columns.Add("colVehId", "Item ID");
            dgvVehicleFleet.Columns.Add("colVehName", "Vehicle Model");
            dgvVehicleFleet.Columns.Add("colDomain", "Terrain Domain");
            dgvVehicleFleet.Columns.Add("colFuel", "Max Fuel");
            dgvVehicleFleet.Columns.Add("colSpeed", "Speed Modifier");
            dgvVehicleFleet.Columns.Add("colSeats", "Seats");

            dgvVehicleFleet.Columns["colVehId"].Width = 80;
            dgvVehicleFleet.Columns["colVehName"].Width = 180;
            dgvVehicleFleet.Columns["colDomain"].Width = 130;
            dgvVehicleFleet.Columns["colFuel"].Width = 90;
            dgvVehicleFleet.Columns["colSpeed"].Width = 110;
            dgvVehicleFleet.Columns["colSeats"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            pnlTop.Controls.Add(dgvVehicleFleet);
            pnlTop.Controls.Add(lblCat);

            // Bottom: Active Mounts
            Panel pnlBot = new Panel { Dock = DockStyle.Fill };
            Label lblAct = new Label { Text = "Active Mounted Operators & Passengers", Dock = DockStyle.Top, Height = 25, Font = ModernTheme.FontBold, ForeColor = ModernTheme.Slate800 };
            dgvActiveMounts = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvActiveMounts.Columns.Add("colDriver", "Pilot");
            dgvActiveMounts.Columns.Add("colMount", "Mounted Vehicle");
            dgvActiveMounts.Columns.Add("colCurFuel", "Fuel Gauge");
            dgvActiveMounts.Columns.Add("colDur", "Durability");
            dgvActiveMounts.Columns.Add("colPassengers", "Passengers");

            dgvActiveMounts.Columns["colDriver"].Width = 130;
            dgvActiveMounts.Columns["colMount"].Width = 160;
            dgvActiveMounts.Columns["colCurFuel"].Width = 100;
            dgvActiveMounts.Columns["colDur"].Width = 90;
            dgvActiveMounts.Columns["colPassengers"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            pnlBot.Controls.Add(dgvActiveMounts);
            pnlBot.Controls.Add(lblAct);

            split.Panel1.Controls.Add(pnlTop);
            split.Panel2.Controls.Add(pnlBot);
            page.Controls.Add(split);
            return page;
        }

        private void RefreshVehiclesGrid()
        {
            if (dgvVehicleFleet == null) return;
            dgvVehicleFleet.Rows.Clear();

            var defs = ExperimentalFeaturesManager.Instance.VehicleTransport.GetAllDefinitions();
            foreach (var d in defs)
            {
                dgvVehicleFleet.Rows.Add(d.VehicleItemId, d.Name, d.Domain, $"{d.MaxFuel} L", $"+{d.MaxSpeedModifier}%", $"{d.PassengerCapacity} Seats");
            }

            if (dgvActiveMounts == null) return;
            dgvActiveMounts.Rows.Clear();

            var mounts = ExperimentalFeaturesManager.Instance.VehicleTransport.GetAllActiveMounts();
            foreach (var m in mounts)
            {
                dgvActiveMounts.Rows.Add(m.DriverName, m.VehicleName, $"{m.CurrentFuel}/{m.MaxFuel}", $"{m.Durability}%", $"{m.PassengerCharIds.Count} passengers");
            }
        }
        #endregion

        #region Sub-Tab 17: ATM Banking Vault (AC 26)
        private TabPage BuildBankingSubTab()
        {
            TabPage page = new TabPage("ATM Banking (AC 26)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnSimDeposit = new Button
            {
                Text = "Simulate Gold Deposit (20,000G)",
                Size = new Size(210, 28),
                BackColor = ModernTheme.Emerald600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnSimDeposit.FlatAppearance.BorderSize = 0;
            btnSimDeposit.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 5501, CharName = "Vault Customer" };
                    dummy.Gold = 100000;
                    bool ok = ExperimentalFeaturesManager.Instance.AtmBanking.Deposit(dummy, 20000);
                    RefreshBankingGrid();
                    if (ok)
                        MessageBox.Show($"Deposited 20,000G into Vault for {dummy.CharName}! Remaining cash: {dummy.Gold:N0}G", "Deposit Confirmed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Deposit error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Button btnSimTransfer = new Button
            {
                Text = "Simulate Wire Transfer (5,000G)",
                Size = new Size(210, 28),
                BackColor = ModernTheme.Blue600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(230, 8)
            };
            btnSimTransfer.FlatAppearance.BorderSize = 0;
            btnSimTransfer.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 5501, CharName = "Vault Customer" };
                    bool ok = ExperimentalFeaturesManager.Instance.AtmBanking.Transfer(dummy, 5502, 5000);
                    RefreshBankingGrid();
                    if (ok)
                        MessageBox.Show($"Wired 5,000G from {dummy.CharName} to Citizen #5502 (Fee: 50G)!", "Wire Transfer Confirmed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Wire transfer error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(btnSimDeposit);
            toolbar.Controls.Add(btnSimTransfer);

            dgvBankAccounts = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvBankAccounts.Columns.Add("colCharId", "Char ID");
            dgvBankAccounts.Columns.Add("colOwner", "Account Holder");
            dgvBankAccounts.Columns.Add("colBalance", "Vault Balance");
            dgvBankAccounts.Columns.Add("colTrans", "Total Transactions");
            dgvBankAccounts.Columns.Add("colLastActive", "Last Active");

            dgvBankAccounts.Columns["colCharId"].Width = 90;
            dgvBankAccounts.Columns["colOwner"].Width = 160;
            dgvBankAccounts.Columns["colBalance"].Width = 140;
            dgvBankAccounts.Columns["colTrans"].Width = 130;
            dgvBankAccounts.Columns["colLastActive"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvBankAccounts);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshBankingGrid()
        {
            if (dgvBankAccounts == null) return;
            dgvBankAccounts.Rows.Clear();

            var accounts = ExperimentalFeaturesManager.Instance.AtmBanking.GetAccounts();
            foreach (var a in accounts)
            {
                dgvBankAccounts.Rows.Add(a.CharId, a.OwnerName, $"{a.BalanceGold:N0}G", a.TotalTransactions, $"{a.LastTransactionTime:HH:mm:ss}");
            }

            if (ext_lblExpMetricBank != null)
            {
                ext_lblExpMetricBank.Text = $"Vault: {ExperimentalFeaturesManager.Instance.AtmBanking.TotalVaultedGold:N0}G";
            }
        }
        #endregion

        #region Sub-Tab 18: Prestige Titles & Achievements (AC 40)
        private TabPage BuildTitlesSubTab()
        {
            TabPage page = new TabPage("Prestige Titles (AC 40)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnSimUnlockTitle = new Button
            {
                Text = "Simulate Title Unlock & Equip",
                Size = new Size(210, 28),
                BackColor = ModernTheme.Blue600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnSimUnlockTitle.FlatAppearance.BorderSize = 0;
            btnSimUnlockTitle.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 3301, CharName = "Title Champion" };
                    var mgr = ExperimentalFeaturesManager.Instance.PlayerTitles;
                    mgr.UnlockTitle(dummy, 2); // Beast Vanquisher
                    mgr.EquipTitle(dummy, 2);
                    RefreshTitlesGrid();
                    MessageBox.Show($"Unlocked and equipped Title #2 'Beast Vanquisher' (+15 ATK, +5 SPD, +100 HP) on {dummy.CharName}!", "Title Synchronized", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Title error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(btnSimUnlockTitle);

            dgvTitleCatalog = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvTitleCatalog.Columns.Add("colTitleId", "ID");
            dgvTitleCatalog.Columns.Add("colTitleName", "Prestige Title");
            dgvTitleCatalog.Columns.Add("colAtk", "ATK");
            dgvTitleCatalog.Columns.Add("colDef", "DEF");
            dgvTitleCatalog.Columns.Add("colSpd", "SPD");
            dgvTitleCatalog.Columns.Add("colHp", "Max HP");
            dgvTitleCatalog.Columns.Add("colDesc", "Achievement Requirement");

            dgvTitleCatalog.Columns["colTitleId"].Width = 60;
            dgvTitleCatalog.Columns["colTitleName"].Width = 180;
            dgvTitleCatalog.Columns["colAtk"].Width = 60;
            dgvTitleCatalog.Columns["colDef"].Width = 60;
            dgvTitleCatalog.Columns["colSpd"].Width = 60;
            dgvTitleCatalog.Columns["colHp"].Width = 70;
            dgvTitleCatalog.Columns["colDesc"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvTitleCatalog);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshTitlesGrid()
        {
            if (dgvTitleCatalog == null) return;
            dgvTitleCatalog.Rows.Clear();

            var catalog = ExperimentalFeaturesManager.Instance.PlayerTitles.GetCatalog();
            foreach (var t in catalog)
            {
                dgvTitleCatalog.Rows.Add(t.TitleId, t.TitleName, $"+{t.AtkBonus}", $"+{t.DefBonus}", $"+{t.SpdBonus}", $"+{t.HpBonus}", t.Description);
            }
        }
        #endregion

        #region Sub-Tab 19: Mining & Geological Excavation (AC 90)
        private TabPage BuildExcavationSubTab()
        {
            TabPage page = new TabPage("Mining & Digging (AC 90)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnSimDig = new Button
            {
                Text = "Simulate Geological Excavation",
                Size = new Size(210, 28),
                BackColor = ModernTheme.Amber600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnSimDig.FlatAppearance.BorderSize = 0;
            btnSimDig.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 2201, CharName = "Deep Miner" };
                    var res = ExperimentalFeaturesManager.Instance.TreasureExcavation.PerformExcavation(dummy);
                    RefreshExcavationGrid();
                    if (res != null)
                        MessageBox.Show($"Excavated at {res.DepthMeters}m depth: {res.Quantity}x {res.ItemName} {(res.IsTreasureChest ? "[RARE CHEST!]" : "")}", "Excavation Struck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Excavation error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(btnSimDig);

            dgvExcavationFinds = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvExcavationFinds.Columns.Add("colMiner", "Miner");
            dgvExcavationFinds.Columns.Add("colMap", "Map ID");
            dgvExcavationFinds.Columns.Add("colDepth", "Depth (m)");
            dgvExcavationFinds.Columns.Add("colOre", "Extracted Ore / Antiquity");
            dgvExcavationFinds.Columns.Add("colQty", "Quantity");
            dgvExcavationFinds.Columns.Add("colChest", "Antiquity Chest");
            dgvExcavationFinds.Columns.Add("colTime", "Timestamp");

            dgvExcavationFinds.Columns["colMiner"].Width = 140;
            dgvExcavationFinds.Columns["colMap"].Width = 80;
            dgvExcavationFinds.Columns["colDepth"].Width = 90;
            dgvExcavationFinds.Columns["colOre"].Width = 220;
            dgvExcavationFinds.Columns["colQty"].Width = 80;
            dgvExcavationFinds.Columns["colChest"].Width = 120;
            dgvExcavationFinds.Columns["colTime"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvExcavationFinds);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshExcavationGrid()
        {
            if (dgvExcavationFinds == null) return;
            dgvExcavationFinds.Rows.Clear();

            var finds = ExperimentalFeaturesManager.Instance.TreasureExcavation.GetRecentFinds();
            foreach (var f in finds)
            {
                dgvExcavationFinds.Rows.Add(f.MinerName, f.MapId, $"{f.DepthMeters:F1}m", f.ItemName, f.Quantity, f.IsTreasureChest ? "Yes" : "No", $"{f.Timestamp:HH:mm:ss}");
            }
        }
        #endregion

        #region Sub-Tab 20: Carnival Lottery & Gacha Toy (AC 76)
        private TabPage BuildLotterySubTab()
        {
            TabPage page = new TabPage("Carnival Lottery (AC 76)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnSimSpinWheel = new Button
            {
                Text = "Spin Fortune Wheel (1,000G)",
                Size = new Size(210, 28),
                BackColor = ModernTheme.Rose600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnSimSpinWheel.FlatAppearance.BorderSize = 0;
            btnSimSpinWheel.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 1101, CharName = "Carnival Gambler" };
                    dummy.Gold = 50000;
                    var res = ExperimentalFeaturesManager.Instance.LotteryGacha.SpinWheel(dummy, 1000);
                    RefreshLotteryGrid();
                    if (res != null)
                        MessageBox.Show($"Wheel halted on Slice {res.SliceIndex}! Prize: {res.Quantity}x {res.PrizeName} {(res.IsJackpot ? "[GRAND JACKPOT!]" : "")}", "Lottery Spin Concluded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lottery spin error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(btnSimSpinWheel);

            dgvLotterySpins = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvLotterySpins.Columns.Add("colPlayer", "Contestant");
            dgvLotterySpins.Columns.Add("colSlice", "Wheel Slice");
            dgvLotterySpins.Columns.Add("colPrize", "Prize Item Awarded");
            dgvLotterySpins.Columns.Add("colQty", "Quantity");
            dgvLotterySpins.Columns.Add("colJackpot", "Jackpot Won");
            dgvLotterySpins.Columns.Add("colTime", "Timestamp");

            dgvLotterySpins.Columns["colPlayer"].Width = 140;
            dgvLotterySpins.Columns["colSlice"].Width = 90;
            dgvLotterySpins.Columns["colPrize"].Width = 240;
            dgvLotterySpins.Columns["colQty"].Width = 80;
            dgvLotterySpins.Columns["colJackpot"].Width = 110;
            dgvLotterySpins.Columns["colTime"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvLotterySpins);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshLotteryGrid()
        {
            if (dgvLotterySpins == null) return;
            dgvLotterySpins.Rows.Clear();

            var spins = ExperimentalFeaturesManager.Instance.LotteryGacha.GetHistory();
            foreach (var s in spins)
            {
                dgvLotterySpins.Rows.Add(s.PlayerName, $"Slice #{s.SliceIndex}", s.PrizeName, s.Quantity, s.IsJackpot ? "JACKPOT!" : "Normal", $"{s.SpinTime:HH:mm:ss}");
            }

            if (ext_lblExpMetricLottery != null)
            {
                ext_lblExpMetricLottery.Text = $"Jackpot: {ExperimentalFeaturesManager.Instance.LotteryGacha.ProgressiveJackpotPool:N0}G";
            }
        }
        #endregion

        #region Sub-Tab 21: Tent Housekeeper & Maid (AC 62)
        private TabPage BuildMaidSubTab()
        {
            TabPage page = new TabPage("Tent Housekeeper (AC 62)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnSimAutoCook = new Button
            {
                Text = "Simulate Maid Meal Service",
                Size = new Size(210, 28),
                BackColor = ModernTheme.Emerald600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnSimAutoCook.FlatAppearance.BorderSize = 0;
            btnSimAutoCook.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 4401, CharName = "Tent Landlord" };
                    bool ok = ExperimentalFeaturesManager.Instance.TentMaidButler.TriggerAutoCook(dummy);
                    RefreshMaidGrid();
                    if (ok)
                        MessageBox.Show($"Housekeeper served warm restorative stew to {dummy.CharName}!", "Meal Prepared", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Auto-cook error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(btnSimAutoCook);

            dgvMaidAssistants = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvMaidAssistants.Columns.Add("colMaster", "Tent Master");
            dgvMaidAssistants.Columns.Add("colAssistant", "Assistant Name");
            dgvMaidAssistants.Columns.Add("colModel", "Model ID");
            dgvMaidAssistants.Columns.Add("colMeals", "Meals Cooked");
            dgvMaidAssistants.Columns.Add("colAuto", "Auto-Cook");
            dgvMaidAssistants.Columns.Add("colGreeting", "Welcome Greeting");

            dgvMaidAssistants.Columns["colMaster"].Width = 140;
            dgvMaidAssistants.Columns["colAssistant"].Width = 150;
            dgvMaidAssistants.Columns["colModel"].Width = 80;
            dgvMaidAssistants.Columns["colMeals"].Width = 100;
            dgvMaidAssistants.Columns["colAuto"].Width = 90;
            dgvMaidAssistants.Columns["colGreeting"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvMaidAssistants);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshMaidGrid()
        {
            if (dgvMaidAssistants == null) return;
            dgvMaidAssistants.Rows.Clear();

            var assistants = ExperimentalFeaturesManager.Instance.TentMaidButler.GetAssistants();
            foreach (var a in assistants)
            {
                dgvMaidAssistants.Rows.Add(a.OwnerName, a.AssistantName, a.ModelId, a.ServicedMealsCount, a.AutoCookEnabled ? "Enabled" : "Disabled", a.WelcomeGreeting);
            }
        }
        #endregion

        #region Sub-Tab 22: Tactical Combat Status & Buffs (AC 114)
        private TabPage BuildCombatStatusSubTab()
        {
            TabPage page = new TabPage("Combat Status & Buffs (AC 114)");
            page.BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(10, 8, 10, 8) };

            Button btnSimFreeze = new Button
            {
                Text = "Afflict Freeze CC (3 Turns)",
                Size = new Size(190, 28),
                BackColor = ModernTheme.Blue600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(10, 8)
            };
            btnSimFreeze.FlatAppearance.BorderSize = 0;
            btnSimFreeze.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 7001, CharName = "Arena Challenger" };
                    bool ok = ExperimentalFeaturesManager.Instance.CombatStatus.AfflictStatus(dummy, StatusConditionType.Freeze, 3);
                    RefreshCombatStatusGrid();
                    if (ok)
                        MessageBox.Show($"Afflicted {dummy.CharName} with Freeze for 3 turns! CanAct: {ExperimentalFeaturesManager.Instance.CombatStatus.CanAct(dummy.CharID)}", "Status Afflicted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Afflict error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Button btnSimDecay = new Button
            {
                Text = "Simulate Turn Decay Tick",
                Size = new Size(190, 28),
                BackColor = ModernTheme.Slate700,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBoldSmall,
                Location = new Point(210, 8)
            };
            btnSimDecay.FlatAppearance.BorderSize = 0;
            btnSimDecay.Click += (s, e) =>
            {
                try
                {
                    var dummy = new Player { CharID = 7001, CharName = "Arena Challenger" };
                    var expired = ExperimentalFeaturesManager.Instance.CombatStatus.ProcessTurnDecay(dummy);
                    RefreshCombatStatusGrid();
                    MessageBox.Show($"Turn decay evaluated for {dummy.CharName}. Expired effects: {expired.Count}", "Decay Tick Processed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Decay error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            toolbar.Controls.Add(btnSimFreeze);
            toolbar.Controls.Add(btnSimDecay);

            dgvCombatStatusEffects = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                Font = ModernTheme.FontDefault,
                RowHeadersVisible = false
            };
            dgvCombatStatusEffects.Columns.Add("colTarget", "Target Fighter");
            dgvCombatStatusEffects.Columns.Add("colCondition", "Condition / Buff");
            dgvCombatStatusEffects.Columns.Add("colTurns", "Remaining Turns");
            dgvCombatStatusEffects.Columns.Add("colPotency", "Potency");
            dgvCombatStatusEffects.Columns.Add("colApplied", "Applied Time");

            dgvCombatStatusEffects.Columns["colTarget"].Width = 150;
            dgvCombatStatusEffects.Columns["colCondition"].Width = 160;
            dgvCombatStatusEffects.Columns["colTurns"].Width = 120;
            dgvCombatStatusEffects.Columns["colPotency"].Width = 90;
            dgvCombatStatusEffects.Columns["colApplied"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            page.Controls.Add(dgvCombatStatusEffects);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshCombatStatusGrid()
        {
            if (dgvCombatStatusEffects == null) return;
            dgvCombatStatusEffects.Rows.Clear();

            var effects = ExperimentalFeaturesManager.Instance.CombatStatus.GetActiveEffects();
            foreach (var ef in effects)
            {
                dgvCombatStatusEffects.Rows.Add(ef.TargetName, ef.Type.ToString(), $"{ef.RemainingTurns} turns", ef.Potency, $"{ef.AppliedAt:HH:mm:ss}");
            }
        }
        #endregion

        private void RefreshExperimentalTab()
        {
            try
            {
                // Wave 1
                RefreshDungeonsGrid();
                RefreshArenaGrids();
                RefreshSiegeControls();
                RefreshArcadeGrid();
                RefreshRacesGrid();

                // Wave 2
                RefreshBulletinGrid();
                RefreshCharityGrid();
                RefreshGobangGrid();
                RefreshFishingGrid();
                RefreshHotSpringGrid();
                RefreshPinVaultGrid();
                RefreshBazaarGrid();
                RefreshWeatherGrid();
                RefreshVehiclesGrid();

                // Wave 3
                RefreshBankingGrid();
                RefreshTitlesGrid();
                RefreshExcavationGrid();
                RefreshLotteryGrid();
                RefreshMaidGrid();
                RefreshCombatStatusGrid();
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
