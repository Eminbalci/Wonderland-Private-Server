using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game;
using Game.Battle;
using Game.Maps;
using Game.Code;
using Game.QuestRelated;
using Game.SkillRelated;
using Network;

namespace Game.PlayerRelated
{
    public static class GmManager
    {
        private static readonly HashSet<string> _gmNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();
        private static string ConfigPath => RCLibrary.Core.PathHelper.GetDataFilePath("gm_list.txt");

        public static event Action OnGmListChanged;

        static GmManager()
        {
            Initialize();
        }

        public static void Initialize()
        {
            LoadFromDatabase();
        }

        public static bool IsGm(Player player)
        {
            if (player == null) return false;

            lock (_lock)
            {
                if (!string.IsNullOrEmpty(player.CharName) && _gmNames.Contains(player.CharName))
                    return true;

                if (player.UserAccount != null)
                {
                    if (player.UserAccount.GMlvl > 0) return true;
                    if (!string.IsNullOrEmpty(player.UserAccount.UserName) && _gmNames.Contains(player.UserAccount.UserName))
                        return true;
                }
            }

            return false;
        }

        public static bool IsGm(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (_lock)
            {
                return _gmNames.Contains(name);
            }
        }

        public static List<string> GetGmList()
        {
            lock (_lock)
            {
                return _gmNames.OrderBy(n => n).ToList();
            }
        }

        public static void VerifyTable()
        {
            try
            {
                RCLibrary.Core.DataBase.Execute("CREATE TABLE IF NOT EXISTS gm_accounts (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, username TEXT, added_at TEXT, added_by TEXT);");
                RCLibrary.Core.DataBase.StaticAddColumnIfNotExists("gm_accounts", "name", "TEXT");
                RCLibrary.Core.DataBase.StaticAddColumnIfNotExists("gm_accounts", "username", "TEXT");
                RCLibrary.Core.DataBase.StaticAddColumnIfNotExists("gm_accounts", "added_at", "TEXT");
                RCLibrary.Core.DataBase.StaticAddColumnIfNotExists("gm_accounts", "added_by", "TEXT");

                RCLibrary.Core.DataBase.Execute("UPDATE gm_accounts SET name = username WHERE (name IS NULL OR name = '') AND username IS NOT NULL;");
                RCLibrary.Core.DataBase.Execute("UPDATE gm_accounts SET username = name WHERE (username IS NULL OR username = '') AND name IS NOT NULL;");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GmManager] Error verifying gm_accounts table: {ex.Message}");
            }
        }

        public static bool AddGm(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();

            lock (_lock)
            {
                if (_gmNames.Add(name))
                {
                    VerifyTable();
                    string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    RCLibrary.Core.DataBase.Execute($"INSERT INTO gm_accounts (name, username, added_at, added_by) VALUES ('{name.Replace("'", "''")}', '{name.Replace("'", "''")}', '{now}', 'ServerAdmin');");
                    OnGmListChanged?.Invoke();
                    DebugSystem.Write($"[GmManager] Added '{name}' to GM list database.");
                    return true;
                }
            }
            return false;
        }

        public static bool RemoveGm(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();

            lock (_lock)
            {
                if (_gmNames.Remove(name))
                {
                    VerifyTable();
                    RCLibrary.Core.DataBase.Execute($"DELETE FROM gm_accounts WHERE name = '{name.Replace("'", "''")}' OR username = '{name.Replace("'", "''")}';");
                    OnGmListChanged?.Invoke();
                    DebugSystem.Write($"[GmManager] Removed '{name}' from GM list database.");
                    return true;
                }
            }
            return false;
        }

        public static void LoadFromFile() => LoadFromDatabase();
        public static void SaveToFile() { }

