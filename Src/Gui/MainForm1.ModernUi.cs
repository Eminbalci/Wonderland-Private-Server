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

            public NavItem(string label, Func<TabPage> resolveTab)
            {
                Label = label;
                ResolveTab = resolveTab;
            }
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
        private readonly Dictionary<TabPage, Button> modernSubTabButtons = new Dictionary<TabPage, Button>();
        private Button modernActiveSubTabButton;
        private bool modernLogAutoScroll = true;
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
                if (this.tabControl3 != null) this.tabControl3.BringToFront();

                // Apply global modern theme across all controls
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
                Text = "ONLINE",
                Font = ModernTheme.FontBoldSmall,
                ForeColor = Color.FromArgb(52, 211, 153), // Emerald-400
                BackColor = Color.FromArgb(6, 78, 59), // Emerald-900
                Location = new Point(205, 12),
                AutoSize = false,
                Size = new Size(68, 24),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Metrics Label
            modernLblMetrics = new Label
            {
                Text = "Players: 0 | Uptime: 00:00:00 | RAM: 0 MB | Ports: 6414, 6415, 6416",
                Font = ModernTheme.FontSmall,
                ForeColor = ModernTheme.Slate300,
                Location = new Point(285, 16),
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
            Label lblSearch = new Label
            {
                Text = "Jump:",
                Font = ModernTheme.FontBoldSmall,
                ForeColor = ModernTheme.Slate400,
                Location = new Point(2, 14),
                AutoSize = true
            };

            modernTxtQuickSearch = new TextBox
            {
                Location = new Point(45, 12),
                Width = 125,
                Font = ModernTheme.FontDefault,
                BackColor = ModernTheme.Slate800,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
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
            modernBtnShutdownHeader.Click += (s, e) => btnSafeShutdown_Click(s, e);

            rightActionsPanel.Controls.Add(lblSearch);
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
                Height = 38,
                BackColor = ModernTheme.Slate800,
                Padding = new Padding(8, 4, 8, 4)
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
                Height = 36,
                BackColor = ModernTheme.Slate100,
                Padding = new Padding(8, 3, 8, 3)
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
                new NavCategory("Server & Operations", new List<NavItem>
                {
                    new NavItem("Status & Realtime Logs", () => this.tabPage7),
                    new NavItem("Online Sessions", () => FindTabBySubstring("Online Sessions")),
                    new NavItem("Database Config", () => FindTabBySubstring("Database Config")),
                    new NavItem("Server Settings", () => this.tabPageSettings),
                    new NavItem("Security & Bans", () => FindTabBySubstring("Security"))
                }),
                new NavCategory("Accounts & Players", new List<NavItem>
                {
                    new NavItem("Characters", () => this.tabPageCharacters),
                    new NavItem("User Accounts", () => this.tabPageUsers),
                    new NavItem("Inventory Viewer", () => this.tabPageInventory),
                    new NavItem("Base Stats & Attributes", () => this.tabPageStats),
                    new NavItem("Friends Roster", () => this.tabPageFriends),
                    new NavItem("Guild Management", () => FindTabBySubstring("Guild")),
                    new NavItem("Mail Dispatcher", () => FindTabBySubstring("Mail")),
                    new NavItem("Marriage Registry", () => FindTabBySubstring("Marriage"))
                }),
                new NavCategory("Combat & GM Tools", new List<NavItem>
                {
                    new NavItem("Quick GM Cheats", () => this.tabPage1),
                    new NavItem("Developer & GM Studio", () => FindTabBySubstring("GM Management", "Developer")),
                    new NavItem("Live Battle Monitor", () => FindTabBySubstring("Battle"))
                }),
                new NavCategory("World & NPC Studio", new List<NavItem>
                {
                    new NavItem("NPC Studio & Scenes", () => FindTabBySubstring("Map NPCs", "Studio")),
                    new NavItem("NPC Binary Resolver", () => FindTabBySubstring("NPC Name Resolver", "Binary")),
                    new NavItem("Dialogue & Talk Resolver", () => FindTabBySubstring("Talk ID Resolver", "Talk")),
                    new NavItem("Portals & Destinations", () => this.tabPagePortals)
                }),
                new NavCategory("Economy & Rewards", new List<NavItem>
                {
                    new NavItem("Monster Drops Editor", () => FindTabBySubstring("Monster Drops")),
                    new NavItem("Chest Drops Editor", () => this.tabPageChestDrops),
                    new NavItem("Item Mall Catalog", () => FindTabBySubstring("Item Mall")),
                    new NavItem("Starter Items Pack", () => FindTabBySubstring("Starter Items"))
                })
            };

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

        private void RenderCategoryButtons()
        {
            modernCatFlow.SuspendLayout();
            modernCatFlow.Controls.Clear();
            modernCategoryButtons.Clear();

            foreach (var cat in modernCategories)
            {
                Button btn = new Button
                {
                    Text = cat.Name,
                    Height = 30,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(148, 163, 184), // Slate-400
                    BackColor = Color.Transparent,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(0, 0, 6, 0),
                    Padding = new Padding(10, 0, 10, 0),
                    Cursor = Cursors.Hand,
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

            TabPage firstTab = null;

            foreach (var item in cat.Items)
            {
                TabPage tab = item.ResolveTab();
                if (tab == null) continue;

                if (firstTab == null) firstTab = tab;

                Button subBtn = new Button
                {
                    Text = item.Label,
                    Height = 28,
                    AutoSize = true,
                    Font = ModernTheme.FontSmall,
                    ForeColor = ModernTheme.Slate700,
                    BackColor = ModernTheme.Slate200,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(0, 1, 6, 1),
                    Padding = new Padding(8, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    Tag = tab
                };
                subBtn.FlatAppearance.BorderSize = 0;
                subBtn.FlatAppearance.MouseOverBackColor = ModernTheme.Slate300;

                subBtn.Click += (s, e) =>
                {
                    SwitchToTab(tab, subBtn);
                };

                modernSubTabFlow.Controls.Add(subBtn);
                modernSubTabButtons[tab] = subBtn;
            }

            modernSubTabFlow.ResumeLayout(true);

            // Select active tab if already within this category, or default to first tab
            if (this.tabControl3 != null && this.tabControl3.SelectedTab != null && modernSubTabButtons.ContainsKey(this.tabControl3.SelectedTab))
            {
                HighlightSubTabButton(modernSubTabButtons[this.tabControl3.SelectedTab]);
            }
            else if (firstTab != null && modernSubTabButtons.TryGetValue(firstTab, out Button firstBtn))
            {
                SwitchToTab(firstTab, firstBtn);
            }
        }

        private void SwitchToTab(TabPage tab, Button subBtn)
        {
            if (tab == null || this.tabControl3 == null) return;

            modernIsInternalTabSwitch = true;
            try
            {
                this.tabControl3.SelectedTab = tab;
            }
            finally
            {
                modernIsInternalTabSwitch = false;
            }

            HighlightSubTabButton(subBtn);
        }

        private void HighlightSubTabButton(Button subBtn)
        {
            if (modernActiveSubTabButton != null && modernActiveSubTabButton != subBtn)
            {
                modernActiveSubTabButton.BackColor = ModernTheme.Slate200;
                modernActiveSubTabButton.ForeColor = ModernTheme.Slate700;
                modernActiveSubTabButton.Font = ModernTheme.FontSmall;
            }

            modernActiveSubTabButton = subBtn;
            if (modernActiveSubTabButton != null)
            {
                modernActiveSubTabButton.BackColor = ModernTheme.Blue600;
                modernActiveSubTabButton.ForeColor = Color.White;
                modernActiveSubTabButton.Font = ModernTheme.FontBoldSmall;
            }
        }

        private void SyncNavToTab(TabPage targetTab)
        {
            if (targetTab == null || modernCategories == null) return;

            foreach (var cat in modernCategories)
            {
                foreach (var item in cat.Items)
                {
                    if (item.ResolveTab() == targetTab)
                    {
                        if (modernActiveCategory != cat)
                        {
                            SelectCategory(cat);
                        }
                        if (modernSubTabButtons.TryGetValue(targetTab, out Button btn))
                        {
                            HighlightSubTabButton(btn);
                        }
                        return;
                    }
                }
            }
        }

        private void ExecuteQuickSearchJump(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || modernCategories == null) return;

            foreach (var cat in modernCategories)
            {
                foreach (var item in cat.Items)
                {
                    if (item.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        TabPage tab = item.ResolveTab();
                        if (tab != null)
                        {
                            SelectCategory(cat);
                            if (modernSubTabButtons.TryGetValue(tab, out Button subBtn))
                            {
                                SwitchToTab(tab, subBtn);
                            }
                            return;
                        }
                    }
                }
            }
        }

        private TabPage FindTabBySubstring(params string[] searchTerms)
        {
            if (this.tabControl3 == null) return null;
            foreach (TabPage tab in this.tabControl3.TabPages)
            {
                string text = tab.Text ?? string.Empty;
                foreach (string term in searchTerms)
                {
                    if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return tab;
                    }
                }
            }
            return null;
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
                        modernLblStatusPill.Text = "ONLINE";
                        modernLblStatusPill.BackColor = Color.FromArgb(6, 78, 59); // Emerald-900
                        modernLblStatusPill.ForeColor = Color.FromArgb(52, 211, 153); // Emerald-400
                    }
                    else
                    {
                        modernLblStatusPill.Text = "OFFLINE";
                        modernLblStatusPill.BackColor = Color.FromArgb(127, 29, 29); // Red-900
                        modernLblStatusPill.ForeColor = Color.FromArgb(252, 165, 165); // Red-300
                    }
                }

                if (modernLblMetrics != null)
                {
                    string uptimeStr = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
                    modernLblMetrics.Text = $"Players: {playerCount} | Uptime: {uptimeStr} | RAM: {memoryMb} MB | Ports: 6414, 6415, 6416";
                }
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
                    BackColor = ModernTheme.Slate100,
                    Padding = new Padding(4, 2, 4, 2)
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

                Button btnAutoScrollToggle = new Button
                {
                    Text = "Auto-Scroll: ON",
                    Location = new Point(365, 3),
                    Size = new Size(110, 26),
                    Font = ModernTheme.FontBoldSmall,
                    BackColor = ModernTheme.Emerald50,
                    ForeColor = ModernTheme.Emerald700,
                    FlatStyle = FlatStyle.Flat
                };
                btnAutoScrollToggle.Click += (s, e) =>
                {
                    modernLogAutoScroll = !modernLogAutoScroll;
                    if (modernLogAutoScroll)
                    {
                        btnAutoScrollToggle.Text = "Auto-Scroll: ON";
                        btnAutoScrollToggle.BackColor = ModernTheme.Emerald50;
                        btnAutoScrollToggle.ForeColor = ModernTheme.Emerald700;
                    }
                    else
                    {
                        btnAutoScrollToggle.Text = "Auto-Scroll: OFF";
                        btnAutoScrollToggle.BackColor = ModernTheme.Rose50;
                        btnAutoScrollToggle.ForeColor = ModernTheme.Rose700;
                    }
                };

                TextBox txtSearch = new TextBox
                {
                    Location = new Point(485, 5),
                    Width = 120,
                    Font = new Font("Segoe UI", 8.5f)
                };

                Button btnFind = new Button
                {
                    Text = "Find",
                    Location = new Point(610, 3),
                    Size = new Size(50, 26),
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
                    Location = new Point(665, 3),
                    Size = new Size(70, 26),
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
                logToolPanel.Controls.Add(btnAutoScrollToggle);
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
