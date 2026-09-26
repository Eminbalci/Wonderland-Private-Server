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



   var village=Map(11005);var bottom=Map(11162);
   foreach(int eventId in new[]{25,26})foreach(byte checkpoint in new byte[]{2,3,255}){
    p=New(transport,village,out sock);if(checkpoint==255)Mark(p,15069,1);else Mark(p,15068,checkpoint);
    Run(p,village,eventId,Pick(p,village,eventId));
    // Installed client 0x30615b sets completionPending for question mode 0;
    // 0x306171 clears it for mode 1. Choice close clears waitingForChoice.
    // 0x3074af then sends a stray AC20:6 before the new CG is complete.
    bool choiceCompletionPending=Frame(sock)[10]==0;
    Input(p,20,9,30);
    if(choiceCompletionPending)Input(p,20,6);
    Check(!choiceCompletionPending,"well choice suppresses its own completion packet");
    var frame=Frame(sock);Check(frame[6]==5&&BitConverter.ToUInt32(frame,11)==25010,"choice closure cannot queue the second movie");
    Check(frame[10]==1,"first CG must not latch the map-load hold flag");
    Check(p.CurMap==village,"no warp before first CG completes");
    // Client FUN_0031ee6c / FUN_0035a560 / main-loop +0x566:
    // mode 2 latches render hold, preventing a following movie from advancing.
    bool renderHeld=frame[10]==2;
    Advance(p);
    frame=Frame(sock);
    Check(!renderHeld,"client renderer can run second CG");
    Check(frame[6]==5&&BitConverter.ToUInt32(frame,11)==156&&frame[10]==2,"full final CG retains map-transition hold");
    Check(p.CurMap==village&&p.OnInteractionComplete!=null,"wait for second CG completion before warp");
    Advance(p);
    Check(p.CurMap==bottom&&p.CurX==1696&&p.CurY==300,"two completed CGs reach well bottom "+eventId+"/"+checkpoint);
    Check(!p.NativeEventActive&&p.OnInteractionComplete==null,"warp releases event callback");
   }
   // Repeat down/up/down on the same player/socket, with native packet choices.
   for(int loop=0;loop<3;loop++){
    if(loop==0){p=New(transport,village,out sock);Mark(p,15068,2);}
    Run(p,village,26,Pick(p,village,26));Check(Frame(sock)[10]==1,"repeat well choice has no trailing completion");
    Input(p,20,9,30);Check(BitConverter.ToUInt32(Frame(sock),11)==25010,"repeat jump first");
    Input(p,20,6);Check(BitConverter.ToUInt32(Frame(sock),11)==156&&p.CurMap==village,"repeat second movie waits");
    Input(p,20,6);Check(p.CurMap==bottom,"repeat arrival after both movie completions");
    p.CurX=1722;p.CurY=295;Input(p,20,4,2,0);Input(p,20,9,30);Check(p.CurMap==village,"repeat climb out");
   }
   p=New(transport,village,out sock);Mark(p,15068,1);var rope=new Game.Code.Item(items.GetItemByID(37200));rope.Ammt=1;p.Inv.AddItem(rope,1,false);
   Run(p,village,25,Pick(p,village,25));Advance(p);Check(BitConverter.ToUInt32(Frame(sock),11)==25010&&Frame(sock)[10]==1,"first rope use reaches jump CG");Advance(p);
   Check(BitConverter.ToUInt32(Frame(sock),11)==156&&p.Inv.GetItemCount(37200)==1,"rope remains until second CG finishes");Advance(p);
   Check(p.CurMap==bottom&&p.Inv.GetItemCount(37200)==0&&p.Quests[15068].Step==2,"first jump consumes rope and persists checkpoint before warp");
   foreach(byte answer in new byte[]{31,40}){p=New(transport,village,out sock);Mark(p,15068,2);Run(p,village,26,Pick(p,village,26));Choose(p,answer);Check(p.CurMap==village&&!p.NativeEventActive,"decline and close never warp "+answer);}
   p=New(transport,village,out sock);Mark(p,15068,1);Run(p,village,25,Pick(p,village,25));Drain(p);Check(p.CurMap==village&&p.Quests[15068].Step==1,"missing rope remains gated");
   foreach(byte answer in new byte[]{30,31,40}){
    p=New(transport,bottom,out sock);p.CurX=1722;p.CurY=295;
    Input(p,20,4,2,0);
    Check(p.NativeEventActive&&p.OnDialogueChoice!=null,"live region packet opens rope prompt");
    var frame=Frame(sock);Check(frame[6]==6&&BitConverter.ToUInt16(frame,15)==1,"authored question 1");
    int count=sock.Packets.Count;var token=p.OnDialogueChoice;
    Input(p,20,4,2,0);Check(sock.Packets.Count==count&&p.OnDialogueChoice==token,"duplicate region keeps same prompt");
    Input(p,20,6);Check(p.OnDialogueChoice!=null&&p.OnDialogueChoice==token,"late completion cannot dismiss rope choice");
    Input(p,20,9,answer);
    if(answer==30)Check(p.CurMap==village&&p.CurX==554&&p.CurY==1223,"packet choice Yes climbs out");
    else Check(p.CurMap==bottom&&!p.NativeEventActive&&p.OnDialogueChoice==null,"packet decline/close unlocks "+answer);
   }
   foreach(ushort id in new ushort[]{0,1,3,65535}){
    p=New(transport,bottom,out sock);p.CurX=1722;p.CurY=295;Input(p,20,4,(byte)id,(byte)(id>>8));
    Check(!p.NativeEventActive&&p.CurMap==bottom&&sock.Packets.Any(b=>b.SequenceEqual(new byte[]{20,8})),"unknown region/door cannot warp and releases client "+id);
   }
   foreach(ushort x in new ushort[]{0,1000,1719,1860,65535}){
    p=New(transport,bottom,out sock);p.CurX=x;p.CurY=295;Input(p,20,4,2,0);
    Check(!p.NativeEventActive&&p.CurMap==bottom,"remote region request rejected "+x);
   }
   foreach(byte[] payload in new[]{new byte[]{20,4},new byte[]{20,4,2},new byte[]{20,4,2,0,0}}){
    p=New(transport,bottom,out sock);p.CurX=1722;p.CurY=295;Input(p,payload);
    Check(!p.NativeEventActive&&p.CurMap==bottom&&sock.Packets.Any(b=>b.SequenceEqual(new byte[]{20,8})),"malformed region releases client");
   }
   p=New(transport,bottom,out sock);p.CurX=1765;p.CurY=200;Input(p,20,4,2,0);Check(p.OnDialogueChoice!=null,"inside exact rope rectangle");p.CancelInteraction();
   Map(11159);Run(p,bottom,1,Pick(p,bottom,1));Check(p.CurMap.MapID==11159,"well bottom still leads to lever room");
   return "PASS: "+checks+" well animation and region packet checks.";
  }
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll','System.Xml.dll'))
try {[WellTransitionChecks]::RunAll($repo)}catch{Write-Output $_.Exception.ToString();exit 1}
