param([string]$BuildDirectory = (Join-Path $PSScriptRoot 'props-phase-bin'))
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','System.Data.SQLite.dll') | ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
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
 public override void SendPacket(IPacket packet){var raw=packet.Buffer.ToArray();
  for(int offset=0;offset<raw.Length;){
   if(offset+4>raw.Length||raw[offset]!=244||raw[offset+1]!=68)throw new Exception("Invalid packet framing");
   int size=BitConverter.ToUInt16(raw,offset+2);
   if(offset+4+size>raw.Length)throw new Exception("Truncated packet");
   Packets.Add(raw.Skip(offset+4).Take(size).ToArray());offset+=4+size;
  }}
}
public static class PropsPhaseChecks {
 static int checks;
 static List<string> failures=new List<string>();
 static DataBase.GameDataBase db;
 static global::DataFiles.PhxItemDat items;
 static BindingFlags hidden=BindingFlags.NonPublic|BindingFlags.Static;
 static MethodInfo find=typeof(EveEventInterpreter).GetMethod("FindBranch",hidden);
 static MethodInfo start=typeof(EveEventInterpreter).GetMethod("StartSession",hidden);
 static MethodInfo execute=typeof(EveEventInterpreter).GetMethod("ExecuteOpcode",hidden);
 static void Check(bool ok,string name){checks++;if(!ok){failures.Add(name);Console.WriteLine("FAILED: "+name);}}
 static GameMap Map(ushort id){var m=new GameMap();typeof(GameMap).GetField("m_mapid",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(m,(uint)id);return m;}
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
 static void Advance(Player p){Check(p.ContinueInteraction(),"pending interaction advances");}
 static void Drain(Player p){int n=0;while(p.NativeEventActive&&p.OnInteractionComplete!=null&&n++<80)Advance(p);Check(n<80,"bounded event");}
 static void Choose(Player p,byte answer){var callback=p.OnDialogueChoice;p.OnDialogueChoice=null;Check(callback!=null,"choice is armed");callback(answer);}
 static void Mark(Player p,uint id,byte step){p.Quests[id]=new PlayerQuest(id,QuestState.InProgress,step);}
 static void Fill(Player p,int free){for(byte slot=1;slot<=50-free;slot++){var item=new Game.Code.Item(items.GetItemByID(34014));item.Ammt=50;p.Inv.AddItem(item,slot,false);}}
 static Player.PlayerPetData Pet(uint id,byte slot){return new Player.PlayerPetData{PetID=id,Slot=slot,ClientSlot=slot,PetName="Fixture pet",Amity=60};}
 // Wire semantics verified in the current aLogin.exe: AC22:10 calls 0x4187d8
 // (hide); AC22:11 calls 0x418828 (show). They are opposing operations.
 static Dictionary<ushort,bool> Visible(RuntimeSocket socket){
  var visible=new Dictionary<ushort,bool>();
  foreach(var b in socket.Packets){
   if(b.Length<2||b[0]!=22)continue;
   if(b[1]==4){
    if((b.Length-2)%14!=0)throw new Exception("Invalid AC22:4 actor record length");
    for(int i=2;i<b.Length;i+=14){
     ushort id=BitConverter.ToUInt16(b,i);uint duration=BitConverter.ToUInt32(b,i+9);
     visible[id]=b[i+8]==1&&duration==0;
    }
   }else if((b[1]==10||b[1]==11)&&b.Length==6){
    visible[BitConverter.ToUInt16(b,2)]=b[1]==11;
   }
  }
  return visible;
 }
 static void Actor(Player p,RuntimeSocket s,ushort id,bool expected,string name){
  var state=Visible(s);Check(state.ContainsKey(id)&&state[id]==expected,name+" client actor "+id);
  Check(p.HiddenNpcClickIDs.Contains(id)==!expected,name+" server cache "+id);
 }
 static GameMap NativeMap(ushort id){
  var m=Map(id);
  foreach(var n in db.EveDat.GetMapData(id).Npclist)
   m.NPCs.Add(new QuestNpc{MapID=id,CickID=n.clickId,TemplateID=n.npcId,Name=n.Name,X=(ushort)n.x,Y=(ushort)n.y,SpawnX=(ushort)n.x,SpawnY=(ushort)n.y,WalkBehavior=n.unknownbyte4,WalkSteps=n.walksteps});
  return m;
 }
 static void Enter(Player p,GameMap m){
  typeof(GameMap).GetMethod("SendMapInfo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,new object[]{p,false});
  QuestManager.ReplayActorVisibility(p,m);
  PreEventInterpreter.EvaluateMapPreEvents(p,(ushort)m.MapID);
 }
 static byte FrameOf(RuntimeSocket s,ushort id){
  byte result=255;
  foreach(var b in s.Packets.Where(b=>b.Length>=2&&b[0]==22&&b[1]==4))
   for(int i=2;i<b.Length;i+=14)if(BitConverter.ToUInt16(b,i)==id)result=b[i+2];
  return result;
 }
 static void Phase(Player p,GameMap map,RuntimeSocket s,ushort[] expected,string label){
  s.Packets.Clear();Enter(p,map);
  foreach(var npc in map.NPCs)Actor(p,s,npc.CickID,expected.Contains(npc.CickID),label);
 }
 public static string RunAll(string root){
  DataBase.GameDataBase.GlobalInstance=null;int init=QuestManager.Count;
  Game.Battle.MonsterDropManager.ItemNameResolver=id=>"Item "+id;
  SceneDataManager.Initialize(root+"/Data");
  items=new global::DataFiles.PhxItemDat();items.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult();
  db=new DataBase.GameDataBase();db.DatabaseFile=root+"/.codex-verify/props-phase-fixture-"+Guid.NewGuid().ToString("N")+".db";
  db.ExecuteNonQuery("CREATE TABLE charquest (pri_key INTEGER PRIMARY KEY, charID INTEGER, quest_started INTEGER, quest_pos INTEGER, step INTEGER)");
  db.EveDat.LoadFile(root+"/Data/eve.Emg");
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   RuntimeSocket sock;var house=NativeMap(12002);var p=New(transport,house,out sock);
   Phase(p,house,sock,new ushort[]{1,2,3,4,5},"Xaolan initial house");
   p.Quests[13004]=new PlayerQuest(13004,QuestState.Completed,1);
   Phase(p,house,sock,new ushort[]{1,2,3,4,5},"reported player cleared mark");
   p.Quests[13032]=new PlayerQuest(13032,QuestState.Completed,1);Mark(p,13033,1);
   Phase(p,house,sock,new ushort[]{1,2,3},"after pirates defeated before recruitment");
   Mark(p,13033,1);Mark(p,13004,1);
   Phase(p,house,sock,new ushort[]{1,6,7},"after rescue and recruitment scene");
   Mark(p,13004,2);
   Phase(p,house,sock,new ushort[]{1,7},"grandmother left");
   p.Quests[13004]=new PlayerQuest(13004,QuestState.Completed,1);Mark(p,13005,1);
   Phase(p,house,sock,new ushort[]{1,7},"completed Xaolan absent may rejoin");
   p.PlayerPets[1]=Pet(14156,1);
   Phase(p,house,sock,new ushort[]{1},"Xaolan recruited");
   // Capture intact prop frames on real maps, followed by idle server ticks.
   int props=0;
   foreach(ushort mapId in new ushort[]{12000,12002,12005,12006,12007,12009}){
    var map=NativeMap(mapId);p=New(transport,map,out sock);map.PlayersList.Add(p);Enter(p,map);
    var state=Visible(sock);
    foreach(QuestNpc npc in map.NPCs){
     var info=SceneDataManager.GetNpcBaseStats(npc.TemplateID);
     if(info==null||(info.Type!=6&&info.Type!=9&&info.Type!=10))continue;
     props++;
     if(state.ContainsKey(npc.CickID)&&state[npc.CickID])Check(FrameOf(sock,npc.CickID)==0,"intact prop fixed frame map "+mapId+" actor "+npc.CickID);
     PreEventInterpreter.SendActorHide(p,npc.CickID);PreEventInterpreter.SendActorShow(p,npc.CickID);
     Check(FrameOf(sock,npc.CickID)==0,"reveal retains fixed prop frame map "+mapId+" actor "+npc.CickID);
     int count=sock.Packets.Count;
     for(int tick=0;tick<120;tick++)npc.Update(DateTime.Now.AddMilliseconds(tick*100),map);
     Check(sock.Packets.Count==count,"idle prop sends no repeated animation/visibility map "+mapId+" actor "+npc.CickID);
    }
   }
   Check(props>=8,"real pots jars honeycomb and quest props covered");
   var humanMap=NativeMap(12002);p=New(transport,humanMap,out sock);Enter(p,humanMap);
   Check(FrameOf(sock,2)==255&&FrameOf(sock,4)==255,"humans and hijackers keep native idle animation");
  }
  if(failures.Count>0)throw new Exception(failures.Count+" failures / "+checks+" checks: "+string.Join("; ",failures));
  return "PASS: "+checks+" prop frame and Xaolan phase checks (real EVE/Npc.dat, isolated database).";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll'))
try {[PropsPhaseChecks]::RunAll((Split-Path $PSScriptRoot -Parent))}catch{Write-Output $_.Exception.ToString();exit 1}
