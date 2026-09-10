using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Network;

namespace Game.PlayerRelated
{
    public enum GuildMemberRank : byte
    {
        Member = 0,
        ViceLeader = 1,
        Leader = 2
    }

    public class GuildMember
    {
        public uint CharID { get; set; }
        public uint ID => CharID;
        public string CharName { get; set; }
        public int Level { get; set; }
        public byte Job { get; set; }
        public byte Element { get; set; }
        public GuildMemberRank Rank { get; set; } = GuildMemberRank.Member;
        public bool IsOnline => PlayerRef != null;
        public Player PlayerRef { get; set; }

        public GuildMember() { }

        public GuildMember(Player p, GuildMemberRank rank = GuildMemberRank.Member)
        {
            if (p == null) return;
            CharID = p.CharID;
            CharName = p.CharName;
            Level = p.Level;
            Job = (byte)p.Job;
            Element = (byte)p.Element;
            Rank = rank;
            PlayerRef = p;
        }
    }

    public class Guild
    {
        public ushort GuildID { get; set; }
        public string GuildName { get; set; } = string.Empty;
        public uint LeaderID { get; set; }
        public string LeaderName { get; set; } = string.Empty;
        public uint Icon { get; set; } = 3402;
        public string Rules { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public readonly Dictionary<uint, GuildMember> Members = new Dictionary<uint, GuildMember>();
        private readonly object _lock = new object();

        public int MemberCount => Members.Count;

        public Guild() { }

        public Guild(ushort id, string name, Player leader, uint icon = 3402)
        {
            GuildID = id;
            GuildName = name;
            Icon = icon;
            DateCreated = DateTime.UtcNow;
            if (leader != null)
            {
                LeaderID = leader.CharID;
                LeaderName = leader.CharName;
                var gm = new GuildMember(leader, GuildMemberRank.Leader);
                Members[leader.CharID] = gm;
            }
        }

        public bool IsLeader(uint charId) => LeaderID == charId;

        public bool IsViceLeader(uint charId)
        {
            lock (_lock)
            {
                return Members.TryGetValue(charId, out var m) && (m.Rank == GuildMemberRank.ViceLeader || m.Rank == GuildMemberRank.Leader);
            }
        }

        public void AddMember(Player player, GuildMemberRank rank = GuildMemberRank.Member)
        {
            if (player == null) return;

            lock (_lock)
            {
                var gm = new GuildMember(player, rank);
                Members[player.CharID] = gm;
                player.CurGuild = this;
            }

            // 1. Send Clean Tab & Guild Info to new member
            SendInfo(player);

            // 2. Broadcast updated member list to all online guild members
            BroadcastMemberList();

            // 3. Broadcast Insignia badge
            SendInsignia(player);

            DebugSystem.Write($"[Guild] Player {player.CharName} joined guild '{GuildName}' (ID: {GuildID}).");
        }

        public bool RemoveMember(uint charId)
        {
            GuildMember removed = null;
            lock (_lock)
            {
                if (Members.TryGetValue(charId, out removed))
                {
                    Members.Remove(charId);
                    if (removed.PlayerRef != null)
                    {
                        removed.PlayerRef.CurGuild = null;

                        // Send clear guild UI packet to removed player
                        SendPacket clean = new SendPacket();
                        clean.Pack8(39);
                        clean.Pack8(1);
                        clean.Pack8(0);
                        removed.PlayerRef.Send(clean);

                        // Clear Insignia badge
                        SendPacket clearBadge = new SendPacket();
                        clearBadge.Pack8(39);
                        clearBadge.Pack8(30);
                        clearBadge.Pack32(charId);
                        clearBadge.Pack32(0);
                        clearBadge.PackString("");
                        removed.PlayerRef.CurMap?.Broadcast(clearBadge);
                    }
                }
            }

            if (removed != null)
            {
                BroadcastMemberList();
                DebugSystem.Write($"[Guild] Member {removed.CharName} (ID: {charId}) removed from guild '{GuildName}'.");
                return true;
            }
            return false;
        }

        public void SendInfo(Player player)
        {
            if (player == null) return;

            // 1. AC 39:1 [00] Clean Tab
            SendPacket sClean = new SendPacket();
            sClean.Pack8(39);
            sClean.Pack8(1);
            sClean.Pack8(0);
            player.Send(sClean);

            // 2. AC 39:8 Member List & Info
            SendMemberListTo(player);

            // 3. AC 39:30 Insignia Badge
            SendInsignia(player);
        }

        public void SendInsignia(Player player)
        {
            if (player == null) return;

            SendPacket badge = new SendPacket();
            badge.Pack8(39);
            badge.Pack8(30);
            badge.Pack32(player.CharID);
            badge.Pack32(Icon);
            badge.PackString(GuildName);

            if (player.CurMap != null)
                player.CurMap.Broadcast(badge);
            else
                player.Send(badge);
        }

        public void BroadcastMemberList()
        {
            lock (_lock)
            {
                foreach (var member in Members.Values)
                {
                    if (member.PlayerRef != null && member.IsOnline)
                    {
                        SendMemberListTo(member.PlayerRef);
                    }
                }
            }
        }

        private void SendMemberListTo(Player target)
        {
            if (target == null) return;

            lock (_lock)
            {
                SendPacket s = new SendPacket();
                s.Pack8(39);
                s.Pack8(8);
                s.PackString(GuildName);
                s.Pack32(Icon);
                s.PackString(LeaderName);
                s.Pack8((byte)Members.Count);

                foreach (var m in Members.Values)
                {
                    s.Pack32(m.CharID);
                    s.PackString(m.CharName ?? $"Player #{m.CharID}");
                    s.Pack8((byte)m.Level);
                    s.Pack8(m.Job);
                    s.Pack8(m.Element);
                    s.Pack8((byte)m.Rank);
                    s.Pack8((byte)(m.IsOnline ? 1 : 0));
                }

                s.PackString(Rules ?? "");
                target.Send(s);
            }
        }

        public void BroadCastGuild(SendPacket spk, uint exceptCharId = 0)
        {
            if (spk == null) return;

            lock (_lock)
            {
                foreach (var member in Members.Values)
                {
                    if (member.CharID != exceptCharId && member.PlayerRef != null && member.IsOnline)
                    {
                        member.PlayerRef.Send(spk);
                    }
                }
            }
        }

        public void SetNotice(string notice)
        {
            Rules = notice ?? "";
            BroadcastMemberList();
        }

        public ushort ID => GuildID;
        public GuildMember Leader => Members.TryGetValue(LeaderID, out var l) ? l : new GuildMember { CharID = LeaderID, CharName = LeaderName };

        public void AddNewMemberGuild(Player player, uint m) => AddMember(player);
        public void LeaveGuild(Player player) { if (player != null) RemoveMember(player.CharID); }
        public void Dismiss(uint target, uint callerId) => RemoveMember(target);
        public void BroadCast(ushort guildId, string msg, uint senderId)
        {
            SendPacket s = new SendPacket();
            s.Pack8(39);
            s.Pack8(8);
            s.PackString(msg);
            BroadCastGuild(s);
        }
        public void Edit_Rule(string rule) => SetNotice(rule);
        public void HoldThePostOfViceOrgleader(Player p, uint target)
        {
            lock (_lock)
            {
                if (Members.TryGetValue(target, out var m))
                {
                    m.Rank = GuildMemberRank.ViceLeader;
                    BroadcastMemberList();
                }
            }
        }
        public void RemoveHoldThePostOfViceOrgleader(Player p, uint target)
        {
            lock (_lock)
            {
                if (Members.TryGetValue(target, out var m))
                {
                    m.Rank = GuildMemberRank.Member;
                    BroadcastMemberList();
                }
            }
        }
        public void Permission(uint target, Player p, byte pos)
        {
            lock (_lock)
            {
                if (Members.TryGetValue(target, out var m))
                {
                    m.Rank = (GuildMemberRank)pos;
                    BroadcastMemberList();
                }
            }
        }
        public void ChangeInsigna(byte[] a)
        {
            if (a != null && a.Length >= 4)
            {
                Icon = BitConverter.ToUInt32(a, 0);
                BroadcastMemberList();
            }
        }
    }

