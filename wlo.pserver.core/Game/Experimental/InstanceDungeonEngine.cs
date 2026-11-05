using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Game.Code;
using Game.Maps;
using Network;

namespace Game.Experimental
{
    /// <summary>
    /// Instance Room participant data model.
    /// </summary>
    public class InstanceMember
    {
        public uint CharId { get; set; }
        public string CharName { get; set; }
        public byte Level { get; set; }
        public bool IsReady { get; set; }
        public DateTime JoinTime { get; set; }
        public Player PlayerInstance { get; set; }

        public InstanceMember(Player player)
        {
            PlayerInstance = player;
            CharId = player?.CharID ?? 0;
            CharName = player?.CharName ?? "Unknown";
            Level = player?.Level ?? 1;
            IsReady = true;
            JoinTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Instance Dungeon active room model.
    /// Manages an isolated chamber instance (GameMap) so multiple parties do not collide.
    /// </summary>
    public class InstanceRoom
    {
        public uint RoomId { get; set; }
        public string RoomName { get; set; }
        public ushort MapId { get; set; }
        public ushort BaseMapId { get; set; }
        public ushort SpawnX { get; set; } = 1000;
        public ushort SpawnY { get; set; } = 1000;
        public uint LeaderCharId { get; set; }
        public string LeaderName { get; set; }
        public int MaxMembers { get; set; }
        public int CurrentWave { get; set; }
        public int MaxWaves { get; set; }
        public bool IsInProgress { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<InstanceMember> Members { get; } = new List<InstanceMember>();

        /// <summary>
        /// Isolated GameMap instance for this dungeon chamber session.
        /// </summary>
        public GameMap InstanceMap { get; set; }

        public DateTime? EvacuationExpiresAt { get; set; }

        public InstanceRoom(uint id, string name, ushort mapId, Player leader, int maxMembers = 4, int maxWaves = 20)
        {
            RoomId = id;
            RoomName = name;
            MapId = mapId;
            BaseMapId = mapId;
            LeaderCharId = leader?.CharID ?? 0;
            LeaderName = leader?.CharName ?? "Leader";
            MaxMembers = Math.Max(1, Math.Min(4, maxMembers));
            CurrentWave = 1;
            MaxWaves = maxWaves;
            IsInProgress = false;
            CreatedAt = DateTime.UtcNow;

            if (leader != null)
            {
                Members.Add(new InstanceMember(leader));
            }
        }
    }

    /// <summary>
    /// High-performance Instance Dungeon and Wave Trial Engine.
    /// Implements AC 85 party floor advancement, Delphi MZ_DupMis.pas wire parity,
    /// and dynamic chamber map isolation for Maps 30000+.
    /// </summary>
    public class InstanceDungeonEngine
    {
        private readonly ConcurrentDictionary<uint, InstanceRoom> m_rooms = new ConcurrentDictionary<uint, InstanceRoom>();
        private uint m_nextRoomId = 1000;
        private readonly object m_lock = new object();

        public int ActiveRoomCount => m_rooms.Count;

        public IReadOnlyCollection<InstanceRoom> GetRooms()
        {
            return m_rooms.Values.ToList().AsReadOnly();
        }

        public Player FindPlayer(uint charId)
        {
            if (MapManager.Instance != null)
            {
                foreach (var map in MapManager.Instance.ActiveMaps)
                {
                    if (map?.PlayersList != null)
                    {
                        var found = map.PlayersList.FirstOrDefault(p => p != null && p.CharID == charId);
                        if (found != null) return found;
                    }
                }
            }
            return null;
        }

        public InstanceRoom GetPlayerRoom(uint charId)
        {
            return m_rooms.Values.FirstOrDefault(r => r.Members.Any(m => m.CharId == charId));
        }

        public InstanceRoom CreateRoom(Player leader, string roomName, ushort mapId, byte maxMembers = 4)
        {
            if (leader == null) return null;

            lock (m_lock)
            {
                // If player is already in an idle room, clean up previous membership
                var existing = GetPlayerRoom(leader.CharID);
                if (existing != null)
                {
                    if (!existing.IsInProgress)
                    {
                        LeaveRoom(leader);
                    }
                    else
                    {
                        DebugSystem.Write($"[InstanceDungeonEngine] {leader.CharName} already has an active dungeon in progress.");
                        SendRosterUpdate(existing, leader);
                        return existing;
                    }
                }

                uint roomId = ++m_nextRoomId;
                if (string.IsNullOrWhiteSpace(roomName))
                {
                    roomName = $"{leader.CharName}'s Trial #{roomId}";
                }

                ushort targetMap = mapId >= 30000 ? mapId : (ushort)30001; // Default to 20-Round Challenge Map 30001
                var room = new InstanceRoom(roomId, roomName, targetMap, leader, maxMembers, 20);

                // Allocate an isolated GameMap chamber instance for this party
                room.InstanceMap = MapManager.Instance?.CreateInstanceMap(targetMap);
                if (room.InstanceMap != null)
                {
                    MapManager.Instance?.RegisterInstanceMap(room.RoomId, room.InstanceMap);
                }

                m_rooms[roomId] = room;
                DebugSystem.Write($"[InstanceDungeonEngine] Created Room #{roomId} '{roomName}' for {leader.CharName} on Map {targetMap} (Isolated Map Instance: {room.InstanceMap?.GetHashCode()})");

                // Send SubCode 0 (Success & Member Roster) expected by Delphi FUN_0019da10 / FUN_001a10c4
                SendRosterUpdate(room, leader);

                // Send Room Joined Ack (SubCode 4)
                SendRoomJoinedAck(leader, room);
                return room;
            }
        }

        public bool JoinRoom(Player player, uint roomId)
        {
            if (player == null) return false;

            if (m_rooms.TryGetValue(roomId, out var room))
            {
                lock (room)
                {
                    if (room.IsInProgress)
                    {
                        DebugSystem.Write($"[InstanceDungeonEngine] Player {player.CharName} failed to join Room #{roomId}: Already in progress");
                        // Error code 12: "Make instance later"
                        SendErrorCode(player, 12);
                        return false;
                    }

                    if (room.Members.Count >= room.MaxMembers)
                    {
                        DebugSystem.Write($"[InstanceDungeonEngine] Player {player.CharName} failed to join Room #{roomId}: Room is full");
                        // Error code 1: "Max instance amount"
                        SendErrorCode(player, 1);
                        return false;
                    }

                    if (room.Members.Any(m => m.CharId == player.CharID))
                    {
                        SendRosterUpdate(room, player);
                        return true;
                    }

                    room.Members.Add(new InstanceMember(player));
                    DebugSystem.Write($"[InstanceDungeonEngine] Player {player.CharName} joined Room #{roomId} ({room.Members.Count}/{room.MaxMembers})");
                    
                    SendRoomJoinedAck(player, room);
                    SendRosterUpdate(room); // Broadcast updated roster to all members
                    return true;
                }
            }

            DebugSystem.Write($"[InstanceDungeonEngine] JoinRoom failed: Room #{roomId} does not exist.");
            SendErrorCode(player, 11); // "Unavailable yet"
            return false;
        }

        public void LeaveRoom(Player player)
        {
            if (player == null) return;

            foreach (var kvp in m_rooms)
            {
                var room = kvp.Value;
                lock (room)
                {
                    var member = room.Members.FirstOrDefault(m => m.CharId == player.CharID);
                    if (member != null)
                    {
                        room.Members.Remove(member);
                        DebugSystem.Write($"[InstanceDungeonEngine] Player {player.CharName} left Room #{room.RoomId}");

                        // If player is inside the isolated chamber, warp them out to their previous map
                        if (player.CurMap != null && room.InstanceMap != null && player.CurMap == room.InstanceMap)
                        {
                            ushort retMap = player.PrevMap != null && player.PrevMap.DstMap > 0 ? player.PrevMap.DstMap : (ushort)12000;
                            ushort retX = player.PrevMap != null && player.PrevMap.DstX_Axis > 0 ? player.PrevMap.DstX_Axis : (ushort)1000;
                            ushort retY = player.PrevMap != null && player.PrevMap.DstY_Axis > 0 ? player.PrevMap.DstY_Axis : (ushort)1000;

                            DebugSystem.Write($"[InstanceDungeonEngine] Warping exiting player {player.CharName} from chamber to Map {retMap} ({retX},{retY})");
                            player.CurMap.Teleport(TeleportType.CmD, player, 0, new WarpData { DstMap = retMap, DstX_Axis = retX, DstY_Axis = retY });
                        }

                        // Send Leave ACK (AC 85:2)
                        SendPacket leavePkt = new SendPacket();
                        leavePkt.Pack8(85);
                        leavePkt.Pack8(2); // Exit ACK
                        leavePkt.Pack8(1);
                        player.Send(leavePkt);

                        if (room.LeaderCharId == player.CharID)
                        {
                            if (room.Members.Count > 0)
                            {
                                room.LeaderCharId = room.Members[0].CharId;
                                room.LeaderName = room.Members[0].CharName;
                                DebugSystem.Write($"[InstanceDungeonEngine] Leadership of Room #{room.RoomId} transferred to {room.LeaderName}");
                                SendRosterUpdate(room);
                            }
                            else
                            {
                                // Disband empty room and release chamber map
                                if (room.InstanceMap != null)
                                {
                                    MapManager.Instance?.UnregisterInstanceMap(room.RoomId);
                                    room.InstanceMap.Dispose();
                                    room.InstanceMap = null;
                                }
                                m_rooms.TryRemove(room.RoomId, out _);
                                DebugSystem.Write($"[InstanceDungeonEngine] Room #{room.RoomId} disbanded: No remaining members");
                            }
                        }
                        else
                        {
                            SendRosterUpdate(room);
                        }
                        break;
                    }
                }
            }
        }

        public bool DismissMember(Player leader, uint targetCharId)
        {
            if (leader == null) return false;

            var room = m_rooms.Values.FirstOrDefault(r => r.LeaderCharId == leader.CharID);
            if (room != null)
            {
                lock (room)
                {
                    var target = room.Members.FirstOrDefault(m => m.CharId == targetCharId);
                    if (target != null && target.CharId != leader.CharID)
                    {
                        room.Members.Remove(target);
                        DebugSystem.Write($"[InstanceDungeonEngine] Leader {leader.CharName} dismissed {target.CharName} from Room #{room.RoomId}");

                        Player targetPlayer = target.PlayerInstance ?? FindPlayer(target.CharId);
                        if (targetPlayer != null)
                        {
                            if (targetPlayer.CurMap != null && room.InstanceMap != null && targetPlayer.CurMap == room.InstanceMap)
                            {
                                ushort retMap = targetPlayer.PrevMap != null && targetPlayer.PrevMap.DstMap > 0 ? targetPlayer.PrevMap.DstMap : (ushort)12000;
                                ushort retX = targetPlayer.PrevMap != null && targetPlayer.PrevMap.DstX_Axis > 0 ? targetPlayer.PrevMap.DstX_Axis : (ushort)1000;
                                ushort retY = targetPlayer.PrevMap != null && targetPlayer.PrevMap.DstY_Axis > 0 ? targetPlayer.PrevMap.DstY_Axis : (ushort)1000;
                                targetPlayer.CurMap.Teleport(TeleportType.CmD, targetPlayer, 0, new WarpData { DstMap = retMap, DstX_Axis = retX, DstY_Axis = retY });
                            }

                            SendPacket leavePkt = new SendPacket();
                            leavePkt.Pack8(85);
                            leavePkt.Pack8(2);
                            leavePkt.Pack8(1);
                            targetPlayer.Send(leavePkt);
                        }

                        SendRosterUpdate(room);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Starts the dungeon session: warps all party members into the isolated chamber map.
        /// </summary>
        public bool StartInstance(Player leader)
        {
            if (leader == null) return false;

            var room = m_rooms.Values.FirstOrDefault(r => r.LeaderCharId == leader.CharID);
            if (room == null)
            {
                DebugSystem.Write($"[InstanceDungeonEngine] StartInstance failed: {leader.CharName} is not leading any room.");
                return false;
            }

            lock (room)
            {
                if (room.IsInProgress)
                {
                    DebugSystem.Write($"[InstanceDungeonEngine] Room #{room.RoomId} is already in progress.");
                    return true;
                }

                room.IsInProgress = true;
                room.CurrentWave = 1;

                // Ensure isolated instance map exists
                if (room.InstanceMap == null)
                {
                    room.InstanceMap = MapManager.Instance?.CreateInstanceMap(room.MapId);
                    if (room.InstanceMap != null)
                    {
                        MapManager.Instance?.RegisterInstanceMap(room.RoomId, room.InstanceMap);
                    }
                }

                DebugSystem.Write($"[InstanceDungeonEngine] Starting Dungeon Room #{room.RoomId} on Map {room.MapId} with {room.Members.Count} members. Warping party into isolated chamber...");

                ushort spawnX = room.SpawnX > 0 ? room.SpawnX : (ushort)1000;
                ushort spawnY = room.SpawnY > 0 ? room.SpawnY : (ushort)1000;
                WarpData dungeonWarp = new WarpData { DstMap = room.MapId, DstX_Axis = spawnX, DstY_Axis = spawnY };

                // Warp every room member into the isolated chamber
                foreach (var mem in room.Members.ToList())
                {
                    Player memPlayer = mem.PlayerInstance ?? FindPlayer(mem.CharId);
                    if (memPlayer != null)
                    {
                        // Capture pre-warp location for clean return
                        if (memPlayer.CurMap != null && memPlayer.CurMap != room.InstanceMap)
                        {
                            memPlayer.PrevMap = new WarpData
                            {
                                DstMap = (ushort)memPlayer.CurMap.MapID,
                                DstX_Axis = memPlayer.CurX,
                                DstY_Axis = memPlayer.CurY
                            };
                        }

                        // Warp into isolated chamber instance
                        if (memPlayer.CurMap is GameMap currentGm && room.InstanceMap != null)
                        {
                            currentGm.TeleportToInstance(room.InstanceMap, memPlayer, dungeonWarp);
                        }
                        else
                        {
                            memPlayer.CurMap?.Teleport(TeleportType.CmD, memPlayer, 0, dungeonWarp);
                        }

                        // Send status packet (AC 85:5)
                        SendPacket startPkt = new SendPacket();
                        startPkt.Pack8(85);
                        startPkt.Pack8(5); // Instance status / active sync
                        startPkt.Pack32(room.RoomId);
                        startPkt.Pack16(room.MapId);
                        startPkt.Pack8((byte)room.CurrentWave);
                        memPlayer.Send(startPkt);

                        DebugSystem.Write($"[InstanceDungeonEngine] Warped member {memPlayer.CharName} (CharID={mem.CharId}) into instance chamber {room.MapId}");
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Handles Eve.emg Opcode 17 wave completion signal.
        /// Advances the party to the next floor/round inside Maps 30000+.
        /// </summary>
        public bool OnWaveSignal(Player player, ushort waveParam1, ushort waveParam2)
        {
            if (player == null) return false;

            var room = m_rooms.Values.FirstOrDefault(r => r.Members.Any(m => m.CharId == player.CharID));
            if (room == null)
            {
                DebugSystem.Write($"[InstanceDungeonEngine] Opcode 17 wave signal from {player.CharName} without active room");
                return false;
            }

            lock (room)
            {
                room.CurrentWave++;
                DebugSystem.Write($"[InstanceDungeonEngine] Room #{room.RoomId} wave completed! Advanced to Wave {room.CurrentWave}/{room.MaxWaves}");

                if (room.CurrentWave > room.MaxWaves)
                {
                    // Full trial cleared!
                    DebugSystem.Write($"[InstanceDungeonEngine] Room #{room.RoomId} fully cleared all {room.MaxWaves} waves! Distributing rewards.");
                    room.IsInProgress = false;

                    SendPacket victoryPkt = new SendPacket();
                    victoryPkt.Pack8(85);
                    victoryPkt.Pack8(0x0B); // Boss Defeated / Victory (FUN_001a1820)
                    victoryPkt.Pack16(120);  // Clear time in seconds
                    victoryPkt.Pack16(23001);// Trial Reward ItemID
                    victoryPkt.Pack32(5000); // Bonus EXP

                    foreach (var mem in room.Members)
                    {
                        Player memPlayer = mem.PlayerInstance ?? FindPlayer(mem.CharId);
                        memPlayer?.Send(victoryPkt);
                    }
                }
                else
                {
                    // Broadcast round advancement (AC 85:7)
                    SendPacket advPkt = new SendPacket();
                    advPkt.Pack8(85);
                    advPkt.Pack8(7); // Next wave trigger
                    advPkt.Pack8((byte)room.CurrentWave);
                    advPkt.Pack16(room.MapId);

                    foreach (var mem in room.Members)
                    {
                        Player memPlayer = mem.PlayerInstance ?? FindPlayer(mem.CharId);
                        memPlayer?.Send(advPkt);
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Sends SubCode 0 with room roster layout matching Delphi client FUN_0019da10.
        /// Prevents client displaying error code 1 ("Max instance amount").
        /// </summary>
        public void SendRosterUpdate(InstanceRoom room, Player targetPlayer = null)
        {
            if (room == null) return;

            try
            {
                SendPacket p = new SendPacket();
                p.Pack8(85);
                p.Pack8(0); // Subcode 0: Success & Member Roster
                p.Pack16(room.MapId);
                p.Pack8((byte)room.Members.Count);

                foreach (var mem in room.Members)
                {
                    p.PackStringN(mem.CharName ?? "Member");
                    p.Pack32(mem.CharId);
                }

                if (targetPlayer != null)
                {
                    targetPlayer.Send(p);
                }
                else
                {
                    foreach (var mem in room.Members)
                    {
                        Player memPlayer = mem.PlayerInstance ?? FindPlayer(mem.CharId);
                        memPlayer?.Send(p);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        public void SendRoomList(Player player, byte page = 1)
        {
            if (player == null) return;

            try
            {
                var existing = GetPlayerRoom(player.CharID);
                if (existing != null)
                {
                    SendRosterUpdate(existing, player);
                    return;
                }

                // If not in a room, send SubCode 0 with empty roster (prevents "Max instance amount" error)
                SendPacket p = new SendPacket();
                p.Pack8(85);
                p.Pack8(0); // Subcode 0: Success
                p.Pack16(30001); // Default trial chamber
                p.Pack8(0); // 0 members
                player.Send(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[InstanceDungeonEngine] SendRoomList Error: {ex.Message}");
            }
        }

        public void QueryOrPreJoin(Player player, uint instanceOrMapId)
        {
            if (player == null) return;

            var room = m_rooms.Values.FirstOrDefault(r => r.RoomId == instanceOrMapId || r.MapId == instanceOrMapId);
            if (room != null)
            {
                SendRosterUpdate(room, player);
                SendRoomJoinedAck(player, room);
            }
            else
            {
                SendPacket ack = new SendPacket();
                ack.Pack8(85);
                ack.Pack8(0); // Success response
                ack.Pack16((ushort)(instanceOrMapId >= 30000 ? instanceOrMapId : 30001));
                ack.Pack8(0);
                player.Send(ack);
            }
        }

        public void SendTabs(Player player, byte tabIndex)
        {
            if (player == null) return;

            try
            {
                SendPacket p = new SendPacket();
                p.Pack8(85);
                p.Pack8(2); // Tab list response
                p.Pack8(tabIndex);
                p.Pack8((byte)m_rooms.Count);
                player.Send(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[InstanceDungeonEngine] SendTabs Error: {ex.Message}");
            }
        }

        public void SendErrorCode(Player player, byte errorCode)
        {
            if (player == null) return;

            SendPacket p = new SendPacket();
            p.Pack8(85);
            p.Pack8(errorCode);
            player.Send(p);
        }

        private void SendRoomJoinedAck(Player player, InstanceRoom room)
        {
            if (player == null || room == null) return;

            SendPacket p = new SendPacket();
            p.Pack8(85);
            p.Pack8(4); // Room joined ACK
            p.Pack32(room.RoomId);
            p.Pack16(room.MapId);
            p.Pack8((byte)room.Members.Count);
            p.Pack8((byte)room.MaxMembers);
            player.Send(p);
        }
    }
}
