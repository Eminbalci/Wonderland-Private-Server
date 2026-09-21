using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.QuestRelated;
using Network;
using RCLibrary.Core;

namespace Game.PlayerRelated
{
    public static class NotebookManager
    {
        // Mark.dat records contain a native mark ID and a separate completion-bit ID.
        // They are not the record number and are not interchangeable on AC24.
        private static readonly Lazy<Dictionary<ushort, ushort>> Marks =
            new Lazy<Dictionary<ushort, ushort>>(() => LoadMarks(PathHelper.GetDataFilePath("Mark.dat")));

        private static Dictionary<ushort, ushort> LoadMarks(string path)
        {
            var result = new Dictionary<ushort, ushort>();
            byte[] data = File.ReadAllBytes(path);
            for (int offset = 553; offset + 553 <= data.Length; offset += 553)
            {
                ushort id = (ushort)((BitConverter.ToUInt16(data, offset + 256) ^ 0x2774) - 7);
                ushort flag = (ushort)((BitConverter.ToUInt16(data, offset + 258) ^ 0x2774) - 7);
                if (id != 0) result[id] = flag;
            }
            return result;
        }

        internal static bool TryGetCompletionFlag(ushort markId, out ushort flag)
        {
            return Marks.Value.TryGetValue(markId, out flag);
        }

        public static void SendQuestJournal(Player player)
        {
            if (player == null) return;
            lock (player.SentNotebookMarks)
            {
                // AC24:4 removes ONE active mark; it is not a counted journal list.
                foreach (ushort id in player.SentNotebookMarks)
                    player.Send(Tools.FromFormat("bbw", 24, 4, id));
                player.SentNotebookMarks.Clear();

                byte[] completed = new byte[250]; // Native completion flags 1..2000.
                var active = new SendPacket();
                active.PackArray(new byte[] { 24, 6 });
                byte slot = 0;
                if (player.Quests != null)
                {
                    foreach (var pair in player.Quests.OrderBy(q => q.Key))
                    {
                        ushort flag;
                        if (pair.Key > ushort.MaxValue || !Marks.Value.TryGetValue((ushort)pair.Key, out flag)) continue;
                        var quest = pair.Value;
                        // EVE "Completed" means this mark was removed. The completion
                        // entry (e.g. 12021 -> bit 14) is a separate active mark.
                        if (quest == null || quest.State != QuestState.InProgress || quest.Step <= 0) continue;
                        if (flag != 0)
                        {
                            if (flag <= 2000) completed[(flag - 1) / 8] |= (byte)(1 << ((flag - 1) % 8));
                        }
                        else if (slot < 200)
                        {
                            active.Pack8(++slot);
                            active.Pack16((ushort)pair.Key);
                            active.Pack8((byte)Math.Min(255, quest.Step));
                            player.SentNotebookMarks.Add((ushort)pair.Key);
                        }
                    }
                }
                if (slot != 0) player.Send(active);
                // AC24:7 is [byte-index, bits] pairs, with no record count.
                var flags = new SendPacket();
                flags.PackArray(new byte[] { 24, 7 });
                for (int i = 0; i < completed.Length; i++)
                {
                    flags.Pack8((byte)(i + 1));
                    flags.Pack8(completed[i]);
                }
                player.Send(flags);
            }
        }

        public static void SendQuestUpdate(Player player, uint markId, QuestState state, byte step)
        {
            ushort flag;
            if (player == null || markId > ushort.MaxValue || !Marks.Value.TryGetValue((ushort)markId, out flag)) return;
            bool enabled = state == QuestState.InProgress && step > 0;
            lock (player.SentNotebookMarks)
            {
                if (flag != 0)
                {
                    if (flag <= 2000) player.Send(Tools.FromFormat("bbwb", 24, 5, flag, (byte)(enabled ? 1 : 0)));
                    return;
                }
                // AC24:1 adds and AC24:2 subtracts. Replace the old value before
                // sending an absolute step so replays never accumulate or cancel it.
                player.Send(Tools.FromFormat("bbw", 24, 4, (ushort)markId));
                player.SentNotebookMarks.Remove((ushort)markId);
                if (enabled && player.SentNotebookMarks.Count < 200)
                {
                    player.Send(Tools.FromFormat("bbwb", 24, 1, (ushort)markId, step));
                    player.SentNotebookMarks.Add((ushort)markId);
                }
            }
        }

        private static bool IsBookMonster(uint npcId)
        {
            var npc = Game.DataFiles.SceneDataManager.GetNpcBaseStats(npcId);
            // Native Npc.dat uses a separate book index; zero is not an entry.
            return npc != null && npc.MonsterBookIndex > 0 && npc.MonsterBookIndex <= 5500;
        }

        public static void DiscoverMonster(Player player, uint npcId)
        {
            if (player == null || !IsBookMonster(npcId)) return;
            lock (player.DiscoveredMonsters)
            {
                if (player.DiscoveredMonsters.Contains(npcId)) return;
                var db = DataBase.CharacterDataBase.GlobalInstance;
                if (db == null || !db.SaveMonsterDiscovery(player.CharID, npcId)) return;
                player.DiscoveredMonsters.Add(npcId);
                // AC53:9 takes a UInt32 NPC template ID, not a book index.
                player.Send(Tools.FromFormat("bbd", 53, 9, npcId));
            }
        }

        public static void SendMonsterBook(Player player)
        {
            if (player == null) return;
            lock (player.DiscoveredMonsters)
            {
                foreach (uint npcId in player.DiscoveredMonsters.OrderBy(id => id))
                    if (IsBookMonster(npcId)) player.Send(Tools.FromFormat("bbd", 53, 9, npcId));
            }
        }
    }
}
