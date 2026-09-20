param([string]$BuildDirectory)
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','System.Data.SQLite.dll')|ForEach-Object{Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
$code=@"
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Net.Sockets;
using Game;
using Game.Battle;
using Game.DataFiles;
using Game.QuestRelated;
using Network;
using DataFiles;
using RCLibrary.Core.Networking;
public class Branch1Socket : SocketClient {
 public ConcurrentQueue<byte[]> Packets=new ConcurrentQueue<byte[]>();
 public override void SendPacket(IPacket p){Packets.Enqueue(p.Buffer.ToArray());}
}
public class FixedDropRandom : Random {
 readonly double value;
 public FixedDropRandom(double value){this.value=value;}
 public override double NextDouble(){return value;}
 public override int Next(int min,int max){return min;}
}
public static class Branch1Checks {
 static int checks;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 static Player MakePlayer(PhxItemDat dat,Socket transport){
  var socket=(Branch1Socket)FormatterServices.GetUninitializedObject(typeof(Branch1Socket));
  socket.Packets=new ConcurrentQueue<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  foreach(var name in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
  typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
  return new Player(socket,dat);
 }
 static byte[] Condition(ushort id,ushort state){var data=new byte[21];data[0]=5;data[1]=(byte)id;data[2]=(byte)(id>>8);data[3]=(byte)state;data[5]=1;data[7]=(byte)(state==1?2:0);data[12]=1;return data;}
 public static string Run(string itemPath){
  var dat=new PhxItemDat();dat.Load(itemPath).GetAwaiter().GetResult();
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   var p=MakePlayer(dat,transport);p.Eqs.SetLevel(10);p.Eqs.CurHP=63;p.Eqs.CurSP=41;
   Server.ServerStatusManager.ExpRate=1;p.Eqs.AddExp(10);int before=p.Eqs.CurExp;long total=p.Eqs.TotalExp;
   p.Eqs.AddExp(7);Check(p.Eqs.CurExp==before+7&&p.Eqs.TotalExp==total+7,"Reward must not re-add existing EXP");
   Server.ServerStatusManager.ExpRate=2;before=p.Eqs.CurExp;p.Eqs.AddExp(11);Check(p.Eqs.CurExp==before+22,"Player EXP multiplier applies once");
   before=p.Eqs.CurExp;p.Eqs.AddExp(9,false);Check(p.Eqs.CurExp==before+9,"Explicit raw EXP bypass");
   Check(p.Eqs.CurHP==63&&p.Eqs.CurSP==41,"Non-level-up reward must not refill vitals");
   var pet=new Player.PlayerPetData{PetID=12032,Level=5,Exp=0,Con=4,Wis=3};
   uint petReward=(uint)Server.ServerStatusManager.ScaleExperience(11);pet.GainExp(petReward);Check(pet.Exp==22&&pet.Level==5,"Pet uses same multiplier and native progression");
   foreach(double rate in new[]{0.5,1.0,2.0,3.5}){Server.ServerStatusManager.ExpRate=rate;Check(Server.ServerStatusManager.ScaleExperience(20)==(long)(20*rate),"EXP rate "+rate);}
   foreach(double rate in new[]{double.NaN,double.PositiveInfinity,-1.0,0.0}){Server.ServerStatusManager.ExpRate=rate;Check(Server.ServerStatusManager.ScaleExperience(20)==20,"Invalid rate falls back to one");}
   Check(Server.ServerStatusManager.ScaleExperience(0)==0&&Server.ServerStatusManager.ScaleExperience(-1)==0,"No reward for nonpositive EXP");
   Server.ServerStatusManager.ExpRate=double.MaxValue;Check(Server.ServerStatusManager.ScaleExperience(long.MaxValue)==int.MaxValue,"EXP native field cannot overflow");
   Server.ServerStatusManager.ExpRate=1;
   var evaluate=typeof(PreEventInterpreter).GetMethod("EvaluateConditionBlock",BindingFlags.NonPublic|BindingFlags.Static);
   p.Quests.Clear();Check((bool)evaluate.Invoke(null,new object[]{p,Condition(65000,2)}),"Unstarted quest condition");
   p.Quests[65000]=new PlayerQuest(65000,QuestState.Completed,1);
   Check((bool)evaluate.Invoke(null,new object[]{p,Condition(65000,2)}),"Completed native mark is cleared");
   Check(!(bool)evaluate.Invoke(null,new object[]{p,Condition(65000,1)}),"Completed quest rejects in-progress branch");
   var other=MakePlayer(dat,transport);Check((bool)evaluate.Invoke(null,new object[]{other,Condition(65000,2)}),"Quest state is isolated per player");
   var evt=new preEventEntries{clickID=7};
   var first=new preEventSubEntry{unknown=Condition(65000,2).ToList()};first.subentry2.Add(new preEventSubSubEntry{unknown=new List<byte>{2,7,0,2,0,0,0,0,255,255}});evt.subentry1.Add(first);
   evt.subentry1.Add(new preEventSubEntry{unknown=Condition(65001,2).ToList()});
   var third=new preEventSubEntry{unknown=Condition(65002,2).ToList()};third.subentry2.Add(new preEventSubSubEntry());evt.subentry1.Add(third);
   var group=typeof(PreEventInterpreter).GetMethod("GroupSubEntriesIntoRules",BindingFlags.NonPublic|BindingFlags.Static);
   var rules=(IList)group.Invoke(null,new object[]{evt});Check(rules.Count==2,"Compound conditions belong to two action groups");
   Check(((IList)rules[0].GetType().GetProperty("Conditions").GetValue(rules[0],null)).Count==2,"First action requires both condition blocks");
   p.Quests[12002]=new PlayerQuest(12002,QuestState.Completed,1);Check(!p.HasRecruitedCompanion("S.Monkey",17162),"Completed recruit quest is history, not current party ownership");
   Check(!PreEventInterpreter.ShouldNpcBeVisible(p,11016,1),"Recruited monkey hidden for owner");
   Check(PreEventInterpreter.ShouldNpcBeVisible(other,11016,1),"Monkey remains visible to another player");
   var mapDb=new DataBase.GameDataBase();
   var maps=(Dictionary<ushort,MapData>)typeof(EveManager).GetField("Maps",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(mapDb.EveDat);
   maps[65000]=new MapData{mapID=65000};
   var map=(Game.GameMap)FormatterServices.GetUninitializedObject(typeof(Game.GameMap));
   typeof(Game.GameMap).GetField("mlock",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(map,new object());
   typeof(Game.GameMap).GetField("m_mapid",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(map,(uint)65000);
   Check(!Game.Maps.EveEventInterpreter.TryExecute(p,map,5),"NPC without EVE event must reach storage/shop fallback");

  }
  MonsterDropManager.ItemNameResolver=id=>dat.GetItemByID(id)==null?null:"known fixture item";
  var random=typeof(MonsterDropManager).GetField("_rng",BindingFlags.NonPublic|BindingFlags.Static);
  foreach(double configured in new[]{0.0,1.0,10.0,50.0,100.0}){
   MonsterDropManager.MonsterLootTables[65000]=new List<MonsterDropEntry>{new MonsterDropEntry(34014,"fixture",2,3,configured)};
   double threshold=Math.Max(5,configured*0.35)/100;
   random.SetValue(null,new FixedDropRandom(threshold-0.00001));var yes=MonsterDropManager.RollDrops(65000,"fixture",1);Check(yes.Count==1&&yes[0].Count==2,"Drop below boundary "+configured);
   random.SetValue(null,new FixedDropRandom(threshold+0.00001));Check(MonsterDropManager.RollDrops(65000,"fixture",1).Count==0,"Drop above boundary "+configured);
  }
  MonsterDropManager.MonsterLootTables[65000].Add(new MonsterDropEntry(45001,"second",1,1,100));random.SetValue(null,new FixedDropRandom(0));
  Check(MonsterDropManager.RollDrops(65000,"fixture",1).Count==1,"At most one drop entry per kill");
  RCLibrary.Core.DataBase.Execute("CREATE TABLE IF NOT EXISTS npc_data(id INTEGER PRIMARY KEY,name TEXT,level INTEGER,hp INTEGER,element INTEGER)");
  RCLibrary.Core.DataBase.Execute("INSERT OR REPLACE INTO npc_data VALUES(65000,'fixture',7,321,2)");
  var db=new DataBase.GameDataBase();var npc=db.ResolveNpcInfo(1,1,65000);Check(npc.Level==7&&npc.HP==321,"NPC stats resolved from database");
  db.UpdateNpcTemplate(65000,"updated",8,400,3);npc=db.ResolveNpcInfo(1,1,65000);Check(npc.Level==8&&npc.HP==400,"NPC update invalidates cached stats");
  RCLibrary.Core.DataBase.Execute("DELETE FROM npc_data WHERE id=65000");db.LoadNpcCache();npc=db.ResolveNpcInfo(1,1,65000);Check(npc.Level==1&&npc.HP==100,"Cache reload removes deleted NPC");
  var eve=new EveManager();Check(eve.LoadFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(itemPath),"eve.Emg")),"Full client EVE loads");
  Check(eve.AllMaps.Count>100,"Native EVE map catalogue populated");
  string summary;Check(!RCLibrary.Core.DataBase.MigrateSqliteToMySql(itemPath+".missing","127.0.0.1","3306","unused","unused","",null,out summary)&&summary.Contains("not found"),"Missing migration source rejected before connecting");
  return checks+" branch1 EXP, quest isolation, drop boundary and NPC cache checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll'))
[Branch1Checks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'))
