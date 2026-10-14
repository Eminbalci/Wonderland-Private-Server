using System;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 88: Dynamic Weather Simulation & Day-Night Ambient Lighting Protocol.
    /// Handles time-of-day illumination cycles and localized dynamic meteorological weather patterns.
    /// </summary>
    public class AC88 : AC
    {
        public override int ID => 88;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC88] Weather/Lighting action from {c.CharName}: SubCode={subCode}");

            try
            {
                if (ExperimentalFeaturesManager.Instance.EnableDynamicWeather)
                {
                    ushort mapId = (ushort)(c.CurMap?.MapID ?? 0);

                    switch (subCode)
                    {
                        case 1: // Query / Synchronize Weather & Ambient Lighting
                            SendPacket syncPkt = ExperimentalFeaturesManager.Instance.DynamicWeather.BuildWeatherPacket(mapId);
                            c.Send(syncPkt);
                            break;

                        case 2: // Set Weather (Admin or map trigger)
                            byte weatherCode = 0;
                            if (p.Buffer.Length >= 8)
                            {
                                weatherCode = p.Unpack8();
                            }
                            ExperimentalFeaturesManager.Instance.DynamicWeather.SetWeather(mapId, (WeatherType)weatherCode);

                            SendPacket bcastPkt = ExperimentalFeaturesManager.Instance.DynamicWeather.BuildWeatherPacket(mapId);
                            c.CurMap?.Broadcast(bcastPkt);
                            break;

                        default:
                            SendPacket resp = new SendPacket();
                            resp.Pack8((byte)ID);
                            resp.Pack8(subCode);
                            resp.Pack8(1);
                            c.Send(resp);
                            break;
                    }
                }
                else
                {
                    // Fallback agricultural/farming or basic response
                    byte plotIndex = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)0;
                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(subCode);
                    resp.Pack8(plotIndex);
                    resp.Pack8(1);
                    c.Send(resp);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
