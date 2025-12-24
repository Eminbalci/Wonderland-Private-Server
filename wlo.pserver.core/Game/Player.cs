using Game.Code;
using Game.Maps;
using Network;
using RCLibrary.Core.Networking;
using System;
using System.Collections.Generic;
//using Server.Events;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Game.Code.PlayerRelated;

namespace Game
{
    public delegate void PlayerSocketInfo(Player src);

    public class PlayerFlagManager
    {
        List<PlayerFlag> m_Flags;

        public PlayerFlagManager()
        {
            m_Flags = new List<PlayerFlag>();
        }

        public void Add(params PlayerFlag[] flag)
        {
            foreach (var f in flag)
                if (!m_Flags.Contains(f))
                    m_Flags.Add(f);
        }
        public void Remove(params PlayerFlag[] flag)
        {
            foreach (var f in flag)
                if (m_Flags.Contains(f))
                    m_Flags.Remove(f);
        }
        public bool HasFlag(PlayerFlag flag)
        {
            return m_Flags.Contains(flag);
        }
    }


    public class Player : Game.Character, IDisposable, INotifyPropertyChanged
    {

        #region Events
        public event PlayerSocketInfo Disconnected;
        #endregion

        #region Definitions

        readonly object mlock = new object();

        SocketClient m_socket;
        Thread net;

        PlayerFlagManager m_Flags;

        Queue<SendPacket> QueueData;
        SendMode m_sendMode;

        WarpData prevMap;
        WarpData returnSpawnMap;
        WarpData recordMap;
        WarpData gpsMap;

        int slot;
        byte emote;

        User m_useracc;
        Inventory m_inv;
        List<Quest> m_started_Quests;
        ClientSettings m_settings;
        Game.Battle.BattleScene m_battle;
        MailManager m_Mail;
        Friendlist m_friendlist;
        RiceBall m_riceball;
        PetList m_petlist;
        Game.Code.Tent m_tent;

        // Active mount/pet/vehicle tracking for broadcasting to other players
        public uint ActiveVehicleID { get; set; } = 0;
        public uint ActiveMountID { get; set; } = 0; // Riding pet
        public uint ActivePetID { get; set; } = 0; // Battle pet

        // FIX: Added properties for ActionCodes compatibility
        public Game.Battle.BattleScene BattleScene { get { return m_battle; } }
        public uint UserID { get { return m_useracc != null ? m_useracc.UserID : 0; } }
        public int ID { get { return (int)UserID; } }
        public object CurGuild { get { return null; } } // Placeholder (object to bypass type error)
        public object Guild { get { return CurGuild; } } // Alias
        #endregion


        public Action<Player> OnDisconnect { get; set; }

        public Player(SocketClient src, global::DataFiles.PhxItemDat itemdat)
            : base(src.SendPacket, itemdat)
        {
            m_socket = src;
            m_socket.onConnectionLost += m_socket_onConnectionLost;
            m_socket.onPacketRecved = ProcessSocket;
            QueueData = new Queue<SendPacket>(25);


            m_inv = new Inventory(this, itemdat);
            onWearEquip = m_inv.onWearEquip;
            onEquip_Remove = m_inv.onUnEquip;

            m_useracc = new User(); // Init UserAcc BEFORE Tent because Tent uses CharID
            m_tent = new Game.Code.Tent(this);

            Flags = new PlayerFlagManager();
            m_settings = new ClientSettings();
            m_friendlist = new Friendlist(new Action<SendPacket>(Send));
            m_Mail = new MailManager(this);

            m_teammembers = new List<Player>();
            m_petlist = new PetList(this);
            m_riceball = new RiceBall(this);
            m_started_Quests = new List<Quest>();

            while (m_socket.m_IncomingPackets.Count > 0)
            {
                IPacket p;
                m_socket.m_IncomingPackets.TryDequeue(out p);
                ProcessSocket(p);

            }


        }
        ~Player()
        {
        }


        public void Dispose()
        {
            m_useracc = null;
        }

        public override void Clear()
        {
            m_Flags = new PlayerFlagManager();
            m_inv.RemoveAll(true);
            QueueData = new Queue<SendPacket>(25);
            UserAcc.Clear();
            base.Clear();
        }

        #region ThreadSafe  Properties

        #region Socket
        public TimeSpan TimeIdle { get { return m_socket.Elapsed(); } }
        public bool isDisconnected() { return m_socket.isDisconnected(); }
        public String SockAddress() { return m_socket.SockAddress(); }
        public String LocalPort() { return m_socket.LocalPort(); }
        #endregion

        #region User Account
        public User UserAcc { get { return m_useracc; } }
        public bool GM { get { return m_useracc.GMlvl > 0; } }
        public bool Busy { get; set; }
        #endregion

        #region Player


        public PlayerFlagManager Flags
        {
            get
            {
                lock (mlock) return m_Flags;
            }
            set
            {
                lock (mlock) m_Flags = value;
            }
        }
        public override uint CharID
        {
            get
            {
                return (Slot == 1) ? UserAcc.Character1ID : UserAcc.Character2ID;
            }
            set
            {
                base.CharID = value;
            }
        }
        //public bool BlockSave { get; set; }
        //public bool inGame { get; set; }
        //public PlayerState State
        //{
        //    get
        //    {
        //        if (m_battle != null)
        //        {
        //            if (CurHP != 0)
        //                return PlayerState.InGame_Battling_Alive;
        //        }

