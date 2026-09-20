param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'critical-hit-bin'))
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','Wonderland Private Server.exe') | ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
$code=@"
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Net.Sockets;
using DataFiles;
using Game;
using Game.Code;
using Game.Battle;
using Network;
using RCLibrary.Core.Networking;
public class CritSocket : SocketClient {
 public ConcurrentQueue<byte[]> Packets;
 public override void SendPacket(IPacket p){Packets.Enqueue(p.Buffer.ToArray());}
}
public static class CritChecks {
 static int checks;static PhxItemDat dat;static CritSocket socket;
 static Socket transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static Player NewPlayer(){
  socket=(CritSocket)FormatterServices.GetUninitializedObject(typeof(CritSocket));socket.Packets=new ConcurrentQueue<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  foreach(var name in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
  typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
  var p=new Player(socket,dat);p.CharID=1;p.Con=50;p.Wis=30;p.CurHP=100;p.CurSP=100;return p;
 }
 static int Apply(int damage,BattleFighter actor,string action,int roll){return (int)typeof(CriticalHitManager).GetMethod("ApplyDamage",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{damage,actor,action,roll});}
 static void Equip(Player p,ushort id){var data=dat.GetItemByID(id);Check(data!=null,"Fixture equipment exists: "+id);p.Eqs[3].CopyFrom(new Item(data));}
 static BattleFighter Actor(Player p,bool pet,ushort item){
  if(!pet)Equip(p,item);
  return new BattleFighter{FighterType=pet?BattleFighterType.Pet:BattleFighterType.Player,PlayerRef=pet?null:p,
   PetRef=pet?new Player.PlayerPetData{Eq_Weapon=item,SP=100,HP=100}:null,OwnerID=1,GridX=pet?(byte)3:(byte)4,GridY=2,Atk=100,Def=10,Spd=100,CurHP=100,MaxHP=100,CurSP=100};
 }
 static void Hit(bool critical,bool pet,bool combo,bool defend,bool shield,bool hot,ushort skill){
  var p=NewPlayer();var s=socket;var actor=Actor(p,pet,critical?(ushort)10068:(ushort)11100);
  if(hot)actor.AddStatus(FighterStatusType.HotBlooded,2);
  var mob=new BattleMonster{MonsterHP=10000,MonsterMaxHP=10000,GridX=1,GridY=2,MonsterSpd=1};
  var target=new BattleFighter{FighterType=BattleFighterType.Monster,MonsterRef=mob,GridX=1,GridY=2,CurHP=10000,MaxHP=10000,Def=10,Spd=1};
  if(shield)target.AddStatus(FighterStatusType.Shielded,3);
  var battle=new ActiveBattle();battle.AttackingPlayers.Add(p);battle.Attackers.Add(actor);battle.Defenders.Add(target);
  battle.PendingActions[(actor.GridX<<8)|2]=new PendingAction{Actor=actor,Player=p,ActionType="attack",SkillId=skill,TargetGridX=1,TargetGridY=2};
  if(combo){var partner=Actor(p,true,11100);battle.Attackers.Add(partner);battle.PendingActions[(3<<8)|2]=new PendingAction{Actor=partner,Player=p,ActionType="attack",SkillId=skill,TargetGridX=1,TargetGridY=2};}
  if(defend)battle.PendingActions[(1<<8)|2]=new PendingAction{Actor=target,ActionType="defend"};
  typeof(PvEBattleManager).GetMethod("ExecuteTurn",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{battle});
  Check(System.Threading.SpinWait.SpinUntil(()=>mob.MonsterHP<10000,3000),"Actual turn applies damage");battle.IsFinished=true;battle.CancelTurnTimer();
  var packet=s.Packets.ToArray().Single(b=>b.Length>6&&b[4]==50&&b[5]==1);
  Check(packet.Length==(combo?44:25),"Attack packet record count");
  int sum=0;
  for(int i=0;i<(combo?2:1);i++){
   int start=6+i*19;int damage=(int)BitConverter.ToUInt32(packet,start+14);sum+=damage;
   bool thisCritical=critical&&packet[start+2]==actor.GridX;
   var expected=new List<int>();
   foreach(int jitter in Enumerable.Range(1,5)){
    int value=skill>10001?275:190+jitter;if(hot&&packet[start+2]==actor.GridX)value*=2;if(combo)value=(int)(value*1.25);
    if(thisCritical)value=(int)Math.Floor(value*1.5);if(defend||shield)value=Math.Max(1,value/2);expected.Add(value);
   }
   Check(expected.Contains(damage),"Real damage respects crit, combo, shield/defense and buff ordering");
   Check(packet[start+10]==1&&packet[start+11]==(defend?1:0),"No false miss or defense animation flag");
  }
  Check(10000-target.CurHP==sum&&mob.MonsterHP==target.CurHP,"Server HP subtracts packet damage exactly once");
  Check(!s.Packets.Any(b=>b.Length>8&&b[4]==51&&b[5]==1&&b[6]==1&&b[7]==2&&b[8]==25),"No pre-impact absolute HP packet double subtracts damage");
 }
 public static string Run(string root){
  dat=new PhxItemDat();Check(dat.Load(Path.Combine(root,"Data/itemDat.wpdat")).GetAwaiter().GetResult(),"Item data loaded");
  CriticalHitManager.Load(Path.Combine(root,"Data/critical_hits.json"));
  var raw=File.ReadAllBytes(@"D:/Game Private/WLRI/data/Item.dat");int count=0;
  for(int offset=0;offset<raw.Length;offset+=451){
   int special=((raw[offset+47]^0x9a)-9)&255,grade=((raw[offset+45]^0x9a)-9)&255;
   ushort id=(ushort)((BitConverter.ToUInt16(raw,offset+16)^0xefc3)-9);
   int expected=special==137&&grade>0?Math.Min(100,grade*2+10):0;
   Check(CriticalHitManager.GetItemChance(id)==expected,"Native tooltip rate matches item "+id);
   if(expected>0)count++;
  }
  Check(count==217,"All native crit items imported");
  var p=NewPlayer();var actor=Actor(p,false,11101);Check(p.Eqs.Crit==24,"Sky Sword24 percent");
  foreach(ushort id in new ushort[]{11101,11105,11048,11100,10068}){
   Equip(p,id);int chance=CriticalHitManager.GetChance(actor),wins=0;
   for(int roll=0;roll<100;roll++){int damage=Apply(100,actor,"attack",roll);Check(damage==(roll<chance?150:100),"Exact probability boundary "+id+"/"+roll);if(damage==150)wins++;}
   Check(wins==chance,"All100 equally likely rolls have exact configured chance");
  }
  Equip(p,11101);p.Eqs[1].CopyFrom(new Item(dat.GetItemByID(11105)));Check(p.Eqs.Crit==54,"Multiple equipped crit bonuses add");
  p.Eqs[3].Clear();Check(p.Eqs.Crit==30,"Unequip removes only its own bonus");p.Eqs[1].Clear();Check(p.Eqs.Crit==0,"Empty equipment has zero crit");
  Equip(p,10068);p.Eqs[1].CopyFrom(new Item(dat.GetItemByID(11101)));Check(p.Eqs.Crit==100,"Combined chance capped at100");
  var petActor=Actor(p,true,11100);petActor.PlayerRef=p;Check(CriticalHitManager.GetChance(petActor)==0,"Pet never borrows owner crit even when owner reference exists");
  petActor.PetRef.Eq_Weapon=11105;Check(CriticalHitManager.GetChance(petActor)==30,"Pet uses own weapon crit");
  foreach(string action in new[]{"heal","buff","status","catch","flee","defend","monster"})Check(Apply(100,actor,action,0)==100,"No crit for "+action);
  Check(Apply(0,actor,"attack",0)==0,"Zero damage stays zero");Check(Apply(int.MaxValue,actor,"attack",0)==int.MaxValue,"Crit damage cannot overflow negative");
  Check(Apply(100,new BattleFighter{FighterType=BattleFighterType.Monster,PlayerRef=p},"attack",0)==100,"Monster has no invented player equipment crit");
  Hit(false,false,false,false,false,false,11005);Hit(true,false,false,false,false,false,11005);
  Hit(true,false,false,true,false,false,11005);Hit(true,false,false,false,true,true,11005);
  Hit(true,true,false,false,false,false,11005);Hit(true,false,true,false,false,false,11005);
  Hit(true,false,false,false,false,false,10001);Hit(false,false,false,false,false,false,10001);
  return checks+" native critical rates/probability/equipment/real combat packet checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[CritChecks]::Run((Split-Path $PSScriptRoot -Parent))
