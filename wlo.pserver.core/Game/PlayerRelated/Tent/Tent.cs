using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Threading;
using System.Collections.Concurrent;
using DataFiles;
using Game.Maps;
using Network;


namespace Game.Code
{



    public partial class Tent : GameMap
    {
        private struct TentAccessoryEntry
        {
            public ushort clientIdx;
            public ushort toolId;
            public ushort hostIdx;

            public TentAccessoryEntry(ushort clientIdx, ushort toolId, ushort hostIdx)
            {
                this.clientIdx = clientIdx;
                this.toolId = toolId;
                this.hostIdx = hostIdx;
            }
        }
        Game.Player _owner;
        GameMap _ownerMap;
        //List<TentFloor> _floors;

        uint _mapx, _mapy;
        bool _closed;

        ushort _floorcolor = 39062, _wallcolor = 39064;
        ushort _floor2color = 0, _wall2color = 0;
        bool _locked = false;
        bool _enlarged = false;
        byte _tenttype = 1;

        public ushort Floor1Color { get { return _floorcolor; } set { _floorcolor = value; } }
        public ushort Floor1Wallpaper { get { return _wallcolor; } set { _wallcolor = value; } }
        public ushort Floor2Color { get { return _floor2color; } set { _floor2color = value; } }
        public ushort Floor2Wallpaper { get { return _wall2color; } set { _wall2color = value; } }
        public bool Locked { get { return _locked; } set { _locked = value; } }
        public bool Enlarged { get { return _enlarged; } set { _enlarged = value; } }
        public byte TentType { get { return _tenttype == 0 ? (byte)1 : _tenttype; } set { _tenttype = (value == 0 ? (byte)1 : value); } }
        public bool IsClosed { get { return _closed; } }
        public bool IsDirty { get; set; } = false;

        // TENT ITEMS
        private List<TentItem> _tentObjects;
        public List<TentItem> TentObjects { get { return _tentObjects; } }

        public Tent(Game.Player src)
        {
            _owner = src;
            _closed = true;

            // Initialize items collection
            _tentObjects = new List<TentItem>();

            // Assign unique MapID for this tent instance
            // Using 100000 + CharID to ensure unique MapID per player
            this.MapID = 100000 + src.CharID;

            InitializeDefaultItems();
        }

        public uint X { get { return _mapx; } }
        public uint Y { get { return _mapy; } }
        public GameMap OwnerMap { get { return _ownerMap; } }
        public uint OwnerMapID { get { return _ownerMap != null ? _ownerMap.MapID : 0; } }
        public Game.Player Owner { get { return _owner; } }

        public void SetReturnLocation(ushort mapId, ushort x, ushort y)
        {
            if (mapId > 0)
            {
                if (MapManager.Instance != null)
                {
                    _ownerMap = MapManager.Instance.GetMap(mapId);
                }
                if (x > 0 && y > 0 && !(x == 460 && y == 700))
                {
                    _mapx = x;
                    _mapy = y;
                }

                // Update exit portal 1 target
                WarpPortal exitPortal;
                if (this.Portals.ContainsKey(1))
                {
                    exitPortal = this.Portals[1];
                }
                else
                {
                    exitPortal = new WarpPortal();
                    this.Portals.Add(1, exitPortal);
                }

                exitPortal.DstID = (int)mapId;
                exitPortal.x = (int)_mapx;
                exitPortal.y = (int)_mapy;
                exitPortal.accessBy = AccessFlags.Any;
            }
        }