        //        return m_state;
        //    }
        //    set
        //    {
        //        m_state = value;
        //    }
        //}
        public List<Quest> Started_Quests { get { return m_started_Quests; } }
        //public IReadOnlyList<Quest> Completed_Quest { get { return m_started_Quests.Where(c => c.progress == c.total).ToList(); } }
        public ClientSettings Settings
        {
            get
            {
                return m_settings;
            }
        }
        //public List<Character> Friends
        //{
        //    get
        //    {
        //        return m_friends;
        //    }
        //}
        //public List<Mail> MailBox
        //{
        //    get
        //    {
        //        return mailBox;
        //    }
        //}
        public Inventory Inv { get { return m_inv ?? null; } }
        public EquipManager Eqs { get { return ((EquipManager)this) ?? null; } }
        public byte Emote { get { lock (mlock) return emote; } set { lock (mlock) emote = value; } }
        public PetList Pets { get { return m_petlist; } }
        public Game.Code.Tent Tent { get { return m_tent; } }
        public Game.Battle.BattleScene MyBattle { get { return m_battle; } set { m_battle = value; } }
        public RiceBall RiceBall { get { return m_riceball; } }
        public int CurInstance { get; set; }

        public void WearEQ(byte fromLoc)
        {
            if (m_inv != null)
                m_inv.onWearEquip(fromLoc);
        }

        public void unWearEQ(byte fromLoc, byte toLoc)
        {
            // Assuming logic: Unequip item at 'fromLoc' and move to 'toLoc' in inventory?
            // Inventory.onUnEquip takes (Item src, byte loc, bool senddata)
            // Need to get Item from Equipment first? Casting to EquipManager might be needed if Eqs property uses it.
            // For now, attempting to use Eqs if available or standard inventory lookup if equipment is managed there.
            if (Eqs != null)
                Eqs.unWear(fromLoc);
        }

        //public SendType DataOut
        //{
        //    get { return dataout; }
        //    set
        //    {
        //        prevdataout = dataout; dataout = value;
        //        if (prevdataout == SendType.Multi && value == SendType.Normal) Send(MultiPkt);
        //        else if (value == SendType.Multi) MultiPkt = new SendPacket(false, true);

        //    }
        //}
        //public override string CharacterName
        //{
        //    get
        //    {
        //        return (this.GM) ? GMStuff.Name + " " + base.CharacterName : base.CharacterName;
        //    }
        //    set
        //    {
        //        base.CharacterName = value;
        //    }
        //}
        public override IMap CurMap
        {
            get
            {
                return base.CurMap;
            }
            set
            {
                if (base.CurMap != null)
                {
                    if (base.CurMap is GameMap)
                    {
                        prevMap = new WarpData();
                        prevMap.DstMap = (ushort)base.CurMap.MapID;
                        prevMap.DstX_Axis = CurX;
                        prevMap.DstY_Axis = CurY;

                        (base.CurMap as GameMap).onItemDropped_fromMap = null;
                        (base.CurMap as GameMap).onItemPickup_fromMap = null;
                    }
                }
                base.CurMap = value;
                if (base.CurMap is GameMap)
                {
                    (base.CurMap as GameMap).onItemDropped_fromMap = m_inv.onItemDropped_fromMap;
                    (base.CurMap as GameMap).onItemPickup_fromMap = m_inv.onItemPickedUp_fromMap;
                }
            }
        }

        public WarpData PrevMap
        {
            get { lock (mlock) return prevMap; }
            set { lock (mlock) prevMap = value; }
        }

        #endregion

        #region Fighter
        //public BattleSide BattlePosition { get; set; }
        //public eFighterType TypeofFighter { get; set; }
        //public BattleAction myAction { get; set; }
        //public UInt16 ClickID { get { return 0; } set { } }
        //public UInt16 OwnerID { get { return 0; } set { } }
        //public byte GridX { get; set; }
        //public byte GridY { get; set; }
        //public bool ActionDone { get { return (myAction != null || DateTime.Now > rndend); } }
        //public DateTime RdEndTime { set { rndend = value; } }
        //public Int32 MaxHP { get { return (Eqs != null) ? Eqs.FullHP : 0; } }
        //public Int16 MaxSP { get { return (Eqs != null) ? (short)Eqs.FullSP : (short)0; } }
        //public override int CurHP
        //{
        //    get
        //    {
        //        return base.CurHP;
        //    }
        //    set
        //    {
        //        base.CurHP = value;
        //    }
        //}
        //public override int CurSP
        //{
        //    get
        //    {
        //        return base.CurSP;
        //    }
        //    set
        //    {
        //        base.CurSP = value;
        //    }
        //}

        #endregion

        #region Team
        public List<Player> m_teammembers;

        public bool PartyLeader
        {
            get
            {
                if (m_teammembers == null || m_teammembers.Count == 0) return false;
                return (m_teammembers[0] == this);
            }
        }

