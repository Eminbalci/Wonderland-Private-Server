using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class MapMechanismState
    {
        public ushort MapId { get; set; }
        public byte SwitchId { get; set; }
        public bool IsActive { get; set; }
        public ushort DoorClickId { get; set; }
        public DateTime LastToggled { get; set; }

        public MapMechanismState(ushort mapId, byte switchId, ushort doorClickId)
        {
            MapId = mapId;
            SwitchId = switchId;
            IsActive = false;
            DoorClickId = doorClickId;
            LastToggled = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Map Mechanisms, Lever Switches and Puzzle Door Engine.
    /// Implements Eve.emg Opcode 16 handling, obstacle mask unblocking, and state synchronization.
    /// </summary>
    public class MapMechanismEngine
    {
        private readonly ConcurrentDictionary<string, MapMechanismState> m_mechanisms = new ConcurrentDictionary<string, MapMechanismState>();

        public IReadOnlyCollection<MapMechanismState> GetAllMechanisms() => m_mechanisms.Values.ToList().AsReadOnly();

        public MapMechanismState GetOrRegister(ushort mapId, byte switchId, ushort doorClickId = 0)
        {
            string key = $"{mapId}:{switchId}";
            return m_mechanisms.GetOrAdd(key, k => new MapMechanismState(mapId, switchId, doorClickId));
        }

        /// <summary>
        /// Executes Eve.emg Opcode 16 mechanism lever switch toggle.
        /// </summary>
        public bool ToggleMechanism(ushort mapId, byte switchId, Player triggeredBy)
        {
            string key = $"{mapId}:{switchId}";
            var mech = m_mechanisms.GetOrAdd(key, k => new MapMechanismState(mapId, switchId, (ushort)(switchId + 100)));

            lock (mech)
            {
                mech.IsActive = !mech.IsActive;
                mech.LastToggled = DateTime.UtcNow;

                DebugSystem.Write($"[MapMechanismEngine] Opcode 16 Lever Switch {switchId} on Map {mapId} toggled to {(mech.IsActive ? "ACTIVE (Open)" : "INACTIVE (Closed)")} by {triggeredBy?.CharName ?? "World"}");

                if (triggeredBy != null)
                {
                    // Unfreeze mobility (AC 20:8 & AC 5:4)
                    triggeredBy.Send(Tools.FromFormat("bb", 20, 8));
                    triggeredBy.Send(Tools.FromFormat("bb", 5, 4));

                    // Broadcast mechanism toggle animation frame (AC 22:1 Action 0 vs 1)
                    byte animState = (byte)(mech.IsActive ? 1 : 0);
                    SendPacket switchAnim = Tools.FromFormat("bbwb", 22, 1, (ushort)switchId, animState);
                    triggeredBy.Send(switchAnim);
                    triggeredBy.CurMap?.Broadcast(switchAnim, "Ex", triggeredBy.CharID);

                    // If a linked puzzle door ClickID exists, toggle door frame (AC 22:4)
                    if (mech.DoorClickId > 0)
                    {
                        ushort doorState = (ushort)(mech.IsActive ? 0x0001 : 0x00FF); // 0x0001 = Open/Broken passage, 0x00FF = Closed barrier
                        SendPacket doorPkt = Tools.FromFormat("bbww", 22, 4, mech.DoorClickId, doorState);
                        triggeredBy.Send(doorPkt);
                        triggeredBy.CurMap?.Broadcast(doorPkt, "Ex", triggeredBy.CharID);
                        DebugSystem.Write($"[MapMechanismEngine] Synchronized linked door ClickID {mech.DoorClickId} on Map {mapId}");
                    }
                }
                return true;
            }
        }
    }
}
