using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game
{
    /// <summary>
    /// Lifecycle and operational state of a World Server instance.
    /// </summary>
    public enum WorldServerState
    {
        Stopped = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3,
        Faulted = 4
    }

    /// <summary>
    /// Tick intervals and rates for World Server background loops (in milliseconds).
    /// </summary>
    public enum WorldTickRate
    {
        Realtime = 2,
        Fast = 50,
        EventTick = 120,
        MapTick = 500,
        AutoSaveStep = 250,
        Heartbeat = 1000
    }

    /// <summary>
    /// World-level events dispatched to listeners or plugins.
    /// </summary>
    public enum WorldEventType
    {
        PlayerLogon = 0,
        PlayerLogoff = 1,
        PlayerWarp = 2,
        PlayerKick = 3,
        WorldBroadcast = 4,
        MapLoaded = 5,
        MapUnloaded = 6
    }
}