        public void Open()
        {
            if (!_closed) return;
            if (_owner.CurMap is Game.Code.Tent || _owner.CurMap?.Type == MapType.Tent)
            {
                DebugSystem.Write($"[Tent.Open] {_owner.CharName} attempted to open tent while already inside a tent.");
                return;
            }
            _mapx = _owner.CurX;
            _mapy = _owner.CurY;

            // Fix: Cast explicitly
            if (_owner.CurMap is GameMap)
                _ownerMap = (GameMap)_owner.CurMap;
            else
                _ownerMap = null;

            if (_ownerMap == null) return;

            // Save owner's overworld return point before entering
            if (_ownerMap.Type != MapType.Tent)
            {
                _owner.TentReturnMap = new WarpData()
                {
                    DstMap = (ushort)_ownerMap.MapID,
                    DstX_Axis = (ushort)_mapx,
                    DstY_Axis = (ushort)_mapy
                };
                _owner.PrevMap = new WarpData()
                {
                    DstMap = (ushort)_ownerMap.MapID,
                    DstX_Axis = (ushort)_mapx,
                    DstY_Axis = (ushort)_mapy
                };
                DebugSystem.Write($"[Tent.Open] Saved return location for {_owner.CharName}: Map {_ownerMap.MapID} ({_mapx},{_mapy})");
            }

            // Create/Update Exit Portal (ID 1) to return player to where they came from
            WarpPortal exitPortal;
            if (this.Portals.ContainsKey(1))
            {
                exitPortal = this.Portals[1];
            }
            else
            {
                exitPortal = new WarpPortal();
                this.Portals.Add(1, exitPortal);
            }

            exitPortal.DstID = (int)_ownerMap.MapID;
            exitPortal.x = (int)_mapx;
            exitPortal.y = (int)_mapy;
            exitPortal.accessBy = AccessFlags.Any;

            _ownerMap.onTentOpened(this);
            _owner.Send(Tools.FromFormat("bbb", 62, 59, 2));

            // Send items immediately upon opening/entering
            // Tent items synchronize upon entering interior (SendMapInfo / AC12:1)

            _closed = false;
        }
        public void Close()
        {
            if (_closed) return;
            if (_ownerMap != null)
            {
                _ownerMap.onTentClosing(this);

                // Teleport all occupants currently inside the tent back to the overworld
                foreach (var player in m_playerlist.ToList())
                {
                    if (player != null && player.CurMap == this)
                    {
                        ushort dstMap = (ushort)_ownerMap.MapID;
                        ushort dstX = (ushort)_mapx;
                        ushort dstY = (ushort)_mapy;

                        if (player.TentReturnMap != null && player.TentReturnMap.DstMap > 0)
                        {
                            dstMap = player.TentReturnMap.DstMap;
                            dstX = player.TentReturnMap.DstX_Axis;
                            dstY = player.TentReturnMap.DstY_Axis;
                        }

                        WarpData warp = new WarpData()
                        {
                            DstMap = dstMap,
                            DstX_Axis = dstX,
                            DstY_Axis = dstY
                        };
                        player.Flags.Remove(PlayerFlag.InTent);
                        player.CurMap.Teleport(TeleportType.CmD, player, 0, warp);
                        player.SaveCharacterData();
                        DebugSystem.Write($"[Tent.Close] Teleported occupant {player.CharName} out of closing tent to Map {dstMap} ({dstX},{dstY})");
                    }
                }
            }
            _closed = true;
        }

        #region Map Base
        public override uint MapID
        {
            get
            {
                return _owner.CharID;
            }

            set
            {
            }
        }
        public override string MapName
        {
            get
            {
                return _owner.CharName + "'s Home";
            }
        }
        public override MapType Type
        {
            get
            {
                return MapType.Tent;
            }
        }

        protected override void LoadData()
        {
            base.LoadData();
        }


