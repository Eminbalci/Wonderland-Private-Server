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
    public class GameDataBase : RCLibrary.Core.DataBase
    {
        const string DBServer = "GameDataBase";
        //DBConnector.DBOAuth DBAssist;

        public global::DataFiles.PhxItemDat ItemDat { private get; set; }
        public Game.DataFiles.EveManager EveDat { get; set; } // Added Property
        public global::DataFiles.PhxNpcDat NpcDat { get; set; } // Added Property for Npc.dat

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
            src = GetDataTable("SELECT * FROM inventory where charID = '" + c.CharID + "'");

            if (src.Rows.Count > 0)
            {
                ushort id;

                for (int i = 0; i < src.Rows.Count; i++)
                {
                    id = ushort.Parse(src.Rows[i]["itemID"].ToString());
                    switch (uint.Parse(src.Rows[i]["storID"].ToString()))
                    {
                        case 0:
                            if (id != 0)
                            {
                                Game.Code.InvItem data = new Game.Code.InvItem();
                                data.CopyFrom(ItemDat.GetItemByID(id));
                                data.Ammt = byte.Parse(src.Rows[i]["qty"].ToString());
                                data.Damage = byte.Parse(src.Rows[i]["dmg"].ToString());
                                c.Inv[byte.Parse(src.Rows[i]["pos"].ToString())].CopyFrom(data);
                                //rows[i]["socketID"].ToString(), rows[i]["bombID"].ToString(),rows[i]["sewID"].ToString(),rows[i]["dmg"].ToString(),rows[i]["forge"].ToString(), , });
                            }
                            break;
                    }
                }
            }

            src = null;
            #endregion

            #region Tent
            #endregion

            #region Friends
            //src = cGlobal.gDataBaseConnection.GetDataTable("SELECT * FROM charactersextdata where charID = '" + c.ID + "'");

            //if (src.Rows.Count > 0)
            //    c.LoadFriends(src.Rows[0]["Friends"].ToString());

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
                // Force Re-creation (User Request)
                ExecuteNonQuery("DROP TABLE IF EXISTS npc_data");

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

                // Use the global instance instead of creating a new loader
                NpcDat.onDebug = (obj) => { DebugSystem.Write($"[PhxNpcDat] {obj}"); };
                NpcDat.Load(datPath).Wait(); // Wait for task to complete

                int debugLimit = 0;
                foreach (var npc in NpcDat.NpcList)
                {
                    // Fix: Reverse Name String (WLO binary data often has reversed strings or Little Endian issues)
                    string decodedName = System.Text.Encoding.GetEncoding(950).GetString(npc.NpcName).Trim('\0');
                    char[] nameArray = decodedName.ToCharArray();
                    Array.Reverse(nameArray);
                    string finalName = new string(nameArray).Trim();

                    // Using Parameterized Query to prevent SQL Syntax Errors (e.g. quotes in names)
                    string query = "INSERT OR REPLACE INTO npc_data (id, name, level, hp, element) VALUES (@id, @name, @level, @hp, @element)";

                    DbParam[] paramsList = new DbParam[] {
                        new DbParam { identifier = "@id", value = npc.NpcID.ToString() },
                        new DbParam { identifier = "@name", value = finalName }, // Use reversed name
                        new DbParam { identifier = "@level", value = npc.Level.ToString() },
                        new DbParam { identifier = "@hp", value = npc.HP.ToString() },
                        new DbParam { identifier = "@element", value = npc.element.ToString() }
                    };

                    ExecuteNonQuery(query, paramsList);
                    count++;

                    if (debugLimit < 5)
                    {
                        DebugSystem.Write($"[GameDataBase] Sample Import - ID: {npc.NpcID}, NameRaw: {decodedName}, NameFixed: {finalName}");
                        debugLimit++;
                    }
                }

                DebugSystem.Write($"[GameDataBase] Imported {count} NPCs from Npc.dat");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GameDataBase] Error importing Npc.dat: {ex.Message}");
            }
            return count;
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
                // Escape name
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

        //public void SetupMap(ref Game.Maps.GameMap src)
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
