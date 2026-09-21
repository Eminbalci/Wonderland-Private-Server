param([Parameter(Mandatory=$true)][string]$BuildDirectory)
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','System.Data.SQLite.dll','Wonderland Private Server.exe')|ForEach-Object{Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
$code=@"
using System;using System.Linq;using System.Reflection;using System.Collections.Generic;using System.Collections.Concurrent;using System.Runtime.Serialization;using System.Net.Sockets;
using Game;using Game.Code;using Game.Battle;using Game.QuestRelated;using Game.PetRelated;using DataFiles;using Network;using RCLibrary.Core;using RCLibrary.Core.Networking;
public class MergeSocket:SocketClient {public List<byte[]> Packets=new List<byte[]>();public override void SendPacket(IPacket p){Packets.Add(p.Buffer.ToArray());}}
public class MergeMap:GameMap {public MergeMap(uint id){m_mapid=id;}}
public static class UpstreamMergeChecks {
 static int checks;static MergeSocket socket;static PhxItemDat dat;
 static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
 static Player New(Socket transport){
  socket=(MergeSocket)FormatterServices.GetUninitializedObject(typeof(MergeSocket));socket.Packets=new List<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  foreach(var n in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
  typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
  var p=new Player(socket,dat);p.Slot=2;p.UserAcc.DataBaseID=65001;p.CharName="MergeFixture";return p;
 }
 static void PetCommand(Player p,int ac,byte sub,params byte[] body){var b=new byte[body.Length+6];b[0]=244;b[1]=68;b[2]=(byte)(body.Length+2);b[4]=(byte)ac;b[5]=sub;Array.Copy(body,0,b,6,body.Length);var pkt=new RecievePacket(b);pkt.SetPtr(6);var t=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC"+ac)).First(x=>x!=null);t.GetMethod("ProcessPkt").Invoke(Activator.CreateInstance(t),new object[]{p,pkt});}
 public static string Run(string root){
  dat=new PhxItemDat();Check(dat.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult(),"Load item data");
  RCLibrary.Core.DataBase.Execute("CREATE TABLE merge_params(id INTEGER PRIMARY KEY,value TEXT)");
  Check(RCLibrary.Core.DataBase.Execute("INSERT INTO merge_params(id,value) VALUES(@id,@value)",new DbParam("@id",1),new DbParam("@value","O'Brien"))==1,"Parameterized insert");
  Check(RCLibrary.Core.DataBase.Query("SELECT value FROM merge_params WHERE id=@id",new DbParam("@id",1)).Rows[0][0].ToString()=="O'Brien","Parameterized query preserves apostrophe");
  Check(RCLibrary.Core.DataBase.StaticAddColumnIfNotExists("merge_params","extra","INT DEFAULT 0"),"Add schema column");
  Check(RCLibrary.Core.DataBase.StaticAddColumnIfNotExists("merge_params","extra","INT DEFAULT 0"),"Repeated schema update succeeds");
  var game=new DataBase.GameDataBase();game.VerifyCharacterPetsTable();game.VerifyCharacterPetsTable();
  Check(game.GetColumnNames("character_pets").Contains("skills")&&game.GetColumnNames("character_pets").Contains("equipment_meta"),"New migration retains local pet metadata");
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   var p=New(transport);p.CurMap=new MergeMap(60002);p.CurX=100;p.CurY=200;p.CurMap=new MergeMap(60001);
   Check(p.TentReturnMap==null,"Outdoor North Island maps are not tents");
   p.CurMap=p.Tent;Check(p.TentReturnMap!=null&&p.TentReturnMap.DstMap==60001,"Tent return records North Island field map");
   p.CurMap=new MergeMap(60001);
   var pet=new Player.PlayerPetData{PetID=12032,Slot=4,ClientSlot=2,PetName="FixturePet",Level=10,Amity=60,Str=10,Con=10,Wis=10,SkillPoints=2};p.PlayerPets[4]=pet;
   socket.Packets.Clear();PetCommand(p,68,2,2,1);Check(pet.Str==11&&pet.SkillPoints==1,"Allocation resolves client slot to DB slot");
   Check(socket.Packets.Any(b=>b.Length>10&&b[4]==8&&b[5]==2&&b[7]==2&&b[9]==28),"Allocation uses native STR stat and session slot");
   PetCommand(p,68,2,2,9);Check(pet.Str==11&&pet.SkillPoints==1,"Invalid stat does not consume points");
   PetCommand(p,68,2,4,1);Check(pet.Str==11&&pet.SkillPoints==1,"DB slot cannot target a different session pet");
   socket.Packets.Clear();PetAmityManager.OnPetDeath(p,pet);Check(pet.Amity==59&&p.PlayerPets.ContainsKey(4),"Defeat decreases amity once");
   Check(socket.Packets.Any(b=>b.Length>10&&b[4]==8&&b[5]==2&&b[7]==2&&b[9]==64),"Defeat sends native amity stat");
   pet.Amity=20;socket.Packets.Clear();PetAmityManager.OnPetDeath(p,pet);Check(!p.PlayerPets.ContainsKey(4)&&pet.ClientSlot==0,"Low-amity desertion clears DB and session roster");
   Check(socket.Packets.Any(b=>b.Length==11&&b[4]==15&&b[5]==2&&b[10]==2),"Desertion uses native dismissal packet");
   p=New(transport);p.CurMap=new MergeMap(12000);
   game.ExecuteNonQuery("CREATE TABLE charquest(pri_key INTEGER PRIMARY KEY AUTOINCREMENT,charID INT,quest_started INT,quest_pos INT,step INT)");
   var q=new QuestDefinition(64999,"Merge kill objective","FixtureNPC",QuestType.MonsterBattle){BattleMonsterID=17003,RequiredKillCount=2,InProgressDialogue="Progress",CompleteDialogue="Done"};QuestManager.RegisterQuest(q);p.Quests[64999]=new PlayerQuest(64999,QuestState.InProgress,1);
   QuestManager.OnMonsterDefeated(p,17004,"Other");Check(p.Quests[64999].CurrentKillCount==0,"Unrelated kills ignored");
   QuestManager.OnMonsterDefeated(p,17003,"Grape Mons");Check(p.Quests[64999].CurrentKillCount==1,"Target kill counted");
   QuestManager.SavePlayerQuest(p,64999);p.Quests.Clear();QuestManager.LoadPlayerQuests(p);Check(p.Quests[64999].CurrentKillCount==1,"Kill progress survives reload");
   QuestManager.OnMonsterDefeated(p,17003,"Grape Mons");Check(p.Quests[64999].CurrentKillCount==2,"Second target kill counted");
   var done=new DateTime(2026,9,20,12,0,0,DateTimeKind.Utc);p.Quests[64999].State=QuestState.Completed;p.Quests[64999].CompletedAt=done;QuestManager.SavePlayerQuest(p,64999);p.Quests.Clear();QuestManager.LoadPlayerQuests(p);Check(p.Quests[64999].CompletedAt==done,"Repeatable quest completion time survives reload");
  }
  foreach(Affinity a in new[]{Affinity.Earth,Affinity.Water,Affinity.Fire,Affinity.Wind})Check(PvEBattleManager.GetElementalMultiplier((byte)a,(byte)a)==1,"Same-element neutrality");
  Check(PvEBattleManager.GetElementalMultiplier((byte)Affinity.Earth,(byte)Affinity.Water)==1.7,"Earth advantage");
  Check(PvEBattleManager.GetElementalMultiplier((byte)Affinity.Fire,(byte)Affinity.Wind)==1.5,"Fire advantage");
  Check(PvEBattleManager.GetElementalMultiplier((byte)Affinity.Water,(byte)Affinity.Earth)==0.6,"Water disadvantage");
  Check(PvEBattleManager.GetElementalMultiplier(0,0)==1,"Unknown element neutral");
  return checks+" upstream merge integration checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll','System.Data.dll','System.Xml.dll'))
[UpstreamMergeChecks]::Run((Split-Path $PSScriptRoot -Parent))