        protected override void Warp_In(TeleportType teletype, Player src, WarpData from = null, byte portalID = 0)
        {
            base.Warp_In(teletype, src, from, portalID);
        }
        protected override void Warp_Out(byte portalID, Player src, WarpData To, bool toTent)
        {
            base.Warp_Out(portalID, src, To, toTent);
        }
        protected async override void SendMapInfo(Player t, bool login = false)
        {
            SendPacket p = new SendPacket();



            //build queue
            //storeroom
            #region Tent Interior Setup & Items Send
            byte tentType = this.TentType;
            if (tentType == 0) tentType = 1;

            // 1. Establish floor bounds & interior graphics (AC 65:12)
            t.Send(Tools.FromFormat("bbbb", 65, 12, tentType, 0));
            if (Enlarged)
            {
                t.Send(Tools.FromFormat("bbbb", 65, 12, tentType, 1));
            }

            // 2. Lock status (AC 65:7)
            t.Send(Tools.FromFormat("bbbb", 65, 7, (byte)(Locked ? 1 : 0), 0));

            // 3. Floor texture & wallpaper (AC 62:14 & AC 62:15)
            t.Send(Tools.FromFormat("bbw", 62, 14, _floorcolor));
            t.Send(Tools.FromFormat("bbw", 62, 15, _wallcolor));

            // 4. Placed tent furniture items (AC 62:4)
            // Ensure default items exist in memory, but DO NOT send AC 62:4 here during Warp_In.
            // Items are sent cleanly in AC12.Recv1 once the client acknowledges map load completion (MapID 63507).
            EnsureDefaultItemsIfEmpty();
            DebugSystem.Write(DebugItemType.Error, $"[Tent] SendMapInfo called for {t.CharName} - items deferred to AC12:1 map load completion");
            #endregion

            //extended Tent Item Info


            //ParkingGarage Info

            //p = new SendPacket();
            //p.PackArray(new byte[] { 23, 138 });
            //t.SendPacket(p);

            RCLibrary.Core.Networking.PacketBuilder tmp = new RCLibrary.Core.Networking.PacketBuilder();
            tmp.Begin(null);
            tmp.Add(Tools.FromFormat("bb", 23, 138));

            foreach (var r in m_playerlist)
            {
                tmp.Add(Tools.FromFormat("bbd", 23, 122, r.CharID));
                tmp.Add(Tools.FromFormat("bbdb", 10, 3, r.CharID, 255));

                if (r.CharID != t.CharID)
                {
                    r.Send(Tools.FromFormat("bbd", 23, 122, t.CharID));
                    r.Send(Tools.FromFormat("bbdb", 10, 3, t.CharID, 255));

                    if (r.Emote != 0)
                        tmp.Add(Tools.FromFormat("bbdb", 32, 2, r.CharID, r.Emote));

                    #region Pets in Map
                    //if (t.Pets.BattlePet != null)//to them
                    //{
                    //    SendPacket tmp = new SendPacket();
                    //    tmp.PackArray(new byte[] { 15, 4 });
                    //    tmp.Pack(t.CharID);
                    //    tmp.Pack(t.Pets.BattlePet.ID);
                    //    tmp.Pack((byte)0);
                    //    tmp.Pack((byte)1);
                    //    tmp.PackString(t.Pets.BattlePet.Name);
                    //    tmp.Pack16(0);//weapon
                    //    r.Send(tmp);
                    //}
                    //if (r.Pets.BattlePet != null)//to me
                    //{
                    //    SendPacket tmp = new SendPacket();
                    //    tmp.PackArray(new byte[] { 15, 4 });
                    //    tmp.Pack(r.CharID);
                    //    tmp.Pack(r.Pets.BattlePet.ID);
                    //    tmp.Pack((byte)0);
                    //    tmp.Pack((byte)1);
                    //    tmp.PackString(r.Pets.BattlePet.Name);
                    //    tmp.Pack16(0);//weapon
                    //    t.Send(tmp);
                    //}

                    #endregion
                    #region Riceball
                    //if (characters_in_map[a].riceBall.id > 0)
                    //{
                    //    if (characters_in_map[a].riceBall.active) g.ac5.Send_5(characters_in_map[a].riceBall.id, characters_in_map[a], t);
                    //}
                    //if (t.riceBall.id > 0)
                    //{
                    //    if (t.riceBall.active) g.ac5.Send_5(t.riceBall.id, t, characters_in_map[a]);
                    //}
                    #endregion
                    #region Team
                    //if (t.MyTeam.PartyLeader && t.MyTeam.hasParty && plist[a] != t)
                    //{
                    //    SendPacket fg = t.MyTeam._13_6;
                    //    plist[a].Send(fg);
                    //}
                    //if (plist[a].MyTeam.PartyLeader && plist[a].MyTeam.hasParty)
                    //{
                    //    SendPacket fg = plist[a].MyTeam._13_6;
                    //    t.Send(fg);
                    //}
                    #endregion
                    //if (Player.PlayerID != t.PlayerID)
                    //g.ac23.Send_74(Player.PlayerID, 0, c); //TODO find out what this does
                    #region Pets in Map
                    //AC 15,4 //possibly pet info for players on map with pets

                    //if (plist[a].CharacterState == PlayerState.inBattle)
                    //{
                    //    SendPacket qp = new SendPacket(t);
                    //    qp.PackArray(new byte[]{(11, 4);
                    //    qp.Pack((byte)2);
                    //    qp.Pack(plist[a].CharacterID);
                    //    qp.Pack16(0);
                    //    qp.Pack((byte)0);
                    //    qp.Send();
                    //}
                    #endregion
                    //23_76                    
                }
                tmp.Add(Tools.FromFormat("bbd", 23, 76, r.CharID));

            }

            tmp.Add(Tools.FromFormat("bb", 23, 102));
            tmp.Add(Tools.FromFormat("bb", 20, 8));
            t.LastSpawnX = t.CurX;
            t.LastSpawnY = t.CurY;
            t.LastTeleportTime = DateTime.UtcNow;
            t.Send(new SendPacket(tmp.End()));
            t.Flags.Add(Game.PlayerFlag.InTent); //t.CharacterState = PlayerState.inMap;
        }

        #endregion

