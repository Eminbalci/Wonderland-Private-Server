using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Code;
using Game.Maps;
using Network;
using RCLibrary.Core.Networking;

namespace Network.ActionCodes
{
    public class AC14 : AC
    {
        public override int ID { get { return 14; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            DebugSystem.Write(DebugItemType.Error, $"[AC14] ProcessPkt called. SubCmd={r.B}");
            r.SetPtr(6);

            switch (r.B)
            {
                case 1: Recv1(ref p, r); break; // Mail / In-game message / Friend Request by name
                case 2: Recv2(ref p, r); break; // Friend Request Send / Friend List Request
                case 3: Recv3(ref p, r); break; // Friend Accept
                case 4: Recv4(ref p, r); break; // Friend Remove / Friend List Request
                default:
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Unknown SubAction: {r.B}");
                    break;
            }
        }

        /// <summary>
        /// AC 14:1 - In-Game Mail / Direct Message (Confirmed via mailyolladimreplyyaptim.pcapng)
        /// C->S: 0e 01 <type(1B)> <targetCharId(4B)> <mailText>
        /// S->C: 0e 09 <targetCharId(4B)> 00 (Mail sent ACK)
        /// S->C: 0e 01 <senderCharId(4B)> <timestamp(8B)> <mailText> (Delivered to target)
        /// </summary>
        void Recv1(ref Player p, RecievePacket r)
        {
            try
            {
                var buf = r.Buffer.ToArray();
                // Check if this is a binary mail packet with target CharID (len >= 11)
                if (buf.Length >= 11 && buf[4] == 14 && buf[5] == 1)
                {
                    byte mailType = r.Unpack8();
                    uint targetCharID = r.Unpack32();

                    string mailContent = "";
                    int rawOffset = r.GetPtr();
                    if (rawOffset < buf.Length)
                    {
                        mailContent = Encoding.GetEncoding("big5").GetString(buf, rawOffset, buf.Length - rawOffset).TrimEnd('\0');
                    }

                    DebugSystem.Write($"[AC14.Recv1] Mail from {p.CharName} (#{p.CharID}) to CharID {targetCharID}: '{mailContent}'");

                    // 1. Send Mail Sent ACK to sender (AC 14:9 [14, 9, targetCharID, 0])
                    SendPacket ack = new SendPacket();
                    ack.Pack8(14);
                    ack.Pack8(9);
                    ack.Pack32(targetCharID);
                    ack.Pack8(0);
                    p.Send(ack);

                    // 2. Deliver live to recipient if online
                    Player target = cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.FirstOrDefault(x => x.CharID == targetCharID);
                    if (target == null && p.CurMap is GameMap gMap)
                    {
                        target = gMap.PlayersList.FirstOrDefault(x => x.CharID == targetCharID);
                    }

                    if (target != null)
                    {
                        SendPacket deliv = new SendPacket();
                        deliv.Pack8(14);
                        deliv.Pack8(1);
                        deliv.Pack32(p.CharID);
                        deliv.PackDouble(DateTime.Now.ToOADate());
                        deliv.PackString(mailContent);
                        target.Send(deliv);
                        DebugSystem.Write($"[AC14.Recv1] Delivered mail live to {target.CharName} (#{targetCharID})");
                    }

                    // 3. Persist mail in database
                    try
                    {
                        cGlobal.gGameDataBase?.ExecuteNonQuery(@"
                            CREATE TABLE IF NOT EXISTS Mail (
                                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                SenderID INTEGER NOT NULL,
                                SenderName TEXT NOT NULL,
                                ReceiverID INTEGER NOT NULL,
                                Content TEXT NOT NULL,
                                SendDate TEXT NOT NULL
                            )");
                        cGlobal.gGameDataBase?.ExecuteNonQuery(
                            $"INSERT INTO Mail (SenderID, SenderName, ReceiverID, Content, SendDate) VALUES ({p.CharID}, '{p.CharName?.Replace("'", "''")}', {targetCharID}, '{mailContent?.Replace("'", "''")}', datetime('now'))");
                    }
                    catch { }

                    return;
                }

                // Fallback: Add friend request by character name
                string targetName = r.UnpackString();
                DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv1] Add Friend Request by name from {p.CharName} to {targetName}");

                Player nameTarget = null;
                if (p.CurMap is GameMap map)
                {
                    nameTarget = map.PlayersList.FirstOrDefault(x => x.CharName == targetName);
                }
                if (nameTarget == null)
                {
                    nameTarget = cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.FirstOrDefault(x => x.CharName == targetName);
                }

                if (nameTarget != null)
                {
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 14, 2 });
                    s.Pack32(p.CharID);
                    nameTarget.Send(s);
                }
                else
                {
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 14, 1 });
                    s.Pack8(0);
                    p.Send(s);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Recv1 Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// AC 14:2 - Add Friend Request Send or Friend List Request (Confirmed via arkadaseklemeveonlinegozukme.pcapng)
        /// C->S: 0e 02 <targetCharId(4B)>
        /// S->C to target: 0e 02 <requesterCharId(4B)>
        /// </summary>
        void Recv2(ref Player p, RecievePacket r)
        {
            try
            {
                uint targetCharID = 0;
                var rawBuf = r.Buffer.ToArray();

                if (rawBuf.Length >= 10)
                {
                    targetCharID = BitConverter.ToUInt32(rawBuf, 6);
                }
                else if (r.GetPtr() + 4 <= rawBuf.Length)
                {
                    targetCharID = r.Unpack32();
                }

                DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv2] from {p.CharName}. TargetCharID={targetCharID}");

                if (targetCharID == 0)
                {
                    // CharID = 0 means Friend List Request
                    DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv2] Friend List Request from {p.CharName}");
                    SendFriendList(p);
                }
                else
                {
                    // Non-zero CharID = Friend Request Send
                    DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv2] Friend Request from {p.CharName} to CharID {targetCharID}");

