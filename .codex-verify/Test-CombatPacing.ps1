$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $repo 'wlo.pserver.core/Game/Battle/PvEBattleManager.cs') -Raw
$start = $source.IndexOf('        private static bool IsBasicAttack(')
$end = $source.IndexOf('        private static void ExecuteTurn(', $start)
$timing = $source.Substring($start, $end-$start)
$start = $source.IndexOf('        private static void EndBattleFlee(')
$end = $source.IndexOf('        private static void EndBattleVictory(', $start)
$flee = $source.Substring($start, $end-$start)
# Execute the unchanged production methods; substitute only clock and transport.
$harness = @"
using System;
using System.Collections.Generic;
using System.Linq;
using Task = CombatPacingChecks.Clock;
public static class CombatPacingChecks {
 public static class Clock {
  public static int Now, DelayMs;
  public static System.Threading.Tasks.Task Pending;
  static System.Threading.Tasks.TaskCompletionSource<bool> gate;
  public static void Run(Func<System.Threading.Tasks.Task> action) { Pending = action(); }
  public static System.Threading.Tasks.Task Delay(int ms) {
   DelayMs=ms; gate=new System.Threading.Tasks.TaskCompletionSource<bool>(); return gate.Task;
  }
  public static void Advance() { Now+=DelayMs; gate.SetResult(true); Pending.GetAwaiter().GetResult(); }
 }
 class SendPacket {
  public List<byte> Data=new List<byte>();
  public void PackArray(byte[] a) { Data.AddRange(a); }
  public void Pack32(uint a) { Data.AddRange(BitConverter.GetBytes(a)); }
  public void Pack16(ushort a) { Data.AddRange(BitConverter.GetBytes(a)); }
 }
 static class Tools {
  public static SendPacket FromFormat(string f,params object[] args) {
   var p=new SendPacket(); p.PackArray(args.Select(Convert.ToByte).ToArray()); return p;
  }
 }
 class Equipment { public void Send8_1() {} }
 class Player {
  public Equipment Eqs=new Equipment();
  public uint CharID; public int Saves,Cooldowns; public bool ThrowSave;
  public bool NativeEventActive { get; set; } public void CancelInteraction() { NativeEventActive=false; } public void ClearInteraction() { NativeEventActive=false; }
  public List<Tuple<int,byte[]>> Packets=new List<Tuple<int,byte[]>>();
  public void Send(SendPacket p) { Packets.Add(Tuple.Create(Clock.Now,p.Data.ToArray())); }
  public void SetBattleCooldown() { Cooldowns++; }
  public void SaveCharacterData() { Saves++; if(ThrowSave) throw new Exception("save fixture"); }
 }
 enum BattleFighterType { Player=2,Pet=4,Monster=7 }
 class Fighter { public bool IsDead; public BattleFighterType FighterType; public byte GridX,GridY; }
 class QuestBattleContext { public Action OnFlee; }
 class ActiveBattle {
  public QuestBattleContext QuestContext;
  public bool IsFinished; public int TimerCancels; public object EncounterNpc { get; set; }
  public List<Fighter> Attackers=new List<Fighter>(),Defenders=new List<Fighter>();
  public List<Player> AllPlayers=new List<Player>();
  public void CancelTurnTimer() { TimerCancels++; }
 }
 static object _lock=new object();
 static Dictionary<uint,ActiveBattle> _activeBattles=new Dictionary<uint,ActiveBattle>();
 static class DebugSystem { public static void Write(string s) {} }
 static void BroadcastToBattle(ActiveBattle b,SendPacket p) { foreach(var player in b.AllPlayers) player.Send(p); }
 static void BroadcastBattleState(Player p,bool on) {}
 static int checks;
 static void Check(bool ok,string name) { checks++; if(!ok) throw new Exception(name); }
 static ActiveBattle Setup() {
  Clock.Now=0; _activeBattles.Clear();
  var b=new ActiveBattle(); b.AllPlayers.Add(new Player {CharID=1}); b.AllPlayers.Add(new Player {CharID=2});
  foreach(var p in b.AllPlayers) _activeBattles[p.CharID]=b;
  b.Attackers.Add(new Fighter {FighterType=BattleFighterType.Player,GridX=4,GridY=2});
  b.Attackers.Add(new Fighter {FighterType=BattleFighterType.Pet,GridX=3,GridY=2});
  b.Attackers.Add(new Fighter {FighterType=BattleFighterType.Pet,GridX=3,GridY=3,IsDead=true});
  b.Defenders.Add(new Fighter {FighterType=BattleFighterType.Monster,GridX=2,GridY=1});
  return b;
 }
 public static string Run() {
  Check(GetAnimationDelayMs(11016)>GetAnimationDelayMs(10001),"Flame Attack waits longer than basic attack");
  Check(GetAnimationDelayMs(25221)>GetAnimationDelayMs(10001),"Pet Gale waits longer than basic attack");
  Check(GetAnimationDelayMs(0)==GetAnimationDelayMs(10001),"Zero skill uses basic attack pacing");
  Check(GetAnimationDelayMs(60021,"defend")<GetAnimationDelayMs(10001),"Defense has its own shorter pacing");
  Check(GetAnimationDelayMs(10008,"catch")>GetAnimationDelayMs(10001),"Catch has time for its animation");
  foreach(var type in new[]{"heal","buff","status"}) Check(GetAnimationDelayMs(10001,type)>GetAnimationDelayMs(10001),type+" pacing");
  var b=Setup(); EndBattleFlee(b);
  Check(b.IsFinished && b.TimerCancels==1,"Escape claimed and timer cancelled before async wait");
  Check(_activeBattles.Count==2,"Encounter remains blocked during departure");
  foreach(var p in b.AllPlayers) {
   Check(p.Packets.Count==2,"Exactly one native leave per living allied actor; no skill/fanfare/close yet");
   Check(p.Packets[0].Item2.SequenceEqual(new byte[]{11,1,3,2,0}),"Pet leaves first");
   Check(p.Packets[1].Item2.SequenceEqual(new byte[]{11,1,4,2,0}),"Character leaves once");
  }
  EndBattleFlee(b);
  Check(b.AllPlayers.All(p=>p.Packets.Count==2),"Repeated flee during animation is ignored");
  Check(Clock.DelayMs==GetAnimationDelayMs(60041,"flee") && !Clock.Pending.IsCompleted,"Close awaits flee pacing");
  Clock.Advance();
  foreach(var p in b.AllPlayers) {
   Check(p.Packets.Count==5 && p.Packets[2].Item2[0]==11 && p.Packets[2].Item2[1]==0,"Single close follows departure");
   Check(p.Packets.Skip(2).All(x=>x.Item1>=GetAnimationDelayMs(60041,"flee")),"Close and movement release occur after animation budget");
   Check(p.Cooldowns==1 && p.Saves==1,"Cooldown and save performed once");
  }
  Check(_activeBattles.Count==0,"Finished battle unregistered after departure");
  EndBattleFlee(b); Check(b.AllPlayers.All(p=>p.Packets.Count==5),"Repeat after completion is ignored");
  EndBattleFlee(null); Check(true,"Null battle is safe");
  b=Setup(); b.AllPlayers[0].ThrowSave=true; EndBattleFlee(b); Clock.Advance();
  Check(_activeBattles.Count==0,"Failure still unregisters finished encounter");
  b=Setup(); int callbacks=0;b.QuestContext=new QuestBattleContext {OnFlee=()=>callbacks++};
  EndBattleFlee(b);Check(callbacks==0,"quest continuation waits for flee animation");
  Clock.Advance();Check(callbacks==1&&_activeBattles.Count==0,"quest continuation runs once before completed cleanup");
  EndBattleFlee(b);Check(callbacks==1,"repeated flee cannot repeat quest result");
  b=Setup(); b.QuestContext=new QuestBattleContext();
  foreach(var p in b.AllPlayers) p.NativeEventActive=true;
  EndBattleFlee(b);Check(b.AllPlayers.All(p=>p.NativeEventActive),"quest stays locked during departure animation");
  Clock.Advance();Check(b.AllPlayers.All(p=>!p.NativeEventActive),"quest without flee continuation releases every participant");
  Check(b.AllPlayers.All(p=>p.Packets.Count==5),"quest cleanup adds no duplicate close or location banner");
  b=Setup();foreach(var p in b.AllPlayers) p.NativeEventActive=true;bool activeAtCallback=false;
  b.QuestContext=new QuestBattleContext {OnFlee=()=>{activeAtCallback=b.AllPlayers.All(p=>p.NativeEventActive);foreach(var p in b.AllPlayers)p.ClearInteraction();}};
  EndBattleFlee(b);Clock.Advance();Check(activeAtCallback,"authored flee continuation keeps its event state until callback");
  Check(b.AllPlayers.All(p=>!p.NativeEventActive),"authored continuation remains responsible for cleanup");
  return checks+" combat pacing/escape checks passed";
 }
$timing
$flee
}
"@
$provider = Get-Content (Join-Path $repo 'wlo.pserver.core/Game/SkillRelated/SkillAnimationTiming.cs') -Raw
$provider = $provider.Substring($provider.IndexOf('namespace Game.SkillRelated'))
$providerDependencies = @"
namespace RCLibrary.Core { public static class PathHelper { public static string GetDataFilePath(string name) { return System.IO.Path.Combine(@"$repo", "Data", name); } } public static class DebugSystem { public static void Write(string message) {} } }
"@
Add-Type -TypeDefinition ("using System.IO; using RCLibrary.Core;`n" + $harness + $provider + $providerDependencies)
[CombatPacingChecks]::Run()
# Verify call-site ordering too: removing a caught fighter must not interrupt its animation.
$catchStart=$source.IndexOf('// ---- Phase 3: Catch Actions ----')
$catchEnd=$source.IndexOf('// ---- Phase 4: Attack', $catchStart)
$catchCode=$source.Substring($catchStart,$catchEnd-$catchStart)
$wait=$catchCode.IndexOf('await Task.Delay(GetAnimationDelayMs(10008, "catch"))')
$result=$catchCode.IndexOf('targetMonster.MonsterHP = 0')
if($wait -lt 0 -or $result -lt $wait -or $catchCode.Contains('11, 1, targetFighter.GridX')) { throw 'Capture must finish before granting the pet, without a duplicate leave animation' }
$start=$source.IndexOf('private static void ExecuteTurn(')
$end=$source.IndexOf('public static void HandleFlee(', $start)
$turn=$source.Substring($start,$end-$start)
if($turn -match 'Task.Delay\(\d+\)' -or $turn.Contains('Pack16(60041)')) { throw 'Old fixed action delay or duplicate flee animation remains' }
if(!$turn.Contains('comboPair.Max(a => GetAnimationDelayMs(a.SkillId, a.ActionType))')) { throw 'Combo does not use the longest skill budget' }
'3 packet-order/pacing source checks passed'
