using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Server.Services;

namespace Wonderland_Private_Server
{
    /// <summary>
    /// Server tool pages: database backups, scheduled announcements, activity (sessions + chat)
    /// and the always-visible status bar at the bottom of the window.
    /// </summary>
    public partial class Form1
    {
        private TabPage tabBackups;
        private TabPage tabAnnouncements;
        private TabPage tabActivity;

        // Backups
        private CheckBox st_chkBackupEnabled;
        private NumericUpDown st_numBackupInterval;
        private NumericUpDown st_numBackupKeep;
        private Label st_lblBackupStatus;
        private DataGridView st_dgvBackups;

        // Announcements
        private CheckBox st_chkAnnEnabled;
        private NumericUpDown st_numAnnInterval;
        private ComboBox st_cmbAnnTarget;
        private ListBox st_lstAnnouncements;
        private TextBox st_txtNewAnnouncement;
        private Label st_lblAnnStatus;

        // Activity
        private ComboBox st_cmbActivityDay;
        private TextBox st_txtActivityFilter;
        private DataGridView st_dgvSessions;
        private DataGridView st_dgvChat;
        private Label st_lblActivitySummary;

        // Status bar
        private Panel st_statusBar;
        private Label st_lblStatusPeak;
        private Label st_lblStatusSave;
        private Label st_lblStatusBackup;
        private Label st_lblStatusAnnounce;
        private Label st_lblStatusErrors;

        private static string SettingsFilePath =>
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\PServer\\Config.settings.wlo";

        public void SetupServerToolsTabs()
        {
            try
            {
                tabBackups = BuildBackupsTab();
                tabAnnouncements = BuildAnnouncementsTab();
                tabActivity = BuildActivityTab();
                this.tabControl3?.TabPages.Add(tabBackups);
                this.tabControl3?.TabPages.Add(tabAnnouncements);
                this.tabControl3?.TabPages.Add(tabActivity);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, "[GUI] Error setting up server tool tabs: " + ex.Message);
            }
        }

        /// <summary>Called every time one of the tool pages is opened.</summary>
        private void RefreshServerToolPage(TabPage tab)
        {
            if (tab == tabBackups) RefreshBackupsPage();
            else if (tab == tabAnnouncements) RefreshAnnouncementsPage();
            else if (tab == tabActivity) RefreshActivityPage();
        }