                    Player target = cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.FirstOrDefault(x => x.CharID == targetCharID);
                    if (target == null && p.CurMap is GameMap map)
                    {
                        target = map.PlayersList.FirstOrDefault(x => x.CharID == targetCharID);
                    }

                    if (target != null)
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv2] Target {target.CharName} (ID:{targetCharID}) found. Forwarding friend request...");

                        // S->C AC 14:2 <requesterCharID(4B)>
                        SendPacket s = new SendPacket();
                        s.Pack8(14);
                        s.Pack8(2);
                        s.Pack32(p.CharID);
                        target.Send(s);

                        DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv2] Friend request sent to {target.CharName}");
                    }
                    else
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv2] Target CharID {targetCharID} NOT found online");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Recv2 Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// AC 14:3 - Friend Request Accepted (Confirmed via arkadaseklemeveonlinegozukme.pcapng)
        /// C->S: 0e 03 <requesterCharId(4B)> <group(1B)>
        /// S->C to both: 0e 03 <otherCharId(4B)> <group(1B)>
        /// </summary>
        void Recv3(ref Player p, RecievePacket r)
        {
            try
            {
                var rawBuf = r.Buffer.ToArray();
                if (rawBuf.Length < 10) return;

                uint requesterCharID = BitConverter.ToUInt32(rawBuf, 6);
                byte group = rawBuf.Length > 10 ? rawBuf[10] : (byte)1;

                DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv3] Friend Accept from {p.CharName} (#{p.CharID}) for Requester CharID: {requesterCharID}, Group: {group}");

                // Add friendship to database
                try
                {
                    cGlobal.gGameDataBase?.ExecuteNonQuery(@"
                        CREATE TABLE IF NOT EXISTS Friends (
                            CharID1 INTEGER NOT NULL,
                            CharID2 INTEGER NOT NULL,
                            AddedDate TEXT NOT NULL,
                            PRIMARY KEY (CharID1, CharID2)
                        )");

                    uint smaller = Math.Min(p.CharID, requesterCharID);
                    uint larger = Math.Max(p.CharID, requesterCharID);

                    string query = $"INSERT OR IGNORE INTO Friends (CharID1, CharID2, AddedDate) VALUES ({smaller}, {larger}, datetime('now'))";
                    cGlobal.gGameDataBase?.ExecuteNonQuery(query);

                    DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv3] Friendship stored: {p.CharID} <-> {requesterCharID}");

                    // Send AC 14:3 <otherCharID(4B)> <group(1B)> to requester if online
                    Player requester = cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.FirstOrDefault(x => x.CharID == requesterCharID);
                    if (requester == null && p.CurMap is GameMap gMap)
                    {
                        requester = gMap.PlayersList.FirstOrDefault(x => x.CharID == requesterCharID);
                    }

                    if (requester != null)
                    {
                        // 1. S->C AC 14:3 to requester: [14, 3, otherCharID:d, group:b]
                        SendPacket s = new SendPacket();
                        s.Pack8(14);
                        s.Pack8(3);
                        s.Pack32(p.CharID);
                        s.Pack8(group);
                        requester.Send(s);

                        // 2. S->C AC 14:9 to requester: [14, 9, otherCharID:d, 0:b] (Add Friend Success ACK)
                        SendPacket sAck1 = new SendPacket();
                        sAck1.Pack8(14);
                        sAck1.Pack8(9);
                        sAck1.Pack32(p.CharID);
                        sAck1.Pack8(0);
                        requester.Send(sAck1);

                        SendFriendList(requester);
                    }

                    // 3. S->C AC 14:3 to accepter: [14, 3, requesterCharID:d, group:b]
                    SendPacket s2 = new SendPacket();
                    s2.Pack8(14);
                    s2.Pack8(3);
                    s2.Pack32(requesterCharID);
                    s2.Pack8(group);
                    p.Send(s2);

                    // 4. S->C AC 14:9 to accepter: [14, 9, requesterCharID:d, 0:b] (Add Friend Success ACK)
                    SendPacket sAck2 = new SendPacket();
                    sAck2.Pack8(14);
                    sAck2.Pack8(9);
                    sAck2.Pack32(requesterCharID);
                    sAck2.Pack8(0);
                    p.Send(sAck2);

                    // 5. Auto-refresh friend list for accepter
                    SendFriendList(p);
                }
                catch (Exception dbEx)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv3] Database error: {dbEx.Message}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Recv3 Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// AC 14:4 - Friend Remove or Friend List Request (Confirmed via arkadassilme.pcapng)
        /// C->S: 0e 04 <friendCharId(4B)>
        /// </summary>
        void Recv4(ref Player p, RecievePacket r)
        {
            try
            {
                uint friendCharID = 0;
                var rawBuf = r.Buffer.ToArray();

                if (rawBuf.Length >= 10)
                {
                    friendCharID = BitConverter.ToUInt32(rawBuf, 6);
                }

                if (friendCharID > 0)
                {
                    // Friend Remove
                    DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv4] Friend Remove Request from {p.CharName} for CharID {friendCharID}");

                    try
                    {
                        uint smaller = Math.Min(p.CharID, friendCharID);
                        uint larger = Math.Max(p.CharID, friendCharID);

                        string deleteQuery = $"DELETE FROM Friends WHERE CharID1 = {smaller} AND CharID2 = {larger}";
                        cGlobal.gGameDataBase?.ExecuteNonQuery(deleteQuery);

                        DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv4] Removed friendship: {p.CharID} <-> {friendCharID}");

                        // Auto-refresh friend list for current player
                        SendFriendList(p);

                        // Auto-refresh friend list for ex-friend if online
                        Player exFriend = cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.FirstOrDefault(x => x.CharID == friendCharID);
                        if (exFriend != null)
                        {
                            SendFriendList(exFriend);
                        }
                    }
                    catch (Exception dbEx)
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv4] Database error removing friend: {dbEx.Message}");
                    }
                }
                else
                {
                    // Friend List Request
                    DebugSystem.Write(DebugItemType.Error, $"[AC14.Recv4] Friend List Request from {p.CharName}");
                    SendFriendList(p);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Recv4 Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Public static helper to send friend list to a player
        /// </summary>
        public static void SendFriendList(Player p)
        {
            if (p == null) return;
            try
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] SendFriendList called for {p.CharName}");

                // Query database for friends
                var friendsTable = cGlobal.gGameDataBase?.GetDataTable(
                    $"SELECT CharID1, CharID2 FROM Friends WHERE CharID1 = {p.CharID} OR CharID2 = {p.CharID}");

                List<uint> friendIDs = new List<uint>();

                if (friendsTable != null && friendsTable.Rows.Count > 0)
                {
                    for (int i = 0; i < friendsTable.Rows.Count; i++)
                    {
                        uint charID1 = uint.Parse(friendsTable.Rows[i]["CharID1"].ToString());
                        uint charID2 = uint.Parse(friendsTable.Rows[i]["CharID2"].ToString());

                        if (charID1 == p.CharID)
                            friendIDs.Add(charID2);
                        else
                            friendIDs.Add(charID1);
                    }
                }

                DebugSystem.Write(DebugItemType.Error, $"[AC14] Found {friendIDs.Count} friends for {p.CharName}");

                // Send friend list with complete character data (SubCmd 5)
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 14, 5 });

                foreach (uint friendID in friendIDs)
                {
                    PackFriendEntry(s, friendID);
                }

                p.Send(s);
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Sent friend list ({friendIDs.Count} friends) to {p.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] SendFriendList Exception: {ex.Message}");
            }
        }

        public static bool IsPlayerOnline(uint charId)
        {
            return cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.Any(pl => pl.CharID == charId) == true;
        }

        /// <summary>
        /// Helper to pack a friend entry with live online detection
        /// </summary>
        private static void PackFriendEntry(SendPacket s, uint friendID)
        {
            try
            {
                bool isOnline = IsPlayerOnline(friendID);
                Player onlinePlayer = cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.FirstOrDefault(pl => pl.CharID == friendID);
                Character friendChar = (Character)onlinePlayer ?? cGlobal.gCharacterDataBase?.GetCharacterData(friendID);

                if (friendChar != null)
                {
                    s.Pack32(friendChar.CharID);
                    s.PackString(friendChar.CharName ?? $"Player #{friendID}");
                    s.Pack8((byte)friendChar.Level);
                    s.Pack8((byte)(friendChar.Reborn ? 1 : 0));
                    s.Pack8((byte)friendChar.Job);
                    s.Pack8((byte)friendChar.Element);
                    s.Pack8((byte)friendChar.Body);
                    s.Pack8(friendChar.Head);
                    s.Pack16(friendChar.HairColor);
                    s.Pack16(friendChar.SkinColor);
                    s.Pack16(friendChar.ClothingColor);
                    s.Pack16(friendChar.EyeColor);
                    s.PackString(friendChar.NickName ?? "");
                    s.PackString(""); // GuildName string
                    s.Pack8((byte)(isOnline ? 1 : 0)); // Online status: 1 = Online (Green), 0 = Offline (Grey)

                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Added friend {friendChar.CharName} (ID:{friendID}) - Online: {isOnline}");
                }
                else
                {
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Could not load character data for friend ID {friendID}");
                }
            }
            catch (Exception charEx)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Error loading friend {friendID}: {charEx.Message}");
            }
        }

        /// <summary>
        /// Notifies all online friends of player p when their online status changes
        /// </summary>
        public static void NotifyFriendsStatus(Player p, bool isOnline)
        {
            try
            {
                if (p == null || cGlobal.gGameDataBase == null) return;
                var friendsTable = cGlobal.gGameDataBase.GetDataTable(
                    $"SELECT CharID1, CharID2 FROM Friends WHERE CharID1 = {p.CharID} OR CharID2 = {p.CharID}");

                if (friendsTable == null || friendsTable.Rows.Count == 0) return;

                List<uint> friendIDs = new List<uint>();
                for (int i = 0; i < friendsTable.Rows.Count; i++)
                {
                    uint charID1 = uint.Parse(friendsTable.Rows[i]["CharID1"].ToString());
                    uint charID2 = uint.Parse(friendsTable.Rows[i]["CharID2"].ToString());
                    friendIDs.Add(charID1 == p.CharID ? charID2 : charID1);
                }

                var onlinePlayers = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                if (onlinePlayers == null) return;

                foreach (uint fid in friendIDs)
                {
                    var friend = onlinePlayers.FirstOrDefault(pl => pl.CharID == fid);
                    if (friend != null && friend.CharID != p.CharID)
                    {
                        if (isOnline)
                        {
                            // AC 14:7 Friend Online Notification
                            SendPacket sInfo = new SendPacket();
                            sInfo.PackArray(new byte[] { 14, 7 });
                            sInfo.Pack32(p.CharID);
                            sInfo.PackString(p.CharName ?? string.Empty);
                            friend.Send(sInfo);
                        }
                        else
                        {
                            // AC 14:8 Friend Offline Notification
                            SendPacket sOffline = new SendPacket();
                            sOffline.PackArray(new byte[] { 14, 8 });
                            sOffline.Pack32(p.CharID);
                            friend.Send(sOffline);
                        }

                        SendFriendList(friend);
                        DebugSystem.Write(DebugItemType.Error, $"[AC14] Refreshed friend list for {friend.CharName} due to {p.CharName} status change (Online: {isOnline})");
                    }
                }

                if (isOnline)
                {
                    SendFriendList(p);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] NotifyFriendsStatus Exception: {ex.Message}");
            }
        }
    }
}
