using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Network;
using Game.Code;
using Game.Maps;

namespace Game.PlayerRelated
{
    public class MarriageRecord
    {
        public uint HusbandID { get; set; }
        public string HusbandName { get; set; } = string.Empty;
        public uint WifeID { get; set; }
        public string WifeName { get; set; } = string.Empty;
        public DateTime MarriageDate { get; set; } = DateTime.UtcNow;

        public MarriageRecord() { }

        public MarriageRecord(uint hId, string hName, uint wId, string wName)
        {
            HusbandID = hId;
            HusbandName = hName;
            WifeID = wId;
            WifeName = wName;
            MarriageDate = DateTime.UtcNow;
        }
    }

    public static class MarriageManager
    {
        private static readonly Dictionary<uint, MarriageRecord> _marriagesByCharId = new Dictionary<uint, MarriageRecord>();
        private static readonly Dictionary<uint, uint> _pendingProposals = new Dictionary<uint, uint>(); // TargetID -> ProposerID
        private static readonly object _lock = new object();
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "marriages.txt");

        public static void Initialize()
        {
            LoadFromDatabase();
        }

        public static List<MarriageRecord> GetAllMarriages()
        {
            lock (_lock)
            {
                var set = new HashSet<uint>();
                var list = new List<MarriageRecord>();
                foreach (var m in _marriagesByCharId.Values)
                {
                    if (set.Add(m.HusbandID))
                    {
                        list.Add(m);
                    }
                }
                return list;
            }
        }

        public static bool AdminDivorce(uint charId)
        {
            lock (_lock)
            {
                if (_marriagesByCharId.TryGetValue(charId, out var m))
                {
                    _marriagesByCharId.Remove(m.HusbandID);
                    _marriagesByCharId.Remove(m.WifeID);
                    DeleteMarriage(m.HusbandID, m.WifeID);
                    return true;
                }
            }
            return false;
        }

        public static bool IsMarried(uint charId)
        {
            lock (_lock)
            {
                return _marriagesByCharId.ContainsKey(charId);
            }
        }

        public static MarriageRecord GetMarriage(uint charId)
        {
            lock (_lock)
            {
                _marriagesByCharId.TryGetValue(charId, out var m);
                return m;
            }
        }

        public static bool Propose(Player proposer, Player target)
        {
            if (proposer == null || target == null) return false;

            // 1. Level Requirement (Lv 30+)
            if (proposer.Level < 30 || target.Level < 30)
            {
                SendSystemMessage(proposer, "Requires Level 30 or higher to marry!");
                return false;
            }

            // 2. Already Married Check
            if (IsMarried(proposer.CharID))
            {
                SendSystemMessage(proposer, "You are already married!");
                return false;
            }

            if (IsMarried(target.CharID))
            {
                SendSystemMessage(proposer, "That player is already married!");
                return false;
            }

            // 3. Fee (60,000 gold)
            if (proposer.Gold < 60000)
            {
                SendSystemMessage(proposer, "You need at least 60,000 gold for the wedding ceremony!");
                return false;
            }

            lock (_lock)
            {
                _pendingProposals[target.CharID] = proposer.CharID;
            }

            // 4. Send Proposal Dialog to Target
            SendPacket pProp = new SendPacket();
            pProp.Pack8(23);
            pProp.Pack8(57);
            pProp.Pack8(0);
            pProp.PackString($"{proposer.CharName} has proposed marriage to you! Type /acceptmarry or /declinemarry");
            target.Send(pProp);

            SendSystemMessage(proposer, $"Marriage proposal sent to {target.CharName}!");
            return true;
        }

        public static bool AcceptProposal(Player target)
        {
            if (target == null) return false;
            uint proposerId = 0;

            lock (_lock)
            {
                if (!_pendingProposals.TryGetValue(target.CharID, out proposerId))
                {
                    SendSystemMessage(target, "You do not have any pending marriage proposals.");
                    return false;
                }
                _pendingProposals.Remove(target.CharID);
            }

            // Locate proposer player
            Player proposer = null;
            if (target.CurMap is GameMap curMap)
            {
                proposer = curMap.PlayersList.FirstOrDefault(p => p.CharID == proposerId);
            }

            if (proposer == null)
            {
                SendSystemMessage(target, "Your partner is no longer nearby.");
                return false;
            }

            // Final Checks
            if (proposer.Gold < 60000)
            {
                SendSystemMessage(proposer, "Not enough gold for the wedding fee (60,000g).");
                SendSystemMessage(target, "Your partner does not have enough gold for the ceremony.");
                return false;
            }

            proposer.TakeGold(60000);

            // Register Marriage
            var record = new MarriageRecord(proposer.CharID, proposer.CharName, target.CharID, target.CharName);
            lock (_lock)
            {
                _marriagesByCharId[proposer.CharID] = record;
                _marriagesByCharId[target.CharID] = record;
            }

            proposer.SpouseID = target.CharID;
            proposer.SpouseName = target.CharName;

            target.SpouseID = proposer.CharID;
            target.SpouseName = proposer.CharName;

            // Grant Wedding Rings (Item #49001 / #49002)
            proposer.Inv.AddItem(49001, 1);
            target.Inv.AddItem(49002, 1);

            // Broadcast Wedding Ceremony Fireworks & Visuals to Map
            SendPacket pFireworks = new SendPacket();
            pFireworks.Pack8(5);
            pFireworks.Pack8(5);
            pFireworks.Pack32(proposer.CharID);
            pFireworks.Pack16(60010); // Wedding fireworks visual effect
            target.CurMap?.Broadcast(pFireworks);

            // Global Notification Prompt
            SendPacket pMsg = new SendPacket();
            pMsg.Pack8(23);
            pMsg.Pack8(57);
            pMsg.Pack8(0);
            pMsg.PackString($"Congratulations! {proposer.CharName} and {target.CharName} are now happily married!");
            target.CurMap?.Broadcast(pMsg);

            // Save to database
            SaveMarriage(record);

            DebugSystem.Write($"[MarriageManager] {proposer.CharName} & {target.CharName} were successfully married!");
            return true;
        }