    public static class GuildManager
    {
        private static readonly Dictionary<ushort, Guild> _guilds = new Dictionary<ushort, Guild>();
        private static readonly object _lock = new object();
        private static ushort _nextGuildId = 1000;

        public static void Initialize()
        {
            lock (_lock)
            {
                _guilds.Clear();
                _nextGuildId = 1000;
            }
            LoadFromDatabase();
        }

        public static IReadOnlyDictionary<ushort, Guild> GetAllGuilds()
        {
            lock (_lock)
            {
                return new Dictionary<ushort, Guild>(_guilds);
            }
        }

        public static bool AdminUpdateRules(ushort guildId, string rules)
        {
            lock (_lock)
            {
                if (_guilds.TryGetValue(guildId, out var g))
                {
                    g.Rules = rules ?? "";
                    SaveGuild(g);
                    return true;
                }
            }
            return false;
        }

        public static bool AdminChangeLeader(ushort guildId, uint newLeaderCharId)
        {
            lock (_lock)
            {
                if (_guilds.TryGetValue(guildId, out var g) && g.Members.TryGetValue(newLeaderCharId, out var newLeader))
                {
                    if (g.Members.TryGetValue(g.LeaderID, out var oldLeader))
                    {
                        oldLeader.Rank = GuildMemberRank.Member;
                    }
                    newLeader.Rank = GuildMemberRank.Leader;
                    g.LeaderID = newLeader.CharID;
                    g.LeaderName = newLeader.CharName;
                    SaveGuild(g);
                    return true;
                }
            }
            return false;
        }

