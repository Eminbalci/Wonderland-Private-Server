param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'mall-forging-bin'),[switch]$ExpectDoubleMove)
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','Wonderland Private Server.exe','System.Data.SQLite.dll') | ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
$code=@"
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Net.Sockets;
using DataFiles;
using Game;
using Game.Code;
using Game.PlayerRelated;
using Network;

using RCLibrary.Core.Networking;
public class ForgeSocket : SocketClient {
 public List<byte[]> Packets;
 public override void SendPacket(IPacket p){Packets.Add(p.Buffer.ToArray());}
}
public static class ForgeChecks {
 static Socket transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
 static int checks; static PhxItemDat dat; static Type handlerType; static ForgeSocket socket;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static Player NewPlayer(){
  socket=(ForgeSocket)FormatterServices.GetUninitializedObject(typeof(ForgeSocket));
  socket.Packets=new List<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  foreach(var name in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
  typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
  var p=new Player(socket,dat);p.UserAcc.IM=1000;p.UserAcc.IMBonus=500;p.Eqs.Con=50;p.Eqs.Wis=30;p.Eqs.CurHP=1;p.Eqs.CurSP=1;return p;
 }
 static void Add(Player p,ushort id,byte slot,byte count){var item=new InvItem();item.CopyFrom(dat.GetItemByID(id));item.Ammt=count;Check(p.Inv.AddItem(item,slot,false)==count,"Fixture insertion "+id);}
 static void Command(Player p,byte ac,byte sub,params byte[] data){socket.Packets.Clear();var bytes=new byte[data.Length+6];bytes[0]=244;bytes[1]=68;bytes[2]=(byte)(data.Length+2);bytes[3]=(byte)((data.Length+2)>>8);bytes[4]=ac;bytes[5]=sub;Array.Copy(data,0,bytes,6,data.Length);p.ProcessSocket((IPacket)new RecievePacket(bytes));Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==2&&b[5]==4),"Mall feedback never emits GM chat");}
 static byte[] Cart(params MallPurchaseRequest[] rows){var bytes=new List<byte>{(byte)rows.Length};foreach(var r in rows){bytes.AddRange(BitConverter.GetBytes(r.ItemID));bytes.Add(r.CategoryID);bytes.Add(r.Quantity);bytes.AddRange(BitConverter.GetBytes(r.OrderIndex));}return bytes.ToArray();}
 static MallPurchaseRequest Row(ushort id,byte category,byte qty,ushort order){return new MallPurchaseRequest{ItemID=id,CategoryID=category,Quantity=qty,OrderIndex=order};}
 static int Count(Player p,ushort id){return Enumerable.Range(1,50).Sum(i=>p.Inv[(byte)i].ItemID==id?p.Inv[(byte)i].Ammt:0);}
 static int Balance(byte sub){return (int)BitConverter.ToUInt32(socket.Packets.Last(b=>b.Length>5&&b[4]==75&&b[5]==sub),6);}
 static void Catalog(bool bonus,params MallItemEntry[] entries){var t=typeof(ItemMallManager);var flags=BindingFlags.Static|BindingFlags.NonPublic;var list=(List<MallItemEntry>)t.GetField(bonus?"_bonusCatalog":"_pointsCatalog",flags).GetValue(null);list.Clear();list.AddRange(entries);var map=(Dictionary<int,MallItemEntry>)t.GetField(bonus?"_bonusMap":"_pointsMap",flags).GetValue(null);map.Clear();foreach(var e in entries)map[e.ItemID]=e;}
 static bool Packet(params byte[] payload){return socket.Packets.Any(b=>b.Skip(4).SequenceEqual(payload));}
 static bool Box(string value){return socket.Packets.Any(b=>b.Length>=10&&b[4]==2&&b[5]==16&&BitConverter.ToUInt32(b,6)==0&&System.Text.Encoding.ASCII.GetString(b,10,b.Length-10)==value);}
 static void Qty(Player p,byte slot,int count){Check(p.Inv[slot].Ammt==count,"Quantity at "+slot+" expected "+count+" got "+p.Inv[slot].Ammt);}

 static void Forge(Player p,byte slot){
  var ids=Enumerable.Range(1,50).Select(i=>p.Inv[(byte)i].ItemID).ToArray();
  var qty=Enumerable.Range(1,50).Select(i=>(int)p.Inv[(byte)i].Ammt).ToArray();
  var durability=Enumerable.Range(1,50).Select(i=>p.Inv[(byte)i].Damage).ToArray();
  var im=p.UserAcc.IM;var bonus=p.UserAcc.IMBonus;var gold=p.Gold;
  Command(p,75,3,slot);
  foreach(var b in socket.Packets){
   if(b.Length<6)continue;
   if(b[4]==23&&b[5]==9){int i=b[6]-1;qty[i]-=b[7];Check(qty[i]>=0,"No duplicate subtraction");if(qty[i]==0){ids[i]=0;durability[i]=0;}}
   if(b[4]==23&&b[5]==5){Check(b.Length==37,"Only one replacement sent, never full bag");int i=b[6]-1;Check(ids[i]==0,"Old ID removed before additive replacement");ids[i]=BitConverter.ToUInt16(b,7);qty[i]+=b[9];durability[i]=b[10];}
  }
  Check(ids.SequenceEqual(Enumerable.Range(1,50).Select(i=>p.Inv[(byte)i].ItemID)),"Client/server item IDs agree");
  Check(qty.SequenceEqual(Enumerable.Range(1,50).Select(i=>(int)p.Inv[(byte)i].Ammt)),"Client/server quantities agree");
  Check(durability.SequenceEqual(Enumerable.Range(1,50).Select(i=>p.Inv[(byte)i].Damage)),"Durability retained and synchronized");
  Check(im==p.UserAcc.IM&&bonus==p.UserAcc.IMBonus&&gold==p.Gold,"Never charges any currency");
  Check(socket.Packets.Any(b=>b.Length==7&&b[4]==75&&b[5]==6),"Native Forging result releases UI pending state");
 }
 static Player LoadPlayer(DataBase.CharacterDataBase db){
  var p=NewPlayer();p.Slot=2;p.UserAcc.DataBaseID=1;p.Tent.IsDirty=false;
  Check(db.GetCharacterData(4510001,ref p),"Load fixture character");
  var game=new DataBase.GameDataBase();game.ItemDat=dat;
  var file=typeof(RCLibrary.Core.DataBase).GetField("DBFile",BindingFlags.Instance|BindingFlags.NonPublic);
  file.SetValue(game,file.GetValue(db));game.LoadFinalData(p);return p;
 }
 static DataBase.CharacterDataBase Database(string path){
  var db=new DataBase.CharacterDataBase();db.ItemDat=dat;
  typeof(RCLibrary.Core.DataBase).GetField("DBFile",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(db,path);return db;
 }
 static bool Forced(Player p,byte slot,bool success){
  socket.Packets.Clear();return (bool)typeof(MallForgingManager).GetMethod("ForgeCore",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{p,slot,new Func<bool>(()=>success)});
 }
 static void Metadata(Player p,byte slot,byte forge){
  var bytes=p.Inv.GetAC23_5();var packet=new SendPacket(bytes).Buffer.ToArray();
  bool found=false;for(int i=6;i<packet.Length;i+=31)if(packet[i]==slot){Check(packet[i+23]==forge,"Native bag record forge offset23");found=true;}
  Check(found,"Forged item in initial inventory packet");
 }
 public class NativeFamily { public ushort[] items; }
 public static string Run(string dataFile,bool reproduce){
  dat=new PhxItemDat();Check(dat.Load(dataFile).GetAwaiter().GetResult(),"Data load");
  handlerType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC75")).First(t=>t!=null);
  handlerType.Assembly.GetType("System.cGlobal").GetField("ItemDatManager").SetValue(null,dat);
  var handlers=(Dictionary<int,Network.ActionCodes.AC>)typeof(Network.ActionCodes.AC).GetField("AcList",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  foreach(var id in new[]{23,30,75})handlers[id]=(Network.ActionCodes.AC)Activator.CreateInstance(handlerType.Assembly.GetType("Network.ActionCodes.AC"+id));
  string root=System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(dataFile));
  string fixture=System.IO.Path.Combine(root,".codex-verify/item-mall-fixture");
  typeof(RCLibrary.Core.PathHelper).GetField("_cachedDataDir",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,fixture);
  System.IO.Directory.SetCurrentDirectory(fixture);
  string config=System.IO.Path.Combine(root,"Data/mall_forging.json");MallForgingManager.Load(dat,config);
  var cfg=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<MallForgingManager.Configuration>(System.IO.File.ReadAllText(config));
  var native=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<NativeFamily[]>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,".codex-verify/client-forging-extracted.json")));
  Check(cfg.families.Length==29,"Native29 families");
  for(int i=0;i<native.Length;i++)Check(cfg.families[i].SequenceEqual(native[i].items),"Native chain IDs/order "+i);
  int transitions=0,missing=0;
  foreach(var family in cfg.families){
   for(int level=0;level<family.Length;level++){
    if(dat.GetItemByID(family[level])==null){missing++;continue;}
    var p=NewPlayer();Add(p,family[level],13,1);p.Inv[13].Damage=19;Add(p,30101,4,30);Add(p,45001,25,17);
    Forge(p,13);Check(Count(p,45001)==17,"Unrelated item unchanged");
    if(level+1==family.Length){Check(p.Inv[13].ItemID==family[level]&&Count(p,30101)==30&&Packet(75,6,8),"Maximum upgrade retains equipment and scrolls");continue;}
    if(dat.GetItemByID(family[level+1])==null){Check(p.Inv[13].ItemID==family[level]&&Count(p,30101)==30,"Missing reward data retains items");continue;}
    Check(p.Inv[13].ItemID==family[level+1]&&p.Inv[13].Ammt==1,"Exact native next item at same slot");
    Check(Count(p,30101)==30-level-1,"Scroll cost equals target level");Check(Packet(75,6,6),"Native success and sound result");transitions++;
   }
  }
  var player=NewPlayer();Add(player,11103,13,1);Add(player,30101,4,1);Forge(player,13);
  Check(player.Inv[13].ItemID==11103&&Count(player,30101)==1&&Packet(75,6,5),"Insufficient scrolls never partially consumed");
  Add(player,30101,5,1);player.Inv[5].isLocked=true;Forge(player,13);Check(Count(player,30101)==2&&player.Inv[13].ItemID==11103,"Locked scrolls excluded");
  player.Inv[5].isLocked=false;Forge(player,13);Check(Count(player,30101)==0&&player.Inv[13].ItemID==11105,"Costs spread over two stacks exactly once");
  foreach(byte slot in new byte[]{0,51,255,12}){Forge(player,slot);Check(player.Inv[13].ItemID==11105,"Invalid/empty slot retains equipment");}
  Add(player,30101,2,10);player.Inv[13].isLocked=true;Forge(player,13);Check(Count(player,30101)==10,"Locked equipment retained");player.Inv[13].isLocked=false;
  player.Inv[13].Parent=2;Forge(player,13);Check(Count(player,30101)==10,"Non-root item excluded");player.Inv[13].Parent=0;
  foreach(var bytes in new[]{new byte[0],new byte[]{13,0},new byte[]{13,0,0}}){Command(player,75,3,bytes);Check(Count(player,30101)==10&&player.Inv[13].ItemID==11105&&socket.Packets.Count==0,"Malformed packet ignored before mutation");}
  Add(player,45001,20,1);Forge(player,20);Check(Count(player,45001)==1&&Count(player,30101)==10,"Non-equipment rejected");
  player.CharID=7788;var battles=(Dictionary<uint,Game.Battle.ActiveBattle>)typeof(Game.Battle.PvEBattleManager).GetField("_activeBattles",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  battles[player.CharID]=new Game.Battle.ActiveBattle();try{Forge(player,13);Check(Count(player,30101)==10,"Battle rejects forging");}finally{battles.Remove(player.CharID);player.CharID=0;}
  player=NewPlayer();for(byte i=1;i<=50;i++)Add(player,i==13?(ushort)11103:i==1?(ushort)30101:(ushort)45001,i,i==13?(byte)1:(byte)50);
  Forge(player,13);Check(player.Inv.FilledCount==50&&player.Inv[13].ItemID==11105&&Count(player,30101)==48,"Full bag supports same-slot replacement");
  player=NewPlayer();Add(player,11101,13,1);Add(player,30101,2,1);
  var concurrent=player;System.Threading.Tasks.Parallel.Invoke(()=>MallForgingManager.Forge(concurrent,13),()=>MallForgingManager.Forge(concurrent,13));
  Check(player.Inv[13].ItemID==11103&&Count(player,30101)==0&&socket.Packets.Count(b=>b.Skip(4).SequenceEqual(new byte[]{75,6,6}))==1,"Concurrent last scroll grants only one upgrade");
  player=NewPlayer();Add(player,11103,13,1);Add(player,30101,2,3);Forge(player,13);Forge(player,13);Check(player.Inv[13].ItemID==11105&&Count(player,30101)==1,"Repeated request revalidates next cost");
  // A bad next-item definition must fail before any scroll is removed.
  string bad=System.IO.Path.Combine(fixture,"invalid-forging.json");
  foreach(string text in new[]{"broken","{\"families\":[[11103,0]]}","{\"families\":[[11103,11105],[11103,11107]]}","{\"families\":[[11103,65000]],\"point_items\":[]}"}){
   System.IO.File.WriteAllText(bad,text);MallForgingManager.Load(dat,bad);player=NewPlayer();Add(player,11103,13,1);Add(player,30101,2,10);Forge(player,13);Check(player.Inv[13].ItemID==11103&&Count(player,30101)==10,"Invalid or missing upgrade definition cannot charge");
  }
  MallForgingManager.Load(dat,config);
  int persistedPoints=-1;ItemMallManager.OnPointsChanged=(id,points)=>persistedPoints=points;
  // Regular equipment: deterministic success/failure, independent of Strong Scrolls.
  player=NewPlayer();Add(player,21013,13,1);Add(player,30101,2,20);player.UserAcc.IM=3;
  Check(Forced(player,13,true)&&player.Inv[13].Forge==1&&player.UserAcc.IM==0&&Count(player,30101)==20,"Point success consumes exactly3 points and no scrolls");
  Check(persistedPoints==0,"Point deduction invokes account persistence callback");
  Check(Packet(75,6,1,13,1,0)&&!socket.Packets.Any(b=>b[4]==23),"Success metadata packet without additive item grants");Metadata(player,13,1);
  Check(!Forced(player,13,true)&&player.Inv[13].Forge==1&&player.UserAcc.IM==0&&Packet(75,6,4),"Insufficient points do not modify progress");
  player.UserAcc.IM=3;Check(!Forced(player,13,false)&&player.Inv[13].Forge==1&&player.UserAcc.IM==0&&Packet(75,6,2),"Failed forge costs3 points but preserves progress");
  player.UserAcc.IM=6;Check(Forced(player,13,true)&&player.Inv[13].Forge==2&&player.UserAcc.IM==3,"Second success adds one progress step");
  var baseEquip=new Equip();baseEquip.CopyFrom(dat.GetItemByID(21013));Check(player.Inv.TryEquip(player.Eqs,13),"Equip point-forged item");
  Check(player.Eqs[2].DEF==baseEquip.DEF+4,"Single stat gets+2 per success");
  var eqpacket=new SendPacket(player.Eqs._23_11Data).Buffer.ToArray();Check(eqpacket[6+15]==2,"Native equipped-login forge offset15");
  Check(player.Inv.TryUnequip(player.Eqs,2,13)&&player.Inv[13].Forge==2,"Unequip preserves point progress");
  player.Inv.MoveItem(13,14,1);Check(player.Inv[14].Forge==2,"Moving item preserves point progress");
  player.Inv[14].Forge=200;Check(!Forced(player,14,true)&&player.UserAcc.IM==3&&player.Inv[14].Forge==200,"Maximum200 rejects without charge");
  player.Inv[14].Forge=199;Check(Forced(player,14,true)&&player.Inv[14].Forge==200&&player.UserAcc.IM==0,"Last successful step capped at200");
  player=NewPlayer();Add(player,21013,13,1);player.UserAcc.IM=3;var pointsPlayer=player;
  System.Threading.Tasks.Parallel.Invoke(()=>MallForgingManager.Forge(pointsPlayer,13),()=>MallForgingManager.Forge(pointsPlayer,13));
  Check(player.UserAcc.IM==0&&player.Inv[13].Forge<=1,"Concurrent attempts cannot spend last3 points twice");
  player=NewPlayer();Add(player,21013,13,1);player.Inv[13].Forge=7;player.UserAcc.IM=3;Command(player,75,3,13);
  Check(player.UserAcc.IM==0&&(player.Inv[13].Forge==7||player.Inv[13].Forge==8),"Native request takes real50percent route and retains progress on failure");
  // Positive/negative split does not mutate shared item definitions or invent stats.
  foreach(var pair in new[]{new[]{110,110},new[]{110,98},new[]{98,110},new[]{110,0}}){
   var info=new PhxItemInfo{ItemID=999,ItemName=System.Text.Encoding.ASCII.GetBytes("Stat fixture"),StatusType=new ushort[]{210,(ushort)(pair[1]==0?0:214)},StatusUp=pair};
   var e=new Equip();e.CopyFrom(info);e.Forge=3;
   Check(e.ATK==(pair[0]<100?pair[0]-100:pair[0]-100+(pair[1]<100?6:3)),"ATK split and negative preservation");
   Check(e.SPD==(pair[1]==0?0:pair[1]<100?pair[1]-100:pair[1]-100+(pair[0]<100?6:3)),"SPD split and negative preservation");
   Check(info.StatusUp.SequenceEqual(pair),"Shared base stats unchanged");
  }
  // Save and reconnect against an isolated copy; never use the live character DB.
  string dbPath=System.IO.Path.Combine(root,".codex-verify/forging-regression.db");
  var db=Database(dbPath);player=LoadPlayer(db);player.Inv.RemoveAll(true);Add(player,11103,13,1);player.Inv[13].Damage=23;Add(player,30101,4,5);
  Forge(player,13);var restored=LoadPlayer(Database(dbPath));Check(restored.Inv[13].ItemID==11105&&restored.Inv[13].Damage==23&&Count(restored,30101)==3,"Automatic save persists upgraded ID/durability/scroll cost on uncached reconnect");
  byte wear= (byte)restored.Inv[13].Wear_At;Check(restored.Inv.TryEquip(restored.Eqs,13),"Upgraded equipment can be equipped");
  Check(restored.Eqs[wear].ItemID==11105&&restored.Eqs[wear].ATK==12&&restored.Eqs[wear].SPD==12,"Equip uses native upgraded item stats");
  Check(restored.SaveCharacterData(),"Save equipped upgrade");restored=LoadPlayer(Database(dbPath));Check(restored.Eqs[wear].ItemID==11105&&restored.Eqs[wear].Damage==23,"Equipped upgrade survives reconnect");
  Check(restored.Inv.TryUnequip(restored.Eqs,wear,13),"Unequip upgrade");Command(restored,30,4,18,13);Check(restored.Storage[18].ItemID==11105&&restored.Storage[18].Damage==23,"Upgraded item can move to vault");
  Check(restored.SaveCharacterData(),"Save vault upgrade");restored=LoadPlayer(Database(dbPath));Check(restored.Storage[18].ItemID==11105&&restored.Storage[18].Damage==23,"Stored upgrade survives reconnect");
  restored.Inv.RemoveAll(true);Add(restored,21013,13,1);restored.UserAcc.IM=6;Check(Forced(restored,13,true),"Persistent point success");Check(!Forced(restored,13,false),"Persistent point failure");
  restored=LoadPlayer(Database(dbPath));Check(restored.Inv[13].Forge==1,"Point progress survives save/reload after failure");Metadata(restored,13,1);
  Check(restored.Inv.TryEquip(restored.Eqs,13),"Equip persistent point item");Check(restored.SaveCharacterData(),"Save point equip");restored=LoadPlayer(Database(dbPath));Check(restored.Eqs[2].Forge==1&&restored.Eqs[2].DEF==baseEquip.DEF+2,"Equipped forge and stat survive reconnect");
  Check(restored.Inv.TryUnequip(restored.Eqs,2,13),"Unequip persistent point item");Command(restored,30,4,17,13);Check(restored.Storage[17].Forge==1,"Vault deposit retains forge");Check(restored.SaveCharacterData(),"Save point vault item");
  restored=LoadPlayer(Database(dbPath));Check(restored.Storage[17].Forge==1,"Vault forge survives reconnect");Command(restored,30,5,15,17);Check(restored.Inv[15].Forge==1,"Vault withdrawal retains forge");
  Check(socket.Packets.Any(b=>b.Length==37&&b[4]==23&&b[5]==5&&b[6]==15&&b[29]==1),"Vault withdrawal packet preserves forge");
  ItemMallManager.OnPointsChanged=null;
  DataBase.CharacterDataBase.GlobalInstance=null;
  transport.Dispose();return checks+" forging checks passed; "+transitions+" available native upgrade steps; "+missing+" unavailable native items safely excluded";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll','System.Web.Extensions.dll','System.Data.dll'))
[ForgeChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'),$ExpectDoubleMove.IsPresent)