        public static void DeclineProposal(Player target)
        {
            if (target == null) return;
            lock (_lock)
            {
                if (_pendingProposals.TryGetValue(target.CharID, out uint proposerId))
                {
                    _pendingProposals.Remove(target.CharID);
                    SendSystemMessage(target, "You declined the marriage proposal.");
                }
            }
        }

        public static bool Divorce(Player player)
        {
            if (player == null || player.SpouseID == 0)
            {
                SendSystemMessage(player, "You are not married.");
                return false;
            }

            uint spouseId = player.SpouseID;
            lock (_lock)
            {
                _marriagesByCharId.Remove(player.CharID);
                _marriagesByCharId.Remove(spouseId);
            }

            player.SpouseID = 0;
            player.SpouseName = string.Empty;

            // Locate spouse if online and clear
            if (player.CurMap is GameMap curMap)
            {
                var spouse = curMap.PlayersList.FirstOrDefault(p => p.CharID == spouseId);
                if (spouse != null)
                {
                    spouse.SpouseID = 0;
                    spouse.SpouseName = string.Empty;
                    SendSystemMessage(spouse, "Your marriage has been annulled.");
                }
            }

            DeleteMarriage(player.CharID, spouseId);
            SendSystemMessage(player, "You have divorced successfully.");
            DebugSystem.Write($"[MarriageManager] Player {player.CharName} divorced from Spouse #{spouseId}.");
            return true;
        }

        public static bool TeleportToSpouse(Player player)
        {
            if (player == null || player.SpouseID == 0)
            {
                SendSystemMessage(player, "You must be married to use Warp to Spouse!");
                return false;
            }

            // Find spouse across maps
            Player spouse = null;
            if (player.CurMap is GameMap curMap)
            {
                spouse = curMap.PlayersList.FirstOrDefault(p => p.CharID == player.SpouseID);
            }

            if (spouse == null || spouse.CurMap == null)
            {
                SendSystemMessage(player, "Your spouse is not online or cannot be reached.");
                return false;
            }

            // Teleport to spouse coordinates
            WarpData warp = new WarpData
            {
                DstMap = (ushort)spouse.CurMap.MapID,
                DstX_Axis = (ushort)spouse.CurX,
                DstY_Axis = (ushort)spouse.CurY
            };

            player.CurMap.Teleport(TeleportType.CmD, player, 0, warp);
            SendSystemMessage(player, $"Teleported to your spouse {spouse.CharName}!");
            return true;
        }

        private static void SendSystemMessage(Player p, string message)
        {
            if (p == null || string.IsNullOrEmpty(message)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(message);
            p.Send(s);
        }

        public static void LoadFromDatabase()
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (File.Exists(ConfigPath))
                {
                    var lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                    lock (_lock)
                    {
                        _marriagesByCharId.Clear();
                        foreach (var rawLine in lines)
                        {
                            string line = rawLine.Trim();
                            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                            // Format: HUSBAND_ID|HUSBAND_NAME|WIFE_ID|WIFE_NAME|DATE
                            var parts = line.Split('|');
                            if (parts.Length >= 4)
                            {
                                if (uint.TryParse(parts[0], out uint hId) && uint.TryParse(parts[2], out uint wId))
                                {
                                    var record = new MarriageRecord(hId, parts[1], wId, parts[3]);
                                    if (parts.Length > 4 && DateTime.TryParse(parts[4], out DateTime d))
                                        record.MarriageDate = d;

                                    _marriagesByCharId[hId] = record;
                                    _marriagesByCharId[wId] = record;
                                }
                            }
                        }
                    }
                }
                DebugSystem.Write($"[MarriageManager] Loaded {_marriagesByCharId.Count / 2} marriage records.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MarriageManager] Error loading marriages: {ex.Message}");
            }
        }

        private static void SaveMarriage(MarriageRecord record)
        {
            try
            {
                lock (_lock)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# WLO Marriage Database File");
                    var written = new HashSet<uint>();

                    foreach (var m in _marriagesByCharId.Values)
                    {
                        if (!written.Contains(m.HusbandID))
                        {
                            written.Add(m.HusbandID);
                            sb.AppendLine($"{m.HusbandID}|{m.HusbandName}|{m.WifeID}|{m.WifeName}|{m.MarriageDate:O}");
                        }
                    }
                    File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MarriageManager] Error saving marriages: {ex.Message}");
            }
        }

        private static void DeleteMarriage(uint id1, uint id2)
        {
            try
            {
                lock (_lock)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# WLO Marriage Database File");
                    var written = new HashSet<uint>();

                    foreach (var m in _marriagesByCharId.Values)
                    {
                        if (m.HusbandID != id1 && m.WifeID != id1 && m.HusbandID != id2 && m.WifeID != id2)
                        {
                            if (!written.Contains(m.HusbandID))
                            {
                                written.Add(m.HusbandID);
                                sb.AppendLine($"{m.HusbandID}|{m.HusbandName}|{m.WifeID}|{m.WifeName}|{m.MarriageDate:O}");
                            }
                        }
                    }
                    File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MarriageManager] Error updating marriages after divorce: {ex.Message}");
            }
        }
    }
}
