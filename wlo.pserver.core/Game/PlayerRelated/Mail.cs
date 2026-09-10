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
        private static string ConfigPath => RCLibrary.Core.PathHelper.GetDataFilePath("mails.txt");

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
                    RCLibrary.Core.DataBase.Execute($"DELETE FROM mails WHERE mail_id = {mailId};");
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

        public static void VerifyTable()
        {
            try
            {
                RCLibrary.Core.DataBase.Execute(@"CREATE TABLE IF NOT EXISTS mails (
                    mail_id INTEGER PRIMARY KEY AUTOINCREMENT,
                    sender_id INT DEFAULT 0,
                    sender_name TEXT,
                    receiver_id INT NOT NULL,
                    subject TEXT,
                    content TEXT,
                    gold INT DEFAULT 0,
                    item_id INT DEFAULT 0,
                    count INT DEFAULT 0,
                    date TEXT,
                    is_read INT DEFAULT 0,
                    is_claimed INT DEFAULT 0
                );");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MailSystem] Error verifying mails table: {ex.Message}");
            }
        }

        public static void LoadFromDatabase()
        {
            try
            {
                VerifyTable();
                var dt = RCLibrary.Core.DataBase.Query("SELECT * FROM mails ORDER BY mail_id;");

                if (dt == null || dt.Rows.Count == 0)
                {
                    // Check migration from mails.txt
                    if (File.Exists(ConfigPath))
                    {
                        var lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                        foreach (var rawLine in lines)
                        {
                            string line = rawLine.Trim();
                            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

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
                                    SaveMail(m);
                                }
                            }
                        }
                    }
                    dt = RCLibrary.Core.DataBase.Query("SELECT * FROM mails ORDER BY mail_id;");
                }

                lock (_lock)
                {
                    _inboxes.Clear();
                    if (dt != null)
                    {
                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            uint mId = Convert.ToUInt32(row["mail_id"]);
                            uint sId = Convert.ToUInt32(row["sender_id"]);
                            string sName = row["sender_name"]?.ToString() ?? "";
                            uint rId = Convert.ToUInt32(row["receiver_id"]);
                            string subj = row["subject"]?.ToString() ?? "";
                            string cont = row["content"]?.ToString() ?? "";
                            uint gold = Convert.ToUInt32(row["gold"]);
                            ushort itId = Convert.ToUInt16(row["item_id"]);
                            byte count = Convert.ToByte(row["count"]);
                            bool isRead = Convert.ToInt32(row["is_read"]) == 1;
                            bool isClaimed = Convert.ToInt32(row["is_claimed"]) == 1;

                            var m = new MailMessage(mId, sId, sName, rId, subj, cont, gold, itId, count)
                            {
                                IsRead = isRead,
                                IsClaimed = isClaimed
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

                DebugSystem.Write($"[MailSystem] Loaded {_inboxes.Values.Sum(l => l.Count)} mails from SQLite database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MailSystem] Error loading mails from database: {ex.Message}");
            }
        }

        private static void SaveMail(MailMessage m)
        {
            if (m == null) return;
            try
            {
                VerifyTable();
                string now = m.SentDate.ToString("yyyy-MM-dd HH:mm:ss");
                string sql = $@"INSERT OR REPLACE INTO mails 
                    (mail_id, sender_id, sender_name, receiver_id, subject, content, gold, item_id, count, date, is_read, is_claimed)
                    VALUES ({m.MailID}, {m.SenderID}, '{m.SenderName.Replace("'", "''")}', {m.ReceiverID}, '{m.Subject.Replace("'", "''")}', '{m.Content.Replace("'", "''")}', {m.AttachedGold}, {m.AttachedItemID}, {m.AttachedItemCount}, '{now}', {(m.IsRead ? 1 : 0)}, {(m.IsClaimed ? 1 : 0)});";
                RCLibrary.Core.DataBase.Execute(sql);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MailSystem] Error saving mail to database: {ex.Message}");
            }
        }
    }
}
