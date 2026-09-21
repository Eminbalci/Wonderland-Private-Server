$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $repo 'wlo.pserver.core/Game/Battle/PvEBattleManager.cs') -Raw
function Slice([string]$from,[string]$to,[int]$offset=0) {
 $a=$source.IndexOf($from,$offset); if($a -lt 0) { throw "Missing $from" }
 $b=$source.IndexOf($to,$a); if($b -lt 0) { throw "Missing $to" }
 return $source.Substring($a,$b-$a)
}
$phase=$source.IndexOf('// ---- Phase 3: Catch Actions ----')
$catchPacket=Slice '                            SendPacket pAnim = new SendPacket();' '                            BroadcastToBattle(battle, pAnim);' $phase
$defend=Slice '                    var defendActions = ' '                    foreach (var stepActions in BuildSpeedOrderedActions('
$hit=Slice '                                pAttackAnim.PackArray(new byte[] { 0x11, 0x00 });' "`r`n                                }"
$monster=Slice '                            SendPacket mAnim = new SendPacket();' '                            BroadcastToBattle(battle, mAnim);'
$timing=Slice '        private static bool IsBasicAttack(' '        private static void ExecuteTurn('
$harness=@"
using System;
using System.Linq;
using System.Collections.Generic;
public static class DefenseCaptureChecks {
 class SendPacket {
  public List<byte> Data=new List<byte>();
  public void PackArray(byte[] bytes) {Data.AddRange(bytes);}
  public void Pack8(byte n){Data.Add(n);}
  public void Pack16(ushort n){Data.AddRange(BitConverter.GetBytes(n));}
  public void Pack32(uint n){Data.AddRange(BitConverter.GetBytes(n));}
 }
 class Fighter {public byte GridX,GridY; public bool IsDead; public bool CanAct=true;}
 class Action {public Fighter Actor; public string ActionType; public ushort SkillId=11016;}
 static byte[] Capture(bool catchSuccess) {
  var actor=new Fighter {GridX=4,GridY=2}; var targetFighter=new Fighter {GridX=2,GridY=1};
$catchPacket
  return pAnim.Data.ToArray();
 }
 static byte[] Hit(HashSet<int> defendingActors,byte targetX,bool fromMonster) {
  var actor=new Fighter {GridX=4,GridY=2}; var targetFighter=new Fighter {GridX=targetX,GridY=2};
  var target=targetFighter; var monster=new Fighter {GridX=2,GridY=1};
  int finalDmg=37,rawDmg=37; bool isCritical=false; var a=new Action();
  if(fromMonster) {
$monster
   return mAnim.Data.ToArray();
  }
  var pAttackAnim=new SendPacket(); pAttackAnim.PackArray(new byte[]{50,1});
$hit
  return pAttackAnim.Data.ToArray();
 }
 static HashSet<int> Defend(List<Action> actions) {
  var defendingActors=new HashSet<int>();
$defend
  return defendingActors;
 }
 static int checks;
 static void Check(bool ok,string text){checks++; if(!ok) throw new Exception(text);}
 public static string Run() {
  var success=Capture(true); var fail=Capture(false);
  Check(success.Length==21 && fail.Length==21,"Native capture record length");
  Check(BitConverter.ToUInt16(success,6)==10008,"Success uses Capture");
  Check(BitConverter.ToUInt16(fail,6)==10009,"Failure uses Fail to Capture");
  Check(success.Where((v,i)=>i!=6 && i!=7).SequenceEqual(fail.Where((v,i)=>i!=6 && i!=7)),"Result only changes animation ID, not HP/grid fields");
  Check(BitConverter.ToUInt32(fail,16)==0,"Failed capture carries no damage");
  var chosen=Defend(new List<Action>{
   new Action {Actor=new Fighter {GridX=4,GridY=2},ActionType="defend"},
   new Action {Actor=new Fighter {GridX=3,GridY=2},ActionType="attack"},
   new Action {Actor=new Fighter {GridX=4,GridY=3,IsDead=true},ActionType="defend"},
   new Action {Actor=new Fighter {GridX=4,GridY=4,CanAct=false},ActionType="defend"}
  });
  Check(chosen.SetEquals(new[]{(4<<8)|2}),"Only living, eligible actor choosing defend is registered");
  foreach(bool mob in new[]{true,false}) {
   var defended=Hit(chosen,4,mob); var other=Hit(chosen,3,mob);
   Check(defended[13]==1 && other[13]==0,"Defend reaction belongs to attacked actor only");
   Check(BitConverter.ToUInt32(defended,16)==37,"Reaction flag does not apply damage twice");
   Check(Hit(new HashSet<int>(),4,mob)[13]==0,"No defend choice means normal hit reaction");
   Check(Hit(new HashSet<int>{(3<<8)|2},3,mob)[13]==1,"Pet can defend independently");
  }
  Check(GetAnimationDelayMs(10001)>=1600,"Basic attack retains requested pacing");
  Check(GetAnimationDelayMs(11016)>=3400 && GetAnimationDelayMs(25221)>3400,"Player and pet spells use per-ID budgets with a 3400ms minimum");
  Check(GetAnimationDelayMs(10008,"catch")==6000,"Capture includes result and return movement");
  Check(GetAnimationDelayMs(60041,"flee")==2500,"Flee shortened 500ms");
  foreach(var t in new[]{"heal","buff","status"}) Check(GetAnimationDelayMs(11016,t)==3400,t+" uses requested 3400ms");
  return checks+" defense/capture/pacing checks passed";
 }
$timing
}
"@
$provider = Get-Content (Join-Path $repo 'wlo.pserver.core/Game/SkillRelated/SkillAnimationTiming.cs') -Raw
$provider = $provider.Substring($provider.IndexOf('namespace Game.SkillRelated'))
$providerDependencies = @"
namespace RCLibrary.Core { public static class PathHelper { public static string GetDataFilePath(string name) { return System.IO.Path.Combine(@"$repo", "Data", name); } } public static class DebugSystem { public static void Write(string message) {} } }
"@
Add-Type -TypeDefinition ("using System.IO; using RCLibrary.Core;`n" + $harness + $provider + $providerDependencies)
[DefenseCaptureChecks]::Run()
if($defend -match 'Pack|Broadcast|Task.Delay|AddStatus') {throw 'Defend must not cast or add a skill shield during selection'}
'1 defend-phase source check passed'
