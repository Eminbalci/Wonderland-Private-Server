
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class GobangMatch
    {
        public uint MatchId { get; set; }
        public uint PlayerBlackCharId { get; set; }
        public string PlayerBlackName { get; set; }
        public uint PlayerWhiteCharId { get; set; }
        public string PlayerWhiteName { get; set; }
        public int WagerGold { get; set; }
        public byte CurrentTurn { get; set; } // 1 = Black, 2 = White
        public byte[,] Board { get; } = new byte[15, 15]; // 0: empty, 1: black, 2: white
        public bool IsFinished { get; set; }
        public uint WinnerCharId { get; set; }
        public DateTime StartTime { get; set; }

        public GobangMatch(uint id, Player black, Player white, int wager)
        {
            MatchId = id;
            PlayerBlackCharId = black?.CharID ?? 0;
            PlayerBlackName = black?.CharName ?? "Black";
            PlayerWhiteCharId = white?.CharID ?? 0;
            PlayerWhiteName = white?.CharName ?? "White";
            WagerGold = wager;
            CurrentTurn = 1; // Black moves first
            IsFinished = false;
            WinnerCharId = 0;
            StartTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Gobang (Gomoku) Board Game and Street Wager Ante Engine (AC 41 / AC 70 & Spec 41).
    /// Manages 15x15 board state, 5-in-a-row validation, and wager ante gold payouts.
    /// </summary>
    public class GobangWagerEngine
    {
        private readonly ConcurrentDictionary<uint, GobangMatch> m_matches = new ConcurrentDictionary<uint, GobangMatch>();
        private uint m_nextMatchId = 7000;
        private readonly object m_matchLock = new object();

        public int ActiveMatchCount => m_matches.Values.Count(m => !m.IsFinished);

        public IReadOnlyCollection<GobangMatch> GetActiveMatches() =>
            m_matches.Values.OrderByDescending(m => m.StartTime).Take(20).ToList().AsReadOnly();

        public GobangMatch CreateMatch(Player host, Player challenger, int wagerGold = 1000)
        {
            if (host == null || challenger == null) return null;

            lock (m_matchLock)
            {
                uint id = ++m_nextMatchId;
                var match = new GobangMatch(id, host, challenger, wagerGold);
                m_matches[id] = match;

                DebugSystem.Write($"[GobangWagerEngine] Created Gomoku Match #{id}: {host.CharName} vs {challenger.CharName} (Wager: {wagerGold:N0}G)");

                // S->C AC 41:1 Game Started frame
                SendPacket p = new SendPacket();
                p.Pack8(41);
                p.Pack8(1);
                p.Pack32(id);
                p.Pack32((uint)wagerGold);
                host.Send(p);
                challenger.Send(p);

                return match;
            }
        }

        public bool PlaceStone(uint matchId, Player player, int x, int y)
        {
            if (player == null || x < 0 || x >= 15 || y < 0 || y >= 15) return false;

            if (m_matches.TryGetValue(matchId, out var match) && !match.IsFinished)
            {
                lock (match)
                {
                    byte stoneColor = (player.CharID == match.PlayerBlackCharId) ? (byte)1 :
                                      (player.CharID == match.PlayerWhiteCharId) ? (byte)2 : (byte)0;

                    if (stoneColor == 0 || stoneColor != match.CurrentTurn)
                    {
                        return false; // Not this player's turn
                    }

                    if (match.Board[x, y] != 0)
                    {
                        return false; // Position occupied
                    }

                    match.Board[x, y] = stoneColor;
                    DebugSystem.Write($"[GobangWagerEngine] Match #{matchId}: {player.CharName} placed stone at ({x}, {y})");

                    // S->C AC 41:2 Stone Placement sync
                    SendPacket p = new SendPacket();
                    p.Pack8(41);
                    p.Pack8(2);
                    p.Pack32(matchId);
                    p.Pack8((byte)x);
                    p.Pack8((byte)y);
                    p.Pack8(stoneColor);
                    player.Send(p);

                    // Check for 5-in-a-row victory
                    if (CheckWin(match.Board, x, y, stoneColor))
                    {
                        match.IsFinished = true;
                        match.WinnerCharId = player.CharID;

                        int totalPrize = match.WagerGold * 2;
                        player.Gold += (uint)totalPrize;

                        DebugSystem.Write($"[GobangWagerEngine] VICTORY in Match #{matchId}! {player.CharName} won 5-in-a-row! Prize: {totalPrize:N0}G");

                        // S->C AC 41:3 Match Finished
                        SendPacket finishPkt = new SendPacket();
                        finishPkt.Pack8(41);
                        finishPkt.Pack8(3);
                        finishPkt.Pack32(matchId);
                        finishPkt.Pack32(player.CharID);
                        finishPkt.Pack32((uint)totalPrize);
                        player.Send(finishPkt);
                    }
                    else
                    {
                        // Toggle turn
                        match.CurrentTurn = (byte)(match.CurrentTurn == 1 ? 2 : 1);
                    }
                    return true;
                }
            }
            return false;
        }

        private bool CheckWin(byte[,] board, int x, int y, byte color)
        {
            int[][] directions = new int[][]
            {
                new int[] { 1, 0 },  // Horizontal
                new int[] { 0, 1 },  // Vertical
                new int[] { 1, 1 },  // Diagonal \
                new int[] { 1, -1 }  // Anti-diagonal /
            };

            foreach (var dir in directions)
            {
                int count = 1;
                // Positive direction
                for (int step = 1; step < 5; step++)
                {
                    int nx = x + dir[0] * step;
                    int ny = y + dir[1] * step;
                    if (nx >= 0 && nx < 15 && ny >= 0 && ny < 15 && board[nx, ny] == color) count++;
                    else break;
                }
                // Negative direction
                for (int step = 1; step < 5; step++)
                {
                    int nx = x - dir[0] * step;
                    int ny = y - dir[1] * step;
                    if (nx >= 0 && nx < 15 && ny >= 0 && ny < 15 && board[nx, ny] == color) count++;
                    else break;
                }

                if (count >= 5) return true;
            }
            return false;
        }
    }
}
