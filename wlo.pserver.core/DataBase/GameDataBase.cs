using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Data;
using Game;
using RCLibrary.Core;

using Game.DataFiles; // Added namespace

namespace DataBase
{
    public class NpcTemplateInfo
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public int HP { get; set; }
        public int Element { get; set; }

        public NpcTemplateInfo(string name, int level, int hp, int element)
        {
            Name = name;
            Level = level;
            HP = hp;
            Element = element;
        }
    }

    public class GameDataBase : RCLibrary.Core.DataBase
    {
        const string DBServer = "GameDataBase";
        //DBConnector.DBOAuth DBAssist;

        public global::DataFiles.PhxItemDat ItemDat { private get; set; }
        public Game.DataFiles.EveManager EveDat { get; set; } // Added Property
        public global::DataFiles.PhxNpcDat NpcDat { get; set; } // Added Property for Npc.dat
        public global::DataFiles.PhxTalkDat TalkDat { get; set; } // Added Property for Talk.dat
        public global::DataFiles.PhxMarkDat MarkDat { get; set; } // Added Property for Mark.dat

        public static GameDataBase GlobalInstance;

        public GameDataBase()
        {
            GlobalInstance = this;
            EveDat = new Game.DataFiles.EveManager(); // Initialize
            NpcDat = new global::DataFiles.PhxNpcDat(); // Initialize
            //DBAssist = new DBConnector.DBOAuth();
        }

        public void VerifySetup()
        {
            // Create Friends table if it doesn't exist
            try
            {
                var testQuery = GetDataTable("SELECT * FROM Friends LIMIT 1");
            }
            catch
            {
                DebugSystem.Write("[GameDataBase] Creating Friends table...");
                string createFriends = @"
                    CREATE TABLE IF NOT EXISTS Friends (
                        CharID1 INTEGER NOT NULL,
                        CharID2 INTEGER NOT NULL,
                        AddedDate TEXT NOT NULL,
                        PRIMARY KEY (CharID1, CharID2)
                    )";
                try
                {
                    ExecuteNonQuery(createFriends);
                    DebugSystem.Write("[GameDataBase] Friends table created successfully");
                }
                catch (Exception ex)
                {
                    DebugSystem.Write("[GameDataBase] Failed to create Friends table: " + ex.Message);
                }
            }

            // Create NPCs Spawns table
            try
            {
                // Drop if exists to ensure schema update (User requested this previously for data)
                ExecuteNonQuery("DROP TABLE IF EXISTS npcs");

                var query = @"CREATE TABLE IF NOT EXISTS npcs (
                                npc_id INTEGER PRIMARY KEY AUTOINCREMENT,
                                map_id INT NOT NULL, 
                                click_id INT NOT NULL, 
                                template_id INT DEFAULT 0,
                                npc_type VARCHAR(50), 
                                npc_name VARCHAR(100), 
                                x INT, 
                                y INT
                            )";
                ExecuteNonQuery(query);

                // Auto-Import Spawns from CSV
                string spawnCsv = System.AppDomain.CurrentDomain.BaseDirectory + "listdata\\spawns.csv";
                LoadSpawnsFromCsv(spawnCsv);
            }
            catch (Exception ex) { DebugSystem.Write($"[GameDataBase] Error setup npcs table: {ex.Message}"); }

            // Create NPC Templates table
            VerifyNpcDataSetup();

            // Initialize and synchronize Quests database table
            QuestDataBase.Initialize(this);
        }

        public void LoadSpawnsFromCsv(string path)
        {
            if (!System.IO.File.Exists(path)) return;
            try
            {
                var lines = System.IO.File.ReadAllLines(path);
                int count = 0;
                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length >= 5)
                    {
                        // Format: MapID, ClickID, TemplateID, X, Y
                        int mapId = int.Parse(parts[0]);
                        int clickId = int.Parse(parts[1]);
                        int templateId = int.Parse(parts[2]);
                        int x = int.Parse(parts[3]);
                        int y = int.Parse(parts[4]);
                        string name = "Unknown"; // Will be updated from Template

                        // Fetch Name from Template if possible
                        var tpl = GetDataTable($"SELECT name FROM npc_data WHERE id={templateId} LIMIT 1");
                        if (tpl != null && tpl.Rows.Count > 0) name = tpl.Rows[0]["name"].ToString();

                        string query = $"INSERT OR REPLACE INTO npcs (map_id, click_id, template_id, npc_name, x, y, npc_type) VALUES ({mapId}, {clickId}, {templateId}, '{name}', {x}, {y}, 'QuestNpc')";
                        ExecuteNonQuery(query);
                        count++;
                    }
                }
                DebugSystem.Write($"[GameDataBase] Imported {count} Spawns from spawns.csv");
            }
            catch (Exception ex) { DebugSystem.Write($"[GameDataBase] Error loading spawns.csv: {ex.Message}"); }
        }

        public void VerifyNpcDataSetup_Stub()
        {
            // Stub to match previous structure replacement target if needed, but VerifyNpcDataSetup is outside block
            // Actually, I am replacing the entire 'VerifySetup' end block where it calls VerifyNpcDataSetup
            // The original code had:
            // Create NPCs Spawns table ... catch ... 
            // VerifyNpcDataSetup();
            // Create NPCs table if it doesn't exist ... catch ...
        }
        public void LoadFinalData(Player c)
        {
            DataTable src = null;

            #region Inventory
            try
            {
                src = GetDataTable("SELECT * FROM inventory where charID = '" + c.CharID + "'");

                if (src != null && src.Rows.Count > 0)
                {
                    for (int i = 0; i < src.Rows.Count; i++)
                    {
                        try
                        {
                            ushort id = ushort.Parse(src.Rows[i]["itemID"].ToString());
                            if (id == 0) continue;

                            uint storId = uint.Parse(src.Rows[i]["storID"].ToString());
                            byte pos = byte.Parse(src.Rows[i]["pos"].ToString());
                            byte qty = byte.Parse(src.Rows[i]["qty"].ToString());
                            byte dmg = byte.Parse(src.Rows[i]["dmg"].ToString());

                            var baseItem = ItemDat?.GetItemByID(id) ?? new DataFiles.PhxItemInfo() { ItemID = id, ItemName = Encoding.ASCII.GetBytes("Item " + id) };

                            switch (storId)
                            {
                                case 0: // Bag inventory
                                    if (pos >= 1 && pos <= 50)
                                    {
                                        Game.Code.InvItem data = new Game.Code.InvItem();
                                        data.CopyFrom(baseItem);
                                        data.Ammt = Math.Max((byte)1, qty);
                                        data.Damage = dmg;
                                        c.Inv[pos].CopyFrom(data);
                                    }
                                    break;
                                case 1: // Equips
                                    if (pos >= 1 && pos <= 6)
                                    {
                                        c[pos].CopyFrom(baseItem);
                                        c[pos].Ammt = 1;
                                        c[pos].Damage = dmg;
                                    }
                                    break;
                            }
                        }
                        catch (Exception itemEx)
                        {
                            DebugSystem.Write($"[GameDataBase] Error loading inventory row {i}: {itemEx.Message}");
                        }
                    }
                    DebugSystem.Write($"[GameDataBase] Loaded {src.Rows.Count} inventory/equip records for {c.CharName}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error loading inventory for {c.CharName}: {ex.Message}");
            }
            finally
            {
                src = null;
            }
            #endregion

            #region Tent
            #endregion

            #region Friends
            src = GetDataTable("SELECT Friends FROM charactersextdata where charID = '" + c.CharID + "'");
            if (src != null && src.Rows.Count > 0 && src.Rows[0]["Friends"] != DBNull.Value)
            {
                c.LoadFriends(src.Rows[0]["Friends"].ToString());
            }
            src = null;
            #endregion

            #region Mail
            src = GetDataTable("SELECT * FROM charactersextdata where charID = '" + c.CharID + "'");

            if (src.Rows.Count > 0)
            {
                //foreach (string m in src.Rows[0]["Mail"].ToString().Split('&'))
                //{
                //    if (m == "none") break;
                //    Mail tmp = new Mail();
                //    tmp.Load(m);
                //    var re = cGlobal.WLO_World.GetPlayer(tmp.targetid);
                //    if (tmp.type == "Send")
                //    {
                //        if (!tmp.isSent && re != null)
                //        {
                //            c.MailBox.Add(tmp);
                //            re.RecvMailfrom(c, tmp.message, tmp.when);
                //        }
                //        else
                //            c.MailBox.Add(tmp);
                //    }
                //    else
                //        c.MailBox.Add(tmp);
                //}
            }
            #endregion

            #region Skills

            #endregion

            #region Settings
            src = GetDataTable("SELECT * FROM charactersextdata where charID = '" + c.CharID + "'");

            if (src.Rows.Count > 0)
                c.Settings.Load(src.Rows[0]["Settings"].ToString());

            src = null;
            #endregion

            #region Pets
            try
            {
                // Ensure character_pets table exists with comprehensive stat columns
                ExecuteNonQuery("CREATE TABLE IF NOT EXISTS character_pets (id INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, slot TINYINT NOT NULL, petID INT NOT NULL, petName TEXT, level TINYINT DEFAULT 1, exp INT DEFAULT 0, hp INT DEFAULT 250, maxHp INT DEFAULT 250, sp INT DEFAULT 100, maxSp INT DEFAULT 100, str INT DEFAULT 10, con INT DEFAULT 10, int_ INT DEFAULT 10, wis INT DEFAULT 10, agi INT DEFAULT 10, potential INT DEFAULT 0, skillPoints INT DEFAULT 0, amity TINYINT DEFAULT 60, isBattle TINYINT DEFAULT 1, isRide TINYINT DEFAULT 0, isHotel TINYINT DEFAULT 0, reborn TINYINT DEFAULT 0, job TINYINT DEFAULT 0, eq_head INT DEFAULT 0, eq_body INT DEFAULT 0, eq_weapon INT DEFAULT 0, eq_wrist INT DEFAULT 0, eq_shoes INT DEFAULT 0, eq_special INT DEFAULT 0);");
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN exp INT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN str INT DEFAULT 10;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN con INT DEFAULT 10;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN int_ INT DEFAULT 10;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN wis INT DEFAULT 10;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN agi INT DEFAULT 10;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN potential INT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN skillPoints INT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN isHotel TINYINT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN reborn TINYINT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN job TINYINT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN eq_head INT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN eq_body INT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN eq_weapon INT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN eq_wrist INT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN eq_shoes INT DEFAULT 0;"); } catch { }
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN eq_special INT DEFAULT 0;"); } catch { }

                var petTable = GetDataTable("SELECT * FROM character_pets WHERE charID = '" + c.CharID + "'");
                c.PlayerPets.Clear();
                c.HotelPets.Clear();
                if (petTable != null && petTable.Rows.Count > 0)
                {
                    foreach (DataRow row in petTable.Rows)
                    {
                        byte slot = byte.Parse(row["slot"].ToString());
                        uint petId = uint.Parse(row["petID"].ToString());
                        if (petId == 12178) petId = 12032;
                        string petName = row["petName"] != DBNull.Value ? row["petName"].ToString() : "Robinson";
                        if (petName.StartsWith("Companion #") || petName == "Companion") petName = "Robinson";
                        byte lvl = row.Table.Columns.Contains("level") && row["level"] != DBNull.Value ? byte.Parse(row["level"].ToString()) : (byte)1;
                        uint exp = row.Table.Columns.Contains("exp") && row["exp"] != DBNull.Value ? uint.Parse(row["exp"].ToString()) : 0;
                        int hp = row.Table.Columns.Contains("hp") && row["hp"] != DBNull.Value ? int.Parse(row["hp"].ToString()) : 250;
                        int maxHp = row.Table.Columns.Contains("maxHp") && row["maxHp"] != DBNull.Value ? int.Parse(row["maxHp"].ToString()) : 250;
                        int sp = row.Table.Columns.Contains("sp") && row["sp"] != DBNull.Value ? int.Parse(row["sp"].ToString()) : 100;
                        int maxSp = row.Table.Columns.Contains("maxSp") && row["maxSp"] != DBNull.Value ? int.Parse(row["maxSp"].ToString()) : 100;
                        ushort str = row.Table.Columns.Contains("str") && row["str"] != DBNull.Value ? ushort.Parse(row["str"].ToString()) : (ushort)10;
                        ushort con = row.Table.Columns.Contains("con") && row["con"] != DBNull.Value ? ushort.Parse(row["con"].ToString()) : (ushort)10;
                        ushort int_ = row.Table.Columns.Contains("int_") && row["int_"] != DBNull.Value ? ushort.Parse(row["int_"].ToString()) : (ushort)10;
                        ushort wis = row.Table.Columns.Contains("wis") && row["wis"] != DBNull.Value ? ushort.Parse(row["wis"].ToString()) : (ushort)10;
                        ushort agi = row.Table.Columns.Contains("agi") && row["agi"] != DBNull.Value ? ushort.Parse(row["agi"].ToString()) : (ushort)10;
                        ushort potential = row.Table.Columns.Contains("potential") && row["potential"] != DBNull.Value ? ushort.Parse(row["potential"].ToString()) : (ushort)0;
                        ushort skillPoints = row.Table.Columns.Contains("skillPoints") && row["skillPoints"] != DBNull.Value ? ushort.Parse(row["skillPoints"].ToString()) : (ushort)0;
                        byte amity = row.Table.Columns.Contains("amity") && row["amity"] != DBNull.Value ? byte.Parse(row["amity"].ToString()) : (byte)60;
                        bool isBattle = row["isBattle"].ToString() == "1";
                        bool isRide = row["isRide"].ToString() == "1";
                        bool isHotel = row.Table.Columns.Contains("isHotel") && row["isHotel"].ToString() == "1";
                        bool reborn = row.Table.Columns.Contains("reborn") && row["reborn"].ToString() == "1";
                        byte job = row.Table.Columns.Contains("job") && row["job"] != DBNull.Value ? byte.Parse(row["job"].ToString()) : (byte)0;
                        ushort eqHead = row.Table.Columns.Contains("eq_head") && row["eq_head"] != DBNull.Value ? ushort.Parse(row["eq_head"].ToString()) : (ushort)0;
                        ushort eqBody = row.Table.Columns.Contains("eq_body") && row["eq_body"] != DBNull.Value ? ushort.Parse(row["eq_body"].ToString()) : (ushort)0;
                        ushort eqWeapon = row.Table.Columns.Contains("eq_weapon") && row["eq_weapon"] != DBNull.Value ? ushort.Parse(row["eq_weapon"].ToString()) : (ushort)0;
                        ushort eqWrist = row.Table.Columns.Contains("eq_wrist") && row["eq_wrist"] != DBNull.Value ? ushort.Parse(row["eq_wrist"].ToString()) : (ushort)0;
                        ushort eqShoes = row.Table.Columns.Contains("eq_shoes") && row["eq_shoes"] != DBNull.Value ? ushort.Parse(row["eq_shoes"].ToString()) : (ushort)0;
                        ushort eqSpecial = row.Table.Columns.Contains("eq_special") && row["eq_special"] != DBNull.Value ? ushort.Parse(row["eq_special"].ToString()) : (ushort)0;

                        var petData = new Player.PlayerPetData()
                        {
                            Slot = slot,
                            PetID = petId,
                            PetName = petName,
                            Level = lvl,
                            Exp = exp,
                            HP = hp,
                            MaxHP = maxHp,
                            SP = sp,
                            MaxSP = maxSp,
                            Str = str,
                            Con = con,
                            Int = int_,
                            Wis = wis,
                            Agi = agi,
                            Potential = potential,
                            SkillPoints = skillPoints,
                            Amity = amity,
                            IsBattle = isBattle,
                            IsRide = isRide,
                            Reborn = reborn,
                            Job = job,
                            Eq_Head = eqHead,
                            Eq_Body = eqBody,
                            Eq_Weapon = eqWeapon,
                            Eq_Wrist = eqWrist,
                            Eq_Shoes = eqShoes,
                            Eq_Special = eqSpecial
                        };

                        if (isHotel)
                        {
                            c.HotelPets[slot] = petData;
                            DebugSystem.Write($"[GameDataBase] Loaded Hotel pet '{petName}' (ID: {petId}, Hotel Slot: {slot}) for {c.CharName}");
                        }
                        else
                        {
                            c.PlayerPets[slot] = petData;
                            if (isBattle)
                            {
                                c.ActivePetID = petId;
                            }
                            if (isRide)
                            {
                                c.PutPetToRide(petId.ToString());
                            }
                            DebugSystem.Write($"[GameDataBase] Loaded companion '{petName}' (ID: {petId}, Slot: {slot}, Lv.{lvl}, Battle:{isBattle}) for {c.CharName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error loading pets for {c.CharName}: {ex.Message}");
            }
            #endregion

            #region Quests
            #endregion

            #region SideBar
            #endregion

        }

        public ushort GetBattleBG(ushort map)
        {
            return 140;
        }

        public DataTable GetNPCsForMap(uint mapId)
        {
            try
            {
                string query = $"SELECT * FROM npcs WHERE map_id = {mapId}";
                var result = GetDataTable(query);
                DebugSystem.Write($"[GameDataBase] Loaded {result?.Rows.Count ?? 0} NPCs for map {mapId}");
                return result;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error loading NPCs for map {mapId}: {ex.Message}");
                return null;
            }
        }

        public DataTable GetAllNPCs()
        {
            try
            {
                string query = "SELECT * FROM npcs";
                return GetDataTable(query);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error loading all NPCs: {ex.Message}");
                return null;
            }
        }

        public bool UpdateNPC(int npcId, int mapId, int clickId, string type, string name, int x, int y, int templateId = 0)
        {
            try
            {
                string query = $"UPDATE npcs SET map_id={mapId}, click_id={clickId}, npc_type='{type}', npc_name='{name}', x={x}, y={y}, template_id={templateId} WHERE npc_id={npcId}";
                ExecuteNonQuery(query);
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error updating NPC {npcId}: {ex.Message}");
                return false;
            }
        }

        public bool AddNPC(int mapId, int clickId, string type, string name, int x, int y, int templateId = 0)
        {
            try
            {
                string query = $"INSERT INTO npcs (map_id, click_id, npc_type, npc_name, x, y, template_id) VALUES ({mapId}, {clickId}, '{type}', '{name}', {x}, {y}, {templateId})";
                ExecuteNonQuery(query);
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error adding NPC: {ex.Message}");
                return false;
            }
        }

        public void VerifyNpcDataSetup()
        {
            try
            {
                // Create npc_data table (Templates)
                string query = @"CREATE TABLE IF NOT EXISTS npc_data (
                                    id INT PRIMARY KEY, 
                                    name VARCHAR(100),
                                    level INT DEFAULT 1,
                                    hp INT DEFAULT 100,
                                    element INT DEFAULT 0
                                )";
                ExecuteNonQuery(query);

                // Auto-Import if empty
                var dt = GetDataTable("SELECT COUNT(*) as cnt FROM npc_data");
                if (dt != null && dt.Rows.Count > 0)
                {
                    long count = Convert.ToInt64(dt.Rows[0]["cnt"]);
                    if (count == 0)
                    {
                        // Priority 1: Binary Npc.dat
                        string datPath = System.AppDomain.CurrentDomain.BaseDirectory + "Data\\Npc.dat";
                        if (ImportNpcDat(datPath) == 0)
                        {
                            // Priority 2: CSV
                            string csvPath = System.AppDomain.CurrentDomain.BaseDirectory + "listdata\\npc.csv";
                            // ImportNpcDataFromCsv(csvPath); // Use new method logic for csv if needed, but Dat is preferred
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error creating npc_data table: {ex.Message}");
            }
        }

        public int ImportNpcDat(string datPath)
        {
            int count = 0;
            try
            {
                if (!System.IO.File.Exists(datPath)) return 0;
                DebugSystem.Write("[GameDataBase] Loading Npc.dat...");

                byte[] fileBytes = System.IO.File.ReadAllBytes(datPath);
                int recordSize = 138;
                int totalRecords = fileBytes.Length / recordSize;
                int debugLimit = 0;
                System.Text.StringBuilder batch = new System.Text.StringBuilder();

                for (int rec = 1; rec < totalRecords; rec++)
                {
                    int offset = rec * recordSize;
                    if (offset + recordSize > fileBytes.Length) break;

                    // 1. Read exact fixed binary fields
                    ushort rawId = (ushort)(fileBytes[offset + 12] | (fileBytes[offset + 13] << 8));
                    int id = (ushort)(((rawId ^ 0x5209) - 1) & 0xFFFF);
                    if (id == 0) continue;

                    byte rawLvl = fileBytes[offset + 37];
                    int level = (byte)((rawLvl ^ 0xC8) - 1);

                    uint rawHp = (uint)(fileBytes[offset + 38] | (fileBytes[offset + 39] << 8) | (fileBytes[offset + 40] << 16) | (fileBytes[offset + 41] << 24));
                    int hp = (int)((rawHp ^ 0x0BAEB716) - 1);

                    byte rawElem = fileBytes[offset + 57];
                    int element = (byte)((rawElem ^ 0xC8) - 1);

                    // Extract authentic name
                    var rawChars = new List<byte>();
                    for (int i = offset + 10; i >= offset + 1; i--)
                    {
                        byte b = fileBytes[i];
                        if (b != 0 && b != 0xCA && b != 0xC8 && b != 0xC9)
                        {
                            rawChars.Add(b);
                        }
                    }

                    string finalName = System.Text.Encoding.ASCII.GetString(rawChars.ToArray()).Trim();
                    if (string.IsNullOrEmpty(finalName))
                    {
                        finalName = $"NPC_{id}";
                    }

                    finalName = finalName.Replace("'", "''");

                    // Build SQL batch statement
                    batch.Append($"INSERT OR REPLACE INTO npc_data (id, name, level, hp, element) VALUES ({id}, '{finalName}', {level}, {hp}, {element});\n");
                    count++;
                }

                if (batch.Length > 0)
                {
                    ExecuteNonQuery("BEGIN TRANSACTION;\n" + batch.ToString() + "COMMIT;");
                }

                DebugSystem.Write($"[GameDataBase] Successfully Imported {count} NPCs from Npc.dat directly");
                return count;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error importing Npc.dat: {ex.Message}");
                return 0;
            }
        }


        public DataTable GetAllNpcTemplates()
        {
            return GetDataTable("SELECT * FROM npc_data");
        }

        public bool UpdateNpcTemplate(int id, string name, int level, int hp, int element)
        {
            try
            {
                string query = $"UPDATE npc_data SET name='{name}', level={level}, hp={hp}, element={element} WHERE id={id}";
                ExecuteNonQuery(query);
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error updating NPC Template {id}: {ex.Message}");
                return false;
            }
        }

        public bool AddNpcTemplate(int id, string name, int level, int hp, int element)
        {
            try
            {
                name = name.Replace("'", "''");
                string query = $"INSERT INTO npc_data (id, name, level, hp, element) VALUES ({id}, '{name}', {level}, {hp}, {element}) ON DUPLICATE KEY UPDATE name='{name}', level={level}, hp={hp}, element={element}";
                ExecuteNonQuery(query);
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error adding NPC Template: {ex.Message}");
                return false;
            }
        }

        public DataRow GetNpcTemplate(int id)
        {
            var dt = GetDataTable($"SELECT * FROM npc_data WHERE id = {id}");
            if (dt != null && dt.Rows.Count > 0)
                return dt.Rows[0];
            return null;
        }

        public NpcTemplateInfo ResolveNpcInfo(ushort mapId, byte clickId, ushort templateId)
        {
            // Resolve authentic name from SceneDataManager (direct in-memory Npc.dat engine - instant O(1))
            string authenticName = Game.DataFiles.SceneDataManager.GetNpcName(templateId);
            return new NpcTemplateInfo(authenticName, 1, 100, 0);
        }
        //{

        //}
        //public void WriteTent(Game.Code.PlayerRelated.Tent src)
        //{

        //}

        //public bool WriteStorage(ref Player src, Storagetype writetype, List<long[]> Data)
        //{
        //    if (src == null || src.ID == 0) return false;


        //    MySqlCommand cmd = null;
        //    MySqlDataReader reader = null;
        //    DataTable table = null;
        //    DataRow[] rows = new DataRow[0];
        //    MySqlConnection conn = GenerateConn();

        //    try { conn.Open(); }
        //    catch (MySqlException f) { DebugSystem.Write(f); return false; }

        //    foreach (var r in Data)
        //    {
        //        cmd = new MySqlCommand(string.Format("SELECT * FROM {0} where {1}", "inventory", "charID = '" + src.ID + " AND storID = '" + (byte)writetype + "'"), conn);

        //        try
        //        {
        //            reader = cmd.ExecuteReader();
        //            DebugSystem.Write(DBServer + cmd.CommandText + " Successful");
        //        }
        //        catch (MySqlException ex) { DebugSystem.Write(ex); }

        //        if (reader.HasRows)
        //        {
        //            cmd = new MySqlCommand(string.Format("INSERT INTO inventory (invIdx,charID,storID,itemID,dmg,qty,pos,socketID,bombID,sewID,forge) VALUES {0}", string.Format("('{0}','{1}','1','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}'),",
        //                    Data[0], src.ID, Data[1], Data[2], Data[3], Data[4], Data[5], Data[6], Data[7], Data[8])), conn);

        //            try
        //            {
        //                cmd.ExecuteNonQuery();
        //                DebugSystem.Write(DBServer + cmd.CommandText + " Successful");
        //            }
        //            catch (MySqlException ex) { DebugSystem.Write(ex); return false; }
        //        }
        //        else
        //        {
        //            cmd = new MySqlCommand(string.Format("UPDATE inventory SET {0} where {1}", string.Format("itemID = '{0}', dmg = '{1}', qty = '{2}', pos = '{3}', socketID = '{4}', bombID = '{5}', sewID = '{6}', forge = '{7}'",
        //                 Data[1], Data[2], Data[3], Data[4], Data[5], Data[6], Data[7], Data[8]), "charID ='" + src.ID + "' AND storID ='0' AND invIdx = '" + Data[0] + "'"), conn);

        //            try
        //            {
        //                cmd.ExecuteNonQuery();
        //                DebugSystem.Write(DBServer + cmd.CommandText + " Successful");
        //            }
        //            catch (MySqlException ex) { DebugSystem.Write(ex); return false; }
        //        }

        //    }
        //    return true;
        //}
    }
}
