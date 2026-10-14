using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class PvPQueueEntry
    {
        public uint CharId { get; set; }
        public string CharName { get; set; }
        public byte Mode { get; set; } // 1: 1v1, 2: 2v2, 4: 4v4
        public int EloRating { get; set; }
        public byte Level { get; set; }
        public DateTime EnqueueTime { get; set; }

        public PvPQueueEntry(Player player, byte mode, int elo)
        {
            CharId = player?.CharID ?? 0;
            CharName = player?.CharName ?? "Gladiator";
            Mode = mode;
            EloRating = elo;
            Level = player?.Level ?? 1;
            EnqueueTime = DateTime.UtcNow;
        }
    }

    public class PvPMatch
    {
        public uint MatchId { get; set; }
        public byte Mode { get; set; }
        public List<PvPQueueEntry> TeamA { get; } = new List<PvPQueueEntry>();
        public List<PvPQueueEntry> TeamB { get; } = new List<PvPQueueEntry>();
        public DateTime StartTime { get; set; }
        public bool IsActive { get; set; }

        public PvPMatch(uint id, byte mode)
        {
            MatchId = id;
            Mode = mode;
            StartTime = DateTime.UtcNow;
            IsActive = true;
        }
    }

    public class PvPPlayerRecord
    {
        public uint CharId { get; set; }
        public string CharName { get; set; }
        public int EloRating { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public byte RankTier => (byte)(EloRating >= 2000 ? 4 : EloRating >= 1600 ? 3 : EloRating >= 1300 ? 2 : 1);
        public string RankTitle => EloRating >= 2000 ? "Grandmaster" : EloRating >= 1600 ? "Diamond" : EloRating >= 1300 ? "Gold" : "Bronze";

        public PvPPlayerRecord(uint id, string name, int elo = 1200)
        {
            CharId = id;
            CharName = name;
            EloRating = elo;
            Wins = 0;
            Losses = 0;
        }
    }

    /// <summary>
    /// Ranked PvP Matchmaking and Elo Rating Arena Engine.
    /// Provides Elo calculations, queue management, match pairing, and AC 183/186 wire framing.
    /// </summary>
    public class PvPArenaEngine
    {
        private readonly ConcurrentDictionary<uint, PvPQueueEntry> m_queue = new ConcurrentDictionary<uint, PvPQueueEntry>();
        private readonly ConcurrentDictionary<uint, PvPMatch> m_activeMatches = new ConcurrentDictionary<uint, PvPMatch>();
        private readonly ConcurrentDictionary<uint, PvPPlayerRecord> m_records = new ConcurrentDictionary<uint, PvPPlayerRecord>();
        private uint m_nextMatchId = 5000;
        private readonly object m_matchLock = new object();

        public int QueueCount => m_queue.Count;
        public int ActiveMatchCount => m_activeMatches.Count;

        public IReadOnlyCollection<PvPQueueEntry> GetQueue() => m_queue.Values.ToList().AsReadOnly();
        public IReadOnlyCollection<PvPMatch> GetActiveMatches() => m_activeMatches.Values.ToList().AsReadOnly();
        public IReadOnlyCollection<PvPPlayerRecord> GetLeaderboard() => m_records.Values.OrderByDescending(r => r.EloRating).Take(50).ToList().AsReadOnly();

        public PvPPlayerRecord GetOrCreateRecord(Player player)
        {
            if (player == null) return new PvPPlayerRecord(0, "Guest");
            return m_records.GetOrAdd(player.CharID, id => new PvPPlayerRecord(id, player.CharName, 1200));
        }

        public bool EnqueuePlayer(Player player, byte mode = 1)
        {
            if (player == null) return false;

            var record = GetOrCreateRecord(player);
            var entry = new PvPQueueEntry(player, mode, record.EloRating);
            m_queue[player.CharID] = entry;

            DebugSystem.Write($"[PvPArenaEngine] Player {player.CharName} (Elo: {record.EloRating}) enqueued for Mode {mode}v{mode}");

            // S->C AC 183:11 Matchmaking queue status ACK
            SendPacket s = new SendPacket();
            s.Pack8(183);
            s.Pack8(11);
            s.Pack8(mode);
            s.Pack8(1); // 1 = Enqueued successfully
            player.Send(s);

            CheckAndMatchmake();
            return true;
        }

        public bool DequeuePlayer(Player player)
        {
            if (player == null) return false;

            if (m_queue.TryRemove(player.CharID, out _))
            {
                DebugSystem.Write($"[PvPArenaEngine] Player {player.CharName} dequeued from PvP Arena");

                SendPacket s = new SendPacket();
                s.Pack8(183);
                s.Pack8(11);
                s.Pack8(0); // 0 = Dequeued
                s.Pack8(0);
                player.Send(s);
                return true;
            }
            return false;
        }

        public void CheckAndMatchmake()
        {
            lock (m_matchLock)
            {
                var queued = m_queue.Values.OrderBy(q => q.EnqueueTime).ToList();
                if (queued.Count < 2) return;

                // Group by mode
                var byMode = queued.GroupBy(q => q.Mode);
                foreach (var group in byMode)
                {
                    var list = group.ToList();
                    while (list.Count >= 2)
                    {
                        var player1 = list[0];
                        PvPQueueEntry bestOpponent = null;
                        int minDelta = int.MaxValue;

                        for (int i = 1; i < list.Count; i++)
                        {
                            var candidate = list[i];
                            int delta = Math.Abs(player1.EloRating - candidate.EloRating);
                            double waitSecs = (DateTime.UtcNow - player1.EnqueueTime).TotalSeconds;
                            int allowedDelta = 150 + (int)(waitSecs * 15); // Expanding tolerance

                            if (delta <= allowedDelta && delta < minDelta)
                            {
                                minDelta = delta;
                                bestOpponent = candidate;
                            }
                        }

                        if (bestOpponent != null)
                        {
                            list.Remove(player1);
                            list.Remove(bestOpponent);
                            m_queue.TryRemove(player1.CharId, out _);
                            m_queue.TryRemove(bestOpponent.CharId, out _);

                            uint matchId = ++m_nextMatchId;
                            var match = new PvPMatch(matchId, group.Key);
                            match.TeamA.Add(player1);
                            match.TeamB.Add(bestOpponent);
                            m_activeMatches[matchId] = match;

                            DebugSystem.Write($"[PvPArenaEngine] MATCH FOUND! #{matchId}: {player1.CharName} ({player1.EloRating}) vs {bestOpponent.CharName} ({bestOpponent.EloRating})");
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Record duel outcome and calculate new Elo ratings using official $K=32$ algorithm.
        /// </summary>
        public void RecordMatchResult(uint matchId, uint winnerCharId)
        {
            if (!m_activeMatches.TryRemove(matchId, out var match)) return;

            match.IsActive = false;
            var winner = match.TeamA.Concat(match.TeamB).FirstOrDefault(p => p.CharId == winnerCharId);
            var loser = match.TeamA.Concat(match.TeamB).FirstOrDefault(p => p.CharId != winnerCharId);

            if (winner != null && loser != null)
            {
                var rWinner = m_records.GetOrAdd(winner.CharId, id => new PvPPlayerRecord(id, winner.CharName));
                var rLoser = m_records.GetOrAdd(loser.CharId, id => new PvPPlayerRecord(id, loser.CharName));

                // Expected score
                double expectedWin = 1.0 / (1.0 + Math.Pow(10, (rLoser.EloRating - rWinner.EloRating) / 400.0));
                double expectedLoss = 1.0 - expectedWin;

                int k = 32;
                int winnerGain = (int)Math.Round(k * (1.0 - expectedWin));
                int loserLoss = (int)Math.Round(k * (0.0 - expectedLoss));

                rWinner.EloRating += Math.Max(5, winnerGain);
                rWinner.Wins++;

                rLoser.EloRating = Math.Max(800, rLoser.EloRating + loserLoss);
                rLoser.Losses++;

                DebugSystem.Write($"[PvPArenaEngine] Match #{matchId} settled! Winner: {rWinner.CharName} (+{winnerGain} -> {rWinner.EloRating}), Loser: {rLoser.CharName} ({loserLoss} -> {rLoser.EloRating})");
            }
        }
    }
}
