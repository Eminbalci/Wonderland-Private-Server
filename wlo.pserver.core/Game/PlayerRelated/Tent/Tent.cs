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
        public void PlaceItem(ushort itemID, int x, int y, int floor, byte rotation)
        {
            try
            {
                TentItem newItem = new TentItem(); // Default constructor

                PhxItemInfo info = new PhxItemInfo();
                info.ItemID = itemID;
                newItem.CopyFrom(info); // Use CopyFrom inheriting from Item

                newItem.tentX = (ushort)x; // camelCase
                newItem.tentY = (ushort)y;
                newItem.floor = (byte)floor;
                newItem.rotate = rotation;

                _tentObjects.Add(newItem);
                IsDirty = true;
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Placed item {itemID} at ({x},{y}) on floor {floor}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Error placing item: {ex.Message}");
            }
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
        /// Move an item in the tent (identified by index)
        /// </summary>
        public void MoveItem(ushort index, int x, int y, int floor, byte rotation)
        {
            if (index < _tentObjects.Count)
            {
                var item = _tentObjects[index];
                item.tentX = (ushort)x;
                item.tentY = (ushort)y;
                item.floor = (byte)floor;
                item.rotate = rotation;
                IsDirty = true;
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Moved item {index} to ({x},{y})");
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

                ushort clientIndex = 1;
                int sentCount = 0;
                foreach (var item in _tentObjects)
                {
                    if (item == null || item.ItemID == 0) continue;

                    pkt.Pack16(clientIndex);
                    pkt.Pack16(item.ItemID);
                    pkt.Pack32((uint)item.tentX);
                    pkt.Pack32((uint)item.tentY);
                    pkt.Pack32(1); // Z / Layer MUST BE 1 for ground level furniture (0 is rejected by client FUN_003e67c0)
                    pkt.Pack8(item.rotate);
                    pkt.PackArray(new byte[32]); // 32 bytes padding (offset 15..46)
                    pkt.Pack8((byte)item.floor); // Floor index at offset 47 (0 = ground floor, 1 = upstairs)
                    pkt.PackArray(new byte[9]);  // 9 bytes trailing padding (offset 48..56) -> 57 bytes item data total

                    clientIndex++;
                    sentCount++;
                }

                player.Send(pkt);
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Sent {sentCount} items via AC 62:4 to {player.CharName} (OwnerCharID={_owner?.CharID ?? player.CharID}, MapID={this.MapID})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Error sending items to player: {ex.Message}");
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
