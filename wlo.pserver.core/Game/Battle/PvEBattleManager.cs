using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Maps;
using Game.QuestRelated;
using Network;

namespace Game.Battle
{
    public class BattleMonster
    {
        public uint MonsterId { get; set; }
        public string MonsterName { get; set; }
        public int MonsterLevel { get; set; }
        public int MonsterMaxHP { get; set; }
        public int MonsterHP { get; set; }
        public int MonsterMaxSP { get; set; }
        public int MonsterSP { get; set; }
        public int MonsterElement { get; set; }
        public int MonsterAtk { get; set; }
        public int MonsterDef { get; set; }
        public int MonsterSpd { get; set; }
        public ushort ClickId { get; set; }
        public byte GridX { get; set; }
        public byte GridY { get; set; }
        public bool IsDead => MonsterHP <= 0;
        public bool IsCaptured { get; set; } = false;
    }

    public class QuestBattleContext
    {
        public uint QuestID { get; set; }
        public byte Step { get; set; }
        public ushort MapID { get; set; }
        public ushort ClickID { get; set; }
        public Action OnVictory { get; set; }
        public Action OnDefeat { get; set; }
    }

    public enum BattleFighterType : byte
    {
        Player = 2,
        Pet = 4,
        Monster = 7
    }

    public enum BattleTeamSide : byte
    {
        Attacker = 1, // Right side (GridX = 4 for players, GridX = 3 for pets)
        Defender = 2  // Left side (GridX = 1 for players/monsters, GridX = 2 for pets/frontline)
    }

    public enum FighterStatusType : byte
    {
        None = 0,
        Frozen = 1,       // Freeze / Ice Seal / Freeze Strike (cannot act)
        Sleep = 2,        // Sleep (cannot act, wakes up on damage)
        Sealed = 3,       // Tree Bind / Stone Curse / Seal (cannot act)
        Confused = 4,     // Mess / Chaos (attacks random target)
        Poisoned = 5,     // Loses HP per turn
        Paralyzed = 6,    // Stunned / Bound (cannot act)
        Shielded = 7,     // Earth Barrier / Holy Shield (takes 50% damage or absorbs)
        HotBlooded = 8,   // 2x damage dealt
        SpeedUp = 9,      // Increased speed
        Vanish = 10       // Invisibility
    }

    public class FighterStatus
    {
        public FighterStatusType StatusType { get; set; }
        public int RemainingTurns { get; set; }
        public int Value { get; set; }

        public FighterStatus() { }
        public FighterStatus(FighterStatusType type, int turns, int val = 0)
        {
            StatusType = type;
            RemainingTurns = turns;
            Value = val;
        }
    }

    public class BattleFighter
    {
        public BattleTeamSide Side { get; set; } = BattleTeamSide.Attacker;
        public BattleFighterType FighterType { get; set; } = BattleFighterType.Player;
        public Player PlayerRef { get; set; }
        public Player.PlayerPetData PetRef { get; set; }
        public BattleMonster MonsterRef { get; set; }

        public uint ID { get; set; }
        public ushort ClickID { get; set; }
        public uint OwnerID { get; set; } // CharID for pets, 0 for players/monsters
        public string Name { get; set; }
        public byte Level { get; set; } = 1;
        public byte Element { get; set; } = 0;
        public int MaxHP { get; set; } = 100;
        public int CurHP { get; set; } = 100;
        public int MaxSP { get; set; } = 50;
        public int CurSP { get; set; } = 50;
        public int Atk { get; set; } = 10;
        public int Def { get; set; } = 10;
        public int Spd { get; set; } = 10;

        public byte GridX { get; set; }
        public byte GridY { get; set; }

        public bool IsDead => CurHP <= 0;
        public bool IsCaptured { get; set; } = false;

        public List<FighterStatus> ActiveStatuses { get; set; } = new List<FighterStatus>();
        public bool HasStatus(FighterStatusType type) => ActiveStatuses.Any(s => s.StatusType == type && s.RemainingTurns > 0);
        public void AddStatus(FighterStatusType type, int turns, int val = 0)
        {
            var existing = ActiveStatuses.FirstOrDefault(s => s.StatusType == type);
            if (existing != null)
            {
                existing.RemainingTurns = Math.Max(existing.RemainingTurns, turns);
                existing.Value = val;
            }
            else
            {
                ActiveStatuses.Add(new FighterStatus(type, turns, val));
            }
        }
        public bool CanAct => !HasStatus(FighterStatusType.Frozen) && !HasStatus(FighterStatusType.Sleep) && !HasStatus(FighterStatusType.Sealed) && !HasStatus(FighterStatusType.Paralyzed);
    }

    public class PendingAction
    {
        public BattleFighter Actor { get; set; }
        public Player Player { get; set; }
        public string ActionType { get; set; } // "attack", "defend", "catch", "flee"
        public ushort SkillId { get; set; }
        public byte TargetGridX { get; set; }
        public byte TargetGridY { get; set; }
    }

    public class ActiveBattle
    {
        public List<Player> AttackingPlayers { get; set; } = new List<Player>();
        public List<Player> DefendingPlayers { get; set; } = new List<Player>();

        public List<BattleFighter> Attackers { get; set; } = new List<BattleFighter>();
        public List<BattleFighter> Defenders { get; set; } = new List<BattleFighter>();

        public IEnumerable<Player> AllPlayers => AttackingPlayers.Concat(DefendingPlayers).Where(p => p != null).Distinct();

        public bool IsPvP => DefendingPlayers != null && DefendingPlayers.Count > 0;
        public bool IsRandomEncounter { get; set; } = false;
        public QuestBattleContext QuestContext { get; set; }
        public bool IsFinished { get; set; } = false;
        public int Turn { get; set; } = 0;

        // Turn collection system: collect all player & pet actions before processing (Keyed by (GridX << 8) | GridY)
        public Dictionary<int, PendingAction> PendingActions { get; set; } = new Dictionary<int, PendingAction>();
        public bool IsTurnProcessing { get; set; } = false;
        private System.Threading.Timer _turnTimer;

        public int ExpectedActionCount
        {
            get
            {
                int count = 0;
                foreach (var p in AllPlayers)
                {
                    var f = Attackers.Concat(Defenders).FirstOrDefault(x => x.PlayerRef == p);
                    if (f != null && !f.IsDead) count++;
                }
                foreach (var f in Attackers.Concat(Defenders))
                {
                    if (f.FighterType == BattleFighterType.Pet && !f.IsDead)
                    {
                        var owner = AllPlayers.FirstOrDefault(p => p.CharID == f.OwnerID);
                        if (owner != null) count++;
                    }
                }
                return Math.Max(1, count);
            }
        }

        public void StartTurnTimer(Action<ActiveBattle> onTimeout)
        {
            CancelTurnTimer();
            _turnTimer = new System.Threading.Timer(_ =>
            {
                CancelTurnTimer();
                onTimeout?.Invoke(this);
            }, null, 30000, System.Threading.Timeout.Infinite);
        }

        public void CancelTurnTimer()
        {
            var t = _turnTimer;
            _turnTimer = null;
            t?.Dispose();
        }

        // Backward compatibility properties
        public Player LeaderPlayer => AttackingPlayers.FirstOrDefault();
        public Player Player
        {
            get => AttackingPlayers.FirstOrDefault();
            set
            {
                if (value != null && !AttackingPlayers.Contains(value))
                    AttackingPlayers.Insert(0, value);
            }
        }

        public List<BattleMonster> Monsters { get; set; } = new List<BattleMonster>();

        public Player.PlayerPetData BattlePet
        {
            get => Attackers.FirstOrDefault(a => a.PetRef != null && a.OwnerID == Player?.CharID)?.PetRef;
            set
            {
                if (value != null && Player != null)
                {
                    var existing = Attackers.FirstOrDefault(a => a.PetRef != null && a.OwnerID == Player.CharID);
                    if (existing != null) existing.PetRef = value;
                }
            }
        }

        public byte PlayerGridX => Attackers.FirstOrDefault(a => a.PlayerRef == Player)?.GridX ?? 4;
        public byte PlayerGridY => Attackers.FirstOrDefault(a => a.PlayerRef == Player)?.GridY ?? 2;
        public byte PetGridX => Attackers.FirstOrDefault(a => a.PetRef != null && a.OwnerID == Player?.CharID)?.GridX ?? 3;
        public byte PetGridY => Attackers.FirstOrDefault(a => a.PetRef != null && a.OwnerID == Player?.CharID)?.GridY ?? 2;

        public int PetHP
        {
            get => Attackers.FirstOrDefault(a => a.PetRef != null && a.OwnerID == Player?.CharID)?.CurHP ?? 0;
            set
            {
                var f = Attackers.FirstOrDefault(a => a.PetRef != null && a.OwnerID == Player?.CharID);
                if (f != null) { f.CurHP = value; if (f.PetRef != null) f.PetRef.HP = value; }
            }
        }

        public int PetSP
        {
            get => Attackers.FirstOrDefault(a => a.PetRef != null && a.OwnerID == Player?.CharID)?.CurSP ?? 0;
            set
            {
                var f = Attackers.FirstOrDefault(a => a.PetRef != null && a.OwnerID == Player?.CharID);
                if (f != null) { f.CurSP = value; if (f.PetRef != null) f.PetRef.SP = value; }
            }
        }

        public bool HasPet => Attackers.Any(a => a.PetRef != null && a.OwnerID == Player?.CharID && !a.IsDead);
        public BattleMonster PrimaryMonster => Monsters.FirstOrDefault(m => !m.IsDead) ?? Monsters.FirstOrDefault();
        public uint MonsterId { get => PrimaryMonster?.MonsterId ?? 0; set { if (PrimaryMonster != null) PrimaryMonster.MonsterId = value; } }
        public string MonsterName { get => PrimaryMonster?.MonsterName ?? "Monster"; set { if (PrimaryMonster != null) PrimaryMonster.MonsterName = value; } }
        public int MonsterLevel { get => PrimaryMonster?.MonsterLevel ?? 1; set { if (PrimaryMonster != null) PrimaryMonster.MonsterLevel = value; } }
        public int MonsterMaxHP { get => PrimaryMonster?.MonsterMaxHP ?? 100; set { if (PrimaryMonster != null) PrimaryMonster.MonsterMaxHP = value; } }
        public int MonsterHP { get => PrimaryMonster?.MonsterHP ?? 0; set { if (PrimaryMonster != null) PrimaryMonster.MonsterHP = value; } }
        public int MonsterMaxSP { get => PrimaryMonster?.MonsterMaxSP ?? 50; set { if (PrimaryMonster != null) PrimaryMonster.MonsterMaxSP = value; } }
        public int MonsterSP { get => PrimaryMonster?.MonsterSP ?? 0; set { if (PrimaryMonster != null) PrimaryMonster.MonsterSP = value; } }
        public int MonsterElement { get => PrimaryMonster?.MonsterElement ?? 0; set { if (PrimaryMonster != null) PrimaryMonster.MonsterElement = value; } }
        public int MonsterAtk { get => PrimaryMonster?.MonsterAtk ?? 10; set { if (PrimaryMonster != null) PrimaryMonster.MonsterAtk = value; } }
        public int MonsterDef { get => PrimaryMonster?.MonsterDef ?? 10; set { if (PrimaryMonster != null) PrimaryMonster.MonsterDef = value; } }
        public int MonsterSpd { get => PrimaryMonster?.MonsterSpd ?? 10; set { if (PrimaryMonster != null) PrimaryMonster.MonsterSpd = value; } }
        public ushort ClickId { get => PrimaryMonster?.ClickId ?? 0; set { if (PrimaryMonster != null) PrimaryMonster.ClickId = value; } }
        public byte MonsterGridX { get => PrimaryMonster?.GridX ?? 2; set { if (PrimaryMonster != null) PrimaryMonster.GridX = value; } }
        public byte MonsterGridY { get => PrimaryMonster?.GridY ?? 2; set { if (PrimaryMonster != null) PrimaryMonster.GridY = value; } }
    }

    public static class PvEBattleManager
    {
        private static readonly Dictionary<uint, ActiveBattle> _activeBattles = new Dictionary<uint, ActiveBattle>();
        private static readonly object _lock = new object();
        private static readonly Random _rng = new Random();