        public static void LoadFromDatabase()
        {
            try
            {
                VerifyTable();
                var dt = RCLibrary.Core.DataBase.Query("SELECT COALESCE(name, username) AS name FROM gm_accounts;");
                if (dt == null || dt.Rows.Count == 0)
                {
                    // Seed defaults
                    var defaults = new List<string> { "admin", "developer", "Admin", "gmone", "GM", "test" };
                    if (File.Exists(ConfigPath))
                    {
                        var lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                        foreach (var rawLine in lines)
                        {
                            string line = rawLine.Trim();
                            if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                            {
                                if (!defaults.Contains(line, StringComparer.OrdinalIgnoreCase)) defaults.Add(line);
                            }
                        }
                    }

                    string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    foreach (var gm in defaults)
                    {
                        RCLibrary.Core.DataBase.Execute($"INSERT INTO gm_accounts (name, username, added_at, added_by) VALUES ('{gm.Replace("'", "''")}', '{gm.Replace("'", "''")}', '{now}', 'System');");
                    }

                    dt = RCLibrary.Core.DataBase.Query("SELECT COALESCE(name, username) AS name FROM gm_accounts;");
                }

                lock (_lock)
                {
                    _gmNames.Clear();
                    if (dt != null)
                    {
                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            string n = row["name"]?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(n)) _gmNames.Add(n);
                        }
                    }
                }
                DebugSystem.Write($"[GmManager] Loaded {_gmNames.Count} GM accounts/characters from database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GmManager] Error loading GM list: {ex.Message}");
            }
        }

        public static Func<List<Player>> ExternalPlayerListProvider { get; set; }

        public static List<Player> GetAllOnlinePlayers()
        {
            var list = DataBase.CharacterDataBase.GlobalInstance?.GetOnlinePlayers() ?? new List<Player>();
            if (ExternalPlayerListProvider != null)
            {
                try
                {
                    var ext = ExternalPlayerListProvider();
                    if (ext != null)
                    {
                        foreach (var p in ext)
                        {
                            if (p != null && !list.Any(x => x.CharID == p.CharID))
                            {
                                list.Add(p);
                            }
                        }
                    }
                }
                catch { }
            }
            return list;
        }

        public static Player FindOnlinePlayer(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            query = query.Trim();

            var all = GetAllOnlinePlayers();
            if (uint.TryParse(query, out uint charId))
            {
                var matchId = all.FirstOrDefault(p => p.CharID == charId);
                if (matchId != null) return matchId;
            }

            var exactMatch = all.FirstOrDefault(p => p.CharName != null && p.CharName.Equals(query, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null) return exactMatch;

            return all.FirstOrDefault(p => p.CharName != null && p.CharName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static void SendChatMessage(Player p, byte chatType, string message)
        {
            if (p == null || string.IsNullOrEmpty(message)) return;
            SendPacket pkt = new SendPacket();
            pkt.Pack8(2); // ActionCode 2 (Chat)
            pkt.Pack8(chatType); // 4 = GM (Red/Orange), 1 = World (Yellow), 3 = Channel (Blue), 6 = Whisper (Pink)
            pkt.Pack32(0); // 4-byte Sender Char ID (0 for System/GM)
            pkt.PackStringN(message);
            p.Send(pkt);
        }

        public static int BroadcastNotice(string message, byte chatType = 4)
        {
            if (string.IsNullOrWhiteSpace(message)) return 0;
            message = message.Trim();

            var online = GetAllOnlinePlayers();
            if (online == null || online.Count == 0) return 0;

            int sent = 0;
            foreach (var p in online)
            {
                try
                {
                    SendChatMessage(p, chatType, message);
                    p.SendSystemMessage($"[Notice] {message}");
                    sent++;
                }
                catch { }
            }
            DebugSystem.Write($"[GmManager] Broadcasted notice to {sent} player(s): {message}");
            return sent;
        }

        public static bool KickPlayer(Player target, string reason = null)
        {
            if (target == null) return false;
            try
            {
                string msg = string.IsNullOrEmpty(reason) ? "[Server] You have been disconnected by a Game Master." : $"[Server] Disconnected by GM: {reason}";
                target.SendSystemMessage(msg);
                target.Disconnect();
                DebugSystem.Write($"[GmManager] Kicked player '{target.CharName}' (CharID: {target.CharID}). Reason: {reason ?? "None"}");
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GmManager] Error kicking player: {ex.Message}");
                return false;
            }
        }

        public static bool SummonPlayer(Player gm, Player target, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (gm == null || target == null)
            {
                statusMsg = "GM or Target player is null.";
                return false;
            }
            if (gm.CurMap == null || target.CurMap == null)
            {
                statusMsg = "Map reference is null.";
                return false;
            }

            WarpData warp = new WarpData
            {
                DstMap = (ushort)gm.CurMap.MapID,
                DstX_Axis = (ushort)gm.CurX,
                DstY_Axis = (ushort)gm.CurY
            };
            target.CurMap.Teleport(TeleportType.CmD, target, 0, warp);
            target.SendSystemMessage($"[GM] You have been summoned by GM {gm.CharName}.");
            gm.SendSystemMessage($"[GM] Summoned '{target.CharName}' to Map {gm.CurMap.MapID} ({gm.CurX}, {gm.CurY}).");
            statusMsg = $"Summoned '{target.CharName}' to ({gm.CurX}, {gm.CurY}).";
            return true;
        }

        public static bool GotoPlayer(Player gm, Player target, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (gm == null || target == null)
            {
                statusMsg = "GM or Target player is null.";
                return false;
            }
            if (gm.CurMap == null || target.CurMap == null)
            {
                statusMsg = "Map reference is null.";
                return false;
            }

            WarpData warp = new WarpData
            {
                DstMap = (ushort)target.CurMap.MapID,
                DstX_Axis = (ushort)target.CurX,
                DstY_Axis = (ushort)target.CurY
            };
            gm.CurMap.Teleport(TeleportType.CmD, gm, 0, warp);
            gm.SendSystemMessage($"[GM] Teleported to '{target.CharName}' at Map {target.CurMap.MapID} ({target.CurX}, {target.CurY}).");
            statusMsg = $"Teleported to '{target.CharName}' at Map {target.CurMap.MapID} ({target.CurX}, {target.CurY}).";
            return true;
        }

        public static bool SetPetAmity(Player player, byte amity, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (player == null)
            {
                statusMsg = "Player is null.";
                return false;
            }

            var pet = player.PlayerPets?.Values.FirstOrDefault(pt => pt.IsBattle)
                   ?? (player.ActivePetID > 0 ? player.PlayerPets?.Values.FirstOrDefault(pt => pt.PetID == player.ActivePetID || pt.Slot == player.ActivePetID) : null)
                   ?? player.PlayerPets?.Values.FirstOrDefault();

            if (pet == null)
            {
                statusMsg = "No companion or pet found for this player.";
                return false;
            }

            amity = Math.Min((byte)100, amity);
            pet.Amity = amity;
            QuestManager.SendPetAmity(player, pet);
            DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(player.CharID, player);
            player.SendSystemMessage($"[GM] {pet.PetName}'s Amity has been set to {amity}/100.");
            statusMsg = $"{pet.PetName}'s Amity set to {amity}/100.";
            return true;
        }

        public static bool TriggerPetRebirth(Player player, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (player == null)
            {
                statusMsg = "Player is null.";
                return false;
            }

            var pet = player.PlayerPets?.Values.FirstOrDefault(pt => pt.IsBattle)
                   ?? (player.ActivePetID > 0 ? player.PlayerPets?.Values.FirstOrDefault(pt => pt.PetID == player.ActivePetID || pt.Slot == player.ActivePetID) : null)
                   ?? player.PlayerPets?.Values.FirstOrDefault();

            if (pet == null)
            {
                statusMsg = "No companion or pet found for this player.";
                return false;
            }

            pet.Reborn = true;
            pet.Level = 1;
            pet.Exp = 0;
            pet.SkillPoints += 50;
            pet.Amity = 100;
            pet.MaxHP = 300;
            pet.HP = pet.MaxHP;
            pet.MaxSP = 150;
            pet.SP = pet.MaxSP;

            player.SaveCharacterData();

            SendPacket resp = new SendPacket();
            resp.Pack8(69);
            resp.Pack8(1);
            resp.Pack8(pet.Slot);
            resp.Pack8(1);
            player.Send(resp);

            pet.NormalizeClientStats(true, player.Inv);
            player.Send(QuestManager.CreatePetListPacket(player));
            QuestManager.SendPetProgression(player, pet);

            player.SendSystemMessage($"[Rebirth] {pet.PetName} has attained Rebirth Ascension via GM powers!");
            statusMsg = $"{pet.PetName} (Slot {pet.Slot}) successfully underwent Rebirth!";
            return true;
        }

        public static bool UnlockAllSkills(Player player, byte grade, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (player == null)
            {
                statusMsg = "Player is null.";
                return false;
            }

            grade = Math.Max((byte)1, Math.Min((byte)10, grade));
            var elem = player.Element != Affinity.Normal ? player.Element : (player.Eqs != null ? player.Eqs.Element : Affinity.Fire);

            byte bodyVal = (byte)player.Body;
            if (bodyVal == 0 && player.Eqs != null) bodyVal = (byte)player.Eqs.Body;
            byte headVal = (byte)player.Head;
            if (headVal == 0 && player.Eqs != null) headVal = (byte)player.Eqs.Head;

            List<uint> skillsToUnlock = new List<uint>();
            skillsToUnlock.Add(SkillManager.GetStarterStuntSkill(bodyVal, headVal));

            switch (elem)
            {
                case Affinity.Fire:
                    skillsToUnlock.AddRange(new uint[] { 11016, 15101, 11114, 12039, 15102, 15044, 11166, 11005, 11034, 15109, 15111, 11035, 11056, 11002, 11072, 12045, 11003, 15171 });
                    break;
                case Affinity.Earth:
                    skillsToUnlock.AddRange(new uint[] { 15085, 11017, 11087, 15083, 15049, 15146, 12006, 15056, 11019, 11031, 15086, 11107, 11057, 12043, 12048, 11055, 15070, 15035 });
                    break;
                case Affinity.Water:
                    skillsToUnlock.AddRange(new uint[] { 15091, 11001, 11044, 15019, 12007, 15156, 15097, 11040, 11110, 11113, 11024, 15158, 15100, 11042, 15075, 11080, 11051, 11043 });
                    break;
                case Affinity.Wind:
                    skillsToUnlock.AddRange(new uint[] { 11007, 15079, 15117, 15002, 11046, 15114, 30002, 11015, 15123, 15125, 15048, 15161, 11052, 11073, 12046, 15032, 15036, 11026 });
                    break;
                case Affinity.Dark:
                default:
                    skillsToUnlock.AddRange(new uint[] { 25115, 25116, 25110, 25165, 25169, 25175, 25185, 25246, 25247, 25248 });
                    break;
            }

            int count = 0;
            foreach (var sId in skillsToUnlock.Distinct())
            {
                SkillManager.UnlockSkill(player, sId, grade);
                count++;
            }

            SkillManager.SendAllSkills(player);
            player.SendSystemMessage($"[GM] All element skills ({count} skills) unlocked at Grade {grade}!");
            statusMsg = $"Unlocked {count} skills (Grade {grade}) for {player.CharName}.";
            return true;
        }

        public static bool RecruitCompanion(Player target, uint petId, string petName, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (target == null)
            {
                statusMsg = "Target player is null.";
                return false;
            }

            if (string.IsNullOrEmpty(petName))
            {
                switch (petId)
                {
                    case 12178: petName = "Robinson"; break;
                    case 14161: petName = "Roca"; break;
                    case 14162: petName = "Niss"; break;
                    case 14163: petName = "Clive"; break;
                    case 14164: petName = "Fred"; break;
                    case 14165: petName = "Sam"; break;
                    case 14166: petName = "Elin"; break;
                    case 14167: petName = "Shizune"; break;
                    case 14168: petName = "Victoria"; break;
                    case 14169: petName = "Angela"; break;
                    case 14170: petName = "Eva"; break;
                    case 10727: petName = "Monkey"; break;
                    default: petName = $"Companion_{petId}"; break;
                }
            }

            try
            {
                QuestManager.SendCompanionReward(target, petId, petName, setBattle: true);
                DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(target.CharID, target);
                target.SendSystemMessage($"[GM] Companion '{petName}' (ID: {petId}) has joined your party!");
                statusMsg = $"Recruited companion '{petName}' (ID: {petId}) for {target.CharName}.";
                return true;
            }
            catch (Exception ex)
            {
                statusMsg = $"Error recruiting companion: {ex.Message}";
                return false;
            }
        }

        public static bool MutePlayer(Player target, int minutes, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (target == null)
            {
                statusMsg = "Target player is null.";
                return false;
            }

            minutes = Math.Max(1, minutes);
            target.MutedUntil = DateTime.UtcNow.AddMinutes(minutes);
            target.SendSystemMessage($"[Server] You have been muted for {minutes} minute(s) by a Game Master.");
            statusMsg = $"Muted '{target.CharName}' for {minutes} minute(s).";
            return true;
        }

        public static bool UnmutePlayer(Player target, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (target == null)
            {
                statusMsg = "Target player is null.";
                return false;
            }

            target.MutedUntil = null;
            target.SendSystemMessage("[Server] Your chat privileges have been restored.");
            statusMsg = $"Unmuted '{target.CharName}'.";
            return true;
        }

        public static string ReloadAll()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                QuestManager.InitializeQuests();
                sb.Append($"Quests: {QuestManager.MasterCount} registered. ");
            }
            catch (Exception ex) { sb.Append($"Quests error: {ex.Message}. "); }

            try
            {
                ItemMallManager.Initialize();
                sb.Append($"ItemMall: {ItemMallManager.GetCatalog().Count} items loaded. ");
            }
            catch (Exception ex) { sb.Append($"ItemMall error: {ex.Message}. "); }

            try
            {
                MonsterDropManager.Initialize();
                sb.Append("Monster drops reloaded. ");
            }
            catch (Exception ex) { sb.Append($"Drops error: {ex.Message}. "); }

            try
            {
                LoadFromDatabase();
                sb.Append($"GMs: {_gmNames.Count} accounts loaded.");
            }
            catch (Exception ex) { sb.Append($"GM list error: {ex.Message}."); }

            return sb.ToString();
        }

        #region Extended GM Studio & Administration Operations
        public class TownEntry
        {
            public ushort MapID { get; set; }
            public ushort X { get; set; }
            public ushort Y { get; set; }
            public string Name { get; set; }

            public TownEntry(ushort mapId, ushort x, ushort y, string name)
            {
                MapID = mapId;
                X = x;
                Y = y;
                Name = name;
            }
        }

        public static readonly Dictionary<string, TownEntry> TownDirectory =
            new Dictionary<string, TownEntry>(StringComparer.OrdinalIgnoreCase)
        {
            { "welling", new TownEntry(10001, 800, 750, "Welling Village") },
            { "kelan", new TownEntry(10011, 1000, 1000, "Kelan Village") },
            { "holy", new TownEntry(10016, 1200, 950, "Holy Village") },
            { "kyoto", new TownEntry(10041, 1300, 1100, "Kyoto") },
            { "changan", new TownEntry(10051, 1400, 1200, "Chang'an") },
            { "chang_an", new TownEntry(10051, 1400, 1200, "Chang'an") },
            { "rome", new TownEntry(10061, 1100, 1000, "Rome") },
            { "maya", new TownEntry(10071, 900, 900, "Maya") },
            { "inca", new TownEntry(10081, 800, 850, "Inca") },
            { "bangkok", new TownEntry(10091, 1200, 1100, "Bangkok") },
            { "southpole", new TownEntry(10021, 1000, 1000, "South Pole") },
            { "ghostisle", new TownEntry(10026, 800, 800, "Ghost Isle") },
            { "carnie", new TownEntry(11094, 1180, 875, "Carnie Amusement Park") },
            { "pirate", new TownEntry(10036, 1038, 2235, "Pirate Base") },
            { "kaohsiung", new TownEntry(10003, 1000, 1000, "Kaohsiung") },
            { "jail", new TownEntry(10000, 600, 600, "Jail Cell") }
        };

        public static bool WarpPlayer(Player target, ushort mapId, ushort x, ushort y, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (target == null)
            {
                statusMsg = "Target player is null.";
                return false;
            }
            if (target.CurMap == null)
            {
                statusMsg = "Target map reference is null.";
                return false;
            }

            WarpData warp = new WarpData
            {
                DstMap = mapId,
                DstX_Axis = x,
                DstY_Axis = y
            };
            target.CurMap.Teleport(TeleportType.CmD, target, 0, warp);
            target.SendSystemMessage($"[GM] Warped to Map {mapId} ({x}, {y}).");
            statusMsg = $"Warped '{target.CharName}' to Map {mapId} ({x}, {y}).";
            return true;
        }

        public static bool ToggleInvisibility(Player player, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (player == null)
            {
                statusMsg = "Player is null.";
                return false;
            }

            player.IsInvisible = !player.IsInvisible;
            if (player.IsInvisible)
            {
                SendPacket despawn = new SendPacket();
                despawn.Pack8(12);
                despawn.Pack32(player.CharID);
                despawn.Pack16(0);
                despawn.Pack16(0);
                despawn.Pack16(0);
                despawn.Pack16(0);
                despawn.Pack8(0);
                player.CurMap?.Broadcast(despawn, "Ex", player.CharID);

                player.SendSystemMessage("[GM] Invisibility (Ghost Mode) ACTIVATED! You are now hidden from all other players.");
                statusMsg = $"{player.CharName} is now INVISIBLE (Ghost Mode active).";
            }
            else
            {
                var spawnPkt = player.ToAC4Packet();
                player.CurMap?.Broadcast(spawnPkt, "Ex", player.CharID);

                SendPacket pEquip = new SendPacket();
                pEquip.Pack8(5);
                pEquip.Pack8(0);
                pEquip.Pack32(player.CharID);
                pEquip.PackArray(player.Worn_Equips);
                player.CurMap?.Broadcast(pEquip, "Ex", player.CharID);

                player.SendSystemMessage("[GM] Invisibility (Ghost Mode) DEACTIVATED! You are now visible to players.");
                statusMsg = $"{player.CharName} is now VISIBLE.";
            }
            return true;
        }

        public static bool RestatPlayer(Player player, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (player == null || player.Eqs == null)
            {
                statusMsg = "Player or equipment is null.";
                return false;
            }

            int refund = (player.Eqs.Str - 10) + (player.Eqs.Con - 10) + (player.Eqs.Int - 10) + (player.Eqs.Wis - 10) + (player.Eqs.Agi - 10);
            if (refund < 0) refund = 0;

            player.Eqs.Str = 10;
            player.Eqs.Con = 10;
            player.Eqs.Int = 10;
            player.Eqs.Wis = 10;
            player.Eqs.Agi = 10;
            player.Eqs.SkillPoints = (ushort)Math.Min(ushort.MaxValue, player.Eqs.SkillPoints + refund);
            player.Eqs.CurHP = player.Eqs.FullHP;
            player.Eqs.CurSP = player.Eqs.FullSP;
            player.Eqs.Send8_1(true);
            player.Send_5_3();

            DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(player.CharID, player);
            player.SendSystemMessage($"[GM] Stats refunded! +{refund} points returned. Available Skill Points: {player.Eqs.SkillPoints}.");
            statusMsg = $"Refunded +{refund} points for {player.CharName}. Available: {player.Eqs.SkillPoints}.";
            return true;
        }

        public static bool ClearSkills(Player player, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (player == null)
            {
                statusMsg = "Player is null.";
                return false;
            }

            try
            {
                player.PlayerSkills.Clear();
                var db = DataBase.CharacterDataBase.GlobalInstance;
                db?.ExecuteNonQuery($"DELETE FROM character_skills WHERE charID = '{player.CharID}';");
                SkillManager.InitializePlayerSkills(player);
                player.SendSystemMessage("[GM] Progression skills cleared and reset to baseline starter stunts!");
                statusMsg = $"Cleared and reset skills for {player.CharName}.";
                return true;
            }
            catch (Exception ex)
            {
                statusMsg = $"Error clearing skills: {ex.Message}";
                return false;
            }
        }

        public static bool RepairAllItems(Player player, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (player == null)
            {
                statusMsg = "Player is null.";
                return false;
            }

            int count = 0;
            if (player.Eqs != null)
            {
                for (byte i = 1; i <= 6; i++)
                {
                    var eq = player.Eqs[i];
                    if (eq != null && eq.ItemID > 0)
                    {
                        eq.Damage = 0;
                        count++;
                    }
                }
                player.Eqs.Send8_1(false);
            }

            if (player.Inv != null)
            {
                for (byte i = 1; i <= 50; i++)
                {
                    var itm = player.Inv[i];
                    if (itm != null && itm.ItemID > 0)
                    {
                        if (itm.Damage > 0) count++;
                        itm.Damage = 0;
                    }
                }
            }

            DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(player.CharID, player);
            player.SendSystemMessage("[GM] All equipped gear and inventory items repaired to 100% (0 Damage)!");
            statusMsg = $"Repaired gear & bag items ({count} item(s) restored) for {player.CharName}.";
            return true;
        }

        public static bool AddMallPoints(Player target, int points, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (target == null || target.UserAccount == null)
            {
                statusMsg = "Target player or user account is null.";
                return false;
            }

            int current = ItemMallManager.GetUserPoints(target);
            int updated = Math.Max(0, current + points);
            ItemMallManager.SetUserPoints(target, updated);
            target.SendSystemMessage($"[Item Mall] +{points:N0} IM Points granted by GM! Current Balance: {updated:N0} Points.");
            statusMsg = $"Added {points:N0} IM Points to {target.CharName} (New Total: {updated:N0}).";
            return true;
        }

        public static bool JailPlayer(Player target, int minutes, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (target == null)
            {
                statusMsg = "Target player is null.";
                return false;
            }

            minutes = Math.Max(1, minutes);
            MutePlayer(target, minutes, out _);

            WarpData jailWarp = new WarpData { DstMap = 10000, DstX_Axis = 600, DstY_Axis = 600 };
            target.CurMap?.Teleport(TeleportType.CmD, target, 0, jailWarp);

            target.SendSystemMessage($"[Server] You have been sent to JAIL for {minutes} minute(s) by a Game Master.");
            statusMsg = $"Jailed '{target.CharName}' for {minutes} minute(s) at Map 10000 (600, 600).";
            return true;
        }

        public static bool UnjailPlayer(Player target, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (target == null)
            {
                statusMsg = "Target player is null.";
                return false;
            }

            UnmutePlayer(target, out _);

            WarpData unjailWarp = new WarpData { DstMap = 10001, DstX_Axis = 800, DstY_Axis = 750 };
            target.CurMap?.Teleport(TeleportType.CmD, target, 0, unjailWarp);

            target.SendSystemMessage("[Server] You have been released from Jail! Be on your best behavior.");
            statusMsg = $"Released '{target.CharName}' from Jail to Welling Village.";
            return true;
        }

        public static int SummonAllPlayers(Player gm, out string statusMsg)
        {
            statusMsg = string.Empty;
            if (gm == null || gm.CurMap == null)
            {
                statusMsg = "GM or current map is null.";
                return 0;
            }

            var online = GetAllOnlinePlayers();
            int count = 0;
            WarpData warp = new WarpData
            {
                DstMap = (ushort)gm.CurMap.MapID,
                DstX_Axis = (ushort)gm.CurX,
                DstY_Axis = (ushort)gm.CurY
            };

            foreach (var p in online)
            {
                if (p != gm && p.CurMap != null)
                {
                    try
                    {
                        p.CurMap.Teleport(TeleportType.CmD, p, 0, warp);
                        p.SendSystemMessage($"[Event] You have been summoned by GM {gm.CharName}!");
                        count++;
                    }
                    catch { }
                }
            }

            statusMsg = $"Summoned {count} online player(s) to Map {gm.CurMap.MapID} ({gm.CurX}, {gm.CurY}).";
            return count;
        }

        public static int KickAllPlayers(string reason, out string statusMsg)
        {
            statusMsg = string.Empty;
            var online = GetAllOnlinePlayers();
            int count = 0;
            string kickReason = string.IsNullOrWhiteSpace(reason) ? "Server maintenance in progress." : reason;

            foreach (var p in online)
            {
                if (!IsGm(p))
                {
                    try
                    {
                        p.SendSystemMessage($"[Server] Disconnected by Admin: {kickReason}");
                        p.Disconnect();
                        count++;
                    }
                    catch { }
                }
            }

            statusMsg = $"Kicked {count} non-GM player(s) for: {kickReason}";
            return count;
        }

        public static bool GetPlayerInfo(Player target, out string infoReport)
        {
            infoReport = string.Empty;
            if (target == null)
            {
                infoReport = "Player is null or offline.";
                return false;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[Character Info] {target.CharName} (CharID: {target.CharID})");
            sb.AppendLine($" - Level: {target.Eqs?.Level ?? 1} | Element: {target.Element} | Job: {target.Job}");
            sb.AppendLine($" - HP: {target.Eqs?.CurHP}/{target.Eqs?.FullHP} | SP: {target.Eqs?.CurSP}/{target.Eqs?.FullSP}");
            sb.AppendLine($" - Gold: {target.Gold:N0} | IM Points: {ItemMallManager.GetUserPoints(target):N0} | Stat Points: {target.Eqs?.SkillPoints ?? 0}");
            sb.AppendLine($" - Base Stats: STR={target.Eqs?.Str ?? 0} CON={target.Eqs?.Con ?? 0} INT={target.Eqs?.Int ?? 0} WIS={target.Eqs?.Wis ?? 0} AGI={target.Eqs?.Agi ?? 0}");
            string mapName = target.CurMap != null ? $"Map {target.CurMap.MapID} ({target.CurX}, {target.CurY})" : "Unknown";
            sb.AppendLine($" - Location: {mapName}");

            var pet = target.PlayerPets?.Values.FirstOrDefault(pt => pt.IsBattle) ?? target.PlayerPets?.Values.FirstOrDefault();
            if (pet != null)
            {
                sb.AppendLine($" - Companion: {pet.PetName} (ID: {pet.PetID}, Lv.{pet.Level}, Amity: {pet.Amity}/100, HP: {pet.HP}/{pet.MaxHP})");
            }
            else
            {
                sb.AppendLine(" - Companion: None");
            }

            sb.AppendLine($" - Muted: {(target.IsMuted ? $"Yes (until {target.MutedUntil:u})" : "No")} | Ghost Mode: {(target.IsInvisible ? "Active" : "No")}");
            infoReport = sb.ToString();
            return true;
        }
        #endregion
    }
}
