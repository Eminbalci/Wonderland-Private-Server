using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Game.Experimental
{
    /// <summary>
    /// Carnival arcade game definitions and prize tables, copied from Wonderland-Gonline's
    /// arcade_defaults.json (internal/assets/arcade.go). Loaded once from Data/arcade_games.json.
    /// The arcade logic itself (AC71) still answers with a fixed reply; this table is the data for it.
    /// </summary>
    public static class ArcadeGameTable
    {
        public class Reward
        {
            public byte index { get; set; }
            public ushort item_id { get; set; }
            public byte quantity { get; set; }
            public long weight { get; set; }
            public byte[] reels { get; set; }
        }

        public class Game
        {
            public byte kind { get; set; }
            public bool enabled { get; set; }
            public long point_cost { get; set; }
            public byte token_count { get; set; }
            public uint cooldown_milliseconds { get; set; }
            public Reward[] rewards { get; set; }
        }

        private static List<Game> _games = new List<Game>();

        public static IReadOnlyList<Game> Games => _games;

        public static void Load(string path)
        {
            var loaded = new List<Game>();
            try
            {
                if (File.Exists(path))
                {
                    var parsed = new JavaScriptSerializer().Deserialize<Game[]>(File.ReadAllText(path)) ?? new Game[0];
                    loaded.AddRange(parsed.Where(g => g != null && g.rewards != null && g.rewards.Length > 0));
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write("[Arcade] Failed to load arcade table: " + ex.Message);
            }
            _games = loaded;
            DebugSystem.Write($"[Arcade] Loaded {_games.Count} arcade game definitions.");
        }

        /// <summary>Weighted draw over one game's prize rows (Gonline TotalWeight/Pick). Null when the game is not drawable.</summary>
        public static Reward Draw(byte kind, long roll)
        {
            var game = Find(kind);
            if (game == null || !game.enabled) return null;
            long total = game.rewards.Sum(r => r.weight);
            if (total <= 0 || roll < 0 || roll >= total) return null;
            foreach (var reward in game.rewards)
            {
                if (roll < reward.weight) return reward;
                roll -= reward.weight;
            }
            return null;
        }

        public static Game Find(byte kind)
        {
            return _games.FirstOrDefault(g => g.kind == kind);
        }
    }
}