        // WLO Player/Attacking Team Grid Positions:
        // Slot 0 (Leader): Player (4, 2), Pet (3, 2)
        // Slot 1 (Member 1): Player (4, 3), Pet (3, 3)
        // Slot 2 (Member 2): Player (4, 1), Pet (3, 1)
        // Slot 3 (Member 3): Player (4, 4), Pet (3, 4)
        public static readonly byte[][] AttackerPlayerGridSlots = new byte[][]
        {
            new byte[] { 4, 2 },
            new byte[] { 4, 3 },
            new byte[] { 4, 1 },
            new byte[] { 4, 4 }
        };

        public static readonly byte[][] AttackerPetGridSlots = new byte[][]
        {
            new byte[] { 3, 2 },
            new byte[] { 3, 3 },
            new byte[] { 3, 1 },
            new byte[] { 3, 4 }
        };

        // WLO Defending PvP Team Grid Positions:
        // Slot 0 (Leader): Player (1, 2), Pet (2, 2)
        // Slot 1 (Member 1): Player (1, 3), Pet (2, 3)
        // Slot 2 (Member 2): Player (1, 1), Pet (2, 1)
        // Slot 3 (Member 3): Player (1, 4), Pet (2, 4)
        public static readonly byte[][] DefenderPlayerGridSlots = new byte[][]
        {
            new byte[] { 1, 2 },
            new byte[] { 1, 3 },
            new byte[] { 1, 1 },
            new byte[] { 1, 4 }
        };

        public static readonly byte[][] DefenderPetGridSlots = new byte[][]
        {
            new byte[] { 2, 2 },
            new byte[] { 2, 3 },
            new byte[] { 2, 1 },
            new byte[] { 2, 4 }
        };

        // WLO Enemy PvE Formation Grid Positions (Front row & Back row)
        public static readonly byte[][] EnemyGridSlots = new byte[][]
        {
            new byte[] { 2, 2 }, // Front Center
            new byte[] { 2, 3 }, // Front Right
            new byte[] { 2, 1 }, // Front Left
            new byte[] { 2, 4 }, // Front Far Right
            new byte[] { 1, 2 }, // Back Center
            new byte[] { 1, 3 }, // Back Right
            new byte[] { 1, 1 }, // Back Left
            new byte[] { 1, 4 }  // Back Far Right
        };

        public static bool IsInBattle(Player player)
        {
            if (player == null) return false;
            lock (_lock)
            {
                return _activeBattles.ContainsKey(player.CharID);
            }
        }

        public static ActiveBattle GetBattle(Player player)
        {
            if (player == null) return null;
            lock (_lock)
            {
                if (_activeBattles.TryGetValue(player.CharID, out var b))
                    return b;
                return null;
            }
        }