        public static bool AdminKickMember(ushort guildId, uint memberCharId)
        {
            lock (_lock)
            {
                if (_guilds.TryGetValue(guildId, out var g))
                {
                    g.RemoveMember(memberCharId);
                    SaveGuild(g);
                    return true;
                }
            }
            return false;
        }

        public static bool AdminDisbandGuild(ushort guildId)
        {
            lock (_lock)
            {
                if (_guilds.TryGetValue(guildId, out var guild))
                {
                    var memberIds = guild.Members.Keys.ToList();
                    foreach (var id in memberIds)
                    {
                        guild.RemoveMember(id);
                    }
                    _guilds.Remove(guildId);
                    DeleteGuildFromDatabase(guildId);
                    return true;
                }
            }
            return false;
        }

        public static Guild GetGuild(ushort guildId)
        {
            lock (_lock)
            {
                _guilds.TryGetValue(guildId, out var g);
                return g;
            }
        }

        public static Guild CreateGuild(Player leader, string guildName, uint icon = 3402)
        {
            if (leader == null || string.IsNullOrWhiteSpace(guildName)) return null;

            guildName = guildName.Trim();
            if (guildName.Length > 20) guildName = guildName.Substring(0, 20);

            lock (_lock)
            {
                // Check name duplication
                if (_guilds.Values.Any(g => g.GuildName.Equals(guildName, StringComparison.OrdinalIgnoreCase)))
                {
                    // Name already exists
                    SendPacket pErr = new SendPacket();
                    pErr.Pack8(39);
                    pErr.Pack8(255); // Error packet
                    pErr.PackString("A guild with this name already exists!");
                    leader.Send(pErr);
                    return null;
                }

                while (_guilds.ContainsKey(_nextGuildId))
                {
                    _nextGuildId++;
                }

                ushort newId = _nextGuildId++;
                var guild = new Guild(newId, guildName, leader, icon);
                _guilds[newId] = guild;

                leader.CurGuild = guild;
                guild.SendInfo(leader);

                // Save to database
                SaveGuild(guild);
                SaveMember(newId, leader.CharID, leader.CharName, GuildMemberRank.Leader, leader.Level, (byte)leader.Job, (byte)leader.Element);

                DebugSystem.Write($"[GuildManager] Guild '{guildName}' (ID: {newId}) successfully created by {leader.CharName}.");
                return guild;
            }
        }

