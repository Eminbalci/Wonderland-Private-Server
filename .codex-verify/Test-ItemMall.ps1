param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'item-mall-bin'),[switch]$ExpectDoubleMove)
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
 static byte[] Cart(params MallPurchaseRequest[] rows){var bytes=new List<byte>{(byte)rows.Length};foreach(var r in rows){bytes.AddRange(BitConverter.GetBytes(r.ItemID));bytes.Add(r.BundleCount);bytes.Add(r.Quantity);bytes.AddRange(BitConverter.GetBytes(r.OrderIndex));}return bytes.ToArray();}
 static MallPurchaseRequest Row(ushort id,byte bundle,byte qty,ushort order){return new MallPurchaseRequest{ItemID=id,BundleCount=bundle,Quantity=qty,OrderIndex=order};}
 static int Count(Player p,ushort id){return Enumerable.Range(1,50).Sum(i=>p.Inv[(byte)i].ItemID==id?p.Inv[(byte)i].Ammt:0);}
 static int Balance(byte sub){return (int)BitConverter.ToUInt32(socket.Packets.Last(b=>b.Length>5&&b[4]==75&&b[5]==sub),6);}
 static void Catalog(bool bonus,params MallItemEntry[] entries){var t=typeof(ItemMallManager);var flags=BindingFlags.Static|BindingFlags.NonPublic;var list=(List<MallItemEntry>)t.GetField(bonus?"_bonusCatalog":"_pointsCatalog",flags).GetValue(null);list.Clear();list.AddRange(entries);var map=(Dictionary<int,MallItemEntry>)t.GetField(bonus?"_bonusMap":"_pointsMap",flags).GetValue(null);map.Clear();foreach(var e in entries)map[e.ItemID]=e;}
 static bool Packet(params byte[] payload){return socket.Packets.Any(b=>b.Skip(4).SequenceEqual(payload));}
 static bool Box(string value){return socket.Packets.Any(b=>b.Length>=10&&b[4]==2&&b[5]==16&&BitConverter.ToUInt32(b,6)==0&&System.Text.Encoding.ASCII.GetString(b,10,b.Length-10)==value);}
 static void Qty(Player p,byte slot,int count){Check(p.Inv[slot].Ammt==count,"Quantity at "+slot+" expected "+count+" got "+p.Inv[slot].Ammt);}
 public static string Run(string dataFile,bool reproduce){
  dat=new PhxItemDat();Check(dat.Load(dataFile).GetAwaiter().GetResult(),"Data load");
  (handlerType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC23")).First(t=>t!=null)).Assembly.GetType("System.cGlobal").GetField("ItemDatManager",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,dat);
  var handlers=(Dictionary<int,Network.ActionCodes.AC>)typeof(Network.ActionCodes.AC).GetField("AcList",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  foreach(var id in new[]{34,75})handlers[id]=(Network.ActionCodes.AC)Activator.CreateInstance(handlerType.Assembly.GetType("Network.ActionCodes.AC"+id));
  typeof(RCLibrary.Core.PathHelper).GetField("_cachedDataDir",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(dataFile)),".codex-verify/item-mall-fixture"));
  System.IO.Directory.SetCurrentDirectory(RCLibrary.Core.PathHelper.DataDirectory);
  var loaded=ItemMallManager.GetCatalog();Check(loaded.Count>0,"Isolated database catalog load");
  Console.WriteLine("Real catalog first item exists in ItemDat: "+(dat.GetItemByID(57205)!=null));
  var apple=new MallItemEntry(45001,"Apple","Food",32,1){OrderIndex=7};
  var pack=new MallItemEntry(45001,"Apple pack","Food",60,20){OrderIndex=8};
  var shoes=new MallItemEntry(24013,"Shoes","Armor",100,1){OrderIndex=9};
  var invalid=new MallItemEntry(28006,"Invalid","Food",20,1){OrderIndex=10};
  Catalog(false,apple,pack,shoes,invalid);
  Catalog(true,new MallItemEntry(45001,"Bonus Apple","Food",15,1){OrderIndex=17});
  int charges=0;ItemMallManager.OnPointsChanged=(id,amount)=>charges++;
  ItemMallManager.OnBonusPointsChanged=(id,amount)=>charges++;
  var p=NewPlayer();
  for(int i=0;i<20;i++){
   Command(p,34,1,(byte)(i%2));Check(p.UserAcc.IM==1000&&charges==0&&p.Inv.FilledCount==0,"Balance query never purchases/charges");
   var reply=socket.Packets.Single(b=>b.Length>5&&b[4]==35&&b[5]==4);
   Check(reply.Length==22&&BitConverter.ToUInt32(reply,6)+BitConverter.ToUInt32(reply,10)==1000,"Native checkout receives actual balance");
   Check(Balance(3)==1000&&Balance(9)==500,"Separate IM and Bonus balance");
   Check(socket.Packets.Where(b=>b.Length>5&&b[4]==75).All(b=>BitConverter.ToUInt32(b,10)==0),"Balance synchronization does not fabricate spending");
   Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==75&&b[5]==1),"Pending cart not cleared by catalog refresh");
  }
  Add(p,45001,1,49);Add(p,43002,4,8);
  Command(p,75,1,Cart(Row(45001,1,2,7)));
  Check(socket.Packets.Any(b=>b.Length>=10&&b[4]==2&&b[5]==16&&System.Text.Encoding.ASCII.GetString(b,10,b.Length-10).StartsWith("Item Mall: purchase successful.")),"Purchase success appears in notification box");
  Check(p.UserAcc.IM==936&&charges==1&&Count(p,45001)==51,"Native cart buys selected item, exact quantity and price");
  Check(Packet(75,4,201,175,1,2,7,0,1),"Cart row success ACK exact identity");
  var delta=socket.Packets.Single(b=>b.Length>5&&b[4]==23&&b[5]==5);
  Check(delta.Length==68&&delta[6]==1&&delta[9]==1&&delta[37]==2&&delta[40]==1,"Only added quantities sent, including stack overflow");
  Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==23&&b[5]==6),"No duplicate automatic item grant");
  Check(p.Inv[4].Ammt==8&&Balance(3)==936&&Balance(9)==500,"Unrelated inventory and bonus balance unchanged");
  Command(p,75,1,Cart(Row(45001,20,15,8)));Check(p.UserAcc.IM==36&&Count(p,45001)==351,"Bundle delivery above 255 not truncated");
  int before=charges;Command(p,75,1,Cart(Row(24013,1,1,9)));Check(charges==before&&p.UserAcc.IM==36&&Count(p,24013)==0,"Insufficient balance never charges or delivers");Check(Packet(75,4,205,93,1,1,9,0,0),"Rejected purchase has failure ACK");
  Check(Box("Item Mall: insufficient points. Required: 100, available: 36."),"Insufficient points appears in notification box");
  p=NewPlayer();p.UserAcc.IM=32;Command(p,75,1,Cart(Row(45001,1,1,7)));Check(p.UserAcc.IM==0&&Count(p,45001)==1,"Exact balance is sufficient");
  p=NewPlayer();Command(p,75,5,Cart(Row(45001,1,2,17)));Check(p.UserAcc.IM==1000&&p.UserAcc.IMBonus==470&&Count(p,45001)==2,"Bonus cart uses bonus currency");
  p=NewPlayer();for(byte slot=1;slot<=50;slot++)Add(p,24013,slot,1);
  before=charges;Command(p,75,1,Cart(Row(45001,1,1,7)));Check(p.UserAcc.IM==1000&&charges==before&&Count(p,45001)==0,"Full bag does not charge");
  p.Inv.RemoveItem(50,1,false);Command(p,75,1,Cart(Row(45001,1,1,7),Row(24013,1,1,9)));Check(p.UserAcc.IM==1000&&charges==before&&p.Inv[50].ItemID==0,"Entire cart capacity checked before delivery");
  p=NewPlayer();Command(p,75,1,Cart(Row(45001,1,1,7),Row(28006,1,1,10)));Check(p.UserAcc.IM==1000&&p.Inv.FilledCount==0&&charges==before,"Unknown item cancels entire delivery and charge");
  foreach(var bad in new[]{Row(45001,20,1,7),Row(45001,1,1,99),Row(45001,1,0,7),Row(65000,1,1,7)}){Command(p,75,1,Cart(bad));Check(p.UserAcc.IM==1000&&p.Inv.FilledCount==0&&charges==before,"Forged/invalid cart rejected");}
  foreach(var payload in new[]{new byte[0],new byte[]{0},new byte[]{1},new byte[]{2,201,175,1,1,7,0},new byte[]{1,201,175,1,1,7,0,0}}){Command(p,75,1,payload);Check(p.UserAcc.IM==1000&&p.Inv.FilledCount==0&&charges==before,"Malformed cart cannot purchase");}
  Command(p,34,1,255);Command(p,34,2,201,175,1);Command(p,75,4,201,175,1);Check(p.UserAcc.IM==1000&&charges==before,"Guessed purchase opcodes disabled");
  p.Inv[1].isLocked=true;Command(p,75,1,Cart(Row(45001,1,1,7)));Check(p.Inv[1].ItemID==0&&p.Inv[2].ItemID==45001,"Locked cell not used");
  p=NewPlayer();p.UserAcc.IM=int.MaxValue;apple.PointCost=int.MaxValue;before=charges;Command(p,75,1,Cart(Row(45001,1,255,7)));Check(p.UserAcc.IM==int.MaxValue&&charges==before&&p.Inv.FilledCount==0,"Invalid/overflow catalog price cannot charge");apple.PointCost=32;
  Command(p,75,2);Check(socket.Packets.Count(b=>b.Length>5&&b[4]==75&&b[5]==1)==1,"Explicit catalog refresh supported");
  var cat=socket.Packets.Single(b=>b.Length>5&&b[4]==75&&b[5]==1);Check(BitConverter.ToUInt16(cat,11)==32&&cat[13]==100,"Client advertised price matches charged price");
  transport.Dispose();return checks+" Item Mall protocol/state checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[MallChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'),$ExpectDoubleMove.IsPresent)

