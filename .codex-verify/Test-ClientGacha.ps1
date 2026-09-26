param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'client-gacha-bin'),[switch]$ExpectDoubleMove)
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

 public class NativePack { public ushort item_id; public byte open_subcode; public NativeReward[] rewards; }
 public class NativeReward { public ushort item_id; public int quantity; }
 static Dictionary<ushort,byte> routes;
 static void Open(Player p,byte slot,bool quick){
  var ids=Enumerable.Range(1,50).Select(i=>p.Inv[(byte)i].ItemID).ToArray();var qty=Enumerable.Range(1,50).Select(i=>(int)p.Inv[(byte)i].Ammt).ToArray();
  if(quick)Command(p,23,15,slot,1,0,0);else Command(p,23,routes.ContainsKey(p.Inv[slot].ItemID)?routes[p.Inv[slot].ItemID]:(byte)128,slot,0);
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
  var json=new System.Web.Script.Serialization.JavaScriptSerializer();
  var configured=json.Deserialize<GachaManager.Pack[]>(System.IO.File.ReadAllText(config));
  var native=json.Deserialize<NativePack[]>(System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(config),"../.codex-verify/client-gacha-extracted.json")));
  routes=native.ToDictionary(x=>x.item_id,x=>x.open_subcode);
  Check(configured.Length==24&&!configured.Any(x=>x.item_id==34333),"Exactly24 original pools; invented Lucky pool withdrawn");
  Check(!catalog.Any(x=>x.ItemID==34333),"Lucky withdrawn from seed shop");
  Check(dat.GetItemByID(27025)!=null&&dat.GetItemByID(27025).ItemType==20,"Ice Snowflake preserved as pet food");
  var selector=typeof(GachaManager).GetMethod("SelectReward",BindingFlags.Static|BindingFlags.NonPublic);
  foreach(var pack in configured){
   var expected=native.Single(x=>x.item_id==pack.item_id);
   Check(expected.rewards.Length==pack.rewards.Length,"Native row count");
   for(int i=0;i<pack.rewards.Length;i++){
    var r=pack.rewards[i];Check(r.item_id==expected.rewards[i].item_id&&r.quantity==expected.rewards[i].quantity&&dat.GetItemByID(r.item_id)!=null,"Native reward ID/order/quantity and actual definition");
    if(i>0)Check(pack.rewards[i-1].weight>r.weight,"Later reward has strictly lower chance");
   }
   Check(pack.rewards.Sum(x=>x.weight)==10000,"Weights sum to100 percent");
   var counts=new Dictionary<GachaManager.Reward,int>();
   for(int roll=0;roll<10000;roll++){var reward=(GachaManager.Reward)selector.Invoke(null,new object[]{pack.item_id,roll});if(!counts.ContainsKey(reward))counts[reward]=0;counts[reward]++;}
   Check(counts.Count==pack.rewards.Length,"Every row reachable including repeated IDs with different quantities");
   foreach(var row in counts)Check(row.Value==row.Key.weight,"Exhaustive roll weights, not sampled frequency");
   var offer=catalog.FirstOrDefault(x=>x.IsBonus==0&&x.ItemID==pack.item_id);
   if(offer!=null){var customer=NewPlayer();customer.UserAcc.IM=offer.PointCost;Command(customer,75,1,Cart(Row(pack.item_id,offer.CategoryID,1,offer.OrderIndex)));Check(customer.UserAcc.IM==0&&Count(customer,pack.item_id)==offer.Count,"Original mall offer buys at exact price");Open(customer,1,false);Check(Count(customer,pack.item_id)==offer.Count-1,"Purchased original pack opens via native route");}
   var p=NewPlayer();Command(p,91,1,(byte)pack.item_id,(byte)(pack.item_id>>8),0);var preview=socket.Packets.Single();
   Check(preview.Length==9+pack.rewards.Length*3&&BitConverter.ToUInt16(preview,6)==pack.item_id,"Preview pack identity and size");
   for(int i=0;i<pack.rewards.Length;i++)Check(BitConverter.ToUInt16(preview,9+3*i)==pack.rewards[i].item_id&&preview[11+3*i]==pack.rewards[i].quantity,"Preview exactly matches native quantities and order");
   Check(p.UserAcc.IM==1000&&p.Inv.FilledCount==0,"Preview read-only");
   Add(p,pack.item_id,12,2);Open(p,12,false);Check(Count(p,pack.item_id)==1,"Native double-click consumes one stacked pack");
   var given=Enumerable.Range(1,50).Select(i=>p.Inv[(byte)i]).Where(x=>x.ItemID!=0&&x.ItemID!=pack.item_id).ToArray();
   Check(given.Length>0&&given.Select(x=>x.ItemID).Distinct().Count()==1,"One reward kind added");
   Check(pack.rewards.Any(r=>r.item_id==given[0].ItemID&&r.quantity==given.Sum(x=>(int)x.Ammt)),"Granted quantity matches selected original row");
   Check(Packet(23,15),"Native opening sound");
   p=NewPlayer();Add(p,pack.item_id,12,2);p.Inv[12].isLocked=true;Open(p,12,false);Check(Count(p,pack.item_id)==2&&!Packet(23,15),"Locked pack retained");
   p=NewPlayer();for(byte slot=1;slot<=50;slot++)if(slot!=12)Add(p,24013,slot,1);Add(p,pack.item_id,12,2);Open(p,12,false);Check(Count(p,pack.item_id)==2&&!Packet(23,15),"Full bag leaves pack untouched");
   p.Inv.RemoveItem(12,1,false);Open(p,12,false);Check(Count(p,pack.item_id)==0,"Last pack can reuse own slot");
  }
  // A deterministic multi-quantity exchange using one original Bless Pack row.
  var multi=configured.Single(p=>p.item_id==34296).rewards.First(r=>r.quantity==50);
  var buyer=NewPlayer();Add(buyer,34296,12,2);Check(buyer.Inv.TryExchangeItem(12,34296,new Dictionary<ushort,int>{{multi.item_id,multi.quantity}}),"Original50-item outcome fits");Check(Count(buyer,multi.item_id)==50&&Count(buyer,34296)==1,"All50 delivered for exactly one pack");
  buyer=NewPlayer();Add(buyer,34333,12,1);Open(buyer,12,false);Check(Count(buyer,34333)==1&&!Packet(23,15)&&!GachaManager.IsAvailable(34333),"Retired Lucky retained but never grants invented loot");
  Catalog(false,new MallItemEntry(34333,"Lucky Pack","Gacha",32,1){OrderIndex=1,CategoryID=3});Command(buyer,75,1,Cart(Row(34333,3,1,1)));Check(buyer.UserAcc.IM==1000&&Count(buyer,34333)==1,"Stale retired offer cannot charge");
  buyer=NewPlayer();Add(buyer,34171,12,2);
  foreach(byte sub in new byte[]{75,128})foreach(var args in new[]{new byte[0],new byte[]{12},new byte[]{12,1},new byte[]{0,0},new byte[]{51,0},new byte[]{12,0,1}}){Command(buyer,23,sub,args);Check(Count(buyer,34171)==2,"Malformed slot cannot consume");}
  buyer.CharID=7788;var active=(Dictionary<uint,Game.Battle.ActiveBattle>)typeof(Game.Battle.PvEBattleManager).GetField("_activeBattles",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);active[buyer.CharID]=new Game.Battle.ActiveBattle();try{Open(buyer,12,false);Check(Count(buyer,34171)==2,"Combat keeps pack");}finally{active.Remove(buyer.CharID);}
  buyer=NewPlayer();Add(buyer,34171,1,1);var concurrentBuyer=buyer;System.Threading.Tasks.Parallel.Invoke(()=>GachaManager.TryOpen(concurrentBuyer,1),()=>GachaManager.TryOpen(concurrentBuyer,1));Check(Count(buyer,34171)==0&&socket.Packets.Count(b=>b.Length==8&&b[4]==23&&b[5]==9)==1,"Concurrent last-pack use consumes only once");
  var configText=System.IO.File.ReadAllText(config);string badFile=System.IO.Path.Combine(fixture,"invalid-client-gacha.json");
  foreach(string bad in new[]{"broken",configText.Replace("\"quantity\": 1,","\"quantity\": 0,"),configText.Replace("34105","57205")}){
   System.IO.File.WriteAllText(badFile,bad);GachaManager.Load(dat,badFile);Check(!GachaManager.IsAvailable(34171),"Invalid pool fails closed");buyer=NewPlayer();Add(buyer,34171,12,1);Open(buyer,12,false);Check(Count(buyer,34171)==1,"Invalid data retains pack");
  }
  GachaManager.Load(dat,config);Check(GachaManager.IsAvailable(34199),"Original Strengthen pool now available with Ice Snowflake");
  transport.Dispose();return checks+" gacha purchase/open/inventory checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll','System.Web.Extensions.dll'))
[MallChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'),$ExpectDoubleMove.IsPresent)

