using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Game;
using Game.Maps;
using Game.QuestRelated;
using Network;

namespace Wonderland_Private_Server
{
    public partial class CharacterDataEditorForm : Form
    {
        private uint _charId;
        private string _charName;
        private ushort _currentMapId = 10035;

        public CharacterDataEditorForm(uint charId, string charName)
        {
            InitializeComponent();
            _charId = charId;
            _charName = charName ?? "Unknown";
        }

        private void CharacterDataEditorForm_Load(object sender, EventArgs e)
        {
            lblCharHeader.Text = $"Character Editor: [{_charName}] (CharID: {_charId})";

            // Initialize Quest State Combobox
            cmbQuestState.SelectedIndex = 2; // Completed by default

            // Initialize Pet Presets
            cmbPetPresets.Items.Clear();
            cmbPetPresets.Items.Add(new PetPresetItem(12178, "Robinson (12178) - Water"));
            cmbPetPresets.Items.Add(new PetPresetItem(10727, "Monkey (10727) - Earth"));
            cmbPetPresets.Items.Add(new PetPresetItem(11066, "Niss (11066) - Wind"));
            cmbPetPresets.Items.Add(new PetPresetItem(14156, "Xaolan (14156) - Water"));
            cmbPetPresets.Items.Add(new PetPresetItem(14157, "Elin (14157) - Fire"));
            cmbPetPresets.Items.Add(new PetPresetItem(11067, "Shizune (11067) - Earth"));
            cmbPetPresets.Items.Add(new PetPresetItem(11068, "Cliff (11068) - Wind"));
            cmbPetPresets.Items.Add(new PetPresetItem(11069, "Clive (11069) - Fire"));
            cmbPetPresets.Items.Add(new PetPresetItem(11070, "Sam (11070) - Water"));
            cmbPetPresets.SelectedIndex = 0;

            // Initialize Map Selector
            PopulateMapSelector();

            // Load All Tabs
            RefreshQuests();
            RefreshPets();
            RefreshNpcVisibility();
            RefreshInventory();
            RefreshSkills();
            RefreshLearnedSkills();
        }

        private Player GetOnlinePlayer()
        {
            try
            {
                return cGlobal.gLoginServer?.GetAllPlayers()?.FirstOrDefault(p => p.CharID == _charId);
            }
            catch
            {
                return null;
            }
        }