        public List<Player> TeamMembers
        {
            get
            {
                if (m_teammembers == null) return new List<Player>();
                return m_teammembers.Skip(1).ToList();
            }
        }

        public bool hasParty { get { return (m_teammembers != null && m_teammembers.Count > 0); } }

        public SendPacket _13_6Data
        {
            get
            {
                SendPacket f = new SendPacket();
                f.PackArray(new byte[] { 13, 6 });
                f.Pack32(CharID);
                if (m_teammembers != null)
                {
                    f.Pack8((byte)m_teammembers.Count(c => c.CharID != CharID));
                    foreach (Player y in m_teammembers.Where(c => c.CharID != CharID))
                        f.Pack32(y.CharID);
                }
                else
                {
                    f.Pack8(0);
                }
                return f;
            }
        }
        //}
        #endregion

        #endregion

        #region ThreadSafe  Methods

        #region ClientSock

        /// <summary>
        /// Send Player a packet
        /// </summary>
        /// <param name="src"></param>
        public void Send(SendPacket src)
        {
            if (src.Flags == PacketFlags.Queued || src.Flags == PacketFlags.Queue_Dc)
            {
                src.Flags -= PacketFlags.Queued;
                QueueData.Enqueue(src);
                return;
            }
            Send(src, src.Flags);
        }
        public void Send(byte[] src)
        {

        }
        public void Send(byte[] src, RCLibrary.Core.Networking.PacketFlags pFlags)
        {
            SendPacket p = new SendPacket(src);
            Send(p, pFlags);
        }
        public void Send(SendPacket p, RCLibrary.Core.Networking.PacketFlags pFlags)
        {
            if (pFlags == PacketFlags.Queued || pFlags == PacketFlags.Queue_Dc)
            {
                p.Flags -= PacketFlags.Queued;
                QueueData.Enqueue(p);
                return;
            }
            p.Flags = pFlags;
            m_socket.SendPacket(p);

        }

        public void ProcessSocket(IPacket g)
        {
            try
            {
                RecievePacket p = new RecievePacket(g.Buffer);

                if (m_socket.isDisconnected()) { return; }
                // Confirm packet reception - Use DebugSystem with Error level for visibility
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Recv Packet: AC={p.A}, Sub={p.B}, Len={p.Buffer.Length}");

                p.SetPtr();
                var b = p.Unpack8();
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] About to call GetAction for AC={b}");
                Network.ActionCodes.AC ac = Network.ActionCodes.AC.GetAction(b);
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] GetAction returned: {(ac == null ? "NULL" : ac.GetType().Name)}");

                if (ac == null)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC {b} NOT FOUND in AcList!");
                }
                else
                {
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Processing AC {b}...");
                }