        /// <summary>
        /// Cleans up combat session when a player loses socket connection.
        /// </summary>
        public static void OnPlayerDisconnect(Player player)
        {
            if (player == null || player.CharID == 0) return;
            try
            {
                lock (_lock)
                {
                    if (_activeBattles.TryGetValue(player.CharID, out var battle))
                    {
                        DebugSystem.Write($"[PvEBattle] Player {player.CharName} disconnected during battle. Cleaning up battle session.");
                        _activeBattles.Remove(player.CharID);
                        battle.AttackingPlayers.Remove(player);
                        battle.DefendingPlayers.Remove(player);

                        // If no active players remain in the battle, terminate the battle and stop the turn timer
                        if (!battle.AllPlayers.Any() || battle.AttackingPlayers.Count == 0)
                        {
                            battle.IsFinished = true;
                            battle.CancelTurnTimer();
                            foreach (var p in battle.AllPlayers)
                            {
                                _activeBattles.Remove(p.CharID);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PvEBattle] Error in OnPlayerDisconnect: {ex.Message}");
            }
        }

        public static List<Player> GetTeamMembers(Player leader)
        {
            if (leader == null) return new List<Player>();
            if (leader.m_teammembers != null && leader.m_teammembers.Count > 0)
            {
                return leader.m_teammembers
                    .Where(p => p != null && p.CurMap == leader.CurMap)
                    .Take(4)
                    .ToList();
            }
            return new List<Player> { leader };
        }

        public static Player.PlayerPetData GetActivePet(Player p)
        {
            if (p == null || p.PlayerPets == null || p.PlayerPets.Count == 0) return null;
            var pet = p.PlayerPets.Values.FirstOrDefault(x => x.IsBattle && x.HP > 0);
            if (pet == null && p.ActivePetID > 0)
            {
                pet = p.PlayerPets.Values.FirstOrDefault(x => x.PetID == p.ActivePetID && x.HP > 0);
            }
            if (pet == null)
            {
                pet = p.PlayerPets.Values.FirstOrDefault(x => x.HP > 0);
            }
            if (pet == null)
            {
                pet = p.PlayerPets.Values.FirstOrDefault();
            }
            if (pet != null)
            {
                if (pet.HP <= 0) pet.HP = Math.Max(50, pet.MaxHP);
                if (pet.SP <= 0) pet.SP = Math.Max(20, pet.MaxSP);
                pet.IsBattle = true;
            }
            return pet;
        }

        public static string ResolveMonsterName(uint templateId)
        {
            switch (templateId)
            {
                case 17000: return "Water Jelly";
                case 17001: return "Earth Jelly";
                case 17002: return "Wind Jelly";
                case 17003: return "Fire Jelly";
                case 17004: return "Horned Beetle";
                case 17005: return "Forest Snail";
                case 17100: return "Wild Boar";
                case 17101: return "Fire Boar";
                case 17106: return "Wood Beetle";
                case 17107: return "Stag Beetle";
                case 17410: return "Island Wolf";
                case 17411: return "Wolf Leader";
                case 17412: return "Dire Wolf";
                case 17413: return "Wild Boar";
                case 17414: return "Wild Snail";
                case 17415: return "Giant Beetle";
                case 17416: return "Forest Treant";
                case 17417: return "Dark Bat";
                case 17418: return "Cave Spider";
                case 11066: return "Wolf Guard";
                default:
                    if (templateId >= 17000 && templateId <= 17003) return "Jelly";
                    if (templateId >= 17000 && templateId <= 19500) return $"Monster #{templateId}";
                    return "Wild Monster";
            }
        }

        public static bool IsSafeTownMap(ushort mapId)
        {
            // Town / Village / Interior safe zones (No random encounters)
            if (mapId == 10000) return true; // Kelan Village (Safe town)
            if (mapId >= 10001 && mapId <= 10036) return true; // Kelan houses, tent, beach
            if (mapId == 11000 || (mapId >= 11001 && mapId <= 11035)) return true; // Welling Village & houses
            if (mapId == 12000 || (mapId >= 12001 && mapId <= 12035)) return true; // Holy Village
            if (mapId == 13000 || (mapId >= 13001 && mapId <= 13035)) return true; // Kyoto
            if (mapId == 14000 || (mapId >= 14001 && mapId <= 14035)) return true; // Chang'an
            if (mapId == 15000 || (mapId >= 15001 && mapId <= 15035)) return true; // Maya
            if (mapId == 16000 || (mapId >= 16001 && mapId <= 16035)) return true; // India
            if (mapId == 17000 || (mapId >= 17001 && mapId <= 17035)) return true; // Rome
            if (mapId == 18000 || (mapId >= 18001 && mapId <= 18035)) return true; // Athens
            if (mapId == 19000 || (mapId >= 19001 && mapId <= 19035)) return true; // Egypt / Cairo
            if (mapId == 20000 || (mapId >= 20001 && mapId <= 20035)) return true; // Persia
            if (mapId == 21000 || (mapId >= 21001 && mapId <= 21035)) return true; // Cornwall
            if (mapId == 11094 || (mapId >= 60001 && mapId <= 60020)) return true; // Tents / Special interiors
            return false;
        }

        public static void CheckAndTriggerRandomEncounter(Player player)
        {
            if (player == null || IsInBattle(player)) return;

            GameMap map = player.CurMap as GameMap;
            if (map == null) return;

            ushort mapId = (ushort)map.MapID;
            if (IsSafeTownMap(mapId))
                return;

            var candidateMobs = map.NpcList?
                .OfType<QuestNpc>()
                .Where(n => n.IsWildMonster())
                .ToList();

            if (candidateMobs == null || candidateMobs.Count == 0)
            {
                return;
            }

            StartRandomEncounterFromPool(player, map, candidateMobs);
        }

        public static void StartProximityEncounter(Player player, GameMap map, QuestNpc triggerMob)
        {
            if (player == null || triggerMob == null || map == null) return;
            if (IsSafeTownMap((ushort)map.MapID) || !triggerMob.IsWildMonster()) return;

            lock (_lock)
            {
                if (_activeBattles.ContainsKey(player.CharID)) return;
            }

            int monsterCount = QuestNpc.NextRandom(1, 5);
            ActiveBattle battle = new ActiveBattle
            {
                AttackingPlayers = GetTeamMembers(player),
                IsRandomEncounter = true
            };

            int baseLevel = Math.Max(1, (int)triggerMob.Level);
            int baseHp = triggerMob.HP > 0 ? (int)triggerMob.HP : (baseLevel * 30 + 100);
            int baseSp = baseLevel * 20 + 50;

            for (int i = 0; i < monsterCount; i++)
            {
                int varLv = Math.Max(1, baseLevel + QuestNpc.NextRandom(-1, 2));
                int varHp = (int)(baseHp * (0.9 + (QuestNpc.NextRandom(0, 20) / 100.0)));
                int varSp = varLv * 20 + 50;

                BattleMonster bm = new BattleMonster
                {
                    MonsterId = triggerMob.TemplateID > 0 ? triggerMob.TemplateID : 17003,
                    MonsterName = triggerMob.Name ?? "Monster",
                    MonsterLevel = varLv,
                    MonsterMaxHP = varHp,
                    MonsterHP = varHp,
                    MonsterMaxSP = varSp,
                    MonsterSP = varSp,
                    MonsterElement = triggerMob.Element,
                    MonsterAtk = (int)Math.Round(varLv * 1.5 + 5),
                    MonsterDef = (int)Math.Round(varLv * 1.2 + 3),
                    MonsterSpd = (int)Math.Round(varLv * 1.3 + 4),
                    ClickId = (ushort)(2000 + i),
                    GridX = EnemyGridSlots[i % EnemyGridSlots.Length][0],
                    GridY = EnemyGridSlots[i % EnemyGridSlots.Length][1]
                };

                battle.Monsters.Add(bm);
            }

            DebugSystem.Write($"[PvEBattle] Proximity Encounter triggered for {player.CharName} ({battle.AttackingPlayers.Count} team members) near {triggerMob.Name} on Map {map.MapID}: {battle.Monsters.Count} monsters spawned!");
            InitializeAndStartBattle(battle);
        }

        private static void StartRandomEncounterFromPool(Player player, GameMap map, List<QuestNpc> pool)
        {
            if (player == null || pool == null || pool.Count == 0) return;

            lock (_lock)
            {
                if (_activeBattles.ContainsKey(player.CharID)) return;
            }

            int monsterCount = QuestNpc.NextRandom(1, 5); // 1 to 4 monsters
            ActiveBattle battle = new ActiveBattle
            {
                AttackingPlayers = GetTeamMembers(player),
                IsRandomEncounter = true
            };

            for (int i = 0; i < monsterCount; i++)
            {
                var template = pool[QuestNpc.NextRandom(0, pool.Count)];
                int monLevel = Math.Max(1, (int)template.Level);
                int monHP = template.HP > 0 ? (int)template.HP : (monLevel * 30 + 100);
                int monSP = monLevel * 20 + 50;

                BattleMonster bm = new BattleMonster
                {
                    MonsterId = template.TemplateID > 0 ? template.TemplateID : 17003,
                    MonsterName = template.Name ?? "Monster",
                    MonsterLevel = monLevel,
                    MonsterMaxHP = monHP,
                    MonsterHP = monHP,
                    MonsterMaxSP = monSP,
                    MonsterSP = monSP,
                    MonsterElement = template.Element,
                    MonsterAtk = (int)Math.Round(monLevel * 1.5 + 5),
                    MonsterDef = (int)Math.Round(monLevel * 1.2 + 3),
                    MonsterSpd = (int)Math.Round(monLevel * 1.3 + 4),
                    ClickId = (ushort)(2000 + i),
                    GridX = EnemyGridSlots[i % EnemyGridSlots.Length][0],
                    GridY = EnemyGridSlots[i % EnemyGridSlots.Length][1]
                };

                battle.Monsters.Add(bm);
            }

            DebugSystem.Write($"[PvEBattle] Random Encounter triggered for {player.CharName} ({battle.AttackingPlayers.Count} team members) on Map {map.MapID}: {battle.Monsters.Count} monsters spawned in formation!");
            InitializeAndStartBattle(battle);
        }

        public static void StartPvEBattle(Player player, ushort clickId, string monsterName, int npcLv = 10, int npcHp = 250, uint monsterTid = 11066, QuestBattleContext questContext = null)
        {
            if (player == null) return;

            lock (_lock)
            {
                if (_activeBattles.ContainsKey(player.CharID))
                {
                    DebugSystem.Write($"[PvEBattle] Player {player.CharName} already in battle.");
                    return;
                }
            }

            int monMaxSP = npcLv * 20 + 50;
            int monAtk = (int)Math.Round(npcLv * 1.5 + 5);
            int monDef = (int)Math.Round(npcLv * 1.2 + 3);
            int monSpd = (int)Math.Round(npcLv * 1.3 + 4);

            ActiveBattle battle = new ActiveBattle
            {
                AttackingPlayers = GetTeamMembers(player),
                QuestContext = questContext
            };

            battle.Monsters.Add(new BattleMonster
            {
                MonsterId = monsterTid,
                MonsterName = monsterName,
                MonsterLevel = npcLv,
                MonsterMaxHP = npcHp,
                MonsterHP = npcHp,
                MonsterMaxSP = monMaxSP,
                MonsterSP = monMaxSP,
                MonsterElement = 0,
                MonsterAtk = monAtk,
                MonsterDef = monDef,
                MonsterSpd = monSpd,
                ClickId = clickId,
                GridX = 2,
                GridY = 2
            });

            DebugSystem.Write($"[PvEBattle] Starting Quest PvE battle for {player.CharName} ({battle.AttackingPlayers.Count} team members) vs {monsterName} (Lv: {npcLv}, HP: {npcHp})");
            InitializeAndStartBattle(battle);
        }

        public static void StartBattle(Player player, ushort clickId, uint targetNpcId)
        {
            if (player == null) return;

            lock (_lock)
            {
                if (_activeBattles.ContainsKey(player.CharID))
                {
                    DebugSystem.Write($"[PvEBattle] Player {player.CharName} already in battle.");
                    return;
                }
            }

            ushort realClickId = clickId;
            if (clickId > 255 && (clickId & 0xFF) == 0)
                realClickId = (ushort)(clickId >> 8);

            uint realMonsterTID = targetNpcId;
            string monName = "Monster";
            int monLevel = 1;
            int monMaxHP = 100;
            byte monElement = 0;

            GameMap map = player.CurMap as GameMap;
            var mapNpc = map?.NpcList.FirstOrDefault(n => n.CickID == realClickId) as QuestNpc;
            if (mapNpc == null && targetNpcId > 0)
            {
                mapNpc = map?.NpcList.FirstOrDefault(n => n.CickID == (targetNpcId & 0xFFFF) || (n is QuestNpc q && q.TemplateID == (targetNpcId & 0xFFFF))) as QuestNpc;
            }

            if (mapNpc != null)
            {
                realClickId = mapNpc.CickID;
                realMonsterTID = mapNpc.TemplateID > 0 ? mapNpc.TemplateID : targetNpcId;
                monName = mapNpc.Name;
                monLevel = Math.Max(1, (int)mapNpc.Level);
                monMaxHP = mapNpc.HP > 0 ? (int)mapNpc.HP : (monLevel * 30 + 100);
                monElement = mapNpc.Element;
            }
            else
            {
                ushort mapId = (player.CurMap != null) ? (ushort)player.CurMap.MapID : (ushort)10017;
                var dbInfo = DataBase.GameDataBase.GlobalInstance != null
                    ? DataBase.GameDataBase.GlobalInstance.ResolveNpcInfo(mapId, (byte)realClickId, (ushort)(targetNpcId & 0xFFFF))
                    : null;

                if (dbInfo != null)
                {
                    realMonsterTID = (targetNpcId & 0xFFFF) > 0 ? (targetNpcId & 0xFFFF) : 17003;
                    monName = dbInfo.Name;
                    monLevel = Math.Max(1, dbInfo.Level);
                    monMaxHP = dbInfo.HP > 0 ? dbInfo.HP : (monLevel * 30 + 100);
                    monElement = (byte)dbInfo.Element;
                }
            }

            if (realMonsterTID == 0 || (realMonsterTID > 20000 && mapNpc == null))
            {
                realMonsterTID = 17003;
            }

            int monMaxSP = monLevel * 20 + 50;
            int monAtk = (int)Math.Round(monLevel * 1.5 + 5);
            int monDef = (int)Math.Round(monLevel * 1.2 + 3);
            int monSpd = (int)Math.Round(monLevel * 1.3 + 4);

            ActiveBattle battle = new ActiveBattle
            {
                AttackingPlayers = GetTeamMembers(player)
            };

            battle.Monsters.Add(new BattleMonster
            {
                MonsterId = realMonsterTID,
                MonsterName = monName,
                MonsterLevel = monLevel,
                MonsterMaxHP = monMaxHP,
                MonsterHP = monMaxHP,
                MonsterMaxSP = monMaxSP,
                MonsterSP = monMaxSP,
                MonsterElement = monElement,
                MonsterAtk = monAtk,
                MonsterDef = monDef,
                MonsterSpd = monSpd,
                ClickId = realClickId,
                GridX = 2,
                GridY = 2
            });

            if (realMonsterTID >= 16000 && realMonsterTID <= 19200)
            {
                int companionCount = QuestNpc.NextRandom(0, 3);
                for (int i = 0; i < companionCount; i++)
                {
                    battle.Monsters.Add(new BattleMonster
                    {
                        MonsterId = realMonsterTID,
                        MonsterName = monName,
                        MonsterLevel = monLevel,
                        MonsterMaxHP = monMaxHP,
                        MonsterHP = monMaxHP,
                        MonsterMaxSP = monMaxSP,
                        MonsterSP = monMaxSP,
                        MonsterElement = monElement,
                        MonsterAtk = monAtk,
                        MonsterDef = monDef,
                        MonsterSpd = monSpd,
                        ClickId = (ushort)(realClickId + 100 + i),
                        GridX = EnemyGridSlots[i + 1][0],
                        GridY = EnemyGridSlots[i + 1][1]
                    });
                }
            }

            DebugSystem.Write($"[PvEBattle] Starting PvE battle for {player.CharName} ({battle.AttackingPlayers.Count} team members) vs {battle.MonsterName} ({battle.Monsters.Count} mobs, TID: {realMonsterTID}, ClickID: {realClickId})");
            InitializeAndStartBattle(battle);
        }

        public static void StartPvPBattle(Player attacker, Player defender)
        {
            if (attacker == null || defender == null) return;

            lock (_lock)
            {
                if (_activeBattles.ContainsKey(attacker.CharID) || _activeBattles.ContainsKey(defender.CharID))
                {
                    DebugSystem.Write($"[PvPBattle] One or both players are already in battle.");
                    return;
                }
            }

            ActiveBattle battle = new ActiveBattle
            {
                AttackingPlayers = GetTeamMembers(attacker),
                DefendingPlayers = GetTeamMembers(defender)
            };

            DebugSystem.Write($"[PvPBattle] Starting Team PvP Battle: Team {attacker.CharName} ({battle.AttackingPlayers.Count} players) VS Team {defender.CharName} ({battle.DefendingPlayers.Count} players) on Map {attacker.CurMap?.MapID}!");
            InitializeAndStartBattle(battle);
        }

        private static void BuildFighters(ActiveBattle battle)
        {
            battle.Attackers.Clear();

            // 1. Build Attacking Team (Right side)
            for (int i = 0; i < battle.AttackingPlayers.Count && i < AttackerPlayerGridSlots.Length; i++)
            {
                Player p = battle.AttackingPlayers[i];
                if (p == null) continue;

                battle.Attackers.Add(new BattleFighter
                {
                    Side = BattleTeamSide.Attacker,
                    FighterType = BattleFighterType.Player,
                    PlayerRef = p,
                    ID = p.CharID,
                    Name = p.CharName,
                    Level = p.Eqs?.Level ?? 1,
                    Element = (byte)(p.Eqs?.Element ?? 0),
                    MaxHP = Math.Max(1, p.Eqs?.FullHP ?? 100),
                    CurHP = Math.Max(1, p.Eqs?.CurHP ?? 100),
                    MaxSP = Math.Max(0, p.Eqs?.FullSP ?? 50),
                    CurSP = Math.Max(0, p.Eqs?.CurSP ?? 50),
                    Atk = p.Eqs?.FullAtk ?? 20,
                    Def = p.Eqs?.FullDef ?? 10,
                    Spd = p.Eqs?.FullSpd ?? 10,
                    GridX = AttackerPlayerGridSlots[i][0],
                    GridY = AttackerPlayerGridSlots[i][1]
                });

                // Check active battle pet for this player
                var pet = GetActivePet(p);
                if (pet != null)
                {
                    battle.Attackers.Add(new BattleFighter
                    {
                        Side = BattleTeamSide.Attacker,
                        FighterType = BattleFighterType.Pet,
                        PetRef = pet,
                        OwnerID = p.CharID,
                        ID = pet.PetID,
                        Name = pet.PetName ?? "Pet",
                        Level = pet.Level,
                        Element = 0,
                        MaxHP = Math.Max(1, pet.MaxHP),
                        CurHP = Math.Max(1, pet.HP),
                        MaxSP = Math.Max(0, pet.MaxSP),
                        CurSP = Math.Max(0, pet.SP),
                        Atk = Math.Max(15, (int)(pet.Level * 3 + pet.Str * 2)),
                        Def = Math.Max(10, (int)(pet.Level * 2 + pet.Con * 2)),
                        Spd = Math.Max(10, (int)(pet.Level * 2 + pet.Agi * 2)),
                        GridX = AttackerPetGridSlots[i][0],
                        GridY = AttackerPetGridSlots[i][1]
                    });
                }
            }

            // 2. Build Defending Team (Left side)
            if (battle.IsPvP)
            {
                battle.Defenders.Clear();
                for (int i = 0; i < battle.DefendingPlayers.Count && i < DefenderPlayerGridSlots.Length; i++)
                {
                    Player p = battle.DefendingPlayers[i];
                    if (p == null) continue;

                    battle.Defenders.Add(new BattleFighter
                    {
                        Side = BattleTeamSide.Defender,
                        FighterType = BattleFighterType.Player,
                        PlayerRef = p,
                        ID = p.CharID,
                        Name = p.CharName,
                        Level = p.Eqs?.Level ?? 1,
                        Element = (byte)(p.Eqs?.Element ?? 0),
                        MaxHP = Math.Max(1, p.Eqs?.FullHP ?? 100),
                        CurHP = Math.Max(1, p.Eqs?.CurHP ?? 100),
                        MaxSP = Math.Max(0, p.Eqs?.FullSP ?? 50),
                        CurSP = Math.Max(0, p.Eqs?.CurSP ?? 50),
                        Atk = p.Eqs?.FullAtk ?? 20,
                        Def = p.Eqs?.FullDef ?? 10,
                        Spd = p.Eqs?.FullSpd ?? 10,
                        GridX = DefenderPlayerGridSlots[i][0],
                        GridY = DefenderPlayerGridSlots[i][1]
                    });

                    var pet = GetActivePet(p);
                    if (pet != null)
                    {
                        battle.Defenders.Add(new BattleFighter
                        {
                            Side = BattleTeamSide.Defender,
                            FighterType = BattleFighterType.Pet,
                            PetRef = pet,
                            OwnerID = p.CharID,
                            ID = pet.PetID,
                            Name = pet.PetName ?? "Pet",
                            Level = pet.Level,
                            Element = 0,
                            MaxHP = Math.Max(1, pet.MaxHP),
                            CurHP = Math.Max(1, pet.HP),
                            MaxSP = Math.Max(0, pet.MaxSP),
                            CurSP = Math.Max(0, pet.SP),
                            Atk = Math.Max(15, (int)(pet.Level * 3 + pet.Str * 2)),
                            Def = Math.Max(10, (int)(pet.Level * 2 + pet.Con * 2)),
                            Spd = Math.Max(10, (int)(pet.Level * 2 + pet.Agi * 2)),
                            GridX = DefenderPetGridSlots[i][0],
                            GridY = DefenderPetGridSlots[i][1]
                        });
                    }
                }
            }
            else
            {
                battle.Defenders.Clear();
                for (int i = 0; i < battle.Monsters.Count && i < EnemyGridSlots.Length; i++)
                {
                    var m = battle.Monsters[i];
                    if (m == null) continue;
                    battle.Defenders.Add(new BattleFighter
                    {
                        Side = BattleTeamSide.Defender,
                        FighterType = BattleFighterType.Monster,
                        MonsterRef = m,
                        ID = m.MonsterId,
                        ClickID = m.ClickId > 0 ? m.ClickId : (ushort)(2000 + i),
                        Name = m.MonsterName ?? "Monster",
                        Level = (byte)Math.Min(255, m.MonsterLevel),
                        Element = (byte)m.MonsterElement,
                        MaxHP = Math.Max(1, m.MonsterMaxHP),
                        CurHP = Math.Max(1, m.MonsterHP),
                        MaxSP = Math.Max(0, m.MonsterMaxSP),
                        CurSP = Math.Max(0, m.MonsterSP),
                        Atk = m.MonsterAtk > 0 ? m.MonsterAtk : (int)Math.Round(m.MonsterLevel * 1.5 + 5),
                        Def = m.MonsterDef > 0 ? m.MonsterDef : (int)Math.Round(m.MonsterLevel * 1.2 + 3),
                        Spd = m.MonsterSpd > 0 ? m.MonsterSpd : (int)Math.Round(m.MonsterLevel * 1.3 + 4),
                        GridX = m.GridX != 0 ? m.GridX : EnemyGridSlots[i % EnemyGridSlots.Length][0],
                        GridY = m.GridY != 0 ? m.GridY : EnemyGridSlots[i % EnemyGridSlots.Length][1]
                    });
                }
            }
        }

        private static void InitializeAndStartBattle(ActiveBattle battle)
        {
            if (battle == null) return;

            BuildFighters(battle);

            lock (_lock)
            {
                foreach (var p in battle.AllPlayers)
                {
                    _activeBattles[p.CharID] = battle;
                }
            }

            var leader = battle.LeaderPlayer;
            ushort bgId = (leader?.CurMap != null && leader.CurMap.MapID < 10000) ? (ushort)leader.CurMap.MapID : (ushort)1;

            // Initialize combat state for each participating player
            foreach (var p in battle.AllPlayers)
            {
                bool isAttacker = battle.AttackingPlayers.Contains(p);
                var friendlyFighters = isAttacker ? battle.Attackers : battle.Defenders;
                var friendlyPlayers = friendlyFighters.Where(f => f.FighterType == BattleFighterType.Player).ToList();
                var friendlyPets = friendlyFighters.Where(f => f.FighterType == BattleFighterType.Pet).ToList();
                var enemyFighters = isAttacker ? battle.Defenders : battle.Attackers;

                // 1. AC 20:12 (battle mode enter)
                p.Send(Tools.FromFormat("bb", 20, 12));

                // 2. AC 6:2 [01] (mode change signal)
                p.Send(Tools.FromFormat("bbb", 6, 2, 1));

                // 3. AC 11:250 (Prepare Battle: Pack ONLY self player)
                var selfFighter = friendlyPlayers.FirstOrDefault(f => f.PlayerRef == p);
                if (selfFighter == null) continue; // should not happen

                SendPacket p250 = new SendPacket();
                p250.PackArray(new byte[] { 11, 250 });
                p250.Pack16(bgId);
                p250.Pack8((byte)selfFighter.Side);
                p250.Pack8(2); // ftype = 2 (player)
                p250.Pack32(selfFighter.ID);
                p250.Pack16(0); // click_id
                p250.Pack32(0); // owner_id
                p250.Pack8(selfFighter.GridX);
                p250.Pack8(selfFighter.GridY);
                p250.Pack32((uint)selfFighter.MaxHP);
                p250.Pack16((ushort)Math.Min(0xFFFF, selfFighter.MaxSP));
                p250.Pack32((uint)selfFighter.CurHP);
                p250.Pack16((ushort)Math.Min(0xFFFF, selfFighter.CurSP));
                p250.Pack8(selfFighter.Level);
                p250.Pack8(selfFighter.Element);
                p250.Pack8(0); // reborn
                p250.Pack8(0); // job
                p250.Pack16(0); // trailing pad
                p.Send(p250);

                // 4. AC 11:10 [01] (combat start signal)
                p.Send(Tools.FromFormat("bbb", 11, 10, 1));

                // 5. AC 11:5 Spawn other friendly players (party members)
                foreach (var pf in friendlyPlayers)
                {
                    if (pf.PlayerRef == p) continue; // skip self, already in AC 11:250

                    // Send teammate stats so top portrait HUD shows teammate HP/SP
                    if (pf.PlayerRef != null)
                    {
                        Player.SendTeammateStats(p, pf.PlayerRef);
                    }

                    SendPacket pAlly = new SendPacket();
                    pAlly.PackArray(new byte[] { 11, 5 });
                    pAlly.Pack8((byte)pf.Side);
                    pAlly.Pack8(2); // ftype = 2 (player)
                    pAlly.Pack32(pf.ID);
                    pAlly.Pack16(0); // click_id
                    pAlly.Pack32(0); // owner_id
                    pAlly.Pack8(pf.GridX);
                    pAlly.Pack8(pf.GridY);
                    pAlly.Pack32((uint)pf.MaxHP);
                    pAlly.Pack16((ushort)Math.Min(0xFFFF, pf.MaxSP));
                    pAlly.Pack32((uint)pf.CurHP);
                    pAlly.Pack16((ushort)Math.Min(0xFFFF, pf.CurSP));
                    pAlly.Pack8(pf.Level);
                    pAlly.Pack8(pf.Element);
                    pAlly.Pack8(0); // reborn
                    pAlly.Pack8(0); // job
                    pAlly.Pack16(0); // trailing pad
                    p.Send(pAlly);
                }

                // 6. AC 11:5 Spawn friendly companion pets
                foreach (var pet in friendlyPets)
                {
                    if (pet.OwnerID == p.CharID)
                    {
                        QuestRelated.QuestManager.SendPetSkills(p, pet.ID, pet.PetRef?.Slot ?? 1);
                    }

                    byte petElem = (pet.ID == 12032 || pet.ID == 12178) ? (byte)1 : (byte)0;

                    SendPacket pPet = new SendPacket();
                    pPet.PackArray(new byte[] { 11, 5 });
                    pPet.Pack8((byte)pet.Side);
                    pPet.Pack8(4); // ftype = 4 (pet)
                    pPet.Pack32(pet.ID);
                    pPet.Pack16(0); // click_id
                    pPet.Pack32(pet.OwnerID);
                    pPet.Pack8(pet.GridX);
                    pPet.Pack8(pet.GridY);
                    pPet.Pack32((uint)pet.MaxHP);
                    pPet.Pack16((ushort)Math.Min(0xFFFF, pet.MaxSP));
                    pPet.Pack32((uint)pet.CurHP);
                    pPet.Pack16((ushort)Math.Min(0xFFFF, pet.CurSP));
                    pPet.Pack8(pet.Level);
                    pPet.Pack8(petElem); // element
                    pPet.Pack8(0); // reborn
                    pPet.Pack8(0); // job
                    pPet.Pack16(0); // trailing pad
                    p.Send(pPet);
                }

                // 7. AC 11:5 Spawn all enemy entities (Monsters or Opposing Players/Pets in PvP)
                foreach (var ef in enemyFighters)
                {
                    SendPacket pEnemy = new SendPacket();
                    pEnemy.PackArray(new byte[] { 11, 5 });
                    pEnemy.Pack8((byte)ef.Side);
                    pEnemy.Pack8((byte)ef.FighterType);
                    pEnemy.Pack32(ef.ID);
                    pEnemy.Pack16(ef.ClickID);
                    pEnemy.Pack32(ef.OwnerID);
                    pEnemy.Pack8(ef.GridX);
                    pEnemy.Pack8(ef.GridY);
                    pEnemy.Pack32((uint)ef.MaxHP);
                    pEnemy.Pack16((ushort)Math.Min(0xFFFF, ef.MaxSP));
                    pEnemy.Pack32((uint)ef.CurHP);
                    pEnemy.Pack16((ushort)Math.Min(0xFFFF, ef.CurSP));
                    pEnemy.Pack8(ef.Level);
                    pEnemy.Pack8(ef.Element);
                    pEnemy.Pack8(0); // reborn
                    pEnemy.Pack8(0); // job
                    pEnemy.Pack16(0); // trailing pad
                    p.Send(pEnemy);
                }

                // 8. AC 51:1 Sync HP/SP for all entities in the entire battle
                foreach (var f in battle.Attackers.Concat(battle.Defenders))
                {
                    SendStatSync(p, f.GridX, f.GridY, 0x19, (uint)f.CurHP);
                    SendStatSync(p, f.GridX, f.GridY, 0x1a, (uint)f.CurSP);
                }

                // 9. AC 50:6 & AC 52:1 Start Round & Open Action UI for this player
                var playerFighter = friendlyPlayers.FirstOrDefault(x => x.PlayerRef == p);
                if (playerFighter != null)
                {
                    DebugSystem.Write($"[PvEBattle] Sending AC 50:6 + AC 52:1 to {p.CharName} at grid ({playerFighter.GridX},{playerFighter.GridY})");
                    p.Send(Tools.FromFormat("bbbbb", 50, 6, playerFighter.GridX, playerFighter.GridY, 0));
                    p.Send(Tools.FromFormat("bb", 52, 1));
                }
                else
                {
                    DebugSystem.Write($"[PvEBattle] WARNING: No playerFighter found for {p.CharName} in friendlyPlayers (count: {friendlyPlayers.Count})");
                }
            }

            // Start turn timer: if not all players respond in 30s, auto-defend for missing ones
            battle.StartTurnTimer(OnTurnTimeout);
        }

        private static void OnTurnTimeout(ActiveBattle battle)
        {
            if (battle == null || battle.IsFinished || battle.IsTurnProcessing) return;

            // Fill missing actions with "defend" for all living friendly fighters
            foreach (var f in battle.Attackers.Concat(battle.Defenders))
            {
                if (!f.IsDead && (f.FighterType == BattleFighterType.Player || f.FighterType == BattleFighterType.Pet))
                {
                    int key = (f.GridX << 8) | f.GridY;
                    if (!battle.PendingActions.ContainsKey(key))
                    {
                        battle.PendingActions[key] = new PendingAction
                        {
                            Actor = f,
                            Player = f.PlayerRef ?? battle.AllPlayers.FirstOrDefault(p => p.CharID == f.OwnerID),
                            ActionType = "defend",
                            SkillId = 60021,
                            TargetGridX = 0,
                            TargetGridY = 0
                        };
                        DebugSystem.Write($"[PvEBattle] Turn timeout: Auto-defend for ({f.GridX},{f.GridY}) [{f.Name}]");
                    }
                }
            }

            TryExecuteTurn(battle);
        }

        public static void HandleBattleAction(Player player, byte sub, RecievePacket r)
        {
            ActiveBattle battle = GetBattle(player);
            if (battle == null || battle.IsFinished) return;

            ushort skillId = 10001; // Basic Attack
            byte srcX = 0, srcY = 0, targetX = 0, targetY = 0;
            try
            {
                if ((r.Count - r.GetPtr()) >= 4)
                {
                    srcX = r.Unpack8();
                    srcY = r.Unpack8();
                    targetX = r.Unpack8();
                    targetY = r.Unpack8();
                }
                if ((r.Count - r.GetPtr()) >= 2)
                {
                    ushort unpackedSkill = r.Unpack16();
                    if (unpackedSkill > 0) skillId = unpackedSkill;
                }
            }
            catch { }

            if (battle.IsTurnProcessing) return;

            var allFriendly = battle.Attackers.Concat(battle.Defenders);
            var actingFighter = allFriendly.FirstOrDefault(f => f.GridX == srcX && f.GridY == srcY && (f.PlayerRef == player || f.OwnerID == player.CharID));

            // If the fighter at (srcX,srcY) has already submitted an action this round, or was not found, resolve to the player's next un-acted living fighter (e.g. Companion Pet)
            if (actingFighter == null || battle.PendingActions.ContainsKey((actingFighter.GridX << 8) | actingFighter.GridY))
            {
                actingFighter = allFriendly.FirstOrDefault(f => !f.IsDead && (f.PlayerRef == player || f.OwnerID == player.CharID) && !battle.PendingActions.ContainsKey((f.GridX << 8) | f.GridY));
                if (actingFighter != null)
                {
                    srcX = actingFighter.GridX;
                    srcY = actingFighter.GridY;
                }
            }

            if (actingFighter == null || actingFighter.IsDead) return;

            int actionKey = (srcX << 8) | srcY;
            if (battle.PendingActions.ContainsKey(actionKey)) return;

            // Determine action type from skill
            var skill = SkillRelated.SkillManager.GetSkill(skillId);
            string actionType = "attack";
            if (sub == 5 || skillId == 60041)
            {
                actionType = "flee";
                skillId = 60041;
            }
            else if (sub == 4 || skillId == 60021)
            {
                actionType = "defend";
                skillId = 60021;
            }
            else if (skillId == 10008)
            {
                actionType = "catch";
            }
            else if (skill != null)
            {
                if (skill.IsHeal || skill.IsRevive)
                {
                    actionType = "heal";
                }
                else if (skill.IsShield || skill.IsHotBlooded || skill.IsSpeedUp || skill.IsVanish)
                {
                    actionType = "buff";
                }
                else if (skill.IsFreeze || skill.IsSleep || skill.IsSeal || skill.IsConfuse || skill.IsPoison || skill.IsParalyze)
                {
                    actionType = "status";
                }
            }

            battle.PendingActions[actionKey] = new PendingAction
            {
                Actor = actingFighter,
                Player = player,
                ActionType = actionType,
                SkillId = skillId,
                TargetGridX = targetX,
                TargetGridY = targetY
            };

            DebugSystem.Write($"[PvEBattle] Collected action for ({srcX},{srcY}) [{actingFighter.Name}]: {actionType} (Skill:{skillId} '{(skill?.Name ?? "Skill")}') [{battle.PendingActions.Count}/{battle.ExpectedActionCount}]");

            // AC 53:5 Acknowledge action to all players
            BroadcastToBattle(battle, Tools.FromFormat("bbbb", 53, 5, srcX, srcY));

            // If this player has another living fighter (e.g. Pet or Character) that hasn't acted yet, send AC 50:6 to open their action menu!
            var remainingFightersForPlayer = allFriendly.Where(f => !f.IsDead && (f.PlayerRef == player || f.OwnerID == player.CharID) && !battle.PendingActions.ContainsKey((f.GridX << 8) | f.GridY)).ToList();
            if (remainingFightersForPlayer.Count > 0)
            {
                var nextFighter = remainingFightersForPlayer.First();
                player.Send(Tools.FromFormat("bbbbb", 50, 6, nextFighter.GridX, nextFighter.GridY, 0));
                player.Send(Tools.FromFormat("bb", 52, 1));
                DebugSystem.Write($"[PvEBattle] Prompting next action (AC 50:6) for {player.CharName}'s {nextFighter.Name} at ({nextFighter.GridX},{nextFighter.GridY})");
            }

            TryExecuteTurn(battle);
        }

        private static void TryExecuteTurn(ActiveBattle battle)
        {
            if (battle == null || battle.IsFinished) return;
            if (battle.PendingActions.Count < battle.ExpectedActionCount) return;
            if (battle.IsTurnProcessing) return;

            battle.IsTurnProcessing = true;
            battle.CancelTurnTimer();
            ExecuteTurn(battle);
        }

        private static void ExecuteTurn(ActiveBattle battle)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (battle == null || battle.IsFinished) return;

                    battle.Turn++;

                    var actions = new List<PendingAction>(battle.PendingActions.Values);
                    battle.PendingActions.Clear();

                    var friendlyFighters = battle.Attackers; // Player side
                    var opposingFighters = battle.Defenders; // Monster side

                    // ---- Phase 0: Poison Damage & Status Check at Start of Round ----
                    var allFighters = friendlyFighters.Concat(opposingFighters).Where(f => !f.IsDead).ToList();
                    foreach (var f in allFighters)
                    {
                        if (f.HasStatus(FighterStatusType.Poisoned))
                        {
                            int poisonDmg = Math.Max(5, (int)(f.MaxHP * 0.08));
                            f.CurHP = Math.Max(0, f.CurHP - poisonDmg);
                            if (f.MonsterRef != null) f.MonsterRef.MonsterHP = f.CurHP;
                            if (f.PlayerRef?.Eqs != null) f.PlayerRef.Eqs.CurHP = f.CurHP;
                            if (f.PetRef != null) f.PetRef.HP = f.CurHP;

                            foreach (var p in battle.AllPlayers)
                            {
                                SendStatSync(p, f.GridX, f.GridY, 0x19, (uint)f.CurHP);
                            }

                            if (f.IsDead)
                            {
                                BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 11, 1, f.GridX, f.GridY, 0));
                            }
                        }
                    }

                    // Set of fighters that chose Defend this round
                    var defendingActors = new HashSet<int>();

                    // ---- Phase 0.5: Flee / Escape Actions ----
                    var fleeActions = actions.Where(a => a.ActionType == "flee").ToList();
                    if (fleeActions.Count > 0)
                    {
                        foreach (var fa in fleeActions)
                        {
                            var actor = fa.Actor;
                            if (actor == null || actor.IsDead) continue;

                            BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 50, 6, actor.GridX, actor.GridY, 0));

                            SendPacket pAnim = new SendPacket();
                            pAnim.PackArray(new byte[] { 50, 1 });
                            pAnim.PackArray(new byte[] { 0x11, 0x00 });
                            pAnim.Pack8(actor.GridX); pAnim.Pack8(actor.GridY); // actor
                            pAnim.Pack16(60041); // Flee skill
                            pAnim.Pack8(0); pAnim.Pack8(1);
                            pAnim.Pack8(actor.GridX); pAnim.Pack8(actor.GridY); // target
                            pAnim.Pack8(1); pAnim.Pack8(0); pAnim.Pack8(1);
                            pAnim.Pack8(0); // stat_id = 0
                            pAnim.Pack32(0); // 0 dmg
                            pAnim.Pack8(1);
                            BroadcastToBattle(battle, pAnim);

                            await Task.Delay(600);
                        }

