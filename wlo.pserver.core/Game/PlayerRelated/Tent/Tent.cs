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

        public ushort Floor1Color { get { return _floorcolor; } set { _floorcolor = value; } }
        public ushort Floor1Wallpaper { get { return _wallcolor; } set { _wallcolor = value; } }
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

        public void Open()
        {
            if (!_closed) return;
            _mapx = _owner.CurX;
            _mapy = _owner.CurY;

            // Fix: Cast explicitly
            if (_owner.CurMap is GameMap)
                _ownerMap = (GameMap)_owner.CurMap;
            else
                _ownerMap = null;

            if (_ownerMap == null) return;

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
            SendTentItemsToPlayer(_owner);

            _closed = false;
        }
        public void Close()
        {
            if (_closed) return;
            if (_ownerMap != null)
            {
                _ownerMap.onTentClosing(this);
                if (_owner != null && (_owner.CurMap == this || _owner.CurMap?.Type == MapType.Tent))
                {
                    WarpData warp = new WarpData();
                    warp.DstMap = (ushort)_ownerMap.MapID;
                    warp.DstX_Axis = (ushort)_mapx;
                    warp.DstY_Axis = (ushort)_mapy;
                    _owner.CurMap.Teleport(TeleportType.CmD, _owner, 0, warp);
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
            #region TentItems Send
            // REVERTING: SubCmd 3 was showing items (even if outside tent)
            // Using SubCmd 3 format that worked before
            SendPacket initPacket = new SendPacket();
            initPacket.Pack8(23);
            initPacket.Pack8(3);
            t.Send(initPacket);

            DebugSystem.Write(DebugItemType.Error, $"[Tent] SendMapInfo called - sending items to {t.CharName}");
            SendTentItemsToPlayer(t);
            #endregion

            // TEMPORARILY DISABLED FOR TESTING
            //t.Send(Tools.FromFormat("bbw", 62, 14, _floorcolor));//floor
            //t.Send(Tools.FromFormat("bbw", 62, 15, _wallcolor));//wallpaper

            //65,11 ???

            p = new SendPacket();
            p.PackArray(new byte[] { 65, 7 });
            p.Pack16(0);
            t.Send(p);

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
        /// Initialize default tent items (Resource Recycling and Work Platform)
        /// </summary>
        void InitializeDefaultItems()
        {
            // Default beginner crafting furniture in Wonderland Online
            PlaceItem(38027, 43, 42, 0, 0); // Coconut Basin (38027)
            PlaceItem(38049, 45, 42, 0, 0); // Work Platform / Low Workbench (38049)
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
        /// Send all tent items to the player using discovered protocol
        /// </summary>
        public void SendTentItemsToPlayer(Player player)
        {
            if (player == null) return;
            try
            {
                int sentCount = 0;
                foreach (var item in _tentObjects)
                {
                    if (item.ItemID == 0) continue;

                    SendPacket pkt = new SendPacket();
                    pkt.Pack8(23);
                    pkt.Pack8(3);
                    pkt.Pack16(item.ItemID);
                    pkt.Pack32((uint)item.tentX);
                    pkt.Pack32((uint)item.tentY);
                    pkt.Pack32((uint)item.floor);
                    pkt.Pack8(1);
                    pkt.Pack8(item.rotate);
                    pkt.Pack16(0);
                    player.Send(pkt);
                    sentCount++;
                }

                DebugSystem.Write(DebugItemType.Error, $"[Tent]  Sent {sentCount} items via AC 23:3 to {player.CharName} (MapID={this.MapID})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Error sending items to player: {ex.Message}");
            }
        }
        #endregion

    }
}