        void onPlayerLeaving(Game.Player src)
        {
        }

        public override void Process(Player src, RecievePacket data)
        {
            //resets pointer in packet
            data.SetPtr();



            base.Process(src, data);
        }

        #region Tent Item Management

        /// <summary>
        /// Ensure beginner tent items exist if tent has 0 items
        /// </summary>
        public void EnsureDefaultItemsIfEmpty()
        {
            if (_tentObjects == null) _tentObjects = new List<TentItem>();
            if (_tentObjects.Count == 0)
            {
                InitializeDefaultItems();
            }
        }

        /// <summary>
        /// Initialize default tent items (Resource Recycling and Work Platform)
        /// </summary>
        void InitializeDefaultItems()
        {
            if (_tentObjects == null) _tentObjects = new List<TentItem>();
            if (_tentObjects.Count == 0)
            {
                // Default beginner crafting furniture in Wonderland Online
                PlaceItem(38055, 34, 42, 0, 0); // Resource Recycling / Trash Can (38055)
                PlaceItem(38049, 37, 42, 0, 0); // Work Platform / Low Workbench (38049)
            }
        }

        /// <summary>
        /// Place an item in the tent
        /// </summary>
        public TentItem PlaceItem(ushort itemID, int x, int y, int floor, byte rotation, IEnumerable<ushort> attachedTools = null)
        {
            try
            {
                TentItem newItem = new TentItem(); // Default constructor

                var info = _owner?.Inv?.ItemDat?.GetItemByID(itemID);
                if (info != null)
                {
                    newItem.CopyFrom(info);
                }
                else
                {
                    PhxItemInfo fallback = new PhxItemInfo();
                    fallback.ItemID = itemID;
                    newItem.CopyFrom(fallback);
                }

                newItem.tentX = (ushort)x; // camelCase
                newItem.tentY = (ushort)y;
                newItem.tentZ = 1;
                newItem.floor = (byte)floor;
                newItem.rotate = rotation;

                if (attachedTools != null)
                {
                    foreach (var t in attachedTools)
                    {
                        if (t > 0 && !newItem.AttachedTools.Contains(t))
                        {
                            newItem.AttachedTools.Add(t);
                        }
                    }
                }

                _tentObjects.Add(newItem);
                IsDirty = true;
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Placed item {itemID} at ({x},{y}) on floor {floor} (tools={newItem.AttachedTools.Count})");
                return newItem;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Error placing item: {ex.Message}");
                return null;
            }
        }

        public int CalculateTotalItemCount()
        {
            int total = 0;
            if (_tentObjects != null)
            {
                total += _tentObjects.Count;
                foreach (var item in _tentObjects)
                {
                    if (item != null) total += item.AttachedTools.Count;
                }
            }
            return total;
        }