                if (ac != null)
                {
                    var c = this;
                    try
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Calling ProcessPkt for AC {ac.ID}");
                        ac.ProcessPkt(c, p);
                        DebugSystem.Write(DebugItemType.Error, $"[DEBUG] ProcessPkt completed for AC {ac.ID}");
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[ERROR] Exception in ProcessPkt for AC {ac.ID}: {ex}");
                    }
                    //DebugSystem.Write("Player.cs receive packet: " + p.ToString());
                    //immgithub special cheat-ChatActions: action ID 2 = chat
                    if (ac.ID == 2)
                    {
                        string chatMsg = System.Text.Encoding.ASCII.GetString(p.Buffer.Skip(6).ToArray());
                        string[] msgsSent = chatMsg.Split('>');
                        if (msgsSent.Length > 1)
                        {
                            string cmdChar = msgsSent[0];
                            string cmdValue = msgsSent[1];
                            switch (cmdChar)
                            {
                                case "T": //teleport
                                    TeleportPlayer(cmdValue);
                                    break;
                                case "I": //add item to inventory
                                    AddItemToInventory(cmdValue);
                                    break;
                                case "R": //ride vehicle
                                    UnridePet(); //unride any pet first
                                    RideVehicle(cmdValue);
                                    break;
                                case "P": //add pet 
                                    RideVehicle(""); //unride any vehicles first
                                    if (AddPetToPartyList(cmdValue)) PutPetToBattle(cmdValue);
                                    break;
                                case "PR": //ride pet 
                                    RideVehicle(""); //unride any vehicles first
                                    if (AddPetToPartyList(cmdValue)) PutPetToRide(cmdValue);
                                    break;
                            }
                        }
                    }
                }


                base.ProcessSocket(this, p);
                m_inv.ProcessSocket(p);
                if (m_settings != null)
                    m_settings.ProcessSocket(p);
                if (m_battle != null)
                    m_battle.ProcessSocket(p);
                if (m_tent != null)
                    m_tent.Process(this, p);


            }
            catch (Exception f) { }// DebugSystem.Write(new ExceptionData(f)); DebugSystem.Write(f.StackTrace); }//m_socket.Disconnect(); }
        }

        public void TeleportPlayer(string mapID)
        {
            WarpData tmp = new WarpData();
            tmp.DstMap = ushort.Parse(mapID);
            tmp.DstX_Axis = 600;
            tmp.DstY_Axis = 600;
            CurMap.Teleport(TeleportType.CmD, this, (byte)0, tmp);
        }

        public void AddItemToInventory(string itemID) { Inv.AddItem(UInt16.Parse(itemID), (byte)1); }

        public void RideVehicle(string vehicleID)
        {
            SendPacket vp = new SendPacket();
            int cmdByte = vehicleID != "" ? 10 : 11; //11=unride
            vp.PackArray(new byte[] { 15, (byte)cmdByte, 0 });
            vp.Pack32(this.CharID);
            if (vehicleID != "")
            {
                ushort vid = ushort.Parse(vehicleID);
                vp.Pack16(vid);
                ActiveVehicleID = vid;
            }
            else
            {
                ActiveVehicleID = 0; // Unride
            }

            // Broadcast to all players in map so they can see the vehicle
            if (CurMap != null)
            {
                CurMap.Broadcast(vp);
            }
            else
            {
                Send(vp); // Fallback if not in map yet
            }
        }



        public bool AddPetToPartyList(string petID)
        {
            UnridePet();

            // Dismiss previous pet - send only to owner, not broadcast
            SendPacket dp = new SendPacket();
            dp.PackArray(new byte[] { 15, 2 });
            dp.Pack32(this.CharID);
            dp.Pack8(1); //dismiss previous pet at slot1
            Send(dp); // Only to owner, NOT broadcast

            if (petID == "") return false; //no pet
            SendPacket pkt = new SendPacket();
            pkt.PackArray(new byte[] { 15, 1 }); //add pet to party list
            pkt.Pack32(this.CharID);
            pkt.Pack32(uint.Parse(petID));
            pkt.Pack8((byte)2); pkt.Pack32((uint)100); pkt.Pack8((byte)1); pkt.Pack32(100); pkt.Pack8(1); pkt.Pack32(100); pkt.Pack8(1); pkt.Pack32(100); pkt.Pack8(1); pkt.Pack16(0); pkt.Pack16(0); pkt.Pack8(0);

            // Broadcast to all players so they can see the pet in party
            if (CurMap != null)
            {
                CurMap.Broadcast(pkt);
            }
            else
            {
                Send(pkt); // Fallback if not in map yet
            }
            return true;
        }

        public void PutPetToBattle(string petID)
        {
            SendPacket pp = new SendPacket();
            pp.PackArray(new byte[] { 19, 4 }); //put into battle
            pp.Pack32(this.CharID); // Owner CharID - same as PutPetToRide format
            uint pid = uint.Parse(petID);
            pp.Pack32(pid); // Pet ID
            ActivePetID = pid; // Store for broadcasting

            // Broadcast to all players in map so they can see the pet
            if (CurMap != null)
            {
                CurMap.Broadcast(pp);

                // Try to force refresh player appearance to spawn pet
                SendPacket refresh = new SendPacket();
                refresh.PackArray(new byte[] { 5, 8 });
                refresh.Pack32(this.CharID);
                refresh.Pack8(0);
                CurMap.Broadcast(refresh);
            }
            else
            {
                Send(pp); // Fallback if not in map yet
            }
        }

        public void PutPetToRide(string petID)
        {
            SendPacket rp = new SendPacket();
            rp.PackArray(new byte[] { 15, 16 }); //put into ride npc mode
            rp.Pack8(1);
            rp.Pack32(this.CharID);
            uint pid = uint.Parse(petID);
            rp.Pack32(pid);
            ActiveMountID = pid; // Store for broadcasting

            // Broadcast to all players in map so they can see the mount
            if (CurMap != null)
            {
                CurMap.Broadcast(rp);
            }
            else
            {
                Send(rp); // Fallback if not in map yet
            }
        }
        public void UnridePet()
        {
            SendPacket urp = new SendPacket();
            urp.PackArray(new byte[] { 15, 17 }); //unride pet first to dismiss it
            urp.Pack32(this.CharID);
            ActiveMountID = 0; // Clear mount when unriding
            Send(urp);
        }

        public void Disconnect()
        {
            if (!isDisconnected())
                m_socket.Disconnect();
        }

        //public void Send(SendPacket pkt, bool queue = false)//TODO finished for multi packs
        //{
        //    if (!killFlag)
        //    {
        //        SendPacket o = pkt;
        //        if (QueuePkt != null)
        //            QueuePkt.PackArray(pkt.Data.ToArray());
        //        else if (queue)
        //            DatatoSend.Enqueue(pkt);
        //        else
        //        {
        //            switch (DataOut)
        //            {
        //                case SendType.Multi: MultiPkt.PackArray(o.Data.ToArray()); break;
        //                case SendType.Normal:
        //                    {
        //                        DLogger.NetworkLog(UserName, pkt.Data.ToArray());
        //                        killFlag = pkt.DisconnectAfter();
        //                        var data = pkt.Data.ToArray();
        //                        int offset = 0;
        //                        Encode(ref data);
        //                        int nret = 0;
        //                    retry:
        //                        if (nret < data.Length)
        //                        {
        //                            nret = (UInt16)socket.Send(data.Skip(offset).ToArray(), SocketFlags.None);
        //                            offset += nret; goto retry;
        //                        }
        //                    } break;
        //            }
        //        }
        //    }
        //}


        #endregion

        #region Game.Battle
        public void OnBattle_Start(Game.Battle.BattleScene battle)
        {
        }

        void Battle_OnNewRound(List<Game.Battle.Fighter> fighters_on_my_side, List<Game.Battle.Fighter> fighters_on_other_side)
        {
            PacketBuilder tmp = new PacketBuilder();
            tmp.Begin(null);

            foreach (var f in fighters_on_my_side)
            {
                tmp.Add(Tools.FromFormat("bbbbbwd", 51, 1, f.GridX, f.GridY, 25, f.CurHP, 0));
                tmp.Add(Tools.FromFormat("bbbbbwd", 51, 1, f.GridX, f.GridY, 26, f.CurSP, 0));
            }
            foreach (var f in fighters_on_other_side)
                tmp.Add(Tools.FromFormat("bbbbbwd", 51, 1, f.GridX, f.GridY, 25, f.CurHP, 0));

            tmp.Add(Tools.FromFormat("bb", 52, 1));
            Send(tmp.End());
        }

        #endregion

        #region Game.Mail

        #endregion

        #region Player
        //public void onPlayerLogin(uint id)
        //{
        //    if (Friends.Exists(c => c.ID == id))
        //    {
        //        SendPacket p = new SendPacket();
        //        p.PackArray(new byte[] { 14, 9 });
        //        p.Pack32(id);
        //        p.Pack8(0);
        //        Send(p);
        //    }
        //    for (int a = 0; a < MailBox.Count; a++)
        //    {
        //        if (MailBox[a].targetid == id && MailBox[a].type == "Send" && MailBox[a].isSent)
        //        {
        //            MailBox[a].isSent = true;
        //            SendMailTo(MailBox[a].targetid, MailBox[a].message);
        //        }
        //    }

        //}
        //public bool ContinueInteraction()
        //{
        //    if (DatatoSend.Count == 1)
        //    {
        //        var f = DatatoSend.Dequeue();
        //        Send(f);
        //        return (obj_interacting != null);
        //    }
        //    else if (DatatoSend.Count > 1)
        //    {
        //        Send(DatatoSend.Dequeue()); return true;
        //    }
        //    return false;
        //}
        //public void Send_3_Me()
        //{
        //    SendPacket p = new SendPacket();
        //    p.Pack8(3);
        //    p.Pack32(ID);
        //    p.Pack8((byte)Eqs.Body);
        //    p.Pack16(LoginMap);
        //    p.Pack16(X);
        //    p.Pack16(Y);
        //    p.Pack8(0); p.Pack8(Eqs.Head); p.Pack8(0);
        //    p.Pack16(HairColor);
        //    p.Pack16(SkinColor);
        //    p.Pack16(ClothingColor);
        //    p.Pack16(EyeColor);
        //    p.Pack8(Eqs.WornCount);//clothesAmmt); // ammt of clothes
        //    p.PackArray(Eqs.Worn_Equips);
        //    p.Pack32(0);
        //    p.PackString(CharacterName);
        //    p.PackString(Nickname);
        //    p.Pack32(0);
        //    Send(p);
        //}
        //public SendPacket _3Data()
        //{
        //    SendPacket p = new SendPacket();
        //    p.Pack8(3);
        //    p.Pack32(ID);
        //    p.Pack8((byte)Eqs.Body);
        //    p.Pack8((byte)Eqs.Element);
        //    p.Pack8((byte)Eqs.Level);
        //    p.Pack16(CurrentMap.MapID);
        //    p.Pack16(X);
        //    p.Pack16(Y);
        //    p.Pack8(0); p.Pack8(Eqs.Head); p.Pack8(0);
        //    p.Pack16(HairColor);
        //    p.Pack16(SkinColor);
        //    p.Pack16(ClothingColor);
        //    p.Pack16(EyeColor);
        //    p.Pack8(Eqs.WornCount);//clothesAmmt); // ammt of clothes
        //    p.PackArray(Eqs.Worn_Equips);
        //    p.Pack32(0); p.Pack8(0);
        //    p.PackBoolean(Eqs.Reborn);
        //    p.Pack8((byte)Eqs.Job);
        //    p.PackString(CharacterName);
        //    p.PackString(Nickname);
        //    p.Pack8(255);
        //    return p;
        //}
        //public void Send_5_3() //logging in player info
        //{
        //    SendPacket p = new SendPacket();
        //    p.PackArray(new byte[] { 5, 3 });
        //    p.Pack8((byte)Eqs.Element);
        //    p.Pack32((uint)CurHP);
        //    p.Pack16((ushort)CurSP);
        //    p.Pack16(Eqs.Str); //base str
        //    p.Pack16(Eqs.Con); //base con
        //    p.Pack16(Eqs.Int); //base int
        //    p.Pack16(Eqs.Wis); //base wis
        //    p.Pack16(Eqs.Agi); //base agi
        //    p.Pack8((byte)Eqs.Level); //lvl
        //    p.Pack64((ulong)Eqs.TotalExp); //exp ???
        //    p.Pack32((uint)Eqs.FullHP); //max hp
        //    p.Pack16((ushort)Eqs.FullSP); //max sp

        //    //-------------- 7 DWords
        //    p.Pack32(0);
        //    p.Pack32(0);
        //    p.Pack32(0);
        //    p.Pack32(0);
        //    p.Pack32(0);
        //    p.Pack32(0);
        //    p.Pack32(0);

        //    //--------------- Skills
        //    p.Pack16(0/*(ushort)MySkills.Count*/);
        //    //if (MySkills.Count > 0)
        //    //    p.PackArray(MySkills.GetSkillData());
        //    //p.Pack16(1); //ammt of skills
        //    //p.Pack16(188); p.Pack16(1); p.Pack16(0); p.Pack8(0); //skill data
        //    //--------------- table with rebirth and job
        //    p.Pack16(0); p.Pack16(0);
        //    p.Pack8(BitConverter.GetBytes(Eqs.Reborn)[0]); p.Pack8((byte)Eqs.Job); p.Pack8((byte)Eqs.Potential);

        //    Send(p);
        //}

        public bool Load_CharacterInfo(Character data)
        {
            if (data == null) return false;
            CharID = data.CharID;
            CharName = data.CharName;
            Slot = data.Slot;
            Head = data.Head;
            Body = data.Body;
            TotalExp = 95000478;//data.TotalEXP
            CharName = data.CharName;
            NickName = data.NickName;
            LoginMap = data.LoginMap;
            CurSP = data.CurSP;
            CurHP = data.CurHP;
            CurX = data.CurX;
            CurY = data.CurY;
            HairColor = data.HairColor;
            SkinColor = data.SkinColor;
            ClothingColor = data.ClothingColor;
            EyeColor = data.EyeColor;
            SetGold((int)data.Gold);
            Element = data.Element;
            Job = data.Job;
            Potential = data.Potential;
            foreach (var stat in data.GetStatArray())
                SetBaseStat(stat[0], stat[1]);

            for (byte a = 1; a < 7; a++)
                this[a].CopyFrom(data[a]);

            //remove
            FillHP();
            FillSP();

            return true;
        }
        public bool ContinueInteraction()
        {
            if (QueueData.Count == 1)
            {
                m_socket.SendPacket(QueueData.Dequeue());
                return false;// (object_interactingwith != null);
            }
            else if (QueueData.Count > 1)
            {
                m_socket.SendPacket(QueueData.Dequeue()); return true;
            }
            return false;
        }

        #endregion

        //    #region Friends
        //    public void SendFriendList()
        //    {
        //        SendPacket y = new SendPacket();
        //        y.PackArray(new byte[] { 14, 5 });
        //        y.PackArray(new byte[]{100, 0, 0, 0, 6, 71, 77, 164, 164, 164, 223, 200, 0,      
        //0, 0, 0, 0, 28, 175, 125, 26, 28, 175, 125, 26, 0, 0});
        //        foreach (Character h in Friends)
        //        {
        //            y.Pack32(h.ID);
        //            y.PackString(h.CharacterName);
        //            y.Pack8((byte)h.Level);
        //            y.Pack8(BitConverter.GetBytes(h.Reborn)[0]);
        //            y.Pack8((byte)h.Job);
        //            y.Pack8((byte)h.Element);
        //            y.Pack8((byte)h.Body);
        //            y.Pack8(h.Head);
        //            y.Pack16(h.HairColor);
        //            y.Pack16(h.SkinColor);
        //            y.Pack16(h.ClothingColor);
        //            y.Pack16(h.EyeColor);
        //            y.PackString(h.Nickname);
        //            y.Pack8(0);
        //        }
        //        Send(y);
        //    }
        //    public void AddFriend(Player t)
        //    {
        //        if (Friends.Count == 50) return;
        //        if (!m_friends.Exists(c => c.ID == t.ID))
        //            m_friends.Add(t);
        //        SendPacket s = new SendPacket();
        //        s.PackArray(new byte[] { 14, 9 });
        //        s.Pack32(t.ID);
        //        s.Pack8(0);
        //        Send(s);
        //        s = new SendPacket();
        //        s.PackArray(new byte[] { 14, 7 });
        //        s.Pack32(t.ID);
        //        s.PackString("Test");
        //        Send(s);
        //    }
        //    public void DelFriend(uint t)
        //    {
        //        if (m_friends.Exists(c => c.ID == t))
        //            m_friends.Remove(m_friends.Single(c => c.ID == t));
        //        SendPacket s = new SendPacket();
        //        s.PackArray(new byte[] { 14, 4 });
        //        s.Pack32(t);
        //        Send(s);
        //    }
        //    public bool LoadFriends(string str)
        //    {
        //        foreach (string y in str.Split('&'))
        //        {
        //            if (y.Length > 0 && y != "none")
        //            {
        //                string[] f = y.Split(' ');
        //                m_friends.Add(myhost.CharDataBase.GetCharacterData(uint.Parse(f[0])));
        //            }
        //        }
        //        return true;
        //    }
        //    public string GetFriends_Flag
        //    {
        //        get
        //        {
        //            string query = "";
        //            for (int a = 0; a < m_friends.Count; a++)
        //            {
        //                query += m_friends[a].ID.ToString() + " " + m_friends[a].CharacterName;
        //                if (a < m_friends.Count)
        //                    query += "&";
        //            }
        //            if (query == "")
        //                query += "none";
        //            return query;
        //        }
        //    }
        //    #endregion

        //    #region Mail
        //    public void SendMailTo(Player t, string msg)
        //    {
        //        Mail a = new Mail();
        //        a.message = msg;
        //        a.id = ID;
        //        a.targetid = t.ID;
        //        a.type = "Send";
        //        a.isSent = true;
        //        MailBox.Add(a);
        //        t.RecvMailfrom(this, a.message);
        //    }
        //    public void SendMailTo(uint t, string msg)
        //    {
        //        Mail a = new Mail();
        //        a.message = msg;
        //        a.id = ID;
        //        a.targetid = t;
        //        a.type = "Send";
        //        a.isSent = false;
        //        MailBox.Add(a);
        //    }
        //    public override void RecvMailfrom(Player t, string msg, double Date = 0)
        //    {
        //        base.RecvMailfrom(t, msg, Date);
        //        SendPacket p = new SendPacket();
        //        p.PackArray(new byte[] { 14, 1 });
        //        p.Pack32(t.ID);
        //        p.PackArray(((Date == 0) ? BitConverter.GetBytes(DateTime.Now.ToOADate()) : BitConverter.GetBytes(Date)));
        //        for (int n = 0; n < msg.Length; n++)
        //            p.Pack8((byte)msg[n]);
        //        Send(p);
        //    }
        //    public string GetMailboxFlags()
        //    {
        //        string str = "none";
        //        if (MailBox.Count > 0)
        //        {
        //            for (int a = 0; a < MailBox.Count; a++)
        //            {
        //                str += MailBox[a].id + " " +
        //                    MailBox[a].targetid + " " +
        //                    MailBox[a].when + " " +
        //                    MailBox[a].message + " " +
        //                    MailBox[a].type + " " +
        //                    BitConverter.GetBytes(MailBox[a].isSent)[0].ToString() + " ";

        //                if (a < MailBox.Count)
        //                    str += "&";
        //            }
        //        }
        //        return str;
        //    }
        //    #endregion

        //    #region equips

        //    public bool WearEQ(byte index)
        //    {

        //        bool ret = false;
        //        InvItemCell i = new InvItemCell();

        //        i.CopyFrom(Inv[index]);
        //        if (i.ItemID > 0)
        //        {
        //            Inv[index].Clear();
        //            if (Eqs.Level >= i.Data.Level)
        //            {
        //                DataOut = SendType.Multi;
        //                var retrem = Eqs.SetEQ((byte)i.Data.EquipPos, i);
        //                if (retrem != null && retrem.ItemID > 0)
        //                    Inv.AddItem(retrem, index, false);
        //                Eqs.Send8_1();//send ac8
        //                SendPacket tmp = new SendPacket();
        //                tmp.PackArray(new byte[] { 5, 2 });
        //                tmp.Pack32(ID);
        //                tmp.Pack16(i.ItemID);
        //                CurrentMap.Broadcast(tmp, ID);
        //                tmp = new SendPacket();
        //                tmp.PackArray(new byte[] { 23, 17 });
        //                tmp.Pack8(index);
        //                tmp.Pack8(index);
        //                Send(tmp);
        //                ret = true;
        //                DataOut = SendType.Normal;

        //            }
        //            else
        //            {
        //            }
        //        }
        //        return ret;

        //    }

        //    public bool unWearEQ(byte src, byte dst)
        //    {
        //        bool ret = false;
        //        InvItemCell i = new InvItemCell();

        //        if (Eqs[src].ItemID > 0)
        //        {
        //            i.CopyFrom(Eqs[src]);//copy from clothes
        //            if (i != null && Inv.AddItem(i, dst, false) > 0)
        //            {
        //                Eqs.RemoveEQ(src);
        //                DataOut = SendType.Multi;
        //                SendPacket p = new SendPacket();
        //                p.PackArray(new byte[] { 23, 16 });
        //                p.Pack8(src);
        //                p.Pack8(dst);
        //                Send(p);
        //                Eqs.Send8_1();
        //                p = new SendPacket();
        //                p.PackArray(new byte[] { 5, 1 });
        //                p.Pack32(ID);
        //                p.Pack16(i.ItemID);
        //                CurrentMap.Broadcast(p, ID);
        //                ret = true;
        //                DataOut = SendType.Normal;
        //            }
        //            else if (i != null)
        //                Eqs.SetEQ(src, i);
        //        }
        //        return ret;

        //    }

        //    #endregion

        #region Team
        public void CreateParty()
        {
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] CreateParty called for {CharName}. Current team: {m_teammembers?.Count ?? 0}");
            if (m_teammembers == null) m_teammembers = new List<Player>();
            if (!m_teammembers.Contains(this))
            {
                m_teammembers.Add(this);
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Added {CharName} to their own team. New size: {m_teammembers.Count}");
            }
            BroadcastPartyUpdate();
        }

        public void JoinParty(Player leader)
        {
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] JoinParty: {CharName} joining {leader.CharName}'s party");
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Leader team before: {leader.m_teammembers?.Count ?? -1}");

            // Ensure leader has a team and is in it
            if (leader.m_teammembers == null) leader.m_teammembers = new List<Player>();
            if (!leader.m_teammembers.Contains(leader))
            {
                leader.m_teammembers.Add(leader);
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Added leader {leader.CharName} to their own team. New size: {leader.m_teammembers.Count}");
            }

            // Allow max 4 players
            if (leader.m_teammembers.Count >= 4) return;

            // Add self to leader's list
            if (!leader.m_teammembers.Contains(this))
            {
                leader.m_teammembers.Add(this);
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Added {CharName} to {leader.CharName}'s team. New size: {leader.m_teammembers.Count}");
                this.m_teammembers = leader.m_teammembers; // Share the list reference

                // Broadcast update
                leader.BroadcastPartyUpdate();
            }
        }

        public void LeaveParty()
        {
            if (m_teammembers == null || m_teammembers.Count == 0) return;

            // If leader leaves, disband or pass leadership?
            // Simple logic: remove self, update others

            if (m_teammembers.Contains(this))
            {

                List<Player> oldParty = m_teammembers;
                oldParty.Remove(this);

                // Reset self
                m_teammembers = new List<Player>();

                // Notify others
                BroadcastToParty(oldParty, _13_6Data); // Update old party list

                // Notify self (empty list) is implicit by not sending anything or sending empty
                SendPacket p = new SendPacket();
                p.PackArray(new byte[] { 13, 4 });
                p.Pack32(CharID);
                Send(p);

                if (oldParty.Count > 0)
                {
                    // Update old party members
                    SendPacket update = new SendPacket();
                    update.PackArray(new byte[] { 13, 6 });
                    update.Pack32(oldParty[0].CharID); // Leader ID
                    update.Pack8((byte)(oldParty.Count - 1));
                    foreach (var m in oldParty.Skip(1)) update.Pack32(m.CharID);

                    foreach (var m in oldParty) m.Send(update);
                }
            }
        }

        public void KickPartyMember(uint targetID)
        {
            if (!PartyLeader) return;

            Player target = m_teammembers.FirstOrDefault(p => p.CharID == targetID);
            if (target != null)
            {
                target.LeaveParty();
            }
        }

        public void TransferLeadership(Player newLeader)
        {
            if (!PartyLeader) return; // Only leader can transfer
            if (m_teammembers == null || !m_teammembers.Contains(newLeader)) return; // New leader must be in party

            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] TransferLeadership from {CharName} to {newLeader.CharName}");

            // Reorder the list: new leader first, then others
            List<Player> reordered = new List<Player>();
            reordered.Add(newLeader);
            foreach (var member in m_teammembers)
            {
                if (member.CharID != newLeader.CharID)
                    reordered.Add(member);
            }

            // Update all members to point to new list
            foreach (var member in reordered)
            {
                member.m_teammembers = reordered;
            }

            // Broadcast the update
            newLeader.BroadcastPartyUpdate();
        }

        public void BroadcastPartyUpdate()
        {
            if (m_teammembers == null) return;

            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] BroadcastPartyUpdate from {CharName}. Team size: {m_teammembers.Count}");
            SendPacket p = _13_6Data;
            foreach (var m in m_teammembers)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Sending party packet to {m.CharName}...");
                m.Send(p);
            }
        }

        public void BroadcastToParty(List<Player> party, SendPacket p)
        {
            if (party == null) return;
            foreach (var m in party)
            {
                m.Send(p);
            }
        }

        #endregion
        #endregion

        #region Properties


        //public Inventory Inv { get { return m_inv; } }



        //public MailManager Mail { get { return m_Mail; } }
        //public Friendlist MyFriends { get { return m_friendlist; } }
        //public RiceBall Disguise { get { return m_riceball; } }
        //public PetList Pets { get { return m_petlist; } }
        //public Tent Tent { get { return m_tent; } }

        #endregion

        #region Internal Events
        void m_socket_onConnectionLost()
        {
            if (net != null && net.IsAlive) net.Abort();
            if (Disconnected != null) Disconnected(this);
        }
        public void onTick_Tick()
        {
            OnPropertyChanged("DisplayName");
        }
        #endregion

        #region Gui Update
        public string DisplayName { get { return SockAddress() + " ID: " + UserAcc.UserID + " User: " + UserAcc.UserName + " Char: " + CharName; } }

        #endregion

        #region Inotify Property
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion







        public override string ToString()
        {
            return $"{CharName} (ID: {CharID})";
        }

        public TimeSpan IdleTimer()
        {
            // Implement idle timer logic, possibly returning time since last packet
            return DateTime.Now - LastPacketTime;
        }

        public DateTime LastPacketTime { get; set; } = DateTime.Now;

        public void ProcessSocket()
        {
            // Delegate to socket client processing if applicable, or leaving empty if handled by callbacks
            // m_socket.Process(); // If such method exists
        }
    }
}