        #region 1. Quests Tab
        private void RefreshQuests()
        {
            try
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("QuestID", typeof(uint));
                dt.Columns.Add("QuestName", typeof(string));
                dt.Columns.Add("State", typeof(string));
                dt.Columns.Add("StateCode", typeof(byte));

                var dbQuests = cGlobal.gCharacterDataBase.GetDataTable($"SELECT quest_started, quest_pos FROM charquest WHERE charID = '{_charId}' ORDER BY quest_started");
                if (dbQuests != null && dbQuests.Rows.Count > 0)
                {
                    foreach (DataRow row in dbQuests.Rows)
                    {
                        uint qId = Convert.ToUInt32(row["quest_started"]);
                        byte stateVal = Convert.ToByte(row["quest_pos"]);

                        string qName = "Unknown Quest";
                        if (QuestManager.AllQuests.TryGetValue(qId, out var qDef))
                        {
                            qName = qDef.Title ?? $"Quest #{qId}";
                        }
                        else
                        {
                            qName = $"Quest #{qId}";
                        }

                        string stateStr = stateVal == 2 ? "Completed (2)" : (stateVal == 1 ? "InProgress (1)" : "NotStarted (0)");
                        dt.Rows.Add(qId, qName, stateStr, stateVal);
                    }
                }

                dgvQuests.DataSource = dt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading quests: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddQuest_Click(object sender, EventArgs e)
        {
            uint qId = (uint)numQuestId.Value;
            byte state = (byte)cmbQuestState.SelectedIndex; // 0=NotStarted, 1=InProgress, 2=Completed

            try
            {
                cGlobal.gCharacterDataBase.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS charquest (pri_key INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, quest_started INT NOT NULL, quest_pos INT NOT NULL);");
                cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM charquest WHERE charID = '{_charId}' AND quest_started = '{qId}';");
                cGlobal.gCharacterDataBase.ExecuteNonQuery($"INSERT INTO charquest (charID, quest_started, quest_pos) VALUES ('{_charId}', '{qId}', '{state}');");

                Player livePlayer = GetOnlinePlayer();
                if (livePlayer != null)
                {
                    if (livePlayer.Quests == null) livePlayer.Quests = new Dictionary<uint, PlayerQuest>();
                    livePlayer.Quests[qId] = new PlayerQuest(qId, (QuestState)state);
                    QuestManager.SendQuestUpdate(livePlayer, qId, (QuestState)state, 1);
                }

                RefreshQuests();
                RefreshNpcVisibility();
                MessageBox.Show($"Quest #{qId} saved as {(QuestState)state}!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding quest: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCompleteQuest_Click(object sender, EventArgs e)
        {
            if (dgvQuests.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a quest row to complete.");
                return;
            }

            uint qId = Convert.ToUInt32(dgvQuests.SelectedRows[0].Cells["QuestID"].Value);
            try
            {
                cGlobal.gCharacterDataBase.ExecuteNonQuery($"UPDATE charquest SET quest_pos = 2 WHERE charID = '{_charId}' AND quest_started = '{qId}';");

                Player livePlayer = GetOnlinePlayer();
                if (livePlayer != null)
                {
                    if (livePlayer.Quests != null && livePlayer.Quests.TryGetValue(qId, out var pq))
                    {
                        pq.State = QuestState.Completed;
                        pq.CompletedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        if (livePlayer.Quests == null) livePlayer.Quests = new Dictionary<uint, PlayerQuest>();
                        livePlayer.Quests[qId] = new PlayerQuest(qId, QuestState.Completed) { CompletedAt = DateTime.UtcNow };
                    }
                    QuestManager.SendQuestUpdate(livePlayer, qId, QuestState.Completed, 1);
                }

                RefreshQuests();
                RefreshNpcVisibility();
                MessageBox.Show($"Quest #{qId} marked as Completed!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error completing quest: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAdvanceStep_Click(object sender, EventArgs e)
        {
            if (dgvQuests.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a quest row to advance.");
                return;
            }

            uint qId = Convert.ToUInt32(dgvQuests.SelectedRows[0].Cells["QuestID"].Value);
            try
            {
                cGlobal.gCharacterDataBase.ExecuteNonQuery($"UPDATE charquest SET quest_pos = 1 WHERE charID = '{_charId}' AND quest_started = '{qId}';");

                Player livePlayer = GetOnlinePlayer();
                if (livePlayer != null)
                {
                    if (livePlayer.Quests != null && livePlayer.Quests.TryGetValue(qId, out var pq))
                    {
                        pq.State = QuestState.InProgress;
                    }
                    QuestManager.SendQuestUpdate(livePlayer, qId, QuestState.InProgress, 1);
                }

                RefreshQuests();
                MessageBox.Show($"Quest #{qId} set to InProgress!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error advancing quest: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeleteQuest_Click(object sender, EventArgs e)
        {
            if (dgvQuests.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a quest row to delete.");
                return;
            }

            uint qId = Convert.ToUInt32(dgvQuests.SelectedRows[0].Cells["QuestID"].Value);
            if (MessageBox.Show($"Delete Quest #{qId} from character?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM charquest WHERE charID = '{_charId}' AND quest_started = '{qId}';");

                    Player livePlayer = GetOnlinePlayer();
                    if (livePlayer != null && livePlayer.Quests != null)
                    {
                        livePlayer.Quests.Remove(qId);
                        QuestManager.SendQuestUpdate(livePlayer, qId, QuestState.NotStarted, 0);
                    }

                    RefreshQuests();
                    RefreshNpcVisibility();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting quest: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnRefreshQuests_Click(object sender, EventArgs e)
        {
            RefreshQuests();
        }

        private void btnSyncLiveQuests_Click(object sender, EventArgs e)
        {
            Player livePlayer = GetOnlinePlayer();
            if (livePlayer == null)
            {
                MessageBox.Show("Player is currently offline.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            QuestManager.LoadPlayerQuests(livePlayer);
            QuestManager.SendAllQuestFlags(livePlayer);
            MessageBox.Show($"Synchronized {livePlayer.Quests?.Count ?? 0} quests to live player {livePlayer.CharName}!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        #endregion

        #region 2. Pets Tab
        private class PetPresetItem
        {
            public uint PetID { get; set; }
            public string DisplayName { get; set; }
            public PetPresetItem(uint id, string name) { PetID = id; DisplayName = name; }
            public override string ToString() => DisplayName;
        }

        private void RefreshPets()
        {
            try
            {
                var dt = cGlobal.gCharacterDataBase.GetDataTable($"SELECT slot, petID, petName, level, hp, maxHp, sp, maxSp, amity, isBattle, isRide FROM character_pets WHERE charID = '{_charId}' ORDER BY slot");
                dgvPets.DataSource = dt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading pets: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddPetPreset_Click(object sender, EventArgs e)
        {
            if (cmbPetPresets.SelectedItem is PetPresetItem preset)
            {
                try
                {
                    cGlobal.gCharacterDataBase.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS character_pets (id INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, slot TINYINT NOT NULL, petID INT NOT NULL, petName TEXT, level TINYINT DEFAULT 1, hp INT DEFAULT 250, maxHp INT DEFAULT 250, sp INT DEFAULT 100, maxSp INT DEFAULT 100, amity TINYINT DEFAULT 60, isBattle TINYINT DEFAULT 1, isRide TINYINT DEFAULT 0);");

                    // Find next free slot (1-4)
                    var dt = cGlobal.gCharacterDataBase.GetDataTable($"SELECT slot FROM character_pets WHERE charID = '{_charId}'");
                    byte freeSlot = 1;
                    HashSet<byte> usedSlots = new HashSet<byte>();
                    if (dt != null)
                    {
                        foreach (DataRow r in dt.Rows) usedSlots.Add(Convert.ToByte(r["slot"]));
                    }
                    while (usedSlots.Contains(freeSlot) && freeSlot <= 4) freeSlot++;

                    if (freeSlot > 4)
                    {
                        MessageBox.Show("Maximum companion slots (4) reached for this character!", "Limit Reached", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    string cleanName = preset.DisplayName.Split('-')[0].Trim();
                    cGlobal.gCharacterDataBase.ExecuteNonQuery($"INSERT INTO character_pets (charID, slot, petID, petName, level, hp, maxHp, sp, maxSp, amity, isBattle, isRide) " +
                        $"VALUES ('{_charId}', '{freeSlot}', '{preset.PetID}', '{cleanName}', 1, 250, 250, 100, 100, 60, 1, 0);");

                    Player livePlayer = GetOnlinePlayer();
                    if (livePlayer != null)
                    {
                        QuestManager.SendCompanionReward(livePlayer, preset.PetID, cleanName, setBattle: true);
                    }

                    RefreshPets();
                    RefreshNpcVisibility();
                    MessageBox.Show($"Added companion '{cleanName}' to Slot {freeSlot}!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error adding companion: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnMaxAmityHeal_Click(object sender, EventArgs e)
        {
            if (dgvPets.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a companion row.");
                return;
            }

            byte slot = Convert.ToByte(dgvPets.SelectedRows[0].Cells["slot"].Value);
            try
            {
                cGlobal.gCharacterDataBase.ExecuteNonQuery($"UPDATE character_pets SET amity = 100, hp = maxHp WHERE charID = '{_charId}' AND slot = '{slot}';");

                Player livePlayer = GetOnlinePlayer();
                if (livePlayer != null && livePlayer.PlayerPets != null && livePlayer.PlayerPets.TryGetValue(slot, out var petData))
                {
                    petData.Amity = 100;
                    petData.HP = petData.MaxHP;
                    SendPacket p = QuestManager.CreatePetPacket(livePlayer, petData.PetID, slot, petData.HP, petData.MaxHP, petData.SP, petData.MaxSP, petData.Amity, petData.Level);
                    livePlayer.Send(p);
                }

                RefreshPets();
                MessageBox.Show("Companion healed and set to 100 Amity!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating pet: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeletePet_Click(object sender, EventArgs e)
        {
            if (dgvPets.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a companion row to delete.");
                return;
            }

            byte slot = Convert.ToByte(dgvPets.SelectedRows[0].Cells["slot"].Value);
            uint petId = Convert.ToUInt32(dgvPets.SelectedRows[0].Cells["petID"].Value);

            if (MessageBox.Show($"Delete companion in slot {slot}?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM character_pets WHERE charID = '{_charId}' AND slot = '{slot}';");

                    Player livePlayer = GetOnlinePlayer();
                    if (livePlayer != null && livePlayer.PlayerPets != null)
                    {
                        livePlayer.PlayerPets.Remove(slot);
                        if (livePlayer.ActivePetID == petId)
                        {
                            livePlayer.ActivePetID = 0;
                            // Clear battle companion
                            livePlayer.Send(Tools.FromFormat("bbd", 19, 1, 0));
                            livePlayer.CurMap?.Broadcast(Tools.FromFormat("bbd", 19, 1, 0));
                        }
                    }

                    RefreshPets();
                    RefreshNpcVisibility();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting pet: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnSavePets_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvPets.DataSource is DataTable dt)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        byte slot = Convert.ToByte(row["slot"]);
                        uint petId = Convert.ToUInt32(row["petID"]);
                        string petName = row["petName"]?.ToString() ?? "Companion";
                        byte level = Convert.ToByte(row["level"]);
                        int hp = Convert.ToInt32(row["hp"]);
                        int maxHp = Convert.ToInt32(row["maxHp"]);
                        int sp = Convert.ToInt32(row["sp"]);
                        int maxSp = Convert.ToInt32(row["maxSp"]);
                        byte amity = Convert.ToByte(row["amity"]);
                        byte isBattle = Convert.ToByte(row["isBattle"]);
                        byte isRide = Convert.ToByte(row["isRide"]);

                        cGlobal.gCharacterDataBase.ExecuteNonQuery($"UPDATE character_pets SET petName = '{petName.Replace("'", "''")}', " +
                            $"level = {level}, hp = {hp}, maxHp = {maxHp}, sp = {sp}, maxSp = {maxSp}, amity = {amity}, isBattle = {isBattle}, isRide = {isRide} " +
                            $"WHERE charID = '{_charId}' AND slot = '{slot}';");
                    }
                    MessageBox.Show("Pet modifications saved to database successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshPets();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving pets: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRefreshPets_Click(object sender, EventArgs e)
        {
            RefreshPets();
        }

        private void btnSyncLivePets_Click(object sender, EventArgs e)
        {
            Player livePlayer = GetOnlinePlayer();
            if (livePlayer == null)
            {
                MessageBox.Show("Player is currently offline.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Reload pets from DB into player
            var petTable = cGlobal.gCharacterDataBase.GetDataTable($"SELECT * FROM character_pets WHERE charID = '{_charId}'");
            livePlayer.PlayerPets.Clear();
            if (petTable != null)
            {
                foreach (DataRow row in petTable.Rows)
                {
                    byte slot = byte.Parse(row["slot"].ToString());
                    uint petId = uint.Parse(row["petID"].ToString());
                    string petName = row["petName"]?.ToString() ?? "Companion";
                    byte lvl = byte.Parse(row["level"].ToString());
                    int hp = int.Parse(row["hp"].ToString());
                    int maxHp = int.Parse(row["maxHp"].ToString());
                    int sp = int.Parse(row["sp"].ToString());
                    int maxSp = int.Parse(row["maxSp"].ToString());
                    byte amity = byte.Parse(row["amity"].ToString());
                    bool isBattle = row["isBattle"].ToString() == "1";
                    bool isRide = row["isRide"].ToString() == "1";

                    livePlayer.PlayerPets[slot] = new Player.PlayerPetData()
                    {
                        Slot = slot, PetID = petId, PetName = petName, Level = lvl,
                        HP = hp, MaxHP = maxHp, SP = sp, MaxSP = maxSp, Amity = amity,
                        IsBattle = isBattle, IsRide = isRide
                    };

                    SendPacket p = QuestManager.CreatePetPacket(livePlayer, petId, slot, hp, maxHp, sp, maxSp, amity, lvl);
                    livePlayer.Send(p);

                    if (isBattle)
                    {
                        livePlayer.ActivePetID = petId;
                        livePlayer.Send(Tools.FromFormat("bbd", 19, 1, petId));
                        SendPacket followPkt = new SendPacket();
                        followPkt.Pack8(19); followPkt.Pack8(4);
                        followPkt.Pack32(livePlayer.CharID); followPkt.Pack32(petId);
                        livePlayer.Send(followPkt);
                        livePlayer.CurMap?.Broadcast(followPkt);
                    }
                }
            }

            MessageBox.Show($"Synchronized pets to live player {livePlayer.CharName}!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        #endregion

        #region 3. Map NPC & Prop Visibility Tab
        private void PopulateMapSelector()
        {
            cmbMapSelector.Items.Clear();
            try
            {
                // Try reading character's current location map
                var charDt = cGlobal.gCharacterDataBase.GetDataTable($"SELECT location_map FROM characters WHERE charID = '{_charId}'");
                if (charDt != null && charDt.Rows.Count > 0)
                {
                    _currentMapId = Convert.ToUInt16(charDt.Rows[0]["location_map"]);
                }

                // Add common maps
                cmbMapSelector.Items.Add(new MapItem(10035, "10035 - Rhodes Island (Beach)"));
                cmbMapSelector.Items.Add(new MapItem(10036, "10036 - Rhodes Island Cave / Shore"));
                cmbMapSelector.Items.Add(new MapItem(10017, "10017 - Ship Deck"));
                cmbMapSelector.Items.Add(new MapItem(10026, "10026 - Ship Cabin"));
                cmbMapSelector.Items.Add(new MapItem(10027, "10027 - Ship Bar"));
                cmbMapSelector.Items.Add(new MapItem(10000, "10000 - North Island"));
                cmbMapSelector.Items.Add(new MapItem(10001, "10001 - Astrologer Cave"));
                cmbMapSelector.Items.Add(new MapItem(11016, "11016 - Open World Ocean"));

                // Select current map
                for (int i = 0; i < cmbMapSelector.Items.Count; i++)
                {
                    if (cmbMapSelector.Items[i] is MapItem mi && mi.MapID == _currentMapId)
                    {
                        cmbMapSelector.SelectedIndex = i;
                        return;
                    }
                }
                if (cmbMapSelector.Items.Count > 0) cmbMapSelector.SelectedIndex = 0;
            }
            catch { }
        }

        private class MapItem
        {
            public ushort MapID { get; set; }
            public string Name { get; set; }
            public MapItem(ushort id, string name) { MapID = id; Name = name; }
            public override string ToString() => Name;
        }

        private void cmbMapSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbMapSelector.SelectedItem is MapItem mi)
            {
                _currentMapId = mi.MapID;
                RefreshNpcVisibility();
            }
        }

        private void RefreshNpcVisibility()
        {
            try
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("ClickID", typeof(ushort));
                dt.Columns.Add("Name", typeof(string));
                dt.Columns.Add("NPC_ID", typeof(uint));
                dt.Columns.Add("Pos", typeof(string));
                dt.Columns.Add("Type", typeof(string));
                dt.Columns.Add("State", typeof(string));
                dt.Columns.Add("LinkedQuestID", typeof(uint));

                var eveData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(_currentMapId);
                var charQuestsDt = cGlobal.gCharacterDataBase.GetDataTable($"SELECT quest_started, quest_pos FROM charquest WHERE charID = '{_charId}'");
                HashSet<uint> completedQuests = new HashSet<uint>();
                if (charQuestsDt != null)
                {
                    foreach (DataRow r in charQuestsDt.Rows)
                    {
                        if (Convert.ToByte(r["quest_pos"]) == 2)
                        {
                            completedQuests.Add(Convert.ToUInt32(r["quest_started"]));
                        }
                    }
                }

                // Check character pets for recruited companions
                var petsDt = cGlobal.gCharacterDataBase.GetDataTable($"SELECT petID FROM character_pets WHERE charID = '{_charId}'");
                HashSet<uint> ownedPets = new HashSet<uint>();
                if (petsDt != null)
                {
                    foreach (DataRow r in petsDt.Rows) ownedPets.Add(Convert.ToUInt32(r["petID"]));
                }

                Player livePlayer = GetOnlinePlayer();

                if (eveData != null && eveData.Npclist != null)
                {
                    foreach (var npc in eveData.Npclist.OrderBy(n => n.clickId))
                    {
                        string npcName = npc.Name?.Trim('\0', ' ') ?? "Unknown";
                        uint nId = npc.npcId;
                        string pos = $"({npc.x}, {npc.y})";
                        string type = "Normal NPC";
                        string state = "Visible (0x0000)";
                        uint linkedQuest = 0;

                        // Check eve events
                        var ev = eveData.Events?.FirstOrDefault(e => e.clickID == npc.clickId);
                        if (ev != null && ev.SubEntry != null)
                        {
                            bool isChest = ev.SubEntry.Any(s => s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 2 && o.dialog2 == 5));
                            bool isGathering = (nId == 19039 || npcName.ToLower().Contains("coconut") || npcName.ToLower().Contains("wood"));

                            if (isChest)
                            {
                                type = "Chest / Prop";
                                foreach (var s in ev.SubEntry)
                                {
                                    if (s.unknownword1 > 0)
                                    {
                                        linkedQuest = s.unknownword1;
                                        if (completedQuests.Contains(s.unknownword1))
                                        {
                                            state = "Broken / Opened (0x0001)";
                                            break;
                                        }
                                    }
                                }
                            }
                            else if (isGathering)
                            {
                                type = "Gathering Node (Respawnable)";
                            }
                            else if (livePlayer != null && livePlayer.HasRecruitedCompanion(npcName, (ushort)nId))
                            {
                                type = $"Companion NPC ({npcName})";
                                state = "Hidden / Recruited (0xFFFF)";
                            }
                            else if (_currentMapId == 10035 && npc.clickId == 1) // Robinson
                            {
                                type = "Companion NPC (Robinson)";
                                linkedQuest = 12040;
                                if (ownedPets.Contains(12178) || ownedPets.Contains(12032))
                                {
                                    state = "Hidden / Recruited (0xFFFF)";
                                }
                            }
                        }

                        dt.Rows.Add(npc.clickId, npcName, nId, pos, type, state, linkedQuest);
                    }
                }

                dgvNpcVisibility.DataSource = dt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading NPC visibility: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnOpenBreakChest_Click(object sender, EventArgs e)
        {
            if (dgvNpcVisibility.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an NPC/chest row.");
                return;
            }

            ushort clickId = Convert.ToUInt16(dgvNpcVisibility.SelectedRows[0].Cells["ClickID"].Value);
            uint linkedQuest = Convert.ToUInt32(dgvNpcVisibility.SelectedRows[0].Cells["LinkedQuestID"].Value);

            if (linkedQuest > 0)
            {
                cGlobal.gCharacterDataBase.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS charquest (pri_key INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, quest_started INT NOT NULL, quest_pos INT NOT NULL);");
                cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM charquest WHERE charID = '{_charId}' AND quest_started = '{linkedQuest}';");
                cGlobal.gCharacterDataBase.ExecuteNonQuery($"INSERT INTO charquest (charID, quest_started, quest_pos) VALUES ('{_charId}', '{linkedQuest}', 2);");
            }

            Player livePlayer = GetOnlinePlayer();
            if (livePlayer != null && livePlayer.CurMap?.MapID == _currentMapId)
            {
                SendPacket breakPkt = Tools.FromFormat("bbwb", 22, 1, clickId, (byte)1);
                livePlayer.Send(breakPkt);
                livePlayer.CurMap.Broadcast(breakPkt);
            }

            RefreshNpcVisibility();
            RefreshQuests();
            MessageBox.Show($"Chest ClickID {clickId} marked as Broken/Opened!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnRestoreCloseChest_Click(object sender, EventArgs e)
        {
            if (dgvNpcVisibility.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an NPC/chest row.");
                return;
            }

            ushort clickId = Convert.ToUInt16(dgvNpcVisibility.SelectedRows[0].Cells["ClickID"].Value);
            uint linkedQuest = Convert.ToUInt32(dgvNpcVisibility.SelectedRows[0].Cells["LinkedQuestID"].Value);

            if (linkedQuest > 0)
            {
                cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM charquest WHERE charID = '{_charId}' AND quest_started = '{linkedQuest}';");
            }

            Player livePlayer = GetOnlinePlayer();
            if (livePlayer != null && livePlayer.CurMap?.MapID == _currentMapId)
            {
                SendPacket restorePkt = Tools.FromFormat("bbwb", 22, 1, clickId, (byte)0);
                livePlayer.Send(restorePkt);
                livePlayer.CurMap.Broadcast(restorePkt);
            }

            RefreshNpcVisibility();
            RefreshQuests();
            MessageBox.Show($"Chest ClickID {clickId} restored/reset to Unbroken!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnHideDespawnNpc_Click(object sender, EventArgs e)
        {
            if (dgvNpcVisibility.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an NPC row.");
                return;
            }

            ushort clickId = Convert.ToUInt16(dgvNpcVisibility.SelectedRows[0].Cells["ClickID"].Value);
            Player livePlayer = GetOnlinePlayer();
            if (livePlayer != null && livePlayer.CurMap?.MapID == _currentMapId)
            {
                SendPacket hidePkt = Tools.FromFormat("bbwbb", 22, 10, clickId, (byte)0xFF, (byte)0xFF);
                livePlayer.Send(hidePkt);
                livePlayer.CurMap.Broadcast(hidePkt);
                MessageBox.Show($"Dispatched AC 22:10 hide packet for ClickID {clickId} to live player!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Player is not currently online on Map {_currentMapId}.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnShowUnhideNpc_Click(object sender, EventArgs e)
        {
            if (dgvNpcVisibility.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an NPC row.");
                return;
            }

            ushort clickId = Convert.ToUInt16(dgvNpcVisibility.SelectedRows[0].Cells["ClickID"].Value);
            Player livePlayer = GetOnlinePlayer();
            if (livePlayer != null && livePlayer.CurMap?.MapID == _currentMapId)
            {
                SendPacket unhidePkt = Tools.FromFormat("bbwbb", 22, 10, clickId, (byte)0, (byte)0);
                livePlayer.Send(unhidePkt);
                livePlayer.CurMap.Broadcast(unhidePkt);
                MessageBox.Show($"Dispatched AC 22:10 unhide/respawn packet for ClickID {clickId} to live player!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Player is not currently online on Map {_currentMapId}.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnBroadcastNpcLive_Click(object sender, EventArgs e)
        {
            btnHideDespawnNpc_Click(sender, e);
        }

        private void btnRefreshNpcs_Click(object sender, EventArgs e)
        {
            RefreshNpcVisibility();
        }
        #endregion

        #region 4. Inventory Tab
        private void RefreshInventory()
        {
            try
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("InvIdx", typeof(int));
                dt.Columns.Add("StorID", typeof(byte));
                dt.Columns.Add("ItemID", typeof(ushort));
                dt.Columns.Add("ItemName", typeof(string));
                dt.Columns.Add("Qty", typeof(ushort));
                dt.Columns.Add("Damage", typeof(byte));
                dt.Columns.Add("Pos", typeof(byte));

                var dbInv = cGlobal.gCharacterDataBase.GetDataTable($"SELECT * FROM inventory WHERE charID = '{_charId}' ORDER BY storID, pos");
                if (dbInv != null)
                {
                    foreach (DataRow r in dbInv.Rows)
                    {
                        int idx = Convert.ToInt32(r["invIdx"]);
                        byte stor = Convert.ToByte(r["storID"]);
                        ushort iId = Convert.ToUInt16(r["itemID"]);
                        ushort qty = Convert.ToUInt16(r["qty"]);
                        byte dmg = Convert.ToByte(r["dmg"]);
                        byte pos = Convert.ToByte(r["pos"]);

                        string iName = "Unknown Item";
                        try
                        {
                            var itemDat = cGlobal.ItemDatManager?.GetItemByID(iId);
                            if (itemDat != null && itemDat.ItemName != null && itemDat.ItemName.Length > 0)
                            {
                                iName = Encoding.Default.GetString(itemDat.ItemName).Trim('\0', ' ');
                            }
                        }
                        catch { }

                        dt.Rows.Add(idx, stor, iId, iName, qty, dmg, pos);
                    }
                }
                dgvInventory.DataSource = dt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading inventory: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddItem_Click(object sender, EventArgs e)
        {
            ushort itemId = (ushort)numItemId.Value;
            ushort qty = (ushort)numItemQty.Value;

            try
            {
                Player livePlayer = GetOnlinePlayer();
                if (livePlayer != null && livePlayer.Inv != null)
                {
                    livePlayer.Inv.AddItem(itemId, (byte)qty);
                    MessageBox.Show($"Added {qty}x Item #{itemId} directly to live player!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // Offline insert
                    var maxIdxDt = cGlobal.gCharacterDataBase.GetDataTable($"SELECT MAX(invIdx) as maxIdx FROM inventory WHERE charID = '{_charId}'");
                    int nextIdx = 1;
                    if (maxIdxDt != null && maxIdxDt.Rows.Count > 0 && maxIdxDt.Rows[0]["maxIdx"] != DBNull.Value)
                    {
                        nextIdx = Convert.ToInt32(maxIdxDt.Rows[0]["maxIdx"]) + 1;
                    }

                    cGlobal.gCharacterDataBase.ExecuteNonQuery($"INSERT INTO inventory (invIdx, charID, storID, itemID, dmg, qty, pos, socketID, bombID, sewID, forge) " +
                        $"VALUES ('{nextIdx}', '{_charId}', '0', '{itemId}', '0', '{qty}', '0', '0', '0', '0', '0');");
                    MessageBox.Show($"Added {qty}x Item #{itemId} to database inventory!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                RefreshInventory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeleteItem_Click(object sender, EventArgs e)
        {
            if (dgvInventory.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an item row to delete.");
                return;
            }

            int invIdx = Convert.ToInt32(dgvInventory.SelectedRows[0].Cells["InvIdx"].Value);
            byte stor = Convert.ToByte(dgvInventory.SelectedRows[0].Cells["StorID"].Value);
            byte pos = Convert.ToByte(dgvInventory.SelectedRows[0].Cells["Pos"].Value);

            if (MessageBox.Show("Delete selected item?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM inventory WHERE charID = '{_charId}' AND invIdx = '{invIdx}';");
                    Player livePlayer = GetOnlinePlayer();
                    if (livePlayer != null && livePlayer.Inv != null && stor == 0)
                    {
                        livePlayer.Inv.RemoveItemAtSlot(pos, 1);
                    }
                    RefreshInventory();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnRepairItem_Click(object sender, EventArgs e)
        {
            if (dgvInventory.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an item row to repair.");
                return;
            }

            int invIdx = Convert.ToInt32(dgvInventory.SelectedRows[0].Cells["InvIdx"].Value);
            try
            {
                cGlobal.gCharacterDataBase.ExecuteNonQuery($"UPDATE inventory SET dmg = 0 WHERE charID = '{_charId}' AND invIdx = '{invIdx}';");
                RefreshInventory();
                MessageBox.Show("Item repaired to 100% durability!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error repairing item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRefreshInventory_Click(object sender, EventArgs e)
        {
            RefreshInventory();
        }
        #endregion

        #region 5. Event & NPC Unlocks (charunlocks) Tab
        private void RefreshSkills()
        {
            try
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("PriKey", typeof(int));
                dt.Columns.Add("MapID", typeof(ushort));
                dt.Columns.Add("ClickID", typeof(ushort));
                dt.Columns.Add("TargetInfo", typeof(string));

                cGlobal.gCharacterDataBase.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS charunlocks (pri_key INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, maploc INT NOT NULL, clickID INT NOT NULL);");
                var dbUnlocks = cGlobal.gCharacterDataBase.GetDataTable($"SELECT * FROM charunlocks WHERE charID = '{_charId}' ORDER BY maploc, clickID");
                if (dbUnlocks != null)
                {
                    foreach (DataRow r in dbUnlocks.Rows)
                    {
                        int priKey = Convert.ToInt32(r["pri_key"]);
                        ushort mapLoc = Convert.ToUInt16(r["maploc"]);
                        ushort clickId = Convert.ToUInt16(r["clickID"]);

                        string info = $"Map {mapLoc} ClickID {clickId}";
                        try
                        {
                            var eveMap = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(mapLoc);
                            var npc = eveMap?.Npclist?.FirstOrDefault(n => n.clickId == clickId);
                            if (npc != null && !string.IsNullOrWhiteSpace(npc.Name))
                            {
                                info = $"Map {mapLoc} - NPC '{npc.Name.Trim('\0', ' ')}' (ClickID {clickId})";
                            }
                        }
                        catch { }

                        dt.Rows.Add(priKey, mapLoc, clickId, info);
                    }
                }
                dgvSkills.DataSource = dt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading event unlocks: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddSkill_Click(object sender, EventArgs e)
        {
            ushort mapLoc = (ushort)numSkillId.Value;
            ushort clickId = (ushort)numSkillGrade.Value;

            try
            {
                cGlobal.gCharacterDataBase.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS charunlocks (pri_key INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, maploc INT NOT NULL, clickID INT NOT NULL);");
                var existing = cGlobal.gCharacterDataBase.GetDataTable($"SELECT pri_key FROM charunlocks WHERE charID = '{_charId}' AND maploc = '{mapLoc}' AND clickID = '{clickId}';");
                if (existing == null || existing.Rows.Count == 0)
                {
                    cGlobal.gCharacterDataBase.ExecuteNonQuery($"INSERT INTO charunlocks (charID, maploc, clickID) VALUES ('{_charId}', '{mapLoc}', '{clickId}');");
                }

                Player livePlayer = GetOnlinePlayer();
                if (livePlayer != null && livePlayer.CurMap?.MapID == mapLoc)
                {
                    // Dispatch AC 22:10 despawn packet
                    SendPacket p = Tools.FromFormat("bbwbb", 22, 10, clickId, (byte)0xFF, (byte)0xFF);
                    livePlayer.Send(p);
                    livePlayer.CurMap.Broadcast(p);
                }

                RefreshSkills();
                RefreshNpcVisibility();
                MessageBox.Show($"Unlock flag for Map {mapLoc}, ClickID {clickId} added successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding unlock flag: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeleteSkill_Click(object sender, EventArgs e)
        {
            if (dgvSkills.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an unlock row to revoke.");
                return;
            }

            int priKey = Convert.ToInt32(dgvSkills.SelectedRows[0].Cells["PriKey"].Value);
            ushort mapLoc = Convert.ToUInt16(dgvSkills.SelectedRows[0].Cells["MapID"].Value);
            ushort clickId = Convert.ToUInt16(dgvSkills.SelectedRows[0].Cells["ClickID"].Value);

            if (MessageBox.Show($"Revoke Unlock flag for Map {mapLoc}, ClickID {clickId}?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM charunlocks WHERE charID = '{_charId}' AND pri_key = '{priKey}';");

                    Player livePlayer = GetOnlinePlayer();
                    if (livePlayer != null && livePlayer.CurMap?.MapID == mapLoc)
                    {
                        // Restore NPC visibility
                        SendPacket p = Tools.FromFormat("bbwbb", 22, 10, clickId, (byte)0, (byte)0);
                        livePlayer.Send(p);
                        livePlayer.CurMap.Broadcast(p);
                    }

                    RefreshSkills();
                    RefreshNpcVisibility();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error revoking unlock: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnRefreshSkills_Click(object sender, EventArgs e)
        {
            RefreshSkills();
        }
        #endregion

        #region 6. Learned Skills (character_skills) Tab
        private void RefreshLearnedSkills()
        {
            try
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("SkillID", typeof(uint));
                dt.Columns.Add("SkillName", typeof(string));
                dt.Columns.Add("Grade", typeof(byte));
                dt.Columns.Add("EXP", typeof(uint));

                cGlobal.gCharacterDataBase.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS character_skills (id INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, skillID INT NOT NULL, grade TINYINT DEFAULT 1, exp INT DEFAULT 0, UNIQUE(charID, skillID));");
                
                Player livePlayer = GetOnlinePlayer();
                if (livePlayer != null && livePlayer.PlayerSkills != null && livePlayer.PlayerSkills.Count > 0)
                {
                    foreach (var sk in livePlayer.PlayerSkills)
                    {
                        dt.Rows.Add(sk.SkillID, $"Skill #{sk.SkillID}", sk.Grade, sk.Exp);
                    }
                }
                else
                {
                    var dbSkills = cGlobal.gCharacterDataBase.GetDataTable($"SELECT skillID, grade, exp FROM character_skills WHERE charID = '{_charId}' ORDER BY skillID;");
                    if (dbSkills != null && dbSkills.Rows.Count > 0)
                    {
                        foreach (DataRow r in dbSkills.Rows)
                        {
                            uint sId = Convert.ToUInt32(r["skillID"]);
                            byte grade = Convert.ToByte(r["grade"]);
                            uint exp = Convert.ToUInt32(r["exp"]);
                            dt.Rows.Add(sId, $"Skill #{sId}", grade, exp);
                        }
                    }
                }
                dgvLearnedSkills.DataSource = dt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading learned skills: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddLearnedSkill_Click(object sender, EventArgs e)
        {
            uint skillId = (uint)numLearnedSkillId.Value;
            byte grade = (byte)numLearnedSkillGrade.Value;
            uint exp = (uint)numLearnedSkillExp.Value;

            try
            {
                cGlobal.gCharacterDataBase.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS character_skills (id INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, skillID INT NOT NULL, grade TINYINT DEFAULT 1, exp INT DEFAULT 0, UNIQUE(charID, skillID));");
                cGlobal.gCharacterDataBase.ExecuteNonQuery($"INSERT INTO character_skills (charID, skillID, grade, exp) VALUES ('{_charId}', '{skillId}', '{grade}', '{exp}') ON CONFLICT(charID, skillID) DO UPDATE SET grade = '{grade}', exp = '{exp}';");

                Player livePlayer = GetOnlinePlayer();
                if (livePlayer != null)
                {
                    Game.SkillRelated.SkillManager.UnlockSkill(livePlayer, skillId, grade, exp);
                }

                RefreshLearnedSkills();
                MessageBox.Show($"Skill #{skillId} (Grade {grade}, EXP {exp}) saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving learned skill: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeleteLearnedSkill_Click(object sender, EventArgs e)
        {
            if (dgvLearnedSkills.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a skill row to forget.");
                return;
            }

            uint skillId = Convert.ToUInt32(dgvLearnedSkills.SelectedRows[0].Cells["SkillID"].Value);
            if (MessageBox.Show($"Forget Skill #{skillId} for Character ID {_charId}?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM character_skills WHERE charID = '{_charId}' AND skillID = '{skillId}';");

                    Player livePlayer = GetOnlinePlayer();
                    if (livePlayer != null && livePlayer.PlayerSkills != null)
                    {
                        var sk = livePlayer.PlayerSkills.FirstOrDefault(s => s.SkillID == skillId);
                        if (sk != null)
                        {
                            livePlayer.PlayerSkills.Remove(sk);
                            Game.SkillRelated.SkillManager.SendSkillSlots(livePlayer);
                        }
                    }

                    RefreshLearnedSkills();
                    MessageBox.Show($"Skill #{skillId} removed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing skill: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnRefreshLearnedSkills_Click(object sender, EventArgs e)
        {
            RefreshLearnedSkills();
        }
        #endregion

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