        /// <summary>
        /// Remove an item from the tent at specific coordinates
        /// </summary>
        public bool RemoveItem(int x, int y, int floor)
        {
            var itemToRemove = _tentObjects.FirstOrDefault(i => i.tentX == x && i.tentY == y && i.floor == floor);

            if (itemToRemove != null)
            {
                _tentObjects.Remove(itemToRemove);
                IsDirty = true;
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Removed item at ({x},{y})");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove an item from the tent at specific index
        /// </summary>
        public bool RemoveItem(ushort index)
        {
            if (index < _tentObjects.Count)
            {
                var removed = _tentObjects[index];
                _tentObjects.RemoveAt(index);
                IsDirty = true;
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Removed item {removed?.ItemID} at index {index}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check whether an area on a specific floor is occupied by any placed tent object
        /// </summary>
        public bool IsAreaOccupied(int x, int y, int w, int h, int floor, TentItem ignoreItem = null)
        {
            foreach (var other in _tentObjects)
            {
                if (other == null || other == ignoreItem || other.ItemID == 0) continue;
                if (other.floor != floor) continue;

                int otherW = (other.rotate == 1 || other.rotate == 3) ? other.Height : other.Width;
                int otherH = (other.rotate == 1 || other.rotate == 3) ? other.Width : other.Height;
                if (otherW <= 0) otherW = 1;
                if (otherH <= 0) otherH = 1;

                bool overlapX = x < (other.tentX + otherW) && (x + w) > other.tentX;
                bool overlapY = y < (other.tentY + otherH) && (y + h) > other.tentY;

                if (overlapX && overlapY) return true;
            }
            return false;
        }

        /// <summary>
        /// Move an item in the tent (identified by index)
        /// </summary>
        public void MoveItem(ushort index, int x, int y, int z, byte rotation)
        {
            if (index < _tentObjects.Count)
            {
                var item = _tentObjects[index];
                item.tentX = (ushort)x;
                item.tentY = (ushort)y;
                item.tentZ = (ushort)(z > 0 ? z : 1);
                item.rotate = rotation;
                IsDirty = true;
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Moved item {index} to ({x},{y}) Z={item.tentZ} Rot={rotation}");
            }
            else
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Move failed: Index {index} out of range (Count: {_tentObjects.Count})");
            }
        }

        /// <summary>
        /// Send all tent items to the player using native WLO AC 62:4 protocol
        /// </summary>
        public void SendTentItemsToPlayer(Player player)
        {
            if (player == null) return;
            try
            {
                SendPacket pkt = new SendPacket();
                pkt.Pack8(62); // ActionCode 62 (0x3E)
                pkt.Pack8(4);  // SubCode 4
                pkt.Pack32((uint)(_owner?.CharID ?? player.CharID));

                int rootCount = _tentObjects.Count(i => i != null && i.ItemID > 0);
                ushort nextAccessoryIndex = (ushort)(rootCount + 1);

                var accessoryEntries = new List<TentAccessoryEntry>();

                ushort clientIndex = 1;
                int sentCount = 0;
                foreach (var item in _tentObjects)
                {
                    if (item == null || item.ItemID == 0) continue;

                    ushort currentHostIndex = clientIndex;
                    ushort[] accessorySlots = new ushort[10];

                    for (int s = 0; s < item.AttachedTools.Count && s < 10; s++)
                    {
                        ushort toolId = item.AttachedTools[s];
                        ushort accIdx = nextAccessoryIndex++;
                        accessorySlots[s] = accIdx;
                        accessoryEntries.Add(new TentAccessoryEntry(accIdx, toolId, currentHostIndex));
                    }

                    pkt.Pack16(clientIndex);
                    pkt.Pack16(item.ItemID);
                    pkt.Pack32((uint)item.tentX);
                    pkt.Pack32((uint)item.tentY);
                    pkt.Pack32(1); // Z = 1
                    pkt.Pack8(item.rotate);
                    pkt.Pack16(0); // Offset 15..16: Parent = 0 (2B)
                    pkt.Pack16(0); // Offset 17..18: Reserved / Slot 0 padding (2B)

                    // Offset 19..38: 10 accessory slots (ushort each for slots 1 to 10)
                    for (int s = 0; s < 10; s++)
                    {
                        pkt.Pack16(accessorySlots[s]);
                    }

                    pkt.PackArray(new byte[9]);  // Offset 39..47: 9 bytes padding
                    pkt.Pack8((byte)item.floor); // Offset 48: floor index (1B)
                    pkt.PackArray(new byte[8]);  // Offset 49..56: 8 bytes trailing padding (57 bytes total)

                    clientIndex++;
                    sentCount++;
                }

                // Append accessory child items
                foreach (var acc in accessoryEntries)
                {
                    pkt.Pack16(acc.clientIdx);
                    pkt.Pack16(acc.toolId);
                    pkt.Pack32(0); // X = 0
                    pkt.Pack32(0); // Y = 0
                    pkt.Pack32(1); // Z = 1
                    pkt.Pack8(0);  // rotate = 0
                    pkt.Pack16(acc.hostIdx);     // Offset 15..16: Parent = hostIdx! (2B)
                    pkt.Pack16(0);                // Offset 17..18: Reserved / Slot 0 padding (2B)
                    pkt.PackArray(new byte[20]);  // Offset 19..38: 10 accessory slots (all 0)
                    pkt.PackArray(new byte[9]);   // Offset 39..47: 9 bytes padding
                    pkt.Pack8(0);                 // Offset 48: floor = 0 (1B)
                    pkt.PackArray(new byte[8]);   // Offset 49..56: 8 bytes trailing padding (57 bytes total)
                    sentCount++;
                }

                player.Send(pkt);
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Sent {sentCount} items (including {accessoryEntries.Count} accessories) via AC 62:4 to {player.CharName} (OwnerCharID={_owner?.CharID ?? player.CharID}, MapID={this.MapID})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Error sending items to player: {ex}");
            }
        }

        /// <summary>
        /// Send all tent items to all occupants currently inside the tent
        /// </summary>
        public void SendTentItemsToAll()
        {
            foreach (var player in m_playerlist.ToList())
            {
                if (player != null)
                {
                    SendTentItemsToPlayer(player);
                }
            }
        }
        #endregion

    }
}
