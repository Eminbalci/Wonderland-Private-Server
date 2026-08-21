using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Maps;
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

    public class ActiveBattle
    {
        public Player Player { get; set; }
        public List<BattleMonster> Monsters { get; set; } = new List<BattleMonster>();

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

        public byte PlayerGridX { get; set; } = 4;
        public byte PlayerGridY { get; set; } = 2;
        public byte MonsterGridX { get => PrimaryMonster?.GridX ?? 2; set { if (PrimaryMonster != null) PrimaryMonster.GridX = value; } }
        public byte MonsterGridY { get => PrimaryMonster?.GridY ?? 2; set { if (PrimaryMonster != null) PrimaryMonster.GridY = value; } }

        public int Turn { get; set; } = 0;
        public bool IsFinished { get; set; } = false;
        public bool IsRandomEncounter { get; set; } = false;
    }

    public static class PvEBattleManager
    {
        private static readonly Dictionary<uint, ActiveBattle> _activeBattles = new Dictionary<uint, ActiveBattle>();
        private static readonly object _lock = new object();
        private static readonly Random _rng = new Random();

        // WLO Enemy Formation Grid Positions (Front row & Back row)
        private static readonly byte[][] EnemyGridSlots = new byte[][]
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

        public static bool IsSafeTownMap(ushort mapId)
        {
            // Town / Village / Interior safe zones (No random encounters)
            if (mapId == 10000 || mapId == 60000) return true; // Kelan Village
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

            // Find wild mob templates that exist on this map
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

            // Group size: 1 to 4 monsters
            int monsterCount = QuestNpc.NextRandom(1, 5);
            ActiveBattle battle = new ActiveBattle
            {
                Player = player,
                IsRandomEncounter = true
            };

            int baseLevel = Math.Max(1, (int)triggerMob.Level);
            int baseHp = triggerMob.HP > 0 ? (int)triggerMob.HP : (baseLevel * 30 + 100);
            int baseSp = baseLevel * 20 + 50;

            for (int i = 0; i < monsterCount; i++)
            {
                // Slight level variance (+/- 1) for multi-mob pack realism
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

            lock (_lock)
            {
                _activeBattles[player.CharID] = battle;
            }

            DebugSystem.Write($"[PvEBattle] Proximity Encounter triggered for {player.CharName} near {triggerMob.Name} on Map {map.MapID}: {battle.Monsters.Count} monsters spawned in formation!");
            InitializeAndStartBattle(player, battle);
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
                Player = player,
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

            lock (_lock)
            {
                _activeBattles[player.CharID] = battle;
            }

            DebugSystem.Write($"[PvEBattle] Random Encounter triggered for {player.CharName} on Map {map.MapID}: {battle.Monsters.Count} monsters spawned in formation!");
            InitializeAndStartBattle(player, battle);
        }

        private static void StartRandomEncounter(Player player, GameMap map, uint fallbackTid, string fallbackName, int fallbackLv)
        {
            if (player == null) return;

            lock (_lock)
            {
                if (_activeBattles.ContainsKey(player.CharID)) return;
            }

            int monsterCount = QuestNpc.NextRandom(1, 3); // 1 to 2 monsters
            ActiveBattle battle = new ActiveBattle
            {
                Player = player,
                IsRandomEncounter = true
            };

            for (int i = 0; i < monsterCount; i++)
            {
                int monHP = fallbackLv * 30 + 100;
                int monSP = fallbackLv * 20 + 50;

                BattleMonster bm = new BattleMonster
                {
                    MonsterId = fallbackTid,
                    MonsterName = fallbackName,
                    MonsterLevel = fallbackLv,
                    MonsterMaxHP = monHP,
                    MonsterHP = monHP,
                    MonsterMaxSP = monSP,
                    MonsterSP = monSP,
                    MonsterElement = 0,
                    MonsterAtk = (int)Math.Round(fallbackLv * 1.5 + 5),
                    MonsterDef = (int)Math.Round(fallbackLv * 1.2 + 3),
                    MonsterSpd = (int)Math.Round(fallbackLv * 1.3 + 4),
                    ClickId = (ushort)(2000 + i),
                    GridX = EnemyGridSlots[i % EnemyGridSlots.Length][0],
                    GridY = EnemyGridSlots[i % EnemyGridSlots.Length][1]
                };

                battle.Monsters.Add(bm);
            }

            lock (_lock)
            {
                _activeBattles[player.CharID] = battle;
            }

            DebugSystem.Write($"[PvEBattle] Random Encounter triggered for {player.CharName} on Map {map.MapID} ({battle.Monsters.Count}x {fallbackName})");
            InitializeAndStartBattle(player, battle);
        }

        public static void StartPvEBattle(Player player, ushort clickId, string monsterName, int npcLv = 10, int npcHp = 250, uint monsterTid = 11066)
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
                Player = player
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

            lock (_lock)
            {
                _activeBattles[player.CharID] = battle;
            }

            DebugSystem.Write($"[PvEBattle] Starting Quest PvE battle for {player.CharName} vs {monsterName} (Lv: {npcLv}, HP: {npcHp})");
            InitializeAndStartBattle(player, battle);
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

            // Normalize clickId if high-byte swapped
            ushort realClickId = clickId;
            if (clickId > 255 && (clickId & 0xFF) == 0)
                realClickId = (ushort)(clickId >> 8);

            // Resolve authentic monster info from map NPC or GameDataBase
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
                // Fallback database lookup
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
                Player = player
            };

            // Main clicked monster at front-center (2, 2)
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

            // Randomly add 0 to 2 companion monsters of similar level if wandering mob encounter
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

            lock (_lock)
            {
                _activeBattles[player.CharID] = battle;
            }

            DebugSystem.Write($"[PvEBattle] Starting PvE battle for {player.CharName} vs {battle.MonsterName} ({battle.Monsters.Count} mobs, TID: {realMonsterTID}, ClickID: {realClickId})");
            InitializeAndStartBattle(player, battle);
        }

        private static void InitializeAndStartBattle(Player player, ActiveBattle battle)
        {
            // 1. AC 20:12 (battle mode enter)
            player.Send(Tools.FromFormat("bb", 20, 12));

            // 2. AC 6:2 [01] (mode change signal)
            player.Send(Tools.FromFormat("bbb", 6, 2, 1));

            // 3. AC 11:250 (Player battle entity)
            ushort bgId = (player.CurMap != null && player.CurMap.MapID < 10000) ? (ushort)player.CurMap.MapID : (ushort)1;
            SendPacket p250 = new SendPacket();
            p250.PackArray(new byte[] { 11, 250 });
            p250.Pack16(bgId);
            p250.Pack8(1); // role = 1
            p250.Pack8(2); // ftype = 2 (player)
            p250.Pack32(player.CharID);
            p250.Pack16(0); // click_id
            p250.Pack32(0); // owner_id
            p250.Pack8(battle.PlayerGridX); // grid x = 4
            p250.Pack8(battle.PlayerGridY); // grid y = 2
            p250.Pack32((uint)Math.Max(1, player.Eqs.FullHP));
            p250.Pack16((ushort)Math.Min(0xFFFF, Math.Max(1, player.Eqs.FullSP)));
            p250.Pack32((uint)Math.Max(1, player.Eqs.CurHP));
            p250.Pack16((ushort)Math.Min(0xFFFF, Math.Max(0, player.Eqs.CurSP)));
            p250.Pack8(player.Eqs.Level);
            p250.Pack8((byte)player.Eqs.Element);
            p250.Pack8(0); // reborn
            p250.Pack8(0); // job
            p250.Pack16(0); // trailing pad
            player.Send(p250);

            // 4. AC 11:10 [01] (combat start signal)
            player.Send(Tools.FromFormat("bbb", 11, 10, 1));

            // 5. AC 11:5 (Spawn each monster entity in formation)
            foreach (var monster in battle.Monsters)
            {
                SendPacket p5 = new SendPacket();
                p5.PackArray(new byte[] { 11, 5 });
                p5.Pack8(1); // role = 1
                p5.Pack8(7); // ftype = 7 (monster)
                p5.Pack32((uint)monster.MonsterId);
                p5.Pack16(monster.ClickId);
                p5.Pack32(0); // owner_id
                p5.Pack8(monster.GridX);
                p5.Pack8(monster.GridY);
                p5.Pack32((uint)monster.MonsterMaxHP);
                p5.Pack16((ushort)Math.Min(0xFFFF, monster.MonsterMaxSP));
                p5.Pack32((uint)monster.MonsterHP);
                p5.Pack16((ushort)Math.Min(0xFFFF, monster.MonsterSP));
                p5.Pack8((byte)Math.Min(255, monster.MonsterLevel));
                p5.Pack8((byte)monster.MonsterElement);
                p5.Pack8(0); // reborn
                p5.Pack8(0); // job
                p5.Pack16(0); // trailing pad
                player.Send(p5);
            }

            // 6. AC 51:1 Sync HP/SP for player and all monsters
            SendStatSync(player, battle.PlayerGridX, battle.PlayerGridY, 0x19, (uint)player.Eqs.CurHP);
            SendStatSync(player, battle.PlayerGridX, battle.PlayerGridY, 0x1a, (uint)player.Eqs.CurSP);
            foreach (var monster in battle.Monsters)
            {
                SendStatSync(player, monster.GridX, monster.GridY, 0x19, (uint)monster.MonsterMaxHP);
                SendStatSync(player, monster.GridX, monster.GridY, 0x1a, (uint)monster.MonsterMaxSP);
            }

            // 7. AC 50:6 & AC 52:1 Start Round & Open Action UI
            player.Send(Tools.FromFormat("bbbbb", 50, 6, battle.PlayerGridX, battle.PlayerGridY, 0));
            player.Send(Tools.FromFormat("bb", 52, 1));
        }

        public static void HandleBattleAction(Player player, byte sub, RecievePacket r)
        {
            ActiveBattle battle = GetBattle(player);
            if (battle == null || battle.IsFinished) return;

            // sub 1 / default (Attack, Skill, Defend, Flee, or Catch)
            ushort skillId = 10001; // Basic Attack
            byte targetX = 0, targetY = 0;
            try
            {
                if ((r.Count - r.GetPtr()) >= 4)
                {
                    byte srcX = r.Unpack8();
                    byte srcY = r.Unpack8();
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

            // 1. Flee / Escape (sub == 5 or Skill 60041)
            if (sub == 5 || skillId == 60041)
            {
                HandleFlee(player);
                return;
            }

            // 2. Defend / Shield (sub == 4 or Skill 60021)
            if (sub == 4 || skillId == 60021)
            {
                ProcessTurn(battle, playerAction: "defend", skillId: 60021, targetGridX: 0, targetGridY: 0);
                return;
            }

            // 3. Catch (Skill 10008)
            if (skillId == 10008)
            {
                ProcessTurn(battle, playerAction: "catch", skillId: 10008, targetGridX: targetX, targetGridY: targetY);
                return;
            }

            ProcessTurn(battle, playerAction: "attack", skillId: skillId, targetGridX: targetX, targetGridY: targetY);
        }

        public static void HandleFlee(Player player)
        {
            Task.Run(async () =>
            {
                try
                {
                    ActiveBattle battle = GetBattle(player);
                    if (battle == null) return;

                    battle.IsFinished = true;
                    lock (_lock)
                    {
                        _activeBattles.Remove(player.CharID);
                    }

                    DebugSystem.Write($"[PvEBattle] Player {player.CharName} fled battle.");

                    // 1. AC 53:5 Action notification
                    player.Send(Tools.FromFormat("bbbb", 53, 5, battle.PlayerGridX, battle.PlayerGridY));

                    // 2. AC 50:1 Flee animation
                    SendPacket pAnim = new SendPacket();
                    pAnim.PackArray(new byte[] { 50, 1 });
                    pAnim.PackArray(new byte[] { 0x11, 0x00 });
                    pAnim.Pack8(battle.PlayerGridX); pAnim.Pack8(battle.PlayerGridY); // source
                    pAnim.Pack16(60041); // flee skill
                    pAnim.Pack8(0);
                    pAnim.Pack8(1);
                    pAnim.Pack8(battle.PlayerGridX); pAnim.Pack8(battle.PlayerGridY); // target
                    pAnim.Pack8(1); pAnim.Pack8(0); pAnim.Pack8(1);
                    pAnim.Pack8(0); // stat_id = 0
                    pAnim.Pack32(0); // 0 dmg
                    pAnim.Pack8(1);
                    player.Send(pAnim);

                    await Task.Delay(1200);

                    // 3. AC 11:12 Battle end
                    player.Send(Tools.FromFormat("bbb", 11, 12, 1));

                    // 4. AC 22:6 [11, 0, 1] (Result: 1=Fled)
                    player.Send(Tools.FromFormat("bbbbb", 22, 6, 11, 0, 1));

                    // 5. AC 11:0 Close battle window
                    SendPacket p110 = new SendPacket();
                    p110.PackArray(new byte[] { 11, 0 });
                    p110.Pack32(player.CharID);
                    p110.Pack16(0);
                    player.Send(p110);

                    // 6. Despawn all fighters from battle grid
                    player.Send(Tools.FromFormat("bbbbb", 11, 1, battle.PlayerGridX, battle.PlayerGridY, 0));
                    if (battle.Monsters != null)
                    {
                        foreach (var m in battle.Monsters)
                        {
                            player.Send(Tools.FromFormat("bbbbb", 11, 1, m.GridX, m.GridY, 0));
                        }
                    }

                    // 7. AC 6:2 [00] Return to map normal mode
                    player.Send(Tools.FromFormat("bbb", 6, 2, 0));

                    // 8. AC 20:8 Release movement lock
                    player.Send(Tools.FromFormat("bb", 20, 8));
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PvEBattle] Exception in HandleFlee: {ex.Message}");
                }
            });
        }

        private static void ProcessTurn(ActiveBattle battle, string playerAction, ushort skillId, byte targetGridX, byte targetGridY)
        {
            Task.Run(async () =>
            {
                try
                {
                    Player player = battle?.Player;
                    if (player == null || battle.IsFinished) return;

                    battle.Turn++;

                    // Find targeted monster
                    BattleMonster targetMonster = null;
                    if (targetGridX > 0 && targetGridY > 0 && battle.Monsters != null)
                    {
                        targetMonster = battle.Monsters.FirstOrDefault(m => !m.IsDead && m.GridX == targetGridX && m.GridY == targetGridY);
                    }
                    if (targetMonster == null)
                    {
                        targetMonster = battle.PrimaryMonster;
                    }
                    if (targetMonster == null)
                    {
                        EndBattleVictory(battle);
                        return;
                    }

                    // 1. Defend / Shield Action (Skill 60021)
                    if (playerAction == "defend" || skillId == 60021)
                    {
                        // Action notification
                        player.Send(Tools.FromFormat("bbbb", 53, 5, battle.PlayerGridX, battle.PlayerGridY));

                        // Defend animation on self (0 damage)
                        SendPacket pAnim = new SendPacket();
                        pAnim.PackArray(new byte[] { 50, 1 });
                        pAnim.PackArray(new byte[] { 0x11, 0x00 });
                        pAnim.Pack8(battle.PlayerGridX); pAnim.Pack8(battle.PlayerGridY);
                        pAnim.Pack16(60021); // defend skill
                        pAnim.Pack8(0); pAnim.Pack8(1);
                        pAnim.Pack8(battle.PlayerGridX); pAnim.Pack8(battle.PlayerGridY);
                        pAnim.Pack8(1); pAnim.Pack8(0); pAnim.Pack8(1);
                        pAnim.Pack8(0); // 0 stat
                        pAnim.Pack32(0); // 0 dmg
                        pAnim.Pack8(1);
                        player.Send(pAnim);
                    }
                    // 2. Check if action is Catch (Skill 10008)
                    else if (skillId == 10008)
                    {
                        // Action notification
                        player.Send(Tools.FromFormat("bbbb", 53, 5, battle.PlayerGridX, battle.PlayerGridY));

                        // Catch chance calculation
                        int playerLvl = player.Eqs?.Level ?? 1;
                        int monsterLvl = targetMonster.MonsterLevel;
                        double hpPercent = (double)targetMonster.MonsterHP / Math.Max(1, targetMonster.MonsterMaxHP);
                        
                        double catchChance = 75.0 + ((playerLvl - monsterLvl) * 5.0) + ((1.0 - hpPercent) * 20.0);
                        if (playerLvl >= monsterLvl) catchChance = Math.Max(85.0, catchChance);
                        catchChance = Math.Min(98.0, Math.Max(15.0, catchChance));

                        bool catchSuccess = (_rng.NextDouble() * 100.0) <= catchChance;

                        // Animation packet
                        SendPacket pAnim = new SendPacket();
                        pAnim.PackArray(new byte[] { 50, 1 });
                        pAnim.PackArray(new byte[] { 0x11, 0x00 });
                        pAnim.Pack8(battle.PlayerGridX); pAnim.Pack8(battle.PlayerGridY);
                        pAnim.Pack16(10008); // catch skill
                        pAnim.Pack8(0); pAnim.Pack8(1);
                        pAnim.Pack8(targetMonster.GridX); pAnim.Pack8(targetMonster.GridY);
                        pAnim.Pack8(1); pAnim.Pack8(0); pAnim.Pack8(1);
                        pAnim.Pack8(0); // stat_id = 0 (no damage)
                        pAnim.Pack32(0); // 0 dmg
                        pAnim.Pack8(1);
                        player.Send(pAnim);

                        if (catchSuccess)
                        {
                            int capturedLevel = Math.Max(1, targetMonster.MonsterLevel);
                            int capturedMaxHp = Math.Max(50, targetMonster.MonsterMaxHP);
                            int capturedMaxSp = Math.Max(30, targetMonster.MonsterMaxSP);
                            ushort capturedAtk = (ushort)Math.Max(5, targetMonster.MonsterAtk);
                            ushort capturedDef = (ushort)Math.Max(5, targetMonster.MonsterDef);
                            ushort capturedSpd = (ushort)Math.Max(5, targetMonster.MonsterSpd);

                            targetMonster.MonsterHP = 0; // Mark removed from combat
                            targetMonster.IsCaptured = true; // Mark captured so no drops or exp are given

                            // Add to player's pet list
                            byte petSlot = 1;
                            if (player.PlayerPets != null)
                            {
                                while (player.PlayerPets.ContainsKey(petSlot) && petSlot <= 4) petSlot++;
                                if (petSlot <= 4)
                                {
                                    player.PlayerPets[petSlot] = new Player.PlayerPetData()
                                    {
                                        Slot = petSlot,
                                        PetID = (uint)targetMonster.MonsterId,
                                        PetName = targetMonster.MonsterName,
                                        Level = (byte)capturedLevel,
                                        HP = capturedMaxHp,
                                        MaxHP = capturedMaxHp,
                                        SP = capturedMaxSp,
                                        MaxSP = capturedMaxSp,
                                        Amity = 60,
                                        IsBattle = false,
                                        IsRide = false
                                    };

                                    // AC 15:1 54-byte Pet Recruit Packet
                                    SendPacket petPkt = new SendPacket();
                                    petPkt.PackArray(new byte[] { 15, 1 });
                                    petPkt.Pack32(player.CharID);
                                    petPkt.Pack32((uint)targetMonster.MonsterId);
                                    petPkt.Pack8(petSlot);
                                    petPkt.Pack16(capturedAtk); // STR
                                    petPkt.Pack16(capturedDef); // CON
                                    petPkt.Pack16(5); // INT
                                    petPkt.Pack16(5); // WIS
                                    petPkt.Pack16(capturedSpd); // AGI
                                    petPkt.Pack8((byte)targetMonster.MonsterElement); // Element
                                    petPkt.Pack32((uint)capturedLevel); // Level
                                    petPkt.Pack32((uint)capturedMaxHp); // CurHP
                                    petPkt.Pack32((uint)capturedMaxHp); // MaxHP
                                    for (int i = 0; i < 7; i++) petPkt.Pack8(0);
                                    petPkt.Pack8(60); // Amity: 60
                                    for (int i = 0; i < 13; i++) petPkt.Pack8(0);
                                    player.Send(petPkt);

                                    // Sync Pet Level, HP, and SP stats for Party UI & Status window
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 35, petSlot, (uint)capturedLevel, 0)); // Pet Level (Stat 35)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 37, petSlot, (uint)(capturedLevel - 1), 0)); // Pet Potential / Level offset (Stat 37)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 38, petSlot, 0, 0)); // Pet Potential Points (Stat 38)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 207, petSlot, (uint)capturedMaxHp, 0)); // EquippedMaxHP (Stat 207)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 25, petSlot, (uint)capturedMaxHp, 0)); // CurHP (Stat 25)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 208, petSlot, (uint)capturedMaxSp, 0)); // EquippedMaxSP (Stat 208)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 26, petSlot, (uint)capturedMaxSp, 0)); // CurSP (Stat 26)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 205, petSlot, (uint)capturedMaxHp, 0)); // FullHP (Stat 205)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 206, petSlot, (uint)capturedMaxSp, 0)); // FullSP (Stat 206)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 210, petSlot, (uint)capturedAtk, 0)); // EquippedATK (Stat 210)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 41, petSlot, (uint)capturedAtk, 0)); // FullATK (Stat 41)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 211, petSlot, (uint)capturedDef, 0)); // EquippedDEF (Stat 211)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 42, petSlot, (uint)capturedDef, 0)); // FullDEF (Stat 42)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 214, petSlot, (uint)capturedSpd, 0)); // EquippedSPD (Stat 214)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 45, petSlot, (uint)capturedSpd, 0)); // FullSPD (Stat 45)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 28, petSlot, (uint)Math.Max(5, capturedAtk / 2), 0)); // STR (Stat 28)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 29, petSlot, (uint)Math.Max(5, capturedDef / 2), 0)); // CON (Stat 29)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 27, petSlot, 5, 0)); // INT (Stat 27)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 33, petSlot, 5, 0)); // WIS (Stat 33)
                                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 30, petSlot, (uint)Math.Max(5, capturedSpd / 2), 0)); // AGI (Stat 30)

                                    player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Successfully captured {targetMonster.MonsterName} (Lv.{capturedLevel}) into Pet Slot #{petSlot}!"));
                                    DebugSystem.Write($"[PvEBattle] Player {player.CharName} caught {targetMonster.MonsterName} (Lv.{capturedLevel}, ID: {targetMonster.MonsterId}) into Pet Slot #{petSlot}!");
                                }
                                else
                                {
                                    player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Your Pet bag is full (Max 4 pets)!"));
                                }
                            }

                            // Despawn captured monster from grid
                            player.Send(Tools.FromFormat("bbbbb", 11, 1, targetMonster.GridX, targetMonster.GridY, 0));
                        }
                        else
                        {
                            player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Failed to capture {targetMonster.MonsterName}!"));
                            DebugSystem.Write($"[PvEBattle] Player {player.CharName} failed to catch {targetMonster.MonsterName} (Chance: {catchChance:F1}%).");
                        }
                    }
                    // 3. Normal Attack or Skill Attack
                    else
                    {
                        // Action notification
                        player.Send(Tools.FromFormat("bbbb", 53, 5, battle.PlayerGridX, battle.PlayerGridY));

                        // Calculate player damage
                        int playerDmg = Math.Max(10, ((player.Eqs?.FullAtk ?? 20) * 2) - targetMonster.MonsterDef);
                        if (skillId > 10001)
                        {
                            playerDmg = Math.Max(15, (int)((player.Eqs?.FullAtk ?? 20) * 2.8) - (targetMonster.MonsterDef / 2));
                            if (player.Eqs != null)
                            {
                                player.Eqs.CurSP = Math.Max(0, player.Eqs.CurSP - 10);
                                SendStatSync(player, battle.PlayerGridX, battle.PlayerGridY, 0x1a, (uint)player.Eqs.CurSP);
                            }
                        }

                        // Animation packet
                        SendPacket pAnim = new SendPacket();
                        pAnim.PackArray(new byte[] { 50, 1 });
                        pAnim.PackArray(new byte[] { 0x11, 0x00 });
                        pAnim.Pack8(battle.PlayerGridX); pAnim.Pack8(battle.PlayerGridY);
                        pAnim.Pack16(skillId > 0 ? skillId : (ushort)10001);
                        pAnim.Pack8(0);
                        pAnim.Pack8(1);
                        pAnim.Pack8(targetMonster.GridX); pAnim.Pack8(targetMonster.GridY);
                        pAnim.Pack8(1); pAnim.Pack8(0); pAnim.Pack8(1);
                        pAnim.Pack8(0x19); // HP damage
                        pAnim.Pack32((uint)playerDmg);
                        pAnim.Pack8(1);
                        player.Send(pAnim);

                        targetMonster.MonsterHP = Math.Max(0, targetMonster.MonsterHP - playerDmg);
                        SendStatSync(player, targetMonster.GridX, targetMonster.GridY, 0x19, (uint)targetMonster.MonsterHP);
                    }

                    // Wait for player animation
                    await Task.Delay(1400);

                    // Check if all monsters are defeated
                    if (battle.Monsters == null || battle.Monsters.All(m => m.IsDead))
                    {
                        EndBattleVictory(battle);
                        return;
                    }

                    // 2. Each living monster attacks player in turn
                    var livingMonsters = battle.Monsters.Where(m => !m.IsDead).ToList();
                    foreach (var monster in livingMonsters)
                    {
                        int defVal = player.Eqs?.FullDef ?? 10;
                        int rawDmg = Math.Max(5, (int)(monster.MonsterAtk * 1.2) - defVal);
                        
                        // If player defended, mitigate damage by 65% - 75%
                        int monsterDmg = (playerAction == "defend" || skillId == 60021) 
                            ? Math.Max(1, (int)(rawDmg * 0.35)) 
                            : Math.Max(1, rawDmg);

                        player.Send(Tools.FromFormat("bbbb", 53, 5, monster.GridX, monster.GridY));

                        SendPacket mAnim = new SendPacket();
                        mAnim.PackArray(new byte[] { 50, 1 });
                        mAnim.PackArray(new byte[] { 0x11, 0x00 });
                        mAnim.Pack8(monster.GridX); mAnim.Pack8(monster.GridY);
                        mAnim.Pack16(10001);
                        mAnim.Pack8(0);
                        mAnim.Pack8(1);
                        mAnim.Pack8(battle.PlayerGridX); mAnim.Pack8(battle.PlayerGridY);
                        mAnim.Pack8(1); mAnim.Pack8(0); mAnim.Pack8(1);
                        mAnim.Pack8(0x19); // HP damage
                        mAnim.Pack32((uint)monsterDmg);
                        mAnim.Pack8(1);
                        player.Send(mAnim);

                        if (player.Eqs != null)
                        {
                            player.Eqs.CurHP = Math.Max(0, player.Eqs.CurHP - monsterDmg);
                            SendStatSync(player, battle.PlayerGridX, battle.PlayerGridY, 0x19, (uint)player.Eqs.CurHP);

                            // Player Defeat / Death
                            if (player.Eqs.CurHP <= 0)
                            {
                                EndBattleDefeat(battle);
                                return;
                            }
                        }

                        await Task.Delay(1200);
                    }

                    // 3. Give turn for next round
                    player.Send(Tools.FromFormat("bbbb", 53, 5, battle.PlayerGridX, battle.PlayerGridY));
                    player.Send(Tools.FromFormat("bbbbb", 50, 6, battle.PlayerGridX, battle.PlayerGridY, 0));
                    player.Send(Tools.FromFormat("bb", 52, 1));
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PvEBattle] Exception in ProcessTurn: {ex.Message}");
                }
            });
        }

        private static void EndBattleDefeat(ActiveBattle battle)
        {
            Task.Run(async () =>
            {
                try
                {
                    Player player = battle?.Player;
                    if (player == null) return;

                    battle.IsFinished = true;
                    lock (_lock)
                    {
                        _activeBattles.Remove(player.CharID);
                    }

                    DebugSystem.Write($"[PvEBattle] Player {player.CharName} was defeated in combat!");

                    await Task.Delay(1200);

                    // 1. AC 11:12 Battle finish signal
                    player.Send(Tools.FromFormat("bbb", 11, 12, 1));

                    // 2. AC 22:6 [11, 0, 0] (Battle Result: 0 = Defeat)
                    player.Send(Tools.FromFormat("bbbbb", 22, 6, 11, 0, 0));

                    // 3. AC 11:0 Close battle window
                    SendPacket p110 = new SendPacket();
                    p110.PackArray(new byte[] { 11, 0 });
                    p110.Pack32(player.CharID);
                    p110.Pack16(0);
                    player.Send(p110);

                    // 4. Despawn all fighters from battle grid
                    player.Send(Tools.FromFormat("bbbbb", 11, 1, battle.PlayerGridX, battle.PlayerGridY, 0));
                    if (battle.Monsters != null)
                    {
                        foreach (var m in battle.Monsters)
                        {
                            player.Send(Tools.FromFormat("bbbbb", 11, 1, m.GridX, m.GridY, 0));
                        }
                    }

                    // 5. Restore player HP upon revival
                    if (player.Eqs != null)
                    {
                        player.Eqs.CurHP = Math.Max(10, player.Eqs.FullHP / 2);
                        player.Eqs.Send8_1();
                    }

                    await Task.Delay(500);

                    // 6. Teleport to recorded respawn point or Starter Beach (Map 10036, X: 1038, Y: 2235)
                    WarpData respawnWarp;
                    if (player.RecordMap != null && player.RecordMap.DstMap > 0)
                    {
                        respawnWarp = new WarpData()
                        {
                            DstMap = player.RecordMap.DstMap,
                            DstX_Axis = player.RecordMap.DstX_Axis,
                            DstY_Axis = player.RecordMap.DstY_Axis
                        };
                    }
                    else if (player.ReturnSpawnMap != null && player.ReturnSpawnMap.DstMap > 0)
                    {
                        respawnWarp = new WarpData()
                        {
                            DstMap = player.ReturnSpawnMap.DstMap,
                            DstX_Axis = player.ReturnSpawnMap.DstX_Axis,
                            DstY_Axis = player.ReturnSpawnMap.DstY_Axis
                        };
                    }
                    else
                    {
                        // Fallback to Starter Beach
                        respawnWarp = new WarpData()
                        {
                            DstMap = 10036,
                            DstX_Axis = 1038,
                            DstY_Axis = 2235
                        };
                    }

                    player.CurMap?.Teleport(TeleportType.CmD, player, 0, respawnWarp);
                    player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "You were defeated in battle and transported to your spawn point!"));
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PvEBattle] Exception in EndBattleDefeat: {ex.Message}");
                }
            });
        }

        private static void EndBattleVictory(ActiveBattle battle)
        {
            Task.Run(async () =>
            {
                try
                {
                    Player player = battle?.Player;
                    if (player == null) return;

                    battle.IsFinished = true;
                    lock (_lock)
                    {
                        _activeBattles.Remove(player.CharID);
                    }

                    DebugSystem.Write($"[PvEBattle] Player {player.CharName} won battle against {battle.Monsters?.Count ?? 1} monsters!");

                    // Wait for final death animation
                    await Task.Delay(1200);

                    // 1. AC 11:12 [01] (Combat finish signal)
                    player.Send(Tools.FromFormat("bbb", 11, 12, 1));

                    // Aggregate EXP, Gold and Drops safely
                    uint totalExp = 0;
                    uint totalGold = 0;
                    if (battle.Monsters != null)
                    {
                        foreach (var m in battle.Monsters)
                        {
                            // Captured monsters do NOT grant EXP, Gold, or Item Drops
                            if (m.IsCaptured) continue;

                            totalExp += (uint)Math.Max(10, m.MonsterLevel * 15);
                            totalGold += (uint)Math.Max(5, m.MonsterLevel * 8);

                            // Monster item drops
                            try
                            {
                                var drops = MonsterDropManager.RollDrops(m.MonsterId, m.MonsterName ?? "Monster", m.MonsterLevel);
                                if (drops != null && drops.Count > 0 && player.Inv != null)
                                {
                                    foreach (var drop in drops)
                                    {
                                        if (drop != null)
                                        {
                                            player.Inv.AddItem(drop.ItemID, drop.Count);
                                            player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Obtained {drop.ItemName} x{drop.Count}!"));
                                            DebugSystem.Write($"[PvEBattle] Monster '{m.MonsterName}' dropped {drop.ItemName} x{drop.Count} for {player.CharName}.");
                                        }
                                    }
                                    player.Send(new SendPacket(player.Inv.GetAC23_5()));
                                }
                            }
                            catch (Exception dropEx)
                            {
                                DebugSystem.Write($"[PvEBattle] Drop roll exception: {dropEx.Message}");
                            }
                        }
                    }

                    if (player.Eqs != null && (totalGold > 0 || totalExp > 0))
                    {
                        player.Eqs.AddGold((int)totalGold);
                        player.Eqs.CurExp += (int)totalExp;
                    }

                    // Check Quest Battle Completion
                    try
                    {
                        // Quest 1005: Save Niss (Wolf Guard battle victory)
                        if (battle.Monsters != null && battle.Monsters.Any(m => m.MonsterId == 11066 || (m.MonsterName ?? "").ToLower().Contains("wolf guard")))
                        {
                            if (player.Quests != null && player.Quests.TryGetValue(1005, out var pq) && pq.State == QuestRelated.QuestState.InProgress)
                            {
                                pq.State = QuestRelated.QuestState.Completed;
                                pq.CompletedAt = DateTime.UtcNow;
                                QuestRelated.QuestManager.SavePlayerQuest(player, 1005);
                                QuestRelated.QuestManager.SendQuestUpdate(player, 1005, QuestRelated.QuestState.Completed);
                                QuestRelated.QuestManager.SendCompanionReward(player, 11066, "Niss");
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
                                    player.Quests[1010] = new QuestRelated.PlayerQuest(1010, QuestRelated.QuestState.InProgress, 1);
                                }
                                var pq = player.Quests[1010];
                                pq.State = QuestRelated.QuestState.Completed;
                                pq.CompletedAt = DateTime.UtcNow;
                                QuestRelated.QuestManager.SavePlayerQuest(player, 1010);
                                QuestRelated.QuestManager.SendQuestUpdate(player, 1010, QuestRelated.QuestState.Completed);
                                QuestRelated.QuestManager.SendCompanionReward(player, 14156, "Xaolan");
                                player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "You defeated the pirates and rescued Xaolan! She joined your party."));

                                // Despawn hijackers and Xaolan trapped event for player
                                player.Send(Tools.FromFormat("bbwb", 24, 1, 1010, 2));
                                player.Send(Tools.FromFormat("bbwb", 24, 5, 1010, 1));
                            }
                        }

                        // Quest 1012: Little Red Riding Hood (Wild Wolf victory)
                        if (battle.Monsters != null && battle.Monsters.Any(m => m.MonsterId == 17437 || (m.MonsterName ?? "").ToLower().Contains("wild wolf")))
                        {
                            if (player.Quests != null && player.Quests.TryGetValue(1012, out var pq) && pq.State == QuestRelated.QuestState.InProgress)
                            {
                                pq.State = QuestRelated.QuestState.Completed;
                                pq.CompletedAt = DateTime.UtcNow;
                                QuestRelated.QuestManager.SavePlayerQuest(player, 1012);
                                QuestRelated.QuestManager.SendQuestUpdate(player, 1012, QuestRelated.QuestState.Completed);
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

                    // 2. AC 22:6 [11, 0, 2] (Battle Result: 2=Victory)
                    player.Send(Tools.FromFormat("bbbbb", 22, 6, 11, 0, 2));

                    // 3. AC 22:5 [11 (2B), exp (2B), gold (2B)] (Reward Popup)
                    player.Send(Tools.FromFormat("bbwww", 22, 5, (ushort)11, (ushort)Math.Min(0xFFFF, totalExp), (ushort)Math.Min(0xFFFF, totalGold)));

                    // 4. AC 11:0 [char_id (4B), 0 (2B)] -> CLOSE BATTLE WINDOW
                    SendPacket p110 = new SendPacket();
                    p110.PackArray(new byte[] { 11, 0 });
                    p110.Pack32(player.CharID);
                    p110.Pack16(0);
                    player.Send(p110);

                    // 5. AC 11:1 Despawn all battle fighters from grid
                    player.Send(Tools.FromFormat("bbbbb", 11, 1, battle.PlayerGridX, battle.PlayerGridY, 0));
                    if (battle.Monsters != null)
                    {
                        foreach (var m in battle.Monsters)
                        {
                            player.Send(Tools.FromFormat("bbbbb", 11, 1, m.GridX, m.GridY, 0));
                        }
                    }

                    // 6. AC 6:2 [00] Return to map normal mode
                    player.Send(Tools.FromFormat("bbb", 6, 2, 0));

                    // 7. AC 20:8 (Release movement lock)
                    player.Send(Tools.FromFormat("bb", 20, 8));
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PvEBattle] Exception in EndBattleVictory: {ex.Message}");
                }
            });
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
