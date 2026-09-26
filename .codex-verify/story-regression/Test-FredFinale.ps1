param([Parameter(Mandatory=$true)][string]$BuildDirectory,[Parameter(Mandatory=$true)][string]$ClientDataDirectory)
$clientEve=(Resolve-Path (Join-Path $ClientDataDirectory "eve.Emg")).Path.Replace("\","/")
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path.Replace('\','/')
[void][IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot 'fixtures'))
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','System.Data.SQLite.dll') | ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory "Wonderland Private Server.exe"))
[RCLibrary.Core.PathHelper].GetField('_cachedDataDir',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,(Join-Path $BuildDirectory 'Data'))
$fixtureDefault = Join-Path $PSScriptRoot ('opcode13-default-' + [Guid]::NewGuid().ToString('N') + '.db')
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
 public override void SendPacket(IPacket packet){var b=packet.Buffer;for(int n=0;n<b.Length;){int len=BitConverter.ToUInt16(b,n+2);Packets.Add(b.Skip(n+4).Take(len).ToArray());n+=4+len;}}
}
public class StoryFixtureMap : GameMap {
 public StoryFixtureMap(ushort id){
  m_mapid=id;
  var data=DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(id);
  foreach(var entry in data.Npclist.Where(e=>e.clickId!=0))
   NPCs.Add(new QuestNpc{MapID=id,CickID=entry.clickId,TemplateID=entry.npcId,Name=entry.Name,X=(ushort)entry.x,Y=(ushort)entry.y});
 }
 protected override void Warp_Out(byte portalID,Player p,WarpData to,bool toTent=false){p.CancelInteraction();}
 protected override void Warp_In(TeleportType type,Player p,WarpData from=null,byte portalID=0){p.CurMap=this;p.CurX=from.DstX_Axis;p.CurY=from.DstY_Axis;}
}
public static class FredFinaleChecks {
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
  var p=new Player(socket,items);p.CharName="Fixture";p.CurMap=map;p.Quests=new Dictionary<uint,PlayerQuest>();p.Quests[13151]=new PlayerQuest(13151,QuestState.InProgress,1);return p;
 }
 static EventsinMapEntries Event(GameMap m,int id){return db.EveDat.GetMapData((ushort)m.MapID).Events.First(e=>e.clickID==id);}
 static EventSubEntry Pick(Player p,GameMap m,int eventId,byte trigger=0,ushort question=0,ushort answer=0){return (EventSubEntry)find.Invoke(null,new object[]{p,m,Event(m,eventId),trigger,question,answer,null});}
 static void Run(Player p,GameMap m,int eventId,EventSubEntry branch){start.Invoke(null,new object[]{p,m,(ushort)1,Event(m,eventId),branch,true});}
 static byte[] Frame(RuntimeSocket s){return s.Packets.Last(b=>b.Length==18&&b[0]==20&&b[1]==1);}
 static void FrameIs(RuntimeSocket s,int type,int text,string label){var b=Frame(s);Check(b[6]==type&&BitConverter.ToUInt16(b,15)==text,label);}
 static void Input(Player p,params byte[] bytes){
  // The complete source validates known NPCs and proximity. Model the walk
  // to the native actor before sending the interaction in this story fixture.
  if(bytes.Length>=4&&bytes[0]==20&&bytes[1]==1){var actor=((GameMap)p.CurMap).NPCs.FirstOrDefault(n=>n.CickID==BitConverter.ToUInt16(bytes,2));if(actor!=null){p.CurX=actor.X;p.CurY=actor.Y;}}
  var packet=new SendPacket();packet.PackArray(bytes);p.ProcessSocket(packet);}
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
 static void Outcome(Player p,int result){
  var fight=Game.Battle.PvEBattleManager.GetBattle(p);Check(fight!=null,"authored battle running");var q=fight.QuestContext;
  fight.CancelTurnTimer();fight.IsFinished=true;
  ((Dictionary<uint,Game.Battle.ActiveBattle>)typeof(Game.Battle.PvEBattleManager).GetField("_activeBattles",hidden).GetValue(null)).Remove(p.CharID);
  if(result==1)q.OnVictory();else if(result==2)q.OnDefeat();else{Check(q.OnFlee!=null,"flee has authored cleanup");q.OnFlee();}
  Drain(p);Check(!p.NativeEventActive,"battle outcome releases event");
 }
 static void Click(Player p,GameMap map,ushort id){p.CurMap=map;Check(EveEventInterpreter.TryExecute(p,map,id),"native NPC dispatch "+map.MapID+"/"+id);Drain(p);}
 static void Region(Player p,GameMap map,ushort id,ushort x,ushort y){p.CurMap=map;p.CurX=x;p.CurY=y;EveEventInterpreter.ExecuteRegionRequest(p,id);Drain(p);}
 static void Give(Player p,ushort id,byte amount=1,byte slot=38){var item=new Game.Code.Item(items.GetItemByID(id));item.Ammt=amount;p.Inv.AddItem(item,slot,false);}
 static void Guard(Player p,ushort actor){p.NpcClickResumeAt=DateTime.MinValue;Input(p,20,1,(byte)actor,0);Drain(p);}
 static void Door(Player p){p.LastTeleportTime=DateTime.MinValue;p.CurX=1462;p.CurY=615;Input(p,20,8,1,0);Drain(p);}
 public static string RunAll(string root){
  DataBase.GameDataBase.GlobalInstance=null;int init=QuestManager.Count;Game.Battle.MonsterDropManager.ItemNameResolver=id=>"Item "+id;
  items=new global::DataFiles.PhxItemDat();items.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult();
  db=new DataBase.GameDataBase();db.DatabaseFile=root+"/.codex-verify/story-regression/fixtures/route-"+Guid.NewGuid().ToString("N")+".db";
  db.ExecuteNonQuery("CREATE TABLE charquest (pri_key INTEGER PRIMARY KEY, charID INTEGER, quest_started INTEGER, quest_pos INTEGER, step INTEGER)");db.EveDat.LoadFile(root+"/Data/eve.Emg");
  db.ExecuteNonQuery("CREATE TABLE npc_data(id INTEGER PRIMARY KEY,name TEXT,level INTEGER,hp INTEGER,element INTEGER)");
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   var room=Map(11149);RuntimeSocket sock;Player p;
   Check(!(bool)typeof(EveEventInterpreter).GetMethod("IsNativeEventDisabled",hidden).Invoke(null,new object[]{(uint)11149,(ushort)2}),"Fred finale event enabled");
   foreach(bool following in new[]{false,true}){
    p=New(transport,room,out sock);p.CharID=998201;Mark(p,13172,4);p.PlayerPets[1]=Pet(14162,1);p.QuestPets[1]=Pet(14081,1);if(following)p.ActivePetID=14162;
    Check(Pick(p,room,2).subIndex==(following?20:10),"native branch for Roca following "+following);
    p.NpcClickResumeAt=DateTime.MinValue;Input(p,20,1,2,0);
    int n=0;bool sawMovie=false;while(!sock.Packets.Any(x=>x.SequenceEqual(new byte[]{15,20,6,3}))&&n++<40){
     var frame=Frame(sock);if(frame[6]==5){Check(BitConverter.ToUInt32(frame,11)==11109&&frame[10]==1,"farewell movie releases render for music");sawMovie=true;}
     Check(!p.Quests.ContainsKey(13173)&&!p.PlayerPets.Values.Any(x=>x.PetID==12095),"no reward before constellation");Input(p,20,6);
    }
    Check(n<40&&sawMovie,"farewell reaches constellation after native movie");
    Check(Frame(sock)[6]==13&&BitConverter.ToUInt16(Frame(sock),8)==3&&BitConverter.ToUInt32(Frame(sock),11)==6,"native star6 frame on Niss actor3");
    Check(p.NativeEventActive&&p.OnInteractionComplete!=null,"constellation owns completion");
    Check(p.Quests[13172].State==QuestState.InProgress&&!p.Quests.ContainsKey(13173)&&p.Inv.GetItemCount(34064)==0,"no early completion during star animation");
    Drain(p);
    Check(p.Quests[13172].State==QuestState.Completed&&p.Quests[13173].Step==1,"native finale completes "+following);
    Check(p.PlayerPets.Values.Count(x=>x.PetID==12095)==1&&p.Inv.GetItemCount(34064)==1,"Fred and native reward granted once "+following);
    Check(p.QuestPets.Values.Any(x=>x.PetID==14081),"Niss saved companion preserved");
    Check(sock.Packets.Any(x=>x.SequenceEqual(new byte[]{15,19,2,6,20})),"Niss star list synchronized");
    if(!following)Check(sock.Packets.Any(x=>x.SequenceEqual(new byte[]{22,8,4,0,8,1})),"Roca native actor animation dispatched");
    Guard(p,2);Check(p.PlayerPets.Values.Count(x=>x.PetID==12095)==1&&p.Inv.GetItemCount(34064)==1,"repeat cannot duplicate reward");
   }
   foreach(bool following in new[]{false,true})foreach(int party in new[]{0,1,4})foreach(int free in new[]{0,1}){
    p=New(transport,room,out sock);Mark(p,13172,4);p.QuestPets[1]=Pet(14081,1);if(party>0)p.PlayerPets[1]=Pet(14162,1);for(byte i=2;i<=party;i++)p.PlayerPets[i]=Pet((uint)(14200+i),i);if(following)p.ActivePetID=14162;Fill(p,free);Guard(p,2);
    bool eligible=party==1&&free==1;Check(p.Quests.ContainsKey(13173)==eligible,"party/bag prerequisite "+following+"/"+party+"/"+free);
    Check(p.Inv.GetItemCount(34064)==(eligible?1:0),"no reward when gate fails");
    Check(!p.NativeEventActive,"gate/result releases interaction");
   }
   foreach(bool following in new[]{false,true}){
    p=New(transport,room,out sock);Mark(p,13172,4);p.PlayerPets[1]=Pet(14162,1);p.QuestPets[1]=Pet(14081,1);if(following)p.ActivePetID=14162;
    Input(p,20,1,2,0);int n=0;while(!sock.Packets.Any(x=>x.SequenceEqual(new byte[]{15,20,6,3}))&&n++<40)Advance(p);
    Check(n<40,"cancel test reaches constellation");var stale=p.OnInteractionComplete;p.CancelInteraction();stale();Check(!p.Quests.ContainsKey(13173)&&p.Inv.GetItemCount(34064)==0,"stale callback cannot reward");
    PreEventInterpreter.EvaluateMapPreEvents(p,11149,true);Guard(p,2);Check(p.Quests.ContainsKey(13173)&&p.Inv.GetItemCount(34064)==1,"cancel/retry finishes once");
   }
   // A stale active ID or a Roca in storage cannot satisfy the party prerequisite.
   foreach(bool hotel in new[]{false,true}){p=New(transport,room,out sock);Mark(p,13172,4);p.ActivePetID=14162;if(hotel)p.HotelPets[1]=Pet(14162,1);Guard(p,2);Check(!p.Quests.ContainsKey(13173),"Roca must be in party");}
   // Missing, early and completed quest checkpoints remain gated.
   foreach(byte step in new byte[]{0,1,2,3,5}){p=New(transport,room,out sock);if(step>0)Mark(p,13172,step);p.PlayerPets[1]=Pet(14162,1);Guard(p,2);Check(!p.Quests.ContainsKey(13173)&&p.Inv.GetItemCount(34064)==0,"checkpoint gate "+step);}
   // Keep all three earned stars when resending the list at login.
   p=New(transport,room,out sock);Mark(p,13087,1);Mark(p,13151,1);Mark(p,13173,1);QuestManager.SendStoryConstellations(p);
   Check(sock.Packets.Last(x=>x[0]==15&&x[1]==19).SequenceEqual(new byte[]{15,19,3,1,6,20}),"Niss preserves Cygnus and Bootes");
   foreach(bool following in new[]{false,true})foreach(bool fillParty in new[]{false,true}){
    p=New(transport,room,out sock);Mark(p,13172,4);p.PlayerPets[1]=Pet(14162,1);p.QuestPets[1]=Pet(14081,1);if(following)p.ActivePetID=14162;
    Input(p,20,1,2,0);int n=0;while(!sock.Packets.Any(x=>x.SequenceEqual(new byte[]{15,20,6,3}))&&n++<40)Advance(p);Check(n<40,"late capacity change reaches star");
    if(fillParty){for(byte i=2;i<=4;i++)p.PlayerPets[i]=Pet((uint)(14200+i),i);}else Fill(p,0);
    Drain(p);Check(!p.Quests.ContainsKey(13173)&&p.Inv.GetItemCount(34064)==0&&!p.PlayerPets.Values.Any(x=>x.PetID==12095),"capacity rechecked before final commit");
    if(fillParty){p.PlayerPets.Remove(4);}else{Check(p.Inv.RemoveItem(1,50,false)!=null,"free a bag slot");}
    Guard(p,2);Check(p.Quests.ContainsKey(13173)&&p.Inv.GetItemCount(34064)==1,"retry after freeing capacity completes");
   }
   p=New(transport,room,out sock);Mark(p,13172,4);p.PlayerPets[1]=Pet(14161,1);p.ActivePetID=14161;p.QuestPets[1]=Pet(14081,1);Guard(p,2);Check(p.Quests.ContainsKey(13173),"Roca verified alias can complete finale");
   p=New(transport,room,out sock);Mark(p,13172,4);p.PlayerPets[1]=Pet(14162,1);p.HotelPets[1]=Pet(12095,1);Guard(p,2);
   Check(!p.Quests.ContainsKey(13173)&&p.Inv.GetItemCount(34064)==0,"stored Fred prevents partial rewards or duplication");
   var client=new EveManager();client.LoadFile("$clientEve");var native=Event(room,2);var ce=client.GetMapData(11149).Events.Single(e=>e.clickID==2);
   Check(native.SubEntry.Count==ce.SubEntry.Count,"client/server finale branches match");
   for(int i=0;i<native.SubEntry.Count;i++){var a=native.SubEntry[i];var b=ce.SubEntry[i];Check(a.unknownbyte1==b.unknownbyte1&&a.unknownword1==b.unknownword1&&a.unknownword2==b.unknownword2&&a.unknownword3==b.unknownword3&&a.unknownword4==b.unknownword4&&a.unknownword5==b.unknownword5&&a.unknownword6==b.unknownword6,"native finale condition preserved");Check(a.SubEntry.SequenceEqual(b.SubEntry),"native finale actions preserved");}
   var persistence=root+"/.codex-verify/story-regression/fixtures/checkpoints-"+Guid.NewGuid().ToString("N")+".db";
   System.Data.SQLite.SQLiteConnection.CreateFile(persistence);RCLibrary.Core.DataBase.DefaultDBFile=persistence;db=new DataBase.GameDataBase();db.ItemDat=items;db.VerifySetup();db.EveDat.LoadFile(root+"/Data/eve.Emg");var save=new DataBase.CharacterDataBase();save.VerifySetup();
   p=New(transport,room,out sock);p.Slot=2;p.UserAcc.DataBaseID=1;Mark(p,13172,4);p.PlayerPets[1]=Pet(14162,1);var niss=Pet(14081,1);niss.Level=35;niss.Exp=321;niss.Amity=65;niss.Eq_Weapon=11103;p.QuestPets[1]=niss;Guard(p,2);
   Check(save.WritePlayer(p.CharID,p),"completed Fred saved");var loaded=New(transport,room,out sock);loaded.Slot=2;loaded.UserAcc.DataBaseID=1;db.LoadFinalData(loaded);QuestManager.LoadPlayerQuests(loaded);
   Check(loaded.Quests[13173].Step==1&&loaded.Inv.GetItemCount(34064)==1&&loaded.PlayerPets.Values.Count(x=>x.PetID==12095)==1,"Fred/reward/checkpoint persist");
   var savedNiss=loaded.QuestPets.Values.Single(x=>x.PetID==14081);Check(savedNiss.Level==35&&savedNiss.Exp==321&&savedNiss.Amity==65&&savedNiss.Eq_Weapon==11103,"Niss stats/equipment preserved");
   QuestManager.SendStoryConstellations(loaded);Check(sock.Packets.Last(x=>x[0]==15&&x[1]==19).Contains((byte)6),"Niss star survives login");Guard(loaded,2);Check(loaded.Inv.GetItemCount(34064)==1&&loaded.PlayerPets.Values.Count(x=>x.PetID==12095)==1,"reload does not duplicate rewards");
   return "PASS: "+checks+" Fred finale checks; native NPC/CG/constellation/companion flow, isolated database, no live client replay.";
  }
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll','System.Xml.dll'))
try {[FredFinaleChecks]::RunAll($repo)}catch{Write-Output $_.Exception.ToString();exit 1}
