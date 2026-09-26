using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Game.Code;
using Game.DataFiles;
using Game.QuestRelated;
using Network;
using RCLibrary.Core;

namespace Game.Maps
{
    public static partial class EveEventInterpreter
    {
        // EVE pool 7 variants: sea water (1), fresh water (2). Other pools need
        // their original item weights before they can be enabled.
        private static bool IsWaterReward(EventSubSubEntry op)
        {
            return op.DialogPtr == 1 && op.dialog1 == 1 && op.dialog2 == 6 && op.dialog3 == 7 &&
                (DecodeActionValue(op) == 1 || DecodeActionValue(op) == 2);
        }

        private static bool IsWaterTimer(ushort id)
        {
            return id == 11009 || id == 11016 || id == 11021 || id == 11035;
        }

        private static bool IsWaterTimerAction(EventSubSubEntry op)
        {
            return op.DialogPtr == 14 && IsWaterTimer(op.dialog1) && op.dialog2 == 1 &&
                op.dialog3 == 0 && DecodeActionValue(op) == 180;
        }

        private static DbConnection OpenGatheringDatabase()
        {
            var db = (RCLibrary.Core.DataBase)DataBase.CharacterDataBase.GlobalInstance ??
                (RCLibrary.Core.DataBase)DataBase.GameDataBase.GlobalInstance;
            if (db == null) throw new InvalidOperationException("Character database unavailable.");
            DbConnection connection;
            if (db.DatabaseType == DataBaseTypes.Sqlite)
                connection = new System.Data.SQLite.SQLiteConnection(db.Connection_String);
            else if (db.DatabaseType == DataBaseTypes.MySQl)
                connection = new MySql.Data.MySqlClient.MySqlConnection(db.Connection_String);
            else throw new NotSupportedException("Gathering database provider.");
            try
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE IF NOT EXISTS character_event_timers " +
                        "(charID BIGINT NOT NULL, timerID INTEGER NOT NULL, expiresUtcTicks BIGINT NOT NULL, PRIMARY KEY(charID,timerID))" + (connection is System.Data.SQLite.SQLiteConnection ? "" : " ENGINE=InnoDB");
                    command.ExecuteNonQuery();
                    if (!(connection is System.Data.SQLite.SQLiteConnection))
                    {
                        // A transaction cannot protect legacy MyISAM inventory tables.
                        command.CommandText = "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() " +
                            "AND TABLE_NAME IN ('inventory','charquest','character_event_timers') AND ENGINE='InnoDB'";
                        if (Convert.ToInt32(command.ExecuteScalar()) != 3)
                            throw new NotSupportedException("Gathering requires transactional character tables.");
                    }
                }
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }

        private static bool MatchesGatheringTimer(Player player, EventSubEntry condition)
        {
            if (!IsWaterTimer(condition.unknownword1) || condition.unknownword2 < 1 || condition.unknownword2 > 2 ||
                condition.unknownword3 != 0 || condition.unknownword4 != 0 || condition.unknownword5 != 0 ||
                (condition.unknownword6 & 255) != 0) return false;
            try
            {
                using (var connection = OpenGatheringDatabase())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT expiresUtcTicks FROM character_event_timers WHERE charID=" + player.CharID +
                        " AND timerID=" + condition.unknownword1;
                    object result = command.ExecuteScalar();
                    bool active = result != null && result != DBNull.Value && Convert.ToInt64(result) > DateTime.UtcNow.Ticks;
                    return condition.unknownword2 == 1 ? active : !active;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write("[Gathering] Timer read failed: " + ex.Message);
                return false;
            }
        }

