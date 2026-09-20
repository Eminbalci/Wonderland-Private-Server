$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $repo 'wlo.pserver.core/Game/Battle/PvEBattleManager.cs') -Raw
$start = $source.IndexOf('        public static void HandleBattleAction(')
$end = $source.IndexOf('        private static void TryExecuteTurn(', $start)
$handler = $source.Substring($start, $end - $start)
# Production handler unchanged; transport, skill lookup and turn execution are isolated doubles.
$harness = @"
using System;
using System.Collections.Generic;
using System.Linq;
namespace Network {
 public class SendPacket { public byte[] Data; }
 public class RecievePacket {
  readonly byte[] data; int pos;
  public RecievePacket(params byte[] bytes) { data = bytes; }
  public int Count => data.Length; public int GetPtr() => pos;
  public byte Unpack8() => data[pos++];
  public ushort Unpack16() { var v = BitConverter.ToUInt16(data,pos); pos+=2; return v; }
 }
 public static class Tools {
  public static SendPacket FromFormat(string f, params object[] values) => new SendPacket { Data = values.Select(Convert.ToByte).ToArray() };
 }
}
namespace Game {
 public class Player {
  public uint CharID; public string CharName;
  public List<Network.SendPacket> Packets = new List<Network.SendPacket>();
  public void Send(Network.SendPacket p) { Packets.Add(p); }
 }
}
namespace Game.Battle {
 using Network;
 public static class DebugSystem { public static void Write(string s) {} }
 public class BattleFighter {
  public byte GridX, GridY; public Player PlayerRef; public uint OwnerID; public bool IsDead; public string Name;
 }
 public class PendingAction {
  public BattleFighter Actor; public Player Player; public string ActionType;
  public ushort SkillId; public byte TargetGridX, TargetGridY;
 }
 public class ActiveBattle {
  public bool IsFinished, IsTurnProcessing;
  public List<BattleFighter> Attackers = new List<BattleFighter>(), Defenders = new List<BattleFighter>();
  public Dictionary<int,PendingAction> PendingActions = new Dictionary<int,PendingAction>();
  public int ExpectedActionCount = 2;
 }
 public static class SkillRelated {
  public class Skill {
   public string Name; public bool IsHeal, IsRevive, IsShield, IsHotBlooded, IsSpeedUp, IsVanish,
    IsFreeze, IsSleep, IsSeal, IsConfuse, IsPoison, IsParalyze;
  }
  public static class SkillManager { public static Skill GetSkill(ushort id) => null; }
 }
 public static class CombatSelectionChecks {
  static ActiveBattle battle; static Player player; static int executions,checks;
  static ActiveBattle GetBattle(Player p) => battle;
  static void BroadcastToBattle(ActiveBattle b, SendPacket p) { player.Send(p); }
  static void TryExecuteTurn(ActiveBattle b) { if(b.PendingActions.Count == b.ExpectedActionCount) executions++; }
  static void Check(bool ok,string s) { checks++; if(!ok) throw new Exception(s); }
  static void Reset(bool pvp=false) {
   executions=0; player=new Player { CharID=4510001, CharName="Mizaki" }; battle=new ActiveBattle();
   var list=pvp?battle.Defenders:battle.Attackers;
   list.Add(new BattleFighter {GridX=(byte)(pvp?1:4),GridY=2,PlayerRef=player,Name="Mizaki"});
   list.Add(new BattleFighter {GridX=(byte)(pvp?2:3),GridY=2,OwnerID=player.CharID,Name="Robinson"});
  }
  static void Submit(byte x,ushort skill=10001) {
   HandleBattleAction(player,1,new RecievePacket(x,2,2,3,(byte)skill,(byte)(skill>>8),0,0,1));
  }
  static PendingAction Action(byte x) => battle.PendingActions[(x<<8)|2];
  public static string Run() {
   Reset(); Submit(4);
   Check(executions==0 && battle.PendingActions.Count==1,"Turn started before pet selected");
   Check(player.Packets.Count==1 && player.Packets[0].Data.SequenceEqual(new byte[]{53,5,4,2}),"Character selection reset native pet menu");
   Submit(4,60021);
   Check(battle.PendingActions.Count==1 && Action(4).ActionType=="attack","Duplicate character defend became pet action");
   Check(player.Packets.Count==1 && executions==0,"Duplicate advanced turn");
   Submit(3);
   Check(executions==1 && Action(3).Actor.OwnerID==player.CharID,"Pet command did not select pet");
   Check(battle.PendingActions.Values.All(a=>a.ActionType=="attack" && a.SkillId==10001),"Attack/attack generated defense");
   Check(player.Packets.Count==2 && player.Packets[1].Data.SequenceEqual(new byte[]{53,5,3,2}),"Pet ACK/menu incorrect");
   Reset(); Submit(4,60021); Submit(3);
   Check(Action(4).ActionType=="defend" && Action(3).ActionType=="attack","Character defense leaked to pet");
   Reset(); Submit(4); Submit(3,60021);
   Check(Action(4).ActionType=="attack" && Action(3).ActionType=="defend","Pet defense leaked to character");
   Reset(); Submit(0); Submit(2);
   Check(battle.PendingActions.Count==0 && player.Packets.Count==0,"Invalid actor became owned action");
   battle.Attackers[1].OwnerID=999; Submit(3);
   Check(battle.PendingActions.Count==0,"Another player's pet accepted");
   Reset(); battle.Attackers[1].IsDead=true; Submit(3);
   Check(battle.PendingActions.Count==0,"Dead pet accepted");
   Reset(true); Submit(1); Submit(2);
   Check(executions==1 && Action(1).Actor.PlayerRef==player && Action(2).Actor.OwnerID==player.CharID,"PvP mapping failed");
   Reset(); battle.Attackers[0].IsDead=true; battle.ExpectedActionCount=1; Submit(3);
   Check(executions==1 && Action(3).ActionType=="attack","Living pet failed with dead owner");
   Reset(); Submit(3); Submit(4);
   Check(executions==1 && Action(3).Actor.OwnerID==player.CharID,"Pet-first changed actor");
   Reset(); battle.IsTurnProcessing=true; Submit(4);
   Check(battle.PendingActions.Count==0,"Input accepted during animation");
   Reset(); battle.IsFinished=true; Submit(4);
   Check(battle.PendingActions.Count==0,"Input accepted after battle");
   return "PASS: "+checks+" combat command checks. Native rendering not tested.";
  }
"@
Add-Type -TypeDefinition ($harness + [Environment]::NewLine + $handler + '} }')
[Game.Battle.CombatSelectionChecks]::Run()
