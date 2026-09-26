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
public static class RocaDeathChecks {
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
 static void Result(Player p,int result){var battle=Game.Battle.PvEBattleManager.GetBattle(p);Check(battle!=null,"quest battle running");var context=battle.QuestContext;StopFixtureBattle(p);if(result==1)context.OnVictory();else context.OnDefeat();}
 static void CG(Player p,RuntimeSocket sock,uint id){var f=Frame(sock);Check(f[6]==5&&BitConverter.ToUInt32(f,11)==id,"authored movie "+id);Check(f[10]==1,"movie does not hold renderer "+id);Check(p.OnInteractionComplete!=null,"movie owns ACK "+id);Input(p,20,6);}
 static void Prereqs(Player p){Mark(p,13173,1);Mark(p,13083,1);Mark(p,13011,1);p.PlayerPets[1]=Pet(14162,1);p.PlayerPets[2]=Pet(12095,2);}
 public static string RunAll(string root){
  DataBase.GameDataBase.GlobalInstance=null;int init=QuestManager.Count;Game.Battle.MonsterDropManager.ItemNameResolver=id=>"Item "+id;
  items=new global::DataFiles.PhxItemDat();items.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult();
  db=new DataBase.GameDataBase();db.DatabaseFile=root+"/.codex-verify/story-regression/fixtures/route-"+Guid.NewGuid().ToString("N")+".db";
  db.ExecuteNonQuery("CREATE TABLE charquest (pri_key INTEGER PRIMARY KEY, charID INTEGER, quest_started INTEGER, quest_pos INTEGER, step INTEGER)");db.EveDat.LoadFile(root+"/Data/eve.Emg");
  db.ExecuteNonQuery("CREATE TABLE npc_data(id INTEGER PRIMARY KEY,name TEXT,level INTEGER,hp INTEGER,element INTEGER)");
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   var room=Map(11149);var death=Map(11185);var hall=Map(11148);RuntimeSocket sock;Player p;
   foreach(uint active in new uint[]{0,14162,12095}){
    p=New(transport,room,out sock);p.CharID=999800;Prereqs(p);p.ActivePetID=active;
    p.CurX=1940;p.CurY=900;Input(p,20,4,3,0);Check(p.OnDialogueChoice!=null&&Frame(sock)[6]==6,"native region opens Mooter prompt");
    Check(Frame(sock)[10]==1,"Mooter choice has no trailing completion");Input(p,20,9,30);Drain(p);Check(p.Quests[13202].Step==1,"accept starts quest");
    p.CurX=1140;p.CurY=520;Input(p,20,4,1,0);Drain(p);
    var battle=Game.Battle.PvEBattleManager.GetBattle(p);Check(battle!=null&&battle.Monsters.First().MonsterId==15358,"first native formation starts");
    Result(p,1);CG(p,sock,11144);battle=Game.Battle.PvEBattleManager.GetBattle(p);Check(battle!=null&&battle.Monsters.First().MonsterId==17799,"second native formation starts after CG");
    Result(p,1);Check(!Game.Battle.PvEBattleManager.IsInBattle(p),"second victory does not restart first outcome");
    foreach(uint movie in new uint[]{11145,11146,11147}){Check(p.Quests[13202].Step==1&&p.PlayerPets.Values.Any(x=>x.PetID==14162),"Roca stays until full sacrifice movie ends");CG(p,sock,movie);}
    Check(p.MapID==11185&&p.CurX==1175&&p.CurY==493&&!p.NativeEventActive,"native warp reaches Roca farewell");
    Check(p.Quests[13202].Step==2&&!p.PlayerPets.Values.Any(x=>x.PetID==14162)&&p.QuestPets.Values.Any(x=>x.PetID==14162),"Roca reserved and checkpoint saved after movies");
    // At step2 the exit supplies the authored reminder until the farewell completes.
    p.CurX=1320;p.CurY=700;Input(p,20,8,1,0);Drain(p);Check(p.MapID==11185,"cannot leave before farewell");
    PreEventInterpreter.EvaluateMapPreEvents(p,11185,true);Check(!p.HiddenNpcClickIDs.Contains(1),"dying Roca visible");
    Input(p,20,1,1,0);int n=0;while(Frame(sock)[6]!=5&&n++<30)Input(p,20,6);Check(n<30,"farewell dialogue reaches movie");CG(p,sock,11148);
    Check(Frame(sock)[6]==13&&BitConverter.ToUInt32(Frame(sock),11)==8&&BitConverter.ToUInt16(Frame(sock),8)==1,"Star8 event uses Roca actor1");
    Check(sock.Packets.Any(x=>x.SequenceEqual(new byte[]{15,20,8,1})),"Roca star animation packet");
    Check(!p.Quests.ContainsKey(13203)&&p.Inv.GetItemCount(30025)==0,"no early reward during constellation");
    Input(p,20,6);Check(Frame(sock)[6]==15,"music follows constellation ACK");Drain(p);
    Check(p.Quests[13202].State==QuestState.Completed&&p.Quests[13203].Step==1,"Roca death completes");
    Check(p.Inv.GetItemCount(30025)==4&&p.Inv.GetItemCount(25538)==1,"native four Stars and item25538 granted");
    Check(p.PlayerPets.Values.Any(x=>x.PetID==12095)&&p.QuestPets.Values.Any(x=>x.PetID==14162),"Fred stays and Roca reserve preserved");
    Check(sock.Packets.Any(x=>x.SequenceEqual(new byte[]{15,19,2,6,8})),"Roca and Niss stars both synchronized");
    Guard(p,1);Check(p.Inv.GetItemCount(30025)==4&&p.Inv.GetItemCount(25538)==1,"cannot repeat rewards");
    p.CurX=1320;p.CurY=700;Input(p,20,8,1,0);Drain(p);Check(p.MapID==11148&&p.CurX==1512&&p.CurY==636,"completed exit returns to Ghostdom");
   }
   Check(!(bool)typeof(EveEventInterpreter).GetMethod("IsNativeEventDisabled",hidden).Invoke(null,new object[]{(uint)11185,(ushort)4}),"Roca farewell enabled");
   foreach(uint missing in new uint[]{13173,13083,13011}){
    p=New(transport,room,out sock);Prereqs(p);p.Quests.Remove(missing);p.CurX=1940;p.CurY=900;Input(p,20,4,3,0);Drain(p);Check(p.OnDialogueChoice==null&&!p.Quests.ContainsKey(13202),"missing prerequisite cannot start "+missing);
   }
   foreach(uint missing in new uint[]{14162,12095}){
    p=New(transport,room,out sock);Prereqs(p);var slot=p.PlayerPets.Single(x=>x.Value.PetID==missing).Key;p.PlayerPets.Remove(slot);p.CurX=1940;p.CurY=900;Input(p,20,4,3,0);Drain(p);Check(p.OnDialogueChoice==null&&!p.Quests.ContainsKey(13202),"both companions required");
   }
   foreach(byte answer in new byte[]{31,40}){p=New(transport,room,out sock);Prereqs(p);p.CurX=1940;p.CurY=900;Input(p,20,4,3,0);Input(p,20,9,answer);Drain(p);Check(!p.NativeEventActive&&!p.Quests.ContainsKey(13202),"decline/close leaves quest unstarted");}
   foreach(int round in new[]{1,2}){
    p=New(transport,room,out sock);Prereqs(p);Mark(p,13202,1);Run(p,room,5,Pick(p,room,5));Drain(p);if(round==2){Result(p,1);CG(p,sock,11144);}
    Result(p,2);Check(!p.NativeEventActive&&p.Quests[13202].Step==1&&p.PlayerPets.Values.Any(x=>x.PetID==14162),"loss keeps Roca and retry checkpoint "+round);
    Run(p,room,5,Pick(p,room,5));Drain(p);Check(Game.Battle.PvEBattleManager.GetBattle(p)!=null,"loss allows retry");StopFixtureBattle(p);p.CancelInteraction();
   }
   foreach(bool active in new[]{false,true})foreach(int free in new[]{0,1,2}){
    p=New(transport,death,out sock);Mark(p,13202,2);p.PlayerPets[1]=Pet(12095,1);p.QuestPets[1]=Pet(14162,1);if(active)p.ActivePetID=12095;Fill(p,free);Guard(p,1);bool ok=free==2;
    Check(p.Quests.ContainsKey(13203)==ok&&p.Inv.GetItemCount(30025)==(ok?4:0)&&p.Inv.GetItemCount(25538)==(ok?1:0),"two free bag slots required "+active+"/"+free);
   }
   foreach(bool hotel in new[]{false,true}){p=New(transport,death,out sock);Mark(p,13202,2);p.ActivePetID=12095;if(hotel)p.HotelPets[1]=Pet(12095,1);Guard(p,1);Check(!p.Quests.ContainsKey(13203),"missing or stored Fred cannot complete");}
   p=New(transport,death,out sock);Mark(p,13202,2);p.PlayerPets[1]=Pet(12095,1);p.PlayerPets[2]=Pet(14162,2);Guard(p,1);Check(!p.Quests.ContainsKey(13203),"Roca still in party blocks death reward");
   foreach(bool active in new[]{false,true}){
    p=New(transport,death,out sock);Mark(p,13202,2);p.PlayerPets[1]=Pet(12095,1);p.QuestPets[1]=Pet(14162,1);if(active)p.ActivePetID=12095;Input(p,20,1,1,0);int n=0;while(!sock.Packets.Any(x=>x.SequenceEqual(new byte[]{15,20,8,1}))&&n++<40)Advance(p);Check(n<40,"cancel reaches star8");var stale=p.OnInteractionComplete;p.CancelInteraction();stale();Check(!p.Quests.ContainsKey(13203)&&p.Inv.GetItemCount(30025)==0,"stale constellation callback cannot reward");
    Guard(p,1);Check(p.Quests.ContainsKey(13203)&&p.Inv.GetItemCount(30025)==4,"cancel retry grants once");
   }
   // The native rejoin events must not undo the sacrifice checkpoint.
   foreach(bool completed in new[]{false,true})foreach(int[] pair in new[]{new[]{12000,46},new[]{12000,48},new[]{12001,5}}){
    p=New(transport,Map((ushort)pair[0]),out sock);Mark(p,13011,1);Mark(p,13053,1);if(completed)Mark(p,13203,1);else Mark(p,13202,2);p.QuestPets[1]=Pet(14162,1);var branch=Pick(p,(GameMap)p.CurMap,pair[1]);Check(branch!=null&&branch.SubEntry.All(o=>o.DialogPtr!=3),"rejoin stays blocked after sacrifice");Run(p,(GameMap)p.CurMap,pair[1],branch);Drain(p);Check(p.PlayerPets.Count==0&&p.QuestPets.Count==1,"rejoin preserves absent Roca");}
   var client=new EveManager();client.LoadFile("$clientEve");
   foreach(int[] pair in new[]{new[]{11149,5},new[]{11149,6},new[]{11185,2},new[]{11185,4}}){var a=Event(Map((ushort)pair[0]),pair[1]);var b=client.GetMapData((ushort)pair[0]).Events.Single(e=>e.clickID==pair[1]);Check(a.SubEntry.Count==b.SubEntry.Count,"native client/server branch counts agree");for(int i=0;i<a.SubEntry.Count;i++){var x=a.SubEntry[i];var y=b.SubEntry[i];Check(x.unknownbyte1==y.unknownbyte1&&x.unknownword1==y.unknownword1&&x.unknownword2==y.unknownword2&&x.unknownword3==y.unknownword3&&x.unknownword4==y.unknownword4&&x.unknownword5==y.unknownword5&&x.unknownword6==y.unknownword6,"native conditions agree");Check(x.SubEntry.SequenceEqual(y.SubEntry),"native actions agree");}}
   var persistence=root+"/.codex-verify/story-regression/fixtures/checkpoint-"+Guid.NewGuid().ToString("N")+".db";
   System.Data.SQLite.SQLiteConnection.CreateFile(persistence);RCLibrary.Core.DataBase.DefaultDBFile=persistence;db=new DataBase.GameDataBase();db.ItemDat=items;db.VerifySetup();db.EveDat.LoadFile(root+"/Data/eve.Emg");var save=new DataBase.CharacterDataBase();save.VerifySetup();
   p=New(transport,death,out sock);p.Slot=2;p.UserAcc.DataBaseID=1;Mark(p,13202,2);Mark(p,13087,1);Mark(p,13173,1);Mark(p,13151,1);p.PlayerPets[1]=Pet(12095,1);var roca=Pet(14162,1);roca.Level=35;roca.Exp=321;roca.Amity=65;roca.Eq_Weapon=10063;p.QuestPets[1]=roca;
   foreach(uint q in p.Quests.Keys)QuestManager.SavePlayerQuest(p,q);Check(save.WritePlayer(p.CharID,p),"pre-farewell checkpoint saved");
   var loaded=New(transport,death,out sock);loaded.Slot=2;loaded.UserAcc.DataBaseID=1;db.LoadFinalData(loaded);QuestManager.LoadPlayerQuests(loaded);Check(loaded.Quests[13202].Step==2&&loaded.QuestPets.Count==1,"sacrifice checkpoint reloads");
   Run(loaded,death,4,Pick(loaded,death,4));Drain(loaded);Check(loaded.Quests[13203].Step==1&&loaded.Inv.GetItemCount(30025)==4,"reload can finish farewell");Check(save.WritePlayer(loaded.CharID,loaded),"complete farewell saved");
   p=New(transport,death,out sock);p.Slot=2;p.UserAcc.DataBaseID=1;db.LoadFinalData(p);QuestManager.LoadPlayerQuests(p);var restored=p.QuestPets.Values.Single(x=>x.PetID==14162);
   Check(restored.Level==35&&restored.Exp==321&&restored.Amity==65&&restored.Eq_Weapon==10063,"Roca stats/equipment preserved");Check(p.Inv.GetItemCount(30025)==4&&p.Inv.GetItemCount(25538)==1&&p.Quests[13203].Step==1,"death rewards survive reload");QuestManager.SendStoryConstellations(p);Check(sock.Packets.Last(x=>x[0]==15&&x[1]==19).SequenceEqual(new byte[]{15,19,4,1,6,8,20}),"all earned stars survive login");
   return "PASS: "+checks+" Roca death checks; native prerequisites/two battles/movies/constellation/rewards, isolated database, no live client replay.";
  }
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll','System.Xml.dll'))
try {[RocaDeathChecks]::RunAll($repo)}catch{Write-Output $_.Exception.ToString();exit 1}
