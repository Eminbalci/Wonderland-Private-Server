param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'mall-stock-bin'),[switch]$ExpectDoubleMove)
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

 public static string Run(string dataFile,bool reproduce){
  dat=new PhxItemDat();Check(dat.Load(dataFile).GetAwaiter().GetResult(),"Data load");
  handlerType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC23")).First(t=>t!=null);
  var cg=handlerType.Assembly.GetType("System.cGlobal");cg.GetField("ItemDatManager",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,dat);
  var handlers=(Dictionary<int,Network.ActionCodes.AC>)typeof(Network.ActionCodes.AC).GetField("AcList",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  foreach(var id in new[]{23,34,75})handlers[id]=(Network.ActionCodes.AC)Activator.CreateInstance(handlerType.Assembly.GetType("Network.ActionCodes.AC"+id));
  typeof(RCLibrary.Core.PathHelper).GetField("_cachedDataDir",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(dataFile)),".codex-verify/item-mall-fixture"));
  System.IO.Directory.SetCurrentDirectory(RCLibrary.Core.PathHelper.DataDirectory);
  var entries=ItemMallManager.ParseJsonCatalog(System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dataFile),"item_mall.json")));
  Check(entries.Count==206,"Audited seed has206 offers");
  foreach(var e in entries)Check(dat.GetItemByID(e.ItemID)!=null,"Catalog has item definition: "+e.ItemID);
  var invalid=new MallItemEntry(57205,"Missing bag","Grocery",32){CategoryID=3,OrderIndex=1};
  var payloadMethod=handlerType.Assembly.GetType("Server.ItemMallServer").GetMethod("BuildCatalogPayload");
  foreach(bool bonus in new[]{false,true}){
   var expected=entries.Where(e=>(e.IsBonus!=0)==bonus).ToArray();Catalog(bonus,expected.Concat(new[]{invalid}).ToArray());
   var p=NewPlayer();ItemMallManager.SendCatalog(p,bonus);var wire=socket.Packets.Single(b=>b[4]==75&&b[5]==(bonus?10:1));
   Check(BitConverter.ToUInt16(wire,6)==expected.Length&&wire.Length==8+10*expected.Length,"Native count and packet length exclude missing IDs");
   for(int i=0;i<expected.Length;i++){
    int o=8+10*i;var e=expected[i];Check(BitConverter.ToUInt16(wire,o)==e.ItemID&&wire[o+2]==e.Count&&BitConverter.ToUInt16(wire,o+3)==e.PointCost&&wire[o+7]==e.CategoryID&&BitConverter.ToUInt16(wire,o+8)==e.OrderIndex,"Valid row identity, quantity, price and order unchanged: "+e.ItemID);
   }
   if(!bonus){var payload=(byte[])payloadMethod.Invoke(null,null);Check(payload.Length==3+3*expected.Length,"Secondary catalog excludes missing rows");for(int i=0;i<expected.Length;i++)Check(BitConverter.ToUInt16(payload,3+3*i)==expected[i].ItemID,"Secondary catalog ID order");}
   Catalog(bonus,invalid);socket.Packets.Clear();ItemMallManager.SendCatalog(p,bonus);wire=socket.Packets.Single();Check(wire.Length==8&&BitConverter.ToUInt16(wire,6)==0,"All-missing catalog safely empty");
  }
  cg.GetField("ItemDatManager").SetValue(null,null);Check(((byte[])payloadMethod.Invoke(null,null)).Length==3,"Missing data loader publishes no invalid IDs");cg.GetField("ItemDatManager").SetValue(null,dat);
  Catalog(false,entries.Where(e=>e.IsBonus==0).ToArray());
  foreach(var t in new[]{new[]{32075,100},new[]{32002,150},new[]{34014,5},new[]{34096,20},new[]{34097,26},new[]{34098,32},new[]{34126,10}}){
   var e=entries.Single(x=>x.ItemID==t[0]&&x.IsBonus==0);var p=NewPlayer();p.UserAcc.IM=e.PointCost;
   Command(p,75,1,Cart(Row(e.ItemID,e.CategoryID,1,e.OrderIndex)));
   Check(p.UserAcc.IM==0&&Count(p,e.ItemID)==e.Count,"New offer exact-price purchase and bundle delivery: "+e.ItemID);
   Check(Packet(75,4,(byte)e.ItemID,(byte)(e.ItemID>>8),e.CategoryID,1,(byte)e.OrderIndex,(byte)(e.OrderIndex>>8),1),"New offer success ACK");
   if(e.ItemID==32075||e.ItemID==32002){
    Command(p,23,15,1,1,0,0);Check((e.ItemID==32075?p.CurHP:p.CurSP)==1+t[1],"New food heals advertised stat");
   }else{
    var pet=new Player.PlayerPetData{Slot=3,PetID=17003,PetName="Grape Mons",Amity=40,HP=100,MaxHP=100,SP=100,MaxSP=100,Level=2};p.PlayerPets.Add(3,pet);Check(p.RegisterClientPet(pet),"New pack pet fixture");
    Command(p,23,15,1,1,pet.ClientSlot,0);Check(pet.Amity==40+t[1],"New amity item works at full HP/SP");
   }
   Check(Count(p,e.ItemID)==e.Count-1&&Packet(23,15),"New item consumes once and plays sound");
  }
  // A stale request from the removed catalog cannot charge even with enough points.
  var buyer=NewPlayer();Command(buyer,75,1,Cart(Row(57205,3,1,1)));Check(buyer.UserAcc.IM==1000&&buyer.Inv.FilledCount==0,"Removed bag cannot be purchased from stale cart");
  transport.Dispose();return checks+" catalog availability and new-offer purchase/use checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[MallChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'),$ExpectDoubleMove.IsPresent)
