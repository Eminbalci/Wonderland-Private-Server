param([string]$BuildDirectory = (Join-Path $PSScriptRoot 'quest-runtime-bin'))
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
 public override void SendPacket(IPacket packet){Packets.Add(packet.Buffer.Skip(4).ToArray());}
}
public static class QuestRuntimeChecks {
 static int checks;
 static DataBase.GameDataBase db;
 static global::DataFiles.PhxItemDat items;
 static BindingFlags hidden=BindingFlags.NonPublic|BindingFlags.Static;
 static MethodInfo find=typeof(EveEventInterpreter).GetMethod("FindBranch",hidden);
 static MethodInfo start=typeof(EveEventInterpreter).GetMethod("StartSession",hidden);
 static MethodInfo execute=typeof(EveEventInterpreter).GetMethod("ExecuteOpcode",hidden);
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAILED: "+name);checks++;}
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
 public static string RunAll(string root){
  DataBase.GameDataBase.GlobalInstance=null;int init=QuestManager.Count;Game.Battle.MonsterDropManager.ItemNameResolver=id=>"Item "+id;
  items=new global::DataFiles.PhxItemDat();items.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult();
  db=new DataBase.GameDataBase();db.DatabaseFile=root+"/.codex-verify/quest-runtime-fixture-"+Guid.NewGuid().ToString("N")+".db";db.ExecuteNonQuery("CREATE TABLE charquest (pri_key INTEGER PRIMARY KEY, charID INTEGER, quest_started INTEGER, quest_pos INTEGER, step INTEGER)");db.EveDat.LoadFile(root+"/Data/eve.Emg");
  Game.Battle.MonsterDropManager.ItemNameResolver=id=>"Item "+id;
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   RuntimeSocket sock;var chief=Map(12001);var p=New(transport,chief,out sock);
   Check(Pick(p,chief,3).subIndex==1,"fresh chief asks before recruiting");
   Check(Pick(p,chief,6)==null&&Pick(p,chief,7)==null,"fresh player cannot return scroll/recruit via later flags");
   Check(Pick(p,chief,3,7,1,30).subIndex==5,"yes with space selects recruitment");
   for(byte i=1;i<=4;i++)p.PlayerPets[i]=Pet((uint)(17000+i),i);
   Check(Pick(p,chief,3,7,1,30).subIndex==3,"yes with full party selects full-party dialogue");
   Check(Pick(p,chief,3,7,2,30)==null,"wrong question cannot choose a branch");
   p.PlayerPets.Clear();Mark(p,13044,3);
   Check(Pick(p,chief,6)==null,"no scroll no Roca cannot return");
   p.Inv.AddItem(30001,1);Check(Pick(p,chief,6)==null,"scroll alone cannot return");
   p.PlayerPets[1]=Pet(14161,1);Check(Pick(p,chief,6).subIndex==1,"scroll and Roca alias enable return");
   p.ClearInteraction();p.Inv.RemoveItem(30001,1);Mark(p,13052,1);Mark(p,13098,1);p.PlayerPets.Clear();
   Check(PreEventInterpreter.ShouldNpcBeVisible(p,12000,34),"Roca history does not hide grave actor when absent");
   Check(!PreEventInterpreter.ShouldNpcBeVisible(p,12001,2),"active grave quest keeps home Roca hidden per native PreEvents");
   var village=Map(12000);p=New(transport,village,out sock);p.PlayerPets[1]=Pet(17036,1);
   Check(PreEventInterpreter.ShouldNpcBeVisible(p,12000,25)&&PreEventInterpreter.ShouldNpcBeVisible(p,12000,26),"captured rabbit does not hide minigame rabbits");
   var rabbit=new QuestNpc{MapID=12000,CickID=25,TemplateID=17036,Name="Rabbit"};Check(!rabbit.IsWildMonster(),"linked rabbit event takes priority over combat");
   var wild=new QuestNpc{MapID=0,CickID=99,TemplateID=17036,Name="Rabbit"};Check(wild.IsWildMonster(),"unlinked rabbit remains wild");
   // Pig: two dialogues, native path, hide/show, final dialogue, only then progress.
   p=New(transport,village,out sock);Mark(p,12020,1);Run(p,village,21,Pick(p,village,21));
   FrameIs(sock,1,20256,"pig starts with player dialogue");Check(p.OnDialogueChoice==null,"future prompts are not armed early");
   Advance(p);FrameIs(sock,1,20257,"pig second dialogue");
   Advance(p);FrameIs(sock,4,0,"pig native path frame");var path=Frame(sock);
   Check(BitConverter.ToUInt16(path,8)==15&&path[10]==1&&BitConverter.ToUInt32(path,11)==1,"pig path actor mode and index");
   Check(p.Quests[12020].Step==1&&!p.HiddenNpcClickIDs.Contains(15),"pig remains visible and quest unchanged while running");
   int count=sock.Packets.Count;Check(EveEventInterpreter.TryExecute(p,village,15)&&sock.Packets.Count==count,"repeat click cannot restart running cutscene");
   Advance(p);FrameIs(sock,1,20259,"pig final dialogue follows motion");
   Check(p.HiddenNpcClickIDs.Contains(15)&&!p.HiddenNpcClickIDs.Contains(14),"pig actor switch occurs after path ACK");
   Check(p.Quests[12020].Step==1,"pig progress waits for last dialogue");Advance(p);
   Check(!p.NativeEventActive&&p.Quests[12020].Step==2,"pig completes once");p.ContinueInteraction();Check(p.Quests[12020].Step==2,"extra ACK does not advance quest twice");
   // Dog path must likewise complete before progress.
   p=New(transport,village,out sock);Mark(p,13046,1);Run(p,village,26,Pick(p,village,26));
   FrameIs(sock,1,30606,"dog opening dialogue");Advance(p);FrameIs(sock,1,30012,"dog reply");Advance(p);FrameIs(sock,4,0,"dog native path");
   Check(BitConverter.ToUInt16(Frame(sock),8)==20&&p.Quests[13046].Step==1,"dog awaits animation");
   Advance(p);Check(p.Quests[13046].Step==2&&p.HiddenNpcClickIDs.Contains(20)&&!p.HiddenNpcClickIDs.Contains(28),"dog switches actors and advances after ACK");
   // Cancelling a session invalidates even a previously captured callback.
   p=New(transport,village,out sock);Mark(p,13046,1);Run(p,village,26,Pick(p,village,26));var old=p.OnInteractionComplete;p.ClearInteraction();count=sock.Packets.Count;old();
   Check(sock.Packets.Count==count&&p.Quests[13046].Step==1,"cancelled callback has no effect");
   // Quest reward capacity and explicit completion flag.
   p=New(transport,village,out sock);Mark(p,13046,2);Check(Pick(p,village,38).subIndex==7,"dog reward when bag has room");Fill(p,0);
   Check(Pick(p,village,38).subIndex==10,"full bag chooses error branch");Mark(p,13047,1);Check(Pick(p,village,38)==null,"completed flag blocks repeated dog reward");
   // Unknown Fate: entry -> movie 11003 -> rewards, two free slots required.
   var fate=Map(12003);p=New(transport,fate,out sock);Mark(p,13042,1);p.CurX=600;p.CurY=480;
   Check(EveEventInterpreter.TryExecuteRegion(p,fate,400,400),"enter astrologer doorway triggers event");FrameIs(sock,5,0,"Fate sends movie instead of fanfare");
   Check(Frame(sock)[10]==2&&BitConverter.ToUInt32(Frame(sock),11)==11003,"Fate movie id reconstructed from full EVE value");
   Check(p.Inv.GetItemCount(36002)==0&&p.Inv.GetItemCount(34038)==0&&p.Quests[13042].State==QuestState.InProgress,"Fate waits before granting items and flags");
   Advance(p);Drain(p);Check(p.Inv.GetItemCount(36002)==1&&p.Inv.GetItemCount(34038)==1,"Fate gives exactly one tent and notebook");
   Check(p.Quests[13043].Step==1&&!p.NativeEventActive,"Fate completion flag and release");
   Check(p.HiddenNpcClickIDs.Contains(1)&&!p.HiddenNpcClickIDs.Contains(2),"Fate actor visibility survives end-of-event synchronization");
   Check(!EveEventInterpreter.TryExecuteRegion(p,fate,400,400),"completed Fate does not replay");
   p=New(transport,fate,out sock);Mark(p,13042,1);Fill(p,1);Check(Pick(p,fate,4).subIndex==4,"Fate requires two free slots");
   p=New(transport,fate,out sock);Mark(p,13042,1);p.CurX=100;p.CurY=100;Check(!EveEventInterpreter.TryExecuteRegion(p,fate,0,0),"Fate does not trigger outside doorway");
   p=New(transport,fate,out sock);Mark(p,13042,1);p.CurX=600;p.CurY=480;EveEventInterpreter.TryExecuteRegion(p,fate,400,400);Fill(p,1);Advance(p);
   Check(p.Inv.GetItemCount(36002)==0&&!p.Quests.ContainsKey(13043)&&p.Quests[13042].State==QuestState.InProgress,"bag changed during movie cannot cause partial reward or complete flags");
   Check(!p.NativeEventActive,"failed delivery releases input");
   // Native add operation uses encoded amount, not always +1.
   p=New(transport,chief,out sock);Mark(p,13098,1);var ev=Event(chief,6);var branch=ev.SubEntry.First(s=>s.subIndex==12);var op=branch.SubEntry.Last();
   Check((bool)execute.Invoke(null,new object[]{p,chief,(ushort)1,ev,branch,op})&&p.Quests[13098].Step==3,"quest mark adds encoded two steps");
   // Leaving and amity must never recruit another copy.
   p.PlayerPets[1]=Pet(14161,1);var amity=new EventSubSubEntry{DialogPtr=3,dialog1=5,dialog2=14162,dialog4=512};
   Check((bool)execute.Invoke(null,new object[]{p,chief,(ushort)1,ev,branch,amity})&&p.PlayerPets.Count==1&&p.PlayerPets[1].Amity==62,"companion mode5 increases amity only");
   var leave=new EventSubSubEntry{DialogPtr=3,dialog1=2,dialog2=14162};Check((bool)execute.Invoke(null,new object[]{p,chief,(ushort)1,ev,branch,leave})&&p.PlayerPets.Count==0,"companion mode2 leaves party");
   // Rabbit game must use EVE's carrot reward, not the generic voucher.
   p=New(transport,village,out sock);Run(p,village,41,Pick(p,village,41));FrameIs(sock,6,12,"rabbit asks to play");Choose(p,30);
   Check(p.OnMinigameWon!=null&&p.NativeEventActive,"rabbit game owns the interaction while running");var won=p.OnMinigameWon;won();FrameIs(sock,1,20363,"rabbit win dialogue before reward");Drain(p);
   Check(p.Inv.GetItemCount(32102)==10&&p.Inv.GetItemCount(30002)==0,"rabbit reward is ten carrots not voucher");count=p.Inv.GetItemCount(32102);won();Check(p.Inv.GetItemCount(32102)==count,"stale minigame callback cannot pay twice");
   p=New(transport,village,out sock);Run(p,village,41,Pick(p,village,41));Choose(p,30);p.OnMinigameLost();FrameIs(sock,1,20364,"rabbit loss has correct branch");Drain(p);Check(p.Inv.GetItemCount(32102)==0,"loss never rewards");
   Run(p,village,41,Pick(p,village,41));FrameIs(sock,6,12,"retry clears temporary mark and asks again");
   p.ClearInteraction();
   var ship=Map(10017);p=New(transport,ship,out sock);Check(Pick(p,ship,2).subIndex==1,"unconditional ship greeting remains available");
   Run(p,ship,2,Pick(p,ship,2));FrameIs(sock,1,30126,"ship dialogue still renders");Drain(p);
   p=New(transport,village,out sock);Check(Pick(p,village,25,4,1,1).subIndex==6,"battle selects declared victory branch");
   Check(Pick(p,village,25,4,1,2).subIndex==7,"battle selects declared loss branch");
   Check(Pick(p,village,25,4,2,1)==null,"wrong battle id cannot claim outcome");
   var noBagChange=p.Inv.GetItemCount(36002);Check(p.Inv.CanAddItems(new Dictionary<ushort,int>{{36002,1},{34038,1}})&&p.Inv.GetItemCount(36002)==noBagChange,"delivery preflight is read-only");
   Mark(p,13046,1);Run(p,village,26,Pick(p,village,26));old=p.OnInteractionComplete;p.CurMap=fate;count=sock.Packets.Count;old();Check(sock.Packets.Count==count&&!p.NativeEventActive,"changing map invalidates pending event callback");
   // EVE PreEvent evaluator uses same mark semantics, including absent = 0.
   var condition=typeof(PreEventInterpreter).GetMethod("EvaluateConditionBlock",hidden);
   var data=new byte[21];data[0]=5;Array.Copy(BitConverter.GetBytes((ushort)12021),0,data,1,2);data[3]=1;data[5]=1;data[7]=5;
   Check((bool)condition.Invoke(null,new object[]{p,data}),"absent completion mark equals zero");Mark(p,12021,1);Check(!(bool)condition.Invoke(null,new object[]{p,data}),"set completion mark fails zero condition");
  }
  return "PASS: "+checks+" quest runtime checks (real EVE, in-memory players, isolated fixture database).";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll'))
try {[QuestRuntimeChecks]::RunAll((Split-Path $PSScriptRoot -Parent))}catch{Write-Output $_.Exception.ToString();exit 1}
