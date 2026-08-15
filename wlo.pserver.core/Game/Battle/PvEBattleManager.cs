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
    public class ActiveBattle
    {
        public Player Player { get; set; }
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

        public byte PlayerGridX { get; set; } = 4;
        public byte PlayerGridY { get; set; } = 2;
        public byte MonsterGridX { get; set; } = 2;
        public byte MonsterGridY { get; set; } = 2;

        public int Turn { get; set; } = 0;
        public bool IsFinished { get; set; } = false;
    }

    public static class PvEBattleManager
    {
        private static readonly Dictionary<uint, ActiveBattle> _activeBattles = new Dictionary<uint, ActiveBattle>();
        private static readonly object _lock = new object();

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
                    realMonsterTID = (targetNpcId & 0xFFFF) > 0 ? (targetNpcId & 0xFFFF) : 14001;
                    monName = dbInfo.Name;
                    monLevel = Math.Max(1, dbInfo.Level);
                    monMaxHP = dbInfo.HP > 0 ? dbInfo.HP : (monLevel * 30 + 100);
                    monElement = (byte)dbInfo.Element;
                }
            }

            // Fallback: If template ID is invalid or not in range, use default monster TID
            if (realMonsterTID == 0 || (realMonsterTID > 20000 && mapNpc == null))
            {
                realMonsterTID = 14001;
            }

            int monMaxSP = monLevel * 20 + 50;
            int monAtk = (int)Math.Round(monLevel * 1.5 + 5);
            int monDef = (int)Math.Round(monLevel * 1.2 + 3);
            int monSpd = (int)Math.Round(monLevel * 1.3 + 4);

            ActiveBattle battle = new ActiveBattle
            {
                Player = player,
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
                ClickId = realClickId
            };

            lock (_lock)
            {
                _activeBattles[player.CharID] = battle;
            }

            DebugSystem.Write($"[PvEBattle] Starting PvE battle for {player.CharName} vs {battle.MonsterName} (TID: {realMonsterTID}, ClickID: {realClickId}, Lv: {monLevel}, HP: {monMaxHP})");

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
            p250.Pack8(4); // grid x
            p250.Pack8(2); // grid y
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

            // 5. AC 11:5 (Monster entity)
            SendPacket p5 = new SendPacket();
            p5.PackArray(new byte[] { 11, 5 });
            p5.Pack8(1); // role = 1
            p5.Pack8(7); // ftype = 7 (monster)
            p5.Pack32((uint)realMonsterTID);
            p5.Pack16(realClickId);
            p5.Pack32(0); // owner_id
            p5.Pack8(2); // grid x
            p5.Pack8(2); // grid y
            p5.Pack32((uint)monMaxHP);
            p5.Pack16((ushort)Math.Min(0xFFFF, monMaxSP));
            p5.Pack32((uint)monMaxHP);
            p5.Pack16((ushort)Math.Min(0xFFFF, monMaxSP));
            p5.Pack8((byte)Math.Min(255, monLevel));
            p5.Pack8(monElement);
            p5.Pack8(0); // reborn
            p5.Pack8(0); // job
            p5.Pack16(0); // trailing pad
            player.Send(p5);

            // 6. AC 51:1 Sync HP/SP
            SendStatSync(player, 4, 2, 0x19, (uint)player.Eqs.CurHP);
            SendStatSync(player, 4, 2, 0x1a, (uint)player.Eqs.CurSP);
            SendStatSync(player, 2, 2, 0x19, (uint)monMaxHP);
            SendStatSync(player, 2, 2, 0x1a, (uint)monMaxSP);

            // 7. AC 50:6 Give turn
            player.Send(Tools.FromFormat("bbbbb", 50, 6, 4, 2, 0));

            // 8. AC 52:1 Show combat menu
            player.Send(Tools.FromFormat("bb", 52, 1));
        }

        public static void HandleBattleAction(Player player, byte sub, RecievePacket r)
        {
            ActiveBattle battle = GetBattle(player);
            if (battle == null || battle.IsFinished) return;

            if (sub == 5) // Flee
            {
                HandleFlee(player);
                return;
            }

            if (sub == 4) // Defend
            {
                ProcessTurn(battle, playerAction: "defend", skillId: 60021);
                return;
            }

            // sub 1 / default (Attack or Skill)
            ushort skillId = 10001; // Basic Attack
            try
            {
                if ((r.Count - r.GetPtr()) >= 4)
                {
                    byte srcX = r.Unpack8();
                    byte srcY = r.Unpack8();
                    byte dstX = r.Unpack8();
                    byte dstY = r.Unpack8();
                }
                if ((r.Count - r.GetPtr()) >= 2)
                {
                    ushort unpackedSkill = r.Unpack16();
                    if (unpackedSkill > 0) skillId = unpackedSkill;
                }
            }
            catch { }

            ProcessTurn(battle, playerAction: "attack", skillId: skillId);
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

                    // 6. AC 11:1 Despawn fighter
                    player.Send(Tools.FromFormat("bbbbb", 11, 1, battle.PlayerGridX, battle.PlayerGridY, 0));

                    // 7. AC 20:8 Release movement lock
                    player.Send(Tools.FromFormat("bb", 20, 8));
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PvEBattle] Exception in HandleFlee: {ex.Message}");
                }
            });
        }

        private static void ProcessTurn(ActiveBattle battle, string playerAction, ushort skillId)
        {
            Task.Run(async () =>
            {
                try
                {
                    Player player = battle.Player;
                    if (player == null || battle.IsFinished) return;

                    battle.Turn++;

                    // Calculate player damage
                    int playerDmg = Math.Max(10, (player.Eqs.FullAtk * 2) - battle.MonsterDef);
                    if (skillId > 10001)
                    {
                        playerDmg = Math.Max(15, (int)(player.Eqs.FullAtk * 2.8) - (battle.MonsterDef / 2));
                        player.Eqs.CurSP = Math.Max(0, player.Eqs.CurSP - 10);
                        SendStatSync(player, battle.PlayerGridX, battle.PlayerGridY, 0x1a, (uint)player.Eqs.CurSP);
                    }
                    if (playerAction == "defend") playerDmg = 0;

                    // 1. Player attacks monster
                    if (playerAction != "defend")
                    {
                        // Action notification
                        player.Send(Tools.FromFormat("bbbb", 53, 5, battle.PlayerGridX, battle.PlayerGridY));

                        // Animation packet
                        SendPacket pAnim = new SendPacket();
                        pAnim.PackArray(new byte[] { 50, 1 });
                        pAnim.PackArray(new byte[] { 0x11, 0x00 });
                        pAnim.Pack8(battle.PlayerGridX); pAnim.Pack8(battle.PlayerGridY);
                        pAnim.Pack16(skillId > 0 ? skillId : (ushort)10001);
                        pAnim.Pack8(0);
                        pAnim.Pack8(1);
                        pAnim.Pack8(battle.MonsterGridX); pAnim.Pack8(battle.MonsterGridY);
                        pAnim.Pack8(1); pAnim.Pack8(0); pAnim.Pack8(1);
                        pAnim.Pack8(0x19); // HP damage
                        pAnim.Pack32((uint)playerDmg);
                        pAnim.Pack8(1);
                        player.Send(pAnim);

                        battle.MonsterHP = Math.Max(0, battle.MonsterHP - playerDmg);
                        SendStatSync(player, battle.MonsterGridX, battle.MonsterGridY, 0x19, (uint)battle.MonsterHP);
                    }

                    // Wait for player animation to play out
                    await Task.Delay(1500);

                    // Check if monster is defeated
                    if (battle.MonsterHP <= 0)
                    {
                        EndBattleVictory(battle);
                        return;
                    }

                    // 2. Monster attacks player
                    int monsterDmg = Math.Max(5, (int)(battle.MonsterAtk * 1.2) - (playerAction == "defend" ? player.Eqs.FullDef * 2 : player.Eqs.FullDef));
                    monsterDmg = Math.Max(1, monsterDmg);

                    player.Send(Tools.FromFormat("bbbb", 53, 5, battle.MonsterGridX, battle.MonsterGridY));

                    SendPacket mAnim = new SendPacket();
                    mAnim.PackArray(new byte[] { 50, 1 });
                    mAnim.PackArray(new byte[] { 0x11, 0x00 });
                    mAnim.Pack8(battle.MonsterGridX); mAnim.Pack8(battle.MonsterGridY);
                    mAnim.Pack16(10001);
                    mAnim.Pack8(0);
                    mAnim.Pack8(1);
                    mAnim.Pack8(battle.PlayerGridX); mAnim.Pack8(battle.PlayerGridY);
                    mAnim.Pack8(1); mAnim.Pack8(0); mAnim.Pack8(1);
                    mAnim.Pack8(0x19); // HP damage
                    mAnim.Pack32((uint)monsterDmg);
                    mAnim.Pack8(1);
                    player.Send(mAnim);

                    player.Eqs.CurHP = Math.Max(1, player.Eqs.CurHP - monsterDmg);
                    SendStatSync(player, battle.PlayerGridX, battle.PlayerGridY, 0x19, (uint)player.Eqs.CurHP);

                    // Wait for monster attack animation
                    await Task.Delay(1400);

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

        private static void EndBattleVictory(ActiveBattle battle)
        {
            Task.Run(async () =>
            {
                try
                {
                    Player player = battle.Player;
                    if (player == null) return;

                    battle.IsFinished = true;
                    lock (_lock)
                    {
                        _activeBattles.Remove(player.CharID);
                    }

                    DebugSystem.Write($"[PvEBattle] Player {player.CharName} defeated {battle.MonsterName}!");

                    // 1. Monster HP = 0
                    SendStatSync(player, battle.MonsterGridX, battle.MonsterGridY, 0x19, 0);

                    // 2. Wait for monster death animation (1.8s)
                    await Task.Delay(1800);

                    // 3. AC 11:12 [01] (Combat finish signal)
                    player.Send(Tools.FromFormat("bbb", 11, 12, 1));

                    // Reward EXP and Gold
                    uint expGain = (uint)Math.Max(10, battle.MonsterLevel * 15);
                    uint goldGain = (uint)Math.Max(5, battle.MonsterLevel * 8);
                    player.Eqs.AddGold((int)goldGain);
                    player.CurExp += (int)expGain;

                    // 4. AC 22:6 [11, 0, 2] (Battle Result: 2=Victory)
                    player.Send(Tools.FromFormat("bbbbb", 22, 6, 11, 0, 2));

                    // 5. AC 22:5 [11 (2B), exp (2B), gold (2B)] (Reward Popup)
                    player.Send(Tools.FromFormat("bbwww", 22, 5, (ushort)11, (ushort)Math.Min(0xFFFF, expGain), (ushort)Math.Min(0xFFFF, goldGain)));

                    // 6. AC 11:0 [char_id (4B), 0 (2B)] -> CLOSE BATTLE WINDOW
                    SendPacket p110 = new SendPacket();
                    p110.PackArray(new byte[] { 11, 0 });
                    p110.Pack32(player.CharID);
                    p110.Pack16(0);
                    player.Send(p110);

                    // 7. AC 11:1 [4, 2, 0] -> Despawn battle fighter
                    player.Send(Tools.FromFormat("bbbbb", 11, 1, battle.PlayerGridX, battle.PlayerGridY, 0));

                    // 8. AC 20:8 (Release movement lock)
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
