param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'main-bin'))
$ErrorActionPreference='Stop'
foreach($assembly in @('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory $assembly)) }
$harness=@"
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Game.Battle;
public static class CombatSpeedChecks {
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static PendingAction Action(string name,string type,int speed,byte x,byte y=2) {
  return new PendingAction{Actor=new BattleFighter{Name=name,Spd=speed,GridX=x,GridY=y},ActionType=type,TargetGridX=2,TargetGridY=1};
 }
 static List<List<PendingAction>> Order(List<PendingAction> actions,List<BattleFighter> monsters,bool pvp=false) {
  return (List<List<PendingAction>>)typeof(PvEBattleManager).GetMethod("BuildSpeedOrderedActions",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{actions,monsters,pvp});
 }
 static string Names(List<List<PendingAction>> steps){return string.Join(",",steps.SelectMany(x=>x).Select(x=>x.Actor.Name));}
 public static string Run() {
  var capture=Action("capture","catch",20,4);var pet=Action("pet","attack",30,3);var slow=Action("slow","attack",10,4,3);
  var monster=Action("monster","monster",25,2,1).Actor;
  var monsters=new List<BattleFighter>{monster};
  var ordered=Order(new List<PendingAction>{capture,pet},monsters);
  Check(Names(ordered)=="pet,monster,capture","Catch cannot jump ahead of faster pet or monster");
  Check(Names(Order(new List<PendingAction>{pet,capture},monsters))==Names(ordered),"Submission order does not prioritize capture");
  capture.Actor.Spd=40;
  Check(Names(Order(new List<PendingAction>{pet,capture},monsters))=="capture,pet,monster","Fast capture legitimately goes first");
  capture.Actor.Spd=20;
  ordered=Order(new List<PendingAction>{slow,capture,pet},new List<BattleFighter>());
  Check(ordered.Count==3 && Names(ordered)=="pet,capture,slow","Combo cannot pull slower actor across a capture");
  monster.Spd=20;
  ordered=Order(new List<PendingAction>{slow,pet},monsters);
  Check(ordered.Count==3 && Names(ordered)=="pet,monster,slow","Combo cannot jump over an enemy turn");
  ordered=Order(new List<PendingAction>{slow,pet},new List<BattleFighter>());
  Check(ordered.Count==1 && ordered[0].Count==2,"Adjacent offensive actors retain dual combo");
  slow.TargetGridY=3;
  Check(Order(new List<PendingAction>{slow,pet},new List<BattleFighter>()).Count==2,"Different targets do not combo");
  var heal=Action("heal","heal",15,4,4);var buff=Action("buff","buff",35,4,5);
  Check(Names(Order(new List<PendingAction>{heal,capture,buff,pet},new List<BattleFighter>()))=="buff,pet,capture,heal","Support uses actor SPD too");
  var defense=Action("defend","defend",100,4,1);var flee=Action("flee","flee",100,4,6);
  Check(Order(new List<PendingAction>{defense,flee,capture},monsters,true).SelectMany(x=>x).Count()==1,"Defense/flee stay outside SPD execution and PvP does not invent monster turns");
  var tie=Action("tie","catch",20,3,3);
  Check(Names(Order(new List<PendingAction>{capture,tie},monsters))==Names(Order(new List<PendingAction>{tie,capture},monsters)),"Ties use stable grid order");
  var random=new Random(27);
  for(int i=0;i<100;i++) {
   var a=Action("a","catch",random.Next(1,100),4);var b=Action("b","attack",random.Next(1,100),3);
   monster.Spd=random.Next(1,100);
   var flat=Order(new List<PendingAction>{a,b},monsters).SelectMany(x=>x).ToList();
   Check(flat.Count==3 && flat.Select(x=>x.Actor).Distinct().Count()==3,"Each participant scheduled exactly once");
   Check(flat.Zip(flat.Skip(1),(x,y)=>x.Actor.Spd>=y.Actor.Spd).All(x=>x),"Mixed sequence descends by SPD");
  }
  return checks+" compiled SPD scheduling checks passed";
 }
}
"@
Add-Type -TypeDefinition $harness -ReferencedAssemblies @((Join-Path $BuildDirectory 'wlo.pserver.core.dll'),(Join-Path $BuildDirectory 'RCLibrary.dll'),'System.Core.dll')
[CombatSpeedChecks]::Run()
