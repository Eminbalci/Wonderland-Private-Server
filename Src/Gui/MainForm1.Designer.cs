namespace Wonderland_Private_Server
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tabPage6 = new System.Windows.Forms.TabPage();
            this.tabControl3 = new System.Windows.Forms.TabControl();
            this.tabPage7 = new System.Windows.Forms.TabPage();
            this.MainOutput = new System.Windows.Forms.RichTextBox();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.groupBox_Npc = new System.Windows.Forms.GroupBox();
            this.button_NpcLeave = new System.Windows.Forms.Button();
            this.radioButton_Ride = new System.Windows.Forms.RadioButton();
            this.radioButton_Battle = new System.Windows.Forms.RadioButton();
            this.textBox_FindNPC = new System.Windows.Forms.TextBox();
            this.listBox_NPC = new System.Windows.Forms.ListBox();
            this.groupBox_Items = new System.Windows.Forms.GroupBox();
            this.textBox_FindItems = new System.Windows.Forms.TextBox();
            this.listBox_Items = new System.Windows.Forms.ListBox();
            this.groupBox_Vehicles = new System.Windows.Forms.GroupBox();
            this.button_UnrideVehicle = new System.Windows.Forms.Button();
            this.textBox_FindVehicle = new System.Windows.Forms.TextBox();
            this.listBox_Vehicles = new System.Windows.Forms.ListBox();
            this.groupBox_Maps = new System.Windows.Forms.GroupBox();
            this.textBox_FindMap = new System.Windows.Forms.TextBox();
            this.listBox_Maps = new System.Windows.Forms.ListBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageUsers = new System.Windows.Forms.TabPage();
            this.dataGridViewUsers = new System.Windows.Forms.DataGridView();
            this.btnRefreshUsers = new System.Windows.Forms.Button();
            this.btnDeleteUser = new System.Windows.Forms.Button();
            this.btnChangePassword = new System.Windows.Forms.Button();
            this.tabPagePortals = new System.Windows.Forms.TabPage();
            this.dgvPortals = new System.Windows.Forms.DataGridView();
            this.dgvDestinations = new System.Windows.Forms.DataGridView();
            this.btnRefreshPortals = new System.Windows.Forms.Button();
            this.btnAddPortal = new System.Windows.Forms.Button();
            this.btnDeletePortal = new System.Windows.Forms.Button();
            this.btnAddDestination = new System.Windows.Forms.Button();
            this.btnDeleteDestination = new System.Windows.Forms.Button();
            this.btnEditPortal = new System.Windows.Forms.Button();
            this.btnEditDestination = new System.Windows.Forms.Button();
            this.lblPortals = new System.Windows.Forms.Label();
            this.lblDestinations = new System.Windows.Forms.Label();
            this.tabPageCharacters = new System.Windows.Forms.TabPage();
            this.dgvCharacters = new System.Windows.Forms.DataGridView();
            this.btnRefreshCharacters = new System.Windows.Forms.Button();
            this.btnDeleteCharacter = new System.Windows.Forms.Button();
            this.tabPageSettings = new System.Windows.Forms.TabPage();
            this.dgvSettings = new System.Windows.Forms.DataGridView();
            this.btnRefreshSettings = new System.Windows.Forms.Button();
            this.btnSaveSettings = new System.Windows.Forms.Button();
            this.tabPageFriends = new System.Windows.Forms.TabPage();
            this.dgvFriends = new System.Windows.Forms.DataGridView();
            this.btnRefreshFriends = new System.Windows.Forms.Button();
            this.btnDeleteFriendship = new System.Windows.Forms.Button();
            this.tabPageInventory = new System.Windows.Forms.TabPage();
            this.dgvInventory = new System.Windows.Forms.DataGridView();
            this.cmbCharacterFilter = new System.Windows.Forms.ComboBox();
            this.lblCharacterFilter = new System.Windows.Forms.Label();
            this.btnRefreshInventory = new System.Windows.Forms.Button();
            this.btnDeleteItem = new System.Windows.Forms.Button();
            this.tabPageStats = new System.Windows.Forms.TabPage();
            this.dgvStats = new System.Windows.Forms.DataGridView();
            this.cmbCharacterFilterStats = new System.Windows.Forms.ComboBox();
            this.lblCharacterFilterStats = new System.Windows.Forms.Label();
            this.btnRefreshStats = new System.Windows.Forms.Button();
            this.btnEditStat = new System.Windows.Forms.Button();
            this.tabPage6.SuspendLayout();
            this.tabControl3.SuspendLayout();
            this.tabPage7.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.label_SelectedPlayer = new System.Windows.Forms.Label();
            this.comboBox_OnlinePlayers = new System.Windows.Forms.ComboBox();
            this.tabPageUsers.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewUsers)).BeginInit();
            this.tabPagePortals.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPortals)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDestinations)).BeginInit();
            this.tabPageCharacters.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCharacters)).BeginInit();
            this.groupBox_Npc.SuspendLayout();
            this.groupBox_Items.SuspendLayout();
            this.groupBox_Vehicles.SuspendLayout();
            this.groupBox_Maps.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabPage6
            // 
            this.tabPage6.Controls.Add(this.tabControl3);
            this.tabPage6.Location = new System.Drawing.Point(4, 22);
            this.tabPage6.Name = "tabPage6";
            this.tabPage6.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage6.Size = new System.Drawing.Size(788, 487);
            this.tabPage6.TabIndex = 2;
            this.tabPage6.Text = "Main";
            this.tabPage6.UseVisualStyleBackColor = true;
            // 
            // tabControl3
            // 
            this.tabControl3.Controls.Add(this.tabPage7);
            this.tabControl3.Controls.Add(this.tabPage1);
            this.tabControl3.Controls.Add(this.tabPageUsers);
            this.tabControl3.Controls.Add(this.tabPagePortals);
            this.tabControl3.Controls.Add(this.tabPageCharacters);
            this.tabControl3.Controls.Add(this.tabPageSettings);
            this.tabControl3.Controls.Add(this.tabPageFriends);
            this.tabControl3.Controls.Add(this.tabPageInventory);
            this.tabControl3.Controls.Add(this.tabPageStats);
            this.tabControl3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl3.Location = new System.Drawing.Point(3, 3);
            this.tabControl3.Name = "tabControl3";
            this.tabControl3.SelectedIndex = 0;
            this.tabControl3.Size = new System.Drawing.Size(782, 481);
            this.tabControl3.TabIndex = 0;
            // 
            // tabPage7
            // 
            this.tabPage7.Controls.Add(this.MainOutput);
            this.tabPage7.Location = new System.Drawing.Point(4, 22);
            this.tabPage7.Name = "tabPage7";
            this.tabPage7.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage7.Size = new System.Drawing.Size(774, 455);
            this.tabPage7.TabIndex = 0;
            this.tabPage7.Text = "Status";
            this.tabPage7.UseVisualStyleBackColor = true;
            // 
            // MainOutput
            // 
            this.MainOutput.BackColor = System.Drawing.SystemColors.InactiveCaptionText;
            this.MainOutput.ForeColor = System.Drawing.Color.White;
            this.MainOutput.Location = new System.Drawing.Point(6, 6);
            this.MainOutput.Name = "MainOutput";
            this.MainOutput.Size = new System.Drawing.Size(762, 443);
            this.MainOutput.TabIndex = 8;
            this.MainOutput.Text = "";
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.label_SelectedPlayer);
            this.tabPage1.Controls.Add(this.comboBox_OnlinePlayers);
            this.tabPage1.Controls.Add(this.groupBox_Npc);
            this.tabPage1.Controls.Add(this.groupBox_Items);
            this.tabPage1.Controls.Add(this.groupBox_Vehicles);
            this.tabPage1.Controls.Add(this.groupBox_Maps);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(774, 455);
            this.tabPage1.TabIndex = 1;
            this.tabPage1.Text = "Cheat";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // label_SelectedPlayer
            // 
            this.label_SelectedPlayer.AutoSize = true;
            this.label_SelectedPlayer.Location = new System.Drawing.Point(260, 15); // Moved to top right area
            this.label_SelectedPlayer.Name = "label_SelectedPlayer";
            this.label_SelectedPlayer.Size = new System.Drawing.Size(80, 13);
            this.label_SelectedPlayer.TabIndex = 10;
            this.label_SelectedPlayer.Text = "Target Player:";
            // 
            // comboBox_OnlinePlayers
            // 
            this.comboBox_OnlinePlayers.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_OnlinePlayers.FormattingEnabled = true;
            this.comboBox_OnlinePlayers.Location = new System.Drawing.Point(340, 12); // Moved to top right area
            this.comboBox_OnlinePlayers.Name = "comboBox_OnlinePlayers";
            this.comboBox_OnlinePlayers.Size = new System.Drawing.Size(150, 21);
            this.comboBox_OnlinePlayers.TabIndex = 11;
            // 
            // tabPageUsers
            // 
            this.tabPageUsers.Controls.Add(this.dataGridViewUsers);
            this.tabPageUsers.Controls.Add(this.btnRefreshUsers);
            this.tabPageUsers.Controls.Add(this.btnDeleteUser);
            this.tabPageUsers.Controls.Add(this.btnChangePassword);
            this.tabPageUsers.Location = new System.Drawing.Point(4, 22);
            this.tabPageUsers.Name = "tabPageUsers";
            this.tabPageUsers.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageUsers.Size = new System.Drawing.Size(774, 455);
            this.tabPageUsers.TabIndex = 2;
            this.tabPageUsers.Text = "Users";
            this.tabPageUsers.UseVisualStyleBackColor = true;
            // 
            // dataGridViewUsers
            // 
            this.dataGridViewUsers.AllowUserToAddRows = false;
            this.dataGridViewUsers.AllowUserToDeleteRows = false;
            this.dataGridViewUsers.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewUsers.Location = new System.Drawing.Point(6, 6);
            this.dataGridViewUsers.Name = "dataGridViewUsers";
            this.dataGridViewUsers.ReadOnly = true;
            this.dataGridViewUsers.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewUsers.Size = new System.Drawing.Size(650, 400);
            this.dataGridViewUsers.TabIndex = 0;
            // 
            // btnRefreshUsers
            // 
            this.btnRefreshUsers.Location = new System.Drawing.Point(662, 6);
            this.btnRefreshUsers.Name = "btnRefreshUsers";
            this.btnRefreshUsers.Size = new System.Drawing.Size(100, 30);
            this.btnRefreshUsers.TabIndex = 1;
            this.btnRefreshUsers.Text = "Refresh";
            this.btnRefreshUsers.UseVisualStyleBackColor = true;
            this.btnRefreshUsers.Click += new System.EventHandler(this.btnRefreshUsers_Click);
            // 
            // btnDeleteUser
            // 
            this.btnDeleteUser.Location = new System.Drawing.Point(662, 42);
            this.btnDeleteUser.Name = "btnDeleteUser";
            this.btnDeleteUser.Size = new System.Drawing.Size(100, 30);
            this.btnDeleteUser.TabIndex = 2;
            this.btnDeleteUser.Text = "Delete User";
            this.btnDeleteUser.UseVisualStyleBackColor = true;
            this.btnDeleteUser.Click += new System.EventHandler(this.btnDeleteUser_Click);
            // 
            // btnChangePassword
            // 
            this.btnChangePassword.Location = new System.Drawing.Point(662, 78);
            this.btnChangePassword.Name = "btnChangePassword";
            this.btnChangePassword.Size = new System.Drawing.Size(100, 30);
            this.btnChangePassword.TabIndex = 3;
            this.btnChangePassword.Text = "Change Pass";
            this.btnChangePassword.UseVisualStyleBackColor = true;
            this.btnChangePassword.Click += new System.EventHandler(this.btnChangePassword_Click);
            // 
            // tabPagePortals
            // 
            this.tabPagePortals.Controls.Add(this.lblPortals);
            this.tabPagePortals.Controls.Add(this.lblDestinations);
            this.tabPagePortals.Controls.Add(this.dgvPortals);
            this.tabPagePortals.Controls.Add(this.dgvDestinations);
            this.tabPagePortals.Controls.Add(this.btnRefreshPortals);
            this.tabPagePortals.Controls.Add(this.btnAddPortal);
            this.tabPagePortals.Controls.Add(this.btnDeletePortal);
            this.tabPagePortals.Controls.Add(this.btnAddDestination);
            this.tabPagePortals.Controls.Add(this.btnDeleteDestination);
            this.tabPagePortals.Controls.Add(this.btnEditPortal);
            this.tabPagePortals.Controls.Add(this.btnEditDestination);
            this.tabPagePortals.Location = new System.Drawing.Point(4, 22);
            this.tabPagePortals.Name = "tabPagePortals";
            this.tabPagePortals.Size = new System.Drawing.Size(774, 455);
            this.tabPagePortals.TabIndex = 3;
            this.tabPagePortals.Text = "Portals";
            this.tabPagePortals.UseVisualStyleBackColor = true;
            // 
            // lblPortals
            // 
            this.lblPortals.AutoSize = true;
            this.lblPortals.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.lblPortals.Location = new System.Drawing.Point(6, 8);
            this.lblPortals.Name = "lblPortals";
            this.lblPortals.Size = new System.Drawing.Size(52, 15);
            this.lblPortals.Text = "Portals";
            // 
            // lblDestinations
            // 
            this.lblDestinations.AutoSize = true;
            this.lblDestinations.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.lblDestinations.Location = new System.Drawing.Point(6, 230);
            this.lblDestinations.Name = "lblDestinations";
            this.lblDestinations.Size = new System.Drawing.Size(85, 15);
            this.lblDestinations.Text = "Destinations";
            // 
            // dgvPortals
            // 
            this.dgvPortals.AllowUserToAddRows = false;
            this.dgvPortals.AllowUserToDeleteRows = false;
            this.dgvPortals.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPortals.Location = new System.Drawing.Point(6, 26);
            this.dgvPortals.Name = "dgvPortals";
            this.dgvPortals.ReadOnly = true;
            this.dgvPortals.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvPortals.Size = new System.Drawing.Size(650, 195);
            this.dgvPortals.TabIndex = 0;
            // 
            // dgvDestinations
            // 
            this.dgvDestinations.AllowUserToAddRows = false;
            this.dgvDestinations.AllowUserToDeleteRows = false;
            this.dgvDestinations.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvDestinations.Location = new System.Drawing.Point(6, 248);
            this.dgvDestinations.Name = "dgvDestinations";
            this.dgvDestinations.ReadOnly = true;
            this.dgvDestinations.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvDestinations.Size = new System.Drawing.Size(650, 195);
            this.dgvDestinations.TabIndex = 1;
            // 
            // btnRefreshPortals
            // 
            this.btnRefreshPortals.Location = new System.Drawing.Point(662, 26);
            this.btnRefreshPortals.Name = "btnRefreshPortals";
            this.btnRefreshPortals.Size = new System.Drawing.Size(100, 30);
            this.btnRefreshPortals.TabIndex = 2;
            this.btnRefreshPortals.Text = "Refresh All";
            this.btnRefreshPortals.UseVisualStyleBackColor = true;
            this.btnRefreshPortals.Click += new System.EventHandler(this.btnRefreshPortals_Click);
            // 
            // btnAddPortal
            // 
            this.btnAddPortal.Location = new System.Drawing.Point(662, 62);
            this.btnAddPortal.Name = "btnAddPortal";
            this.btnAddPortal.Size = new System.Drawing.Size(100, 30);
            this.btnAddPortal.TabIndex = 3;
            this.btnAddPortal.Text = "Add Portal";
            this.btnAddPortal.UseVisualStyleBackColor = true;
            this.btnAddPortal.Click += new System.EventHandler(this.btnAddPortal_Click);
            // 
            // btnDeletePortal
            // 
            this.btnDeletePortal.Location = new System.Drawing.Point(662, 98);
            this.btnDeletePortal.Name = "btnDeletePortal";
            this.btnDeletePortal.Size = new System.Drawing.Size(100, 30);
            this.btnDeletePortal.TabIndex = 4;
            this.btnDeletePortal.Text = "Delete Portal";
            this.btnDeletePortal.UseVisualStyleBackColor = true;
            this.btnDeletePortal.Click += new System.EventHandler(this.btnDeletePortal_Click);
            // 
            // btnAddDestination
            // 
            this.btnAddDestination.Location = new System.Drawing.Point(662, 248);
            this.btnAddDestination.Name = "btnAddDestination";
            this.btnAddDestination.Size = new System.Drawing.Size(100, 30);
            this.btnAddDestination.TabIndex = 5;
            this.btnAddDestination.Text = "Add Dest";
            this.btnAddDestination.UseVisualStyleBackColor = true;
            this.btnAddDestination.Click += new System.EventHandler(this.btnAddDestination_Click);
            // 
            // btnDeleteDestination
            // 
            this.btnDeleteDestination.Location = new System.Drawing.Point(662, 284);
            this.btnDeleteDestination.Name = "btnDeleteDestination";
            this.btnDeleteDestination.Size = new System.Drawing.Size(100, 30);
            this.btnDeleteDestination.TabIndex = 6;
            this.btnDeleteDestination.Text = "Delete Dest";
            this.btnDeleteDestination.UseVisualStyleBackColor = true;
            this.btnDeleteDestination.Click += new System.EventHandler(this.btnDeleteDestination_Click);
            // 
            // btnEditPortal
            // 
            this.btnEditPortal.Location = new System.Drawing.Point(662, 134);
            this.btnEditPortal.Name = "btnEditPortal";
            this.btnEditPortal.Size = new System.Drawing.Size(100, 30);
            this.btnEditPortal.TabIndex = 7;
            this.btnEditPortal.Text = "Edit Portal";
            this.btnEditPortal.UseVisualStyleBackColor = true;
            this.btnEditPortal.Click += new System.EventHandler(this.btnEditPortal_Click);
            // 
            // btnEditDestination
            // 
            this.btnEditDestination.Location = new System.Drawing.Point(662, 320);
            this.btnEditDestination.Name = "btnEditDestination";
            this.btnEditDestination.Size = new System.Drawing.Size(100, 30);
            this.btnEditDestination.TabIndex = 8;
            this.btnEditDestination.Text = "Edit Dest";
            this.btnEditDestination.UseVisualStyleBackColor = true;
            this.btnEditDestination.Click += new System.EventHandler(this.btnEditDestination_Click);
            // 
            // tabPageCharacters
            // 
            this.tabPageCharacters.Controls.Add(this.btnDeleteCharacter);
            this.tabPageCharacters.Controls.Add(this.btnRefreshCharacters);
            this.tabPageCharacters.Controls.Add(this.dgvCharacters);
            this.tabPageCharacters.Location = new System.Drawing.Point(4, 22);
            this.tabPageCharacters.Name = "tabPageCharacters";
            this.tabPageCharacters.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageCharacters.Size = new System.Drawing.Size(774, 455);
            this.tabPageCharacters.TabIndex = 4;
            this.tabPageCharacters.Text = "Characters";
            this.tabPageCharacters.UseVisualStyleBackColor = true;
            // 
            // dgvCharacters
            // 
            this.dgvCharacters.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCharacters.Location = new System.Drawing.Point(6, 6);
            this.dgvCharacters.Name = "dgvCharacters";
            this.dgvCharacters.Size = new System.Drawing.Size(650, 443);
            this.dgvCharacters.TabIndex = 0;
            // 
            // btnRefreshCharacters
            // 
            this.btnRefreshCharacters.Location = new System.Drawing.Point(662, 6);
            this.btnRefreshCharacters.Name = "btnRefreshCharacters";
            this.btnRefreshCharacters.Size = new System.Drawing.Size(100, 30);
            this.btnRefreshCharacters.TabIndex = 1;
            this.btnRefreshCharacters.Text = "Refresh";
            this.btnRefreshCharacters.UseVisualStyleBackColor = true;
            this.btnRefreshCharacters.Click += new System.EventHandler(this.btnRefreshCharacters_Click);
            // 
            // btnDeleteCharacter
            // 
            this.btnDeleteCharacter.Location = new System.Drawing.Point(662, 42);
            this.btnDeleteCharacter.Name = "btnDeleteCharacter";
            this.btnDeleteCharacter.Size = new System.Drawing.Size(100, 30);
            this.btnDeleteCharacter.TabIndex = 2;
            this.btnDeleteCharacter.Text = "Delete Char";
            this.btnDeleteCharacter.UseVisualStyleBackColor = true;
            this.btnDeleteCharacter.Click += new System.EventHandler(this.btnDeleteCharacter_Click);
            // 
            // tabPageSettings
            // 
            this.tabPageSettings.Controls.Add(this.dgvSettings);
            this.tabPageSettings.Controls.Add(this.btnRefreshSettings);
            this.tabPageSettings.Controls.Add(this.btnSaveSettings);
            this.tabPageSettings.Location = new System.Drawing.Point(4, 22);
            this.tabPageSettings.Name = "tabPageSettings";
            this.tabPageSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageSettings.Size = new System.Drawing.Size(774, 455);
            this.tabPageSettings.TabIndex = 5;
            this.tabPageSettings.Text = "Player Settings";
            this.tabPageSettings.UseVisualStyleBackColor = true;
            // 
            // dgvSettings
            // 
            this.dgvSettings.AllowUserToAddRows = false;
            this.dgvSettings.AllowUserToDeleteRows = false;
            this.dgvSettings.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSettings.Location = new System.Drawing.Point(6, 6);
            this.dgvSettings.Name = "dgvSettings";
            this.dgvSettings.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvSettings.Size = new System.Drawing.Size(650, 443);
            this.dgvSettings.TabIndex = 0;
            // 
            // btnRefreshSettings
            // 
            this.btnRefreshSettings.Location = new System.Drawing.Point(662, 6);
            this.btnRefreshSettings.Name = "btnRefreshSettings";
            this.btnRefreshSettings.Size = new System.Drawing.Size(100, 30);
            this.btnRefreshSettings.TabIndex = 1;
            this.btnRefreshSettings.Text = "Refresh";
            this.btnRefreshSettings.UseVisualStyleBackColor = true;
            this.btnRefreshSettings.Click += new System.EventHandler(this.btnRefreshSettings_Click);
            // 
            // btnSaveSettings
            // 
            this.btnSaveSettings.Location = new System.Drawing.Point(662, 42);
            this.btnSaveSettings.Name = "btnSaveSettings";
            this.btnSaveSettings.Size = new System.Drawing.Size(100, 30);
            this.btnSaveSettings.TabIndex = 2;
            this.btnSaveSettings.Text = "Save";
            this.btnSaveSettings.UseVisualStyleBackColor = true;
            this.btnSaveSettings.Click += new System.EventHandler(this.btnSaveSettings_Click);
            //
            // tabPageFriends
            //
            this.tabPageFriends.Controls.Add(this.dgvFriends);
            this.tabPageFriends.Controls.Add(this.btnRefreshFriends);
            this.tabPageFriends.Controls.Add(this.btnDeleteFriendship);
            this.tabPageFriends.Location = new System.Drawing.Point(4, 22);
            this.tabPageFriends.Name = "tabPageFriends";
            this.tabPageFriends.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageFriends.Size = new System.Drawing.Size(774, 455);
            this.tabPageFriends.TabIndex = 6;
            this.tabPageFriends.Text = "Friends";
            this.tabPageFriends.UseVisualStyleBackColor = true;
            //
            // dgvFriends
            //
            this.dgvFriends.AllowUserToAddRows = false;
            this.dgvFriends.AllowUserToDeleteRows = false;
            this.dgvFriends.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvFriends.Location = new System.Drawing.Point(6, 6);
            this.dgvFriends.Name = "dgvFriends";
            this.dgvFriends.ReadOnly = true;
            this.dgvFriends.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvFriends.Size = new System.Drawing.Size(650, 400);
            this.dgvFriends.TabIndex = 0;
            //
            // btnRefreshFriends
            //
            this.btnRefreshFriends.Location = new System.Drawing.Point(662, 6);
            this.btnRefreshFriends.Name = "btnRefreshFriends";
            this.btnRefreshFriends.Size = new System.Drawing.Size(100, 30);
            this.btnRefreshFriends.TabIndex = 1;
            this.btnRefreshFriends.Text = "Refresh";
            this.btnRefreshFriends.UseVisualStyleBackColor = true;
            this.btnRefreshFriends.Click += new System.EventHandler(this.btnRefreshFriends_Click);
            //
            // btnDeleteFriendship
            //
            this.btnDeleteFriendship.Location = new System.Drawing.Point(662, 42);
            this.btnDeleteFriendship.Name = "btnDeleteFriendship";
            this.btnDeleteFriendship.Size = new System.Drawing.Size(100, 30);
            this.btnDeleteFriendship.TabIndex = 2;
            this.btnDeleteFriendship.Text = "Delete";
            this.btnDeleteFriendship.UseVisualStyleBackColor = true;
            this.btnDeleteFriendship.Click += new System.EventHandler(this.btnDeleteFriendship_Click);
            //
            // tabPageInventory
            //
            this.tabPageInventory.Controls.Add(this.lblCharacterFilter);
            this.tabPageInventory.Controls.Add(this.cmbCharacterFilter);
            this.tabPageInventory.Controls.Add(this.dgvInventory);
            this.tabPageInventory.Controls.Add(this.btnRefreshInventory);
            this.tabPageInventory.Controls.Add(this.btnDeleteItem);
            this.tabPageInventory.Location = new System.Drawing.Point(4, 22);
            this.tabPageInventory.Name = "tabPageInventory";
            this.tabPageInventory.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageInventory.Size = new System.Drawing.Size(774, 455);
            this.tabPageInventory.TabIndex = 7;
            this.tabPageInventory.Text = "Inventory";
            this.tabPageInventory.UseVisualStyleBackColor = true;
            //
            // lblCharacterFilter
            //
            this.lblCharacterFilter.AutoSize = true;
            this.lblCharacterFilter.Location = new System.Drawing.Point(6, 10);
            this.lblCharacterFilter.Name = "lblCharacterFilter";
            this.lblCharacterFilter.Size = new System.Drawing.Size(56, 13);
            this.lblCharacterFilter.Text = "Character:";
            //
            // cmbCharacterFilter
            //
            this.cmbCharacterFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCharacterFilter.FormattingEnabled = true;
            this.cmbCharacterFilter.Location = new System.Drawing.Point(70, 7);
            this.cmbCharacterFilter.Name = "cmbCharacterFilter";
            this.cmbCharacterFilter.Size = new System.Drawing.Size(200, 21);
            this.cmbCharacterFilter.TabIndex = 0;
            this.cmbCharacterFilter.SelectedIndexChanged += new System.EventHandler(this.cmbCharacterFilter_SelectedIndexChanged);
            //
            // dgvInventory
            //
            this.dgvInventory.AllowUserToAddRows = false;
            this.dgvInventory.AllowUserToDeleteRows = false;
            this.dgvInventory.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvInventory.Location = new System.Drawing.Point(6, 35);
            this.dgvInventory.Name = "dgvInventory";
            this.dgvInventory.ReadOnly = true;
            this.dgvInventory.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvInventory.Size = new System.Drawing.Size(650, 370);
            this.dgvInventory.TabIndex = 1;
            //
            // btnRefreshInventory
            //
            this.btnRefreshInventory.Location = new System.Drawing.Point(662, 35);
            this.btnRefreshInventory.Name = "btnRefreshInventory";
            this.btnRefreshInventory.Size = new System.Drawing.Size(100, 30);
            this.btnRefreshInventory.TabIndex = 2;
            this.btnRefreshInventory.Text = "Refresh";
            this.btnRefreshInventory.UseVisualStyleBackColor = true;
            this.btnRefreshInventory.Click += new System.EventHandler(this.btnRefreshInventory_Click);
            //
            // btnDeleteItem
            //
            this.btnDeleteItem.Location = new System.Drawing.Point(662, 71);
            this.btnDeleteItem.Name = "btnDeleteItem";
            this.btnDeleteItem.Size = new System.Drawing.Size(100, 30);
            this.btnDeleteItem.TabIndex = 3;
            this.btnDeleteItem.Text = "Delete Item";
            this.btnDeleteItem.UseVisualStyleBackColor = true;
            this.btnDeleteItem.Click += new System.EventHandler(this.btnDeleteItem_Click);
            //
            // tabPageStats
            //
            this.tabPageStats.Controls.Add(this.lblCharacterFilterStats);
            this.tabPageStats.Controls.Add(this.cmbCharacterFilterStats);
            this.tabPageStats.Controls.Add(this.dgvStats);
            this.tabPageStats.Controls.Add(this.btnRefreshStats);
            this.tabPageStats.Controls.Add(this.btnEditStat);
            this.tabPageStats.Location = new System.Drawing.Point(4, 22);
            this.tabPageStats.Name = "tabPageStats";
            this.tabPageStats.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageStats.Size = new System.Drawing.Size(774, 455);
            this.tabPageStats.TabIndex = 8;
            this.tabPageStats.Text = "Stats";
            this.tabPageStats.UseVisualStyleBackColor = true;
            //
            // lblCharacterFilterStats
            //
            this.lblCharacterFilterStats.AutoSize = true;
            this.lblCharacterFilterStats.Location = new System.Drawing.Point(6, 10);
            this.lblCharacterFilterStats.Name = "lblCharacterFilterStats";
            this.lblCharacterFilterStats.Size = new System.Drawing.Size(56, 13);
            this.lblCharacterFilterStats.Text = "Character:";
            //
            // cmbCharacterFilterStats
            //
            this.cmbCharacterFilterStats.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCharacterFilterStats.FormattingEnabled = true;
            this.cmbCharacterFilterStats.Location = new System.Drawing.Point(70, 7);
            this.cmbCharacterFilterStats.Name = "cmbCharacterFilterStats";
            this.cmbCharacterFilterStats.Size = new System.Drawing.Size(200, 21);
            this.cmbCharacterFilterStats.TabIndex = 0;
            this.cmbCharacterFilterStats.SelectedIndexChanged += new System.EventHandler(this.cmbCharacterFilterStats_SelectedIndexChanged);
            //
            // dgvStats
            //
            this.dgvStats.AllowUserToAddRows = false;
            this.dgvStats.AllowUserToDeleteRows = false;
            this.dgvStats.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvStats.Location = new System.Drawing.Point(6, 35);
            this.dgvStats.Name = "dgvStats";
            this.dgvStats.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvStats.Size = new System.Drawing.Size(650, 370);
            this.dgvStats.TabIndex = 1;
            //
            // btnRefreshStats
            //
            this.btnRefreshStats.Location = new System.Drawing.Point(662, 35);
            this.btnRefreshStats.Name = "btnRefreshStats";
            this.btnRefreshStats.Size = new System.Drawing.Size(100, 30);
            this.btnRefreshStats.TabIndex = 2;
            this.btnRefreshStats.Text = "Refresh";
            this.btnRefreshStats.UseVisualStyleBackColor = true;
            this.btnRefreshStats.Click += new System.EventHandler(this.btnRefreshStats_Click);
            //
            // btnEditStat
            //
            this.btnEditStat.Location = new System.Drawing.Point(662, 71);
            this.btnEditStat.Name = "btnEditStat";
            this.btnEditStat.Size = new System.Drawing.Size(100, 30);
            this.btnEditStat.TabIndex = 3;
            this.btnEditStat.Text = "Edit Stat";
            this.btnEditStat.UseVisualStyleBackColor = true;
            this.btnEditStat.Click += new System.EventHandler(this.btnEditStat_Click);
            //
            // groupBox_Npc
            // 
            this.groupBox_Npc.Controls.Add(this.button_NpcLeave);
            this.groupBox_Npc.Controls.Add(this.radioButton_Ride);
            this.groupBox_Npc.Controls.Add(this.radioButton_Battle);
            this.groupBox_Npc.Controls.Add(this.textBox_FindNPC);
            this.groupBox_Npc.Controls.Add(this.listBox_NPC);
            this.groupBox_Npc.Location = new System.Drawing.Point(584, 6);
            this.groupBox_Npc.Name = "groupBox_Npc";
            this.groupBox_Npc.Size = new System.Drawing.Size(184, 443);
            this.groupBox_Npc.TabIndex = 3;
            this.groupBox_Npc.TabStop = false;
            this.groupBox_Npc.Text = "NPC/Pet";
            // 
            // button_NpcLeave
            // 
            this.button_NpcLeave.Location = new System.Drawing.Point(131, 412);
            this.button_NpcLeave.Name = "button_NpcLeave";
            this.button_NpcLeave.Size = new System.Drawing.Size(47, 23);
            this.button_NpcLeave.TabIndex = 8;
            this.button_NpcLeave.Text = "Leave";
            this.button_NpcLeave.UseVisualStyleBackColor = true;
            this.button_NpcLeave.Click += new System.EventHandler(this.button_NpcLeave_Click);
            // 
            // radioButton_Ride
            // 
            this.radioButton_Ride.AutoSize = true;
            this.radioButton_Ride.Location = new System.Drawing.Point(65, 415);
            this.radioButton_Ride.Name = "radioButton_Ride";
            this.radioButton_Ride.Size = new System.Drawing.Size(47, 17);
            this.radioButton_Ride.TabIndex = 7;
            this.radioButton_Ride.Text = "Ride";
            this.radioButton_Ride.UseVisualStyleBackColor = true;
            // 
            // radioButton_Battle
            // 
            this.radioButton_Battle.AutoSize = true;
            this.radioButton_Battle.Checked = true;
            this.radioButton_Battle.Location = new System.Drawing.Point(7, 415);
            this.radioButton_Battle.Name = "radioButton_Battle";
            this.radioButton_Battle.Size = new System.Drawing.Size(52, 17);
            this.radioButton_Battle.TabIndex = 6;
            this.radioButton_Battle.TabStop = true;
            this.radioButton_Battle.Text = "Battle";
            this.radioButton_Battle.UseVisualStyleBackColor = true;
            this.radioButton_Battle.CheckedChanged += new System.EventHandler(this.radioButton_Battle_CheckedChanged);
            // 
            // textBox_FindNPC
            // 
            this.textBox_FindNPC.Location = new System.Drawing.Point(7, 20);
            this.textBox_FindNPC.Name = "textBox_FindNPC";
            this.textBox_FindNPC.Size = new System.Drawing.Size(171, 20);
            this.textBox_FindNPC.TabIndex = 3;
            // 
            // listBox_NPC
            // 
            this.listBox_NPC.FormattingEnabled = true;
            this.listBox_NPC.Location = new System.Drawing.Point(7, 41);
            this.listBox_NPC.Name = "listBox_NPC";
            this.listBox_NPC.Size = new System.Drawing.Size(171, 368);
            this.listBox_NPC.TabIndex = 2;
            // 
            // groupBox_Items
            // 
            this.groupBox_Items.Controls.Add(this.textBox_FindItems);
            this.groupBox_Items.Controls.Add(this.listBox_Items);
            this.groupBox_Items.Location = new System.Drawing.Point(368, 6);
            this.groupBox_Items.Name = "groupBox_Items";
            this.groupBox_Items.Size = new System.Drawing.Size(210, 443);
            this.groupBox_Items.TabIndex = 2;
            this.groupBox_Items.TabStop = false;
            this.groupBox_Items.Text = "Items";
            // 
            // textBox_FindItems
            // 
            this.textBox_FindItems.Location = new System.Drawing.Point(7, 20);
            this.textBox_FindItems.Name = "textBox_FindItems";
            this.textBox_FindItems.Size = new System.Drawing.Size(197, 20);
            this.textBox_FindItems.TabIndex = 3;
            // 
            // listBox_Items
            // 
            this.listBox_Items.FormattingEnabled = true;
            this.listBox_Items.Location = new System.Drawing.Point(7, 41);
            this.listBox_Items.Name = "listBox_Items";
            this.listBox_Items.Size = new System.Drawing.Size(197, 394);
            this.listBox_Items.TabIndex = 2;
            // 
            // groupBox_Vehicles
            // 
            this.groupBox_Vehicles.Controls.Add(this.button_UnrideVehicle);
            this.groupBox_Vehicles.Controls.Add(this.textBox_FindVehicle);
            this.groupBox_Vehicles.Controls.Add(this.listBox_Vehicles);
            this.groupBox_Vehicles.Location = new System.Drawing.Point(192, 6);
            this.groupBox_Vehicles.Name = "groupBox_Vehicles";
            this.groupBox_Vehicles.Size = new System.Drawing.Size(171, 443);
            this.groupBox_Vehicles.TabIndex = 1;
            this.groupBox_Vehicles.TabStop = false;
            this.groupBox_Vehicles.Text = "Vehicles";
            // 
            // button_UnrideVehicle
            // 
            this.button_UnrideVehicle.Location = new System.Drawing.Point(7, 414);
            this.button_UnrideVehicle.Name = "button_UnrideVehicle";
            this.button_UnrideVehicle.Size = new System.Drawing.Size(158, 23);
            this.button_UnrideVehicle.TabIndex = 9;
            this.button_UnrideVehicle.Text = "Remove Vehicle";
            this.button_UnrideVehicle.UseVisualStyleBackColor = true;
            this.button_UnrideVehicle.Click += new System.EventHandler(this.button_UnrideVehicle_Click);
            // 
            // textBox_FindVehicle
            // 
            this.textBox_FindVehicle.Location = new System.Drawing.Point(7, 20);
            this.textBox_FindVehicle.Name = "textBox_FindVehicle";
            this.textBox_FindVehicle.Size = new System.Drawing.Size(158, 20);
            this.textBox_FindVehicle.TabIndex = 3;
            // 
            // listBox_Vehicles
            // 
            this.listBox_Vehicles.FormattingEnabled = true;
            this.listBox_Vehicles.Location = new System.Drawing.Point(7, 41);
            this.listBox_Vehicles.Name = "listBox_Vehicles";
            this.listBox_Vehicles.Size = new System.Drawing.Size(158, 368);
            this.listBox_Vehicles.TabIndex = 2;
            // 
            // groupBox_Maps
            // 
            this.groupBox_Maps.Controls.Add(this.textBox_FindMap);
            this.groupBox_Maps.Controls.Add(this.listBox_Maps);
            this.groupBox_Maps.Location = new System.Drawing.Point(6, 6);
            this.groupBox_Maps.Name = "groupBox_Maps";
            this.groupBox_Maps.Size = new System.Drawing.Size(180, 443);
            this.groupBox_Maps.TabIndex = 0;
            this.groupBox_Maps.TabStop = false;
            this.groupBox_Maps.Text = "Maps";
            // 
            // textBox_FindMap
            // 
            this.textBox_FindMap.Location = new System.Drawing.Point(7, 20);
            this.textBox_FindMap.Name = "textBox_FindMap";
            this.textBox_FindMap.Size = new System.Drawing.Size(167, 20);
            this.textBox_FindMap.TabIndex = 1;
            // 
            // listBox_Maps
            // 
            this.listBox_Maps.FormattingEnabled = true;
            this.listBox_Maps.Location = new System.Drawing.Point(7, 41);
            this.listBox_Maps.Name = "listBox_Maps";
            this.listBox_Maps.Size = new System.Drawing.Size(167, 394);
            this.listBox_Maps.TabIndex = 0;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage6);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(796, 513);
            this.tabControl1.TabIndex = 2;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(796, 513);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "Form1";
            this.Text = "WLO Private Server CheatEngine";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tabPage6.ResumeLayout(false);
            this.tabControl3.ResumeLayout(false);
            this.tabPage7.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPageUsers.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewUsers)).EndInit();
            this.tabPagePortals.ResumeLayout(false);
            this.tabPagePortals.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPortals)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDestinations)).EndInit();
            this.tabPageCharacters.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvCharacters)).EndInit();
            this.groupBox_Npc.ResumeLayout(false);
            this.groupBox_Npc.PerformLayout();
            this.groupBox_Items.ResumeLayout(false);
            this.groupBox_Items.PerformLayout();
            this.groupBox_Vehicles.ResumeLayout(false);
            this.groupBox_Vehicles.PerformLayout();
            this.groupBox_Maps.ResumeLayout(false);
            this.groupBox_Maps.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.TabPage tabPage6;
        private System.Windows.Forms.TabControl tabControl3;
        private System.Windows.Forms.TabPage tabPage7;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.RichTextBox MainOutput;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.GroupBox groupBox_Maps;
        private System.Windows.Forms.GroupBox groupBox_Vehicles;
        private System.Windows.Forms.GroupBox groupBox_Npc;
        private System.Windows.Forms.GroupBox groupBox_Items;
        private System.Windows.Forms.ListBox listBox_Maps;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBox_OnlinePlayers; // User selection for cheat commands
        private System.Windows.Forms.Label label_SelectedPlayer; // Label for ComboBox
        private System.Windows.Forms.TextBox textBox_FindMap;
        private System.Windows.Forms.TextBox textBox_FindNPC;
        private System.Windows.Forms.ListBox listBox_NPC;
        private System.Windows.Forms.TextBox textBox_FindItems;
        private System.Windows.Forms.ListBox listBox_Items;
        private System.Windows.Forms.TextBox textBox_FindVehicle;
        private System.Windows.Forms.ListBox listBox_Vehicles; // Added back
        private System.Windows.Forms.RadioButton radioButton_Ride;
        private System.Windows.Forms.RadioButton radioButton_Battle;
        private System.Windows.Forms.Button button_NpcLeave;
        private System.Windows.Forms.Button button_UnrideVehicle;
        private System.Windows.Forms.TabPage tabPageUsers;
        private System.Windows.Forms.DataGridView dataGridViewUsers;
        private System.Windows.Forms.Button btnRefreshUsers;
        private System.Windows.Forms.Button btnDeleteUser;
        private System.Windows.Forms.Button btnChangePassword;
        private System.Windows.Forms.TabPage tabPagePortals;
        private System.Windows.Forms.DataGridView dgvPortals;
        private System.Windows.Forms.DataGridView dgvDestinations;
        private System.Windows.Forms.Button btnRefreshPortals;
        private System.Windows.Forms.Button btnAddPortal;
        private System.Windows.Forms.Button btnDeletePortal;
        private System.Windows.Forms.Button btnAddDestination;
        private System.Windows.Forms.Button btnDeleteDestination;
        private System.Windows.Forms.Label lblPortals;
        private System.Windows.Forms.Label lblDestinations;
        private System.Windows.Forms.TabPage tabPageCharacters;
        private System.Windows.Forms.DataGridView dgvCharacters;
        private System.Windows.Forms.Button btnRefreshCharacters;
        private System.Windows.Forms.Button btnDeleteCharacter;
        private System.Windows.Forms.Button btnEditPortal;
        private System.Windows.Forms.Button btnEditDestination;
        private System.Windows.Forms.TabPage tabPageSettings;
        private System.Windows.Forms.DataGridView dgvSettings;
        private System.Windows.Forms.Button btnRefreshSettings;
        private System.Windows.Forms.Button btnSaveSettings;
        private System.Windows.Forms.TabPage tabPageFriends;
        private System.Windows.Forms.DataGridView dgvFriends;
        private System.Windows.Forms.Button btnRefreshFriends;
        private System.Windows.Forms.Button btnDeleteFriendship;
        private System.Windows.Forms.TabPage tabPageInventory;
        private System.Windows.Forms.DataGridView dgvInventory;
        private System.Windows.Forms.ComboBox cmbCharacterFilter;
        private System.Windows.Forms.Label lblCharacterFilter;
        private System.Windows.Forms.Button btnRefreshInventory;
        private System.Windows.Forms.Button btnDeleteItem;
        private System.Windows.Forms.TabPage tabPageStats;
        private System.Windows.Forms.DataGridView dgvStats;
        private System.Windows.Forms.ComboBox cmbCharacterFilterStats;
        private System.Windows.Forms.Label lblCharacterFilterStats;
        private System.Windows.Forms.Button btnRefreshStats;
        private System.Windows.Forms.Button btnEditStat;
    }
}

