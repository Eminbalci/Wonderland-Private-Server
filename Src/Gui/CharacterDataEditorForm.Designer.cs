namespace Wonderland_Private_Server
{
    partial class CharacterDataEditorForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabQuests = new System.Windows.Forms.TabPage();
            this.dgvQuests = new System.Windows.Forms.DataGridView();
            this.pnlQuestControls = new System.Windows.Forms.Panel();
            this.btnCompleteQuest = new System.Windows.Forms.Button();
            this.btnAdvanceStep = new System.Windows.Forms.Button();
            this.btnDeleteQuest = new System.Windows.Forms.Button();
            this.btnAddQuest = new System.Windows.Forms.Button();
            this.numQuestId = new System.Windows.Forms.NumericUpDown();
            this.lblQuestId = new System.Windows.Forms.Label();
            this.cmbQuestState = new System.Windows.Forms.ComboBox();
            this.lblQuestState = new System.Windows.Forms.Label();
            this.btnRefreshQuests = new System.Windows.Forms.Button();
            this.btnSyncLiveQuests = new System.Windows.Forms.Button();
            
            this.tabPets = new System.Windows.Forms.TabPage();
            this.dgvPets = new System.Windows.Forms.DataGridView();
            this.pnlPetControls = new System.Windows.Forms.Panel();
            this.btnAddPetPreset = new System.Windows.Forms.Button();
            this.cmbPetPresets = new System.Windows.Forms.ComboBox();
            this.btnMaxAmityHeal = new System.Windows.Forms.Button();
            this.btnDeletePet = new System.Windows.Forms.Button();
            this.btnSavePets = new System.Windows.Forms.Button();
            this.btnRefreshPets = new System.Windows.Forms.Button();
            this.btnSyncLivePets = new System.Windows.Forms.Button();

            this.tabNpcVisibility = new System.Windows.Forms.TabPage();
            this.dgvNpcVisibility = new System.Windows.Forms.DataGridView();
            this.pnlNpcControls = new System.Windows.Forms.Panel();
            this.cmbMapSelector = new System.Windows.Forms.ComboBox();
            this.lblSelectMap = new System.Windows.Forms.Label();
            this.btnOpenBreakChest = new System.Windows.Forms.Button();
            this.btnRestoreCloseChest = new System.Windows.Forms.Button();
            this.btnHideDespawnNpc = new System.Windows.Forms.Button();
            this.btnShowUnhideNpc = new System.Windows.Forms.Button();
            this.btnBroadcastNpcLive = new System.Windows.Forms.Button();
            this.btnRefreshNpcs = new System.Windows.Forms.Button();

            this.tabInventory = new System.Windows.Forms.TabPage();
            this.dgvInventory = new System.Windows.Forms.DataGridView();
            this.pnlInvControls = new System.Windows.Forms.Panel();
            this.btnAddItem = new System.Windows.Forms.Button();
            this.numItemId = new System.Windows.Forms.NumericUpDown();
            this.numItemQty = new System.Windows.Forms.NumericUpDown();
            this.lblItemId = new System.Windows.Forms.Label();
            this.lblItemQty = new System.Windows.Forms.Label();
            this.btnDeleteItem = new System.Windows.Forms.Button();
            this.btnRepairItem = new System.Windows.Forms.Button();
            this.btnRefreshInventory = new System.Windows.Forms.Button();

            this.tabSkills = new System.Windows.Forms.TabPage();
            this.dgvSkills = new System.Windows.Forms.DataGridView();
            this.pnlSkillControls = new System.Windows.Forms.Panel();
            this.btnAddSkill = new System.Windows.Forms.Button();
            this.numSkillId = new System.Windows.Forms.NumericUpDown();
            this.numSkillGrade = new System.Windows.Forms.NumericUpDown();
            this.lblSkillId = new System.Windows.Forms.Label();
            this.lblSkillGrade = new System.Windows.Forms.Label();
            this.btnDeleteSkill = new System.Windows.Forms.Button();
            this.btnRefreshSkills = new System.Windows.Forms.Button();

            this.tabLearnedSkills = new System.Windows.Forms.TabPage();
            this.dgvLearnedSkills = new System.Windows.Forms.DataGridView();
            this.pnlLearnedSkillControls = new System.Windows.Forms.Panel();
            this.lblLearnedSkillId = new System.Windows.Forms.Label();
            this.numLearnedSkillId = new System.Windows.Forms.NumericUpDown();
            this.lblLearnedSkillGrade = new System.Windows.Forms.Label();
            this.numLearnedSkillGrade = new System.Windows.Forms.NumericUpDown();
            this.lblLearnedSkillExp = new System.Windows.Forms.Label();
            this.numLearnedSkillExp = new System.Windows.Forms.NumericUpDown();
            this.btnAddLearnedSkill = new System.Windows.Forms.Button();
            this.btnDeleteLearnedSkill = new System.Windows.Forms.Button();
            this.btnRefreshLearnedSkills = new System.Windows.Forms.Button();

            this.lblCharHeader = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Button();

            this.tabControlMain.SuspendLayout();
            this.tabQuests.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvQuests)).BeginInit();
            this.pnlQuestControls.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numQuestId)).BeginInit();

            this.tabPets.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPets)).BeginInit();
            this.pnlPetControls.SuspendLayout();

            this.tabNpcVisibility.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvNpcVisibility)).BeginInit();
            this.pnlNpcControls.SuspendLayout();

            this.tabInventory.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvInventory)).BeginInit();
            this.pnlInvControls.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numItemId)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numItemQty)).BeginInit();

            this.tabSkills.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSkills)).BeginInit();
            this.pnlSkillControls.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numSkillId)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSkillGrade)).BeginInit();

            this.tabLearnedSkills.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLearnedSkills)).BeginInit();
            this.pnlLearnedSkillControls.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numLearnedSkillId)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLearnedSkillGrade)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLearnedSkillExp)).BeginInit();

            this.SuspendLayout();

            // 
            // lblCharHeader
            // 
            this.lblCharHeader.AutoSize = true;
            this.lblCharHeader.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCharHeader.Location = new System.Drawing.Point(12, 9);
            this.lblCharHeader.Name = "lblCharHeader";
            this.lblCharHeader.Size = new System.Drawing.Size(260, 20);
            this.lblCharHeader.TabIndex = 0;
            this.lblCharHeader.Text = "Character Editor: [ID: 0] Loading...";

            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(822, 6);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(85, 28);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);

            // 
            // tabControlMain
            // 
            this.tabControlMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControlMain.Controls.Add(this.tabQuests);
            this.tabControlMain.Controls.Add(this.tabPets);
            this.tabControlMain.Controls.Add(this.tabNpcVisibility);
            this.tabControlMain.Controls.Add(this.tabInventory);
            this.tabControlMain.Controls.Add(this.tabSkills);
            this.tabControlMain.Controls.Add(this.tabLearnedSkills);
            this.tabControlMain.Location = new System.Drawing.Point(12, 38);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(895, 545);
            this.tabControlMain.TabIndex = 2;

            // 
            // tabQuests
            // 
            this.tabQuests.Controls.Add(this.dgvQuests);
            this.tabQuests.Controls.Add(this.pnlQuestControls);
            this.tabQuests.Location = new System.Drawing.Point(4, 22);
            this.tabQuests.Name = "tabQuests";
            this.tabQuests.Padding = new System.Windows.Forms.Padding(3);
            this.tabQuests.Size = new System.Drawing.Size(887, 519);
            this.tabQuests.TabIndex = 0;
            this.tabQuests.Text = "Quests & Flags (charquest)";
            this.tabQuests.UseVisualStyleBackColor = true;

            // 
            // dgvQuests
            // 
            this.dgvQuests.AllowUserToAddRows = false;
            this.dgvQuests.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvQuests.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvQuests.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvQuests.Location = new System.Drawing.Point(3, 3);
            this.dgvQuests.MultiSelect = false;
            this.dgvQuests.Name = "dgvQuests";
            this.dgvQuests.ReadOnly = true;
            this.dgvQuests.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvQuests.Size = new System.Drawing.Size(881, 413);
            this.dgvQuests.TabIndex = 0;

            // 
            // pnlQuestControls
            // 
            this.pnlQuestControls.Controls.Add(this.btnSyncLiveQuests);
            this.pnlQuestControls.Controls.Add(this.btnRefreshQuests);
            this.pnlQuestControls.Controls.Add(this.btnCompleteQuest);
            this.pnlQuestControls.Controls.Add(this.btnAdvanceStep);
            this.pnlQuestControls.Controls.Add(this.btnDeleteQuest);
            this.pnlQuestControls.Controls.Add(this.btnAddQuest);
            this.pnlQuestControls.Controls.Add(this.numQuestId);
            this.pnlQuestControls.Controls.Add(this.lblQuestId);
            this.pnlQuestControls.Controls.Add(this.cmbQuestState);
            this.pnlQuestControls.Controls.Add(this.lblQuestState);
            this.pnlQuestControls.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlQuestControls.Location = new System.Drawing.Point(3, 416);
            this.pnlQuestControls.Name = "pnlQuestControls";
            this.pnlQuestControls.Size = new System.Drawing.Size(881, 100);
            this.pnlQuestControls.TabIndex = 1;

            // 
            // lblQuestId
            // 
            this.lblQuestId.AutoSize = true;
            this.lblQuestId.Location = new System.Drawing.Point(10, 14);
            this.lblQuestId.Name = "lblQuestId";
            this.lblQuestId.Size = new System.Drawing.Size(53, 13);
            this.lblQuestId.TabIndex = 0;
            this.lblQuestId.Text = "Quest ID:";

            // 
            // numQuestId
            // 
            this.numQuestId.Location = new System.Drawing.Point(69, 11);
            this.numQuestId.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.numQuestId.Name = "numQuestId";
            this.numQuestId.Size = new System.Drawing.Size(80, 20);
            this.numQuestId.TabIndex = 1;
            this.numQuestId.Value = new decimal(new int[] { 10001, 0, 0, 0 });

            // 
            // lblQuestState
            // 
            this.lblQuestState.AutoSize = true;
            this.lblQuestState.Location = new System.Drawing.Point(165, 14);
            this.lblQuestState.Name = "lblQuestState";
            this.lblQuestState.Size = new System.Drawing.Size(35, 13);
            this.lblQuestState.TabIndex = 2;
            this.lblQuestState.Text = "State:";

            // 
            // cmbQuestState
            // 
            this.cmbQuestState.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbQuestState.FormattingEnabled = true;
            this.cmbQuestState.Items.AddRange(new object[] { "NotStarted (0)", "InProgress (1)", "Completed (2)" });
            this.cmbQuestState.Location = new System.Drawing.Point(206, 10);
            this.cmbQuestState.Name = "cmbQuestState";
            this.cmbQuestState.Size = new System.Drawing.Size(120, 21);
            this.cmbQuestState.TabIndex = 3;

            // 
            // btnAddQuest
            // 
            this.btnAddQuest.Location = new System.Drawing.Point(340, 9);
            this.btnAddQuest.Name = "btnAddQuest";
            this.btnAddQuest.Size = new System.Drawing.Size(110, 25);
            this.btnAddQuest.TabIndex = 4;
            this.btnAddQuest.Text = "+ Add / Set Quest";
            this.btnAddQuest.UseVisualStyleBackColor = true;
            this.btnAddQuest.Click += new System.EventHandler(this.btnAddQuest_Click);

            // 
            // btnCompleteQuest
            // 
            this.btnCompleteQuest.Location = new System.Drawing.Point(460, 9);
            this.btnCompleteQuest.Name = "btnCompleteQuest";
            this.btnCompleteQuest.Size = new System.Drawing.Size(120, 25);
            this.btnCompleteQuest.TabIndex = 5;
            this.btnCompleteQuest.Text = " Mark Completed";
            this.btnCompleteQuest.UseVisualStyleBackColor = true;
            this.btnCompleteQuest.Click += new System.EventHandler(this.btnCompleteQuest_Click);

            // 
            // btnAdvanceStep
            // 
            this.btnAdvanceStep.Location = new System.Drawing.Point(590, 9);
            this.btnAdvanceStep.Name = "btnAdvanceStep";
            this.btnAdvanceStep.Size = new System.Drawing.Size(100, 25);
            this.btnAdvanceStep.TabIndex = 6;
            this.btnAdvanceStep.Text = " Step +1";
            this.btnAdvanceStep.UseVisualStyleBackColor = true;
            this.btnAdvanceStep.Click += new System.EventHandler(this.btnAdvanceStep_Click);

            // 
            // btnDeleteQuest
            // 
            this.btnDeleteQuest.Location = new System.Drawing.Point(700, 9);
            this.btnDeleteQuest.Name = "btnDeleteQuest";
            this.btnDeleteQuest.Size = new System.Drawing.Size(85, 25);
            this.btnDeleteQuest.TabIndex = 7;
            this.btnDeleteQuest.Text = " Delete";
            this.btnDeleteQuest.UseVisualStyleBackColor = true;
            this.btnDeleteQuest.Click += new System.EventHandler(this.btnDeleteQuest_Click);

            // 
            // btnRefreshQuests
            // 
            this.btnRefreshQuests.Location = new System.Drawing.Point(13, 50);
            this.btnRefreshQuests.Name = "btnRefreshQuests";
            this.btnRefreshQuests.Size = new System.Drawing.Size(100, 28);
            this.btnRefreshQuests.TabIndex = 8;
            this.btnRefreshQuests.Text = " Refresh";
            this.btnRefreshQuests.UseVisualStyleBackColor = true;
            this.btnRefreshQuests.Click += new System.EventHandler(this.btnRefreshQuests_Click);

            // 
            // btnSyncLiveQuests
            // 
            this.btnSyncLiveQuests.Location = new System.Drawing.Point(125, 50);
            this.btnSyncLiveQuests.Name = "btnSyncLiveQuests";
            this.btnSyncLiveQuests.Size = new System.Drawing.Size(170, 28);
            this.btnSyncLiveQuests.TabIndex = 9;
            this.btnSyncLiveQuests.Text = " Sync Quests to Live Client";
            this.btnSyncLiveQuests.UseVisualStyleBackColor = true;
            this.btnSyncLiveQuests.Click += new System.EventHandler(this.btnSyncLiveQuests_Click);

            // 
            // tabPets
            // 
            this.tabPets.Controls.Add(this.dgvPets);
            this.tabPets.Controls.Add(this.pnlPetControls);
            this.tabPets.Location = new System.Drawing.Point(4, 22);
            this.tabPets.Name = "tabPets";
            this.tabPets.Padding = new System.Windows.Forms.Padding(3);
            this.tabPets.Size = new System.Drawing.Size(887, 519);
            this.tabPets.TabIndex = 1;
            this.tabPets.Text = "Companions & Pets (character_pets)";
            this.tabPets.UseVisualStyleBackColor = true;

            // 
            // dgvPets
            // 
            this.dgvPets.AllowUserToAddRows = false;
            this.dgvPets.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvPets.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPets.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvPets.Location = new System.Drawing.Point(3, 3);
            this.dgvPets.MultiSelect = false;
            this.dgvPets.Name = "dgvPets";
            this.dgvPets.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvPets.Size = new System.Drawing.Size(881, 413);
            this.dgvPets.TabIndex = 0;

            // 
            // pnlPetControls
            // 
            this.pnlPetControls.Controls.Add(this.btnSyncLivePets);
            this.pnlPetControls.Controls.Add(this.btnRefreshPets);
            this.pnlPetControls.Controls.Add(this.btnSavePets);
            this.pnlPetControls.Controls.Add(this.btnDeletePet);
            this.pnlPetControls.Controls.Add(this.btnMaxAmityHeal);
            this.pnlPetControls.Controls.Add(this.btnAddPetPreset);
            this.pnlPetControls.Controls.Add(this.cmbPetPresets);
            this.pnlPetControls.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlPetControls.Location = new System.Drawing.Point(3, 416);
            this.pnlPetControls.Name = "pnlPetControls";
            this.pnlPetControls.Size = new System.Drawing.Size(881, 100);
            this.pnlPetControls.TabIndex = 1;

            // 
            // cmbPetPresets
            // 
            this.cmbPetPresets.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPetPresets.FormattingEnabled = true;
            this.cmbPetPresets.Location = new System.Drawing.Point(10, 12);
            this.cmbPetPresets.Name = "cmbPetPresets";
            this.cmbPetPresets.Size = new System.Drawing.Size(200, 21);
            this.cmbPetPresets.TabIndex = 0;

            // 
            // btnAddPetPreset
            // 
            this.btnAddPetPreset.Location = new System.Drawing.Point(220, 9);
            this.btnAddPetPreset.Name = "btnAddPetPreset";
            this.btnAddPetPreset.Size = new System.Drawing.Size(120, 26);
            this.btnAddPetPreset.TabIndex = 1;
            this.btnAddPetPreset.Text = "+ Add Companion";
            this.btnAddPetPreset.UseVisualStyleBackColor = true;
            this.btnAddPetPreset.Click += new System.EventHandler(this.btnAddPetPreset_Click);

            // 
            // btnMaxAmityHeal
            // 
            this.btnMaxAmityHeal.Location = new System.Drawing.Point(350, 9);
            this.btnMaxAmityHeal.Name = "btnMaxAmityHeal";
            this.btnMaxAmityHeal.Size = new System.Drawing.Size(140, 26);
            this.btnMaxAmityHeal.TabIndex = 2;
            this.btnMaxAmityHeal.Text = " Max Amity & Full HP";
            this.btnMaxAmityHeal.UseVisualStyleBackColor = true;
            this.btnMaxAmityHeal.Click += new System.EventHandler(this.btnMaxAmityHeal_Click);

            // 
            // btnDeletePet
            // 
            this.btnDeletePet.Location = new System.Drawing.Point(500, 9);
            this.btnDeletePet.Name = "btnDeletePet";
            this.btnDeletePet.Size = new System.Drawing.Size(85, 26);
            this.btnDeletePet.TabIndex = 3;
            this.btnDeletePet.Text = " Delete";
            this.btnDeletePet.UseVisualStyleBackColor = true;
            this.btnDeletePet.Click += new System.EventHandler(this.btnDeletePet_Click);

            // 
            // btnSavePets
            // 
            this.btnSavePets.Location = new System.Drawing.Point(595, 9);
            this.btnSavePets.Name = "btnSavePets";
            this.btnSavePets.Size = new System.Drawing.Size(100, 26);
            this.btnSavePets.TabIndex = 4;
            this.btnSavePets.Text = " Save Grid";
            this.btnSavePets.UseVisualStyleBackColor = true;
            this.btnSavePets.Click += new System.EventHandler(this.btnSavePets_Click);

            // 
            // btnRefreshPets
            // 
            this.btnRefreshPets.Location = new System.Drawing.Point(10, 50);
            this.btnRefreshPets.Name = "btnRefreshPets";
            this.btnRefreshPets.Size = new System.Drawing.Size(100, 28);
            this.btnRefreshPets.TabIndex = 5;
            this.btnRefreshPets.Text = " Refresh";
            this.btnRefreshPets.UseVisualStyleBackColor = true;
            this.btnRefreshPets.Click += new System.EventHandler(this.btnRefreshPets_Click);

            // 
            // btnSyncLivePets
            // 
            this.btnSyncLivePets.Location = new System.Drawing.Point(120, 50);
            this.btnSyncLivePets.Name = "btnSyncLivePets";
            this.btnSyncLivePets.Size = new System.Drawing.Size(170, 28);
            this.btnSyncLivePets.TabIndex = 6;
            this.btnSyncLivePets.Text = " Sync Pets to Live Client";
            this.btnSyncLivePets.UseVisualStyleBackColor = true;
            this.btnSyncLivePets.Click += new System.EventHandler(this.btnSyncLivePets_Click);

            // 
            // tabNpcVisibility
            // 
            this.tabNpcVisibility.Controls.Add(this.dgvNpcVisibility);
            this.tabNpcVisibility.Controls.Add(this.pnlNpcControls);
            this.tabNpcVisibility.Location = new System.Drawing.Point(4, 22);
            this.tabNpcVisibility.Name = "tabNpcVisibility";
            this.tabNpcVisibility.Padding = new System.Windows.Forms.Padding(3);
            this.tabNpcVisibility.Size = new System.Drawing.Size(887, 519);
            this.tabNpcVisibility.TabIndex = 2;
            this.tabNpcVisibility.Text = "Map NPC & Chest Visibility";
            this.tabNpcVisibility.UseVisualStyleBackColor = true;

            // 
            // dgvNpcVisibility
            // 
            this.dgvNpcVisibility.AllowUserToAddRows = false;
            this.dgvNpcVisibility.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvNpcVisibility.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvNpcVisibility.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvNpcVisibility.Location = new System.Drawing.Point(3, 3);
            this.dgvNpcVisibility.MultiSelect = false;
            this.dgvNpcVisibility.Name = "dgvNpcVisibility";
            this.dgvNpcVisibility.ReadOnly = true;
            this.dgvNpcVisibility.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvNpcVisibility.Size = new System.Drawing.Size(881, 413);
            this.dgvNpcVisibility.TabIndex = 0;

            // 
            // pnlNpcControls
            // 
            this.pnlNpcControls.Controls.Add(this.btnRefreshNpcs);
            this.pnlNpcControls.Controls.Add(this.btnBroadcastNpcLive);
            this.pnlNpcControls.Controls.Add(this.btnShowUnhideNpc);
            this.pnlNpcControls.Controls.Add(this.btnHideDespawnNpc);
            this.pnlNpcControls.Controls.Add(this.btnRestoreCloseChest);
            this.pnlNpcControls.Controls.Add(this.btnOpenBreakChest);
            this.pnlNpcControls.Controls.Add(this.lblSelectMap);
            this.pnlNpcControls.Controls.Add(this.cmbMapSelector);
            this.pnlNpcControls.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlNpcControls.Location = new System.Drawing.Point(3, 416);
            this.pnlNpcControls.Name = "pnlNpcControls";
            this.pnlNpcControls.Size = new System.Drawing.Size(881, 100);
            this.pnlNpcControls.TabIndex = 1;

            // 
            // lblSelectMap
            // 
            this.lblSelectMap.AutoSize = true;
            this.lblSelectMap.Location = new System.Drawing.Point(10, 15);
            this.lblSelectMap.Name = "lblSelectMap";
            this.lblSelectMap.Size = new System.Drawing.Size(64, 13);
            this.lblSelectMap.TabIndex = 0;
            this.lblSelectMap.Text = "Select Map:";

            // 
            // cmbMapSelector
            // 
            this.cmbMapSelector.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMapSelector.FormattingEnabled = true;
            this.cmbMapSelector.Location = new System.Drawing.Point(80, 11);
            this.cmbMapSelector.Name = "cmbMapSelector";
            this.cmbMapSelector.Size = new System.Drawing.Size(220, 21);
            this.cmbMapSelector.TabIndex = 1;
            this.cmbMapSelector.SelectedIndexChanged += new System.EventHandler(this.cmbMapSelector_SelectedIndexChanged);

            // 
            // btnOpenBreakChest
            // 
            this.btnOpenBreakChest.Location = new System.Drawing.Point(320, 9);
            this.btnOpenBreakChest.Name = "btnOpenBreakChest";
            this.btnOpenBreakChest.Size = new System.Drawing.Size(120, 26);
            this.btnOpenBreakChest.TabIndex = 2;
            this.btnOpenBreakChest.Text = " Break / Open";
            this.btnOpenBreakChest.UseVisualStyleBackColor = true;
            this.btnOpenBreakChest.Click += new System.EventHandler(this.btnOpenBreakChest_Click);

            // 
            // btnRestoreCloseChest
            // 
            this.btnRestoreCloseChest.Location = new System.Drawing.Point(445, 9);
            this.btnRestoreCloseChest.Name = "btnRestoreCloseChest";
            this.btnRestoreCloseChest.Size = new System.Drawing.Size(120, 26);
            this.btnRestoreCloseChest.TabIndex = 3;
            this.btnRestoreCloseChest.Text = " Reset / Unbroken";
            this.btnRestoreCloseChest.UseVisualStyleBackColor = true;
            this.btnRestoreCloseChest.Click += new System.EventHandler(this.btnRestoreCloseChest_Click);

            // 
            // btnHideDespawnNpc
            // 
            this.btnHideDespawnNpc.Location = new System.Drawing.Point(575, 9);
            this.btnHideDespawnNpc.Name = "btnHideDespawnNpc";
            this.btnHideDespawnNpc.Size = new System.Drawing.Size(120, 26);
            this.btnHideDespawnNpc.TabIndex = 4;
            this.btnHideDespawnNpc.Text = " Hide / Despawn";
            this.btnHideDespawnNpc.UseVisualStyleBackColor = true;
            this.btnHideDespawnNpc.Click += new System.EventHandler(this.btnHideDespawnNpc_Click);

            // 
            // btnShowUnhideNpc
            // 
            this.btnShowUnhideNpc.Location = new System.Drawing.Point(705, 9);
            this.btnShowUnhideNpc.Name = "btnShowUnhideNpc";
            this.btnShowUnhideNpc.Size = new System.Drawing.Size(110, 26);
            this.btnShowUnhideNpc.TabIndex = 5;
            this.btnShowUnhideNpc.Text = " Show / Visible";
            this.btnShowUnhideNpc.UseVisualStyleBackColor = true;
            this.btnShowUnhideNpc.Click += new System.EventHandler(this.btnShowUnhideNpc_Click);

            // 
            // btnRefreshNpcs
            // 
            this.btnRefreshNpcs.Location = new System.Drawing.Point(10, 50);
            this.btnRefreshNpcs.Name = "btnRefreshNpcs";
            this.btnRefreshNpcs.Size = new System.Drawing.Size(100, 28);
            this.btnRefreshNpcs.TabIndex = 6;
            this.btnRefreshNpcs.Text = " Refresh";
            this.btnRefreshNpcs.UseVisualStyleBackColor = true;
            this.btnRefreshNpcs.Click += new System.EventHandler(this.btnRefreshNpcs_Click);

            // 
            // btnBroadcastNpcLive
            // 
            this.btnBroadcastNpcLive.Location = new System.Drawing.Point(120, 50);
            this.btnBroadcastNpcLive.Name = "btnBroadcastNpcLive";
            this.btnBroadcastNpcLive.Size = new System.Drawing.Size(200, 28);
            this.btnBroadcastNpcLive.TabIndex = 7;
            this.btnBroadcastNpcLive.Text = " Send AC 22 Live Packet";
            this.btnBroadcastNpcLive.UseVisualStyleBackColor = true;
            this.btnBroadcastNpcLive.Click += new System.EventHandler(this.btnBroadcastNpcLive_Click);

            // 
            // tabInventory
            // 
            this.tabInventory.Controls.Add(this.dgvInventory);
            this.tabInventory.Controls.Add(this.pnlInvControls);
            this.tabInventory.Location = new System.Drawing.Point(4, 22);
            this.tabInventory.Name = "tabInventory";
            this.tabInventory.Padding = new System.Windows.Forms.Padding(3);
            this.tabInventory.Size = new System.Drawing.Size(887, 519);
            this.tabInventory.TabIndex = 3;
            this.tabInventory.Text = "Inventory (inventory)";
            this.tabInventory.UseVisualStyleBackColor = true;

            // 
            // dgvInventory
            // 
            this.dgvInventory.AllowUserToAddRows = false;
            this.dgvInventory.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvInventory.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvInventory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvInventory.Location = new System.Drawing.Point(3, 3);
            this.dgvInventory.MultiSelect = false;
            this.dgvInventory.Name = "dgvInventory";
            this.dgvInventory.ReadOnly = true;
            this.dgvInventory.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvInventory.Size = new System.Drawing.Size(881, 413);
            this.dgvInventory.TabIndex = 0;

            // 
            // pnlInvControls
            // 
            this.pnlInvControls.Controls.Add(this.btnRefreshInventory);
            this.pnlInvControls.Controls.Add(this.btnRepairItem);
            this.pnlInvControls.Controls.Add(this.btnDeleteItem);
            this.pnlInvControls.Controls.Add(this.btnAddItem);
            this.pnlInvControls.Controls.Add(this.lblItemQty);
            this.pnlInvControls.Controls.Add(this.numItemQty);
            this.pnlInvControls.Controls.Add(this.lblItemId);
            this.pnlInvControls.Controls.Add(this.numItemId);
            this.pnlInvControls.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlInvControls.Location = new System.Drawing.Point(3, 416);
            this.pnlInvControls.Name = "pnlInvControls";
            this.pnlInvControls.Size = new System.Drawing.Size(881, 100);
            this.pnlInvControls.TabIndex = 1;

            // 
            // lblItemId
            // 
            this.lblItemId.AutoSize = true;
            this.lblItemId.Location = new System.Drawing.Point(10, 15);
            this.lblItemId.Name = "lblItemId";
            this.lblItemId.Size = new System.Drawing.Size(44, 13);
            this.lblItemId.TabIndex = 0;
            this.lblItemId.Text = "Item ID:";

            // 
            // numItemId
            // 
            this.numItemId.Location = new System.Drawing.Point(60, 12);
            this.numItemId.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.numItemId.Name = "numItemId";
            this.numItemId.Size = new System.Drawing.Size(80, 20);
            this.numItemId.TabIndex = 1;
            this.numItemId.Value = new decimal(new int[] { 10001, 0, 0, 0 });

            // 
            // lblItemQty
            // 
            this.lblItemQty.AutoSize = true;
            this.lblItemQty.Location = new System.Drawing.Point(155, 15);
            this.lblItemQty.Name = "lblItemQty";
            this.lblItemQty.Size = new System.Drawing.Size(26, 13);
            this.lblItemQty.TabIndex = 2;
            this.lblItemQty.Text = "Qty:";

            // 
            // numItemQty
            // 
            this.numItemQty.Location = new System.Drawing.Point(185, 12);
            this.numItemQty.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
            this.numItemQty.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numItemQty.Name = "numItemQty";
            this.numItemQty.Size = new System.Drawing.Size(60, 20);
            this.numItemQty.TabIndex = 3;
            this.numItemQty.Value = new decimal(new int[] { 1, 0, 0, 0 });

            // 
            // btnAddItem
            // 
            this.btnAddItem.Location = new System.Drawing.Point(260, 9);
            this.btnAddItem.Name = "btnAddItem";
            this.btnAddItem.Size = new System.Drawing.Size(100, 26);
            this.btnAddItem.TabIndex = 4;
            this.btnAddItem.Text = "+ Add Item";
            this.btnAddItem.UseVisualStyleBackColor = true;
            this.btnAddItem.Click += new System.EventHandler(this.btnAddItem_Click);

            // 
            // btnDeleteItem
            // 
            this.btnDeleteItem.Location = new System.Drawing.Point(370, 9);
            this.btnDeleteItem.Name = "btnDeleteItem";
            this.btnDeleteItem.Size = new System.Drawing.Size(90, 26);
            this.btnDeleteItem.TabIndex = 5;
            this.btnDeleteItem.Text = " Delete";
            this.btnDeleteItem.UseVisualStyleBackColor = true;
            this.btnDeleteItem.Click += new System.EventHandler(this.btnDeleteItem_Click);

            // 
            // btnRepairItem
            // 
            this.btnRepairItem.Location = new System.Drawing.Point(470, 9);
            this.btnRepairItem.Name = "btnRepairItem";
            this.btnRepairItem.Size = new System.Drawing.Size(120, 26);
            this.btnRepairItem.TabIndex = 6;
            this.btnRepairItem.Text = " Repair 100%";
            this.btnRepairItem.UseVisualStyleBackColor = true;
            this.btnRepairItem.Click += new System.EventHandler(this.btnRepairItem_Click);

            // 
            // btnRefreshInventory
            // 
            this.btnRefreshInventory.Location = new System.Drawing.Point(10, 50);
            this.btnRefreshInventory.Name = "btnRefreshInventory";
            this.btnRefreshInventory.Size = new System.Drawing.Size(100, 28);
            this.btnRefreshInventory.TabIndex = 7;
            this.btnRefreshInventory.Text = " Refresh";
            this.btnRefreshInventory.UseVisualStyleBackColor = true;
            this.btnRefreshInventory.Click += new System.EventHandler(this.btnRefreshInventory_Click);

            // 
            // tabSkills
            // 
            this.tabSkills.Controls.Add(this.dgvSkills);
            this.tabSkills.Controls.Add(this.pnlSkillControls);
            this.tabSkills.Location = new System.Drawing.Point(4, 22);
            this.tabSkills.Name = "tabSkills";
            this.tabSkills.Padding = new System.Windows.Forms.Padding(3);
            this.tabSkills.Size = new System.Drawing.Size(887, 519);
            this.tabSkills.TabIndex = 4;
            this.tabSkills.Text = "Event & NPC Unlocks (charunlocks)";
            this.tabSkills.UseVisualStyleBackColor = true;

            // 
            // dgvSkills
            // 
            this.dgvSkills.AllowUserToAddRows = false;
            this.dgvSkills.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvSkills.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSkills.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvSkills.Location = new System.Drawing.Point(3, 3);
            this.dgvSkills.MultiSelect = false;
            this.dgvSkills.Name = "dgvSkills";
            this.dgvSkills.ReadOnly = true;
            this.dgvSkills.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvSkills.Size = new System.Drawing.Size(881, 413);
            this.dgvSkills.TabIndex = 0;

            // 
            // pnlSkillControls
            // 
            this.pnlSkillControls.Controls.Add(this.btnRefreshSkills);
            this.pnlSkillControls.Controls.Add(this.btnDeleteSkill);
            this.pnlSkillControls.Controls.Add(this.btnAddSkill);
            this.pnlSkillControls.Controls.Add(this.lblSkillGrade);
            this.pnlSkillControls.Controls.Add(this.numSkillGrade);
            this.pnlSkillControls.Controls.Add(this.lblSkillId);
            this.pnlSkillControls.Controls.Add(this.numSkillId);
            this.pnlSkillControls.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlSkillControls.Location = new System.Drawing.Point(3, 416);
            this.pnlSkillControls.Name = "pnlSkillControls";
            this.pnlSkillControls.Size = new System.Drawing.Size(881, 100);
            this.pnlSkillControls.TabIndex = 1;

            // 
            // lblSkillId
            // 
            this.lblSkillId.AutoSize = true;
            this.lblSkillId.Location = new System.Drawing.Point(10, 15);
            this.lblSkillId.Name = "lblSkillId";
            this.lblSkillId.Size = new System.Drawing.Size(48, 13);
            this.lblSkillId.TabIndex = 0;
            this.lblSkillId.Text = "Map ID:";

            // 
            // numSkillId
            // 
            this.numSkillId.Location = new System.Drawing.Point(65, 12);
            this.numSkillId.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.numSkillId.Name = "numSkillId";
            this.numSkillId.Size = new System.Drawing.Size(80, 20);
            this.numSkillId.TabIndex = 1;
            this.numSkillId.Value = new decimal(new int[] { 10035, 0, 0, 0 });

            // 
            // lblSkillGrade
            // 
            this.lblSkillGrade.AutoSize = true;
            this.lblSkillGrade.Location = new System.Drawing.Point(160, 15);
            this.lblSkillGrade.Name = "lblSkillGrade";
            this.lblSkillGrade.Size = new System.Drawing.Size(48, 13);
            this.lblSkillGrade.TabIndex = 2;
            this.lblSkillGrade.Text = "Click ID:";

            // 
            // numSkillGrade
            // 
            this.numSkillGrade.Location = new System.Drawing.Point(215, 12);
            this.numSkillGrade.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.numSkillGrade.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numSkillGrade.Name = "numSkillGrade";
            this.numSkillGrade.Size = new System.Drawing.Size(60, 20);
            this.numSkillGrade.TabIndex = 3;
            this.numSkillGrade.Value = new decimal(new int[] { 1, 0, 0, 0 });

            // 
            // btnAddSkill
            // 
            this.btnAddSkill.Location = new System.Drawing.Point(290, 9);
            this.btnAddSkill.Name = "btnAddSkill";
            this.btnAddSkill.Size = new System.Drawing.Size(140, 26);
            this.btnAddSkill.TabIndex = 4;
            this.btnAddSkill.Text = "+ Add Unlock Flag";
            this.btnAddSkill.UseVisualStyleBackColor = true;
            this.btnAddSkill.Click += new System.EventHandler(this.btnAddSkill_Click);

            // 
            // btnDeleteSkill
            // 
            this.btnDeleteSkill.Location = new System.Drawing.Point(440, 9);
            this.btnDeleteSkill.Name = "btnDeleteSkill";
            this.btnDeleteSkill.Size = new System.Drawing.Size(130, 26);
            this.btnDeleteSkill.TabIndex = 5;
            this.btnDeleteSkill.Text = " Revoke Unlock";
            this.btnDeleteSkill.UseVisualStyleBackColor = true;
            this.btnDeleteSkill.Click += new System.EventHandler(this.btnDeleteSkill_Click);

            // 
            // btnRefreshSkills
            // 
            this.btnRefreshSkills.Location = new System.Drawing.Point(10, 50);
            this.btnRefreshSkills.Name = "btnRefreshSkills";
            this.btnRefreshSkills.Size = new System.Drawing.Size(100, 28);
            this.btnRefreshSkills.TabIndex = 6;
            this.btnRefreshSkills.Text = " Refresh";
            this.btnRefreshSkills.UseVisualStyleBackColor = true;
            this.btnRefreshSkills.Click += new System.EventHandler(this.btnRefreshSkills_Click);

            // 
            // tabLearnedSkills
            // 
            this.tabLearnedSkills.Controls.Add(this.dgvLearnedSkills);
            this.tabLearnedSkills.Controls.Add(this.pnlLearnedSkillControls);
            this.tabLearnedSkills.Location = new System.Drawing.Point(4, 22);
            this.tabLearnedSkills.Name = "tabLearnedSkills";
            this.tabLearnedSkills.Padding = new System.Windows.Forms.Padding(3);
            this.tabLearnedSkills.Size = new System.Drawing.Size(887, 519);
            this.tabLearnedSkills.TabIndex = 5;
            this.tabLearnedSkills.Text = "Learned Skills (character_skills)";
            this.tabLearnedSkills.UseVisualStyleBackColor = true;

            // 
            // dgvLearnedSkills
            // 
            this.dgvLearnedSkills.AllowUserToAddRows = false;
            this.dgvLearnedSkills.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvLearnedSkills.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvLearnedSkills.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvLearnedSkills.Location = new System.Drawing.Point(3, 3);
            this.dgvLearnedSkills.MultiSelect = false;
            this.dgvLearnedSkills.Name = "dgvLearnedSkills";
            this.dgvLearnedSkills.ReadOnly = true;
            this.dgvLearnedSkills.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvLearnedSkills.Size = new System.Drawing.Size(881, 413);
            this.dgvLearnedSkills.TabIndex = 0;

            // 
            // pnlLearnedSkillControls
            // 
            this.pnlLearnedSkillControls.Controls.Add(this.btnRefreshLearnedSkills);
            this.pnlLearnedSkillControls.Controls.Add(this.btnDeleteLearnedSkill);
            this.pnlLearnedSkillControls.Controls.Add(this.btnAddLearnedSkill);
            this.pnlLearnedSkillControls.Controls.Add(this.lblLearnedSkillExp);
            this.pnlLearnedSkillControls.Controls.Add(this.numLearnedSkillExp);
            this.pnlLearnedSkillControls.Controls.Add(this.lblLearnedSkillGrade);
            this.pnlLearnedSkillControls.Controls.Add(this.numLearnedSkillGrade);
            this.pnlLearnedSkillControls.Controls.Add(this.lblLearnedSkillId);
            this.pnlLearnedSkillControls.Controls.Add(this.numLearnedSkillId);
            this.pnlLearnedSkillControls.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlLearnedSkillControls.Location = new System.Drawing.Point(3, 416);
            this.pnlLearnedSkillControls.Name = "pnlLearnedSkillControls";
            this.pnlLearnedSkillControls.Size = new System.Drawing.Size(881, 100);
            this.pnlLearnedSkillControls.TabIndex = 1;

            // 
            // lblLearnedSkillId
            // 
            this.lblLearnedSkillId.AutoSize = true;
            this.lblLearnedSkillId.Location = new System.Drawing.Point(10, 15);
            this.lblLearnedSkillId.Name = "lblLearnedSkillId";
            this.lblLearnedSkillId.Size = new System.Drawing.Size(46, 13);
            this.lblLearnedSkillId.TabIndex = 0;
            this.lblLearnedSkillId.Text = "Skill ID:";

            // 
            // numLearnedSkillId
            // 
            this.numLearnedSkillId.Location = new System.Drawing.Point(60, 12);
            this.numLearnedSkillId.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.numLearnedSkillId.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numLearnedSkillId.Name = "numLearnedSkillId";
            this.numLearnedSkillId.Size = new System.Drawing.Size(80, 20);
            this.numLearnedSkillId.TabIndex = 1;
            this.numLearnedSkillId.Value = new decimal(new int[] { 11016, 0, 0, 0 });

            // 
            // lblLearnedSkillGrade
            // 
            this.lblLearnedSkillGrade.AutoSize = true;
            this.lblLearnedSkillGrade.Location = new System.Drawing.Point(150, 15);
            this.lblLearnedSkillGrade.Name = "lblLearnedSkillGrade";
            this.lblLearnedSkillGrade.Size = new System.Drawing.Size(41, 13);
            this.lblLearnedSkillGrade.TabIndex = 2;
            this.lblLearnedSkillGrade.Text = "Grade:";

            // 
            // numLearnedSkillGrade
            // 
            this.numLearnedSkillGrade.Location = new System.Drawing.Point(195, 12);
            this.numLearnedSkillGrade.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numLearnedSkillGrade.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numLearnedSkillGrade.Name = "numLearnedSkillGrade";
            this.numLearnedSkillGrade.Size = new System.Drawing.Size(50, 20);
            this.numLearnedSkillGrade.TabIndex = 3;
            this.numLearnedSkillGrade.Value = new decimal(new int[] { 1, 0, 0, 0 });

            // 
            // lblLearnedSkillExp
            // 
            this.lblLearnedSkillExp.AutoSize = true;
            this.lblLearnedSkillExp.Location = new System.Drawing.Point(255, 15);
            this.lblLearnedSkillExp.Name = "lblLearnedSkillExp";
            this.lblLearnedSkillExp.Size = new System.Drawing.Size(28, 13);
            this.lblLearnedSkillExp.TabIndex = 4;
            this.lblLearnedSkillExp.Text = "EXP:";

            // 
            // numLearnedSkillExp
            // 
            this.numLearnedSkillExp.Location = new System.Drawing.Point(290, 12);
            this.numLearnedSkillExp.Maximum = new decimal(new int[] { 10000000, 0, 0, 0 });
            this.numLearnedSkillExp.Name = "numLearnedSkillExp";
            this.numLearnedSkillExp.Size = new System.Drawing.Size(90, 20);
            this.numLearnedSkillExp.TabIndex = 5;
            this.numLearnedSkillExp.Value = new decimal(new int[] { 0, 0, 0, 0 });

            // 
            // btnAddLearnedSkill
            // 
            this.btnAddLearnedSkill.Location = new System.Drawing.Point(395, 9);
            this.btnAddLearnedSkill.Name = "btnAddLearnedSkill";
            this.btnAddLearnedSkill.Size = new System.Drawing.Size(140, 26);
            this.btnAddLearnedSkill.TabIndex = 6;
            this.btnAddLearnedSkill.Text = "+ Learn / Update Skill";
            this.btnAddLearnedSkill.UseVisualStyleBackColor = true;
            this.btnAddLearnedSkill.Click += new System.EventHandler(this.btnAddLearnedSkill_Click);

            // 
            // btnDeleteLearnedSkill
            // 
            this.btnDeleteLearnedSkill.Location = new System.Drawing.Point(545, 9);
            this.btnDeleteLearnedSkill.Name = "btnDeleteLearnedSkill";
            this.btnDeleteLearnedSkill.Size = new System.Drawing.Size(120, 26);
            this.btnDeleteLearnedSkill.TabIndex = 7;
            this.btnDeleteLearnedSkill.Text = " Forget Skill";
            this.btnDeleteLearnedSkill.UseVisualStyleBackColor = true;
            this.btnDeleteLearnedSkill.Click += new System.EventHandler(this.btnDeleteLearnedSkill_Click);

            // 
            // btnRefreshLearnedSkills
            // 
            this.btnRefreshLearnedSkills.Location = new System.Drawing.Point(10, 50);
            this.btnRefreshLearnedSkills.Name = "btnRefreshLearnedSkills";
            this.btnRefreshLearnedSkills.Size = new System.Drawing.Size(100, 28);
            this.btnRefreshLearnedSkills.TabIndex = 8;
            this.btnRefreshLearnedSkills.Text = " Refresh";
            this.btnRefreshLearnedSkills.UseVisualStyleBackColor = true;
            this.btnRefreshLearnedSkills.Click += new System.EventHandler(this.btnRefreshLearnedSkills_Click);

            // 
            // CharacterDataEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(919, 595);
            this.Controls.Add(this.tabControlMain);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.lblCharHeader);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "CharacterDataEditorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Character Relational Database & Map Visibility Editor";
            this.Load += new System.EventHandler(this.CharacterDataEditorForm_Load);

            this.tabControlMain.ResumeLayout(false);
            this.tabQuests.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvQuests)).EndInit();
            this.pnlQuestControls.ResumeLayout(false);
            this.pnlQuestControls.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numQuestId)).EndInit();

            this.tabPets.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPets)).EndInit();
            this.pnlPetControls.ResumeLayout(false);

            this.tabNpcVisibility.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvNpcVisibility)).EndInit();
            this.pnlNpcControls.ResumeLayout(false);
            this.pnlNpcControls.PerformLayout();

            this.tabInventory.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvInventory)).EndInit();
            this.pnlInvControls.ResumeLayout(false);
            this.pnlInvControls.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numItemId)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numItemQty)).EndInit();

            this.tabSkills.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvSkills)).EndInit();
            this.pnlSkillControls.ResumeLayout(false);
            this.pnlSkillControls.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numSkillId)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSkillGrade)).EndInit();

            this.tabLearnedSkills.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvLearnedSkills)).EndInit();
            this.pnlLearnedSkillControls.ResumeLayout(false);
            this.pnlLearnedSkillControls.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numLearnedSkillId)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLearnedSkillGrade)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLearnedSkillExp)).EndInit();

            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.Label lblCharHeader;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.TabControl tabControlMain;
        
        // Quests
        private System.Windows.Forms.TabPage tabQuests;
        private System.Windows.Forms.DataGridView dgvQuests;
        private System.Windows.Forms.Panel pnlQuestControls;
        private System.Windows.Forms.Label lblQuestId;
        private System.Windows.Forms.NumericUpDown numQuestId;
        private System.Windows.Forms.Label lblQuestState;
        private System.Windows.Forms.ComboBox cmbQuestState;
        private System.Windows.Forms.Button btnAddQuest;
        private System.Windows.Forms.Button btnCompleteQuest;
        private System.Windows.Forms.Button btnAdvanceStep;
        private System.Windows.Forms.Button btnDeleteQuest;
        private System.Windows.Forms.Button btnRefreshQuests;
        private System.Windows.Forms.Button btnSyncLiveQuests;

        // Pets
        private System.Windows.Forms.TabPage tabPets;
        private System.Windows.Forms.DataGridView dgvPets;
        private System.Windows.Forms.Panel pnlPetControls;
        private System.Windows.Forms.ComboBox cmbPetPresets;
        private System.Windows.Forms.Button btnAddPetPreset;
        private System.Windows.Forms.Button btnMaxAmityHeal;
        private System.Windows.Forms.Button btnDeletePet;
        private System.Windows.Forms.Button btnSavePets;
        private System.Windows.Forms.Button btnRefreshPets;
        private System.Windows.Forms.Button btnSyncLivePets;

        // Map NPC Visibility
        private System.Windows.Forms.TabPage tabNpcVisibility;
        private System.Windows.Forms.DataGridView dgvNpcVisibility;
        private System.Windows.Forms.Panel pnlNpcControls;
        private System.Windows.Forms.Label lblSelectMap;
        private System.Windows.Forms.ComboBox cmbMapSelector;
        private System.Windows.Forms.Button btnOpenBreakChest;
        private System.Windows.Forms.Button btnRestoreCloseChest;
        private System.Windows.Forms.Button btnHideDespawnNpc;
        private System.Windows.Forms.Button btnShowUnhideNpc;
        private System.Windows.Forms.Button btnRefreshNpcs;
        private System.Windows.Forms.Button btnBroadcastNpcLive;

        // Inventory
        private System.Windows.Forms.TabPage tabInventory;
        private System.Windows.Forms.DataGridView dgvInventory;
        private System.Windows.Forms.Panel pnlInvControls;
        private System.Windows.Forms.Label lblItemId;
        private System.Windows.Forms.NumericUpDown numItemId;
        private System.Windows.Forms.Label lblItemQty;
        private System.Windows.Forms.NumericUpDown numItemQty;
        private System.Windows.Forms.Button btnAddItem;
        private System.Windows.Forms.Button btnDeleteItem;
        private System.Windows.Forms.Button btnRepairItem;
        private System.Windows.Forms.Button btnRefreshInventory;

        // Unlocks (charunlocks)
        private System.Windows.Forms.TabPage tabSkills;
        private System.Windows.Forms.DataGridView dgvSkills;
        private System.Windows.Forms.Panel pnlSkillControls;
        private System.Windows.Forms.Label lblSkillId;
        private System.Windows.Forms.NumericUpDown numSkillId;
        private System.Windows.Forms.Label lblSkillGrade;
        private System.Windows.Forms.NumericUpDown numSkillGrade;
        private System.Windows.Forms.Button btnAddSkill;
        private System.Windows.Forms.Button btnDeleteSkill;
        private System.Windows.Forms.Button btnRefreshSkills;

        // Learned Skills (character_skills)
        private System.Windows.Forms.TabPage tabLearnedSkills;
        private System.Windows.Forms.DataGridView dgvLearnedSkills;
        private System.Windows.Forms.Panel pnlLearnedSkillControls;
        private System.Windows.Forms.Label lblLearnedSkillId;
        private System.Windows.Forms.NumericUpDown numLearnedSkillId;
        private System.Windows.Forms.Label lblLearnedSkillGrade;
        private System.Windows.Forms.NumericUpDown numLearnedSkillGrade;
        private System.Windows.Forms.Label lblLearnedSkillExp;
        private System.Windows.Forms.NumericUpDown numLearnedSkillExp;
        private System.Windows.Forms.Button btnAddLearnedSkill;
        private System.Windows.Forms.Button btnDeleteLearnedSkill;
        private System.Windows.Forms.Button btnRefreshLearnedSkills;
    }
}
