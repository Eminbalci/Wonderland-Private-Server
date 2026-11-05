using System;
using System.Collections.Concurrent;
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

        // Isolated instance chambers (dungeons, trials, raids)
        private ConcurrentDictionary<uint, GameMap> _instanceMaps = new ConcurrentDictionary<uint, GameMap>();

        public IEnumerable<GameMap> ActiveMaps
        {
            get
            {
                lock (_mapCache)
                {
                    return _mapCache.Values.Concat(_instanceMaps.Values).ToList();
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

        /// <summary>
        /// Creates a brand new, isolated GameMap instance with its own spawns and NPC list.
        /// Does not register in global map cache, enabling chamber isolation for instance dungeons.
        /// </summary>
        public GameMap CreateInstanceMap(ushort baseMapId)
        {
            GameMap tmp = new GameMap();
            tmp.MapID = baseMapId;
            tmp.ReloadSpawns();
            DebugSystem.Write($"[MapManager] Created isolated instance map {baseMapId} (instance: {tmp.GetHashCode()})");
            return tmp;
        }

        public void RegisterInstanceMap(uint instanceId, GameMap map)
        {
            if (map != null)
            {
                _instanceMaps[instanceId] = map;
                DebugSystem.Write($"[MapManager] Registered instance map #{instanceId} (BaseMap: {map.MapID}, instance: {map.GetHashCode()})");
            }
        }

        public void UnregisterInstanceMap(uint instanceId)
        {
            if (_instanceMaps.TryRemove(instanceId, out var removed))
            {
                DebugSystem.Write($"[MapManager] Unregistered instance map #{instanceId} (BaseMap: {removed?.MapID})");
            }
        }

        public GameMap GetInstanceMap(uint instanceId)
        {
            _instanceMaps.TryGetValue(instanceId, out var map);
            return map;
        }

    }
}
