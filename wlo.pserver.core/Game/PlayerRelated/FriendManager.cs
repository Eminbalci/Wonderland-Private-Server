using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using Game.Code;

namespace Game.PlayerRelated
{
    public static class FriendManager
    {
        private static string ConnectionString => cGlobal.gGameDataBase.ConnectionString;

        /// <summary>
        /// Add a friendship between two characters
        /// </summary>
        public static bool AddFriend(uint charID1, uint charID2)
        {
            try
            {
                // Ensure consistent ordering (smaller ID first)
                uint smaller = Math.Min(charID1, charID2);
                uint larger = Math.Max(charID1, charID2);

                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();

                    string query = @"INSERT OR IGNORE INTO Friends (CharID1, CharID2, AddedDate) 
                                    VALUES (@CharID1, @CharID2, @AddedDate)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CharID1", smaller);
                        command.Parameters.AddWithValue("@CharID2", larger);
                        command.Parameters.AddWithValue("@AddedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                        int rows = command.ExecuteNonQuery();
                        DebugSystem.Write(DebugItemType.Error, $"[FriendManager] Added friendship: {charID1} <-> {charID2}");
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[FriendManager] AddFriend Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove a friendship
        /// </summary>
        public static bool RemoveFriend(uint charID1, uint charID2)
        {
            try
            {
                uint smaller = Math.Min(charID1, charID2);
                uint larger = Math.Max(charID1, charID2);

                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();

                    string query = @"DELETE FROM Friends 
                                    WHERE CharID1 = @CharID1 AND CharID2 = @CharID2";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CharID1", smaller);
                        command.Parameters.AddWithValue("@CharID2", larger);

                        int rows = command.ExecuteNonQuery();
                        DebugSystem.Write(DebugItemType.Error, $"[FriendManager] Removed friendship: {charID1} <-> {charID2}");
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[FriendManager] RemoveFriend Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get list of friend CharIDs for a character
        /// </summary>
        public static List<uint> GetFriends(uint charID)
        {
            var friends = new List<uint>();

            try
            {
                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();

                    string query = @"SELECT CharID1, CharID2 FROM Friends 
                                    WHERE CharID1 = @CharID OR CharID2 = @CharID";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CharID", charID);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                uint id1 = Convert.ToUInt32(reader["CharID1"]);
                                uint id2 = Convert.ToUInt32(reader["CharID2"]);

                                // Add the other ID (not our own)
                                friends.Add(id1 == charID ? id2 : id1);
                            }
                        }
                    }
                }

                DebugSystem.Write(DebugItemType.Error, $"[FriendManager] Loaded {friends.Count} friends for CharID {charID}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[FriendManager] GetFriends Exception: {ex.Message}");
            }

            return friends;
        }

        /// <summary>
        /// Check if two characters are friends
        /// </summary>
        public static bool AreFriends(uint charID1, uint charID2)
        {
            try
            {
                uint smaller = Math.Min(charID1, charID2);
                uint larger = Math.Max(charID1, charID2);

                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();

                    string query = @"SELECT COUNT(*) FROM Friends 
                                    WHERE CharID1 = @CharID1 AND CharID2 = @CharID2";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CharID1", smaller);
                        command.Parameters.AddWithValue("@CharID2", larger);

                        long count = (long)command.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[FriendManager] AreFriends Exception: {ex.Message}");
                return false;
            }
        }
    }
}