        public static void HandlePlayerLogin(Player player, ushort guildId)
        {
            if (player == null || guildId == 0) return;

            lock (_lock)
            {
                if (_guilds.TryGetValue(guildId, out var guild))
                {
                    if (guild.Members.TryGetValue(player.CharID, out var member))
                    {
                        member.PlayerRef = player;
                        member.Level = player.Level;
                        member.Job = (byte)player.Job;
                        member.Element = (byte)player.Element;
                    }
                    else
                    {
                        guild.Members[player.CharID] = new GuildMember(player);
                    }

                    player.CurGuild = guild;
                    guild.SendInfo(player);
                }
            }
        }

        public static void DisbandGuild(Player leader)
        {
            if (leader == null || leader.CurGuild == null) return;
            var guild = leader.CurGuild;

            if (!guild.IsLeader(leader.CharID)) return;

            lock (_lock)
            {
                // Remove all members
                var memberIds = guild.Members.Keys.ToList();
                foreach (var id in memberIds)
                {
                    guild.RemoveMember(id);
                }

                _guilds.Remove(guild.GuildID);
            }

            // Remove from database
            DeleteGuildFromDatabase(guild.GuildID);
            DebugSystem.Write($"[GuildManager] Guild '{guild.GuildName}' (ID: {guild.GuildID}) disbanded by leader {leader.CharName}.");
        }

        public static void LoadFromDatabase()
        {
            try
            {
                VerifyTables();

                var dtGuilds = RCLibrary.Core.DataBase.Query("SELECT * FROM guilds ORDER BY guild_id;");
                if (dtGuilds == null || dtGuilds.Rows.Count == 0)
                {
                    // Check migration from guilds.txt
                    string configPath = RCLibrary.Core.PathHelper.GetDataFilePath("guilds.txt");
                    if (File.Exists(configPath))
                    {
                        var lines = File.ReadAllLines(configPath, Encoding.UTF8);
                        foreach (var rawLine in lines)
                        {
                            string line = rawLine.Trim();
                            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                            var parts = line.Split('|');
                            if (parts.Length >= 4 && parts[0].StartsWith("GUILD:"))
                            {
                                if (ushort.TryParse(parts[0].Substring(6), out ushort gid))
                                {
                                    string gname = parts[1];
                                    uint.TryParse(parts[2], out uint leaderId);
                                    string leaderName = parts[3];
                                    uint icon = parts.Length > 4 && uint.TryParse(parts[4], out uint ic) ? ic : 3402;
                                    string rules = parts.Length > 5 ? parts[5] : "";

                                    var g = new Guild
                                    {
                                        GuildID = gid,
                                        GuildName = gname,
                                        LeaderID = leaderId,
                                        LeaderName = leaderName,
                                        Icon = icon,
                                        Rules = rules
                                    };
                                    SaveGuild(g);
                                }
                            }
                        }
                    }
                    dtGuilds = RCLibrary.Core.DataBase.Query("SELECT * FROM guilds ORDER BY guild_id;");
                }

                lock (_lock)
                {
                    _guilds.Clear();
                    if (dtGuilds != null)
                    {
                        foreach (System.Data.DataRow row in dtGuilds.Rows)
                        {
                            ushort gid = Convert.ToUInt16(row["guild_id"]);
                            var g = new Guild
                            {
                                GuildID = gid,
                                GuildName = row["guild_name"]?.ToString() ?? "",
                                LeaderID = Convert.ToUInt32(row["leader_id"]),
                                LeaderName = row["leader_name"]?.ToString() ?? "",
                                Icon = Convert.ToUInt32(row["icon"]),
                                Rules = row["rules"]?.ToString() ?? ""
                            };
                            _guilds[gid] = g;
                            if (gid >= _nextGuildId) _nextGuildId = (ushort)(gid + 1);
                        }
                    }

                    // Load members
                    var dtMembers = RCLibrary.Core.DataBase.Query("SELECT * FROM guild_members;");
                    if (dtMembers != null)
                    {
                        foreach (System.Data.DataRow row in dtMembers.Rows)
                        {
                            ushort gid = Convert.ToUInt16(row["guild_id"]);
                            if (_guilds.TryGetValue(gid, out var g))
                            {
                                var mem = new GuildMember
                                {
                                    CharID = Convert.ToUInt32(row["char_id"]),
                                    CharName = row["char_name"]?.ToString() ?? "",
                                    Rank = (GuildMemberRank)Convert.ToInt32(row["rank"]),
                                    Level = Convert.ToInt32(row["level"]),
                                    Job = Convert.ToByte(row["job"]),
                                    Element = Convert.ToByte(row["element"])
                                };
                                g.Members[mem.CharID] = mem;
                            }
                        }
                    }
                }

                DebugSystem.Write($"[GuildManager] Loaded {_guilds.Count} guilds from SQLite database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GuildManager] Error in LoadFromDatabase: {ex.Message}");
            }
        }

