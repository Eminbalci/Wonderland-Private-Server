using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.Maps
{
    /// <summary>
    /// Per-run variables of the mission maps (30000-30041). Eve opcode 17 [index,1,0,value]
    /// adds to a variable (e.g. battles won, boss defeated) and branch condition type 19
    /// [index, mode, comparison, value] tests it: mode 1 compares, mode 2 means "not started" (== 0).
    /// Variables reset when the player enters a different mission map.
    /// </summary>
    public static class MissionVariables
    {
        private sealed class State
        {
            public uint MapId;
            public readonly Dictionary<int, int> Values = new Dictionary<int, int>();
        }

        private static readonly ConditionalWeakTable<Player, State> states = new ConditionalWeakTable<Player, State>();

        private static State For(Player player, uint mapId)
        {
            var state = states.GetValue(player, p => new State { MapId = mapId });
            lock (state)
            {
                if (state.MapId != mapId)
                {
                    state.Values.Clear();
                    state.MapId = mapId;
                }
            }
            return state;
        }

        public static int Get(Player player, uint mapId, int index)
        {
            if (player == null) return 0;
            var state = For(player, mapId);
            lock (state)
            {
                int value;
                return state.Values.TryGetValue(index, out value) ? value : 0;
            }
        }

        public static int Add(Player player, uint mapId, int index, int delta)
        {
            if (player == null) return 0;
            var state = For(player, mapId);
            lock (state)
            {
                int value;
                state.Values.TryGetValue(index, out value);
                value += delta;
                state.Values[index] = value;
                return value;
            }
        }

        public static bool MatchesCondition(Player player, uint mapId, int index, int mode, int comparison, int value, Func<int, int, int, bool> compare)
        {
            if (player == null) return false;
            int actual = Get(player, mapId, index);
            if (mode == 1) return compare(actual, value, comparison);
            if (mode == 2) return actual == 0;
            return false;
        }
    }
}
