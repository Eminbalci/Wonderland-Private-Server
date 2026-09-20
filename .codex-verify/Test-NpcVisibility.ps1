param([string]$BuildDirectory = (Join-Path $PSScriptRoot 'npc-visibility-bin'))
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
public static class NpcVisibilityChecks {
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
   m.NPCs.Add(new QuestNpc{MapID=id,CickID=n.clickId,TemplateID=n.npcId,Name=n.Name,X=(ushort)n.x,Y=(ushort)n.y,SpawnX=(ushort)n.x,SpawnY=(ushort)n.y});
  return m;
 }
 static void Enter(Player p,GameMap m){
  typeof(GameMap).GetMethod("SendMapInfo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,new object[]{p,false});
  QuestManager.ReplayActorVisibility(p,m);
  PreEventInterpreter.EvaluateMapPreEvents(p,(ushort)m.MapID);
 }
 public static string RunAll(string root){
  DataBase.GameDataBase.GlobalInstance=null;int init=QuestManager.Count;
  Game.Battle.MonsterDropManager.ItemNameResolver=id=>"Item "+id;
  items=new global::DataFiles.PhxItemDat();items.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult();
  db=new DataBase.GameDataBase();db.DatabaseFile=root+"/.codex-verify/npc-visibility-fixture-"+Guid.NewGuid().ToString("N")+".db";
  db.ExecuteNonQuery("CREATE TABLE charquest (pri_key INTEGER PRIMARY KEY, charID INTEGER, quest_started INTEGER, quest_pos INTEGER, step INTEGER)");
  db.EveDat.LoadFile(root+"/Data/eve.Emg");
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   RuntimeSocket sock,otherSocket;
   foreach(ushort mapId in new ushort[]{12000,12001}){
    var map=NativeMap(mapId);
    foreach(uint petId in new uint[]{0,14161,14162}){
     var p=New(transport,map,out sock);
     if(petId!=0)p.PlayerPets[1]=Pet(petId,1);
     for(int round=0;round<2;round++){
      sock.Packets.Clear();Enter(p,map);
      foreach(var npc in map.NPCs){
       bool expected=PreEventInterpreter.ShouldNpcBeVisible(p,mapId,npc.CickID);
       Actor(p,sock,npc.CickID,expected,"map "+mapId+" pet "+petId+" entry "+round);
      }
     }
     byte target=(byte)(mapId==12000?32:3);
     Check(!map.ProcessInteraction(target,p),"hidden actor rejects interaction");
     Actor(p,sock,target,false,"hidden click stays hidden");
     var other=New(transport,map,out otherSocket);Enter(other,map);
     Actor(other,otherSocket,(ushort)(mapId==12000?32:2),mapId==12001,"other player's phase is independent");
    }
   }
   var village=NativeMap(12000);var player=New(transport,village,out sock);player.PlayerPets[1]=Pet(17036,1);Enter(player,village);
   Actor(player,sock,25,true,"captured rabbit does not hide native rabbit");
   // Replay actual path/movie completion, not just internal visibility flags.
   player=New(transport,village,out sock);Mark(player,12020,1);Run(player,village,21,Pick(player,village,21));Drain(player);
   Actor(player,sock,15,false,"pig after path");Actor(player,sock,14,true,"pig returned");
   player=New(transport,village,out sock);Mark(player,13046,1);Run(player,village,26,Pick(player,village,26));Drain(player);
   Actor(player,sock,20,false,"dog after path");Actor(player,sock,28,true,"dog returned");
   var fate=NativeMap(12003);player=New(transport,fate,out sock);Mark(player,13042,1);player.CurX=600;player.CurY=480;
   Check(EveEventInterpreter.TryExecuteRegion(player,fate,400,400),"Fate starts movie");Drain(player);
   Actor(player,sock,1,false,"Fate old actor");Actor(player,sock,2,true,"Fate new actor");
   // Shared renewable prop: both clients and both caches must agree through hide/show/respawn.
   var shared=Map(0);var prop=new QuestNpc{CickID=1,Name="Coconut",TemplateID=0,X=100,Y=100};shared.NPCs.Add(prop);
   player=New(transport,shared,out sock);var viewer=New(transport,shared,out otherSocket);shared.PlayersList.Add(player);shared.PlayersList.Add(viewer);
   var hide=new EventSubSubEntry{DialogPtr=2,dialog1=1,dialog2=2};var show=new EventSubSubEntry{DialogPtr=2,dialog1=1,dialog2=3};
   execute.Invoke(null,new object[]{player,shared,(ushort)1,null,null,hide});
   Actor(player,sock,1,false,"shared hide owner");Actor(viewer,otherSocket,1,false,"shared hide viewer");
   execute.Invoke(null,new object[]{player,shared,(ushort)1,null,null,show});
   Actor(player,sock,1,true,"shared show owner");Actor(viewer,otherSocket,1,true,"shared show viewer");
   execute.Invoke(null,new object[]{player,shared,(ushort)1,null,null,hide});prop.Update(DateTime.Now.AddMinutes(2),shared);
   Actor(player,sock,1,true,"respawn owner");Actor(viewer,otherSocket,1,true,"respawn viewer");Check(!prop.IsBroken,"respawn clears broken state");
  }
  if(failures.Count>0)throw new Exception(failures.Count+" failures / "+checks+" checks: "+string.Join("; ",failures));
  return "PASS: "+checks+" NPC wire visibility checks (real EVE, native packet semantics, isolated database).";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll'))
try {[NpcVisibilityChecks]::RunAll((Split-Path $PSScriptRoot -Parent))}catch{Write-Output $_.Exception.ToString();exit 1}