        private void SaveServerSettings()
        {
            try
            {
                cGlobal.SrvSettings?.SaveSettings(SettingsFilePath);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, "[GUI] Could not save settings: " + ex.Message);
            }
        }

        #region Shared helpers

        private static GroupBox ToolCard(string title, Point location, Size size)
        {
            return new GroupBox { Text = title, Location = location, Size = size, Padding = new Padding(10) };
        }

        private static Label ToolLabel(string text, Point location, bool bold = false)
        {
            return new Label
            {
                Text = text,
                Location = location,
                AutoSize = true,
                Font = bold ? ModernTheme.FontBold : ModernTheme.FontDefault,
                UseMnemonic = false
            };
        }

        private static Button ToolButton(string text, Point location, int width, Color? solid = null)
        {
            var b = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(width, 28),
                Font = ModernTheme.FontBoldSmall,
                UseMnemonic = false
            };
            if (solid.HasValue)
            {
                b.BackColor = solid.Value;
                b.ForeColor = Color.White;
            }
            return b;
        }

        private static DataGridView ToolGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            ModernTheme.StyleGrid(g);
            return g;
        }

        /// <summary>Gives the first columns fixed widths; the remaining columns share the rest.</summary>
        private static void SetFixedColumnWidths(DataGridView grid, params int[] widths)
        {
            for (int i = 0; i < grid.Columns.Count; i++)
            {
                var col = grid.Columns[i];
                if (i < widths.Length)
                {
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    col.Width = widths[i];
                }
                else
                {
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                }
            }
        }

        private static string Ago(DateTime? when)
        {
            if (when == null) return "never";
            TimeSpan t = DateTime.Now - when.Value;
            if (t.TotalSeconds < 60) return $"{Math.Max(0, (int)t.TotalSeconds)}s ago";
            if (t.TotalMinutes < 60) return $"{(int)t.TotalMinutes}m ago";
            return $"{(int)t.TotalHours}h {t.Minutes}m ago";
        }

        private static void OpenFolder(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open folder: " + ex.Message, "Open Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        #endregion

        #region Backups page

        private TabPage BuildBackupsTab()
        {
            var page = new TabPage("Backups") { Padding = new Padding(12) };

            var top = new Panel { Dock = DockStyle.Top, Height = 168 };

            var cardSettings = ToolCard("Automatic Backups", new Point(0, 0), new Size(430, 156));
            cardSettings.Dock = DockStyle.Left;
            st_chkBackupEnabled = new CheckBox { Text = "Back up the database automatically", Location = new Point(14, 28), AutoSize = true };
            cardSettings.Controls.Add(st_chkBackupEnabled);
            cardSettings.Controls.Add(ToolLabel("Every", new Point(14, 62)));
            st_numBackupInterval = new NumericUpDown { Location = new Point(110, 59), Width = 70, Minimum = 5, Maximum = 1440, Value = 30, Font = ModernTheme.FontDefault };
            cardSettings.Controls.Add(st_numBackupInterval);
            cardSettings.Controls.Add(ToolLabel("minutes", new Point(186, 62)));
            cardSettings.Controls.Add(ToolLabel("Keep last", new Point(14, 94)));
            st_numBackupKeep = new NumericUpDown { Location = new Point(110, 91), Width = 70, Minimum = 1, Maximum = 200, Value = 10, Font = ModernTheme.FontDefault };
            cardSettings.Controls.Add(st_numBackupKeep);
            cardSettings.Controls.Add(ToolLabel("backups", new Point(186, 94)));
            var btnSave = ToolButton("Save Settings", new Point(300, 112), 116, ModernTheme.Blue600);
            btnSave.Click += (s, e) =>
            {
                if (cGlobal.SrvSettings == null) return;
                cGlobal.SrvSettings.BackupEnabled = st_chkBackupEnabled.Checked;
                cGlobal.SrvSettings.BackupIntervalMinutes = (int)st_numBackupInterval.Value;
                cGlobal.SrvSettings.BackupKeepCount = (int)st_numBackupKeep.Value;
                SaveServerSettings();
                DebugSystem.Write($"[Backup] Settings saved: enabled={cGlobal.SrvSettings.BackupEnabled}, every {cGlobal.SrvSettings.BackupIntervalMinutes} min, keep {cGlobal.SrvSettings.BackupKeepCount}.");
                RefreshBackupsPage();
            };
            cardSettings.Controls.Add(btnSave);

            var cardActions = ToolCard("Backup Now", new Point(442, 0), new Size(600, 156));
            cardActions.Dock = DockStyle.Fill;
            var btnBackup = ToolButton("Back Up Now", new Point(14, 28), 130, ModernTheme.Emerald600);
            btnBackup.Click += (s, e) =>
            {
                Cursor = Cursors.WaitCursor;
                string file = DatabaseBackupService.BackupNow("manual", out string error);
                Cursor = Cursors.Default;
                if (file == null)
                {
                    MessageBox.Show("Backup failed: " + error, "Backup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                RefreshBackupsPage();
            };
            var btnOpen = ToolButton("Open Folder", new Point(152, 28), 110);
            btnOpen.Click += (s, e) => OpenFolder(DatabaseBackupService.BackupDirectory);
            var btnRefresh = ToolButton("Refresh", new Point(270, 28), 90);
            btnRefresh.Click += (s, e) => RefreshBackupsPage();
            st_lblBackupStatus = new Label
            {
                Location = new Point(14, 66),
                Size = new Size(570, 84),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = ModernTheme.FontSmall,
                ForeColor = ModernTheme.Slate600,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            cardActions.Controls.Add(btnBackup);
            cardActions.Controls.Add(btnOpen);
            cardActions.Controls.Add(btnRefresh);
            cardActions.Controls.Add(st_lblBackupStatus);

            top.Controls.Add(cardActions);
            top.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 12 });
            top.Controls.Add(cardSettings);

            st_dgvBackups = ToolGrid();
            st_dgvBackups.Columns.Add("colFile", "Backup File");
            st_dgvBackups.Columns.Add("colCreated", "Created");
            st_dgvBackups.Columns.Add("colSize", "Size");
            st_dgvBackups.Columns["colFile"].FillWeight = 220;

            page.Controls.Add(st_dgvBackups);
            page.Controls.Add(top);
            return page;
        }

        private void RefreshBackupsPage()
        {
            try
            {
                var s = cGlobal.SrvSettings;
                if (s != null)
                {
                    st_chkBackupEnabled.Checked = s.BackupEnabled;
                    st_numBackupInterval.Value = Math.Max(st_numBackupInterval.Minimum, Math.Min(st_numBackupInterval.Maximum, s.BackupIntervalMinutes));
                    st_numBackupKeep.Value = Math.Max(st_numBackupKeep.Minimum, Math.Min(st_numBackupKeep.Maximum, s.BackupKeepCount));
                }

                var backups = DatabaseBackupService.ListBackups();
                st_dgvBackups.Rows.Clear();
                foreach (var f in backups)
                {
                    st_dgvBackups.Rows.Add(f.Name, f.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"), $"{f.Length / 1024:N0} KB");
                }

                string dbLine = DatabaseBackupService.TryGetDatabaseFile(out string dbFile)
                    ? "Database: " + Path.GetFileName(dbFile) + " (SQLite)"
                    : "Database: not SQLite — automatic backups are not available.";
                string last = ServerMetrics.LastBackup.HasValue
                    ? $"Last backup: {ServerMetrics.LastBackup:yyyy-MM-dd HH:mm:ss} ({Ago(ServerMetrics.LastBackup)})"
                    : (backups.Count > 0 ? $"Newest file: {backups[0].CreationTime:yyyy-MM-dd HH:mm:ss}" : "No backups yet.");
                string err = ServerMetrics.LastBackupError != null ? "\nLast error: " + ServerMetrics.LastBackupError : "";
                st_lblBackupStatus.Text = $"{last}  ·  {backups.Count} file(s) stored\n{dbLine}\nFolder: {DatabaseBackupService.BackupDirectory}{err}";
            }
            catch (Exception ex)
            {
                st_lblBackupStatus.Text = "Could not read backups: " + ex.Message;
            }
        }

        #endregion

        #region Announcements page

        private TabPage BuildAnnouncementsTab()
        {
            var page = new TabPage("Announcements") { Padding = new Padding(12) };

            var cardSchedule = ToolCard("Schedule", new Point(0, 0), new Size(360, 214));
            cardSchedule.Dock = DockStyle.Left;
            st_chkAnnEnabled = new CheckBox { Text = "Send announcements automatically", Location = new Point(14, 28), AutoSize = true };
            cardSchedule.Controls.Add(st_chkAnnEnabled);
            cardSchedule.Controls.Add(ToolLabel("Every", new Point(14, 62)));
            st_numAnnInterval = new NumericUpDown { Location = new Point(90, 59), Width = 70, Minimum = 1, Maximum = 1440, Value = 15, Font = ModernTheme.FontDefault };
            cardSchedule.Controls.Add(st_numAnnInterval);
            cardSchedule.Controls.Add(ToolLabel("minutes, one message at a time", new Point(166, 62)));
            cardSchedule.Controls.Add(ToolLabel("Show as", new Point(14, 96)));
            st_cmbAnnTarget = new ComboBox { Location = new Point(90, 92), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, Font = ModernTheme.FontDefault };
            st_cmbAnnTarget.Items.AddRange(new object[] { "Notification box", "World chat (yellow)" });
            st_cmbAnnTarget.SelectedIndex = 0;
            cardSchedule.Controls.Add(st_cmbAnnTarget);
            var btnSave = ToolButton("Save Schedule", new Point(14, 132), 130, ModernTheme.Blue600);
            btnSave.Click += (s, e) => SaveAnnouncements("Schedule saved.");
            cardSchedule.Controls.Add(btnSave);
            st_lblAnnStatus = new Label
            {
                Location = new Point(14, 168),
                Size = new Size(336, 40),
                ForeColor = ModernTheme.Slate600,
                Font = ModernTheme.FontSmall,
                UseMnemonic = false
            };
            cardSchedule.Controls.Add(st_lblAnnStatus);

            var cardMessages = new GroupBox
            {
                Text = "Messages (sent in this order)",
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 12, 12, 12)
            };
            var inputRow = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(0, 4, 0, 6) };
            st_txtNewAnnouncement = new TextBox { Dock = DockStyle.Fill, Font = ModernTheme.FontDefault };
            ModernTheme.SetPlaceholder(st_txtNewAnnouncement, "Type a message and press Enter...");
            var btnAdd = ToolButton("Add", Point.Empty, 80, ModernTheme.Emerald600);
            btnAdd.Dock = DockStyle.Right;
            btnAdd.Click += (s, e) => AddAnnouncement();
            st_txtNewAnnouncement.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { AddAnnouncement(); e.Handled = true; e.SuppressKeyPress = true; }
            };
            inputRow.Controls.Add(st_txtNewAnnouncement);
            inputRow.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            inputRow.Controls.Add(btnAdd);

            var actionRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(0, 8, 0, 0) };
            var btnSendNow = ToolButton("Send Selected Now", Point.Empty, 150, ModernTheme.Blue600);
            btnSendNow.Click += (s, e) =>
            {
                if (!(st_lstAnnouncements.SelectedItem is string text)) return;
                int sent = AnnouncementScheduler.Send(text, st_cmbAnnTarget.SelectedIndex);
                DebugSystem.Write($"[Announce] Manual announcement sent to {sent} player(s): {text}");
                MessageBox.Show(sent > 0 ? $"Sent to {sent} online player(s)." : "No players are online.", "Announcement",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshAnnouncementsPage();
            };
            var btnUp = ToolButton("Move Up", Point.Empty, 90);
            btnUp.Click += (s, e) => MoveAnnouncement(-1);
            var btnDown = ToolButton("Move Down", Point.Empty, 100);
            btnDown.Click += (s, e) => MoveAnnouncement(1);
            var btnRemove = ToolButton("Remove", Point.Empty, 90, ModernTheme.Rose600);
            btnRemove.Click += (s, e) =>
            {
                int i = st_lstAnnouncements.SelectedIndex;
                if (i < 0) return;
                st_lstAnnouncements.Items.RemoveAt(i);
                if (st_lstAnnouncements.Items.Count > 0) st_lstAnnouncements.SelectedIndex = Math.Min(i, st_lstAnnouncements.Items.Count - 1);
                SaveAnnouncements(null);
            };
            actionRow.Controls.AddRange(new Control[] { btnSendNow, btnUp, btnDown, btnRemove });

            st_lstAnnouncements = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Font = ModernTheme.FontDefault };

            cardMessages.Controls.Add(st_lstAnnouncements);
            cardMessages.Controls.Add(actionRow);
            cardMessages.Controls.Add(inputRow);
            cardMessages.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 14 });

            page.Controls.Add(cardMessages);
            page.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 12 });
            page.Controls.Add(cardSchedule);
            return page;
        }

        private void AddAnnouncement()
        {
            string text = st_txtNewAnnouncement.Text.Trim();
            if (text.Length == 0) return;
            st_lstAnnouncements.Items.Add(text);
            st_lstAnnouncements.SelectedIndex = st_lstAnnouncements.Items.Count - 1;
            st_txtNewAnnouncement.Clear();
            SaveAnnouncements(null);
        }

        private void MoveAnnouncement(int delta)
        {
            int i = st_lstAnnouncements.SelectedIndex;
            int j = i + delta;
            if (i < 0 || j < 0 || j >= st_lstAnnouncements.Items.Count) return;
            object item = st_lstAnnouncements.Items[i];
            st_lstAnnouncements.Items.RemoveAt(i);
            st_lstAnnouncements.Items.Insert(j, item);
            st_lstAnnouncements.SelectedIndex = j;
            SaveAnnouncements(null);
        }

        private void SaveAnnouncements(string confirmation)
        {
            var s = cGlobal.SrvSettings;
            if (s == null) return;
            s.AnnouncementsEnabled = st_chkAnnEnabled.Checked;
            s.AnnouncementIntervalMinutes = (int)st_numAnnInterval.Value;
            s.AnnouncementTarget = Math.Max(0, st_cmbAnnTarget.SelectedIndex);
            s.Announcements = st_lstAnnouncements.Items.Cast<string>().ToList();
            SaveServerSettings();
            AnnouncementScheduler.ResetCountdown();
            if (confirmation != null)
            {
                DebugSystem.Write($"[Announce] {confirmation} Enabled={s.AnnouncementsEnabled}, every {s.AnnouncementIntervalMinutes} min, {s.Announcements.Count} message(s).");
            }
            UpdateAnnouncementStatus();
        }

        private void RefreshAnnouncementsPage()
        {
            var s = cGlobal.SrvSettings;
            if (s == null) return;
            st_chkAnnEnabled.Checked = s.AnnouncementsEnabled;
            st_numAnnInterval.Value = Math.Max(st_numAnnInterval.Minimum, Math.Min(st_numAnnInterval.Maximum, s.AnnouncementIntervalMinutes));
            st_cmbAnnTarget.SelectedIndex = s.AnnouncementTarget == 1 ? 1 : 0;
            int selected = st_lstAnnouncements.SelectedIndex;
            st_lstAnnouncements.BeginUpdate();
            st_lstAnnouncements.Items.Clear();
            foreach (string m in s.Announcements ?? new List<string>()) st_lstAnnouncements.Items.Add(m);
            st_lstAnnouncements.EndUpdate();
            if (selected >= 0 && selected < st_lstAnnouncements.Items.Count) st_lstAnnouncements.SelectedIndex = selected;
            UpdateAnnouncementStatus();
        }

        private void UpdateAnnouncementStatus()
        {
            if (st_lblAnnStatus == null) return;
            DateTime? next = AnnouncementScheduler.NextDueAt;
            string nextText = next.HasValue ? $"Next: {next:HH:mm}" : "Scheduler is off or has no messages";
            string lastText = AnnouncementScheduler.LastSentAt.HasValue ? $"  ·  Last sent {Ago(AnnouncementScheduler.LastSentAt)}" : "";
            st_lblAnnStatus.Text = nextText + lastText;
        }

        #endregion

        #region Activity page

        private TabPage BuildActivityTab()
        {
            var page = new TabPage("Activity") { Padding = new Padding(12) };

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 42 };
            toolbar.Controls.Add(ToolLabel("Day:", new Point(0, 11), true));
            st_cmbActivityDay = new ComboBox { Location = new Point(40, 7), Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, Font = ModernTheme.FontDefault };
            st_cmbActivityDay.SelectedIndexChanged += (s, e) => LoadActivityRows();
            toolbar.Controls.Add(st_cmbActivityDay);
            toolbar.Controls.Add(ToolLabel("Filter:", new Point(186, 11), true));
            st_txtActivityFilter = new TextBox { Location = new Point(236, 8), Width = 220 };
            ModernTheme.SetPlaceholder(st_txtActivityFilter, "Character, account, IP or text");
            var filterTimer = new Timer { Interval = 250 };
            filterTimer.Tick += (s, e) => { filterTimer.Stop(); LoadActivityRows(); };
            st_txtActivityFilter.TextChanged += (s, e) => { filterTimer.Stop(); filterTimer.Start(); };
            toolbar.Controls.Add(st_txtActivityFilter);
            var btnRefresh = ToolButton("Refresh", new Point(468, 6), 90);
            btnRefresh.Click += (s, e) => RefreshActivityPage();
            toolbar.Controls.Add(btnRefresh);
            var btnOpen = ToolButton("Open Folder", new Point(566, 6), 110);
            btnOpen.Click += (s, e) => OpenFolder(ActivityLog.ActivityDirectory);
            toolbar.Controls.Add(btnOpen);
            st_lblActivitySummary = new Label
            {
                Location = new Point(690, 12),
                AutoSize = true,
                ForeColor = ModernTheme.Slate600,
                Font = ModernTheme.FontDefault,
                UseMnemonic = false
            };
            toolbar.Controls.Add(st_lblActivitySummary);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Size = new Size(1250, 600),
                SplitterDistance = 610,
                SplitterWidth = 10
            };

            var cardSessions = new GroupBox { Text = "Logins & Logouts", Dock = DockStyle.Fill, Padding = new Padding(10, 26, 10, 10) };
            st_dgvSessions = ToolGrid();
            foreach (string c in ActivityLog.SessionColumns) st_dgvSessions.Columns.Add("s" + c, c);
            SetFixedColumnWidths(st_dgvSessions, 62, 64, 56, 96, 80, 92);
            cardSessions.Controls.Add(st_dgvSessions);

            var cardChat = new GroupBox { Text = "Chat", Dock = DockStyle.Fill, Padding = new Padding(10, 26, 10, 10) };
            st_dgvChat = ToolGrid();
            foreach (string c in ActivityLog.ChatColumns) st_dgvChat.Columns.Add("c" + c, c);
            SetFixedColumnWidths(st_dgvChat, 70, 70, 62, 110);
            st_dgvChat.Columns["cCommand"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            st_dgvChat.Columns["cCommand"].Width = 80;
            st_dgvChat.Columns["cCommand"].DisplayIndex = 4;
            cardChat.Controls.Add(st_dgvChat);

            split.Panel1.Controls.Add(cardSessions);
            split.Panel2.Controls.Add(cardChat);

            page.Controls.Add(split);
            page.Controls.Add(toolbar);
            return page;
        }

        private void RefreshActivityPage()
        {
            var days = ActivityLog.AvailableDays();
            DateTime? keep = st_cmbActivityDay.SelectedItem is string sel && DateTime.TryParse(sel, out DateTime d) ? d : (DateTime?)null;
            st_cmbActivityDay.BeginUpdate();
            st_cmbActivityDay.Items.Clear();
            foreach (DateTime day in days) st_cmbActivityDay.Items.Add(day.ToString("yyyy-MM-dd"));
            st_cmbActivityDay.EndUpdate();
            int idx = keep.HasValue ? days.IndexOf(keep.Value) : 0;
            st_cmbActivityDay.SelectedIndex = idx >= 0 ? idx : 0; // triggers LoadActivityRows
        }

        private void LoadActivityRows()
        {
            try
            {
                if (!(st_cmbActivityDay.SelectedItem is string sel) || !DateTime.TryParse(sel, out DateTime day)) return;
                string filter = st_txtActivityFilter.Text.Trim();
                bool Match(string[] cells) => filter.Length == 0 ||
                    cells.Any(c => c != null && c.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

                var sessions = ActivityLog.Read("sessions", day).Where(Match).ToList();
                var chat = ActivityLog.Read("chat", day).Where(Match).ToList();

                FillGrid(st_dgvSessions, sessions, ActivityLog.SessionColumns.Length);
                FillGrid(st_dgvChat, chat, ActivityLog.ChatColumns.Length);

                int logins = sessions.Count(r => r.Length > 1 && r[1] == "LOGIN");
                int players = sessions.Where(r => r.Length > 3).Select(r => r[3]).Distinct().Count();
                st_lblActivitySummary.Text = $"{logins} login(s) · {players} player(s) · {chat.Count} chat line(s)" +
                    (day == DateTime.Today ? $" · {ActivityLog.OpenSessionCount} online now" : "");
            }
            catch (Exception ex)
            {
                st_lblActivitySummary.Text = "Could not read activity: " + ex.Message;
            }
        }

        private static void FillGrid(DataGridView grid, List<string[]> rows, int columns)
        {
            // The day is chosen above, so the time column only needs HH:mm:ss.
            string TimeOnly(string v) => v != null && v.Length == 19 && v[10] == ' ' ? v.Substring(11) : v;
            grid.SuspendLayout();
            grid.Rows.Clear();
            foreach (var r in rows.Take(5000))
            {
                var cells = new object[columns];
                for (int i = 0; i < columns; i++) cells[i] = i < r.Length ? (i == 0 ? TimeOnly(r[i]) : r[i]) : "";
                grid.Rows.Add(cells);
            }
            grid.ResumeLayout();
        }

        #endregion

        #region Status bar

        /// <summary>Thin bar at the bottom of the window with server health at a glance.</summary>
        private void BuildServerStatusBar()
        {
            st_statusBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                BackColor = ModernTheme.Slate900,
                Padding = new Padding(12, 0, 12, 0)
            };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            Label item()
            {
                return new Label
                {
                    AutoSize = true,
                    Font = ModernTheme.FontSmall,
                    ForeColor = ModernTheme.Slate300,
                    Margin = new Padding(0, 5, 22, 0),
                    UseMnemonic = false
                };
            }

            st_lblStatusPeak = item();
            st_lblStatusSave = item();
            st_lblStatusBackup = item();
            st_lblStatusAnnounce = item();
            st_lblStatusErrors = item();
            st_lblStatusErrors.Cursor = Cursors.Hand;
            st_lblStatusErrors.Click += (s, e) => JumpToFirstError();

            var tip = new ToolTip();
            tip.SetToolTip(st_lblStatusErrors, "Click to open the console at the first error (right-click to reset the counter)");
            st_lblStatusErrors.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right) { ServerMetrics.ResetErrors(); UpdateServerStatusBar(); }
            };
            st_lblStatusBackup.Cursor = Cursors.Hand;
            st_lblStatusBackup.Click += (s, e) => JumpToNavItem("Backups");
            st_lblStatusAnnounce.Cursor = Cursors.Hand;
            st_lblStatusAnnounce.Click += (s, e) => JumpToNavItem("Announcements");

            flow.Controls.AddRange(new Control[] { st_lblStatusPeak, st_lblStatusSave, st_lblStatusBackup, st_lblStatusAnnounce, st_lblStatusErrors });
            st_statusBar.Controls.Add(flow);
            ModernTheme.Exclude(st_statusBar);
        }

        private void UpdateServerStatusBar()
        {
            if (st_statusBar == null) return;
            try
            {
                string peakAt = ServerMetrics.PeakPlayersAt.HasValue ? $" at {ServerMetrics.PeakPlayersAt:HH:mm}" : "";
                st_lblStatusPeak.Text = $"Peak today: {ServerMetrics.PeakPlayersToday}{peakAt}";

                st_lblStatusSave.Text = ServerMetrics.LastAutoSave.HasValue
                    ? $"Auto-save: {ServerMetrics.LastAutoSavePlayers} player(s) in {ServerMetrics.LastAutoSaveMs:N0} ms, {Ago(ServerMetrics.LastAutoSave)}"
                    : "Auto-save: waiting for players";
                st_lblStatusSave.ForeColor = ServerMetrics.LastAutoSaveMs > 1000 ? Color.FromArgb(253, 224, 71) : ModernTheme.Slate300;

                if (ServerMetrics.LastBackupError != null)
                {
                    st_lblStatusBackup.Text = "Backup: FAILED";
                    st_lblStatusBackup.ForeColor = Color.FromArgb(252, 165, 165);
                }
                else
                {
                    st_lblStatusBackup.Text = $"Backup: {Ago(ServerMetrics.LastBackup)}";
                    st_lblStatusBackup.ForeColor = ModernTheme.Slate300;
                }

                DateTime? next = AnnouncementScheduler.NextDueAt;
                st_lblStatusAnnounce.Text = next.HasValue ? $"Next announcement: {next:HH:mm}" : "Announcements: off";

                int errors = ServerMetrics.ErrorCount;
                st_lblStatusErrors.Text = errors == 0 ? "No errors" : $"● {errors} error(s) logged";
                st_lblStatusErrors.ForeColor = errors == 0 ? Color.FromArgb(110, 231, 183) : Color.FromArgb(252, 165, 165);

                if (this.tabControl3?.SelectedTab == tabAnnouncements) UpdateAnnouncementStatus();
            }
            catch { }
        }

        private void JumpToNavItem(string label)
        {
            if (modernCategories == null) return;
            foreach (var cat in modernCategories)
            {
                var item = cat.Items.FirstOrDefault(i => i.Label == label);
                if (item == null) continue;
                SelectCategory(cat);
                SwitchToItem(item);
                return;
            }
        }

        private void JumpToFirstError()
        {
            JumpToNavItem("Dashboard & Logs");
            string first = ServerMetrics.FirstErrorText;
            if (string.IsNullOrEmpty(first) || this.MainOutput == null) return;

            string probe = first.Length > 60 ? first.Substring(0, 60) : first;
            int idx = this.MainOutput.Text.IndexOf(probe, StringComparison.Ordinal);
            if (idx < 0)
            {
                MessageBox.Show("First error (no longer in the console):\n\n" + first, "Errors", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DebugSystem.AutoScroll = false;
            this.MainOutput.Select(idx, probe.Length);
            this.MainOutput.ScrollToCaret();
            this.MainOutput.Focus();
        }

        #endregion
    }
}
