using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RCLibrary;

namespace Game.Maps
{
    public class MapManager
    {

        public static MapManager Instance { get; private set; }

        // Cache to store map instances - same map ID = same instance!
        private Dictionary<ushort, GameMap> _mapCache = new Dictionary<ushort, GameMap>();

        public IEnumerable<GameMap> ActiveMaps
        {
            get
            {
                lock (_mapCache)
                {
                    return _mapCache.Values.ToList();
                }
            }
        }

        public MapManager()
        {
            Instance = this;
        }

        public GameMap GetMap(ushort ID)
        {
            lock (_mapCache)
            {
                // Check if map already exists in cache
                if (_mapCache.ContainsKey(ID))
                {
                    DebugSystem.Write($"[MapManager] Returning cached map {ID} (instance: {_mapCache[ID].GetHashCode()})");
                    return _mapCache[ID];
                }

                // Create new map and cache it
                GameMap tmp = new GameMap();
                tmp.MapID = ID;
                tmp.ReloadSpawns(); // Reload spawns now that MapID is set
                _mapCache[ID] = tmp;
                DebugSystem.Write($"[MapManager] Created new map {ID} (instance: {tmp.GetHashCode()})");
                return tmp;
            }
        }

    }
}