                        // If any attacking player or pet chose to flee, exit battle
                        var fledLeader = fleeActions.Any(a => a.Actor != null && (a.Actor.FighterType == BattleFighterType.Player || a.Actor.FighterType == BattleFighterType.Pet));
                        if (fledLeader)
                        {
                            EndBattleFlee(battle);
                            return;
                        }
                    }

                    // ---- Phase 1: Defend Actions ----
                    var defendActions = actions.Where(a => a.ActionType == "defend").ToList();
                    foreach (var da in defendActions)
                    {
                        var actor = da.Actor;
                        if (actor == null || actor.IsDead || !actor.CanAct) continue;
                        defendingActors.Add((actor.GridX << 8) | actor.GridY);
                        actor.AddStatus(FighterStatusType.Shielded, 1);

                        BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 50, 6, actor.GridX, actor.GridY, 0));

                        SendPacket pAnim = new SendPacket();
                        pAnim.PackArray(new byte[] { 50, 1 });
                        pAnim.PackArray(new byte[] { 0x11, 0x00 });
                        pAnim.Pack8(actor.GridX); pAnim.Pack8(actor.GridY);
                        pAnim.Pack16(60021);
                        pAnim.Pack8(0); pAnim.Pack8(1);
                        pAnim.Pack8(actor.GridX); pAnim.Pack8(actor.GridY);
                        pAnim.Pack8(1); pAnim.Pack8(0); pAnim.Pack8(1);
                        pAnim.Pack8(0);
                        pAnim.Pack32(0);
                        pAnim.Pack8(1);
                        BroadcastToBattle(battle, pAnim);

                        await Task.Delay(400);
                    }

                    // ---- Phase 2: Support, Healing & Buff Actions ----
                    var supportActions = actions.Where(a => a.ActionType == "heal" || a.ActionType == "buff").ToList();
                    foreach (var sa in supportActions)
                    {
                        var actor = sa.Actor;
                        if (actor == null || actor.IsDead || !actor.CanAct) continue;

                        var sk = SkillRelated.SkillManager.GetSkill(sa.SkillId);
                        ushort spCost = sk?.SP ?? 15;

                        // Deduct SP
                        if (actor.PlayerRef?.Eqs != null)
                        {
                            actor.PlayerRef.Eqs.CurSP = Math.Max(0, actor.PlayerRef.Eqs.CurSP - spCost);
                            actor.CurSP = actor.PlayerRef.Eqs.CurSP;
                            foreach (var p in battle.AllPlayers)
                                SendStatSync(p, actor.GridX, actor.GridY, 0x1a, (uint)actor.CurSP);
                        }
                        else if (actor.PetRef != null)
                        {
                            actor.PetRef.SP = Math.Max(0, actor.PetRef.SP - spCost);
                            actor.CurSP = actor.PetRef.SP;
                            foreach (var p in battle.AllPlayers)
                                SendStatSync(p, actor.GridX, actor.GridY, 0x1a, (uint)actor.CurSP);
                        }

                        // Target ally
                        var targetAlly = friendlyFighters.FirstOrDefault(f => f.GridX == sa.TargetGridX && f.GridY == sa.TargetGridY)
                                      ?? friendlyFighters.FirstOrDefault(f => !f.IsDead) ?? actor;

                        // Award skill proficiency EXP to player
                        if (actor.PlayerRef != null && sa.SkillId > 0)
                        {
                            SkillRelated.SkillManager.AddSkillExp(actor.PlayerRef, sa.SkillId, 1);
                        }

                        BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 50, 6, actor.GridX, actor.GridY, 0));

                        if (sk != null && sk.IsRevive && targetAlly.IsDead)
                        {
                            int reviveHp = Math.Max(50, targetAlly.MaxHP / 3);
                            targetAlly.CurHP = reviveHp;
                            if (targetAlly.PlayerRef?.Eqs != null) targetAlly.PlayerRef.Eqs.CurHP = reviveHp;
                            if (targetAlly.PetRef != null) targetAlly.PetRef.HP = reviveHp;

                            SendPacket pRevive = new SendPacket();
                            pRevive.PackArray(new byte[] { 50, 1 });
                            pRevive.PackArray(new byte[] { 0x11, 0x00 });
                            pRevive.Pack8(actor.GridX); pRevive.Pack8(actor.GridY);
                            pRevive.Pack16(sa.SkillId);
                            pRevive.Pack8(0); pRevive.Pack8(1);
                            pRevive.Pack8(targetAlly.GridX); pRevive.Pack8(targetAlly.GridY);
                            pRevive.Pack8(1); pRevive.Pack8(0); pRevive.Pack8(1);
                            pRevive.Pack8(0x19);
                            pRevive.Pack32((uint)reviveHp);
                            pRevive.Pack8(1);
                            BroadcastToBattle(battle, pRevive);

                            foreach (var p in battle.AllPlayers)
                                SendStatSync(p, targetAlly.GridX, targetAlly.GridY, 0x19, (uint)targetAlly.CurHP);
                        }
                        else if (sk != null && sk.IsHeal)
                        {
                            int healAmt = Math.Max(40, (int)(actor.Atk * 2.2) + (actor.Level * 15));
                            targetAlly.CurHP = Math.Min(targetAlly.MaxHP, targetAlly.CurHP + healAmt);
                            if (targetAlly.PlayerRef?.Eqs != null) targetAlly.PlayerRef.Eqs.CurHP = targetAlly.CurHP;
                            if (targetAlly.PetRef != null) targetAlly.PetRef.HP = targetAlly.CurHP;

                            SendPacket pHeal = new SendPacket();
                            pHeal.PackArray(new byte[] { 50, 1 });
                            pHeal.PackArray(new byte[] { 0x11, 0x00 });
                            pHeal.Pack8(actor.GridX); pHeal.Pack8(actor.GridY);
                            pHeal.Pack16(sa.SkillId);
                            pHeal.Pack8(0); pHeal.Pack8(1);
                            pHeal.Pack8(targetAlly.GridX); pHeal.Pack8(targetAlly.GridY);
                            pHeal.Pack8(1); pHeal.Pack8(0); pHeal.Pack8(1);
                            pHeal.Pack8(0x19);
                            pHeal.Pack32((uint)healAmt);
                            pHeal.Pack8(1);
                            BroadcastToBattle(battle, pHeal);

                            foreach (var p in battle.AllPlayers)
                                SendStatSync(p, targetAlly.GridX, targetAlly.GridY, 0x19, (uint)targetAlly.CurHP);
                        }
                        else if (sk != null && sk.IsShield)
                        {
                            targetAlly.AddStatus(FighterStatusType.Shielded, sk.NumberOfTurns > 0 ? sk.NumberOfTurns : 3);

                            SendPacket pShield = new SendPacket();
                            pShield.PackArray(new byte[] { 50, 1 });
                            pShield.PackArray(new byte[] { 0x11, 0x00 });
                            pShield.Pack8(actor.GridX); pShield.Pack8(actor.GridY);
                            pShield.Pack16(sa.SkillId);
                            pShield.Pack8(0); pShield.Pack8(1);
                            pShield.Pack8(targetAlly.GridX); pShield.Pack8(targetAlly.GridY);
                            pShield.Pack8(1); pShield.Pack8(0); pShield.Pack8(1);
                            pShield.Pack8(0);
                            pShield.Pack32(0);
                            pShield.Pack8(1);
                            BroadcastToBattle(battle, pShield);
                        }
                        else if (sk != null && sk.IsHotBlooded)
                        {
                            targetAlly.AddStatus(FighterStatusType.HotBlooded, sk.NumberOfTurns > 0 ? sk.NumberOfTurns : 3);

                            SendPacket pHot = new SendPacket();
                            pHot.PackArray(new byte[] { 50, 1 });
                            pHot.PackArray(new byte[] { 0x11, 0x00 });
                            pHot.Pack8(actor.GridX); pHot.Pack8(actor.GridY);
                            pHot.Pack16(sa.SkillId);
                            pHot.Pack8(0); pHot.Pack8(1);
                            pHot.Pack8(targetAlly.GridX); pHot.Pack8(targetAlly.GridY);
                            pHot.Pack8(1); pHot.Pack8(0); pHot.Pack8(1);
                            pHot.Pack8(0);
                            pHot.Pack32(0);
                            pHot.Pack8(1);
                            BroadcastToBattle(battle, pHot);
                        }
                        else if (sk != null && sk.IsSpeedUp)
                        {
                            targetAlly.AddStatus(FighterStatusType.SpeedUp, sk.NumberOfTurns > 0 ? sk.NumberOfTurns : 3);
                            targetAlly.Spd += 30;

                            SendPacket pSpd = new SendPacket();
                            pSpd.PackArray(new byte[] { 50, 1 });
                            pSpd.PackArray(new byte[] { 0x11, 0x00 });
                            pSpd.Pack8(actor.GridX); pSpd.Pack8(actor.GridY);
                            pSpd.Pack16(sa.SkillId);
                            pSpd.Pack8(0); pSpd.Pack8(1);
                            pSpd.Pack8(targetAlly.GridX); pSpd.Pack8(targetAlly.GridY);
                            pSpd.Pack8(1); pSpd.Pack8(0); pSpd.Pack8(1);
                            pSpd.Pack8(0);
                            pSpd.Pack32(0);
                            pSpd.Pack8(1);
                            BroadcastToBattle(battle, pSpd);
                        }
                        else
                        {
                            // General support
                            SendPacket pGen = new SendPacket();
                            pGen.PackArray(new byte[] { 50, 1 });
                            pGen.PackArray(new byte[] { 0x11, 0x00 });
                            pGen.Pack8(actor.GridX); pGen.Pack8(actor.GridY);
                            pGen.Pack16(sa.SkillId);
                            pGen.Pack8(0); pGen.Pack8(1);
                            pGen.Pack8(targetAlly.GridX); pGen.Pack8(targetAlly.GridY);
                            pGen.Pack8(1); pGen.Pack8(0); pGen.Pack8(1);
                            pGen.Pack8(0);
                            pGen.Pack32(0);
                            pGen.Pack8(1);
                            BroadcastToBattle(battle, pGen);
                        }

                        await Task.Delay(1200);
                    }

                    // ---- Phase 3: Catch Actions ----
                    var catchActions = actions.Where(a => a.ActionType == "catch").ToList();
                    foreach (var ca in catchActions)
                    {
                        var actor = ca.Actor;
                        if (actor == null || actor.IsDead || !actor.CanAct) continue;

                        var targetFighter = opposingFighters.FirstOrDefault(f => !f.IsDead && f.GridX == ca.TargetGridX && f.GridY == ca.TargetGridY)
                                         ?? opposingFighters.FirstOrDefault(f => !f.IsDead);
                        if (targetFighter == null) break;

                        if (!battle.IsPvP && targetFighter.MonsterRef != null)
                        {
                            var targetMonster = targetFighter.MonsterRef;
                            BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 50, 6, actor.GridX, actor.GridY, 0));

                            int playerLvl = ca.Player?.Eqs?.Level ?? 1;
                            int monsterLvl = targetMonster.MonsterLevel;
                            double hpPercent = (double)targetMonster.MonsterHP / Math.Max(1, targetMonster.MonsterMaxHP);

                            double catchChance = 75.0 + ((playerLvl - monsterLvl) * 5.0) + ((1.0 - hpPercent) * 20.0);
                            if (playerLvl >= monsterLvl) catchChance = Math.Max(85.0, catchChance);
                            catchChance = Math.Min(98.0, Math.Max(15.0, catchChance));

                            bool catchSuccess = (_rng.NextDouble() * 100.0) <= catchChance;

                            SendPacket pAnim = new SendPacket();
                            pAnim.PackArray(new byte[] { 50, 1 });
                            pAnim.PackArray(new byte[] { 0x11, 0x00 });
                            pAnim.Pack8(actor.GridX); pAnim.Pack8(actor.GridY);
                            pAnim.Pack16(10008);
                            pAnim.Pack8(0); pAnim.Pack8(1);
                            pAnim.Pack8(targetFighter.GridX); pAnim.Pack8(targetFighter.GridY);
                            pAnim.Pack8(1); pAnim.Pack8(0); pAnim.Pack8(1);
                            pAnim.Pack8(0);
                            pAnim.Pack32(0);
                            pAnim.Pack8(1);
                            BroadcastToBattle(battle, pAnim);

                            if (catchSuccess)
                            {
                                targetMonster.MonsterHP = 0;
                                targetFighter.CurHP = 0;
                                targetMonster.IsCaptured = true;
                                targetFighter.IsCaptured = true;

                                byte petSlot = 1;
                                if (ca.Player?.PlayerPets != null)
                                {
                                    while (ca.Player.PlayerPets.ContainsKey(petSlot) && petSlot <= 4) petSlot++;
                                    if (petSlot <= 4)
                                    {
                                        ca.Player.PlayerPets[petSlot] = new Player.PlayerPetData
                                        {
                                            Slot = petSlot,
                                            PetID = (uint)targetMonster.MonsterId,
                                            PetName = targetMonster.MonsterName,
                                            Level = (byte)targetMonster.MonsterLevel,
                                            HP = targetMonster.MonsterMaxHP,
                                            MaxHP = targetMonster.MonsterMaxHP,
                                            SP = targetMonster.MonsterMaxSP,
                                            MaxSP = targetMonster.MonsterMaxSP,
                                            Amity = 60,
                                            IsBattle = false,
                                            IsRide = false
                                        };

                                        SendPacket petPkt = new SendPacket();
                                        petPkt.PackArray(new byte[] { 15, 1 });
                                        petPkt.Pack32(ca.Player.CharID);
                                        petPkt.Pack32((uint)targetMonster.MonsterId);
                                        petPkt.Pack8(petSlot);
                                        petPkt.Pack16((ushort)targetMonster.MonsterAtk);
                                        petPkt.Pack16((ushort)targetMonster.MonsterDef);
                                        petPkt.Pack16(5); // INT
                                        petPkt.Pack16(5); // WIS
                                        petPkt.Pack16((ushort)targetMonster.MonsterSpd);
                                        petPkt.Pack8((byte)targetMonster.MonsterElement);
                                        petPkt.Pack32((uint)targetMonster.MonsterLevel);
                                        petPkt.Pack32((uint)targetMonster.MonsterMaxHP);
                                        petPkt.Pack32((uint)targetMonster.MonsterMaxHP);
                                        for (int i = 0; i < 7; i++) petPkt.Pack8(0);
                                        petPkt.Pack8(60);
                                        for (int i = 0; i < 13; i++) petPkt.Pack8(0);
                                        ca.Player.Send(petPkt);

                                        ca.Player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Successfully captured {targetMonster.MonsterName} into Pet Slot #{petSlot}!"));
                                    }
                                }

                                BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 11, 1, targetFighter.GridX, targetFighter.GridY, 0));
                            }
                            else
                            {
                                ca.Player?.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Failed to capture {targetMonster.MonsterName}!"));
                            }

                            await Task.Delay(800);
                        }
                    }

                    // ---- Phase 4: Attack, Debuff & Status Actions (Targeting Enemies) ----
                    var offensiveActions = actions.Where(a => a.ActionType == "attack" || a.ActionType == "status").ToList();

                    // Group attacks targeting the same enemy coordinate
                    var attackGroups = offensiveActions.GroupBy(a => (a.TargetGridX << 8) | a.TargetGridY).ToList();

                    foreach (var group in attackGroups)
                    {
                        if (battle.IsFinished) break;

                        byte tGridX = (byte)(group.Key >> 8);
                        byte tGridY = (byte)(group.Key & 0xFF);

                        var targetFighter = opposingFighters.FirstOrDefault(f => !f.IsDead && f.GridX == tGridX && f.GridY == tGridY)
                                         ?? opposingFighters.FirstOrDefault(f => !f.IsDead);
                        if (targetFighter == null)
                        {
                            if (opposingFighters.All(f => f.IsDead))
                            {
                                EndBattleVictory(battle);
                                return;
                            }
                            break;
                        }

                        var validAttackers = group.Where(a => a.Actor != null && !a.Actor.IsDead && a.Actor.CanAct).ToList();
                        if (validAttackers.Count == 0) continue;

                        // Execute in pairs of max 2 attackers (Dual Combo)
                        for (int i = 0; i < validAttackers.Count; i += 2)
                        {
                            if (targetFighter.IsDead)
                            {
                                targetFighter = opposingFighters.FirstOrDefault(f => !f.IsDead);
                                if (targetFighter == null) break;
                            }

                            var comboPair = validAttackers.Skip(i).Take(2).ToList();
                            bool isCombo = comboPair.Count > 1;

                            foreach (var a in comboPair)
                            {
                                BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 50, 6, a.Actor.GridX, a.Actor.GridY, 0));
                            }

                            SendPacket pAttackAnim = new SendPacket();
                            pAttackAnim.PackArray(new byte[] { 50, 1 });

                            int pairDmg = 0;
                            foreach (var a in comboPair)
                            {
                                var actor = a.Actor;
                                var sk = SkillRelated.SkillManager.GetSkill(a.SkillId);

                                int baseDmg = Math.Max(10, (actor.Atk * 2) - targetFighter.Def + QuestNpc.NextRandom(1, 6));

                                if (a.SkillId > 10001)
                                {
                                    ushort spCost = sk?.SP ?? 15;
                                    baseDmg = Math.Max(15, (int)(actor.Atk * 2.8) - (targetFighter.Def / 2));
                                    if (actor.PlayerRef?.Eqs != null)
                                    {
                                        actor.PlayerRef.Eqs.CurSP = Math.Max(0, actor.PlayerRef.Eqs.CurSP - spCost);
                                        actor.CurSP = actor.PlayerRef.Eqs.CurSP;
                                        foreach (var p in battle.AllPlayers)
                                            SendStatSync(p, actor.GridX, actor.GridY, 0x1a, (uint)actor.CurSP);
                                    }
                                    else if (actor.PetRef != null)
                                    {
                                        actor.PetRef.SP = Math.Max(0, actor.PetRef.SP - spCost);
                                        actor.CurSP = actor.PetRef.SP;
                                        foreach (var p in battle.AllPlayers)
                                            SendStatSync(p, actor.GridX, actor.GridY, 0x1a, (uint)actor.CurSP);
                                    }
                                }

                                // Apply HotBlooded 2x ATK buff
                                if (actor.HasStatus(FighterStatusType.HotBlooded))
                                {
                                    baseDmg = (int)(baseDmg * 2.0);
                                }

                                // Combo bonus
                                int finalDmg = isCombo ? (int)(baseDmg * 1.25) : baseDmg;

                                // Target Shielded reduction
                                if (targetFighter.HasStatus(FighterStatusType.Shielded))
                                {
                                    finalDmg = Math.Max(1, finalDmg / 2);
                                }

                                pairDmg += finalDmg;

                                // Apply status effects to target
                                if (sk != null)
                                {
                                    int turns = sk.NumberOfTurns > 0 ? sk.NumberOfTurns : 2;
                                    if (sk.IsFreeze) targetFighter.AddStatus(FighterStatusType.Frozen, turns);
                                    else if (sk.IsSleep) targetFighter.AddStatus(FighterStatusType.Sleep, turns);
                                    else if (sk.IsSeal) targetFighter.AddStatus(FighterStatusType.Sealed, turns);
                                    else if (sk.IsConfuse) targetFighter.AddStatus(FighterStatusType.Confused, turns);
                                    else if (sk.IsPoison) targetFighter.AddStatus(FighterStatusType.Poisoned, turns + 1);
                                    else if (sk.IsParalyze) targetFighter.AddStatus(FighterStatusType.Paralyzed, turns);
                                }

                                // Award skill proficiency EXP to player
                                if (actor.PlayerRef != null && a.SkillId > 0)
                                {
                                    SkillRelated.SkillManager.AddSkillExp(actor.PlayerRef, a.SkillId, 1);
                                }

                                pAttackAnim.PackArray(new byte[] { 0x11, 0x00 });
                                pAttackAnim.Pack8(actor.GridX); pAttackAnim.Pack8(actor.GridY);
                                pAttackAnim.Pack16(a.SkillId > 0 ? a.SkillId : (ushort)10001);
                                pAttackAnim.Pack8(0); pAttackAnim.Pack8(1);
                                pAttackAnim.Pack8(targetFighter.GridX); pAttackAnim.Pack8(targetFighter.GridY);
                                pAttackAnim.Pack8(1); pAttackAnim.Pack8(0); pAttackAnim.Pack8(1);
                                pAttackAnim.Pack8(0x19); // HP damage
                                pAttackAnim.Pack32((uint)finalDmg);
                                pAttackAnim.Pack8(1);
                            }

                            BroadcastToBattle(battle, pAttackAnim);

                            targetFighter.CurHP = Math.Max(0, targetFighter.CurHP - pairDmg);
                            if (targetFighter.MonsterRef != null) targetFighter.MonsterRef.MonsterHP = targetFighter.CurHP;
                            if (targetFighter.PlayerRef?.Eqs != null) targetFighter.PlayerRef.Eqs.CurHP = targetFighter.CurHP;
                            if (targetFighter.PetRef != null) targetFighter.PetRef.HP = targetFighter.CurHP;

                            // Waking up target if sleeping
                            if (pairDmg > 0 && targetFighter.HasStatus(FighterStatusType.Sleep))
                            {
                                targetFighter.ActiveStatuses.RemoveAll(s => s.StatusType == FighterStatusType.Sleep);
                            }

                            foreach (var p in battle.AllPlayers)
                            {
                                SendStatSync(p, targetFighter.GridX, targetFighter.GridY, 0x19, (uint)targetFighter.CurHP);
                            }

                            if (targetFighter.IsDead)
                            {
                                BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 11, 1, targetFighter.GridX, targetFighter.GridY, 0));
                            }

                            await Task.Delay(isCombo ? 1500 : 1200);
                        }
                    }

                    if (battle.IsFinished) return;

                    // Check if all opposing fighters defeated
                    if (opposingFighters.All(f => f.IsDead))
                    {
                        EndBattleVictory(battle);
                        return;
                    }

                    // ---- Phase 5: Enemy Monster Retaliation (PvE) ----
                    if (!battle.IsPvP)
                    {
                        var livingMonsters = opposingFighters.Where(f => !f.IsDead).ToList();
                        foreach (var monster in livingMonsters)
                        {
                            if (battle.IsFinished) break;

                            // Check if monster is incapacitated
                            if (!monster.CanAct)
                            {
                                DebugSystem.Write($"[PvEBattle] Monster {monster.Name} is incapacitated (Freeze/Sleep/Seal/Stun) and skips turn.");
                                continue;
                            }

                            var livingTargets = friendlyFighters.Where(f => !f.IsDead).ToList();
                            if (livingTargets.Count == 0) break;

                            // Confused monster might hit other monsters
                            if (monster.HasStatus(FighterStatusType.Confused) && _rng.Next(0, 2) == 0 && livingMonsters.Count > 1)
                            {
                                var allyMonsters = livingMonsters.Where(m => m != monster && !m.IsDead).ToList();
                                if (allyMonsters.Count > 0)
                                {
                                    var confusedTarget = allyMonsters[_rng.Next(0, allyMonsters.Count)];
                                    int cDmg = Math.Max(5, (int)(monster.Atk * 1.2) - confusedTarget.Def);
                                    BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 50, 6, monster.GridX, monster.GridY, 0));

                                    SendPacket cmAnim = new SendPacket();
                                    cmAnim.PackArray(new byte[] { 50, 1 });
                                    cmAnim.PackArray(new byte[] { 0x11, 0x00 });
                                    cmAnim.Pack8(monster.GridX); cmAnim.Pack8(monster.GridY);
                                    cmAnim.Pack16(10001);
                                    cmAnim.Pack8(0); cmAnim.Pack8(1);
                                    cmAnim.Pack8(confusedTarget.GridX); cmAnim.Pack8(confusedTarget.GridY);
                                    cmAnim.Pack8(1); cmAnim.Pack8(0); cmAnim.Pack8(1);
                                    cmAnim.Pack8(0x19);
                                    cmAnim.Pack32((uint)cDmg);
                                    cmAnim.Pack8(1);
                                    BroadcastToBattle(battle, cmAnim);

                                    confusedTarget.CurHP = Math.Max(0, confusedTarget.CurHP - cDmg);
                                    if (confusedTarget.MonsterRef != null) confusedTarget.MonsterRef.MonsterHP = confusedTarget.CurHP;
                                    foreach (var p in battle.AllPlayers)
                                        SendStatSync(p, confusedTarget.GridX, confusedTarget.GridY, 0x19, (uint)confusedTarget.CurHP);

                                    if (confusedTarget.IsDead)
                                        BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 11, 1, confusedTarget.GridX, confusedTarget.GridY, 0));

                                    await Task.Delay(1000);
                                    continue;
                                }
                            }

                            var target = livingTargets[_rng.Next(0, livingTargets.Count)];
                            int rawDmg = Math.Max(5, (int)(monster.Atk * 1.2) - target.Def);

                            if (target.HasStatus(FighterStatusType.Shielded))
                            {
                                rawDmg = Math.Max(1, (int)(rawDmg * 0.4));
                            }

                            BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 50, 6, monster.GridX, monster.GridY, 0));

                            SendPacket mAnim = new SendPacket();
                            mAnim.PackArray(new byte[] { 50, 1 });
                            mAnim.PackArray(new byte[] { 0x11, 0x00 });
                            mAnim.Pack8(monster.GridX); mAnim.Pack8(monster.GridY);
                            mAnim.Pack16(10001);
                            mAnim.Pack8(0); mAnim.Pack8(1);
                            mAnim.Pack8(target.GridX); mAnim.Pack8(target.GridY);
                            mAnim.Pack8(1); mAnim.Pack8(0); mAnim.Pack8(1);
                            mAnim.Pack8(0x19);
                            mAnim.Pack32((uint)rawDmg);
                            mAnim.Pack8(1);
                            BroadcastToBattle(battle, mAnim);

                            target.CurHP = Math.Max(0, target.CurHP - rawDmg);
                            if (target.PlayerRef?.Eqs != null) target.PlayerRef.Eqs.CurHP = target.CurHP;
                            if (target.PetRef != null) target.PetRef.HP = target.CurHP;

                            if (rawDmg > 0 && target.HasStatus(FighterStatusType.Sleep))
                            {
                                target.ActiveStatuses.RemoveAll(s => s.StatusType == FighterStatusType.Sleep);
                            }

                            foreach (var p in battle.AllPlayers)
                            {
                                SendStatSync(p, target.GridX, target.GridY, 0x19, (uint)target.CurHP);
                            }

                            if (target.IsDead)
                            {
                                BroadcastToBattle(battle, Tools.FromFormat("bbbbb", 11, 1, target.GridX, target.GridY, 0));
                                if (target.FighterType == BattleFighterType.Pet)
                                {
                                    target.PlayerRef?.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"{target.Name} has fallen in battle!"));
                                }
                            }

                            await Task.Delay(1000);
                        }

                        if (friendlyFighters.All(f => f.IsDead))
                        {
                            EndBattleDefeat(battle);
                            return;
                        }
                    }

                    // ---- Phase 6: End of Round Status Tick ----
                    foreach (var f in friendlyFighters.Concat(opposingFighters))
                    {
                        foreach (var st in f.ActiveStatuses)
                        {
                            st.RemainingTurns--;
                        }
                        f.ActiveStatuses.RemoveAll(st => st.RemainingTurns <= 0);
                    }

                    // ---- Phase 7: Open next round for all living players ----
                    battle.IsTurnProcessing = false;

                    foreach (var p in battle.AllPlayers)
                    {
                        var pf = battle.Attackers.Concat(battle.Defenders).FirstOrDefault(f => f.PlayerRef == p && !f.IsDead);
                        if (pf == null)
                        {
                            pf = battle.Attackers.Concat(battle.Defenders).FirstOrDefault(f => f.OwnerID == p.CharID && !f.IsDead);
                        }

                        if (pf != null)
                        {
                            p.Send(Tools.FromFormat("bbbbb", 50, 6, pf.GridX, pf.GridY, 0));
                            p.Send(Tools.FromFormat("bb", 52, 1));
                        }
                    }

                    battle.StartTurnTimer(OnTurnTimeout);
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PvEBattle] Exception in ExecuteTurn: {ex.Message}");
                    battle.IsTurnProcessing = false;
                }
            });
        }

        public static void HandleFlee(Player player)
        {
            if (player == null) return;
            var battle = GetBattle(player);
            if (battle != null) EndBattleFlee(battle);
        }

        private static void EndBattleFlee(ActiveBattle battle)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (battle == null) return;

                    battle.IsFinished = true;
                    battle.CancelTurnTimer();
                    lock (_lock)
                    {
                        foreach (var p in battle.AllPlayers)
                        {
                            _activeBattles.Remove(p.CharID);
                        }
                    }

                    await Task.Delay(800);

                    // Despawn and clean all players
                    foreach (var p in battle.AllPlayers)
                    {
                        p.Send(Tools.FromFormat("bbb", 11, 12, 1));
                        p.Send(Tools.FromFormat("bbbbb", 22, 6, 11, 0, 1)); // Fled

                        SendPacket p110 = new SendPacket();
                        p110.PackArray(new byte[] { 11, 0 });
                        p110.Pack32(p.CharID);
                        p110.Pack16(0);
                        p.Send(p110);

                        foreach (var af in battle.Attackers.Concat(battle.Defenders))
                        {
                            p.Send(Tools.FromFormat("bbbbb", 11, 1, af.GridX, af.GridY, 0));
                        }

                        p.Send(Tools.FromFormat("bbb", 6, 2, 0));
                        p.Send(Tools.FromFormat("bb", 20, 8));
                        p.SaveCharacterData();
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PvEBattle] Exception in HandleFlee: {ex.Message}");
                }
            });
        }

        private static void EndBattleVictory(ActiveBattle battle)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (battle == null) return;
                    battle.IsFinished = true;
                    battle.CancelTurnTimer();

                    lock (_lock)
                    {
                        foreach (var p in battle.AllPlayers)
                        {
                            _activeBattles.Remove(p.CharID);
                        }
                    }

                    DebugSystem.Write($"[PvEBattle] Attacking team won battle! (Attackers: {battle.AttackingPlayers.Count}, Defenders: {battle.Defenders.Count})");
                    await Task.Delay(1800);

                    uint totalExp = 0;
                    uint totalGold = 0;

                    if (!battle.IsPvP)
                    {
                        foreach (var m in battle.Monsters)
                        {
                            if (m.IsCaptured) continue;
                            totalExp += (uint)Math.Max(10, m.MonsterLevel * 15);
                            totalGold += (uint)Math.Max(5, m.MonsterLevel * 8);

                            // Drops roll for leader player
                            try
                            {
                                var drops = MonsterDropManager.RollDrops(m.MonsterId, m.MonsterName ?? "Monster", m.MonsterLevel);
                                if (drops != null && drops.Count > 0 && battle.LeaderPlayer?.Inv != null)
                                {
                                    foreach (var drop in drops)
                                    {
                                        if (drop != null)
                                        {
                                            battle.LeaderPlayer.Inv.AddItem(drop.ItemID, drop.Count);
                                            battle.LeaderPlayer.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Obtained {drop.ItemName} x{drop.Count}!"));
                                        }
                                    }
                                    battle.LeaderPlayer.Send(new SendPacket(battle.LeaderPlayer.Inv.GetAC23_5()));
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        totalExp = 150;
                        totalGold = 100;
                    }

                    // Reward and cleanup Attacking team (Winners)
                    foreach (var p in battle.AttackingPlayers)
                    {
                        if (p.Eqs != null && (totalGold > 0 || totalExp > 0))
                        {
                            p.Eqs.AddGold((int)totalGold);
                            p.Eqs.CurExp += (int)totalExp;
                        }

                        // Pet progression
                        var petFighter = battle.Attackers.FirstOrDefault(a => a.PetRef != null && a.OwnerID == p.CharID);
                        if (petFighter?.PetRef != null)
                        {
                            petFighter.PetRef.HP = Math.Max(1, petFighter.CurHP);
                            petFighter.PetRef.SP = Math.Max(0, petFighter.CurSP);
                            if (totalExp >= 50 && petFighter.PetRef.Level < 199)
                            {
                                petFighter.PetRef.Level++;
                                petFighter.PetRef.MaxHP += 30;
                                petFighter.PetRef.HP = petFighter.PetRef.MaxHP;
                                petFighter.PetRef.MaxSP += 15;
                                petFighter.PetRef.SP = petFighter.PetRef.MaxSP;
                                p.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"{petFighter.PetRef.PetName} leveled up to Lv.{petFighter.PetRef.Level}!"));
                            }
                        }

                        // 1. AC 11:12 Combat finish
                        p.Send(Tools.FromFormat("bbb", 11, 12, 1));
                        // 2. AC 22:6 Victory
                        p.Send(Tools.FromFormat("bbbbb", 22, 6, 11, 0, 2));
                        // 3. AC 22:5 Rewards
                        p.Send(Tools.FromFormat("bbwww", 22, 5, (ushort)11, (ushort)Math.Min(0xFFFF, totalExp), (ushort)Math.Min(0xFFFF, totalGold)));

                        // 4. Despawn pets (4-byte AC 11:1)
                        foreach (var f in battle.Attackers.Where(a => a.FighterType == BattleFighterType.Pet))
                        {
                            p.Send(Tools.FromFormat("bbbb", 11, 1, f.GridX, f.GridY));
                        }

                        // 5. Despawn players & close battle window for each team member
                        foreach (var pMember in battle.AttackingPlayers)
                        {
                            var pf = battle.Attackers.FirstOrDefault(a => a.PlayerRef == pMember);
                            if (pf != null)
                            {
                                p.Send(Tools.FromFormat("bbbbb", 11, 1, pf.GridX, pf.GridY, 0));
                            }
                            SendPacket p110 = new SendPacket();
                            p110.PackArray(new byte[] { 11, 0 });
                            p110.Pack32(pMember.CharID);
                            p110.Pack16(0);
                            p.Send(p110);
                        }

                        // 6. Normal map mode and movement release
                        p.Send(Tools.FromFormat("bbb", 6, 2, 0));
                        p.Send(Tools.FromFormat("bb", 20, 8));
                    }

                    // Defending team cleanup (if PvP)
                    foreach (var p in battle.DefendingPlayers)
                    {
                        p.Send(Tools.FromFormat("bbb", 11, 12, 1));
                        p.Send(Tools.FromFormat("bbbbb", 22, 6, 11, 0, 0)); // Defeat
                        SendPacket p110 = new SendPacket();
                        p110.PackArray(new byte[] { 11, 0 });
                        p110.Pack32(p.CharID);
                        p110.Pack16(0);
                        p.Send(p110);

                        foreach (var f in battle.Attackers.Concat(battle.Defenders))
                        {
                            p.Send(Tools.FromFormat("bbbbb", 11, 1, f.GridX, f.GridY, 0));
                        }

                        if (p.Eqs != null)
                        {
                            p.Eqs.CurHP = Math.Max(10, p.Eqs.FullHP / 2);
                            p.Eqs.Send8_1();
                        }

                        p.Send(Tools.FromFormat("bbb", 6, 2, 0));
                        p.Send(Tools.FromFormat("bb", 20, 8));
                    }

                    // Check Quest Battle Completion for Leader
                    CheckQuestBattleCompletion(battle);

                    // Persist state for all combat participants
                    foreach (var p in battle.AllPlayers)
                    {
                        p.SaveCharacterData();
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PvEBattle] Exception in EndBattleVictory: {ex.Message}");
                }
            });
        }

        private static void EndBattleDefeat(ActiveBattle battle)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (battle == null) return;
                    battle.IsFinished = true;
                    battle.CancelTurnTimer();

                    lock (_lock)
                    {
                        foreach (var p in battle.AllPlayers)
                        {
                            _activeBattles.Remove(p.CharID);
                        }
                    }

                    DebugSystem.Write($"[PvEBattle] Attacking team was defeated in battle!");
                    await Task.Delay(1200);

                    // Attacking players defeat cleanup
                    foreach (var p in battle.AttackingPlayers)
                    {
                        p.Send(Tools.FromFormat("bbb", 11, 12, 1));
                        p.Send(Tools.FromFormat("bbbbb", 22, 6, 11, 0, 0)); // Defeat

                        SendPacket p110 = new SendPacket();
                        p110.PackArray(new byte[] { 11, 0 });
                        p110.Pack32(p.CharID);
                        p110.Pack16(0);
                        p.Send(p110);

                        foreach (var f in battle.Attackers.Concat(battle.Defenders))
                        {
                            p.Send(Tools.FromFormat("bbbbb", 11, 1, f.GridX, f.GridY, 0));
                        }

                        if (p.Eqs != null)
                        {
                            p.Eqs.CurHP = Math.Max(10, p.Eqs.FullHP / 2);
                            p.Eqs.Send8_1();
                        }

                        p.Send(Tools.FromFormat("bbb", 6, 2, 0));
                        p.Send(Tools.FromFormat("bb", 20, 8));
                    }

                    // Defending players victory cleanup (if PvP)
                    foreach (var p in battle.DefendingPlayers)
                    {
                        p.Send(Tools.FromFormat("bbb", 11, 12, 1));
                        p.Send(Tools.FromFormat("bbbbb", 22, 6, 11, 0, 2)); // Victory
                        p.Send(Tools.FromFormat("bbwww", 22, 5, (ushort)11, (ushort)150, (ushort)100));

                        SendPacket p110 = new SendPacket();
                        p110.PackArray(new byte[] { 11, 0 });
                        p110.Pack32(p.CharID);
                        p110.Pack16(0);
                        p.Send(p110);

                        foreach (var f in battle.Attackers.Concat(battle.Defenders))
                        {
                            p.Send(Tools.FromFormat("bbbbb", 11, 1, f.GridX, f.GridY, 0));
                        }

                        p.Send(Tools.FromFormat("bbb", 6, 2, 0));
                        p.Send(Tools.FromFormat("bb", 20, 8));
                    }
                    if (battle.QuestContext?.OnDefeat != null)
                    {
                        try
                        {
                            battle.QuestContext.OnDefeat.Invoke();
                            DebugSystem.Write($"[PvEBattle] Executed QuestBattleContext.OnDefeat for {battle.LeaderPlayer?.CharName}");
                        }
                        catch (Exception qcbEx)
                        {
                            DebugSystem.Write($"[PvEBattle] Error in QuestBattleContext.OnDefeat: {qcbEx.Message}");
                        }
                    }
                    // Persist state for all combat participants
                    foreach (var p in battle.AllPlayers)
                    {
                        p.SaveCharacterData();
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PvEBattle] Exception in EndBattleDefeat: {ex.Message}");
                }
            });
        }

        private static void CheckQuestBattleCompletion(ActiveBattle battle)
        {
            try
            {
                var player = battle?.LeaderPlayer;
                if (player == null) return;

                // Dynamic Quest / Event Battle Callback (Eve.Emg engine)
                if (battle.QuestContext?.OnVictory != null)
                {
                    try
                    {
                        battle.QuestContext.OnVictory.Invoke();
                        DebugSystem.Write($"[PvEBattle] Executed QuestBattleContext.OnVictory for {player.CharName}");
                    }
                    catch (Exception qcbEx)
                    {
                        DebugSystem.Write($"[PvEBattle] Error in QuestBattleContext.OnVictory: {qcbEx.Message}");
                    }
                }

                // Quest 1005: Save Niss (Wolf Guard battle victory)
                if (battle.Monsters != null && battle.Monsters.Any(m => m.MonsterId == 11066 || (m.MonsterName ?? "").ToLower().Contains("wolf guard")))
                {
                    if (player.Quests != null && player.Quests.TryGetValue(1005, out var pq) && pq.State == QuestState.InProgress)
                    {
                        pq.State = QuestState.Completed;
                        pq.CompletedAt = DateTime.UtcNow;
                        QuestManager.SavePlayerQuest(player, 1005);
                        QuestManager.SendQuestUpdate(player, 1005, QuestState.Completed);
                        QuestManager.SendCompanionReward(player, 11066, "Niss");
                        player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "You rescued Niss! She has joined your party."));
                    }
                }

                // Quest 1010: Rescue Xaolan (Pirate Lea / Hijacker victory)
                if (battle.Monsters != null && battle.Monsters.Any(m => m.MonsterId == 14155 || m.MonsterId == 12049 || m.MonsterId == 12050 || (m.MonsterName ?? "").ToLower().Contains("pirate lea") || (m.MonsterName ?? "").ToLower().Contains("hijacker")))
                {
                    if (player.Quests != null)
                    {
                        if (!player.Quests.ContainsKey(1010))
                        {
                            player.Quests[1010] = new PlayerQuest(1010, QuestState.InProgress, 1);
                        }
                        var pq = player.Quests[1010];
                        pq.State = QuestState.Completed;
                        pq.CompletedAt = DateTime.UtcNow;
                        QuestManager.SavePlayerQuest(player, 1010);
                        QuestManager.SendQuestUpdate(player, 1010, QuestState.Completed);
                        QuestManager.SendCompanionReward(player, 14156, "Xaolan");
                        player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "You defeated the pirates and rescued Xaolan! She joined your party."));

                        player.Send(Tools.FromFormat("bbwb", 24, 1, 1010, 2));
                        player.Send(Tools.FromFormat("bbwb", 24, 5, 1010, 1));
                    }
                }

                // Quest 1012: Little Red Riding Hood (Wild Wolf victory)
                if (battle.Monsters != null && battle.Monsters.Any(m => m.MonsterId == 17437 || (m.MonsterName ?? "").ToLower().Contains("wild wolf")))
                {
                    if (player.Quests != null && player.Quests.TryGetValue(1012, out var pq) && pq.State == QuestState.InProgress)
                    {
                        pq.State = QuestState.Completed;
                        pq.CompletedAt = DateTime.UtcNow;
                        QuestManager.SavePlayerQuest(player, 1012);
                        QuestManager.SendQuestUpdate(player, 1012, QuestState.Completed);
                        player.Gold += 400;
                        player.Send(Tools.FromFormat("bbd", 23, 114, (uint)player.Gold));
                        player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "You defeated the wolf and saved Grandmother! Quest Completed."));
                    }
                }
            }
            catch (Exception qEx)
            {
                DebugSystem.Write($"[PvEBattle] Quest completion check exception: {qEx.Message}");
            }
        }

        private static void BroadcastToBattle(ActiveBattle battle, SendPacket p)
        {
            if (battle == null || p == null) return;
            foreach (var pl in battle.AllPlayers)
            {
                pl.Send(p);
            }
        }

        private static void SendStatSync(Player player, byte x, byte y, byte statId, uint val)
        {
            SendPacket p = new SendPacket();
            p.PackArray(new byte[] { 51, 1 });
            p.Pack8(x);
            p.Pack8(y);
            p.Pack8(statId);
            p.Pack32(val);
            player.Send(p);
        }
    }
}
