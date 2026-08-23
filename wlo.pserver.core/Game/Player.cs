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

        public Queue<SendPacket> QueueData;
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
        Game.PlayerRelated.Friendlist m_friendlist;
        RiceBall m_riceball;
        PetList m_petlist;
        Game.Code.Tent m_tent;

        // Active mount/pet/vehicle tracking for broadcasting to other players
        public uint ActiveVehicleID { get; set; } = 0;
        public uint ActiveMountID { get; set; } = 0; // Riding pet
        public uint ActivePetID { get; set; } = 0; // Battle pet
        public WarpData CarnieReturnMap { get; set; } = null; // Return destination when exiting Carnie (Map 11094)
        public int StepsSinceLastBattle { get; set; } = 0;
        public int NextBattleSteps { get; set; } = 25;
        public DateTime LastTeleportTime { get; set; } = DateTime.MinValue;
        public ushort LastSpawnX { get; set; } = 0;
        public ushort LastSpawnY { get; set; } = 0;
        public ushort LastOriginMapID { get; set; } = 0;
        public int BreillatTalkCount { get; set; } = 0;

        // FIX: Added properties for ActionCodes compatibility
        public Game.Battle.BattleScene BattleScene { get { return m_battle; } }
        public uint UserID { get { return m_useracc != null ? m_useracc.UserID : 0; } }
        public Game.PlayerRelated.Guild CurGuild { get; set; }
        public ushort GuildID => (ushort)(CurGuild?.GuildID ?? 0);
        public Game.PlayerRelated.Guild Guild => CurGuild;
        public User UserAccount => m_useracc;
        public void SendSystemMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(msg);
            Send(s);
        }

        public void SendHeadBanner(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(msg);
            Send(s);
        }
        public List<Game.SkillRelated.PlayerSkill> PlayerSkills { get; set; } = new List<Game.SkillRelated.PlayerSkill>();
        public Dictionary<uint, Game.QuestRelated.PlayerQuest> Quests { get; set; } = new Dictionary<uint, Game.QuestRelated.PlayerQuest>();
        public Dictionary<byte, PlayerPetData> PlayerPets { get; set; } = new Dictionary<byte, PlayerPetData>();
        public Dictionary<byte, PlayerPetData> HotelPets { get; set; } = new Dictionary<byte, PlayerPetData>();
        public bool MotdSent { get; set; } = false;
        #endregion

        public class PlayerPetData
        {
            public byte Slot { get; set; } = 1;
            public uint PetID { get; set; }
            public string PetName { get; set; } = "";
            public byte Level { get; set; } = 1;
            public uint Exp { get; set; } = 0;
            public int HP { get; set; } = 250;
            public int MaxHP { get; set; } = 250;
            public int SP { get; set; } = 100;
            public int MaxSP { get; set; } = 100;
            public ushort Str { get; set; } = 10;
            public ushort Con { get; set; } = 10;
            public ushort Int { get; set; } = 10;
            public ushort Wis { get; set; } = 10;
            public ushort Agi { get; set; } = 10;
            public ushort SkillPoints { get; set; } = 0;
            public ushort Potential { get; set; } = 0;
            public byte Amity { get; set; } = 60;
            public bool IsBattle { get; set; } = true;
            public bool IsRide { get; set; } = false;
            public bool Reborn { get; set; } = false;
            public byte Job { get; set; } = 0;
            public ushort Eq_Head { get; set; } = 0;
            public ushort Eq_Body { get; set; } = 0;
            public ushort Eq_Weapon { get; set; } = 0;
            public ushort Eq_Wrist { get; set; } = 0;
            public ushort Eq_Shoes { get; set; } = 0;
            public ushort Eq_Special { get; set; } = 0;
        }

        public bool HasRecruitedCompanion(string npcName, ushort templateId)
        {
            string cleanNpcName = (npcName ?? "").Trim();

            // 1. Check PlayerPets dictionary
            if (PlayerPets != null && PlayerPets.Count > 0)
            {
                foreach (var pet in PlayerPets.Values)
                {
                    if (pet == null) continue;
                    if (pet.Amity < 20) continue; // Runaway / abandoned companion

                    if (templateId > 0 && pet.PetID == templateId) return true;

                    // Companion template ID mappings:
                    // Robinson: 12032 (NPC TID) <-> 12178 (Pet TID)
                    if ((templateId == 12032 || templateId == 12178) && (pet.PetID == 12032 || pet.PetID == 12178)) return true;
                    // S.Monkey: 17162 (NPC TID) <-> 10727 (Pet TID)
                    if ((templateId == 17162 || templateId == 10727) && (pet.PetID == 17162 || pet.PetID == 10727)) return true;
                    // Roca: 14161 <-> 14001
                    if ((templateId == 14161 || templateId == 14001) && (pet.PetID == 14161 || pet.PetID == 14001)) return true;
                    // Niss: 14162 <-> 14002
                    if ((templateId == 14162 || templateId == 14002) && (pet.PetID == 14162 || pet.PetID == 14002)) return true;
                    // Clive: 14163 <-> 14003
                    if ((templateId == 14163 || templateId == 14003) && (pet.PetID == 14163 || pet.PetID == 14003)) return true;
                    // Fred: 14164 <-> 14004
                    if ((templateId == 14164 || templateId == 14004) && (pet.PetID == 14164 || pet.PetID == 14004)) return true;
                    // Elin: 14165 <-> 14005
                    if ((templateId == 14165 || templateId == 14005) && (pet.PetID == 14165 || pet.PetID == 14005)) return true;
                    // Sam: 14166 <-> 14006
                    if ((templateId == 14166 || templateId == 14006) && (pet.PetID == 14166 || pet.PetID == 14006)) return true;
                    // Shizune: 14167 <-> 14007
                    if ((templateId == 14167 || templateId == 14007) && (pet.PetID == 14167 || pet.PetID == 14007)) return true;
                    // Suzan: 14168 <-> 14008
                    if ((templateId == 14168 || templateId == 14008) && (pet.PetID == 14168 || pet.PetID == 14008)) return true;

                    // Match by companion Name
                    if (!string.IsNullOrEmpty(cleanNpcName) && !string.IsNullOrEmpty(pet.PetName))
                    {
                        if (cleanNpcName.Equals(pet.PetName.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
                        if (cleanNpcName.StartsWith(pet.PetName.Trim(), StringComparison.OrdinalIgnoreCase) || pet.PetName.Trim().StartsWith(cleanNpcName, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }

            // 2. Check ActivePetID
            if (ActivePetID > 0)
            {
                if (ActivePetID == templateId) return true;
                if ((templateId == 12032 || templateId == 12178) && (ActivePetID == 12032 || ActivePetID == 12178)) return true;
                if ((templateId == 17162 || templateId == 10727) && (ActivePetID == 17162 || ActivePetID == 10727)) return true;
            }

            return false;
        }

        public Action<Player> OnDisconnect { get; set; }

        public Player(SocketClient src, global::DataFiles.PhxItemDat itemdat)
            : base(src.SendPacket, itemdat)
        {
            m_socket = src;
            m_socket.onConnectionLost += m_socket_onConnectionLost;
            m_socket.onPacketRecved = ProcessSocket;
            QueueData = new Queue<SendPacket>(25);


            m_inv = new Inventory(this, itemdat);
            m_storage = new Inventory(this, itemdat);
            onWearEquip = m_inv.onWearEquip;
            onEquip_Remove = m_inv.onUnEquip;

            m_useracc = new User(); // Init UserAcc BEFORE Tent because Tent uses CharID
            m_tent = new Game.Code.Tent(this);

            Flags = new PlayerFlagManager();
            m_settings = new ClientSettings();
            m_friendlist = new Game.PlayerRelated.Friendlist(this, new Action<SendPacket>(Send));
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
        private Inventory m_storage;
        public Inventory Storage { get { return m_storage ?? null; } }

        public void OpenPropsKeeper()
        {
            // Official sequence from propskeeper.pcapng (Frames 4-8):
            // 1. AC 29 Sub 6 (Storage window session init)
            Send(Tools.FromFormat("bb", 29, 6));

            // 2. AC 20 Sub 9 (Dialogue ack)
            Send(Tools.FromFormat("bb", 20, 9));

            // 3. AC 35 Sub 12 (Catalog/UI ID 0x00019898: F4 44 07 00 23 0C 98 98 01 00 00)
            SendPacket uiPkt = new SendPacket();
            uiPkt.PackArray(new byte[] { 35, 12, 0x98, 0x98, 0x01, 0x00, 0x00 });
            Send(uiPkt);

            // 4. AC 20 Sub 8 (Close dialogue prompt) & AC 5:4 (Player animation reset)
            Send(Tools.FromFormat("bb", 20, 8));
            Send(Tools.FromFormat("bb", 5, 4));

            // 5. Sync all bag items so "items held" pane on the right is populated
            if (m_inv != null)
            {
                Send(new SendPacket(m_inv.GetAC23_5()));
            }

            // 2. Sync all stored items for this character (storID = 2) so "store items" on left is populated
            if (m_storage != null)
            {
                Send(new SendPacket(m_storage.GetAC30_5(30, 5)));
                Send(new SendPacket(m_storage.GetAC30_5(30, 1)));

                for (byte slot = 1; slot <= 50; slot++)
                {
                    var item = m_storage[slot];
                    if (item != null && item.ItemID > 0)
                    {
                        SendPacket sp30_2 = new SendPacket();
                        sp30_2.Pack8(30);
                        sp30_2.Pack8(2);
                        sp30_2.Pack8(slot);
                        sp30_2.Pack16(item.ItemID);
                        sp30_2.Pack8(item.Ammt);
                        sp30_2.Pack8(item.Damage);
                        sp30_2.PackArray(new byte[24]);
                        Send(sp30_2);

                        SendPacket sp30_1 = new SendPacket();
                        sp30_1.Pack8(30);
                        sp30_1.Pack8(1);
                        sp30_1.Pack8(slot);
                        sp30_1.Pack16(item.ItemID);
                        sp30_1.Pack8(item.Ammt);
                        sp30_1.Pack8(item.Damage);
                        sp30_1.PackArray(new byte[24]);
                        Send(sp30_1);
                    }
                }
            }
        }

        public void OpenMoneyBank()
        {
            Send(Tools.FromFormat("bb", 29, 6));
            Send(Tools.FromFormat("bb", 20, 8));
            Send(Tools.FromFormat("bb", 5, 4));
        }

        public void OpenPetHotel()
        {
            Send(Tools.FromFormat("bb", 31, 1));
            Send(Tools.FromFormat("bb", 20, 9));
            Send(Tools.FromFormat("bb", 20, 8));
            Send(Tools.FromFormat("bb", 5, 4));

            // Sync all hotel pets for this character
            if (HotelPets != null && HotelPets.Count > 0)
            {
                foreach (var kvp in HotelPets)
                {
                    var pet = kvp.Value;
                    if (pet != null && pet.PetID > 0)
                    {
                        SendPacket hPkt = new SendPacket();
                        hPkt.Pack8(31);
                        hPkt.Pack8(3);
                        hPkt.Pack8(pet.Slot);
                        hPkt.Pack16((ushort)pet.PetID);
                        hPkt.Pack8(pet.Level);
                        hPkt.Pack32((uint)pet.HP);
                        hPkt.Pack32((uint)pet.MaxHP);
                        hPkt.Pack16((ushort)pet.SP);
                        hPkt.Pack16((ushort)pet.MaxSP);
                        hPkt.Pack8(pet.Amity);
                        hPkt.PackString(pet.PetName ?? "");
                        Send(hPkt);
                    }
                }
            }
        }
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

        public WarpData ReturnSpawnMap
        {
            get { lock (mlock) return returnSpawnMap; }
            set { lock (mlock) returnSpawnMap = value; }
        }

        public WarpData RecordMap
        {
            get { lock (mlock) return recordMap; }
            set { lock (mlock) recordMap = value; }
        }

        public int MallQueryCount { get; set; } = 0;

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

                uint leaderId = (m_teammembers != null && m_teammembers.Count > 0) ? m_teammembers[0].CharID : CharID;
                var otherMembers = (m_teammembers != null && m_teammembers.Count > 1)
                    ? m_teammembers.Where(m => m != null && m.CharID != leaderId).ToList()
                    : new List<Player>();

                f.Pack32(leaderId);
                f.Pack8((byte)otherMembers.Count);
                foreach (Player m in otherMembers)
                {
                    f.Pack32(m.CharID);
                }
                return f;
            }
        }
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
            if (src != null && src.Length > 0)
            {
                Send(new SendPacket(src));
            }
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

                p.SetPtr();
                var b = p.Unpack8();
                Network.ActionCodes.AC ac = Network.ActionCodes.AC.GetAction(b);

                string hexData = BitConverter.ToString(p.Buffer).Replace("-", " ");
                string who = string.IsNullOrEmpty(CharName) ? "Client" : CharName;

                if (ac == null)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[RECV PKT] [{who}] AC={p.A}, Sub={p.B} (Handler: NONE) Len={p.Buffer.Length} Hex: {hexData}");
                }
                else
                {
                    DebugSystem.Write(DebugItemType.Error, $"[RECV PKT] [{who}] AC={p.A}, Sub={p.B} (Handler: {ac.GetType().Name}) Len={p.Buffer.Length} Hex: {hexData}");
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
                    if (ac.ID == 2)
                    {
                        string chatMsg = System.Text.Encoding.ASCII.GetString(p.Buffer.Skip(6).ToArray()).Trim('\0', ' ');
                        
                        if (chatMsg.StartsWith("/im", StringComparison.OrdinalIgnoreCase) ||
                            chatMsg.StartsWith(":im", StringComparison.OrdinalIgnoreCase) ||
                            chatMsg.StartsWith("/mall", StringComparison.OrdinalIgnoreCase) ||
                            chatMsg.StartsWith(":mall", StringComparison.OrdinalIgnoreCase) ||
                            chatMsg.StartsWith("/shop", StringComparison.OrdinalIgnoreCase) ||
                            chatMsg.StartsWith(":shop", StringComparison.OrdinalIgnoreCase))
                        {
                            int pts = Game.PlayerRelated.ItemMallManager.GetUserPoints(c);
                            c.SendSystemMessage($"========== 🛍️ ITEM MALL (Balance: {pts} IM Pts) ==========");
                            var catalog = Game.PlayerRelated.ItemMallManager.GetCatalog();
                            for (int i = 0; i < catalog.Count; i++)
                            {
                                var it = catalog[i];
                                c.SendSystemMessage($"[{i + 1}] {it.ItemName} (x{it.Count}) - {it.PointCost} Pts -> Type: /buy {i + 1}");
                            }
                            c.SendSystemMessage("💡 Type /buy <number> to purchase directly into your inventory!");
                        }
                        else if (chatMsg.StartsWith("/buy", StringComparison.OrdinalIgnoreCase) ||
                                 chatMsg.StartsWith(":buy", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = chatMsg.Split(' ');
                            if (parts.Length > 1)
                            {
                                var catalog = Game.PlayerRelated.ItemMallManager.GetCatalog();
                                Game.PlayerRelated.MallItemEntry targetItem = null;

                                if (int.TryParse(parts[1], out int idx) && idx >= 1 && idx <= catalog.Count)
                                {
                                    targetItem = catalog[idx - 1];
                                }
                                else if (ushort.TryParse(parts[1], out ushort itemId))
                                {
                                    targetItem = catalog.FirstOrDefault(it => it.ItemID == itemId);
                                }

                                if (targetItem != null)
                                {
                                    if (Game.PlayerRelated.ItemMallManager.PurchaseItem(c, targetItem.ItemID, targetItem.Count))
                                    {
                                        c.SendSystemMessage($"🎉 Purchased {targetItem.ItemName} (x{targetItem.Count}) for {targetItem.PointCost} IM Points! Remaining: {Game.PlayerRelated.ItemMallManager.GetUserPoints(c)} Pts.");
                                    }
                                    else
                                    {
                                        c.SendSystemMessage($"❌ Purchase failed! Cost: {targetItem.PointCost} Pts (Your Balance: {Game.PlayerRelated.ItemMallManager.GetUserPoints(c)} Pts).");
                                    }
                                }
                                else
                                {
                                    c.SendSystemMessage("❌ Item not found! Type /im to see the available list.");
                                }
                            }
                            else
                            {
                                c.SendSystemMessage("💡 Usage: /buy <number> (e.g. /buy 1)");
                            }
                        }
                        else if (chatMsg.StartsWith("/points", StringComparison.OrdinalIgnoreCase) ||
                                 chatMsg.StartsWith(":points", StringComparison.OrdinalIgnoreCase) ||
                                 chatMsg.StartsWith("/myim", StringComparison.OrdinalIgnoreCase) ||
                                 chatMsg.StartsWith(":myim", StringComparison.OrdinalIgnoreCase))
                        {
                            int pts = Game.PlayerRelated.ItemMallManager.GetUserPoints(c);
                            c.SendSystemMessage($"💎 Your Current Balance: {pts} IM Points.");
                        }
                        else if (chatMsg.StartsWith("/acceptmarry", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.PlayerRelated.MarriageManager.AcceptProposal(c);
                        }
                        else if (chatMsg.StartsWith("/declinemarry", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.PlayerRelated.MarriageManager.DeclineProposal(c);
                        }
                        else if (chatMsg.StartsWith("/divorce", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.PlayerRelated.MarriageManager.Divorce(c);
                        }
                        else if (chatMsg.StartsWith("/warptospouse", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.PlayerRelated.MarriageManager.TeleportToSpouse(c);
                        }
                        else if (chatMsg.StartsWith("/reborn", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!Game.PlayerRelated.GmManager.IsGm(c))
                            {
                                c.SendSystemMessage("You do not have GM privileges to use the /reborn command.");
                            }
                            else
                            {
                                var parts = chatMsg.Split(' ');
                                if (parts.Length > 1 && Enum.TryParse<Game.PlayerRelated.RebornJob>(parts[1], true, out var job))
                                {
                                    Game.PlayerRelated.RebornManager.PerformReborn(c, job);
                                }
                            }
                        }
                        else if (chatMsg.StartsWith("/compound", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = chatMsg.Split(' ');
                            if (parts.Length > 2 && byte.TryParse(parts[1], out byte s1) && byte.TryParse(parts[2], out byte s2))
                            {
                                Game.Crafting.AlchemyManager.CompoundItems(c, s1, s2);
                            }
                        }
                        else if (chatMsg.StartsWith("/fish", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.Crafting.GatheringManager.StartGathering(c, Game.Crafting.GatheringType.Fishing);
                        }
                        else if (chatMsg.StartsWith("/mine", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.Crafting.GatheringManager.StartGathering(c, Game.Crafting.GatheringType.Mining);
                        }
                        else if (chatMsg.StartsWith("/chop", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.Crafting.GatheringManager.StartGathering(c, Game.Crafting.GatheringType.Woodcutting);
                        }
                        else if (chatMsg.StartsWith("/stopgather", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.Crafting.GatheringManager.StopGathering(c);
                        }
                        else if (chatMsg.StartsWith("/inbox", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.PlayerRelated.MailSystem.OpenInbox(c);
                        }
                        else if (chatMsg.StartsWith("/duel", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = chatMsg.Split(' ');
                            if (parts.Length > 1 && uint.TryParse(parts[1], out uint tid))
                            {
                                Player target = null;
                                if (c.CurMap is GameMap curMap)
                                    target = curMap.PlayersList.FirstOrDefault(pl => pl.CharID == tid);
                                if (target != null)
                                    Game.Battle.PvPManager.RequestDuel(c, target);
                            }
                        }
                        else if (chatMsg.StartsWith("/acceptduel", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.Battle.PvPManager.AcceptDuel(c);
                        }
                        else if (chatMsg.StartsWith("/declineduel", StringComparison.OrdinalIgnoreCase))
                        {
                            Game.Battle.PvPManager.DeclineDuel(c);
                        }
                        else if (chatMsg.StartsWith("/feedpet", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = chatMsg.Split(' ');
                            ushort foodId = (parts.Length > 1 && ushort.TryParse(parts[1], out ushort fid)) ? fid : (ushort)30025;
                            Game.PetRelated.PetAmityManager.FeedPet(c, foodId);
                        }
                        else if (chatMsg.StartsWith("/repair", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = chatMsg.Split(' ');
                            if (parts.Length > 1 && byte.TryParse(parts[1], out byte slot))
                            {
                                Game.Crafting.EquipmentRepairManager.RepairItem(c, slot);
                            }
                        }
                        else if (chatMsg.StartsWith("/forge", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = chatMsg.Split(' ');
                            if (parts.Length > 2 && byte.TryParse(parts[1], out byte eqSlot) && byte.TryParse(parts[2], out byte gemSlot))
                            {
                                Game.Crafting.ForgingManager.ForgeGem(c, eqSlot, gemSlot);
                            }
                        }
                        else if (chatMsg.StartsWith("/manufacture", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = chatMsg.Split(' ');
                            if (parts.Length > 5 && ushort.TryParse(parts[2], out ushort in1) && byte.TryParse(parts[3], out byte c1) && ushort.TryParse(parts[4], out ushort in2) && byte.TryParse(parts[5], out byte c2))
                            {
                                Game.Crafting.TentManufactureManager.Manufacture(c, parts[1], in1, c1, in2, c2);
                            }
                        }
                        else if (chatMsg.StartsWith("/palace", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!Game.PlayerRelated.GmManager.IsGm(c))
                            {
                                c.SendSystemMessage("You do not have GM privileges to use the /palace command.");
                            }
                            else
                            {
                                var parts = chatMsg.Split(' ');
                                if (parts.Length > 1 && byte.TryParse(parts[1], out byte stage))
                                {
                                    Game.Battle.PalaceTrialManager.EnterPalaceTrial(c, stage);
                                }
                            }
                        }

                        string[] msgsSent = chatMsg.Split('>');
                        if (msgsSent.Length > 1)
                        {
                            if (!Game.PlayerRelated.GmManager.IsGm(c))
                            {
                                c.SendSystemMessage("You do not have GM privileges to execute cheat commands.");
                            }
                            else
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

        public void AddItemToInventory(string itemID)
        {
            if (ushort.TryParse(itemID, out ushort id))
            {
                DebugSystem.Write($"[Player.AddItemToInventory] Adding Item #{id} to {CharName}");
                Inv?.AddItem(id, (byte)1);
                SendSystemMessage($"[Cheat] Added Item {id} to inventory!");
            }
        }

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



        public SendPacket CreatePetMapPacket(uint petId = 0, string petName = "")
        {
            if (petId == 0) petId = ActivePetID;
            if (petId == 0) return null;
            if (string.IsNullOrEmpty(petName))
            {
                var pet = PlayerPets?.Values?.FirstOrDefault(x => x.PetID == petId);
                petName = pet?.PetName;
                if (string.IsNullOrWhiteSpace(petName)) petName = QuestRelated.QuestManager.GetNpcName(petId);
                if (string.IsNullOrWhiteSpace(petName)) petName = $"Pet #{petId}";
            }

            SendPacket pkt = new SendPacket();
            pkt.Pack8(15);
            pkt.Pack8(4);
            pkt.Pack32(this.CharID);
            pkt.Pack32(petId);
            pkt.Pack8(0);
            pkt.Pack8(1);
            pkt.PackString(petName);
            pkt.Pack16(0);
            return pkt;
        }

        public void BroadcastPetAppearance(uint petId = 0, string petName = "")
        {
            if (petId == 0) petId = ActivePetID;
            if (petId == 0) return;

            var mapPkt = CreatePetMapPacket(petId, petName);
            if (mapPkt != null)
            {
                Send(mapPkt);
                CurMap?.Broadcast(mapPkt, "Ex", this.CharID);
            }

            var pet = PlayerPets?.Values?.FirstOrDefault(x => x.PetID == petId);
            SendPacket petPkt;
            if (pet != null)
            {
                petPkt = QuestRelated.QuestManager.CreatePetPacket(this, pet.PetID, pet.Slot, pet.HP, pet.MaxHP, pet.SP, pet.MaxSP, pet.Amity, pet.Level);
            }
            else
            {
                petPkt = QuestRelated.QuestManager.CreatePetPacket(this, petId, 1);
            }
            Send(petPkt);
            CurMap?.Broadcast(petPkt, "Ex", this.CharID);

            SendPacket followPkt = new SendPacket();
            followPkt.Pack8(19);
            followPkt.Pack8(4);
            followPkt.Pack32(this.CharID);
            followPkt.Pack32(petId);
            Send(followPkt);
            CurMap?.Broadcast(followPkt, "Ex", this.CharID);

            SendPacket petFollow = new SendPacket();
            petFollow.PackArray(new byte[] { 13, 5 });
            petFollow.Pack32(this.CharID);
            petFollow.Pack32(petId);
            Send(petFollow);
            CurMap?.Broadcast(petFollow, "Ex", this.CharID);

            SendPacket petRefresh = new SendPacket();
            petRefresh.PackArray(new byte[] { 5, 8 });
            petRefresh.Pack32(this.CharID);
            petRefresh.Pack8(0);
            Send(petRefresh);
            CurMap?.Broadcast(petRefresh, "Ex", this.CharID);
        }

        public bool AddPetToPartyList(string petID)
        {
            UnridePet();

            // Dismiss previous pet - send only to owner, not broadcast
            SendPacket dp = new SendPacket();
            dp.PackArray(new byte[] { 15, 2 });
            dp.Pack32(this.CharID);
            dp.Pack8(1); // dismiss previous pet at slot1
            Send(dp);

            if (string.IsNullOrEmpty(petID) || !uint.TryParse(petID, out uint pid) || pid == 0) return false;

            ActivePetID = pid;
            BroadcastPetAppearance(pid);
            return true;
        }

        public void PutPetToBattle(string petID)
        {
            if (string.IsNullOrEmpty(petID) || !uint.TryParse(petID, out uint pid) || pid == 0) return;

            UnridePet();
            ActivePetID = pid;

            if (PlayerPets != null)
            {
                foreach (var kvp in PlayerPets)
                {
                    if (kvp.Value.PetID == pid)
                    {
                        kvp.Value.IsBattle = true;
                    }
                    else
                    {
                        kvp.Value.IsBattle = false;
                    }
                }
            }

            // AC 19:1 Set battle companion state to owner
            Send(Tools.FromFormat("bbd", 19, 1, pid));

            // Full broadcast of AC 15:4, AC 15:1, AC 19:4, AC 13:5, and AC 5:8
            BroadcastPetAppearance(pid);
        }

        public void PutPetToRide(string petID)
        {
            if (string.IsNullOrEmpty(petID) || !uint.TryParse(petID, out uint pid) || pid == 0) return;

            ActivePetID = 0; // Un-battle
            ActiveMountID = pid;

            SendPacket rp = new SendPacket();
            rp.PackArray(new byte[] { 15, 16 }); // put into ride npc mode
            rp.Pack8(1);
            rp.Pack32(this.CharID);
            rp.Pack32(pid);
            for (int i = 0; i < 26; i++) rp.Pack8(0);

            Send(rp);
            CurMap?.Broadcast(rp, "Ex", this.CharID);

            SendPacket refresh = new SendPacket();
            refresh.PackArray(new byte[] { 5, 8 });
            refresh.Pack32(this.CharID);
            refresh.Pack8(0);
            Send(refresh);
            CurMap?.Broadcast(refresh, "Ex", this.CharID);
        }

        public void UnridePet()
        {
            if (ActiveMountID == 0) return;
            SendPacket urp = new SendPacket();
            urp.PackArray(new byte[] { 15, 17 }); // unride pet
            urp.Pack32(this.CharID);
            ActiveMountID = 0;

            Send(urp);
            CurMap?.Broadcast(urp, "Ex", this.CharID);

            SendPacket refresh = new SendPacket();
            refresh.PackArray(new byte[] { 5, 8 });
            refresh.Pack32(this.CharID);
            refresh.Pack8(0);
            Send(refresh);
            CurMap?.Broadcast(refresh, "Ex", this.CharID);
        }

        public void Disconnect()
        {
            if (!isDisconnected())
                m_socket.Disconnect();
            OnConnectionLost();
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
        public override void Send_5_3() //logging in player info
        {
            PacketBuilder p = new PacketBuilder();
            p.Begin();
            p.Add((byte)5);                              // Offset 0: ActionCode (byte)
            p.Add((byte)3);                              // Offset 1: SubCode (byte)
            p.Add((byte)Element);                        // Offset 2: Element (byte)
            p.Add((uint)CurHP);                          // Offset 3..6: CurHP (uint, 4B)
            p.Add((ushort)CurSP);                        // Offset 7..8: CurSP (ushort, 2B)
            p.Add((ushort)Con);                          // Offset 9..10: CON (ushort, 2B)
            p.Add((ushort)Int);                          // Offset 11..12: INT (ushort, 2B)
            p.Add((ushort)Str);                          // Offset 13..14: STR (ushort, 2B)
            p.Add((ushort)Agi);                          // Offset 15..16: AGI (ushort, 2B)
            p.Add((ushort)Wis);                          // Offset 17..18: WIS (ushort, 2B)
            p.Add((byte)(Level > 0 ? Level : 1));        // Offset 19: Level (byte, 1B)
            p.Add((uint)TotalExp);                       // Offset 20..23: TotalExp (uint, 4B)
            p.Add((ushort)FullHP);                       // Offset 24..25: FullHP (ushort, 2B)
            p.Add((ushort)FullSP);                       // Offset 26..27: FullSP (ushort, 2B)
            
            // Client internal static state offsets (0x1c .. 0x3d = 34 bytes)
            p.Add((uint)417);                            // Offset 28..31: DWord (4B)
            p.Add((ushort)0);                            // Offset 32..33: Word (2B)
            p.Add((uint)0);                              // Offset 34..37: DWord (4B)
            p.Add((uint)240);                            // Offset 38..41: DWord (4B)
            p.Add((uint)0);                              // Offset 42..45: DWord (4B)
            p.Add((uint)0);                              // Offset 46..49: DWord (4B)
            p.Add((uint)0);                              // Offset 50..53: DWord (4B)
            p.Add((uint)0);                              // Offset 54..57: DWord (4B)
            p.Add((uint)0);                              // Offset 58..61: DWord (4B)

            // Offset 62..63 (0x3E..0x3F): SkillCount (ushort)
            if (PlayerSkills != null && PlayerSkills.Count > 0)
            {
                p.Add((ushort)PlayerSkills.Count);
                foreach (var sk in PlayerSkills)
                {
                    var skillData = Game.SkillRelated.SkillManager.GetSkill((ushort)sk.SkillID);
                    ushort tableOrder = (skillData != null) ? skillData.SkillTableOrder : (ushort)0;
                    p.Add((ushort)tableOrder);           // 2 bytes: TableOrder
                    p.Add((byte)sk.Grade);               // 1 byte: Grade
                    p.Add((uint)sk.Exp);                 // 4 bytes: Exp
                }
            }
            else
            {
                p.Add((ushort)0);
            }

            // Post-skill trailer offsets
            p.Add((ushort)SkillPoints);                  // 2 bytes: Available Stat Points (StatusUp)
            p.Add((ushort)Potential);                    // 2 bytes: Potential
            p.Add((byte)0);                              // 1 byte: Padding
            p.Add((byte)(Reborn ? 1 : 0));               // 1 byte: Reborn flag
            p.Add((byte)Potential);                      // 1 byte: Potential byte
            p.Add((byte)Job);                            // 1 byte: Reborn Job

            SendPacket pkt = new SendPacket(p.End());
            Send(pkt);
        }

        public bool Load_CharacterInfo(Character data)
        {
            if (data == null) return false;
            CharID = data.CharID;
            CharName = data.CharName;
            Slot = data.Slot;
            Head = data.Head;
            Body = data.Body;
            TotalExp = data.TotalExp;
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
        public Action OnInteractionComplete;
        public Action<byte> OnDialogueChoice;
        public Action OnMinigameWon;
        public Action OnMinigameLost;
        public ushort TransformedModelID { get; set; } = 0;
        public bool PendingBeachCutscene { get; set; }
        public bool BeachCutsceneActive { get; set; }
        public Queue<Action> StepQueue { get; set; } = new Queue<Action>();
        public int LastDialogueAdvanceTick { get; set; } = 0;

        public bool ContinueInteraction()
        {
            int now = Environment.TickCount;
            if (now - LastDialogueAdvanceTick < 100 && LastDialogueAdvanceTick != 0 && ((StepQueue != null && StepQueue.Count > 0) || (QueueData != null && QueueData.Count > 0)))
            {
                return true;
            }
            LastDialogueAdvanceTick = now;

            if (StepQueue != null && StepQueue.Count > 0)
            {
                var stepAction = StepQueue.Dequeue();
                stepAction?.Invoke();
                return true;
            }
            if (QueueData != null && QueueData.Count > 0)
            {
                var nextPkt = QueueData.Dequeue();
                Send(nextPkt);
                DebugSystem.Write($"[Player.ContinueInteraction] Dispatched next queued step to {CharName} (Remaining in queue: {QueueData.Count})");
                return true;
            }
            if (OnDialogueChoice != null)
            {
                // Active choice prompt is awaiting player selection
                return true;
            }
            if (OnInteractionComplete != null)
            {
                var action = OnInteractionComplete;
                OnInteractionComplete = null;
                action.Invoke();
                if (StepQueue != null && StepQueue.Count > 0)
                {
                    var stepAction = StepQueue.Dequeue();
                    stepAction?.Invoke();
                    return true;
                }
                if (QueueData != null && QueueData.Count > 0)
                {
                    var nextPkt = QueueData.Dequeue();
                    Send(nextPkt);
                    DebugSystem.Write($"[Player.ContinueInteraction] Dispatched next queued step after action to {CharName} (Remaining in queue: {QueueData.Count})");
                    return true;
                }
                return false;
            }
            return false;
        }

        /// <summary>
        /// Instantly commits character data (inventory, skills, quests, map, coords, stats, gold, pets, equips) to database.
        /// </summary>
        public bool SaveCharacterData()
        {
            if (CharID == 0) return false;
            try
            {
                var db = DataBase.CharacterDataBase.GlobalInstance;
                if (db != null)
                {
                    return db.WritePlayer(CharID, this);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[Player.SaveCharacterData] Error saving char {CharName} ({CharID}): {ex.Message}");
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

                // 1. Broadcast authentic AC 13:5 (Join & Follow in formation) to map
                SendPacket followPkt = new SendPacket();
                followPkt.PackArray(new byte[] { 13, 5 });
                followPkt.Pack32(leader.CharID);
                followPkt.Pack32(this.CharID);
                leader.CurMap?.Broadcast(followPkt);

                // 2. Synchronize member stats and update party HUD
                leader.Eqs?.Send8_1();
                this.Eqs?.Send8_1();
                leader.BroadcastPartyUpdate();
            }
        }

        public void LeaveParty()
        {
            if (m_teammembers == null || m_teammembers.Count == 0) return;

            if (m_teammembers.Contains(this))
            {
                List<Player> oldParty = m_teammembers.ToList();
                oldParty.Remove(this);

                // Reset self
                m_teammembers = new List<Player>();
                this.Send(_13_6Data); // Reset party HUD on client

                // 1. Broadcast AC 13:4 (Leave Party & detach follower) to map
                SendPacket leavePkt = new SendPacket();
                leavePkt.PackArray(new byte[] { 13, 4 });
                leavePkt.Pack32(CharID);
                CurMap?.Broadcast(leavePkt);

                if (oldParty.Count > 0)
                {
                    // Update remaining members list reference and broadcast
                    foreach (var m in oldParty)
                    {
                        m.m_teammembers = oldParty;
                    }
                    oldParty[0].BroadcastPartyUpdate();
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
            if (m_teammembers == null || m_teammembers.Count == 0) return;
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] BroadcastPartyUpdate from {CharName}. Team size: {m_teammembers.Count}");

            SendPacket p13_6 = _13_6Data;
            foreach (var m in m_teammembers)
            {
                if (m == null) continue;
                m.Send(p13_6);

                // Send stats of all other team members to m
                foreach (var other in m_teammembers)
                {
                    if (other != null && other != m)
                    {
                        SendTeammateStats(m, other);
                    }
                }
            }
        }

        public static void SendTeammateStat(Player recipient, uint teammateCharId, ushort statId, long val)
        {
            if (recipient == null) return;
            SendPacket p = new SendPacket();
            p.Pack8(8);
            p.Pack8(3);
            p.Pack32(teammateCharId);
            p.Pack16(statId);
            p.Pack64((ulong)val);
            recipient.Send(p);
        }

        public static void SendTeammateStats(Player recipient, Player teammate)
        {
            if (recipient == null || teammate == null || teammate.Eqs == null) return;
            uint tId = teammate.CharID;
            SendTeammateStat(recipient, tId, 0x011D, teammate.Eqs.Level); // Level (285)
            SendTeammateStat(recipient, tId, 0x0119, teammate.Eqs.FullHP); // MaxHP (281)
            SendTeammateStat(recipient, tId, 0x011A, teammate.Eqs.FullSP); // MaxSP (282)
            SendTeammateStat(recipient, tId, 0x0123, teammate.Eqs.CurHP);  // CurHP (291)
            SendTeammateStat(recipient, tId, 0x0124, teammate.Eqs.CurSP);  // CurSP (292)
            SendTeammateStat(recipient, tId, 0x01CF, teammate.Eqs.EquippedMaxHP); // Equip MaxHP (463)
            SendTeammateStat(recipient, tId, 0x01D0, teammate.Eqs.EquippedMaxSP); // Equip MaxSP (464)
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



        public Game.PlayerRelated.Friendlist MyFriends => m_friendlist;
        public string GetFriends_Flag => m_friendlist?.GetFriends_Flag ?? "none";
        public void LoadFriends(string str) => m_friendlist?.LoadFriends(str);

        #endregion

        #region Internal Events
        public void OnConnectionLost()
        {
            try
            {
                DebugSystem.Write($"[Player.OnConnectionLost] Processing disconnect for {CharName} (ID: {CharID})");

                // 0. Clean up active battle state if disconnected during combat
                Game.Battle.PvEBattleManager.OnPlayerDisconnect(this);

                // 1. Leave party if in party
                LeaveParty();

                // 2. Remove from current map and notify peers to despawn player & pet
                if (CurMap != null)
                {
                    // Despawn active companion/pet if any
                    if (ActivePetID > 0)
                    {
                        SendPacket petLeave = new SendPacket();
                        petLeave.PackArray(new byte[] { 13, 4 });
                        petLeave.Pack32(ActivePetID);
                        CurMap.Broadcast(petLeave, "Ex", CharID);
                    }

                    // Despawn player character from map peers using AC 12 (warp/despawn)
                    SendPacket despawnPkt = new SendPacket();
                    despawnPkt.Pack8(12);
                    despawnPkt.Pack32(CharID);
                    despawnPkt.Pack16(0);
                    despawnPkt.Pack16(0);
                    despawnPkt.Pack16(0);
                    despawnPkt.Pack16(0);
                    despawnPkt.Pack8(0);
                    CurMap.Broadcast(despawnPkt, "Ex", CharID);

                    // Remove from map player list
                    CurMap.RemovePlayer(this);
                }

                // 3. Fire Disconnected event (executes OnCharacterLeave, saves DB data, notifies friends list)
                Disconnected?.Invoke(this);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[Player.OnConnectionLost] Exception during disconnect for {CharName}: {ex.Message}");
            }
        }

        void m_socket_onConnectionLost()
        {
            OnConnectionLost();
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
