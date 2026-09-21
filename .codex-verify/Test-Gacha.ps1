param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'gacha-bin'),[switch]$ExpectDoubleMove)
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','Wonderland Private Server.exe') | ForEach-Object {Join-Path $BuildDirectory $_}
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
public class MallSocket : SocketClient {
 public List<byte[]> Packets;
 public override void SendPacket(IPacket p){Packets.Add(p.Buffer.ToArray());}
}
public static class MallChecks {
 static Socket transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
 static int checks; static PhxItemDat dat; static Type handlerType; static MallSocket socket;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static Player NewPlayer(){
  socket=(MallSocket)FormatterServices.GetUninitializedObject(typeof(MallSocket));
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

 static HashSet<ushort> Pool(ushort id){return new HashSet<ushort>(Enumerable.Range(0,10000).Select(x=>(ushort)typeof(GachaManager).GetMethod("SelectReward",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{id,x})));}
 static void Open(Player p,byte slot,bool quick){
  var ids=Enumerable.Range(1,50).Select(i=>p.Inv[(byte)i].ItemID).ToArray();var qty=Enumerable.Range(1,50).Select(i=>(int)p.Inv[(byte)i].Ammt).ToArray();
  if(quick)Command(p,23,15,slot,1,0,0);else Command(p,23,p.Inv[slot].ItemID==34333?(byte)128:(byte)75,slot,0);
  foreach(var b in socket.Packets){
   if(b.Length<6)continue;
   Check(b[4]!=55,"No fabricated native AC55 reward response");
   if(b[4]==23&&b[5]==9){qty[b[6]-1]-=b[7];if(qty[b[6]-1]==0)ids[b[6]-1]=0;}
   if(b[4]==23&&b[5]==5){Check((b.Length-6)%31==0,"Reward packet record shape");for(int k=6;k<b.Length;k+=31){int index=b[k]-1;ushort id=BitConverter.ToUInt16(b,k+1);Check(ids[index]==0||ids[index]==id,"Reward never overlays another item");ids[index]=id;qty[index]+=b[k+3];}}
  }
  Check(ids.SequenceEqual(Enumerable.Range(1,50).Select(i=>p.Inv[(byte)i].ItemID))&&qty.SequenceEqual(Enumerable.Range(1,50).Select(i=>(int)p.Inv[(byte)i].Ammt)),"Client additive inventory exactly matches server after opening");
 }
 public static string Run(string dataFile,bool reproduce){
  dat=new PhxItemDat();Check(dat.Load(dataFile).GetAwaiter().GetResult(),"Data load");
  handlerType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC23")).First(t=>t!=null);
  handlerType.Assembly.GetType("System.cGlobal").GetField("ItemDatManager").SetValue(null,dat);
  var handlers=(Dictionary<int,Network.ActionCodes.AC>)typeof(Network.ActionCodes.AC).GetField("AcList",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  foreach(var id in new[]{23,55,75,91})handlers[id]=(Network.ActionCodes.AC)Activator.CreateInstance(handlerType.Assembly.GetType("Network.ActionCodes.AC"+id));
  var fixture=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(dataFile)),".codex-verify/item-mall-fixture");
  typeof(RCLibrary.Core.PathHelper).GetField("_cachedDataDir",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,fixture);System.IO.Directory.SetCurrentDirectory(fixture);
  string config=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dataFile),"gacha_packs.json");GachaManager.Load(dat,config);
  var catalog=ItemMallManager.ParseJsonCatalog(System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dataFile),"item_mall.json")));Catalog(false,catalog.Where(e=>e.IsBonus==0).ToArray());
  Check(catalog.Count==200&&!catalog.Any(e=>e.IsBonus==0&&new ushort[]{32075,32002,34014,34096,34097,34098,34126}.Contains(e.ItemID)),"Seven mistaken consumable offers removed");
  var selector=typeof(GachaManager).GetMethod("SelectReward",BindingFlags.Static|BindingFlags.NonPublic);
  foreach(ushort id in new ushort[]{34229,34333,34199}){
   Check(GachaManager.IsAvailable(id),"Valid reward configuration enabled");var pool=Pool(id);
   var counts=Enumerable.Range(0,10000).Select(x=>(ushort)selector.Invoke(null,new object[]{id,x})).GroupBy(x=>x).Select(g=>g.Count()).OrderBy(x=>x).ToArray();
   Check(counts.SequenceEqual(new[]{200,300,1200,1300,3500,3500}),"Exact exhaustive weights over all10000 possible rolls");
   foreach(ushort reward in pool)Check(dat.GetItemByID(reward)!=null&&dat.GetItemByID(reward).Equippos>=1&&dat.GetItemByID(reward).Equippos<=6,"All rewards are real equippable gear");
   var viewer=NewPlayer();Command(viewer,91,1,(byte)id,(byte)(id>>8),0);
   var preview=socket.Packets.Single();Check(preview[4]==91&&preview[5]==2&&BitConverter.ToUInt16(preview,6)==id&&preview[8]==1&&preview.Length==27,"Native pack preview header and six rows");
   Check(new HashSet<ushort>(Enumerable.Range(0,6).Select(n=>BitConverter.ToUInt16(preview,9+n*3))).SetEquals(pool),"Preview matches actual reward pool");
   Check(Enumerable.Range(0,6).All(n=>preview[11+n*3]==1)&&viewer.UserAcc.IM==1000&&viewer.UserAcc.IMBonus==500&&viewer.Inv.FilledCount==0,"Preview only shows quantity one, never spends points or grants items");
   var offer=catalog.Single(e=>e.IsBonus==0&&e.ItemID==id);var p=NewPlayer();p.UserAcc.IM=offer.PointCost;
   Command(p,75,1,Cart(Row(id,offer.CategoryID,1,offer.OrderIndex)));Check(p.UserAcc.IM==0&&Count(p,id)==1,"Gacha exact-balance purchase");
   Open(p,1,false);Check(Count(p,id)==0&&pool.Contains(p.Inv[1].ItemID)&&p.Inv[1].Ammt==1,"Purchased gacha exchanged for one configured gear");Check(Packet(23,15)&&socket.Packets.Any(b=>b.Length>10&&b[4]==2&&b[5]==16),"Sound and notification box on success");
   ushort awarded=p.Inv[1].ItemID;Command(p,23,11,1);Check(p.Eqs[(byte)dat.GetItemByID(awarded).Equippos].ItemID==awarded,"Awarded item can actually equip");
   p=NewPlayer();Add(p,id,1,2);for(byte slot=2;slot<=50;slot++)Add(p,24013,slot,1);Open(p,1,true);Check(Count(p,id)==2&&!Packet(23,15),"Full bag retains stacked pack and gives no reward");
   p.Inv.RemoveItem(1,1,false);Open(p,1,true);Check(Count(p,id)==0&&pool.Contains(p.Inv[1].ItemID),"Single last pack frees own slot in full bag");
   p=NewPlayer();Add(p,id,1,2);p.Inv[1].isLocked=true;Open(p,1,false);Check(Count(p,id)==2&&!Packet(23,15),"Locked pack retained");p.Inv[1].isLocked=false;
   foreach(var args in new[]{new byte[]{1,0,0,0},new byte[]{1,2,0,0},new byte[]{1,1,1,0},new byte[]{0,1,0,0},new byte[]{51,1,0,0}}){Command(p,23,15,args);Check(Count(p,id)==2&&!Packet(23,15),"Invalid quantity/target/slot cannot consume pack");}
   Open(p,1,true);Check(Count(p,id)==1&&p.Inv.FilledCount==2,"One use consumes exactly one of stacked packs");
   // Repeat openings: every outcome stays in the declared pool, never fixed item23001.
   var results=new HashSet<ushort>();for(int n=0;n<24;n++){p=NewPlayer();Add(p,id,1,1);Open(p,1,n%2==0);Check(pool.Contains(p.Inv[1].ItemID),"RNG result in configured pool");results.Add(p.Inv[1].ItemID);}Check(results.Count>1,"Multiple reward outcomes observed");
  }
  var malformed=NewPlayer();Add(malformed,34333,12,2);
  foreach(byte sub in new byte[]{75,128})foreach(var args in new[]{new byte[0],new byte[]{12},new byte[]{12,1},new byte[]{0,0},new byte[]{51,0},new byte[]{12,0,1}}){Command(malformed,23,sub,args);Check(Count(malformed,34333)==2&&!Packet(23,15),"Malformed native slot cannot consume pack");}
  Command(malformed,23,128,12,0);Check(Count(malformed,34333)==1&&malformed.Inv.FilledCount==2,"Captured Lucky Pack slot12 packet consumes exactly one and adds reward");
  Add(malformed,34172,11,1);Command(malformed,23,75,11,0);Check(Count(malformed,34172)==1&&Box("This pack has no configured rewards. Item retained."),"Unconfigured egg remains intact with explanation");
  Command(malformed,91,1,0,0,0);Check(Packet(91,2,0,0,1),"Unknown preview cannot advertise fake rewards");
  Command(malformed,91,3,95,137);Check(malformed.UserAcc.IMBonus==500&&Count(malformed,35167)==0,"Preview protocol cannot claim arbitrary bonus rewards");
  foreach(var args in new[]{new byte[0],new byte[]{29,134},new byte[]{29,134,0,1}}){Command(malformed,91,1,args);Check(socket.Packets.Count==0,"Malformed preview request ignored");}
  var buyer=NewPlayer();Add(buyer,24013,1,1);Command(buyer,55,1,1);Check(buyer.Inv[1].ItemID==24013&&buyer.Inv.FilledCount==1,"AC55 no longer destroys arbitrary items for fake reward");
  Check(!buyer.Inv.TryExchangeItem(1,999,new Dictionary<ushort,int>{{11101,1}})&&buyer.Inv[1].ItemID==24013,"Exchange validates expected source identity");
  Check(!buyer.Inv.TryExchangeItem(1,24013,new Dictionary<ushort,int>{{57205,1}})&&buyer.Inv[1].ItemID==24013,"Missing reward retains source");
  buyer=NewPlayer();Add(buyer,34333,1,1);var concurrentBuyer=buyer;
  System.Threading.Tasks.Parallel.Invoke(()=>GachaManager.TryOpen(concurrentBuyer,1),()=>GachaManager.TryOpen(concurrentBuyer,1));
  Check(Count(buyer,34333)==0&&buyer.Inv.FilledCount==1&&socket.Packets.Count(b=>b.Length==8&&b[4]==23&&b[5]==9)==1,"Concurrent requests exchange the last pack only once");
  buyer=NewPlayer();buyer.CharID=7788;Add(buyer,34333,1,1);
  var active=(Dictionary<uint,Game.Battle.ActiveBattle>)typeof(Game.Battle.PvEBattleManager).GetField("_activeBattles",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  active[buyer.CharID]=new Game.Battle.ActiveBattle();
  try{Open(buyer,1,false);Check(Count(buyer,34333)==1&&!Packet(23,15),"Combat cannot consume gacha pack");}finally{active.Remove(buyer.CharID);}
  var configText=System.IO.File.ReadAllText(config);string badFile=System.IO.Path.Combine(fixture,"invalid-gacha.json");
  foreach(string bad in new[]{"broken",configText.Replace("3500","0"),configText.Replace("3500","3501"),configText.Replace("22135","57205"),configText.Replace("22135","32075")}){
   System.IO.File.WriteAllText(badFile,bad);GachaManager.Load(dat,badFile);Check(!GachaManager.IsAvailable(34229),"Bad reward configuration disables pack");
   buyer=NewPlayer();Add(buyer,34229,1,1);Open(buyer,1,false);Check(Count(buyer,34229)==1&&!Packet(23,15),"Bad config cannot consume existing pack");
   socket.Packets.Clear();ItemMallManager.SendCatalog(buyer);var wire=socket.Packets.Single();Check(!Enumerable.Range(0,BitConverter.ToUInt16(wire,6)).Any(i=>GachaManager.IsGacha(BitConverter.ToUInt16(wire,8+10*i))),"Unavailable gacha hidden from shop");
   var e=catalog.Single(x=>x.IsBonus==0&&x.ItemID==34229);Command(buyer,75,1,Cart(Row(e.ItemID,e.CategoryID,1,e.OrderIndex)));Check(buyer.UserAcc.IM==1000&&Count(buyer,34229)==1,"Disabled gacha stale cart never charges");
  }
  GachaManager.Load(dat,config);Check(GachaManager.IsAvailable(34229),"Valid config restored");
  transport.Dispose();return checks+" gacha purchase/open/inventory checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[MallChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'),$ExpectDoubleMove.IsPresent)
