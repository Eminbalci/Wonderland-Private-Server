using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Game;
using Game.Battle;
using Game.Code;
using Game.Maps;
using Game.PlayerRelated;
using Network;
using Network.ActionCodes;

namespace Wonderland_Private_Server
{
    public partial class Form1
    {
        private static string ResolveItemName(ushort itemId)
        {
            if (itemId == 0) return "None";
            try
            {
                var item = cGlobal.ItemDatManager?.GetItemByID(itemId);
                if (item != null && item.ItemName != null)
                {
                    string name = Encoding.Default.GetString(item.ItemName).Trim('\0', ' ');
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            catch { }
            return $"Item #{itemId}";
        }

        #region TAB 1: Online Sessions (tab_players)
        private DataGridView ext_dgvOnlineSessions;
        private TextBox ext_txtSearchSessions;
        private Label ext_lblSessionCount;
        private Label ext_lblSelectedSessionPlayer;
        private Player ext_selectedOnlinePlayer;

        private void SetupOnlineSessionsTab()
        {
            try
            {
                TabPage tabSessions = new TabPage(" Online Sessions");
                tabSessions.BackColor = Color.White;

                // Top filter panel
                Panel topPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 48,
                    Padding = new Padding(10, 8, 10, 8)
                };

                Label lblFilter = new Label
                {
                    Text = " Filter Sessions:",
                    Location = new Point(10, 14),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
                };

                ext_txtSearchSessions = new TextBox
                {
                    Location = new Point(130, 11),
                    Width = 220,
                    Font = new Font("Segoe UI", 9.5f)
                };
                ext_txtSearchSessions.TextChanged += (s, e) => FilterOnlineSessions();

                Button btnRefresh = new Button
                {
                    Text = " Refresh Sessions",
                    Location = new Point(365, 9),
                    Size = new Size(140, 28),
                    Font = new Font("Segoe UI", 9f)
                };
                btnRefresh.Click += (s, e) => RefreshOnlineSessions();

                ext_lblSessionCount = new Label
                {
                    Text = "Total Sessions: 0",
                    Location = new Point(525, 14),
                    AutoSize = true,
                    ForeColor = Color.DarkSlateBlue,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
                };

                topPanel.Controls.Add(lblFilter);
                topPanel.Controls.Add(ext_txtSearchSessions);
                topPanel.Controls.Add(btnRefresh);
                topPanel.Controls.Add(ext_lblSessionCount);

                // Right action panel
                Panel rightPanel = new Panel
                {
                    Dock = DockStyle.Right,
                    Width = 320,
                    BackColor = Color.FromArgb(248, 249, 250),
                    Padding = new Padding(15, 15, 15, 15)
                };

                Label lblToolsHeader = new Label
                {
                    Text = "Live GM Session Tools",
                    Location = new Point(12, 12),
                    Size = new Size(290, 24),
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 41, 59)
                };

                ext_lblSelectedSessionPlayer = new Label
                {
                    Text = "Selected: (None)",
                    Location = new Point(12, 40),
                    Size = new Size(290, 22),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(217, 119, 6)
                };

                Button btnOpenCharEditor = new Button
                {
                    Text = " Open Deep Character Editor",
                    Location = new Point(12, 75),
                    Size = new Size(290, 34),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(37, 99, 235),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnOpenCharEditor.Click += (s, e) =>
                {
                    if (ext_selectedOnlinePlayer == null)
                    {
                        MessageBox.Show("Please select an online player first.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    using (var editor = new CharacterDataEditorForm(ext_selectedOnlinePlayer.CharID, ext_selectedOnlinePlayer.CharName))
                    {
                        editor.ShowDialog(this);
                    }
                    RefreshOnlineSessions();
                };

                Button btnHealPlayer = new Button
                {
                    Text = " Heal HP/SP to 100%",
                    Location = new Point(12, 117),
                    Size = new Size(290, 32),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(16, 185, 129),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnHealPlayer.Click += (s, e) =>
                {
                    if (ext_selectedOnlinePlayer == null) return;
                    try
                    {
                        ext_selectedOnlinePlayer.Eqs.CurHP = ext_selectedOnlinePlayer.Eqs.FullHP;
                        ext_selectedOnlinePlayer.Eqs.CurSP = ext_selectedOnlinePlayer.Eqs.FullSP;
                        ext_selectedOnlinePlayer.Eqs.Send8_1(true);
                        ext_selectedOnlinePlayer.SendSystemMessage("[GM] Your HP & SP have been fully restored by the administrator!");
                        MessageBox.Show($"Restored HP/SP for {ext_selectedOnlinePlayer.CharName}.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error healing player: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                Button btnAddGold = new Button
                {
                    Text = " Add 100,000 Gold",
                    Location = new Point(12, 157),
                    Size = new Size(290, 32),
                    Font = new Font("Segoe UI", 9f)
                };
                btnAddGold.Click += (s, e) =>
                {
                    if (ext_selectedOnlinePlayer == null) return;
                    try
                    {
                        ext_selectedOnlinePlayer.SetGold((int)Math.Min((long)int.MaxValue, (long)ext_selectedOnlinePlayer.Gold + 100000));
                        ext_selectedOnlinePlayer.Send(Tools.FromFormat("bbd", 26, 4, (uint)ext_selectedOnlinePlayer.Gold));
                        DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(ext_selectedOnlinePlayer.CharID, ext_selectedOnlinePlayer);
                        ext_selectedOnlinePlayer.SendSystemMessage("[GM] Received 100,000 Gold from Server Admin!");
                        RefreshOnlineSessions();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error adding gold: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                Button btnGodMode = new Button
                {
                    Text = " Toggle Invincible God Mode",
                    Location = new Point(12, 197),
                    Size = new Size(290, 32),
                    Font = new Font("Segoe UI", 9f)
                };
                btnGodMode.Click += (s, e) =>
                {
                    if (ext_selectedOnlinePlayer == null) return;
                    try
                    {
                        ext_selectedOnlinePlayer.Eqs.CurHP = 99999;
                        ext_selectedOnlinePlayer.Eqs.CurSP = 99999;
                        ext_selectedOnlinePlayer.Eqs.Send8_1(true);
                        ext_selectedOnlinePlayer.SendSystemMessage("[GM] Invincible God Mode activated (99,999 HP/SP)!");
                        MessageBox.Show($"God mode granted to {ext_selectedOnlinePlayer.CharName}.", "God Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error setting god mode: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                Button btnTeleportPlayer = new Button
                {
                    Text = " Teleport Player to Coordinates...",
                    Location = new Point(12, 237),
                    Size = new Size(290, 32),
                    Font = new Font("Segoe UI", 9f)
                };
                btnTeleportPlayer.Click += (s, e) =>
                {
                    if (ext_selectedOnlinePlayer == null) return;
                    string res = ShowInputDialog("Enter Target <MapID> <X> <Y>:", "Teleport Player", $"{ext_selectedOnlinePlayer.MapID} 500 500");
                    if (!string.IsNullOrEmpty(res))
                    {
                        string[] parts = res.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3 && ushort.TryParse(parts[0], out ushort mId) && ushort.TryParse(parts[1], out ushort x) && ushort.TryParse(parts[2], out ushort y))
                        {
                            ext_selectedOnlinePlayer.CurMap?.Teleport(TeleportType.CmD, ext_selectedOnlinePlayer, 0, new WarpData { DstMap = mId, DstX_Axis = x, DstY_Axis = y });
                            RefreshOnlineSessions();
                        }
                        else
                        {
                            MessageBox.Show("Format must be: <MapID> <X> <Y>", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                };

                Button btnKickPlayer = new Button
                {
                    Text = " Kick Selected Player",
                    Location = new Point(12, 277),
                    Size = new Size(290, 32),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(71, 85, 105),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnKickPlayer.Click += (s, e) =>
                {
                    if (ext_selectedOnlinePlayer == null) return;
                    if (MessageBox.Show($"Kick player '{ext_selectedOnlinePlayer.CharName}' from the server?", "Confirm Kick", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        ext_selectedOnlinePlayer.Disconnect();
                        RefreshOnlineSessions();
                    }
                };

                Button btnBanAccount = new Button
                {
                    Text = " Ban Player Account",
                    Location = new Point(12, 317),
                    Size = new Size(290, 32),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(220, 38, 38),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnBanAccount.Click += (s, e) =>
                {
                    if (ext_selectedOnlinePlayer == null) return;
                    string reason = ShowInputDialog($"Ban account for '{ext_selectedOnlinePlayer.CharName}'. Reason:", "Ban Account", "Rule violation");
                    if (reason != null)
                    {
                        BanAccountInternal(ext_selectedOnlinePlayer.UserAcc?.UserName ?? ext_selectedOnlinePlayer.CharName, ext_selectedOnlinePlayer.UserID, reason);
                        ext_selectedOnlinePlayer.Disconnect();
                        RefreshOnlineSessions();
                        RefreshBannedAccountsTable();
                    }
                };

                Button btnBanIp = new Button
                {
                    Text = " Ban Player IP",
                    Location = new Point(12, 357),
                    Size = new Size(290, 32),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(153, 27, 27),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnBanIp.Click += (s, e) =>
                {
                    if (ext_selectedOnlinePlayer == null) return;
                    string ip = ext_selectedOnlinePlayer.SockAddress();
                    string reason = ShowInputDialog($"Ban IP '{ip}' for '{ext_selectedOnlinePlayer.CharName}'. Reason:", "Ban IP", "Malicious activity");
                    if (reason != null)
                    {
                        BanIpInternal(ip, reason);
                        ext_selectedOnlinePlayer.Disconnect();
                        RefreshOnlineSessions();
                        RefreshBannedIpsTable();
                    }
                };

                rightPanel.Controls.Add(lblToolsHeader);
                rightPanel.Controls.Add(ext_lblSelectedSessionPlayer);
                rightPanel.Controls.Add(btnOpenCharEditor);
                rightPanel.Controls.Add(btnHealPlayer);
                rightPanel.Controls.Add(btnAddGold);
                rightPanel.Controls.Add(btnGodMode);
                rightPanel.Controls.Add(btnTeleportPlayer);
                rightPanel.Controls.Add(btnKickPlayer);
                rightPanel.Controls.Add(btnBanAccount);
                rightPanel.Controls.Add(btnBanIp);

                // Grid view
                ext_dgvOnlineSessions = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = Color.White,
                    BorderStyle = BorderStyle.None,
                    RowHeadersVisible = false,
                    Font = new Font("Segoe UI", 9f)
                };

                ext_dgvOnlineSessions.Columns.Add("colCharId", "CharID");
                ext_dgvOnlineSessions.Columns.Add("colName", "Name");
                ext_dgvOnlineSessions.Columns.Add("colAccount", "Account");
                ext_dgvOnlineSessions.Columns.Add("colLevel", "Level");
                ext_dgvOnlineSessions.Columns.Add("colGold", "Gold");
                ext_dgvOnlineSessions.Columns.Add("colMap", "MapID");
                ext_dgvOnlineSessions.Columns.Add("colX", "X");
                ext_dgvOnlineSessions.Columns.Add("colY", "Y");
                ext_dgvOnlineSessions.Columns.Add("colIp", "IP Address");

                ext_dgvOnlineSessions.Columns["colCharId"].Width = 70;
                ext_dgvOnlineSessions.Columns["colLevel"].Width = 55;
                ext_dgvOnlineSessions.Columns["colMap"].Width = 65;
                ext_dgvOnlineSessions.Columns["colX"].Width = 55;
                ext_dgvOnlineSessions.Columns["colY"].Width = 55;
                ext_dgvOnlineSessions.Columns["colIp"].Width = 120;
                ext_dgvOnlineSessions.Columns["colName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                ext_dgvOnlineSessions.SelectionChanged += (s, e) =>
                {
                    if (ext_dgvOnlineSessions.SelectedRows.Count > 0)
                    {
                        var row = ext_dgvOnlineSessions.SelectedRows[0];
                        uint charId = Convert.ToUInt32(row.Cells["colCharId"].Value);
                        ext_selectedOnlinePlayer = cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.FirstOrDefault(p => p.CharID == charId);
                        ext_lblSelectedSessionPlayer.Text = ext_selectedOnlinePlayer != null ? $"Selected: {ext_selectedOnlinePlayer.CharName} ({charId})" : "Selected: (None)";
                    }
                    else
                    {
                        ext_selectedOnlinePlayer = null;
                        ext_lblSelectedSessionPlayer.Text = "Selected: (None)";
                    }
                };

                tabSessions.Controls.Add(ext_dgvOnlineSessions);
                tabSessions.Controls.Add(rightPanel);
                tabSessions.Controls.Add(topPanel);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabSessions);
                }

                RefreshOnlineSessions();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error setting up Online Sessions tab: {ex.Message}");
            }
        }

        private void RefreshOnlineSessions()
        {
            if (ext_dgvOnlineSessions == null) return;
            try
            {
                ext_dgvOnlineSessions.Rows.Clear();
                var players = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                if (players != null)
                {
                    string search = ext_txtSearchSessions?.Text.Trim().ToLower() ?? "";
                    foreach (var p in players)
                    {
                        string acc = p.UserAcc?.UserName ?? "";
                        string mapStr = p.MapID.ToString();
                        string charName = p.CharName ?? "";

                        if (!string.IsNullOrEmpty(search))
                        {
                            if (!charName.ToLower().Contains(search) && !acc.ToLower().Contains(search) && !mapStr.Contains(search))
                                continue;
                        }

                        ext_dgvOnlineSessions.Rows.Add(
                            p.CharID,
                            charName,
                            acc,
                            p.Level,
                            p.Gold.ToString("N0"),
                            p.MapID,
                            p.X,
                            p.Y,
                            p.SockAddress()
                        );
                    }
                    ext_lblSessionCount.Text = $"Total Sessions: {players.Count}";
                }
                else
                {
                    ext_lblSessionCount.Text = "Total Sessions: 0";
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error refreshing online sessions: {ex.Message}");
            }
        }

        private void FilterOnlineSessions()
        {
            RefreshOnlineSessions();
        }
        #endregion

        #region TAB 2: Guilds (tab_guilds)
        private DataGridView ext_dgvGuilds;
        private DataGridView ext_dgvGuildMembers;
        private TextBox ext_txtGuildSearch;
        private TextBox ext_txtGuildRules;
        private Label ext_lblGuildStats;
        private Label ext_lblSelectedGuild;
        private Guild ext_selectedGuild;

        private void SetupGuildsTab()
        {
            try
            {
                TabPage tabGuilds = new TabPage(" Guilds");
                tabGuilds.BackColor = Color.White;

                // Top filter panel
                Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 8, 10, 8) };

                Button btnRefresh = new Button { Text = " Refresh Guilds", Location = new Point(10, 9), Size = new Size(130, 28), Font = new Font("Segoe UI", 9f) };
                btnRefresh.Click += (s, e) => RefreshGuildsList();

                Label lblFilter = new Label { Text = " Filter:", Location = new Point(155, 14), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
                ext_txtGuildSearch = new TextBox { Location = new Point(210, 11), Width = 200, Font = new Font("Segoe UI", 9.5f) };
                ext_txtGuildSearch.TextChanged += (s, e) => RefreshGuildsList();

                ext_lblGuildStats = new Label
                {
                    Text = "Total Guilds: 0 | Members: 0",
                    Location = new Point(430, 14),
                    AutoSize = true,
                    ForeColor = Color.DarkBlue,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
                };

                topPanel.Controls.Add(btnRefresh);
                topPanel.Controls.Add(lblFilter);
                topPanel.Controls.Add(ext_txtGuildSearch);
                topPanel.Controls.Add(ext_lblGuildStats);

                // Split layout
                SplitContainer split = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical,
                    SplitterDistance = 520,
                    Panel1MinSize = 300,
                    Panel2MinSize = 300
                };

                // Left: Guilds List
                ext_dgvGuilds = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = Color.White,
                    RowHeadersVisible = false,
                    Font = new Font("Segoe UI", 9f)
                };
                ext_dgvGuilds.Columns.Add("colGId", "GuildID");
                ext_dgvGuilds.Columns.Add("colGName", "Guild Name");
                ext_dgvGuilds.Columns.Add("colGLeader", "Leader Name");
                ext_dgvGuilds.Columns.Add("colGLeaderId", "LeaderID");
                ext_dgvGuilds.Columns.Add("colGMembers", "Members");
                ext_dgvGuilds.Columns.Add("colGCreated", "Created");

                ext_dgvGuilds.Columns["colGId"].Width = 65;
                ext_dgvGuilds.Columns["colGLeaderId"].Width = 65;
                ext_dgvGuilds.Columns["colGMembers"].Width = 65;
                ext_dgvGuilds.Columns["colGCreated"].Width = 110;
                ext_dgvGuilds.Columns["colGName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                ext_dgvGuilds.SelectionChanged += (s, e) => OnGuildSelectionChanged();
                split.Panel1.Controls.Add(ext_dgvGuilds);

                // Right: Guild Details & Members
                Panel rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 10, 12, 10) };

                ext_lblSelectedGuild = new Label
                {
                    Text = "Selected: (None)",
                    Location = new Point(10, 8),
                    Size = new Size(380, 22),
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 41, 59)
                };

                Label lblRules = new Label { Text = "Guild Announcement / Rules:", Location = new Point(10, 35), AutoSize = true, Font = new Font("Segoe UI", 9f) };
                ext_txtGuildRules = new TextBox { Location = new Point(10, 55), Width = 380, Height = 55, Multiline = true, Font = new Font("Segoe UI", 9f) };

                Button btnSaveRules = new Button { Text = " Save Notice", Location = new Point(10, 115), Size = new Size(110, 28), Font = new Font("Segoe UI", 9f) };
                btnSaveRules.Click += (s, e) =>
                {
                    if (ext_selectedGuild == null) return;
                    if (GuildManager.AdminUpdateRules(ext_selectedGuild.GuildID, ext_txtGuildRules.Text))
                    {
                        MessageBox.Show("Guild notice updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                Label lblRoster = new Label { Text = "Guild Member Roster:", Location = new Point(10, 150), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

                ext_dgvGuildMembers = new DataGridView
                {
                    Location = new Point(10, 175),
                    Width = 380,
                    Height = 220,
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = Color.White,
                    RowHeadersVisible = false,
                    Font = new Font("Segoe UI", 8.5f)
                };
                ext_dgvGuildMembers.Columns.Add("colMId", "CharID");
                ext_dgvGuildMembers.Columns.Add("colMName", "Name");
                ext_dgvGuildMembers.Columns.Add("colMLvl", "Level");
                ext_dgvGuildMembers.Columns.Add("colMRank", "Rank");
                ext_dgvGuildMembers.Columns.Add("colMOnline", "Status");
                ext_dgvGuildMembers.Columns["colMId"].Width = 60;
                ext_dgvGuildMembers.Columns["colMLvl"].Width = 45;
                ext_dgvGuildMembers.Columns["colMRank"].Width = 70;
                ext_dgvGuildMembers.Columns["colMOnline"].Width = 65;
                ext_dgvGuildMembers.Columns["colMName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                // Action buttons
                FlowLayoutPanel pnlGuildActions = new FlowLayoutPanel
                {
                    Location = new Point(10, 405),
                    Size = new Size(380, 45),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
                };

                Button btnChangeLeader = new Button { Text = " Change Leader", Size = new Size(115, 32), Font = new Font("Segoe UI", 9f) };
                btnChangeLeader.Click += (s, e) =>
                {
                    if (ext_selectedGuild == null) return;
                    string res = ShowInputDialog("Enter new leader CharID (must be a current member):", "Change Leader", "");
                    if (uint.TryParse(res, out uint newLeaderId))
                    {
                        if (GuildManager.AdminChangeLeader(ext_selectedGuild.GuildID, newLeaderId))
                        {
                            MessageBox.Show("Guild leader changed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            RefreshGuildsList();
                        }
                        else
                        {
                            MessageBox.Show("Failed to change leader. Target must be a valid member.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                };

                Button btnKickMember = new Button { Text = " Kick Member", Size = new Size(110, 32), Font = new Font("Segoe UI", 9f) };
                btnKickMember.Click += (s, e) =>
                {
                    if (ext_selectedGuild == null || ext_dgvGuildMembers.SelectedRows.Count == 0) return;
                    uint memId = Convert.ToUInt32(ext_dgvGuildMembers.SelectedRows[0].Cells["colMId"].Value);
                    if (MessageBox.Show($"Kick member ID {memId} from '{ext_selectedGuild.GuildName}'?", "Confirm Kick", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        if (GuildManager.AdminKickMember(ext_selectedGuild.GuildID, memId))
                        {
                            RefreshGuildsList();
                        }
                    }
                };

                Button btnDisband = new Button
                {
                    Text = " Disband Guild",
                    Size = new Size(115, 32),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(220, 38, 38),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnDisband.Click += (s, e) =>
                {
                    if (ext_selectedGuild == null) return;
                    if (MessageBox.Show($"Are you sure you want to completely DISBAND '{ext_selectedGuild.GuildName}'?", "Confirm Disband", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        if (GuildManager.AdminDisbandGuild(ext_selectedGuild.GuildID))
                        {
                            MessageBox.Show("Guild disbanded.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            RefreshGuildsList();
                        }
                    }
                };

                pnlGuildActions.Controls.Add(btnChangeLeader);
                pnlGuildActions.Controls.Add(btnKickMember);
                pnlGuildActions.Controls.Add(btnDisband);

                rightPanel.Controls.Add(ext_lblSelectedGuild);
                rightPanel.Controls.Add(lblRules);
                rightPanel.Controls.Add(ext_txtGuildRules);
                rightPanel.Controls.Add(btnSaveRules);
                rightPanel.Controls.Add(lblRoster);
                rightPanel.Controls.Add(ext_dgvGuildMembers);
                rightPanel.Controls.Add(pnlGuildActions);

                split.Panel2.Controls.Add(rightPanel);

                tabGuilds.Controls.Add(split);
                tabGuilds.Controls.Add(topPanel);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabGuilds);
                }

                RefreshGuildsList();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error setting up Guilds tab: {ex.Message}");
            }
        }

        private void RefreshGuildsList()
        {
            if (ext_dgvGuilds == null) return;
            try
            {
                ext_dgvGuilds.Rows.Clear();
                var guilds = GuildManager.GetAllGuilds();
                string filter = ext_txtGuildSearch?.Text.Trim().ToLower() ?? "";
                int totalMembers = 0;

                if (guilds != null)
                {
                    foreach (var g in guilds.Values)
                    {
                        totalMembers += g.MemberCount;
                        if (!string.IsNullOrEmpty(filter))
                        {
                            if (!g.GuildName.ToLower().Contains(filter) && !g.GuildID.ToString().Contains(filter))
                                continue;
                        }
                        ext_dgvGuilds.Rows.Add(
                            g.GuildID,
                            g.GuildName,
                            g.LeaderName,
                            g.LeaderID,
                            g.MemberCount,
                            g.DateCreated.ToString("yyyy-MM-dd HH:mm")
                        );
                    }
                    ext_lblGuildStats.Text = $"Total Guilds: {guilds.Count} | Members: {totalMembers}";
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error refreshing guilds: {ex.Message}");
            }
        }

        private void OnGuildSelectionChanged()
        {
            if (ext_dgvGuilds.SelectedRows.Count == 0)
            {
                ext_selectedGuild = null;
                ext_lblSelectedGuild.Text = "Selected: (None)";
                ext_txtGuildRules.Clear();
                ext_dgvGuildMembers.Rows.Clear();
                return;
            }

            ushort gId = Convert.ToUInt16(ext_dgvGuilds.SelectedRows[0].Cells["colGId"].Value);
            var guilds = GuildManager.GetAllGuilds();
            if (guilds != null && guilds.TryGetValue(gId, out var guild))
            {
                ext_selectedGuild = guild;
                ext_lblSelectedGuild.Text = $"Selected: {guild.GuildName} (ID: {gId})";
                ext_txtGuildRules.Text = guild.Rules ?? "";
                ext_dgvGuildMembers.Rows.Clear();

                var onlinePlayers = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                foreach (var m in guild.Members.Values)
                {
                    bool isOnline = onlinePlayers != null && onlinePlayers.Any(p => p.CharID == m.CharID);
                    ext_dgvGuildMembers.Rows.Add(
                        m.CharID,
                        m.CharName,
                        m.Level,
                        m.Rank.ToString(),
                        isOnline ? "Online" : "Offline"
                    );
                }
            }
        }
        #endregion

        #region TAB 3: In-Game Mail (tab_mail)
        private ComboBox ext_cmbMailTarget;
        private TextBox ext_txtMailRecipient;
        private TextBox ext_txtMailSubject;
        private TextBox ext_txtMailBody;
        private NumericUpDown ext_numMailGold;
        private NumericUpDown ext_numMailItemId;
        private NumericUpDown ext_numMailItemCount;
        private Label ext_lblMailItemPreview;
        private DataGridView ext_dgvMail;

        private void SetupMailTab()
        {
            try
            {
                TabPage tabMail = new TabPage(" In-Game Mail");
                tabMail.BackColor = Color.White;

                SplitContainer split = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical,
                    SplitterDistance = 380,
                    Panel1MinSize = 340,
                    Panel2MinSize = 340
                };

                // Left: Dispatch Mail Form
                Panel leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 12, 15, 12), AutoScroll = true };

                Label lblHeader = new Label
                {
                    Text = " Send GM Mail & Gifts",
                    Location = new Point(10, 10),
                    Size = new Size(330, 24),
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 41, 59)
                };

                Label lblTargetType = new Label { Text = "Recipient Target:", Location = new Point(10, 40), AutoSize = true, Font = new Font("Segoe UI", 9f) };
                ext_cmbMailTarget = new ComboBox { Location = new Point(10, 60), Width = 330, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };
                ext_cmbMailTarget.Items.AddRange(new object[] { "Single Character", "All Online Players", "All Registered Characters" });
                ext_cmbMailTarget.SelectedIndex = 0;
                ext_cmbMailTarget.SelectedIndexChanged += (s, e) =>
                {
                    ext_txtMailRecipient.Enabled = ext_cmbMailTarget.SelectedIndex == 0;
                };

                Label lblRecipient = new Label { Text = "Target Character Name or ID:", Location = new Point(10, 92), AutoSize = true, Font = new Font("Segoe UI", 9f) };
                ext_txtMailRecipient = new TextBox { Location = new Point(10, 112), Width = 330, Font = new Font("Segoe UI", 9.5f) };

                Label lblSubject = new Label { Text = "Mail Subject:", Location = new Point(10, 142), AutoSize = true, Font = new Font("Segoe UI", 9f) };
                ext_txtMailSubject = new TextBox { Location = new Point(10, 162), Width = 330, Text = " Server Special Gift", Font = new Font("Segoe UI", 9.5f) };

                Label lblBody = new Label { Text = "Message Body:", Location = new Point(10, 192), AutoSize = true, Font = new Font("Segoe UI", 9f) };
                ext_txtMailBody = new TextBox
                {
                    Location = new Point(10, 212),
                    Width = 330,
                    Height = 70,
                    Multiline = true,
                    Text = "Greetings Adventurer!\r\nPlease accept this reward from the server administration. Have fun!",
                    Font = new Font("Segoe UI", 9f)
                };

                // Attachments box
                GroupBox grpAttach = new GroupBox
                {
                    Text = " Attachments (Optional)",
                    Location = new Point(10, 290),
                    Width = 330,
                    Height = 135,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                };

                Label lblGold = new Label { Text = " Gold:", Location = new Point(15, 25), AutoSize = true, Font = new Font("Segoe UI", 9f) };
                ext_numMailGold = new NumericUpDown
                {
                    Location = new Point(90, 22),
                    Width = 150,
                    Maximum = 2000000000,
                    Font = new Font("Segoe UI", 9f)
                };

                Label lblItemId = new Label { Text = " Item ID:", Location = new Point(15, 55), AutoSize = true, Font = new Font("Segoe UI", 9f) };
                ext_numMailItemId = new NumericUpDown
                {
                    Location = new Point(90, 52),
                    Width = 100,
                    Maximum = 65535,
                    Font = new Font("Segoe UI", 9f)
                };

                Label lblCount = new Label { Text = "Qty:", Location = new Point(200, 55), AutoSize = true, Font = new Font("Segoe UI", 9f) };
                ext_numMailItemCount = new NumericUpDown
                {
                    Location = new Point(235, 52),
                    Width = 60,
                    Minimum = 1,
                    Maximum = 255,
                    Value = 1,
                    Font = new Font("Segoe UI", 9f)
                };

                ext_lblMailItemPreview = new Label
                {
                    Text = "Item: None",
                    Location = new Point(90, 80),
                    Size = new Size(220, 40),
                    ForeColor = Color.DarkCyan,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
                };

                ext_numMailItemId.ValueChanged += (s, e) =>
                {
                    ushort iid = (ushort)ext_numMailItemId.Value;
                    if (iid == 0)
                    {
                        ext_lblMailItemPreview.Text = "Item: None";
                    }
                    else
                    {
                        string iname = ResolveItemName(iid);
                        ext_lblMailItemPreview.Text = $"Item: {iname} ({iid})";
                    }
                };

                grpAttach.Controls.Add(lblGold);
                grpAttach.Controls.Add(ext_numMailGold);
                grpAttach.Controls.Add(lblItemId);
                grpAttach.Controls.Add(ext_numMailItemId);
                grpAttach.Controls.Add(lblCount);
                grpAttach.Controls.Add(ext_numMailItemCount);
                grpAttach.Controls.Add(ext_lblMailItemPreview);

                Button btnDispatch = new Button
                {
                    Text = " Dispatch Mail to Target(s)",
                    Location = new Point(10, 435),
                    Size = new Size(330, 36),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    BackColor = Color.FromArgb(16, 185, 129),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnDispatch.Click += (s, e) => ActionDispatchMail();

                leftPanel.Controls.Add(lblHeader);
                leftPanel.Controls.Add(lblTargetType);
                leftPanel.Controls.Add(ext_cmbMailTarget);
                leftPanel.Controls.Add(lblRecipient);
                leftPanel.Controls.Add(ext_txtMailRecipient);
                leftPanel.Controls.Add(lblSubject);
                leftPanel.Controls.Add(ext_txtMailSubject);
                leftPanel.Controls.Add(lblBody);
                leftPanel.Controls.Add(ext_txtMailBody);
                leftPanel.Controls.Add(grpAttach);
                leftPanel.Controls.Add(btnDispatch);

                split.Panel1.Controls.Add(leftPanel);

                // Right: Mailbox Records
                Panel rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 10, 10, 10) };
                Panel rightTop = new Panel { Dock = DockStyle.Top, Height = 42 };

                Label lblMailHistory = new Label
                {
                    Text = " Mailbox Records (charmail)",
                    Location = new Point(5, 10),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold)
                };

                Button btnRefreshMail = new Button { Text = " Refresh", Location = new Point(230, 7), Size = new Size(90, 28), Font = new Font("Segoe UI", 9f) };
                btnRefreshMail.Click += (s, e) => RefreshMailHistory();

                Button btnDeleteMail = new Button
                {
                    Text = " Delete Mail",
                    Location = new Point(330, 7),
                    Size = new Size(110, 28),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(220, 38, 38),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnDeleteMail.Click += (s, e) => ActionDeleteSelectedMail();

                rightTop.Controls.Add(lblMailHistory);
                rightTop.Controls.Add(btnRefreshMail);
                rightTop.Controls.Add(btnDeleteMail);

                ext_dgvMail = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = Color.White,
                    RowHeadersVisible = false,
                    Font = new Font("Segoe UI", 9f)
                };
                ext_dgvMail.Columns.Add("colMailId", "MailID");
                ext_dgvMail.Columns.Add("colSender", "Sender");
                ext_dgvMail.Columns.Add("colReceiverId", "ReceiverID");
                ext_dgvMail.Columns.Add("colSubject", "Subject");
                ext_dgvMail.Columns.Add("colGold", "Gold");
                ext_dgvMail.Columns.Add("colItemId", "ItemID");
                ext_dgvMail.Columns.Add("colItemName", "Item Name");
                ext_dgvMail.Columns.Add("colQty", "Qty");
                ext_dgvMail.Columns.Add("colClaimed", "Claimed");
                ext_dgvMail.Columns.Add("colDate", "Date");

                ext_dgvMail.Columns["colMailId"].Width = 60;
                ext_dgvMail.Columns["colReceiverId"].Width = 65;
                ext_dgvMail.Columns["colGold"].Width = 65;
                ext_dgvMail.Columns["colItemId"].Width = 60;
                ext_dgvMail.Columns["colQty"].Width = 45;
                ext_dgvMail.Columns["colClaimed"].Width = 60;
                ext_dgvMail.Columns["colDate"].Width = 110;
                ext_dgvMail.Columns["colSubject"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                rightPanel.Controls.Add(ext_dgvMail);
                rightPanel.Controls.Add(rightTop);
                split.Panel2.Controls.Add(rightPanel);

                tabMail.Controls.Add(split);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabMail);
                }

                RefreshMailHistory();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error setting up Mail tab: {ex.Message}");
            }
        }

        private void ActionDispatchMail()
        {
            try
            {
                string subject = ext_txtMailSubject.Text.Trim();
                string body = ext_txtMailBody.Text.Trim();
                uint gold = (uint)ext_numMailGold.Value;
                ushort itemId = (ushort)ext_numMailItemId.Value;
                byte count = (byte)ext_numMailItemCount.Value;

                if (string.IsNullOrEmpty(subject))
                {
                    MessageBox.Show("Please enter a mail subject.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int mode = ext_cmbMailTarget.SelectedIndex;
                if (mode == 0) // Single Character
                {
                    string target = ext_txtMailRecipient.Text.Trim();
                    if (string.IsNullOrEmpty(target))
                    {
                        MessageBox.Show("Please specify a target character name or ID.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    uint targetCharId = 0;
                    if (!uint.TryParse(target, out targetCharId))
                    {
                        // Look up character by name
                        var dt = cGlobal.gCharacterDataBase?.GetDataTable($"SELECT charID FROM characters WHERE charName = '{target.Replace("'", "''")}'");
                        if (dt != null && dt.Rows.Count > 0)
                        {
                            targetCharId = Convert.ToUInt32(dt.Rows[0]["charID"]);
                        }
                    }

                    if (targetCharId == 0)
                    {
                        MessageBox.Show($"Could not find character '{target}'.", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    MailSystem.AdminDispatchMail(targetCharId, "Server GM", subject, body, gold, itemId, count);
                    MessageBox.Show($"Mail dispatched to CharID {targetCharId}!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (mode == 1) // All Online Players
                {
                    var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                    if (online == null || online.Count == 0)
                    {
                        MessageBox.Show("No players are currently online.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int dispatched = 0;
                    foreach (var p in online)
                    {
                        MailSystem.AdminDispatchMail(p.CharID, "Server GM", subject, body, gold, itemId, count);
                        dispatched++;
                    }
                    MessageBox.Show($"Mail successfully dispatched to {dispatched} online player(s)!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (mode == 2) // All Registered Characters
                {
                    var dt = cGlobal.gCharacterDataBase?.GetDataTable("SELECT charID FROM characters");
                    if (dt == null || dt.Rows.Count == 0)
                    {
                        MessageBox.Show("No characters found in database.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int dispatched = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        uint cId = Convert.ToUInt32(row["charID"]);
                        MailSystem.AdminDispatchMail(cId, "Server GM", subject, body, gold, itemId, count);
                        dispatched++;
                    }
                    MessageBox.Show($"Mail successfully dispatched to all {dispatched} registered character(s)!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                RefreshMailHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error dispatching mail: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshMailHistory()
        {
            if (ext_dgvMail == null) return;
            try
            {
                ext_dgvMail.Rows.Clear();
                var mails = MailSystem.GetAllMails();
                if (mails != null)
                {
                    foreach (var m in mails)
                    {
                        string itemName = "-";
                        if (m.AttachedItemID > 0)
                        {
                            itemName = ResolveItemName(m.AttachedItemID);
                        }

                        ext_dgvMail.Rows.Add(
                            m.MailID,
                            m.SenderName,
                            m.ReceiverID,
                            m.Subject,
                            m.AttachedGold.ToString("N0"),
                            m.AttachedItemID,
                            itemName,
                            m.AttachedItemCount,
                            m.IsClaimed ? "Yes" : "No",
                            m.SentDate.ToString("yyyy-MM-dd HH:mm")
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error refreshing mail: {ex.Message}");
            }
        }

        private void ActionDeleteSelectedMail()
        {
            if (ext_dgvMail.SelectedRows.Count == 0) return;
            uint mailId = Convert.ToUInt32(ext_dgvMail.SelectedRows[0].Cells["colMailId"].Value);
            if (MessageBox.Show($"Delete mail message #{mailId}?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (MailSystem.AdminDeleteMail(mailId))
                {
                    RefreshMailHistory();
                }
            }
        }
        #endregion

        #region TAB 4: Security & Bans (tab_security)
        private DataGridView ext_dgvBannedIps;
        private DataGridView ext_dgvBannedAccounts;

        private void SetupSecurityTab()
        {
            try
            {
                TabPage tabSecurity = new TabPage(" Security & Bans");
                tabSecurity.BackColor = Color.White;

                EnsureSecurityTablesExist();

                SplitContainer split = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical,
                    SplitterDistance = 500,
                    Panel1MinSize = 320,
                    Panel2MinSize = 320
                };

                // Left: Banned IPs
                Panel pnlLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 10, 12, 10) };
                Panel pnlLeftTop = new Panel { Dock = DockStyle.Top, Height = 44 };

                Label lblIpsHeader = new Label
                {
                    Text = " Banned IP Addresses (banned_ips)",
                    Location = new Point(5, 12),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 41, 59)
                };

                Button btnRefreshIps = new Button { Text = " Refresh", Location = new Point(290, 8), Size = new Size(85, 28), Font = new Font("Segoe UI", 9f) };
                btnRefreshIps.Click += (s, e) => RefreshBannedIpsTable();

                pnlLeftTop.Controls.Add(lblIpsHeader);
                pnlLeftTop.Controls.Add(btnRefreshIps);

                ext_dgvBannedIps = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = Color.White,
                    RowHeadersVisible = false,
                    Font = new Font("Segoe UI", 9f)
                };
                ext_dgvBannedIps.Columns.Add("colBannedIp", "IP Address");
                ext_dgvBannedIps.Columns.Add("colIpReason", "Reason");
                ext_dgvBannedIps.Columns.Add("colIpDate", "Banned At");
                ext_dgvBannedIps.Columns.Add("colIpBy", "Banned By");

                ext_dgvBannedIps.Columns["colBannedIp"].Width = 120;
                ext_dgvBannedIps.Columns["colIpDate"].Width = 120;
                ext_dgvBannedIps.Columns["colIpBy"].Width = 85;
                ext_dgvBannedIps.Columns["colIpReason"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                FlowLayoutPanel pnlIpBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(5, 6, 5, 6) };

                Button btnAddIpBan = new Button
                {
                    Text = " Add IP Ban",
                    Size = new Size(130, 30),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(220, 38, 38),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnAddIpBan.Click += (s, e) =>
                {
                    string ip = ShowInputDialog("Enter IP Address to ban:", "Add IP Ban", "");
                    if (!string.IsNullOrEmpty(ip))
                    {
                        string reason = ShowInputDialog("Enter ban reason:", "Ban Reason", "Admin manual ban");
                        BanIpInternal(ip, reason ?? "Admin manual ban");
                        RefreshBannedIpsTable();
                    }
                };

                Button btnUnbanIp = new Button
                {
                    Text = " Unban Selected IP",
                    Size = new Size(140, 30),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(16, 185, 129),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnUnbanIp.Click += (s, e) =>
                {
                    if (ext_dgvBannedIps.SelectedRows.Count == 0) return;
                    string ip = ext_dgvBannedIps.SelectedRows[0].Cells["colBannedIp"].Value.ToString();
                    UnbanIpInternal(ip);
                    RefreshBannedIpsTable();
                };

                pnlIpBtns.Controls.Add(btnAddIpBan);
                pnlIpBtns.Controls.Add(btnUnbanIp);

                pnlLeft.Controls.Add(ext_dgvBannedIps);
                pnlLeft.Controls.Add(pnlLeftTop);
                pnlLeft.Controls.Add(pnlIpBtns);
                split.Panel1.Controls.Add(pnlLeft);

                // Right: Banned Accounts
                Panel pnlRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 10, 12, 10) };
                Panel pnlRightTop = new Panel { Dock = DockStyle.Top, Height = 44 };

                Label lblAccHeader = new Label
                {
                    Text = " Banned Accounts (banned_users)",
                    Location = new Point(5, 12),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 41, 59)
                };

                Button btnRefreshAcc = new Button { Text = " Refresh", Location = new Point(290, 8), Size = new Size(85, 28), Font = new Font("Segoe UI", 9f) };
                btnRefreshAcc.Click += (s, e) => RefreshBannedAccountsTable();

                pnlRightTop.Controls.Add(lblAccHeader);
                pnlRightTop.Controls.Add(btnRefreshAcc);

                ext_dgvBannedAccounts = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = Color.White,
                    RowHeadersVisible = false,
                    Font = new Font("Segoe UI", 9f)
                };
                ext_dgvBannedAccounts.Columns.Add("colAccId", "UserID");
                ext_dgvBannedAccounts.Columns.Add("colAccUser", "Username");
                ext_dgvBannedAccounts.Columns.Add("colAccReason", "Reason");
                ext_dgvBannedAccounts.Columns.Add("colAccDate", "Banned At");
                ext_dgvBannedAccounts.Columns.Add("colAccBy", "Banned By");

                ext_dgvBannedAccounts.Columns["colAccId"].Width = 65;
                ext_dgvBannedAccounts.Columns["colAccUser"].Width = 110;
                ext_dgvBannedAccounts.Columns["colAccDate"].Width = 120;
                ext_dgvBannedAccounts.Columns["colAccBy"].Width = 85;
                ext_dgvBannedAccounts.Columns["colAccReason"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                FlowLayoutPanel pnlAccBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(5, 6, 5, 6) };

                Button btnAddAccBan = new Button
                {
                    Text = " Ban Account",
                    Size = new Size(130, 30),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(220, 38, 38),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnAddAccBan.Click += (s, e) =>
                {
                    string username = ShowInputDialog("Enter Account Username to ban:", "Ban Account", "");
                    if (!string.IsNullOrEmpty(username))
                    {
                        string reason = ShowInputDialog("Enter ban reason:", "Ban Reason", "Admin manual ban");
                        BanAccountInternal(username, 0, reason ?? "Admin manual ban");
                        RefreshBannedAccountsTable();
                    }
                };

                Button btnUnbanAcc = new Button
                {
                    Text = " Unban Selected Account",
                    Size = new Size(160, 30),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(16, 185, 129),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnUnbanAcc.Click += (s, e) =>
                {
                    if (ext_dgvBannedAccounts.SelectedRows.Count == 0) return;
                    uint uId = Convert.ToUInt32(ext_dgvBannedAccounts.SelectedRows[0].Cells["colAccId"].Value);
                    string uName = ext_dgvBannedAccounts.SelectedRows[0].Cells["colAccUser"].Value.ToString();
                    UnbanAccountInternal(uId, uName);
                    RefreshBannedAccountsTable();
                };

                pnlAccBtns.Controls.Add(btnAddAccBan);
                pnlAccBtns.Controls.Add(btnUnbanAcc);

                pnlRight.Controls.Add(ext_dgvBannedAccounts);
                pnlRight.Controls.Add(pnlRightTop);
                pnlRight.Controls.Add(pnlAccBtns);
                split.Panel2.Controls.Add(pnlRight);

                tabSecurity.Controls.Add(split);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabSecurity);
                }

                RefreshBannedIpsTable();
                RefreshBannedAccountsTable();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error setting up Security tab: {ex.Message}");
            }
        }

        private void EnsureSecurityTablesExist()
        {
            try
            {
                cGlobal.gUserDataBase?.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS banned_ips (ip TEXT PRIMARY KEY, reason TEXT, banned_at TEXT, banned_by TEXT);");
                cGlobal.gUserDataBase?.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS banned_users (userID INT PRIMARY KEY, username TEXT, reason TEXT, banned_at TEXT, banned_by TEXT);");
                try
                {
                    cGlobal.gUserDataBase?.ExecuteNonQuery("ALTER TABLE users ADD COLUMN banned INTEGER DEFAULT 0;");
                }
                catch { }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error verifying security tables: {ex.Message}");
            }
        }

        private void BanIpInternal(string ip, string reason)
        {
            if (string.IsNullOrEmpty(ip)) return;
            try
            {
                EnsureSecurityTablesExist();
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                cGlobal.gUserDataBase?.ExecuteNonQuery($"INSERT OR REPLACE INTO banned_ips (ip, reason, banned_at, banned_by) VALUES ('{ip.Replace("'", "''")}', '{reason.Replace("'", "''")}', '{now}', 'Admin');");
                // Disconnect any online players matching this IP
                var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                if (online != null)
                {
                    foreach (var p in online.Where(pl => pl.SockAddress() == ip))
                    {
                        p.Disconnect();
                    }
                }
                MessageBox.Show($"IP '{ip}' has been banned.", "IP Banned", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error banning IP: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UnbanIpInternal(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return;
            try
            {
                cGlobal.gUserDataBase?.ExecuteNonQuery($"DELETE FROM banned_ips WHERE ip = '{ip.Replace("'", "''")}';");
                MessageBox.Show($"IP '{ip}' unbanned.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error unbanning IP: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BanAccountInternal(string username, uint userId, string reason)
        {
            try
            {
                EnsureSecurityTablesExist();
                if (userId == 0 && !string.IsNullOrEmpty(username))
                {
                    var dt = cGlobal.gUserDataBase?.GetDataTable($"SELECT userID FROM users WHERE username = '{username.Replace("'", "''")}'");
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        userId = Convert.ToUInt32(dt.Rows[0]["userID"]);
                    }
                }

                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                cGlobal.gUserDataBase?.ExecuteNonQuery($"INSERT OR REPLACE INTO banned_users (userID, username, reason, banned_at, banned_by) VALUES ('{userId}', '{username.Replace("'", "''")}', '{reason.Replace("'", "''")}', '{now}', 'Admin');");
                try
                {
                    cGlobal.gUserDataBase?.ExecuteNonQuery($"UPDATE users SET banned = 1 WHERE userID = '{userId}' OR username = '{username.Replace("'", "''")}';");
                }
                catch { }

                // Disconnect player if online
                var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                if (online != null)
                {
                    foreach (var p in online.Where(pl => pl.UserID == userId || (pl.UserAcc != null && pl.UserAcc.UserName.Equals(username, StringComparison.OrdinalIgnoreCase))))
                    {
                        p.Disconnect();
                    }
                }
                MessageBox.Show($"Account '{username}' (ID: {userId}) has been banned.", "Account Banned", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error banning account: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UnbanAccountInternal(uint userId, string username)
        {
            try
            {
                cGlobal.gUserDataBase?.ExecuteNonQuery($"DELETE FROM banned_users WHERE userID = '{userId}' OR username = '{username.Replace("'", "''")}';");
                try
                {
                    cGlobal.gUserDataBase?.ExecuteNonQuery($"UPDATE users SET banned = 0 WHERE userID = '{userId}' OR username = '{username.Replace("'", "''")}';");
                }
                catch { }
                MessageBox.Show($"Account '{username}' has been unbanned.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error unbanning account: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshBannedIpsTable()
        {
            if (ext_dgvBannedIps == null) return;
            try
            {
                ext_dgvBannedIps.Rows.Clear();
                EnsureSecurityTablesExist();
                var dt = cGlobal.gUserDataBase?.GetDataTable("SELECT ip, reason, banned_at, banned_by FROM banned_ips ORDER BY banned_at DESC");
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        ext_dgvBannedIps.Rows.Add(
                            row["ip"]?.ToString() ?? "",
                            row["reason"]?.ToString() ?? "",
                            row["banned_at"]?.ToString() ?? "",
                            row["banned_by"]?.ToString() ?? ""
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error refreshing banned IPs: {ex.Message}");
            }
        }

        private void RefreshBannedAccountsTable()
        {
            if (ext_dgvBannedAccounts == null) return;
            try
            {
                ext_dgvBannedAccounts.Rows.Clear();
                EnsureSecurityTablesExist();
                var dt = cGlobal.gUserDataBase?.GetDataTable("SELECT userID, username, reason, banned_at, banned_by FROM banned_users ORDER BY banned_at DESC");
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        ext_dgvBannedAccounts.Rows.Add(
                            row["userID"]?.ToString() ?? "",
                            row["username"]?.ToString() ?? "",
                            row["reason"]?.ToString() ?? "",
                            row["banned_at"]?.ToString() ?? "",
                            row["banned_by"]?.ToString() ?? ""
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error refreshing banned accounts: {ex.Message}");
            }
        }
        #endregion

        #region TAB 5: Live Battles Monitor (tab_battles)
        private DataGridView ext_dgvBattles;
        private Label ext_lblActiveBattlesBadge;
        private Label ext_lblSelectedBattle;
        private TextBox ext_txtBattleDetails;
        private uint ext_selectedBattleId = 0;

        private void SetupLiveBattlesTab()
        {
            try
            {
                TabPage tabBattles = new TabPage(" Live Battles");
                tabBattles.BackColor = Color.White;

                Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 8, 10, 8) };

                Button btnRefresh = new Button { Text = " Refresh Battles", Location = new Point(10, 9), Size = new Size(140, 28), Font = new Font("Segoe UI", 9f) };
                btnRefresh.Click += (s, e) => RefreshLiveBattles();

                ext_lblActiveBattlesBadge = new Label
                {
                    Text = " Active Battles: 0",
                    Location = new Point(165, 14),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(16, 185, 129),
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold)
                };

                Label lblSub = new Label
                {
                    Text = "Live turn-based combat monitor. Cleanly abort stuck encounters or force victory.",
                    Location = new Point(340, 15),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Font = new Font("Segoe UI", 9f)
                };

                topPanel.Controls.Add(btnRefresh);
                topPanel.Controls.Add(ext_lblActiveBattlesBadge);
                topPanel.Controls.Add(lblSub);

                SplitContainer split = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical,
                    SplitterDistance = 600,
                    Panel1MinSize = 350,
                    Panel2MinSize = 320
                };

                // Left: Battles Grid
                ext_dgvBattles = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = Color.White,
                    RowHeadersVisible = false,
                    Font = new Font("Segoe UI", 9f)
                };
                ext_dgvBattles.Columns.Add("colBId", "BattleID");
                ext_dgvBattles.Columns.Add("colBType", "Type");
                ext_dgvBattles.Columns.Add("colBMap", "MapID");
                ext_dgvBattles.Columns.Add("colBTurn", "Turn");
                ext_dgvBattles.Columns.Add("colBPlayer", "Player");
                ext_dgvBattles.Columns.Add("colBPet", "Pet");
                ext_dgvBattles.Columns.Add("colBEnemies", "Enemies");

                ext_dgvBattles.Columns["colBId"].Width = 70;
                ext_dgvBattles.Columns["colBType"].Width = 90;
                ext_dgvBattles.Columns["colBMap"].Width = 65;
                ext_dgvBattles.Columns["colBTurn"].Width = 55;
                ext_dgvBattles.Columns["colBPlayer"].Width = 110;
                ext_dgvBattles.Columns["colBPet"].Width = 100;
                ext_dgvBattles.Columns["colBEnemies"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                ext_dgvBattles.SelectionChanged += (s, e) => OnBattleSelectionChanged();
                split.Panel1.Controls.Add(ext_dgvBattles);

                // Right: Details & Overrides
                Panel rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 10, 12, 10) };

                Label lblDetailsHeader = new Label
                {
                    Text = " Battle Details & Overrides",
                    Location = new Point(10, 8),
                    Size = new Size(300, 22),
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 41, 59)
                };

                ext_lblSelectedBattle = new Label
                {
                    Text = "Selected Battle: (None)",
                    Location = new Point(10, 32),
                    Size = new Size(300, 20),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(217, 119, 6)
                };

                ext_txtBattleDetails = new TextBox
                {
                    Location = new Point(10, 58),
                    Width = 320,
                    Height = 280,
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Consolas", 9f),
                    BackColor = Color.FromArgb(248, 250, 252)
                };

                Panel pnlBattleBtns = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 90,
                    Padding = new Padding(10, 8, 10, 8)
                };

                Button btnForceWin = new Button
                {
                    Text = " Force Win (Victory)",
                    Location = new Point(10, 8),
                    Size = new Size(320, 34),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    BackColor = Color.FromArgb(16, 185, 129),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnForceWin.Click += (s, e) =>
                {
                    if (ext_selectedBattleId == 0) return;
                    if (PvEBattleManager.ForceWinBattle(ext_selectedBattleId))
                    {
                        MessageBox.Show("Victory forced! Player won the encounter.", "Battle Won", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RefreshLiveBattles();
                    }
                };

                Button btnForceAbort = new Button
                {
                    Text = " Force End / Abort Battle",
                    Location = new Point(10, 48),
                    Size = new Size(320, 34),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    BackColor = Color.FromArgb(220, 38, 38),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnForceAbort.Click += (s, e) =>
                {
                    if (ext_selectedBattleId == 0) return;
                    if (PvEBattleManager.ForceEndBattle(ext_selectedBattleId))
                    {
                        MessageBox.Show("Battle aborted! Combatants restored to normal map state.", "Battle Aborted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RefreshLiveBattles();
                    }
                };

                pnlBattleBtns.Controls.Add(btnForceWin);
                pnlBattleBtns.Controls.Add(btnForceAbort);

                rightPanel.Controls.Add(lblDetailsHeader);
                rightPanel.Controls.Add(ext_lblSelectedBattle);
                rightPanel.Controls.Add(ext_txtBattleDetails);
                rightPanel.Controls.Add(pnlBattleBtns);

                split.Panel2.Controls.Add(rightPanel);

                tabBattles.Controls.Add(split);
                tabBattles.Controls.Add(topPanel);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabBattles);
                }

                RefreshLiveBattles();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error setting up Live Battles tab: {ex.Message}");
            }
        }

        private void RefreshLiveBattles()
        {
            if (ext_dgvBattles == null) return;
            try
            {
                ext_dgvBattles.Rows.Clear();
                var battles = PvEBattleManager.ActiveBattles;
                if (battles != null)
                {
                    foreach (var kvp in battles)
                    {
                        uint bId = kvp.Key;
                        var b = kvp.Value;
                        string typeStr = b.IsPvP ? "PvP Match" : (b.IsRandomEncounter ? "Encounter" : "Scripted/NPC");
                        string playerStr = b.Player != null ? $"{b.Player.CharName} (Lv.{b.Player.Level})" : $"Player {bId}";
                        string petStr = b.HasPet ? $"{b.BattlePet?.PetName ?? "Pet"} ({b.PetHP} HP)" : "None";
                        string enemiesStr = string.Join(", ", b.Monsters.Select(m => $"{m.MonsterName} (Lv.{m.MonsterLevel})"));

                        ext_dgvBattles.Rows.Add(
                            bId,
                            typeStr,
                            b.Player?.MapID ?? 0,
                            b.Turn,
                            playerStr,
                            petStr,
                            enemiesStr
                        );
                    }
                    ext_lblActiveBattlesBadge.Text = $" Active Battles: {battles.Count}";
                }
                else
                {
                    ext_lblActiveBattlesBadge.Text = " Active Battles: 0";
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error refreshing battles: {ex.Message}");
            }
        }

        private void OnBattleSelectionChanged()
        {
            if (ext_dgvBattles.SelectedRows.Count == 0)
            {
                ext_selectedBattleId = 0;
                ext_lblSelectedBattle.Text = "Selected Battle: (None)";
                ext_txtBattleDetails.Clear();
                return;
            }

            ext_selectedBattleId = Convert.ToUInt32(ext_dgvBattles.SelectedRows[0].Cells["colBId"].Value);
            var battles = PvEBattleManager.ActiveBattles;
            if (battles != null && battles.TryGetValue(ext_selectedBattleId, out var b))
            {
                ext_lblSelectedBattle.Text = $"Selected Battle: #{ext_selectedBattleId}";

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"=== Battle ID: #{ext_selectedBattleId} ===");
                sb.AppendLine($"Turn: {b.Turn} | PvP: {b.IsPvP} | Finished: {b.IsFinished}");
                sb.AppendLine();
                sb.AppendLine("--- Attacking Players ---");
                foreach (var p in b.AttackingPlayers)
                {
                    sb.AppendLine($" - {p.CharName} (ID: {p.CharID}) Lvl:{p.Level} HP:{p.HP}/{p.MaxHP} SP:{p.SP}/{p.MaxSP}");
                }

                sb.AppendLine();
                sb.AppendLine("--- Companions / Pets ---");
                if (b.BattlePet != null)
                {
                    sb.AppendLine($" - {b.BattlePet.PetName} (ID: {b.BattlePet.PetID}) Lvl:{b.BattlePet.Level} HP:{b.PetHP}/{b.BattlePet.MaxHP} SP:{b.PetSP}/{b.BattlePet.MaxSP}");
                }
                else
                {
                    sb.AppendLine(" - (No active battle pet)");
                }

                sb.AppendLine();
                sb.AppendLine("--- Opponent Monsters ---");
                foreach (var m in b.Monsters)
                {
                    sb.AppendLine($" - {m.MonsterName} (ID:{m.MonsterId}) Lvl:{m.MonsterLevel} HP:{m.MonsterHP}/{m.MonsterMaxHP} SP:{m.MonsterSP}/{m.MonsterMaxSP} Pos:({m.GridX},{m.GridY})");
                }

                sb.AppendLine();
                sb.AppendLine($"Pending Actions Count: {b.PendingActions?.Count ?? 0} / Expected: {b.ExpectedActionCount}");

                ext_txtBattleDetails.Text = sb.ToString();
            }
        }
        #endregion

        #region TAB 6: Marriage Registry (tab_marriage)
        private DataGridView ext_dgvMarriages;
        private TextBox ext_txtMarriageSearch;
        private Label ext_lblMarriageStats;

        private void SetupMarriagesTab()
        {
            try
            {
                TabPage tabMarriage = new TabPage(" Marriages");
                tabMarriage.BackColor = Color.White;

                Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 8, 10, 8) };

                Button btnRefresh = new Button { Text = " Refresh Marriages", Location = new Point(10, 9), Size = new Size(150, 28), Font = new Font("Segoe UI", 9f) };
                btnRefresh.Click += (s, e) => RefreshMarriagesTable();

                Label lblFilter = new Label { Text = " Filter:", Location = new Point(175, 14), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
                ext_txtMarriageSearch = new TextBox { Location = new Point(230, 11), Width = 220, Font = new Font("Segoe UI", 9.5f) };
                ext_txtMarriageSearch.TextChanged += (s, e) => RefreshMarriagesTable();

                ext_lblMarriageStats = new Label
                {
                    Text = "Total Couples: 0",
                    Location = new Point(470, 14),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(236, 72, 153),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
                };

                topPanel.Controls.Add(btnRefresh);
                topPanel.Controls.Add(lblFilter);
                topPanel.Controls.Add(ext_txtMarriageSearch);
                topPanel.Controls.Add(ext_lblMarriageStats);

                ext_dgvMarriages = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = Color.White,
                    RowHeadersVisible = false,
                    Font = new Font("Segoe UI", 9f)
                };
                ext_dgvMarriages.Columns.Add("colHId", "HusbandID");
                ext_dgvMarriages.Columns.Add("colHName", "Husband Name");
                ext_dgvMarriages.Columns.Add("colWId", "WifeID");
                ext_dgvMarriages.Columns.Add("colWName", "Wife Name");
                ext_dgvMarriages.Columns.Add("colMDate", "Marriage Date");
                ext_dgvMarriages.Columns.Add("colMStatus", "Status");

                ext_dgvMarriages.Columns["colHId"].Width = 85;
                ext_dgvMarriages.Columns["colWId"].Width = 85;
                ext_dgvMarriages.Columns["colMDate"].Width = 140;
                ext_dgvMarriages.Columns["colMStatus"].Width = 90;
                ext_dgvMarriages.Columns["colHName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                ext_dgvMarriages.Columns["colWName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                Panel bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10, 8, 10, 8) };

                Button btnDivorce = new Button
                {
                    Text = " Admin Annul / Divorce",
                    Location = new Point(10, 8),
                    Size = new Size(180, 32),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(220, 38, 38),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnDivorce.Click += (s, e) =>
                {
                    if (ext_dgvMarriages.SelectedRows.Count == 0) return;
                    uint hId = Convert.ToUInt32(ext_dgvMarriages.SelectedRows[0].Cells["colHId"].Value);
                    string hName = ext_dgvMarriages.SelectedRows[0].Cells["colHName"].Value.ToString();
                    string wName = ext_dgvMarriages.SelectedRows[0].Cells["colWName"].Value.ToString();

                    if (MessageBox.Show($"Annul marriage between '{hName}' and '{wName}'?", "Confirm Divorce", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        if (MarriageManager.AdminDivorce(hId))
                        {
                            MessageBox.Show("Marriage successfully annulled.", "Divorce Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            RefreshMarriagesTable();
                        }
                    }
                };

                Button btnTeleportTogether = new Button
                {
                    Text = " Teleport Spouses Together",
                    Location = new Point(200, 8),
                    Size = new Size(210, 32),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(139, 92, 246),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnTeleportTogether.Click += (s, e) =>
                {
                    if (ext_dgvMarriages.SelectedRows.Count == 0) return;
                    uint hId = Convert.ToUInt32(ext_dgvMarriages.SelectedRows[0].Cells["colHId"].Value);
                    uint wId = Convert.ToUInt32(ext_dgvMarriages.SelectedRows[0].Cells["colWId"].Value);

                    var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                    var husband = online?.FirstOrDefault(p => p.CharID == hId);
                    var wife = online?.FirstOrDefault(p => p.CharID == wId);

                    if (husband != null && wife != null)
                    {
                        wife.CurMap?.Teleport(TeleportType.CmD, wife, 0, new WarpData { DstMap = husband.MapID, DstX_Axis = husband.X, DstY_Axis = husband.Y });
                        MessageBox.Show($"Teleported '{wife.CharName}' to spouse '{husband.CharName}' at Map #{husband.MapID} ({husband.X}, {husband.Y})!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else if (husband == null && wife == null)
                    {
                        MessageBox.Show("Neither spouse is currently online.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        string onlineSpouse = husband != null ? husband.CharName : wife.CharName;
                        MessageBox.Show($"Only '{onlineSpouse}' is currently online. Both spouses must be online to teleport together.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                bottomPanel.Controls.Add(btnDivorce);
                bottomPanel.Controls.Add(btnTeleportTogether);

                tabMarriage.Controls.Add(ext_dgvMarriages);
                tabMarriage.Controls.Add(topPanel);
                tabMarriage.Controls.Add(bottomPanel);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabMarriage);
                }

                RefreshMarriagesTable();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error setting up Marriages tab: {ex.Message}");
            }
        }

        private void RefreshMarriagesTable()
        {
            if (ext_dgvMarriages == null) return;
            try
            {
                ext_dgvMarriages.Rows.Clear();
                var records = MarriageManager.GetAllMarriages();
                string filter = ext_txtMarriageSearch?.Text.Trim().ToLower() ?? "";

                if (records != null)
                {
                    foreach (var m in records)
                    {
                        if (!string.IsNullOrEmpty(filter))
                        {
                            if (!m.HusbandName.ToLower().Contains(filter) && !m.WifeName.ToLower().Contains(filter))
                                continue;
                        }

                        ext_dgvMarriages.Rows.Add(
                            m.HusbandID,
                            m.HusbandName,
                            m.WifeID,
                            m.WifeName,
                            m.MarriageDate.ToString("yyyy-MM-dd HH:mm"),
                            "Married"
                        );
                    }
                    ext_lblMarriageStats.Text = $"Total Couples: {records.Count}";
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error refreshing marriages: {ex.Message}");
            }
        }
        #endregion

        #region TAB 7: Starter Items Pack (tab_starter)
        private DataGridView ext_dgvStarters;
        private Label ext_lblStarterSummary;

        private void SetupStarterItemsTab()
        {
            try
            {
                TabPage tabStarter = new TabPage(" Starter Items");
                tabStarter.BackColor = Color.White;

                Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 8, 10, 8) };

                Button btnReload = new Button { Text = " Reload Starters", Location = new Point(10, 9), Size = new Size(125, 28), Font = new Font("Segoe UI", 9f) };
                btnReload.Click += (s, e) => RefreshStarterItemsTable();

                Button btnAdd = new Button
                {
                    Text = " Add Starter Item",
                    Location = new Point(145, 9),
                    Size = new Size(140, 28),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(16, 185, 129),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnAdd.Click += (s, e) => ActionAddStarterItem();

                Button btnEdit = new Button
                {
                    Text = " Edit Selected",
                    Location = new Point(295, 9),
                    Size = new Size(115, 28),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(37, 99, 235),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnEdit.Click += (s, e) => ActionEditStarterItem();

                Button btnDelete = new Button
                {
                    Text = " Remove Item",
                    Location = new Point(420, 9),
                    Size = new Size(115, 28),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(220, 38, 38),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnDelete.Click += (s, e) => ActionDeleteStarterItem();

                Button btnImport = new Button { Text = " Import JSON", Location = new Point(545, 9), Size = new Size(110, 28), Font = new Font("Segoe UI", 9f) };
                btnImport.Click += (s, e) => ActionImportStarterJson();

                Button btnExport = new Button { Text = " Export JSON", Location = new Point(665, 9), Size = new Size(110, 28), Font = new Font("Segoe UI", 9f) };
                btnExport.Click += (s, e) => ActionExportStarterJson();

                Button btnGiveOnline = new Button { Text = " Give to All Online", Location = new Point(785, 9), Size = new Size(140, 28), Font = new Font("Segoe UI", 9f) };
                btnGiveOnline.Click += (s, e) => ActionGiveStarterPackToOnline();

                ext_lblStarterSummary = new Label
                {
                    Text = "Items: 0",
                    Location = new Point(935, 14),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(16, 185, 129),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
                };

                topPanel.Controls.Add(btnReload);
                topPanel.Controls.Add(btnAdd);
                topPanel.Controls.Add(btnEdit);
                topPanel.Controls.Add(btnDelete);
                topPanel.Controls.Add(btnImport);
                topPanel.Controls.Add(btnExport);
                topPanel.Controls.Add(btnGiveOnline);
                topPanel.Controls.Add(ext_lblStarterSummary);

                ext_dgvStarters = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = Color.White,
                    RowHeadersVisible = false,
                    Font = new Font("Segoe UI", 9f)
                };
                ext_dgvStarters.Columns.Add("colSOrder", "Order");
                ext_dgvStarters.Columns.Add("colSItemId", "ItemID");
                ext_dgvStarters.Columns.Add("colSName", "Item Name");
                ext_dgvStarters.Columns.Add("colSQty", "Quantity");
                ext_dgvStarters.Columns.Add("colSDesc", "Description");

                ext_dgvStarters.Columns["colSOrder"].Width = 60;
                ext_dgvStarters.Columns["colSItemId"].Width = 80;
                ext_dgvStarters.Columns["colSQty"].Width = 70;
                ext_dgvStarters.Columns["colSName"].Width = 220;
                ext_dgvStarters.Columns["colSDesc"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                ext_dgvStarters.DoubleClick += (s, e) => ActionEditStarterItem();

                tabStarter.Controls.Add(ext_dgvStarters);
                tabStarter.Controls.Add(topPanel);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabStarter);
                }

                RefreshStarterItemsTable();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error setting up Starter Items tab: {ex.Message}");
            }
        }

        private void RefreshStarterItemsTable()
        {
            if (ext_dgvStarters == null) return;
            try
            {
                ext_dgvStarters.Rows.Clear();
                var items = StarterPackManager.GetItems();
                if (items != null)
                {
                    foreach (var it in items.OrderBy(i => i.OrderIdx))
                    {
                        ext_dgvStarters.Rows.Add(
                            it.OrderIdx,
                            it.ItemID,
                            it.ItemName,
                            it.Count,
                            it.Description
                        );
                    }
                    ext_lblStarterSummary.Text = $"Items: {items.Count}";
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error refreshing starter items: {ex.Message}");
            }
        }

        private void ActionAddStarterItem()
        {
            string strId = ShowInputDialog("Enter Item ID to add:", "Add Starter Item", "");
            if (int.TryParse(strId, out int itemId) && itemId > 0)
            {
                string defaultName = ResolveItemName((ushort)itemId);

                string name = ShowInputDialog("Item Name:", "Starter Item Name", defaultName) ?? defaultName;
                string strQty = ShowInputDialog("Quantity:", "Starter Item Quantity", "1") ?? "1";
                int.TryParse(strQty, out int qty);
                qty = Math.Max(1, qty);

                string desc = ShowInputDialog("Description:", "Starter Item Description", "Starter gift reward") ?? "";

                StarterPackManager.AddItem(itemId, name, qty, desc);
                RefreshStarterItemsTable();
            }
        }

        private void ActionEditStarterItem()
        {
            if (ext_dgvStarters.SelectedRows.Count == 0) return;
            var row = ext_dgvStarters.SelectedRows[0];
            int order = Convert.ToInt32(row.Cells["colSOrder"].Value);
            int itemId = Convert.ToInt32(row.Cells["colSItemId"].Value);
            string name = row.Cells["colSName"].Value.ToString();
            int qty = Convert.ToInt32(row.Cells["colSQty"].Value);
            string desc = row.Cells["colSDesc"].Value?.ToString() ?? "";

            string strNewId = ShowInputDialog("Item ID:", "Edit Starter Item", itemId.ToString());
            if (int.TryParse(strNewId, out int newItemId) && newItemId > 0)
            {
                string newName = ShowInputDialog("Item Name:", "Edit Starter Item", name) ?? name;
                string strQty = ShowInputDialog("Quantity:", "Edit Starter Item", qty.ToString()) ?? qty.ToString();
                int.TryParse(strQty, out int newQty);
                newQty = Math.Max(1, newQty);

                string newDesc = ShowInputDialog("Description:", "Edit Starter Item", desc) ?? desc;

                StarterPackManager.UpdateItem(order, newItemId, newName, newQty, newDesc);
                RefreshStarterItemsTable();
            }
        }

        private void ActionDeleteStarterItem()
        {
            if (ext_dgvStarters.SelectedRows.Count == 0) return;
            int itemId = Convert.ToInt32(ext_dgvStarters.SelectedRows[0].Cells["colSItemId"].Value);
            string name = ext_dgvStarters.SelectedRows[0].Cells["colSName"].Value.ToString();

            if (MessageBox.Show($"Remove starter item '{name}' (ID: {itemId})?", "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                StarterPackManager.DeleteItem(itemId);
                RefreshStarterItemsTable();
            }
        }

        private void ActionImportStarterJson()
        {
            try
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                    ofd.Title = "Import Starter Items JSON";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        string json = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                        if (StarterPackManager.ImportJson(json))
                        {
                            MessageBox.Show("Starter items successfully imported!", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            RefreshStarterItemsTable();
                        }
                        else
                        {
                            MessageBox.Show("Failed to parse JSON file.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing JSON: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ActionExportStarterJson()
        {
            try
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                    sfd.Title = "Export Starter Items JSON";
                    sfd.FileName = "starter_items.json";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        string json = StarterPackManager.ExportJson();
                        File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                        MessageBox.Show($"Starter items exported to {Path.GetFileName(sfd.FileName)}!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting JSON: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ActionGiveStarterPackToOnline()
        {
            try
            {
                var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                if (online == null || online.Count == 0)
                {
                    MessageBox.Show("No players are currently online.", "Starter Pack", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (MessageBox.Show($"Deliver the configured starter item pack to all {online.Count} online player(s)?", "Confirm Starter Pack Delivery", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    int count = 0;
                    foreach (var p in online)
                    {
                        StarterPackManager.DeliverToPlayer(p, sendData: true);
                        try { cGlobal.gCharacterDataBase.WritePlayer(p.CharID, p); } catch { }
                        count++;
                    }
                    MessageBox.Show($"Successfully delivered starter item pack to {count} online player(s)!", "Delivery Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error delivering starter items: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion
    }
}
