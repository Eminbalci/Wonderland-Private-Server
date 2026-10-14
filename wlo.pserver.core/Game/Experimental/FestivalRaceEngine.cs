using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class RaceCheckpoint
    {
        public int Index { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Radius { get; set; }

        public RaceCheckpoint(int idx, int x, int y, int radius = 200)
        {
            Index = idx;
            X = x;
            Y = y;
            Radius = radius;
        }

        public bool IsWithin(int px, int py)
        {
            int dx = px - X;
            int dy = py - Y;
            return (dx * dx + dy * dy) <= (Radius * Radius);
        }
    }

    public class RaceTrack
    {
        public int TrackId { get; set; }
        public string TrackName { get; set; }
        public ushort MapId { get; set; }
        public int TotalLaps { get; set; }
        public int MinLapSeconds { get; set; } // Anti-speedhack minimum duration
        public List<RaceCheckpoint> Checkpoints { get; } = new List<RaceCheckpoint>();

        public RaceTrack(int id, string name, ushort mapId, int totalLaps = 3, int minSeconds = 15)
        {
            TrackId = id;
            TrackName = name;
            MapId = mapId;
            TotalLaps = totalLaps;
            MinLapSeconds = minSeconds;
        }
    }

    public class RaceRacer
    {
        public uint CharId { get; set; }
        public string CharName { get; set; }
        public int CurrentLap { get; set; }
        public int NextCheckpointIndex { get; set; }
        public DateTime LapStartTime { get; set; }
        public DateTime LastCheckpointTime { get; set; }
        public bool IsFinished { get; set; }
        public TimeSpan TotalRaceTime { get; set; }

        public RaceRacer(Player player)
        {
            CharId = player?.CharID ?? 0;
            CharName = player?.CharName ?? "Racer";
            CurrentLap = 1;
            NextCheckpointIndex = 0;
            LapStartTime = DateTime.UtcNow;
            LastCheckpointTime = DateTime.UtcNow;
            IsFinished = false;
        }
    }

    /// <summary>
    /// Seasonal Festival Races and Track Checkpoint Engine.
    /// Manages track racing checkpoints, anti-speedhack verification, and AC 226 wire protocol.
    /// </summary>
    public class FestivalRaceEngine
    {
        private readonly List<RaceTrack> m_tracks = new List<RaceTrack>();
        private readonly ConcurrentDictionary<uint, RaceRacer> m_racers = new ConcurrentDictionary<uint, RaceRacer>();
        private readonly ConcurrentDictionary<int, List<RaceRacer>> m_completedRankings = new ConcurrentDictionary<int, List<RaceRacer>>();
        private readonly object m_raceLock = new object();

        public IReadOnlyList<RaceTrack> Tracks => m_tracks.AsReadOnly();
        public int ActiveRacerCount => m_racers.Values.Count(r => !r.IsFinished);

        public FestivalRaceEngine()
        {
            // South Island Coastal Circuit (Map 11000)
            var track1 = new RaceTrack(1, "South Island Coastal Circuit", 11000, 3, 20);
            track1.Checkpoints.Add(new RaceCheckpoint(0, 800, 1200));
            track1.Checkpoints.Add(new RaceCheckpoint(1, 1600, 1400));
            track1.Checkpoints.Add(new RaceCheckpoint(2, 2200, 800));
            track1.Checkpoints.Add(new RaceCheckpoint(3, 1400, 400));
            m_tracks.Add(track1);

            // Kelan Mountain Ascent (Map 10000)
            var track2 = new RaceTrack(2, "Kelan Mountain Ascent", 10000, 2, 25);
            track2.Checkpoints.Add(new RaceCheckpoint(0, 500, 1500));
            track2.Checkpoints.Add(new RaceCheckpoint(1, 1200, 1100));
            track2.Checkpoints.Add(new RaceCheckpoint(2, 1900, 600));
            m_tracks.Add(track2);
        }

        public RaceTrack GetTrack(int trackId)
        {
            return m_tracks.FirstOrDefault(t => t.TrackId == trackId);
        }

        public bool StartRaceForPlayer(Player player, int trackId)
        {
            if (player == null) return false;

            var track = GetTrack(trackId);
            if (track == null) return false;

            var racer = new RaceRacer(player);
            m_racers[player.CharID] = racer;

            DebugSystem.Write($"[FestivalRaceEngine] Racer {player.CharName} entered {track.TrackName} (Laps: {track.TotalLaps})");

            // S->C AC 226:1 Race started
            SendPacket p = new SendPacket();
            p.Pack8(226);
            p.Pack8(1); // Race Start
            p.Pack16((ushort)track.TrackId);
            p.Pack8((byte)track.TotalLaps);
            player.Send(p);

            return true;
        }

        public bool PassCheckpoint(Player player, int trackId, int checkpointIndex)
        {
            if (player == null) return false;

            var track = GetTrack(trackId);
            if (track == null) return false;

            if (m_racers.TryGetValue(player.CharID, out var racer) && !racer.IsFinished)
            {
                lock (racer)
                {
                    if (racer.NextCheckpointIndex != checkpointIndex)
                    {
                        return false;
                    }

                    // Anti-speedhack minimum elapsed time check
                    double elapsedSinceLast = (DateTime.UtcNow - racer.LastCheckpointTime).TotalSeconds;
                    if (elapsedSinceLast < 2.0 && checkpointIndex > 0)
                    {
                        DebugSystem.Write($"[FestivalRaceEngine] Anti-cheat warning: {player.CharName} reached checkpoint {checkpointIndex} impossibly fast ({elapsedSinceLast:F1}s)");
                        return false;
                    }

                    racer.LastCheckpointTime = DateTime.UtcNow;
                    racer.NextCheckpointIndex++;

                    // Acknowledge checkpoint crossing (AC 226:2)
                    SendPacket ack = new SendPacket();
                    ack.Pack8(226);
                    ack.Pack8(2);
                    ack.Pack8((byte)checkpointIndex);
                    ack.Pack8((byte)racer.CurrentLap);
                    player.Send(ack);

                    DebugSystem.Write($"[FestivalRaceEngine] {player.CharName} cleared Checkpoint {checkpointIndex} on Lap {racer.CurrentLap}/{track.TotalLaps}");

                    // Lap completed?
                    if (racer.NextCheckpointIndex >= track.Checkpoints.Count)
                    {
                        racer.NextCheckpointIndex = 0;
                        racer.CurrentLap++;

                        if (racer.CurrentLap > track.TotalLaps)
                        {
                            // Race finished!
                            racer.IsFinished = true;
                            racer.TotalRaceTime = DateTime.UtcNow - racer.LapStartTime;

                            m_completedRankings.GetOrAdd(trackId, id => new List<RaceRacer>()).Add(racer);

                            DebugSystem.Write($"[FestivalRaceEngine] VICTORY! {player.CharName} finished {track.TrackName} in {racer.TotalRaceTime.TotalSeconds:F2} seconds!");

                            SendPacket finishPkt = new SendPacket();
                            finishPkt.Pack8(226);
                            finishPkt.Pack8(3); // Race Finished
                            finishPkt.Pack32((uint)racer.TotalRaceTime.TotalMilliseconds);
                            finishPkt.Pack16(23001); // Festival Champion Medal
                            player.Send(finishPkt);
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public IReadOnlyList<RaceRacer> GetRankings(int trackId)
        {
            if (m_completedRankings.TryGetValue(trackId, out var list))
            {
                return list.OrderBy(r => r.TotalRaceTime).ToList().AsReadOnly();
            }
            return new List<RaceRacer>().AsReadOnly();
        }
    }
}
