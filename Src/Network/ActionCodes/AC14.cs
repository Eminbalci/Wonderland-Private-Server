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

            switch (r.B)
            {
                case 1: Recv1(ref p, r); break; // Add Friend Request
                case 2: Recv2(ref p, r); break; // Friend Request Response
                case 3: Recv3(ref p, r); break; // Remove Friend
                case 4: Recv4(ref p, r); break; // Friend List Request
                default:
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Unknown SubAction: {r.B}");
                    break;
            }
        }

        void Recv1(ref Player p, RecievePacket r)
        {
            // Add Friend Request
            try
            {
                string targetName = r.UnpackString();
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Add Friend Request from {p.CharName} to {targetName}");

                // Find target player (search all online players)
                Player target = null;

                // Search in requester's current map first
                if (p.CurMap is GameMap)
                {
                    var map = p.CurMap as GameMap;
                    target = map.PlayersList.FirstOrDefault(x => x.CharName == targetName);
                }

                // If not found, could search other maps here (TODO)

                if (target != null)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Target {targetName} found. Sending friend request...");

                    // Send friend request to target player
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 14, 1 });
                    s.PackString(p.CharName); // Requester name
                    target.Send(s);

                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Friend request sent to {targetName}");
                }
                else
                {
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Target {targetName} NOT found");

                    // Send error to requester
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 14, 1 });
                    s.Pack8(0); // Failure
                    p.Send(s);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Recv1 Exception: {ex.Message}");
            }
        }

        void Recv2(ref Player p, RecievePacket r)
        {
            // SubCmd 2: Dual purpose - Friend List Request OR Friend Request Send
            try
            {
                byte flag = r.Unpack8();
                uint targetCharID = r.Unpack32();
                string ignoredString = r.UnpackString(); // Client sends empty string

                DebugSystem.Write(DebugItemType.Error, $"[AC14] Recv2 from {p.CharName}. Flag={flag:X2}, TargetCharID={targetCharID}");

                if (targetCharID == 0)
                {
                    // CharID = 0 means Friend List Request
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Friend List Request from {p.CharName}");
                    SendFriendList(p);
                }
                else
                {
                    // Non-zero CharID = Friend Request Send
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Friend Request from {p.CharName} to CharID {targetCharID}");

                    // Find target player by CharID
                    Player target = null;

                    if (p.CurMap is GameMap)
                    {
                        var map = p.CurMap as GameMap;
                        target = map.PlayersList.FirstOrDefault(x => x.CharID == targetCharID);
                    }

                    if (target != null)
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[AC14] Target {target.CharName} (ID:{targetCharID}) found. Sending friend request...");

                        // Send friend request to target player
                        SendPacket s = new SendPacket();
                        s.PackArray(new byte[] { 14, 2 });
                        s.Pack32(p.CharID); // Requester CharID (not name!)
                        s.PackString(""); // Empty string
                        target.Send(s);

                        DebugSystem.Write(DebugItemType.Error, $"[AC14] Friend request sent to {target.CharName}");
                    }
                    else
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[AC14] Target CharID {targetCharID} NOT found");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Recv2 Exception: {ex.Message}");
            }
        }

        void Recv3(ref Player p, RecievePacket r)
        {
            // Friend Accept - Add to friend list
            try
            {
                byte flag = r.Unpack8();
                uint requesterCharID = r.Unpack32();
                string ignoredString = r.UnpackString();

                // Debug: Show packet bytes
                string hexBytes = BitConverter.ToString(r.Buffer.ToArray()).Replace("-", " ");
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Recv3 Packet bytes: {hexBytes}");
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Friend Accept from {p.CharName}. Requester CharID: {requesterCharID}");

                // Find requester by CharID
                Player requester = null;
                if (p.CurMap is GameMap)
                {
                    var map = p.CurMap as GameMap;
                    requester = map.PlayersList.FirstOrDefault(x => x.CharID == requesterCharID);
                }

                if (requester == null)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Requester CharID {requesterCharID} not found");
                    return;
                }

                // Add friendship to database (inline to avoid cross-project reference)
                try
                {
                    // Create table if it doesn't exist (fallback since VerifySetup not working)
                    try
                    {
                        cGlobal.gGameDataBase.ExecuteNonQuery(@"
                            CREATE TABLE IF NOT EXISTS Friends (
                                CharID1 INTEGER NOT NULL,
                                CharID2 INTEGER NOT NULL,
                                AddedDate TEXT NOT NULL,
                                PRIMARY KEY (CharID1, CharID2)
                            )");
                        DebugSystem.Write(DebugItemType.Error, "[AC14] Friends table created/verified");
                    }
                    catch { /* Table already exists */ }

                    uint smaller = Math.Min(p.CharID, requesterCharID);
                    uint larger = Math.Max(p.CharID, requesterCharID);

                    string query = $"INSERT OR IGNORE INTO Friends (CharID1, CharID2, AddedDate) VALUES ({smaller}, {larger}, datetime('now'))";
                    cGlobal.gGameDataBase.ExecuteNonQuery(query);

                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Friendship added: {p.CharID} <-> {requesterCharID}");

                    // Send success notification to requester
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 14, 3 });
                    s.Pack8(1); // Success
                    s.PackString(p.CharName); // Friend name
                    requester.Send(s);

                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Notified {requester.CharName} of friendship");

                    // Send success to accepter
                    SendPacket s2 = new SendPacket();
                    s2.PackArray(new byte[] { 14, 3 });
                    s2.Pack8(1); // Success
                    p.Send(s2);

                    // Auto-refresh friend list for both players
                    SendFriendList(p); // Accepter
                    SendFriendList(requester); // Requester
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Auto-refreshed friend lists for both players");
                }
                catch (Exception dbEx)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Database error: {dbEx.Message}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC14] Recv3 Exception: {ex.Message}");
            }
        }

        void Recv4(ref Player p, RecievePacket r)
        {
            // SubCmd 4: Friend List Request OR Friend Remove
            try
            {
                // Try to unpack CharID - if successful, it's a friend remove request
                uint friendCharID = 0;
                bool hasFriendID = false;

                try
                {
                    // Packet structure: [Header 4 bytes] [AC 1 byte] [SubCmd 1 byte] [CharID 4 bytes]
                    if (r.Buffer.Length >= 10)
                    {
                        friendCharID = BitConverter.ToUInt32(r.Buffer.ToArray(), 6);
                        hasFriendID = true;
                        DebugSystem.Write(DebugItemType.Error, $"[AC14] Parsed CharID: {friendCharID}");
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Failed to parse CharID: {ex.Message}");
                    hasFriendID = false;
                }

                if (hasFriendID)
                {
                    // Friend Remove
                    string hexBytes = BitConverter.ToString(r.Buffer.ToArray()).Replace("-", " ");
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Packet bytes: {hexBytes}");
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Friend Remove Request from {p.CharName} for CharID {friendCharID}");

                    try
                    {
                        // Remove from database
                        uint smaller = Math.Min(p.CharID, friendCharID);
                        uint larger = Math.Max(p.CharID, friendCharID);

                        string deleteQuery = $"DELETE FROM Friends WHERE CharID1 = {smaller} AND CharID2 = {larger}";
                        cGlobal.gGameDataBase.ExecuteNonQuery(deleteQuery);

                        DebugSystem.Write(DebugItemType.Error, $"[AC14] Removed friendship: {p.CharID} <-> {friendCharID}");

                        // Auto-refresh friend list
                        SendFriendList(p);
                        DebugSystem.Write(DebugItemType.Error, $"[AC14] Auto-refreshed friend list after removal");
                    }
                    catch (Exception dbEx)
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[AC14] Database error removing friend: {dbEx.Message}");
                    }
                }
                else
                {
                    // Friend List Request
                    DebugSystem.Write(DebugItemType.Error, $"[AC14] Friend List Request (SubCmd 4) from {p.CharName}");
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
