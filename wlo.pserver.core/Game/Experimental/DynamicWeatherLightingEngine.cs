using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public enum WeatherType : byte
    {
        Clear = 0,
        Rain = 1,
        Thunderstorm = 2,
        Snow = 3,
        Sandstorm = 4,
        Fog = 5
    }

    public enum TimeOfDayState : byte
    {
        Dawn = 0,
        Noon = 1,
        Dusk = 2,
        Midnight = 3
    }

    public class MapWeatherInfo
    {
        public ushort MapId { get; set; }
        public WeatherType Weather { get; set; }
        public DateTime ChangedAt { get; set; }

        public MapWeatherInfo(ushort mapId, WeatherType weather)
        {
            MapId = mapId;
            Weather = weather;
            ChangedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Dynamic Weather Simulation and Day-Night Ambient Lighting Engine (AC 88 & Spec 29/88).
    /// Simulates time-of-day illumination cycles and localized dynamic meteorological weather patterns.
    /// </summary>
    public class DynamicWeatherLightingEngine
    {
        private readonly ConcurrentDictionary<ushort, MapWeatherInfo> m_mapWeather =
            new ConcurrentDictionary<ushort, MapWeatherInfo>();
        private readonly object m_stateLock = new object();

        public TimeOfDayState CurrentTimeOfDay { get; private set; } = TimeOfDayState.Noon;
        public int ManagedMapCount => m_mapWeather.Count;

        public DynamicWeatherLightingEngine()
        {
            // Seed common overworld maps with default atmospheric profiles
            m_mapWeather[10000] = new MapWeatherInfo(10000, WeatherType.Clear);
            m_mapWeather[11000] = new MapWeatherInfo(11000, WeatherType.Rain);
            m_mapWeather[12000] = new MapWeatherInfo(12000, WeatherType.Snow);
        }

        public void SetWeather(ushort mapId, WeatherType weather)
        {
            try
            {
                var info = new MapWeatherInfo(mapId, weather);
                m_mapWeather[mapId] = info;

                DebugSystem.Write($"[DynamicWeather] Map {mapId} weather shifted to {weather}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[DynamicWeather.SetWeather] Error: {ex.Message}");
            }
        }

        public WeatherType GetWeather(ushort mapId)
        {
            if (m_mapWeather.TryGetValue(mapId, out var info))
            {
                return info.Weather;
            }
            return WeatherType.Clear;
        }

        public TimeOfDayState AdvanceTimeOfDay()
        {
            lock (m_stateLock)
            {
                switch (CurrentTimeOfDay)
                {
                    case TimeOfDayState.Dawn:
                        CurrentTimeOfDay = TimeOfDayState.Noon;
                        break;
                    case TimeOfDayState.Noon:
                        CurrentTimeOfDay = TimeOfDayState.Dusk;
                        break;
                    case TimeOfDayState.Dusk:
                        CurrentTimeOfDay = TimeOfDayState.Midnight;
                        break;
                    case TimeOfDayState.Midnight:
                        CurrentTimeOfDay = TimeOfDayState.Dawn;
                        break;
                    default:
                        CurrentTimeOfDay = TimeOfDayState.Noon;
                        break;
                }

                DebugSystem.Write($"[DynamicWeather] World ambient time shifted to {CurrentTimeOfDay}");
                return CurrentTimeOfDay;
            }
        }

        public void SetTimeOfDay(TimeOfDayState timeOfDay)
        {
            lock (m_stateLock)
            {
                CurrentTimeOfDay = timeOfDay;
                DebugSystem.Write($"[DynamicWeather] World ambient time manually set to {CurrentTimeOfDay}");
            }
        }

        public IReadOnlyList<MapWeatherInfo> GetAllWeatherStates()
        {
            return new List<MapWeatherInfo>(m_mapWeather.Values);
        }

        public SendPacket BuildWeatherPacket(ushort mapId)
        {
            WeatherType weather = GetWeather(mapId);
            SendPacket pkt = new SendPacket();
            pkt.Pack8(88); // AC 88
            pkt.Pack8((byte)weather);
            pkt.Pack8((byte)CurrentTimeOfDay);
            pkt.Pack16(mapId);
            return pkt;
        }

        public void BroadcastWeatherToPlayer(Player player)
        {
            if (player == null) return;
            try
            {
                ushort mapId = (ushort)(player.CurMap?.MapID ?? 0);
                SendPacket pkt = BuildWeatherPacket(mapId);
                player.Send(pkt);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[DynamicWeather.Broadcast] Error: {ex.Message}");
            }
        }
    }
}