        public static void VerifyTables()
        {
            try
            {
                RCLibrary.Core.DataBase.Execute(@"CREATE TABLE IF NOT EXISTS guilds (
                    guild_id INTEGER PRIMARY KEY,
                    guild_name TEXT NOT NULL,
                    leader_id INT NOT NULL,
                    leader_name TEXT,
                    icon INT DEFAULT 3402,
                    rules TEXT,
                    created_date TEXT
                );");

                RCLibrary.Core.DataBase.Execute(@"CREATE TABLE IF NOT EXISTS guild_members (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    guild_id INT NOT NULL,
                    char_id INT NOT NULL,
                    char_name TEXT,
                    rank INT DEFAULT 0,
                    level INT DEFAULT 1,
                    job INT DEFAULT 0,
                    element INT DEFAULT 0,
                    joined_date TEXT,
                    UNIQUE(guild_id, char_id)
                );");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GuildManager] Error verifying tables: {ex.Message}");
            }
        }

        public static void SaveGuild(Guild g)
        {
            if (g == null) return;
            try
            {
                VerifyTables();
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string sql = $"INSERT OR REPLACE INTO guilds (guild_id, guild_name, leader_id, leader_name, icon, rules, created_date) VALUES ({g.GuildID}, '{g.GuildName.Replace("'", "''")}', {g.LeaderID}, '{g.LeaderName.Replace("'", "''")}', {g.Icon}, '{g.Rules.Replace("'", "''")}', '{now}');";
                RCLibrary.Core.DataBase.Execute(sql);

                // Also save leader as member
                SaveMember(g.GuildID, g.LeaderID, g.LeaderName, GuildMemberRank.Leader, 1, 0, 0);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GuildManager] Error saving guild: {ex.Message}");
            }
        }

        public static void SaveMember(ushort guildId, uint charId, string charName, GuildMemberRank rank, int level, byte job, byte element)
        {
            try
            {
                VerifyTables();
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string sql = $"INSERT OR REPLACE INTO guild_members (guild_id, char_id, char_name, rank, level, job, element, joined_date) VALUES ({guildId}, {charId}, '{charName?.Replace("'", "''")}', {(int)rank}, {level}, {job}, {element}, '{now}');";
                RCLibrary.Core.DataBase.Execute(sql);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GuildManager] Error saving guild member: {ex.Message}");
            }
        }

        public static void DeleteGuildFromDatabase(ushort guildId)
        {
            try
            {
                VerifyTables();
                RCLibrary.Core.DataBase.Execute($"DELETE FROM guilds WHERE guild_id = {guildId};");
                RCLibrary.Core.DataBase.Execute($"DELETE FROM guild_members WHERE guild_id = {guildId};");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GuildManager] Error deleting guild: {ex.Message}");
            }
        }
    }
}
