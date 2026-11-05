using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Wonderland_Private_Server
{
    public partial class Form1
    {
        private class NavItem
        {
            public string Label { get; }
            public Func<TabPage> ResolveTab { get; }
            /// <summary>Page inside the Experimental host to select, if any.</summary>
            public TabPage InnerPage { get; }
            private readonly Func<bool> isVisible;

            public NavItem(string label, Func<TabPage> resolveTab, TabPage innerPage = null, Func<bool> isVisible = null)
            {
                Label = label;
                ResolveTab = resolveTab;
                InnerPage = innerPage;
                this.isVisible = isVisible;
            }

            public bool IsVisible => isVisible == null || isVisible();
        }

        private class NavCategory
        {
            public string Name { get; }
            public List<NavItem> Items { get; }

            public NavCategory(string name, List<NavItem> items)
            {
                Name = name;
                Items = items;
            }
        }

        private Panel modernHeaderPanel;
        private Panel modernCategoryBar;
        private FlowLayoutPanel modernCatFlow;
        private Panel modernSubTabBar;
        private FlowLayoutPanel modernSubTabFlow;
        private Label modernLblStatusPill;
        private Label modernLblMetrics;
        private TextBox modernTxtQuickSearch;
        private Button modernBtnShutdownHeader;
        private Timer modernMetricsTimer;
        private Timer modernCheatDebounceTimer;
        private Action modernCheatDebounceAction;
        private List<NavCategory> modernCategories;
        private NavCategory modernActiveCategory;
        private readonly Dictionary<NavCategory, Button> modernCategoryButtons = new Dictionary<NavCategory, Button>();
        private readonly Dictionary<NavItem, Button> modernSubTabButtons = new Dictionary<NavItem, Button>();
        private Button modernActiveSubTabButton;
        private bool modernLogAutoScroll = true;
        private Button modernBtnAutoScroll;
        private int modernLogSearchIndex = 0;
        private bool modernIsInternalTabSwitch = false;

        public void InitializeModernUi()
        {
            try
            {
                this.SuspendLayout();

                // Unparent tabControl3 from legacy outer tabControl1/tabPage6
                if (this.tabPage6 != null && this.tabControl3 != null)
                {
                    this.tabPage6.Controls.Remove(this.tabControl3);
                }
                if (this.tabControl1 != null)
                {
                    this.Controls.Remove(this.tabControl1);
                }

                // Configure tabControl3 to operate cleanly as full-width content host without multiline jumping
                if (this.tabControl3 != null)
                {
                    this.tabControl3.Dock = DockStyle.Fill;
                    this.tabControl3.Appearance = TabAppearance.FlatButtons;
                    this.tabControl3.ItemSize = new Size(0, 1);
                    this.tabControl3.SizeMode = TabSizeMode.Fixed;
                }

                // Build modern executive header and two-tier categorized navigation
                BuildHeaderPanel();
                BuildCategoryNavigation();
                HookFeatureToggles();
                BuildLogUtilityBar();
                EnhanceCheatTabControls();

                // Add controls to Form in proper Z-order
                if (this.tabControl3 != null)
                {
                    this.Controls.Add(this.tabControl3);
                }
                if (this.modernSubTabBar != null)
                {
                    this.Controls.Add(this.modernSubTabBar);
                }
                if (this.modernCategoryBar != null)
                {
                    this.Controls.Add(this.modernCategoryBar);
                }
                if (this.modernHeaderPanel != null)
                {
                    this.Controls.Add(this.modernHeaderPanel);
                }

                // Ensure proper docking layering
                if (this.modernHeaderPanel != null) this.modernHeaderPanel.BringToFront();
                if (this.modernCategoryBar != null) this.modernCategoryBar.BringToFront();
                if (this.modernSubTabBar != null) this.modernSubTabBar.BringToFront();
                BuildServerStatusBar();
                this.Controls.Add(st_statusBar);
                st_statusBar.BringToFront();
                if (this.tabControl3 != null) this.tabControl3.BringToFront();

                // Apply global modern theme across all controls (hand-built chrome keeps its own look)
                ModernTheme.Exclude(this.modernHeaderPanel);
                ModernTheme.Exclude(this.modernCategoryBar);
                ModernTheme.Exclude(this.modernSubTabBar);
                ApplyModernTheme(this);

                // Two-way synchronization: when tabControl3.SelectedTab changes, update category & sub-tab highlights
                if (this.tabControl3 != null)
                {
                    this.tabControl3.SelectedIndexChanged += (s, e) =>
                    {
                        if (modernIsInternalTabSwitch) return;
                        if (this.tabControl3.SelectedTab != null)
                        {
                            SyncNavToTab(this.tabControl3.SelectedTab);
                        }
                    };
                }

                // Mirror shutdown countdown text from btnSafeShutdown to modernBtnShutdownHeader
                if (this.btnSafeShutdown != null)
                {
                    this.btnSafeShutdown.TextChanged += (s, e) =>
                    {
                        if (modernBtnShutdownHeader != null)
                        {
                            modernBtnShutdownHeader.Text = this.btnSafeShutdown.Text;
                            modernBtnShutdownHeader.Enabled = this.btnSafeShutdown.Enabled;
                        }
                    };
                }

                // Start real-time metrics timer
                modernMetricsTimer = new Timer();
                modernMetricsTimer.Interval = 1000;
                modernMetricsTimer.Tick += (s, e) => UpdateModernMetrics();
                modernMetricsTimer.Start();
                UpdateModernMetrics();

                // Select default initial category and tab
                if (modernCategories != null && modernCategories.Count > 0)
                {
                    SelectCategory(modernCategories[0]);
                }

                this.ResumeLayout(true);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, "[ModernUI] Initialization warning: " + ex.Message);
            }
        }

        private void BuildHeaderPanel()
        {
            modernHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = ModernTheme.Slate900,
                Padding = new Padding(12, 4, 12, 4)
            };

            // Server Logo / Title
            Label lblTitle = new Label
            {
                Text = "WLO PRIVATE SERVER",
                Font = ModernTheme.FontHeading,
                ForeColor = Color.White,
                Location = new Point(12, 6),
                AutoSize = true
            };

            Label lblSubtitle = new Label
            {
                Text = "Administrator Console v2.0",
                Font = ModernTheme.FontSmall,
                ForeColor = ModernTheme.Slate400,
                Location = new Point(13, 27),
                AutoSize = true
            };

            // Status Badge
            modernLblStatusPill = new Label
            {
                Text = "\u25CF  ONLINE",
                Font = ModernTheme.FontBoldSmall,
                ForeColor = Color.FromArgb(52, 211, 153), // Emerald-400
                BackColor = Color.FromArgb(6, 78, 59), // Emerald-900
                Location = new Point(205, 12),
                AutoSize = false,
                Size = new Size(84, 24),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Metrics Label
            modernLblMetrics = new Label
            {
                Text = "Players 0   \u00B7   Uptime 00:00:00   \u00B7   RAM 0 MB   \u00B7   Ports 6414 / 6415 / 6416",
                Font = ModernTheme.FontSmall,
                ForeColor = ModernTheme.Slate300,
                Location = new Point(302, 16),
                AutoSize = true
            };

            // Right Action Controls Panel
            Panel rightActionsPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 475,
                BackColor = Color.Transparent
            };

            // Quick Search Box
            modernTxtQuickSearch = new TextBox
            {
                Location = new Point(8, 12),
                Width = 162,
                Font = ModernTheme.FontDefault,
                BackColor = ModernTheme.Slate800,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            ModernTheme.SetPlaceholder(modernTxtQuickSearch, "Jump to tab...  (Ctrl+K)");
            modernTxtQuickSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    ExecuteQuickSearchJump(modernTxtQuickSearch.Text.Trim());
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            // Launch Client Button (F5)
            Button btnLaunchClient = new Button
            {
                Text = "Launch Client (F5)",
                Location = new Point(178, 10),
                Size = new Size(130, 28),
                Font = ModernTheme.FontBoldSmall,
                BackColor = ModernTheme.Emerald600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnLaunchClient.FlatAppearance.BorderSize = 0;
            btnLaunchClient.FlatAppearance.MouseOverBackColor = ModernTheme.Emerald700;
            btnLaunchClient.Cursor = Cursors.Hand;
            btnLaunchClient.Click += (s, e) => RunClientProgram();

            // Save All Button
            Button btnSaveHeader = new Button
            {
                Text = "Save All",
                Location = new Point(314, 10),
                Size = new Size(72, 28),
                Font = ModernTheme.FontBoldSmall,
                BackColor = ModernTheme.Blue600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSaveHeader.FlatAppearance.BorderSize = 0;
            btnSaveHeader.FlatAppearance.MouseOverBackColor = ModernTheme.Blue700;
            btnSaveHeader.Cursor = Cursors.Hand;
            btnSaveHeader.Click += (s, e) => btnSaveAllNow_Click(s, e);

            // Safe Shutdown Button
            modernBtnShutdownHeader = new Button
            {
                Text = "Shutdown",
                Location = new Point(392, 10),
                Size = new Size(78, 28),
                Font = ModernTheme.FontBoldSmall,
                BackColor = ModernTheme.Rose600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            modernBtnShutdownHeader.FlatAppearance.BorderSize = 0;
            modernBtnShutdownHeader.FlatAppearance.MouseOverBackColor = ModernTheme.Rose700;
            modernBtnShutdownHeader.Cursor = Cursors.Hand;
            modernBtnShutdownHeader.Click += (s, e) => btnSafeShutdown_Click(s, e);

            rightActionsPanel.Controls.Add(modernTxtQuickSearch);
            rightActionsPanel.Controls.Add(btnLaunchClient);
            rightActionsPanel.Controls.Add(btnSaveHeader);
            rightActionsPanel.Controls.Add(modernBtnShutdownHeader);

            modernHeaderPanel.Controls.Add(rightActionsPanel);
            modernHeaderPanel.Controls.Add(lblTitle);
            modernHeaderPanel.Controls.Add(lblSubtitle);
            modernHeaderPanel.Controls.Add(modernLblStatusPill);
            modernHeaderPanel.Controls.Add(modernLblMetrics);
        }

        private void BuildCategoryNavigation()
        {
            // Category Bar
            modernCategoryBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = ModernTheme.Slate800,
                Padding = new Padding(10, 5, 10, 5)
            };

            modernCatFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            modernCategoryBar.Controls.Add(modernCatFlow);

            // Sub-Tab Bar
            modernSubTabBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.White,
                Padding = new Padding(10, 5, 10, 5)
            };
            modernSubTabBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(ModernTheme.BorderColor))
                {
                    e.Graphics.DrawLine(pen, 0, modernSubTabBar.Height - 1, modernSubTabBar.Width, modernSubTabBar.Height - 1);
                }
            };

            modernSubTabFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            modernSubTabBar.Controls.Add(modernSubTabFlow);

            SetupCategoriesData();
            RenderCategoryButtons();
        }

        private void SetupCategoriesData()
        {
            modernCategories = new List<NavCategory>
            {
                new NavCategory("Server", new List<NavItem>
                {
                    new NavItem("Dashboard & Logs", () => this.tabPage7),
                    new NavItem("Online Sessions", () => FindTabByTitle("Online Sessions")),
                    new NavItem("Security & Bans", () => FindTabByTitle("Security & Bans")),
                    new NavItem("Activity", () => tabActivity),
                    new NavItem("Announcements", () => tabAnnouncements),
                    new NavItem("Database", () => FindTabByTitle("Database Config")),
                    new NavItem("Backups", () => tabBackups)
                }),
                new NavCategory("Players", new List<NavItem>
                {
                    new NavItem("Characters", () => this.tabPageCharacters),
                    new NavItem("Accounts", () => this.tabPageUsers),
                    new NavItem("Inventory", () => this.tabPageInventory),
                    new NavItem("Stats", () => this.tabPageStats),
                    new NavItem("Player Settings", () => this.tabPageSettings)
                }),
                new NavCategory("Social", new List<NavItem>
                {
                    new NavItem("Friends", () => this.tabPageFriends),
                    new NavItem("Guilds", () => FindTabByTitle("Guilds")),
                    new NavItem("Marriages", () => FindTabByTitle("Marriages")),
                    new NavItem("In-Game Mail", () => FindTabByTitle("In-Game Mail"))
                }),
                new NavCategory("GM Tools", new List<NavItem>
                {
                    new NavItem("GM Studio", () => FindTabByTitle("GM Studio")),
                    new NavItem("Quick Cheats", () => this.tabPage1),
                    new NavItem("Live Battles", () => FindTabByTitle("Live Battles"))
                }),
                new NavCategory("World", new List<NavItem>
                {
                    new NavItem("Live Map & NPCs", () => this.tabMapNpcStudio ?? FindTabByTitle("Map NPCs & Events")),
                    new NavItem("Portals", () => this.tabPagePortals),
                    new NavItem("NPC Names", () => this.tabNpcResolver ?? FindTabByTitle("NPC Name Resolver")),
                    new NavItem("Dialogue IDs", () => this.tabTalkResolver ?? FindTabByTitle("Talk ID Resolver"))
                }),
                new NavCategory("Economy", new List<NavItem>
                {
                    new NavItem("Item Mall", () => FindTabByTitle("Item Mall")),
                    new NavItem("Monster Drops", () => FindTabByTitle("Monster Drops")),
                    new NavItem("Chest Drops", () => this.tabPageChestDrops),
                    new NavItem("Starter Items", () => FindTabByTitle("Starter Items"))
                })
            };

            // Experimental engines: one nav button per module, grouped into their own categories.
            // A module whose master toggle is off is hidden until it is switched back on.
            foreach (string group in new[] { "Events", "Lifestyle", "Lab" })
            {
                var items = expModules
                    .Where(m => m.Group == group)
                    .Select(m => new NavItem(m.Label, () => this.tabPageExperimental, m.Page, () => m.IsEnabled))
                    .ToList();
                if (items.Count > 0) modernCategories.Add(new NavCategory(group, items));
            }

            // Catch any unmapped tabs in tabControl3 so no functionality is ever omitted
            var mappedTabs = new HashSet<TabPage>();
            foreach (var cat in modernCategories)
            {
                foreach (var item in cat.Items)
                {
                    TabPage t = item.ResolveTab();
                    if (t != null) mappedTabs.Add(t);
                }
            }

            if (this.tabControl3 != null)
            {
                var unmapped = this.tabControl3.TabPages.Cast<TabPage>().Where(t => !mappedTabs.Contains(t)).ToList();
                if (unmapped.Count > 0)
                {
                    var extraItems = unmapped.Select(t => new NavItem(t.Text.Trim(), () => t)).ToList();
                    modernCategories.Add(new NavCategory("Other Tools", extraItems));
                }
            }
        }

        private bool IsCategoryVisible(NavCategory cat) => cat.Items.Any(i => i.IsVisible && i.ResolveTab() != null);

        private void RenderCategoryButtons()
        {
            modernCatFlow.SuspendLayout();
            modernCatFlow.Controls.Clear();
            modernCategoryButtons.Clear();

            foreach (var cat in modernCategories)
            {
                if (!IsCategoryVisible(cat)) continue;

                Button btn = new Button
                {
                    Text = cat.Name,
                    Height = 30,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(148, 163, 184), // Slate-400
                    BackColor = Color.Transparent,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(0, 0, 4, 0),
                    Padding = new Padding(10, 0, 10, 0),
                    Cursor = Cursors.Hand,
                    UseMnemonic = false,
                    Tag = cat
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(51, 65, 85);

                btn.Click += (s, e) => SelectCategory(cat);

                modernCatFlow.Controls.Add(btn);
                modernCategoryButtons[cat] = btn;
            }

            modernCatFlow.ResumeLayout(true);
        }

        private void SelectCategory(NavCategory cat)
        {
            if (cat == null) return;
            modernActiveCategory = cat;

            // Update category button highlights
            foreach (var kvp in modernCategoryButtons)
            {
                bool isActive = kvp.Key == cat;
                Button btn = kvp.Value;
                btn.BackColor = isActive ? ModernTheme.Blue600 : Color.Transparent;
                btn.ForeColor = isActive ? Color.White : ModernTheme.Slate400;
            }

            // Populate Sub-Tabs for this category
            RenderSubTabButtons(cat);
        }

        private void RenderSubTabButtons(NavCategory cat)
        {
            modernSubTabFlow.SuspendLayout();
            modernSubTabFlow.Controls.Clear();
            modernSubTabButtons.Clear();
            modernActiveSubTabButton = null;

            NavItem firstItem = null;

            foreach (var item in cat.Items)
            {
                if (!item.IsVisible || item.ResolveTab() == null) continue;
                if (firstItem == null) firstItem = item;

                Button subBtn = new Button
                {
                    Text = item.Label,
                    Height = 28,
                    AutoSize = true,
                    Font = ModernTheme.FontSmall,
                    ForeColor = ModernTheme.Slate600,
                    BackColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(0, 1, 4, 1),
                    Padding = new Padding(8, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    UseMnemonic = false,
                    Tag = item
                };
                subBtn.FlatAppearance.BorderSize = 0;
                subBtn.FlatAppearance.MouseOverBackColor = ModernTheme.Slate100;

                NavItem captured = item;
                subBtn.Click += (s, e) => SwitchToItem(captured);

                modernSubTabFlow.Controls.Add(subBtn);
                modernSubTabButtons[item] = subBtn;
            }

            modernSubTabFlow.ResumeLayout(true);

            // Keep the current page if it belongs to this category, otherwise open the first one
            NavItem current = FindItemForCurrentPage(cat);
            if (current != null) HighlightSubTabButton(modernSubTabButtons[current]);
            else if (firstItem != null) SwitchToItem(firstItem);
        }

        /// <summary>The visible item of <paramref name="cat"/> that matches what is on screen now.</summary>
        private NavItem FindItemForCurrentPage(NavCategory cat)
        {
            TabPage selected = this.tabControl3?.SelectedTab;
            if (selected == null) return null;
            TabPage inner = selected == this.tabPageExperimental ? expTabHost?.SelectedTab : null;

            return cat.Items.FirstOrDefault(i =>
                modernSubTabButtons.ContainsKey(i) &&
                i.ResolveTab() == selected &&
                (i.InnerPage == null || i.InnerPage == inner));
        }

        private void SwitchToItem(NavItem item)
        {
            if (item == null) return;
            TabPage tab = item.ResolveTab();
            if (tab == null || this.tabControl3 == null) return;

            modernIsInternalTabSwitch = true;
            try
            {
                this.tabControl3.SelectedTab = tab;
                if (item.InnerPage != null && expTabHost != null)
                {
                    expTabHost.SelectedTab = item.InnerPage;
                    if (expHeaderTitle != null) expHeaderTitle.Text = item.Label.ToUpperInvariant();
                    if (expHeaderSubtitle != null) expHeaderSubtitle.Text = "Experimental  ·  " + item.InnerPage.Text;
                }
            }
            finally
            {
                modernIsInternalTabSwitch = false;
            }

            if (modernSubTabButtons.TryGetValue(item, out Button btn)) HighlightSubTabButton(btn);

            AutoLoadTab(tab);
        }

        // Legacy grids that stay empty until "Refresh" is pressed get filled on their first visit.
        private readonly HashSet<TabPage> modernAutoLoadedTabs = new HashSet<TabPage>();

        private void AutoLoadTab(TabPage tab)
        {
            try
            {
                if (tab == this.tabPageInventory)
                {
                    LoadCharacterFilters();
                    if (cmbCharacterFilter.SelectedIndex >= 0) btnRefreshInventory_Click(null, null);
                    return;
                }
                if (tab == this.tabPageStats)
                {
                    LoadCharacterFilters();
                    if (cmbCharacterFilterStats.SelectedIndex >= 0) btnRefreshStats_Click(null, null);
                    return;
                }

                RefreshServerToolPage(tab);

                if (!modernAutoLoadedTabs.Add(tab)) return;
                if (tab == this.tabPageCharacters) btnRefreshCharacters_Click(null, null);
                else if (tab == this.tabPageUsers) btnRefreshUsers_Click(null, null);
                else if (tab == this.tabPageSettings) btnRefreshSettings_Click(null, null);
                else if (tab == this.tabPageFriends) btnRefreshFriends_Click(null, null);
                else if (tab == this.tabPagePortals) btnRefreshPortals_Click(null, null);
                else if (tab == this.tabPageChestDrops) btnRefreshChestDrops_Click(null, null);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, "[ModernUI] Auto-load failed: " + ex.Message);
            }
        }

        private void HighlightSubTabButton(Button subBtn)
        {
            if (modernActiveSubTabButton != null && modernActiveSubTabButton != subBtn)
            {
                modernActiveSubTabButton.BackColor = Color.White;
                modernActiveSubTabButton.ForeColor = ModernTheme.Slate600;
                modernActiveSubTabButton.Font = ModernTheme.FontSmall;
                modernActiveSubTabButton.FlatAppearance.MouseOverBackColor = ModernTheme.Slate100;
            }

            modernActiveSubTabButton = subBtn;
            if (modernActiveSubTabButton != null)
            {
                modernActiveSubTabButton.BackColor = ModernTheme.Blue50;
                modernActiveSubTabButton.ForeColor = ModernTheme.Blue700;
                modernActiveSubTabButton.Font = ModernTheme.FontBoldSmall;
                modernActiveSubTabButton.FlatAppearance.MouseOverBackColor = ModernTheme.Blue100;
            }
        }

        private void SyncNavToTab(TabPage targetTab)
        {
            if (targetTab == null || modernCategories == null) return;

            foreach (var cat in modernCategories)
            {
                if (cat.Items.Any(i => i.IsVisible && i.ResolveTab() == targetTab))
                {
                    if (modernActiveCategory != cat || !modernCategoryButtons.ContainsKey(cat)) SelectCategory(cat);
                    NavItem current = FindItemForCurrentPage(cat);
                    if (current != null) HighlightSubTabButton(modernSubTabButtons[current]);
                    return;
                }
            }
        }

        /// <summary>
        /// Re-applies feature visibility: hides nav entries and header badges of disabled
        /// experimental engines, and moves away from a page that just became hidden.
        /// </summary>
        private void RefreshNavVisibility()
        {
            if (modernCategories == null) return;

            NavCategory keep = modernActiveCategory;
            RenderCategoryButtons();

            if (keep == null || !IsCategoryVisible(keep))
            {
                keep = modernCategories.FirstOrDefault(IsCategoryVisible);
            }
            SelectCategory(keep);

            UpdateExperimentalBadges();
        }

        private void UpdateExperimentalBadges()
        {
            void show(Label badge, CheckBox toggle)
            {
                if (badge != null) badge.Visible = toggle == null || toggle.Checked;
            }
            show(ext_lblExpMetricDungeons, chkEnableDungeons);
            show(ext_lblExpMetricArena, chkEnablePvPArena);
            show(ext_lblExpMetricSiege, chkEnableGuildSiege);
            show(ext_lblExpMetricMinigames, chkEnableArcadeMinigames);
            show(ext_lblExpMetricBazaar, chkEnableBazaar);
            show(ext_lblExpMetricWeather, chkEnableWeather);
            show(ext_lblExpMetricBank, chkEnableAtmBanking);
            show(ext_lblExpMetricLottery, chkEnableLotteryGacha);
        }

        private void HookFeatureToggles()
        {
            foreach (var module in expModules)
            {
                CheckBox toggle = module.Toggle;
                if (toggle != null) toggle.CheckedChanged += (s, e) => RefreshNavVisibility();
            }
            UpdateExperimentalBadges();
        }

        private void ExecuteQuickSearchJump(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || modernCategories == null) return;

            foreach (var cat in modernCategories)
            {
                foreach (var item in cat.Items)
                {
                    if (!item.IsVisible || item.ResolveTab() == null) continue;
                    if (item.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        SelectCategory(cat);
                        SwitchToItem(item);
                        return;
                    }
                }
            }
        }

        /// <summary>Finds a tab by its exact title, ignoring leading icons/spaces and case.</summary>
        private TabPage FindTabByTitle(string title)
        {
            if (this.tabControl3 == null) return null;
            foreach (TabPage tab in this.tabControl3.TabPages)
            {
                string text = new string((tab.Text ?? string.Empty).SkipWhile(ch => !char.IsLetterOrDigit(ch)).ToArray()).Trim();
                if (string.Equals(text, title, StringComparison.OrdinalIgnoreCase))
                {
                    return tab;
                }
            }
            return null;
        }

        /// <summary>Turns console auto-scroll on/off and keeps the toggle button in sync.</summary>
        private void SetLogAutoScroll(bool on)
        {
            modernLogAutoScroll = on;
            DebugSystem.AutoScroll = on;
            if (modernBtnAutoScroll == null) return;
            modernBtnAutoScroll.Text = on ? "Auto-Scroll: ON" : "Auto-Scroll: OFF";
            modernBtnAutoScroll.BackColor = on ? ModernTheme.Emerald50 : ModernTheme.Rose50;
            modernBtnAutoScroll.ForeColor = on ? ModernTheme.Emerald700 : ModernTheme.Rose700;
        }

        private void UpdateModernMetrics()
        {
            try
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;

                bool isRunning = cGlobal.Run;
                int playerCount = 0;
                try
                {
                    playerCount = cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.Count ?? 0;
                }
                catch { }

                TimeSpan uptime = TimeSpan.Zero;
                long memoryMb = 0;
                try
                {
                    var proc = Process.GetCurrentProcess();
                    uptime = DateTime.Now - proc.StartTime;
                    memoryMb = proc.WorkingSet64 / (1024 * 1024);
                }
                catch { }

                if (modernLblStatusPill != null)
                {
                    if (isRunning)
                    {
                        modernLblStatusPill.Text = "\u25CF  ONLINE";
                        modernLblStatusPill.BackColor = Color.FromArgb(6, 78, 59); // Emerald-900
                        modernLblStatusPill.ForeColor = Color.FromArgb(52, 211, 153); // Emerald-400
                    }
                    else
                    {
                        modernLblStatusPill.Text = "\u25CF  OFFLINE";
                        modernLblStatusPill.BackColor = Color.FromArgb(127, 29, 29); // Red-900
                        modernLblStatusPill.ForeColor = Color.FromArgb(252, 165, 165); // Red-300
                    }
                }

                if (modernLblMetrics != null)
                {
                    string uptimeStr = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
                    modernLblMetrics.Text = $"Players {playerCount}   \u00B7   Uptime {uptimeStr}   \u00B7   RAM {memoryMb} MB   \u00B7   Ports 6414 / 6415 / 6416";
                }

                UpdateServerStatusBar();
            }
            catch { }
        }

        private void BuildLogUtilityBar()
        {
            try
            {
                if (this.tabPage7 == null || this.MainOutput == null) return;

                // Hide redundant duplicate Save All and Shutdown buttons on tabPage7 to eliminate clutter with header bar
                if (this.btnSaveAllNow != null) this.btnSaveAllNow.Visible = false;
                if (this.btnSafeShutdown != null) this.btnSafeShutdown.Visible = false;

                // Adjust MainOutput font and style
                this.MainOutput.Font = new Font("Consolas", 9.5f, FontStyle.Regular);
                this.MainOutput.BackColor = Color.FromArgb(15, 23, 42); // Slate-900
                this.MainOutput.ForeColor = Color.FromArgb(241, 245, 249); // Slate-100

                Panel logToolPanel = new Panel
                {
                    Location = new Point(6, 204),
                    Size = new Size(this.tabPage7.Width - 12, 32),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BackColor = ModernTheme.CardBackground,
                    Padding = new Padding(4, 2, 4, 2)
                };

                logToolPanel.Paint += (s, e) =>
                {
                    using (var pen = new Pen(ModernTheme.BorderColor))
                    {
                        e.Graphics.DrawRectangle(pen, 0, 0, logToolPanel.Width - 1, logToolPanel.Height - 1);
                    }
                };

                Label lblConsole = new Label
                {
                    Text = "Server Diagnostic Console",
                    Font = ModernTheme.FontBold,
                    ForeColor = ModernTheme.Slate800,
                    Location = new Point(6, 7),
                    AutoSize = true
                };

                Button btnClear = new Button
                {
                    Text = "Clear Console",
                    Location = new Point(185, 3),
                    Size = new Size(95, 26),
                    Font = ModernTheme.FontSmall,
                    BackColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnClear.Click += (s, e) => { this.MainOutput.Clear(); };

                Button btnCopy = new Button
                {
                    Text = "Copy All",
                    Location = new Point(285, 3),
                    Size = new Size(75, 26),
                    Font = ModernTheme.FontSmall,
                    BackColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnCopy.Click += (s, e) =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(this.MainOutput.Text))
                        {
                            Clipboard.SetText(this.MainOutput.Text);
                            MessageBox.Show("Console output copied to clipboard.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch { }
                };

                modernBtnAutoScroll = new Button
                {
                    Text = "Auto-Scroll: ON",
                    Location = new Point(365, 3),
                    Size = new Size(110, 26),
                    Font = ModernTheme.FontBoldSmall,
                    BackColor = ModernTheme.Emerald50,
                    ForeColor = ModernTheme.Emerald700,
                    FlatStyle = FlatStyle.Flat
                };
                modernBtnAutoScroll.Click += (s, e) => SetLogAutoScroll(!modernLogAutoScroll);

                Button btnPacketDebugToggle = new Button
                {
                    Text = DebugSystem.PacketDebugEnabled ? "Packet Debug: ON" : "Packet Debug: OFF",
                    Location = new Point(480, 3),
                    Size = new Size(130, 26),
                    Font = ModernTheme.FontBoldSmall,
                    BackColor = DebugSystem.PacketDebugEnabled ? ModernTheme.Emerald50 : ModernTheme.Rose50,
                    ForeColor = DebugSystem.PacketDebugEnabled ? ModernTheme.Emerald700 : ModernTheme.Rose700,
                    FlatStyle = FlatStyle.Flat
                };
                btnPacketDebugToggle.Click += (s, e) =>
                {
                    DebugSystem.PacketDebugEnabled = !DebugSystem.PacketDebugEnabled;
                    if (cGlobal.SrvSettings != null)
                    {
                        cGlobal.SrvSettings.EnablePacketDebugLogging = DebugSystem.PacketDebugEnabled;
                    }
                    if (DebugSystem.PacketDebugEnabled)
                    {
                        btnPacketDebugToggle.Text = "Packet Debug: ON";
                        btnPacketDebugToggle.BackColor = ModernTheme.Emerald50;
                        btnPacketDebugToggle.ForeColor = ModernTheme.Emerald700;
                    }
                    else
                    {
                        btnPacketDebugToggle.Text = "Packet Debug: OFF";
                        btnPacketDebugToggle.BackColor = ModernTheme.Rose50;
                        btnPacketDebugToggle.ForeColor = ModernTheme.Rose700;
                    }
                    DebugSystem.Write(DebugItemType.Info_Light, $"[System] Packet Debug Logging has been turned {(DebugSystem.PacketDebugEnabled ? "ON" : "OFF")}.");
                };

                TextBox txtSearch = new TextBox
                {
                    Location = new Point(615, 5),
                    Width = 110,
                    Font = new Font("Segoe UI", 8.5f),
                    BorderStyle = BorderStyle.FixedSingle
                };
                ModernTheme.SetPlaceholder(txtSearch, "Find in log...");

                Button btnFind = new Button
                {
                    Text = "Find",
                    Location = new Point(730, 3),
                    Size = new Size(48, 26),
                    Font = new Font("Segoe UI", 8.5f),
                    BackColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnFind.Click += (s, e) =>
                {
                    string target = txtSearch.Text;
                    if (string.IsNullOrEmpty(target)) return;
                    int idx = this.MainOutput.Text.IndexOf(target, modernLogSearchIndex, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        this.MainOutput.Select(idx, target.Length);
                        this.MainOutput.ScrollToCaret();
                        this.MainOutput.Focus();
                        modernLogSearchIndex = idx + target.Length;
                    }
                    else
                    {
                        modernLogSearchIndex = 0;
                        idx = this.MainOutput.Text.IndexOf(target, 0, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            this.MainOutput.Select(idx, target.Length);
                            this.MainOutput.ScrollToCaret();
                            this.MainOutput.Focus();
                            modernLogSearchIndex = idx + target.Length;
                        }
                        else
                        {
                            MessageBox.Show($"Text '{target}' not found.", "Search Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                };

                Button btnExportLog = new Button
                {
                    Text = "Export...",
                    Location = new Point(782, 3),
                    Size = new Size(68, 26),
                    Font = new Font("Segoe UI", 8.5f),
                    BackColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnExportLog.Click += (s, e) =>
                {
                    try
                    {
                        using (SaveFileDialog sfd = new SaveFileDialog())
                        {
                            sfd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                            sfd.FileName = $"wlo_console_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                            if (sfd.ShowDialog(this) == DialogResult.OK)
                            {
                                File.WriteAllText(sfd.FileName, this.MainOutput.Text);
                                MessageBox.Show("Log exported successfully.", "Export Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to export log: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                logToolPanel.Controls.Add(lblConsole);
                logToolPanel.Controls.Add(btnClear);
                logToolPanel.Controls.Add(btnCopy);
                logToolPanel.Controls.Add(modernBtnAutoScroll);
                logToolPanel.Controls.Add(btnPacketDebugToggle);
                logToolPanel.Controls.Add(txtSearch);
                logToolPanel.Controls.Add(btnFind);
                logToolPanel.Controls.Add(btnExportLog);

                this.tabPage7.Controls.Add(logToolPanel);

                // Re-position MainOutput below the log tool panel
                this.MainOutput.Location = new Point(6, 240);
                this.MainOutput.Size = new Size(this.tabPage7.Width - 12, this.tabPage7.Height - 246);
                this.MainOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            }
            catch { }
        }

        private void EnhanceCheatTabControls()
        {
            try
            {
                if (this.tabPage1 == null) return;

                modernCheatDebounceTimer = new Timer();
                modernCheatDebounceTimer.Interval = 150;
                modernCheatDebounceTimer.Tick += (s, e) =>
                {
                    modernCheatDebounceTimer.Stop();
                    modernCheatDebounceAction?.Invoke();
                };

                // Enhance textBox_FindMap
                if (this.textBox_FindMap != null)
                {
                    this.textBox_FindMap.TextChanged += (s, e) =>
                    {
                        modernCheatDebounceAction = () => FilterMapsList(textBox_FindMap.Text);
                        modernCheatDebounceTimer.Stop();
                        modernCheatDebounceTimer.Start();
                    };
                }

                // Enhance textBox_FindVehicle
                if (this.textBox_FindVehicle != null)
                {
                    this.textBox_FindVehicle.TextChanged += (s, e) =>
                    {
                        modernCheatDebounceAction = () => FilterVehiclesList(textBox_FindVehicle.Text);
                        modernCheatDebounceTimer.Stop();
                        modernCheatDebounceTimer.Start();
                    };
                }

                // Enhance textBox_FindItems
                if (this.textBox_FindItems != null)
                {
                    this.textBox_FindItems.TextChanged += (s, e) =>
                    {
                        modernCheatDebounceAction = () => FilterItemsList(textBox_FindItems.Text);
                        modernCheatDebounceTimer.Stop();
                        modernCheatDebounceTimer.Start();
                    };
                }

                // Enhance textBox_FindNPC
                if (this.textBox_FindNPC != null)
                {
                    this.textBox_FindNPC.TextChanged += (s, e) =>
                    {
                        modernCheatDebounceAction = () => FilterNpcList(textBox_FindNPC.Text);
                        modernCheatDebounceTimer.Stop();
                        modernCheatDebounceTimer.Start();
                    };
                }

                // Set initial group box titles with counts
                UpdateCheatGroupTitle(groupBox_Maps, "Maps", listBox_Maps?.Items?.Count ?? 0);
                UpdateCheatGroupTitle(groupBox_Vehicles, "Vehicles", listBox_Vehicles?.Items?.Count ?? 0);
                UpdateCheatGroupTitle(groupBox_Items, "Items", listBox_Items?.Items?.Count ?? 0);
                UpdateCheatGroupTitle(groupBox_Npc, "NPCs & Companions", listBox_NPC?.Items?.Count ?? 0);

                if (this.label_CheatHint != null)
                {
                    this.label_CheatHint.Text = "Hint: Double-click map to warp | item to give | vehicle to ride | NPC to summon";
                    this.label_CheatHint.ForeColor = Color.FromArgb(37, 99, 235); // Blue-600
                    this.label_CheatHint.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                }
            }
            catch { }
        }

        private void FilterCheatDictionary(ListBox listBox, GroupBox groupBox, string groupTitle, IEnumerable<KeyValuePair<string, string>> data, string searchKey)
        {
            if (listBox == null || data == null) return;
            try
            {
                listBox.BeginUpdate();
                listBox.Items.Clear();
                bool empty = string.IsNullOrWhiteSpace(searchKey);
                foreach (var row in data)
                {
                    if (empty || row.Value.IndexOf(searchKey, StringComparison.OrdinalIgnoreCase) >= 0 || row.Key.ToString().Contains(searchKey))
                    {
                        listBox.Items.Add(row.Key + " " + row.Value);
                    }
                }
                listBox.EndUpdate();
                UpdateCheatGroupTitle(groupBox, groupTitle, listBox.Items.Count);
            }
            catch { }
        }

        private void FilterMapsList(string searchKey) => FilterCheatDictionary(listBox_Maps, groupBox_Maps, "Maps", MapsData, searchKey);
        private void FilterVehiclesList(string searchKey) => FilterCheatDictionary(listBox_Vehicles, groupBox_Vehicles, "Vehicles", VehiclesData, searchKey);
        private void FilterItemsList(string searchKey) => FilterCheatDictionary(listBox_Items, groupBox_Items, "Items", ItemsData, searchKey);
        private void FilterNpcList(string searchKey) => FilterCheatDictionary(listBox_NPC, groupBox_Npc, "NPCs & Companions", NpcData, searchKey);

        private void UpdateCheatGroupTitle(GroupBox grp, string baseName, int count)
        {
            if (grp == null) return;
            grp.Text = count > 0 ? $"{baseName} ({count:N0})" : baseName;
        }

        public static void ApplyModernTheme(Control root)
        {
            ModernTheme.Apply(root);
        }

        public static void StyleDataGridView(DataGridView dgv)
        {
            ModernTheme.StyleGrid(dgv);
        }

        public void DisposeModernUi()
        {
            try
            {
                if (modernMetricsTimer != null)
                {
                    modernMetricsTimer.Stop();
                    modernMetricsTimer.Dispose();
                    modernMetricsTimer = null;
                }
                if (modernCheatDebounceTimer != null)
                {
                    modernCheatDebounceTimer.Stop();
                    modernCheatDebounceTimer.Dispose();
                    modernCheatDebounceTimer = null;
                }
            }
            catch { }
        }
    }
}