        private static bool TryRunWaterGathering(Player player, GameMap map, EventsinMapEntries ev, EventSubEntry branch)
        {
            if (!branch.SubEntry.Any(o => IsWaterReward(o) || IsWaterTimerAction(o))) return false;
            ushort timer = map.MapID == 60001 && ev.clickID == 88 ? (ushort)11009 :
                map.MapID == 60003 && ev.clickID == 48 ? (ushort)11016 :
                map.MapID == 12268 && ev.clickID == 2 ? (ushort)11021 :
                map.MapID == 60005 && ev.clickID == 12 ? (ushort)11035 : (ushort)0;
            ushort item = timer == 11009 || timer == 11016 ? (ushort)60001 : (ushort)60002;
            var ops = branch.SubEntry;
            bool shape = timer != 0 && (branch.subIndex == 2 && ops.Count == 2 || branch.subIndex == 5 && ops.Count == 3) &&
                IsWaterReward(ops[0]) && DecodeActionValue(ops[0]) == (item == 60001 ? 1u : 2u) &&
                IsWaterTimerAction(ops[1]) && ops[1].dialog1 == timer;
            if (shape && ops.Count == 3)
                shape = ops[2].DialogPtr == 5 && ops[2].dialog1 == timer && ops[2].dialog2 == 1 &&
                    ops[2].dialog3 == 1 && DecodeActionValue(ops[2]) == 1;
            player.ClearInteraction();
            player.Send(Tools.FromFormat("bbb", 6, 2, 1));
            bool granted = false;
            try
            {
                lock (player.Inv.SyncRoot)
                {
                    // Recheck the complete authored branch after obtaining the inventory lock.
                    if (shape && GetPlayerFreeSlots(player) >= 1 && FindBranch(player, map, ev) == branch)
                    {
                        granted = player.Inv.TryApplyQuestItems(new[] { new KeyValuePair<ushort, int>(item, 1) }, true,
                            planned => PersistWaterGathering(player, timer, planned));
                        if (granted)
                        {
                            if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();
                            player.Quests[timer] = new PlayerQuest(timer, QuestState.InProgress, 1);
                            QuestManager.SendQuestUpdate(player, timer, QuestState.InProgress, 1);
                        }
                    }
                }
            }
            catch (Exception ex) { DebugSystem.Write("[Gathering] Event failed: " + ex.Message); }
            if (!granted) player.SendHeadBanner("Cannot gather now. Check bag space and wait for the resource to recover.");
            // Inventory, mark and timer were saved together; do not run the ordinary multi-step save here.
            player.CancelInteraction();
            return true;
        }

        private static bool PersistWaterGathering(Player player, ushort timer, InvItem[] planned)
        {
            try
            {
                using (var connection = OpenGatheringDatabase())
                using (var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    string key = "charID=" + player.CharID + " AND timerID=" + timer;
                    string insert = connection is System.Data.SQLite.SQLiteConnection ? "INSERT OR IGNORE" : "INSERT IGNORE";
                    command.CommandText = insert + " INTO character_event_timers (charID,timerID,expiresUtcTicks) VALUES (" + player.CharID + "," + timer + ",0)";
                    command.ExecuteNonQuery();
                    long now = DateTime.UtcNow.Ticks;
                    command.CommandText = "UPDATE character_event_timers SET expiresUtcTicks=" + (now + TimeSpan.FromSeconds(180).Ticks) +
                        " WHERE " + key + " AND expiresUtcTicks<=" + now;
                    if (command.ExecuteNonQuery() != 1) return false;
                    // Save the full current bag in this transaction. Preserve extra DB metadata
                    // for unchanged item identities instead of deleting all inventory rows.
                    for (int i = 0; i < planned.Length; i++)
                    {
                        var slot = planned[i];
                        string invKey = "charID=" + player.CharID + " AND storID=0 AND invIdx=" + (i + 1);
                        command.CommandText = "DELETE FROM inventory WHERE " + invKey + " AND itemID<>" + slot.ItemID;
                        command.ExecuteNonQuery();
                        if (slot.ItemID == 0) continue;
                        command.CommandText = "SELECT COUNT(*) FROM inventory WHERE " + invKey;
                        bool exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
                        command.CommandText = exists ? "UPDATE inventory SET qty=" + slot.Ammt + ",dmg=" + slot.Damage + ",forge=" + slot.Forge + " WHERE " + invKey :
                            "INSERT INTO inventory (invIdx,charID,storID,itemID,dmg,qty,pos,socketID,bombID,sewID,forge) VALUES (" +
                            (i + 1) + "," + player.CharID + ",0," + slot.ItemID + "," + slot.Damage + "," + slot.Ammt + "," + (i + 1) + ",0,0,0," + slot.Forge + ")";
                        command.ExecuteNonQuery();
                    }
                    command.CommandText = "DELETE FROM charquest WHERE charID=" + player.CharID + " AND quest_started=" + timer;
                    command.ExecuteNonQuery();
                    command.CommandText = "INSERT INTO charquest (charID,quest_started,quest_pos,step) VALUES (" +
                        player.CharID + "," + timer + "," + (byte)QuestState.InProgress + ",1)";
                    command.ExecuteNonQuery();
                    transaction.Commit();
                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write("[Gathering] Reward transaction rolled back: " + ex.Message);
                return false;
            }
        }
    }
}
