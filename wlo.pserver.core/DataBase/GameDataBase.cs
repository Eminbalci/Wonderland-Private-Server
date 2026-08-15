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
                            }
                            break;
                        case 1:
                            if (id != 0)
                            {
                                byte pos = byte.Parse(src.Rows[i]["pos"].ToString());
                                if (pos >= 1 && pos <= 6)
                                {
                                    c[pos].CopyFrom(ItemDat.GetItemByID(id));
                                    c[pos].Ammt = 1;
                                    c[pos].Damage = byte.Parse(src.Rows[i]["dmg"].ToString());
                                }
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

                // Load npc.json lookup dictionary if available
                Dictionary<int, string> npcJsonNames = new Dictionary<int, string>();
                string jsonPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "npc.json");
                if (System.IO.File.Exists(jsonPath))
                {
                    try
                    {
                        string jsonText = System.IO.File.ReadAllText(jsonPath);
                        var matches = System.Text.RegularExpressions.Regex.Matches(jsonText, @"""(\d+)""\s*:\s*""([^""]+)""");
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            if (int.TryParse(match.Groups[1].Value, out int jid))
                            {
                                npcJsonNames[jid] = match.Groups[2].Value;
                            }
                        }
                        DebugSystem.Write($"[GameDataBase] Loaded {npcJsonNames.Count} NPC names from npc.json");
                    }
                    catch (Exception jsonEx)
                    {
                        DebugSystem.Write($"[GameDataBase] Error parsing npc.json: {jsonEx.Message}");
                    }
                }

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
                    int id = (ushort)((rawId ^ 0x5209) - 1);
                    if (id == 0) continue;

                    byte rawLvl = fileBytes[offset + 37];
                    int level = (byte)((rawLvl ^ 0xC8) - 1);

                    uint rawHp = (uint)(fileBytes[offset + 38] | (fileBytes[offset + 39] << 8) | (fileBytes[offset + 40] << 16) | (fileBytes[offset + 41] << 24));
                    int hp = (int)((rawHp ^ 0x0BAEB716) - 1);

                    byte rawElem = fileBytes[offset + 57];
                    int element = (byte)((rawElem ^ 0xC8) - 1);

                    // 2. Extract authentic name from reversed 10-byte buffer (offset + 1 to offset + 10)
                    var rawChars = new List<byte>();
                    for (int i = offset + 10; i >= offset + 1; i--)
                    {
                        byte b = fileBytes[i];
                        if (b != 0 && b != 0xCA && b != 0xC8 && b != 0xC9)
                        {
                            rawChars.Add(b);
                        }
                    }

                    string datName = System.Text.Encoding.ASCII.GetString(rawChars.ToArray()).Trim();

                    string finalName = null;

                    // Priority 1: Exact lookup in npc.json for full canonical English names
                    if (npcJsonNames.TryGetValue(id, out string exactName))
                    {
                        finalName = exactName;
                    }
                    // Priority 2: Decoded ASCII name from Npc.dat
                    else if (!string.IsNullOrEmpty(datName) && datName.Length >= 2)
                    {
                        finalName = datName;
                    }
                    // Priority 3: Big5 CJK decoding
                    else
                    {
                        var cjkBytes = new List<byte>();
                        for (int i = offset + 1; i <= offset + 10; i++)
                        {
                            byte b = fileBytes[i];
                            if (b != 0 && b != 0xCA && b != 0xC8 && b != 0xC9) cjkBytes.Add(b);
                        }
                        if (cjkBytes.Count > 0)
                        {
                            cjkBytes.Reverse();
                            string cjk = System.Text.Encoding.GetEncoding(950).GetString(cjkBytes.ToArray()).Trim('\0', ' ');
                            if (!cjk.Contains("?") && !string.IsNullOrEmpty(cjk) && cjk.Length >= 2)
                            {
                                finalName = cjk;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(finalName))
                    {
                        finalName = $"NPC_{id}";
                    }
                    else
                    {
                        finalName = new string(finalName.Where(c => !char.IsControl(c)).ToArray()).Trim();
                    }

                    finalName = finalName.Replace("'", "''");
                    batch.Append($"({id}, '{finalName}', {level}, {hp}, {element}),");
                    count++;

                    if (debugLimit < 5)
                    {
                        DebugSystem.Write($"[GameDataBase] Sample Import - ID: {id}, Name: {finalName}");
                        debugLimit++;
                    }

                    if (count % 200 == 0)
                    {
                        string batchSql = "INSERT OR REPLACE INTO npc_data (id, name, level, hp, element) VALUES " + batch.ToString().TrimEnd(',') + ";";
                        ExecuteNonQuery(batchSql);
                        batch.Clear();
                    }
                }

                if (batch.Length > 0)
                {
                    string batchSql = "INSERT OR REPLACE INTO npc_data (id, name, level, hp, element) VALUES " + batch.ToString().TrimEnd(',') + ";";
                    ExecuteNonQuery(batchSql);
                    batch.Clear();
                }

                DebugSystem.Write($"[GameDataBase] Successfully Imported {count} NPCs from Npc.dat");
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

        public DataRow GetNpcTemplate(int id)
        {
            var dt = GetDataTable($"SELECT * FROM npc_data WHERE id = {id}");
            if (dt != null && dt.Rows.Count > 0)
                return dt.Rows[0];
            return null;
        }

        public NpcTemplateInfo ResolveNpcInfo(ushort mapId, byte clickId, ushort templateId)
        {
            // 1. Map/Click specific overrides matching Python server
            var overrides = new Dictionary<string, int>
            {
                { "10017_9", 25787 },
                { "10017_10", 25789 },
                { "10017_3", 25786 },
                { "10017_11", 25786 },
            };

            string key = $"{mapId}_{clickId}";
            if (overrides.TryGetValue(key, out int overrideId))
            {
                var r = GetNpcTemplate(overrideId);
                if (r != null)
                    return new NpcTemplateInfo(r["name"].ToString(), Convert.ToInt32(r["level"]), Convert.ToInt32(r["hp"]), Convert.ToInt32(r["element"]));
            }

            // 2. Pre-decode client-side special template ID mappings
            ushort mappedId = templateId;
            if (templateId == 0x908e) mappedId = 0x5209;
            else if (templateId == 0x9092) mappedId = 0x9090;
            else if (templateId == 0x9093) mappedId = 0x9091;
            else if (templateId == 0x9094) mappedId = 0x9095;
            else if (templateId == 0x9096) mappedId = 0x9097;

            int decNoOffset = (mappedId & 0xFFFF) ^ 0x5209;
            int decWithOffset = decNoOffset - 9;
            int[] candidates = new int[]
            {
                templateId,
                decNoOffset,
                decWithOffset,
                decNoOffset + 27000,
                decWithOffset + 27000,
                decNoOffset + 10000,
                decWithOffset + 10000,
                templateId * 2,
                templateId + 16000
            };

            // Priority pass: Find a template that has a named identity (not NPC_xxx)
            foreach (var candId in candidates)
            {
                var r = GetNpcTemplate(candId);
                if (r != null)
                {
                    string n = r["name"].ToString();
                    if (!string.IsNullOrEmpty(n) && !n.StartsWith("NPC_"))
                        return new NpcTemplateInfo(n, Convert.ToInt32(r["level"]), Convert.ToInt32(r["hp"]), Convert.ToInt32(r["element"]));
                }
            }

            // Secondary pass: Any matching template
            foreach (var candId in candidates)
            {
                var r = GetNpcTemplate(candId);
                if (r != null)
                    return new NpcTemplateInfo(r["name"].ToString(), Convert.ToInt32(r["level"]), Convert.ToInt32(r["hp"]), Convert.ToInt32(r["element"]));
            }

            return new NpcTemplateInfo($"NPC_{templateId}", 1, 100, 0);
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
