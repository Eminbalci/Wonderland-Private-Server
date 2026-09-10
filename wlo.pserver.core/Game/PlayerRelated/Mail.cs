using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Network;

namespace Game.PlayerRelated
{
    public class MailMessage
    {
        public uint MailID { get; set; }
        public uint SenderID { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public uint ReceiverID { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public uint AttachedGold { get; set; } = 0;
        public ushort AttachedItemID { get; set; } = 0;
        public byte AttachedItemCount { get; set; } = 0;
        public DateTime SentDate { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public bool IsClaimed { get; set; } = false;

        public MailMessage() { }

        public MailMessage(uint id, uint sId, string sName, uint rId, string subject, string content, uint gold = 0, ushort itemId = 0, byte count = 0)
        {
            MailID = id;
            SenderID = sId;
            SenderName = sName;
            ReceiverID = rId;
            Subject = subject;
            Content = content;
            AttachedGold = gold;
            AttachedItemID = itemId;
            AttachedItemCount = count;
            SentDate = DateTime.UtcNow;
        }
    }

    public static class MailSystem
    {
        private static readonly Dictionary<uint, List<MailMessage>> _inboxes = new Dictionary<uint, List<MailMessage>>(); // ReceiverID -> List of mails
        private static readonly object _lock = new object();
        private static uint _nextMailId = 1;
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "mails.txt");

        public static void Initialize()
        {
            LoadFromDatabase();
        }

        public static List<MailMessage> GetAllMails()
        {
            lock (_lock)
            {
                var all = new List<MailMessage>();
                foreach (var list in _inboxes.Values)
                {
                    all.AddRange(list);
                }
                return all.OrderByDescending(m => m.MailID).ToList();
            }
        }

        public static bool AdminDispatchMail(uint targetCharId, string senderName, string subject, string content, uint gold = 0, ushort itemId = 0, byte count = 0)
        {
            try
            {
                MailMessage msg;
                lock (_lock)
                {
                    uint mailId = _nextMailId++;
                    msg = new MailMessage(mailId, 0, string.IsNullOrWhiteSpace(senderName) ? "System GM" : senderName, targetCharId, subject, content, gold, itemId, count);

                    if (!_inboxes.TryGetValue(targetCharId, out var list))
                    {
                        list = new List<MailMessage>();
                        _inboxes[targetCharId] = list;
                    }
                    list.Add(msg);
                }

                SaveMail(msg);
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MailSystem] Error in AdminDispatchMail: {ex.Message}");
                return false;
            }
        }

        public static bool AdminDeleteMail(uint mailId)
        {
            lock (_lock)
            {
                bool found = false;
                foreach (var list in _inboxes.Values)
                {
                    if (list.RemoveAll(m => m.MailID == mailId) > 0)
                    {
                        found = true;
                    }
                }
                if (found)
                {
                    SaveMail(null);
                    return true;
                }
            }
            return false;
        }

        public static bool SendMail(Player sender, uint targetCharId, string subject, string content, uint gold = 0, ushort itemId = 0, byte count = 0)
        {
            if (sender == null || string.IsNullOrWhiteSpace(subject)) return false;

            // Check attachments
            if (gold > 0)
            {
                if (sender.Gold < (int)gold)
                {
                    SendSystemMsg(sender, "Not enough gold to attach to mail!");
                    return false;
                }
                sender.TakeGold((int)gold);
            }

            if (itemId > 0 && count > 0)
            {
                // Item will be sent from inventory
                sender.Inv.RemoveItemById(itemId, count);
            }

            MailMessage msg;
            lock (_lock)
            {
                uint mailId = _nextMailId++;
                msg = new MailMessage(mailId, sender.CharID, sender.CharName, targetCharId, subject, content, gold, itemId, count);

                if (!_inboxes.TryGetValue(targetCharId, out var list))
                {
                    list = new List<MailMessage>();
                    _inboxes[targetCharId] = list;
                }
                list.Add(msg);
            }

            SaveMail(msg);

            // Notify online receiver
            if (sender.CurMap is GameMap curMap)
            {
                var receiver = curMap.PlayersList.FirstOrDefault(p => p.CharID == targetCharId);
                if (receiver != null)
                {
                    SendPacket pNotify = new SendPacket();
                    pNotify.Pack8(23);
                    pNotify.Pack8(57);
                    pNotify.Pack8(0);
                    pNotify.PackString($"You have received a new letter from {sender.CharName}!");
                    receiver.Send(pNotify);
                }
            }

            SendSystemMsg(sender, "Letter sent successfully!");
            DebugSystem.Write($"[MailSystem] Mail #{msg.MailID} sent from {sender.CharName} to CharID #{targetCharId}.");
            return true;
        }

        public static void OpenInbox(Player player)
        {
            if (player == null) return;

            List<MailMessage> list = null;
            lock (_lock)
            {
                if (_inboxes.TryGetValue(player.CharID, out var mails))
                {
                    list = new List<MailMessage>(mails);
                }
            }

            // Sync Mail List
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(76); // Mail list ActionCode
            p.Pack8((byte)(list?.Count ?? 0));

            if (list != null)
            {
                foreach (var m in list)
                {
                    p.Pack32(m.MailID);
                    p.Pack32(m.SenderID);
                    p.PackString(m.SenderName);
                    p.PackString(m.Subject);
                    p.Pack8((byte)(m.IsRead ? 1 : 0));
                    p.Pack8((byte)(m.IsClaimed ? 1 : 0));
                    p.Pack32(m.AttachedGold);
                    p.Pack16(m.AttachedItemID);
                    p.Pack8(m.AttachedItemCount);
                }
            }

            player.Send(p);
        }

        public static void ReadMail(Player player, uint mailId)
        {
            if (player == null) return;

            MailMessage target = null;
            lock (_lock)
            {
                if (_inboxes.TryGetValue(player.CharID, out var list))
                {
                    target = list.FirstOrDefault(m => m.MailID == mailId);
                    if (target != null) target.IsRead = true;
                }
            }

            if (target != null)
            {
                SendPacket p = new SendPacket();
                p.Pack8(23);
                p.Pack8(77); // Mail details
                p.Pack32(target.MailID);
                p.PackString(target.Content);
                player.Send(p);
            }
        }

        public static void ClaimAttachment(Player player, uint mailId)
        {
            if (player == null) return;

            MailMessage target = null;
            lock (_lock)
            {
                if (_inboxes.TryGetValue(player.CharID, out var list))
                {
                    target = list.FirstOrDefault(m => m.MailID == mailId && !m.IsClaimed);
                }
            }

            if (target != null)
            {
                if (target.AttachedGold > 0)
                {
                    player.AddGold((int)target.AttachedGold);
                }
                if (target.AttachedItemID > 0 && target.AttachedItemCount > 0)
                {
                    player.Inv.AddItem(target.AttachedItemID, target.AttachedItemCount);
                }

                target.IsClaimed = true;
                target.AttachedGold = 0;
                target.AttachedItemID = 0;
                target.AttachedItemCount = 0;

                SendSystemMsg(player, "Claimed attachments from mail!");
                OpenInbox(player);
            }
        }

        public static void DeleteMail(Player player, uint mailId)
        {
            if (player == null) return;

            lock (_lock)
            {
                if (_inboxes.TryGetValue(player.CharID, out var list))
                {
                    list.RemoveAll(m => m.MailID == mailId);
                }
            }

            OpenInbox(player);
            SendSystemMsg(player, "Mail deleted.");
        }

        private static void SendSystemMsg(Player p, string msg)
        {
            if (p == null || string.IsNullOrEmpty(msg)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(msg);
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
                        _inboxes.Clear();
                        foreach (var rawLine in lines)
                        {
                            string line = rawLine.Trim();
                            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                            // Format: ID|SenderID|SenderName|ReceiverID|Subject|Content|Gold|ItemID|Count|Date|Read|Claimed
                            var parts = line.Split('|');
                            if (parts.Length >= 6)
                            {
                                if (uint.TryParse(parts[0], out uint mId) && uint.TryParse(parts[1], out uint sId) && uint.TryParse(parts[3], out uint rId))
                                {
                                    uint gold = parts.Length > 6 && uint.TryParse(parts[6], out uint g) ? g : 0;
                                    ushort itId = parts.Length > 7 && ushort.TryParse(parts[7], out ushort it) ? it : (ushort)0;
                                    byte count = parts.Length > 8 && byte.TryParse(parts[8], out byte c) ? c : (byte)0;
                                    bool read = parts.Length > 10 && bool.TryParse(parts[10], out bool rd) && rd;
                                    bool claimed = parts.Length > 11 && bool.TryParse(parts[11], out bool cl) && cl;

                                    var m = new MailMessage(mId, sId, parts[2], rId, parts[4], parts[5], gold, itId, count)
                                    {
                                        IsRead = read,
                                        IsClaimed = claimed
                                    };

                                    if (!_inboxes.TryGetValue(rId, out var list))
                                    {
                                        list = new List<MailMessage>();
                                        _inboxes[rId] = list;
                                    }
                                    list.Add(m);

                                    if (mId >= _nextMailId) _nextMailId = mId + 1;
                                }
                            }
                        }
                    }
                }
                DebugSystem.Write($"[MailSystem] Loaded {_inboxes.Values.Sum(l => l.Count)} mails into system.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MailSystem] Error loading mails: {ex.Message}");
            }
        }

        private static void SaveMail(MailMessage m)
        {
            try
            {
                lock (_lock)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# WLO Mails Database File");
                    foreach (var list in _inboxes.Values)
                    {
                        foreach (var msg in list)
                        {
                            sb.AppendLine($"{msg.MailID}|{msg.SenderID}|{msg.SenderName}|{msg.ReceiverID}|{msg.Subject}|{msg.Content}|{msg.AttachedGold}|{msg.AttachedItemID}|{msg.AttachedItemCount}|{msg.SentDate:O}|{msg.IsRead}|{msg.IsClaimed}");
                        }
                    }
                    File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MailSystem] Error saving mails: {ex.Message}");
            }
        }
    }
}
