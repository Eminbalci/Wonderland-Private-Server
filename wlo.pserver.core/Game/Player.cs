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


    public class Player : Game.Character, IDisposable
    {

        #region Events
        public event PlayerSocketInfo Disconnected;
        #endregion

        #region Definitions

        readonly object mlock = new object();

        SocketClient m_socket;
        PlayerFlagManager m_Flags;

        public Queue<SendPacket> QueueData;

        WarpData prevMap;
        WarpData returnSpawnMap;
        WarpData recordMap;

        User m_useracc;
        Inventory m_inv;
        List<Quest> m_started_Quests;
        ClientSettings m_settings;
        Game.Battle.BattleScene m_battle;
        MailManager m_Mail;
        Game.PlayerRelated.Friendlist m_friendlist;
        RiceBall m_riceball;
        byte emote;
        Game.Code.Tent m_tent;

        // Active mount/pet/vehicle tracking for broadcasting to other players
        public uint ActiveVehicleID { get; set; } = 0;
        public byte MountedVehicleSlot { get; set; } = 0; // Inventory slot of current active vehicle
        public ushort VehicleFuel { get; set; } = 0;
        public ushort VehicleMaxFuel { get; set; } = 0;
        public uint ActiveMountID { get; set; } = 0; // Riding pet
        public uint ActivePetID { get; set; } = 0; // Battle pet
        public WarpData CarnieReturnMap { get; set; } = null; // Return destination when exiting Carnie (Map 11094)
        public WarpData TentReturnMap { get; set; } = null; // Return destination when exiting an actual tent
        public DateTime? MutedUntil { get; set; } = null;
        public bool IsMuted => MutedUntil.HasValue && MutedUntil.Value > DateTime.UtcNow;
        public bool IsInvisible { get; set; } = false;
        public int StepsSinceLastBattle { get; set; } = 0;
        public int NextBattleSteps { get; set; } = 25;
        public bool ProximityEncounterArmed { get; set; } = true;
        public DateTime LastTeleportTime { get; set; } = DateTime.MinValue;
        public DateTime LastMapEnterTime { get; set; } = DateTime.UtcNow;
        public DateTime LastBattleEndTime { get; set; } = DateTime.MinValue;
        public double BattleCooldownSeconds { get; set; } = 3.0;
        public const double MapEncounterGraceSeconds = 4.0;
        private static readonly Random _cooldownRng = new Random();

        public bool IsInBattleCooldown()
        {
            if (LastBattleEndTime == DateTime.MinValue) return false;
            double elapsed = (DateTime.UtcNow - LastBattleEndTime).TotalSeconds;
            return elapsed >= 0 && elapsed < BattleCooldownSeconds;
        }

        public bool IsInMapEncounterGracePeriod()
        {
            double elapsed = (DateTime.UtcNow - LastMapEnterTime).TotalSeconds;
            return elapsed >= 0 && elapsed < MapEncounterGraceSeconds;
        }

        public void SetBattleCooldown()
        {
            LastBattleEndTime = DateTime.UtcNow;
            lock (_cooldownRng)
            {
                // Random grace period between 2.0 and 4.0 seconds
                BattleCooldownSeconds = 2.0 + (_cooldownRng.NextDouble() * 2.0);
            }
            StepsSinceLastBattle = 0;
            ProximityEncounterArmed = false;
        }
        public ushort LastSpawnX { get; set; } = 0;
        public ushort LastSpawnY { get; set; } = 0;
        public ushort LastOriginMapID { get; set; } = 0;
        public int BreillatTalkCount { get; set; } = 0;
        public HashSet<ushort> HiddenNpcClickIDs { get; } = new HashSet<ushort>();
        // Native mechanism state is per player and per map visit, not a loot flag.
        public Dictionary<ushort, int> NativePropStates { get; } = new Dictionary<ushort, int>();
        public Dictionary<ushort, bool> NativeActorVisibility { get; } = new Dictionary<ushort, bool>();
        internal Dictionary<uint, byte> SentQuestMinimapMarkers { get; } = new Dictionary<uint, byte>();
        internal HashSet<ushort> SentNotebookMarks { get; } = new HashSet<ushort>();
        public HashSet<uint> DiscoveredMonsters { get; } = new HashSet<uint>();

        // FIX: Added properties for ActionCodes compatibility
        public Game.Battle.BattleScene BattleScene { get { return m_battle; } }
        public uint UserID { get { return m_useracc != null ? m_useracc.UserID : 0; } }
        public Game.PlayerRelated.Guild CurGuild { get; set; }
        public ushort GuildID => (ushort)(CurGuild?.GuildID ?? 0);
        public Game.PlayerRelated.Guild Guild => CurGuild;
        public ushort MapID => (ushort)(CurMap?.MapID ?? 0);
        public int HP { get => Eqs?.CurHP ?? 0; set { if (Eqs != null) Eqs.CurHP = (ushort)value; } }
        public int MaxHP => Eqs?.FullHP ?? 0;
        public int SP { get => Eqs?.CurSP ?? 0; set { if (Eqs != null) Eqs.CurSP = (ushort)value; } }
        public int MaxSP => Eqs?.FullSP ?? 0;
        public User UserAccount => m_useracc;
        public ushort X { get => CurX; set => CurX = value; }
        public ushort Y { get => CurY; set => CurY = value; }
        public bool AllowPK { get => Settings?.PKABLE ?? true; set { if (Settings != null) Settings.PKABLE = value; } }
        public bool TradeLock { get => !(Settings?.TRADABLE ?? true); set { if (Settings != null) Settings.TRADABLE = !value; } }
        public bool RejectTeam { get => !(Settings?.JOINABLE ?? true); set { if (Settings != null) Settings.JOINABLE = !value; } }
        public byte WalkMode { get; set; } = 0;
        public ushort Title { get; set; } = 0;
        public uint BankGold { get; set; } = 0;
        public byte RebornJob { get; set; } = 0;
        public bool Fishing { get; set; } = false;
        public void SendSystemMessage(string msg)
        {
            // Legacy GM-chat notifications are disabled. Keep this entry point so
            // existing gameplay calls retain their control flow; dialogs use SendHeadBanner.
        }

        public void SendHeadBanner(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            // Native AC2:16 displays raw text in the same box as equip notices.
            SendPacket s = new SendPacket();
            s.Pack8(2);
            s.Pack8(16);
            s.Pack32(0);
            s.PackStringN(msg);
            Send(s);
        }
        public List<Game.SkillRelated.PlayerSkill> PlayerSkills { get; set; } = new List<Game.SkillRelated.PlayerSkill>();
        public bool HasSkill(ushort skillId) => PlayerSkills != null && PlayerSkills.Any(s => s.SkillID == skillId);
        public Dictionary<uint, Game.QuestRelated.PlayerQuest> Quests { get; set; } = new Dictionary<uint, Game.QuestRelated.PlayerQuest>();
        public Dictionary<byte, PlayerPetData> PlayerPets { get; set; } = new Dictionary<byte, PlayerPetData>();
        public Dictionary<byte, PlayerPetData> HotelPets { get; set; } = new Dictionary<byte, PlayerPetData>();
        // Companions temporarily absent for story events; never exposed in Pet Hotel.
        public Dictionary<byte, PlayerPetData> QuestPets { get; set; } = new Dictionary<byte, PlayerPetData>();
        public bool PetRosterSynchronized { get; set; } = false;

        // Client AC 15:1 allocates its first free internal slot, independently
        // of the database slot. Keep this session-only identity for AC 8:2 etc.
        public PlayerPetData GetClientPet(byte slot)
        {
            return slot == 0 ? null : PlayerPets?.Values.FirstOrDefault(p => p != null && p.ClientSlot == slot);
        }

        public bool RegisterClientPet(PlayerPetData pet)
        {
            if (pet == null || pet.ClientSlot != 0 || PlayerPets == null) return false;
            if (PlayerPets.Values.Any(p => p != null && p.ClientSlot != 0 && IsSamePetOrCompanion(p.PetID, pet.PetID)))
                return false; // Native client rejects duplicate template IDs; preserve the stored copy.
            for (byte slot = 1; slot <= 4; slot++)
            {
                if (GetClientPet(slot) != null) continue;
                pet.ClientSlot = slot;
                return true;
            }
            return false;
        }
        public bool MotdSent { get; set; } = false;
        #endregion

        public class PlayerPetData
        {
            public byte ClientSlot { get; set; } // Not persisted; assigned when the roster is sent.
            private static readonly Random GrowthRandom = new Random();
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
            public bool IsBattle { get; set; } = false;
            public bool IsRide { get; set; } = false;
            public bool Reborn { get; set; } = false;
            public byte Job { get; set; } = 0;
            public ushort Eq_Head { get; set; } = 0;
            public ushort Eq_Body { get; set; } = 0;
            public ushort Eq_Weapon { get; set; } = 0;
            public ushort Eq_Wrist { get; set; } = 0;
            public ushort Eq_Shoes { get; set; } = 0;
            public ushort Eq_Special { get; set; } = 0;


            // Six (damage, forge) pairs, persisted with the pet in every container.
            public byte[] EquipmentMetadata { get; } = new byte[12];

            public ushort GetEquipmentId(byte slot)
            {
                switch (slot)
                {
                    case 1: return Eq_Head;
                    case 2: return Eq_Body;
                    case 3: return Eq_Weapon;
                    case 4: return Eq_Wrist;
                    case 5: return Eq_Shoes;
                    case 6: return Eq_Special;
                    default: return 0;
                }
            }

            public void SetEquipment(byte slot, Item item)
            {
                ushort id = item?.ItemID ?? 0;
                switch (slot)
                {
                    case 1: Eq_Head = id; break;
                    case 2: Eq_Body = id; break;
                    case 3: Eq_Weapon = id; break;
                    case 4: Eq_Wrist = id; break;
                    case 5: Eq_Shoes = id; break;
                    case 6: Eq_Special = id; break;
                    default: return;
                }
                EquipmentMetadata[(slot - 1) * 2] = item?.Damage ?? 0;
                EquipmentMetadata[(slot - 1) * 2 + 1] = item?.Forge ?? 0;
            }

            public void LoadEquipmentMetadata(string saved)
            {
                Array.Clear(EquipmentMetadata, 0, EquipmentMetadata.Length);
                if (string.IsNullOrEmpty(saved)) return;
                try
                {
                    var bytes = Convert.FromBase64String(saved);
                    if (bytes.Length == EquipmentMetadata.Length)
                        Array.Copy(bytes, EquipmentMetadata, bytes.Length);
                }
                catch (FormatException) { }
            }

            public List<SkillRelated.PlayerSkill> Skills { get; } = new List<SkillRelated.PlayerSkill>();

            public void EnsureSkills()
            {
                foreach (ushort id in QuestRelated.QuestManager.GetDefaultPetSkills(PetID))
                    if (!Skills.Any(s => s.SkillID == id)) Skills.Add(new SkillRelated.PlayerSkill(id));
            }

            // Store progress on the pet row so hotel/quest transfers keep its identity.
            public string SerializeSkills()
            {
                EnsureSkills();
                return string.Join(";", Skills.Select(s => s.SkillID + ":" + s.Grade + ":" + s.Exp));
            }

            public void LoadSkills(string data)
            {
                Skills.Clear();
                EnsureSkills(); // Existing saves start with the native default skills.
                foreach (string entry in (data ?? "").Split(';'))
                {
                    var fields = entry.Split(':');
                    if (fields.Length != 3 || !uint.TryParse(fields[0], out uint id) ||
                        !byte.TryParse(fields[1], out byte grade) || !uint.TryParse(fields[2], out uint exp)) continue;
                    var skill = Skills.FirstOrDefault(s => s.SkillID == id);
                    if (skill == null) continue;
                    skill.Grade = (byte)Math.Max(1, Math.Min(10, (int)grade));
                    skill.Exp = skill.Grade >= 10 ? 0 : Math.Min(exp, (uint)(skill.Grade * 100 - 1));
                }
            }

            public void PackSkills(SendPacket packet)
            {
                EnsureSkills();
                var native = DataFiles.SceneDataManager.GetNpcBaseStats(GetCompanionBroadcastId(PetID))?.Skills;
                for (int i = 0; i < 3; i++)
                {
                    var skill = native != null && i < native.Length ? Skills.FirstOrDefault(s => s.SkillID == native[i]) : null;
                    packet.Pack8(skill?.Grade ?? 0);
                    packet.Pack32(skill?.Exp ?? 0);
                }
            }

            public static uint GetRequiredExpForNextLevel(byte level)
            {
                int currentLevel = Math.Max(1, Math.Min(199, (int)level));
                return (uint)Math.Max(1, (int)Math.Round(Math.Pow(currentLevel + 1, 3.1) + 5));
            }

            public static uint GetClientTotalExp(byte level, uint currentLevelExp)
            {
                // Level-zero costs 6 EXP once. Repeating that base per attained
                // level adds phantom EXP every time a pet levels up.
                int currentLevel = Math.Max(1, Math.Min(199, (int)level));
                ulong totalExp = 6UL + currentLevelExp;
                for (int completedLevel = 1; completedLevel < currentLevel; completedLevel++)
                {
                    totalExp += GetRequiredExpForNextLevel((byte)completedLevel);
                }
                return (uint)Math.Min(uint.MaxValue, totalExp);
            }

            public uint ClientTotalExp => GetClientTotalExp(Level, Exp);

            public void InitializeBaseStats()
            {
                var stats = DataFiles.SceneDataManager.GetNpcBaseStats(GetCompanionBroadcastId(PetID));
                if (stats == null) return; // Preserve existing defaults for unknown templates.
                Str = stats.Str;
                Con = stats.Con;
                Int = stats.Int;
                Wis = stats.Wis;
                Agi = stats.Agi;
            }

            private void GrowLevelStat()
            {
                // One automatically allocated point per pet level. The template's
                // strongest three base stats define the species' growth tendency.
                // These weights are server policy, not a recovered official RNG.
                var stats = DataFiles.SceneDataManager.GetNpcBaseStats(GetCompanionBroadcastId(PetID));
                int[] weights = stats == null
                    ? new int[] { Str, Con, Int, Wis, Agi }
                    : new int[] { stats.Str, stats.Con, stats.Int, stats.Wis, stats.Agi };
                ushort[] values = { Str, Con, Int, Wis, Agi };
                var candidates = Enumerable.Range(0, 5)
                    .Where(i => values[i] < ushort.MaxValue)
                    .OrderByDescending(i => weights[i]).Take(3).ToArray();
                if (candidates.Length == 0) return;
                int totalWeight = candidates.Sum(i => Math.Max(1, weights[i]));
                int roll;
                lock (GrowthRandom) roll = GrowthRandom.Next(totalWeight);
                foreach (int index in candidates)
                {
                    roll -= Math.Max(1, weights[index]);
                    if (roll >= 0) continue;
                    values[index]++;
                    break;
                }
                Str = values[0]; Con = values[1]; Int = values[2]; Wis = values[3]; Agi = values[4];
            }

            public byte Element => (PetID == 12032 || PetID == 12178) ? (byte)1 : (byte)0;

            public int CalculatedMaxHP => Math.Max(1, (int)Math.Round(
                (Math.Pow(Level, 0.35) * Con * 2) + Level + (Con * 2) + 180));

            public int CalculatedMaxSP => Math.Max(0, (int)Math.Round(
                (Math.Pow(Level, 0.3) * Wis * 3.2) + Level + (Wis * 2) + 94));

            public int CalculatedAtk => Math.Max(0, (int)Math.Round(
                (Level * (Element == 2 ? 2.0 : 1.4)) + (Str * 2.0)));

            public int CalculatedDef => Math.Max(0, (int)Math.Round(
                (Level * (Element == 0 ? 8.0 : 2.0)) + (Con * 1.75)));

            public int CalculatedMatk => Math.Max(0, (int)Math.Round(
                (Level * (Element == 2 ? 1.6 : 1.4)) + (Int * 2.0)));

            public int CalculatedMdef => Math.Max(0, (int)Math.Round(
                (Level * (Element == 2 ? 2.2 : 2.0)) + (Wis * 2.2)));

            public int CalculatedSpd => Math.Max(0, (int)Math.Round(
                (Level * (Element == 3 ? 2.1 : 1.6)) + (Agi * 2.2)));

            public void NormalizeClientStats(bool refillVitals = false, Inventory inventory = null)
            {
                MaxHP = Math.Max(1, CalculatedMaxHP + (inventory?.GetPetEquipmentBonus(this, eq => eq.HP) ?? 0));
                MaxSP = Math.Max(0, CalculatedMaxSP + (inventory?.GetPetEquipmentBonus(this, eq => eq.SP) ?? 0));
                if (refillVitals)
                {
                    HP = MaxHP;
                    SP = MaxSP;
                }
                else
                {
                    HP = Math.Max(0, Math.Min(HP, MaxHP));
                    SP = Math.Max(0, Math.Min(SP, MaxSP));
                }
            }

            public void NormalizeExpForLevel()
            {
                if (Level < 1) Level = 1;
                while (Level < 199)
                {
                    uint requiredExp = GetRequiredExpForNextLevel(Level);
                    if (Exp < requiredExp) break;
                    Exp -= requiredExp;
                    Level++;
                    GrowLevelStat();
                }
            }

            public int GainExp(uint amount)
            {
                Exp = (uint)Math.Min(uint.MaxValue, (ulong)Exp + amount);
                byte oldLevel = Level;
                NormalizeExpForLevel();
                return Level - oldLevel;
            }
        }

        public static bool IsSamePetOrCompanion(uint id1, uint id2)
        {
            if (id1 == id2) return true;
            if (id1 == 0 || id2 == 0) return false;

            // Robinson: 12032 (NPC TID) <-> 12178 (Pet TID)
            if ((id1 == 12032 || id1 == 12178) && (id2 == 12032 || id2 == 12178)) return true;
            // Native Npc.dat identifies both Roca variants as Roca.
            if ((id1 == 14161 || id1 == 14162) && (id2 == 14161 || id2 == 14162)) return true;

            return false;
        }

        /// <summary>
        /// Resolves the verified client-facing companion ID. Robinson is the only
        /// protocol alias; other pets use their native Npc.dat ID in every packet.
        /// </summary>
        public static uint GetCompanionBroadcastId(uint petId)
        {
            if (petId == 12032) return 12178; // Robinson
            return petId; // No alias — return as-is
        }


        public bool HasStoryCompanionInParty(ushort templateId)
        {
            return QuestRelated.QuestManager.IsStoryCompanion(templateId) && HasRecruitedCompanion(templateId);
        }

        // Presence in the party is separate from historical quest completion and Hotel ownership.
        public bool HasRecruitedCompanion(string npcName, ushort templateId)
        {
            return PlayerPets != null && PlayerPets.Values.Any(p => p != null &&
                (templateId > 0 ? IsSamePetOrCompanion(p.PetID, templateId) :
                !string.IsNullOrWhiteSpace(npcName) && string.Equals(p.PetName, npcName.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        public bool HasRecruitedCompanion(ushort templateId) => HasRecruitedCompanion("", templateId);

        public Action<Player> OnDisconnect { get; set; }

        public Player() : base()
        {
            QueueData = new Queue<SendPacket>(25);
        }

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
            // Native storage records are additive. Clear the client storage cache
            // before one AC30:1 list; the bag is already populated at login.
            Send(Tools.FromFormat("bb", 30, 8));
            if (m_storage != null)
                Send(new SendPacket(m_storage.GetAC30_5(30, 1)));

            Send(Tools.FromFormat("bb", 29, 6));
            Send(Tools.FromFormat("bb", 20, 9));
            SendPacket uiPkt = new SendPacket();
            uiPkt.PackArray(new byte[] { 35, 12, 0x98, 0x98, 0x01, 0x00, 0x00 });
            Send(uiPkt);
            Send(Tools.FromFormat("bb", 20, 8));
        }

        public void OpenMoneyBank()
        {
            Send(Tools.FromFormat("bb", 29, 6));
            Send(Tools.FromFormat("bb", 20, 8));
        }

        public ushort PendingRestMap { get; set; }
        public byte? NpcSaleMode { get; set; }
        public ushort NpcSaleMap { get; set; }
        public ushort LastNpcClick { get; set; }
        public ushort LastNpcMap { get; set; }
        public DateTime NpcClickResumeAt { get; set; }

        public void OpenNpcSale(byte mode)
        {
            NpcSaleMode = mode;
            NpcSaleMap = (ushort)CurMap.MapID;
            Send(Tools.FromFormat("bb", 27, mode == 0 ? 4 : 3));
        }

        public void BeginNpcRest()
        {
            bool needsRest = Eqs.CurHP < Eqs.FullHP || Eqs.CurSP < Eqs.FullSP ||
                PlayerPets.Values.Any(p => p != null && (p.HP < p.MaxHP || p.SP < p.MaxSP));
            PendingRestMap = needsRest ? (ushort)CurMap.MapID : (ushort)0;
            var packet = new SendPacket();
            packet.Pack8(31); packet.Pack8(2);
            packet.Pack32(needsRest ? 0u : uint.MaxValue); // free confirmation / already rested
            Send(packet);
        }

        public void ConfirmNpcRest()
        {
            if (PendingRestMap == 0 || CurMap == null || CurMap.MapID != PendingRestMap ||
                Game.Battle.PvEBattleManager.IsInBattle(this)) return;
            PendingRestMap = 0;
            Eqs.CurHP = Eqs.FullHP; Eqs.CurSP = Eqs.FullSP; Eqs.Send8_1(true);
            foreach (var pet in PlayerPets.Values.Where(p => p != null))
            {
                pet.HP = pet.MaxHP; pet.SP = pet.MaxSP;
                Game.QuestRelated.QuestManager.SendPetProgression(this, pet);
            }
            Send(Tools.FromFormat("bbd", 5, 18, CharID));
            Send(Tools.FromFormat("bbb", 31, 1, 0));
            SaveCharacterData();
        }

        public void OpenPetHotel()
        {
            SendPetHotelList();
            Send(Tools.FromFormat("bb", 31, 7));
        }

        public void SendPetHotelList()
        {
            // Native 0x3b377c: 64-byte records plus a length-prefixed name.
            // Explicit removals clear stale slots; AC31:6 only updates supplied records.
            for (byte slot = 1; slot <= 10; slot++)
                if (HotelPets == null || !HotelPets.ContainsKey(slot)) Send(Tools.FromFormat("bbb", 31, 4, slot));
            if (HotelPets == null || HotelPets.Count == 0) return;
            var packet = new SendPacket();
            packet.PackArray(new byte[] { 31, 6 });
            foreach (var entry in HotelPets.OrderBy(kv => kv.Key))
            {
                var pet = entry.Value;
                if (entry.Key < 1 || entry.Key > 10 || pet == null || pet.PetID == 0) continue;
                packet.Pack8(entry.Key);
                uint clientId = GetCompanionBroadcastId(pet.PetID);
                packet.Pack16((ushort)clientId);
                packet.Pack32(pet.ClientTotalExp);
                packet.Pack8(pet.Level);
                packet.Pack32((uint)Math.Max(0, pet.HP));
                packet.Pack16((ushort)Math.Max(0, Math.Min(ushort.MaxValue, pet.SP)));
                packet.Pack16(pet.Int); packet.Pack16(pet.Str); packet.Pack16(pet.Con);
                packet.Pack16(pet.Agi); packet.Pack16(pet.Wis);
                packet.Pack8(pet.Reborn ? (byte)1 : (byte)0);
                packet.Pack8(pet.Job); packet.Pack8(0); packet.Pack16(pet.SkillPoints);
                byte[] name = System.Text.Encoding.ASCII.GetBytes(pet.PetName ?? "").Take(10).ToArray();
                packet.Pack8((byte)name.Length); packet.PackArray(name);
                pet.PackSkills(packet);
                foreach (ushort id in new[] { pet.Eq_Head, pet.Eq_Body, pet.Eq_Weapon, pet.Eq_Wrist, pet.Eq_Shoes, pet.Eq_Special })
                    packet.Pack16(id);
                packet.Pack8(0); packet.Pack8(0); packet.Pack8(0);
                packet.Pack16(0); packet.Pack16(0);
            }
            Send(packet);
        }
        public EquipManager Eqs { get { return ((EquipManager)this) ?? null; } }
        public byte Emote { get { lock (mlock) return emote; } set { lock (mlock) emote = value; } }
        public Game.Code.Tent Tent { get { return m_tent; } }
        public Game.Battle.BattleScene MyBattle { get { return m_battle; } set { m_battle = value; } }
        public RiceBall RiceBall { get { return m_riceball; } }
        public int CurInstance { get; set; }

        public void WearEQ(byte fromLoc)
        {
            var previousStats = EquipmentStats();
            if (m_inv == null || !m_inv.TryEquip(Eqs, fromLoc)) return;
            // Native 23:17 moves the bag item onto its equipment slot and returns
            // the previously worn item to the second inventory coordinate.
            Send(Tools.FromFormat("bbbb", 23, 17, fromLoc, fromLoc));
            Eqs.Send8_1();
            SendEquipmentStatChanges(previousStats, EquipmentStats(), "Equipment: ");
        }

        public void unWearEQ(byte fromLoc, byte toLoc)
        {
            var previousStats = EquipmentStats();
            if (m_inv == null || !m_inv.TryUnequip(Eqs, fromLoc, toLoc)) return;
            Send(Tools.FromFormat("bbbb", 23, 16, fromLoc, toLoc));
            Eqs.Send8_1();
            SendEquipmentStatChanges(previousStats, EquipmentStats(), "Equipment: ");
        }

        internal int[] PetEquipmentStats(PlayerPetData pet)
        {
            return new[] {
                Math.Max(1, pet.CalculatedMaxHP + Inv.GetPetEquipmentBonus(pet, eq => eq.HP)),
                Math.Max(0, pet.CalculatedMaxSP + Inv.GetPetEquipmentBonus(pet, eq => eq.SP)),
                Math.Max(0, pet.CalculatedAtk + Inv.GetPetEquipmentBonus(pet, eq => eq.ATK)),
                Math.Max(0, pet.CalculatedDef + Inv.GetPetEquipmentBonus(pet, eq => eq.DEF)),
                Math.Max(0, pet.CalculatedMatk + Inv.GetPetEquipmentBonus(pet, eq => eq.MAT)),
                Math.Max(0, pet.CalculatedMdef + Inv.GetPetEquipmentBonus(pet, eq => eq.MDF)),
                Math.Max(0, pet.CalculatedSpd + Inv.GetPetEquipmentBonus(pet, eq => eq.SPD))
            };
        }

        internal void SendPetEquipmentStatChanges(PlayerPetData pet, int[] previous)
        {
            QuestRelated.QuestManager.SendPetEquipmentStats(this, pet);
            SendEquipmentStatChanges(previous, PetEquipmentStats(pet), (pet.PetName ?? "Pet") + " equipment: ");
        }

        int[] EquipmentStats()
        {
            return new[] { Eqs.FullHP, Eqs.FullSP, Eqs.FullAtk, Eqs.FullDef, Eqs.FullMatk, Eqs.FullMdef, Eqs.FullSpd };
        }

        void SendEquipmentStatChanges(int[] previous, int[] current, string prefix)
        {
            string[] names = { "Max HP", "Max SP", "ATK", "DEF", "MAT", "MDF", "SPD" };
            var changes = new List<string>();
            for (int i = 0; i < current.Length; i++)
            {
                int delta = current[i] - previous[i];
                if (delta != 0) changes.Add(names[i] + " " + (delta > 0 ? "+" : "") + delta);
            }
            if (changes.Count > 0) SendHeadBanner(prefix + string.Join(", ", changes));
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
                NativePropStates.Clear();
                NativeActorVisibility.Clear();
                if (base.CurMap != value)
                {
                    ClearInteraction();
                    PendingRestMap = 0;
                    NpcSaleMode = null;
                    NpcClickResumeAt = DateTime.MinValue;
                }
                if (base.CurMap != null)
                {
                    if (base.CurMap is GameMap)
                    {
                        prevMap = new WarpData();
                        prevMap.DstMap = (ushort)base.CurMap.MapID;
                        prevMap.DstX_Axis = CurX;
                        prevMap.DstY_Axis = CurY;

                        // When entering a tent from an overworld map, record the tent return location
                        if (value != null && (value is Game.Code.Tent || (value as GameMap)?.Type == MapType.Tent))
                        {
                            if ((base.CurMap as GameMap)?.Type != MapType.Tent)
                            {
                                TentReturnMap = new WarpData()
                                {
                                    DstMap = (ushort)base.CurMap.MapID,
                                    DstX_Axis = CurX,
                                    DstY_Axis = CurY
                                };
                            }
                        }

                        (base.CurMap as GameMap).onItemDropped_fromMap = null;
                        (base.CurMap as GameMap).onItemPickup_fromMap = null;
                    }
                }
                base.CurMap = value;
                LastMapEnterTime = DateTime.UtcNow;
                StepsSinceLastBattle = 0;
                ProximityEncounterArmed = true;
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

        public void SendRecordPointStatus()
        {
            var point = ReturnSpawnMap;
            // Native AC5:21: 1 enables Record, 2 clears it; 0 leaves it unchanged.
            Send(Tools.FromFormat("bbb", 5, 21, (byte)(point != null && point.DstMap != 0 ? 1 : 2)));
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
            m_socket?.SendPacket(p);

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

                // The installed host has no AC20:4 dispatcher. Handle native region
                // notifications here so this core hotfix preserves the running host.
                if (p.A == 20 && p.B == 4)
                {
                    if (p.Buffer.Length == 8)
                        EveEventInterpreter.ExecuteRegionRequest(this, BitConverter.ToUInt16(p.Buffer, 6));
                    else if (!NativeEventActive && !Game.Battle.PvEBattleManager.IsInBattle(this))
                        Send(Tools.FromFormat("bb", 20, 8));
                    return;
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
                            c.SendSystemMessage($"==========  ITEM MALL (Balance: {pts} IM Pts) ==========");
                            var catalog = Game.PlayerRelated.ItemMallManager.GetCatalog();
                            for (int i = 0; i < catalog.Count; i++)
                            {
                                var it = catalog[i];
                                c.SendSystemMessage($"[{i + 1}] {it.ItemName} (x{it.Count}) - {it.PointCost} Pts -> Type: /buy {i + 1}");
                            }
                            c.SendSystemMessage(" Type /buy <number> to purchase directly into your inventory!");
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
                                        c.SendSystemMessage($" Purchased {targetItem.ItemName} (x{targetItem.Count}) for {targetItem.PointCost} IM Points! Remaining: {Game.PlayerRelated.ItemMallManager.GetUserPoints(c)} Pts.");
                                    }
                                    else
                                    {
                                        c.SendSystemMessage($" Purchase failed! Cost: {targetItem.PointCost} Pts (Your Balance: {Game.PlayerRelated.ItemMallManager.GetUserPoints(c)} Pts).");
                                    }
                                }
                                else
                                {
                                    c.SendSystemMessage(" Item not found! Type /im to see the available list.");
                                }
                            }
                            else
                            {
                                c.SendSystemMessage(" Usage: /buy <number> (e.g. /buy 1)");
                            }
                        }
                        else if (chatMsg.StartsWith("/points", StringComparison.OrdinalIgnoreCase) ||
                                 chatMsg.StartsWith(":points", StringComparison.OrdinalIgnoreCase) ||
                                 chatMsg.StartsWith("/myim", StringComparison.OrdinalIgnoreCase) ||
                                 chatMsg.StartsWith(":myim", StringComparison.OrdinalIgnoreCase))
                        {
                            int pts = Game.PlayerRelated.ItemMallManager.GetUserPoints(c);
                            c.SendSystemMessage($" Your Current Balance: {pts} IM Points.");
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


                // AC23 owns inventory/equipment commands; legacy handlers would apply them twice.
                if (p.A != 23)
                {
                    base.ProcessSocket(this, p);
                    m_inv.ProcessSocket(p);
                }
                if (m_settings != null)
                    m_settings.ProcessSocket(p);
                if (m_battle != null)
                    m_battle.ProcessSocket(p);
                if (m_tent != null)
                    m_tent.Process(this, p);
            }
            catch (Exception) { }
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
                var pet = PlayerPets?.Values?.FirstOrDefault(x => IsSamePetOrCompanion(x.PetID, petId));
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
            pkt.Pack16(0);
            pkt.Pack8(0);
            pkt.Pack8(0);
            pkt.Pack16(0); // Native AC 15:4 has eight bytes after the length-prefixed name.
            return pkt;
        }

        public void BroadcastPetAppearance(uint petId = 0, string petName = "")
        {
            if (petId == 0) petId = ActivePetID;
            if (petId == 0) return;

            var mapPkt = CreatePetMapPacket(petId, petName);
            if (mapPkt != null)
            {
                CurMap?.Broadcast(mapPkt, "Ex", this.CharID);
            }

            var pet = PlayerPets?.Values?.FirstOrDefault(x => IsSamePetOrCompanion(x.PetID, petId));
            if (pet != null)
            {
                pet.NormalizeExpForLevel();
                // Neither the owner nor peers need another recruit notification.
                SendPacket petNamePacket = QuestRelated.QuestManager.CreatePetNamePacket(this, pet);
                if (petNamePacket != null) Send(petNamePacket);
            }
            if (pet != null) QuestRelated.QuestManager.SendPetProgression(this, pet);

            // AC 19:4 overwrites the local active pet ID. It is not a map broadcast.

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
            if (string.IsNullOrEmpty(petID) || !uint.TryParse(petID, out uint pid) || pid == 0) return false;

            if (PlayerPets == null) PlayerPets = new Dictionary<byte, PlayerPetData>();

            // Ensure pet is registered in PlayerPets (slots 1..4)
            if (!PlayerPets.Values.Any(p => p.PetID == pid))
            {
                byte freeSlot = 1;
                while (PlayerPets.ContainsKey(freeSlot) && freeSlot <= 4) freeSlot++;
                if (freeSlot <= 4)
                {
                    string petName = QuestRelated.QuestManager.GetNpcName(pid) ?? $"Pet #{pid}";
                    var newPet = new PlayerPetData
                    {
                        Slot = freeSlot,
                        PetID = pid,
                        PetName = petName,
                        Level = 10,
                        HP = 500,
                        MaxHP = 500,
                        SP = 200,
                        MaxSP = 200,
                        Amity = 100,
                        IsBattle = false,
                        IsRide = false
                    };
                    newPet.NormalizeExpForLevel();
                    newPet.InitializeBaseStats();
                    newPet.NormalizeClientStats(true);
                    PlayerPets[freeSlot] = newPet;
                    SendPacket p = QuestRelated.QuestManager.CreatePetPacket(this, pid, freeSlot, newPet.HP, newPet.MaxHP, newPet.SP, newPet.MaxSP, newPet.Amity, newPet.Level, newPet.Str, newPet.Con, newPet.Int, newPet.Wis, newPet.Agi, newPet.Exp, newPet.Reborn, newPet.Job);
                    if (RegisterClientPet(newPet)) Send(p);
                    QuestRelated.QuestManager.SendPetProgression(this, newPet);
                }
            }

            return true;
        }

        public void PutPetToBattle(string petID)
        {
            if (string.IsNullOrEmpty(petID) || !uint.TryParse(petID, out uint pid) || pid == 0) return;

            ActivePetID = pid;

            if (PlayerPets != null)
            {
                foreach (var kvp in PlayerPets)
                {
                    kvp.Value.IsBattle = (kvp.Value.PetID == pid);
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

            ActiveMountID = pid;

            byte slot = 1;
            if (PlayerPets != null)
            {
                foreach (var kvp in PlayerPets)
                {
                    if (kvp.Value.PetID == pid)
                    {
                        kvp.Value.IsRide = true;
                        slot = kvp.Value.Slot;
                    }
                    else
                    {
                        kvp.Value.IsRide = false;
                    }
                }
            }

            SendPacket rp = new SendPacket();
            rp.PackArray(new byte[] { 15, 16 }); // put into ride npc mode
            rp.Pack8(slot);
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

            if (PlayerPets != null)
            {
                foreach (var kvp in PlayerPets)
                {
                    kvp.Value.IsRide = false;
                }
            }

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

        #region Player
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
                    var skillData = Game.SkillRelated.SkillManager.GetSkill((ushort)Game.SkillRelated.SkillManager.GetClientSkillId(this, sk.SkillID));
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

            // Native trailer starts with permanent HP/SP bonuses, not POINT/Potential.
            // POINT and Potential are synchronized separately through AC8:1 stats 38/37.
            p.Add((ushort)0);                            // 2 bytes: Additional maximum HP
            p.Add((ushort)0);                            // 2 bytes: Additional maximum SP
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
        public bool NativeEventActive { get; internal set; }
        internal object NativeEventToken;
        public Action OnInteractionComplete;
        public Action<byte> OnDialogueChoice;
        public Action OnMinigameWon;
        public Action OnMinigameLost;
        public ushort TransformedModelID { get; set; } = 0;
        public bool PendingBeachCutscene { get; set; }
        public bool BeachCutsceneActive { get; set; }
        public int BeachCutsceneStep { get; set; } = 0;
        public bool PlayingStormCutscene { get; set; }
        public Queue<Action> StepQueue { get; set; } = new Queue<Action>();
        public int LastDialogueAdvanceTick { get; set; } = 0;

        public void CancelInteraction()
        {
            ClearInteraction();
            NpcClickResumeAt = DateTime.UtcNow.AddMilliseconds(1500);
            // AC6:2/AC20:8 release interaction; AC5:4 would replay map-entry UI.
            Send(Tools.FromFormat("bbb", 6, 2, 0));
            Send(Tools.FromFormat("bb", 20, 8));
        }

        public void ClearInteraction()
        {
            NativeEventActive = false;
            NativeEventToken = null;
            QueueData?.Clear();
            StepQueue?.Clear();
            OnDialogueChoice = null;
            OnInteractionComplete = null;
            OnMinigameWon = null;
            OnMinigameLost = null;
        }

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
                bool nativeEvent = NativeEventActive;
                OnInteractionComplete = null;
                action.Invoke();
                if (!nativeEvent && !NativeEventActive && this.CurMap is GameMap gmap)
                {
                    QuestRelated.QuestManager.ReplayActorVisibility(this, gmap);
                }
                return true;
            }
            return NativeEventActive;
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

                // 0.1 Clean up deployed tent if open (evacuate occupants to overworld)
                if (m_tent != null && !m_tent.IsClosed)
                {
                    m_tent.Close();
                }

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
