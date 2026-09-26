param([Parameter(Mandatory=$true)][string]$BuildDirectory)
$ErrorActionPreference='Stop'
$repo=(Split-Path $PSScriptRoot -Parent).Replace('\','/')
$fixtureDirectory=Join-Path $PSScriptRoot 'elin-tests-fixtures'
[void][IO.Directory]::CreateDirectory($fixtureDirectory)
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','System.Data.SQLite.dll') | ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory "Wonderland Private Server.exe"))
[RCLibrary.Core.PathHelper].GetField('_cachedDataDir',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,(Join-Path $BuildDirectory 'Data'))
$fixtureDefault = Join-Path $fixtureDirectory ('opcode13-default-' + [Guid]::NewGuid().ToString('N') + '.db')
[RCLibrary.Core.DataBase]::LoadGlobalConfig()
[RCLibrary.Core.DataBase]::DefaultDBFile = $fixtureDefault
[RCLibrary.Core.DataBase]::DefaultServType = [RCLibrary.Core.DataBaseTypes]::Sqlite
$fixtureConnection = New-Object System.Data.SQLite.SQLiteConnection ('Data Source=' + $fixtureDefault + ';Version=3;')
$fixtureConnection.Open()
$fixtureCommand = $fixtureConnection.CreateCommand()
$fixtureCommand.CommandText = 'CREATE TABLE monster_drops(monster_tid INTEGER,monster_pattern TEXT,item_id INTEGER,item_name TEXT,min_count INTEGER,max_count INTEGER,drop_rate REAL); INSERT INTO monster_drops VALUES(1,NULL,34014,"Fixture",1,1,1);'
[void]$fixtureCommand.ExecuteNonQuery()
$fixtureCommand.Dispose()
$fixtureConnection.Dispose()
$code=@"
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Net.Sockets;
using Game;
using Game.Maps;
using Game.DataFiles;
using Game.QuestRelated;
using Network;
using RCLibrary.Core.Networking;
public class RuntimeSocket : SocketClient {
 public List<byte[]> Packets = new List<byte[]>();
 public override void SendPacket(IPacket packet){Packets.Add(packet.Buffer.Skip(4).ToArray());}
}
public class StoryFixtureMap : GameMap {
 public StoryFixtureMap(ushort id){m_mapid=id;}
 protected override void Warp_Out(byte portalID,Player p,WarpData to,bool toTent=false){p.CancelInteraction();}
 protected override void Warp_In(TeleportType type,Player p,WarpData from=null,byte portalID=0){p.CurMap=this;p.CurX=from.DstX_Axis;p.CurY=from.DstY_Axis;}
}
public static class WellTransitionChecks {
 static int checks;
 static DataBase.GameDataBase db;
 static global::DataFiles.PhxItemDat items;
 static BindingFlags hidden=BindingFlags.NonPublic|BindingFlags.Static;
 static MethodInfo find=typeof(EveEventInterpreter).GetMethod("FindBranch",hidden);
 static MethodInfo start=typeof(EveEventInterpreter).GetMethod("StartSession",hidden);
 static MethodInfo execute=typeof(EveEventInterpreter).GetMethod("ExecuteOpcode",hidden);
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAILED: "+name);checks++;}
 static MapManager maps;
 static GameMap Map(ushort id){
  if(maps==null)maps=new MapManager();
  var cache=(Dictionary<ushort,GameMap>)typeof(MapManager).GetField("_mapCache",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(maps);
  GameMap m;if(!cache.TryGetValue(id,out m)){m=new StoryFixtureMap(id);cache[id]=m;}return m;
 }
 static Player New(Socket transport,GameMap map,out RuntimeSocket socket){
  socket=(RuntimeSocket)FormatterServices.GetUninitializedObject(typeof(RuntimeSocket));socket.Packets=new List<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  foreach(var name in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
  typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
  var p=new Player(socket,items);p.CharName="Fixture";p.CurMap=map;p.Quests=new Dictionary<uint,PlayerQuest>();return p;
 }
 static EventsinMapEntries Event(GameMap m,int id){return db.EveDat.GetMapData((ushort)m.MapID).Events.First(e=>e.clickID==id);}
 static EventSubEntry Pick(Player p,GameMap m,int eventId,byte trigger=0,ushort question=0,ushort answer=0){return (EventSubEntry)find.Invoke(null,new object[]{p,m,Event(m,eventId),trigger,question,answer,null});}
 static void Run(Player p,GameMap m,int eventId,EventSubEntry branch){start.Invoke(null,new object[]{p,m,(ushort)1,Event(m,eventId),branch,true});}
 static byte[] Frame(RuntimeSocket s){return s.Packets.Last(b=>b.Length==18&&b[0]==20&&b[1]==1);}
 static void FrameIs(RuntimeSocket s,int type,int text,string label){var b=Frame(s);Check(b[6]==type&&BitConverter.ToUInt16(b,15)==text,label);}
 static void Input(Player p,params byte[] bytes){var packet=new SendPacket();packet.PackArray(bytes);p.ProcessSocket(packet);}
 static void Advance(Player p){Check(p.ContinueInteraction(),"pending interaction advances");}
 static void Drain(Player p){int n=0;while(p.NativeEventActive&&p.OnInteractionComplete!=null&&n++<80)Advance(p);Check(n<80,"bounded event");}
 static void Choose(Player p,byte answer){var callback=p.OnDialogueChoice;p.OnDialogueChoice=null;Check(callback!=null,"choice is armed");callback(answer);}
 static void Mark(Player p,uint id,byte step){p.Quests[id]=new PlayerQuest(id,QuestState.InProgress,step);}
 static void Fill(Player p,int free){for(byte slot=1;slot<=50-free;slot++){var item=new Game.Code.Item(items.GetItemByID(34014));item.Ammt=50;p.Inv.AddItem(item,slot,false);}}
 static void StopFixtureBattle(Player p){
  var battle=Game.Battle.PvEBattleManager.GetBattle(p);battle.CancelTurnTimer();battle.IsFinished=true;
  var registry=(Dictionary<uint,Game.Battle.ActiveBattle>)typeof(Game.Battle.PvEBattleManager).GetField("_activeBattles",hidden).GetValue(null);registry.Remove(p.CharID);
 }
 static Player.PlayerPetData Pet(uint id,byte slot){return new Player.PlayerPetData{PetID=id,Slot=slot,ClientSlot=slot,PetName="Fixture pet",Amity=60};}
 public static byte Icon(RuntimeSocket socket,byte kind,ushort id){return socket.Packets.Last(b=>b.Length==6&&b[0]==22&&b[1]==12&&b[2]==kind&&BitConverter.ToUInt16(b,3)==id)[5];}
 static bool Compare(int a,int b,int c){switch(c){case 1:return a<b;case 2:return a>b;case 3:return a<=b;case 4:return a>=b;case 5:return a==b;case 6:return a!=b;default:return false;}}
 static void WaterfallActors(Player p,RuntimeSocket socket,bool standing,bool bathing,string label){
  foreach(ushort id in new ushort[]{41,42,43,44}){
   bool expected=id>=43?standing:bathing;
   Check(PreEventInterpreter.ShouldNpcBeVisible(p,60002,id)==expected,label+" visibility "+id);
  }
  PreEventInterpreter.EvaluateMapPreEvents(p,60002,true);
  foreach(ushort id in new ushort[]{41,42,43,44}){
   bool expected=id>=43?standing:bathing;
   var packet=socket.Packets.Last(b=>b.Length==16&&b[0]==22&&b[1]==4&&BitConverter.ToUInt16(b,2)==id);
   Check(packet[10]==(expected?1:2),label+" final actor packet "+id);
   Check(p.HiddenNpcClickIDs.Contains(id)!=expected,label+" hidden cache "+id);
  }
 }
 public static string RunAll(string root){
  DataBase.GameDataBase.GlobalInstance=null;int init=QuestManager.Count;
  Game.Battle.MonsterDropManager.ItemNameResolver=id=>"Item "+id;
  items=new global::DataFiles.PhxItemDat();items.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult();
  db=new DataBase.GameDataBase();db.DatabaseFile=root+"/.codex-verify/elin-tests-fixtures/elin-"+Guid.NewGuid().ToString("N")+".db";
  db.ExecuteNonQuery("CREATE TABLE charquest (pri_key INTEGER PRIMARY KEY, charID INTEGER, quest_started INTEGER, quest_pos INTEGER, step INTEGER)");db.EveDat.LoadFile(root+"/Data/eve.Emg");
  db.ExecuteNonQuery("CREATE TABLE npc_data(id INTEGER PRIMARY KEY,name TEXT,level INTEGER,hp INTEGER,element INTEGER); INSERT INTO npc_data VALUES(14163,'Presbyter Pawn',10,110,1)");
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   var match=typeof(EveEventInterpreter).GetMethod("MatchesCondition",hidden);
   var guard=typeof(EveEventInterpreter).GetMethod("UnsupportedRewardAction",hidden);
   RuntimeSocket sock;var map=Map(12146);var p=New(transport,map,out sock);



   var lab=Map(12521);
   foreach(byte step in new byte[]{2,3,4,5}){
    p=New(transport,lab,out sock);Mark(p,13176,step);
    foreach(bool force in new[]{true,false,true}){
     PreEventInterpreter.EvaluateMapPreEvents(p,12521,force);
     foreach(ushort id in new ushort[]{3,4}){
      bool visible=id==3?step==4:step==5;
      Check(PreEventInterpreter.ShouldNpcBeVisible(p,12521,id)==visible,"lab predicate stage "+step+" actor "+id);
      var last=sock.Packets.Last(b=>b.Length==16&&b[0]==22&&b[1]==4&&BitConverter.ToUInt16(b,2)==id);
      Check(last[10]==(visible?1:2),"lab final packet stage "+step+" actor "+id);
      Check(p.HiddenNpcClickIDs.Contains(id)!=visible,"lab hidden cache "+id);
     }
    }
   }
   p=New(transport,lab,out sock);p.Quests[13176]=new PlayerQuest(13176,QuestState.Completed,5);Mark(p,13177,1);p.PlayerPets[1]=Pet(14230,1);
   PreEventInterpreter.EvaluateMapPreEvents(p,12521,true);
   foreach(ushort id in new ushort[]{3,4}){
    Check(!PreEventInterpreter.ShouldNpcBeVisible(p,12521,id),"recruited lab actor hidden "+id);
    Check(sock.Packets.Last(b=>b.Length==16&&b[0]==22&&b[1]==4&&BitConverter.ToUInt16(b,2)==id)[10]==2,"recruited final packet hidden "+id);
   }
   var mirror=Map(11157);p=New(transport,mirror,out sock);
   PreEventInterpreter.EvaluateMapPreEvents(p,11157,true);
   Check(Icon(sock,1,4)==7,"mirror side quest initially available");
   p.Quests[15068]=new PlayerQuest(15068,QuestState.Completed,3);Mark(p,15069,1);
   var key=new Game.Code.Item(items.GetItemByID(30153));key.Ammt=1;p.Inv.AddItem(key,1,false);
   EveEventInterpreter.SyncQuestMinimapMarkers(p,mirror,true);
   Check(Icon(sock,1,4)==7,"key quest completion does not finish mirror side quest");
   Check(Pick(p,mirror,10).SubEntry.Any(o=>o.DialogPtr==1&&o.dialog2==25492),"marker belongs to rusty mirror dialogue");
   p.Quests[15072]=new PlayerQuest(15072,QuestState.Completed,11);Mark(p,15073,1);
   EveEventInterpreter.SyncQuestMinimapMarkers(p,mirror,true);
   Check(Icon(sock,1,4)==0,"mirror completion clears marker");
   p.CurMap=lab;p.CurMap=mirror;PreEventInterpreter.EvaluateMapPreEvents(p,11157,true);
   Check(Icon(sock,1,4)==0,"mirror completion stays clear after reentry");
   return "PASS: "+checks+" lab visibility and mirror marker checks.";
  }
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll','System.Xml.dll'))
try {[WellTransitionChecks]::RunAll($repo)}catch{Write-Output $_.Exception.ToString();exit 1}
