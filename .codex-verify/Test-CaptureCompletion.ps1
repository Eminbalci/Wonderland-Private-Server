$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $repo 'wlo.pserver.core/Game/Battle/PvEBattleManager.cs') -Raw
$start=$source.IndexOf('                    var catchActions =')
$end=$source.IndexOf('                    // ---- Phase 4:', $start)
$phase=$source.Substring($start,$end-$start)
$start=$source.IndexOf('        private static bool IsBasicAttack(')
$end=$source.IndexOf('        private static void ExecuteTurn(', $start)
$timing=$source.Substring($start,$end-$start)
$harness=@"
using System;
using System.Linq;
using System.Collections.Generic;
public static class CaptureCompletionChecks {
 class SendPacket {
  public List<byte> Data=new List<byte>(); public string Text;
  public void PackArray(byte[] x){Data.AddRange(x);} public void Pack8(byte x){Data.Add(x);}
  public void Pack16(ushort x){Data.AddRange(BitConverter.GetBytes(x));}
  public void Pack32(uint x){Data.AddRange(BitConverter.GetBytes(x));}
 }
 class Tools {public static SendPacket FromFormat(string format,params object[] args){return new SendPacket{Data=args.Take(2).Select(Convert.ToByte).ToList(),Text=args.Last() as string};}}
 class DebugSystem {public static List<string> Lines=new List<string>(); public static void Write(string x){Lines.Add(x);}}
 class Eqs {public int Level=7;}
 class Player {
  public Eqs Eqs=new Eqs(); public Dictionary<byte,PlayerPetData> PlayerPets=new Dictionary<byte,PlayerPetData>();
  public List<SendPacket> Packets=new List<SendPacket>(); public void Send(SendPacket p){Packets.Add(p);}
  public static bool IsSamePetOrCompanion(uint a,uint b){return a==b;}
  public bool RegisterClientPet(PlayerPetData pet){return true;}
  public class PlayerPetData {
   public byte Slot,Level,Amity; public uint PetID; public string PetName; public int HP,MaxHP,SP,MaxSP;
   public bool IsBattle,IsRide; public int Str,Con,Int,Wis,Agi,Exp,Reborn,Job;
   public void InitializeBaseStats(){} public void NormalizeClientStats(bool x){}
  }
 }
 class QuestRelated {public class QuestManager {
  public static SendPacket CreatePetPacket(params object[] x){return new SendPacket{Data=new List<byte>{15,1}};}
  public static void SendPetProgression(Player p,Player.PlayerPetData pet){}
 }}
 class Monster {public int MonsterLevel=2,MonsterHP=40,MonsterMaxHP=100,MonsterMaxSP=50; public uint MonsterId=17003; public string MonsterName="Grape Mons"; public bool IsCaptured;}
 class Fighter {public byte GridX,GridY; public int CurHP=40; public bool IsDead{get{return CurHP<=0;}} public bool CanAct=true,IsCaptured; public Monster MonsterRef;}
 class PendingAction {public string ActionType="catch"; public Fighter Actor; public byte TargetGridX=2,TargetGridY=1; public Player Player;}
 class Battle {public bool IsPvP; public List<SendPacket> Packets=new List<SendPacket>();}
 class FixedRandom {public double Roll; public double NextDouble(){return Roll;}}
 static FixedRandom _rng=new FixedRandom();
 static void BroadcastToBattle(Battle b,SendPacket p){b.Packets.Add(p);}
 class Task {
  public static int Milliseconds; public static System.Threading.Tasks.TaskCompletionSource<bool> Gate;
  public static System.Threading.Tasks.Task Delay(int milliseconds){Milliseconds=milliseconds; Gate=new System.Threading.Tasks.TaskCompletionSource<bool>();return Gate.Task;}
 }
 static async System.Threading.Tasks.Task RunPhase(Battle battle,List<PendingAction> actions,List<Fighter> opposingFighters) {
  var stepActions=actions;
$phase
 }
$timing
 static int checks;
 static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
 static void Attempt(bool success,double roll,int level,int hp,uint[] roster,string failureText,int attempts=1,byte actorX=4) {
  var p=new Player(); p.Eqs.Level=level;
  foreach(uint id in roster) {byte slot=(byte)(p.PlayerPets.Count+1);p.PlayerPets[slot]=new Player.PlayerPetData{PetID=id,Slot=slot};}
  var monster=new Monster{MonsterHP=hp}; var target=new Fighter{GridX=2,GridY=1,MonsterRef=monster,CurHP=hp};
  int initialCount=p.PlayerPets.Count;
  for(int i=0;i<attempts;i++) {
   var battle=new Battle();_rng.Roll=roll;
   var action=new PendingAction{Actor=new Fighter{GridX=actorX,GridY=2},Player=p};
   var work=RunPhase(battle,new List<PendingAction>{action},new List<Fighter>{target});
   Check(!work.IsCompleted && Task.Milliseconds==6000,"Capture must await the complete result and return budget");
   Check(p.Packets.Count==0 && p.PlayerPets.Count==initialCount && target.CurHP==hp,"No roster/message/HP mutation before completion");
   var anim=battle.Packets.Single(x=>x.Data[0]==50 && x.Data[1]==1);
   Check(anim.Data[4]==actorX && anim.Data[5]==2,"Capture animation belongs to selected character or pet");
   Check(BitConverter.ToUInt16(anim.Data.ToArray(),6)==(success?10008:10009),"Native animation matches outcome");
   Task.Gate.SetResult(true);work.GetAwaiter().GetResult();
   Check(!battle.Packets.Any(x=>x.Data[0]==11),"No second leave animation after native capture");
   if(success) {
    Check(p.PlayerPets.Count==initialCount+1 && p.PlayerPets.Values.Count(x=>x.PetID==17003)==1,"Exactly one captured pet added");
    Check(target.CurHP==0 && monster.MonsterHP==0 && target.IsCaptured && monster.IsCaptured,"Successful target removed from server combat");
    Check(p.Packets.Count(x=>x.Data[0]==15)==1,"Roster spawn sent once");
   } else {
    Check(target.CurHP==hp && monster.MonsterHP==hp && !target.IsCaptured && !monster.IsCaptured,"Failed target retains HP and capture flags");
    Check(p.PlayerPets.Count==initialCount && !p.Packets.Any(x=>x.Data[0]==15),"Failure cannot grant a pet");
    Check(p.Packets.Last().Text.Contains(failureText),"Failure explains actual reason");
   }
   p.Packets.Clear();
  }
 }
 public static string Run() {
  Attempt(true,.979,7,49,new uint[]{12032},null);
  Attempt(false,.98,7,49,new uint[]{12032},"resisted");
  Attempt(false,0,7,1,new uint[]{12032,14156,17003,17003},"full (4/4), and you already have",3);
  Attempt(false,0,7,1,new uint[]{12032,14156,14002,14001},"full (4/4). Make room");
  Attempt(false,0,7,1,new uint[]{12032,17003},"already have");
  Attempt(true,.86,2,40,new uint[]{12032},null);
  Attempt(false,.87,2,40,new uint[]{12032},"resisted");
  Attempt(true,0,7,40,new uint[]{17162},null,1,3);
  Attempt(false,.99,7,40,new uint[]{17162},"resisted",1,3);
  Attempt(false,0,7,40,new uint[]{17162,17003},"already have",1,3);
  Attempt(false,0,7,40,new uint[]{17162,12032,14156,14001},"full (4/4)",1,3);
  foreach(string skipped in new[]{"dead actor","stunned actor","dead target"}) {
   var player=new Player(); var actor=new Fighter{GridX=4,GridY=2};
   var target=new Fighter{GridX=2,GridY=1,CurHP=40,MonsterRef=new Monster()};
   var other=new Fighter{GridX=2,GridY=2,CurHP=40,MonsterRef=new Monster()};
   if(skipped=="dead actor") actor.CurHP=0;
   if(skipped=="stunned actor") actor.CanAct=false;
   if(skipped=="dead target") {target.CurHP=0;target.MonsterRef.MonsterHP=0;}
   var battle=new Battle();
   var work=RunPhase(battle,new List<PendingAction>{new PendingAction{Actor=actor,Player=player}},new List<Fighter>{target,other});
   Check(work.IsCompleted && !work.IsFaulted,"Capture skips invalid actor/target after a faster turn");
   Check(battle.Packets.Count==0 && player.Packets.Count==0 && player.PlayerPets.Count==0 && other.CurHP==40,"Skipped capture cannot switch to another living monster");
  }
  Check(DebugSystem.Lines.Any(x=>x.Contains("chance=98.0%")),"Diagnostic records calculated chance");
  return checks+" capture completion/outcome checks passed";
 }
}
"@
$provider = Get-Content (Join-Path $repo 'wlo.pserver.core/Game/SkillRelated/SkillAnimationTiming.cs') -Raw
$provider = $provider.Substring($provider.IndexOf('namespace Game.SkillRelated'))
$providerDependencies = @"
namespace RCLibrary.Core { public static class PathHelper { public static string GetDataFilePath(string name) { return System.IO.Path.Combine(@"$repo", "Data", name); } } public static class DebugSystem { public static void Write(string message) {} } }
"@
Add-Type -TypeDefinition ("using System.IO; using RCLibrary.Core;`n" + $harness + $provider + $providerDependencies) -CompilerOptions /nowarn:0649
[CaptureCompletionChecks]::Run()
