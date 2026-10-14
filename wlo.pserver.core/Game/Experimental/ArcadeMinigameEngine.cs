using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public enum MinigameKind : byte
    {
        ClawMachine = 1,   // cMZ_Catcher
        RhythmBoxing = 2,  // StrengthBarFrame
        WhackAMole = 3,    // HitMouse
        DigHole = 4,       // MiniGame_DigHole_Form
        GobangChess = 5    // yRE_Chess
    }

    public class ArcadeSession
    {
        public uint SessionId { get; set; }
        public uint CharId { get; set; }
        public string CharName { get; set; }
        public MinigameKind GameKind { get; set; }
        public int Score { get; set; }
        public bool IsFinished { get; set; }
        public DateTime StartTime { get; set; }

        public ArcadeSession(uint sessionId, Player player, MinigameKind kind)
        {
            SessionId = sessionId;
            CharId = player?.CharID ?? 0;
            CharName = player?.CharName ?? "ArcadePlayer";
            GameKind = kind;
            Score = 0;
            IsFinished = false;
            StartTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Arcade Minigames and Carnival Training Facility Engine.
    /// Manages claw machine, rhythm boxing, whack-a-mole, and gobang arcade sessions via AC 57.
    /// </summary>
    public class ArcadeMinigameEngine
    {
        private readonly ConcurrentDictionary<uint, ArcadeSession> m_sessions = new ConcurrentDictionary<uint, ArcadeSession>();
        private readonly ConcurrentDictionary<uint, int> m_highScores = new ConcurrentDictionary<uint, int>();
        private uint m_nextSessionId = 8000;

        public int ActiveSessionsCount => m_sessions.Values.Count(s => !s.IsFinished);

        public IReadOnlyCollection<ArcadeSession> GetRecentSessions()
        {
            return m_sessions.Values.OrderByDescending(s => s.StartTime).Take(30).ToList().AsReadOnly();
        }

        public ArcadeSession StartSession(Player player, MinigameKind kind, int tokenCost = 1)
        {
            if (player == null) return null;

            uint sessId = ++m_nextSessionId;
            var session = new ArcadeSession(sessId, player, kind);
            m_sessions[sessId] = session;

            DebugSystem.Write($"[ArcadeMinigameEngine] Player {player.CharName} started Arcade Session #{sessId} ({kind})");

            // S->C AC 57:1 Launch Minigame UI
            SendPacket p = new SendPacket();
            p.Pack8(57);
            p.Pack8(1); // Minigame launch
            p.Pack8((byte)kind);
            p.Pack32(sessId);
            player.Send(p);

            return session;
        }

        public bool SubmitScore(Player player, uint sessionId, int score, bool won)
        {
            if (player == null) return false;

            if (m_sessions.TryGetValue(sessionId, out var session))
            {
                if (session.CharId != player.CharID || session.IsFinished)
                {
                    return false;
                }

                int maxRealisticScore = 5000;
                switch (session.GameKind)
                {
                    case MinigameKind.ClawMachine:
                        maxRealisticScore = 1000;
                        break;
                    case MinigameKind.RhythmBoxing:
                        maxRealisticScore = 9999;
                        break;
                    case MinigameKind.WhackAMole:
                        maxRealisticScore = 500;
                        break;
                    case MinigameKind.DigHole:
                        maxRealisticScore = 2000;
                        break;
                    case MinigameKind.GobangChess:
                        maxRealisticScore = 1;
                        break;
                }

                session.Score = Math.Min(score, maxRealisticScore);
                session.IsFinished = true;

                // High score tracking
                m_highScores.AddOrUpdate(player.CharID, session.Score, (k, old) => Math.Max(old, session.Score));

                // Calculate trial exp and ticket reward
                int expAward = won ? Math.Max(100, session.Score * 2) : 50;
                ushort ticketItem = (ushort)(won ? 24001 : 0); // Carnival Prize Ticket

                DebugSystem.Write($"[ArcadeMinigameEngine] Session #{sessionId} concluded! Player {player.CharName} Score: {session.Score} (Won: {won}). Awarded {expAward} EXP");

                // S->C AC 57:2 Minigame result acknowledge
                SendPacket res = new SendPacket();
                res.Pack8(57);
                res.Pack8(2);
                res.Pack8((byte)(won ? 1 : 0));
                res.Pack32((uint)session.Score);
                player.Send(res);

                return true;
            }
            return false;
        }
    }
}
