param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'storage-bin'),[switch]$ExpectDoubleMove)
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
using Network;

using RCLibrary.Core.Networking;
public class InventorySocket : SocketClient {
 public List<byte[]> Packets;
 public override void SendPacket(IPacket p){Packets.Add(p.Buffer.ToArray());}
}
public static class InventoryChecks {
 static Socket transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
 static int checks; static PhxItemDat dat; static object handler; static Type handlerType; static InventorySocket socket;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static Player NewPlayer(){
  socket=(InventorySocket)FormatterServices.GetUninitializedObject(typeof(InventorySocket));
  socket.Packets=new List<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  foreach(var name in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
  typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
  var p=new Player(socket,dat);p.Eqs.Con=50;p.Eqs.Wis=30;p.Eqs.CurHP=1;p.Eqs.CurSP=1;return p;
 }
 static void Add(Player p,ushort id,byte slot,byte count){var item=new InvItem();item.CopyFrom(dat.GetItemByID(id));item.Ammt=count;Check(p.Inv.AddItem(item,slot,false)==count,"Fixture insertion "+id);}
 static void Command(Player p,byte sub,params byte[] data){socket.Packets.Clear();var bytes=new byte[data.Length+6];bytes[0]=244;bytes[1]=68;bytes[2]=(byte)(data.Length+2);bytes[4]=23;bytes[5]=sub;Array.Copy(data,0,bytes,6,data.Length);p.ProcessSocket((IPacket)new RecievePacket(bytes));}
 static bool Packet(params byte[] payload){return socket.Packets.Any(b=>b.Skip(4).SequenceEqual(payload));}
 static bool Chat(string value){return socket.Packets.Any(b=>b.Length>=10&&b[4]==2&&b[5]==4&&BitConverter.ToUInt32(b,6)==0&&System.Text.Encoding.ASCII.GetString(b,10,b.Length-10)==value);}
 static bool Box(string value){return socket.Packets.Any(b=>b.Length>=10&&b[4]==2&&b[5]==16&&BitConverter.ToUInt32(b,6)==0&&System.Text.Encoding.ASCII.GetString(b,10,b.Length-10)==value);}
 static void Qty(Player p,byte slot,int count){Check(p.Inv[slot].Ammt==count,"Quantity at "+slot+" expected "+count+" got "+p.Inv[slot].Ammt);}
 static int[] Bag(Player p){return Enumerable.Range(1,50).Select(i=>(int)p.Inv[(byte)i].Ammt).ToArray();}
 static int[] Vault(Player p){return Enumerable.Range(1,50).Select(i=>(int)p.Storage[(byte)i].Ammt).ToArray();}
 static void Store(Player p,ushort id,byte slot,byte count,byte damage=0){var item=new InvItem();item.CopyFrom(dat.GetItemByID(id));item.Ammt=count;item.Damage=damage;Check(p.Storage.AddItem(item,slot,false)==count,"Storage fixture");}
 static void Bank(Player p,byte sub,params byte[] data){
  var bag=Bag(p);var vault=Vault(p);var total=bag.Sum()+vault.Sum();var gold=p.Gold;var bank=p.BankGold;
  socket.Packets.Clear();var bytes=new byte[data.Length+6];bytes[0]=244;bytes[1]=68;bytes[2]=(byte)(data.Length+2);bytes[4]=30;bytes[5]=sub;Array.Copy(data,0,bytes,6,data.Length);p.ProcessSocket((IPacket)new RecievePacket(bytes));
  foreach(var b in socket.Packets){
   if(b.Length<6)continue;
   Check(b[4]!=29,"Item transfer never emits gold-bank packets");
   if(b[4]==30&&b[5]==8)Array.Clear(vault,0,50);
   else if(b[4]==23&&b[5]==9){Check(b.Length==8,"Bag removal shape");bag[b[6]-1]-=b[7];}
   else if((b[4]==23&&b[5]==5)||(b[4]==30&&b[5]==1)){
    Check((b.Length-6)%31==0,"Native item record size");var target=b[4]==23?bag:vault;
    for(int i=6;i<b.Length;i+=31){Check(b[i]>=1&&b[i]<=50,"Native slot range");target[b[i]-1]+=b[i+3];}
   }
  }
  Check(bag.SequenceEqual(Bag(p)),"Client bag matches server after transfer");Check(vault.SequenceEqual(Vault(p)),"Client storage matches server after transfer");
  Check(total==Bag(p).Sum()+Vault(p).Sum(),"Total count conserved");Check(p.Gold==gold&&p.BankGold==bank,"Gold unchanged");
  Check(Packet(30,6)&&Packet(30,7),"Pending bank selections released");
 }
 public static string Run(string dataFile,bool reproduce){
  dat=new PhxItemDat();Check(dat.Load(dataFile).GetAwaiter().GetResult(),"Data load");
  (handlerType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC23")).First(t=>t!=null)).Assembly.GetType("System.cGlobal").GetField("ItemDatManager",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,dat);
  handler=Activator.CreateInstance(handlerType);
  var handlers=(Dictionary<int,Network.ActionCodes.AC>)typeof(Network.ActionCodes.AC).GetField("AcList",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  handlers[23]=(Network.ActionCodes.AC)handler;
  handlers[30]=(Network.ActionCodes.AC)Activator.CreateInstance(handlerType.Assembly.GetType("Network.ActionCodes.AC30"));
  if(reproduce){var probe=NewPlayer();Add(probe,34014,7,3);Add(probe,45001,12,22);socket.Packets.Clear();probe.ProcessSocket((IPacket)new RecievePacket(new byte[]{244,68,3,0,30,2,7}));Check(socket.Packets.Any(b=>b.Length>5&&b[4]==29&&b[5]==5),"Old deposit opens gold bank");Check(socket.Packets.Any(b=>b.Length>5&&b[4]==23&&b[5]==5&&b.Skip(6).Where((v,i)=>i%31==0).Contains((byte)12)),"Old deposit adds unrelated inventory stack again");transport.Dispose();return "REPRODUCED: old storage deposit re-adds the entire bag and opens gold bank.";}
  var p=NewPlayer();Add(p,21013,8,1);Command(p,11,8);Check(p.Eqs[2].ItemID==21013 && p.Inv[8].ItemID==0,"Equip transfers ownership");Check(Packet(23,17,8,8),"Native equip ack");
  Check(socket.Packets.Count(b=>b.Length>5&&b[4]==23&&b[5]==17)==1,"Equip ack sent exactly once");
  Check(socket.Packets.Any(b=>System.Text.Encoding.ASCII.GetString(b).Contains("DEF +1")),"Wear reports actual DEF gain");Check(Box("Equipment: DEF +1"),"Equipment notice uses native message box");
  Command(p,11,8);Check(p.Eqs[2].ItemID==21013,"Repeated equip preserves item");Check(!Packet(23,17,8,8),"Repeated equip no false ack");
  Add(p,45001,9,3);Command(p,12,2,9);Check(p.Eqs[2].ItemID==21013 && p.Inv[9].ItemID==45001,"Occupied unequip rejected atomically");
  Command(p,12,2,10);Check(p.Eqs[2].ItemID==0 && p.Inv[10].ItemID==21013,"Unequip transfers to bag");Qty(p,10,1);Check(Packet(23,16,2,10),"Native unequip ack");
  Check(socket.Packets.Count(b=>b.Length>5&&b[4]==23&&b[5]==16)==1,"Unequip ack sent exactly once");
  Check(socket.Packets.Any(b=>System.Text.Encoding.ASCII.GetString(b).Contains("DEF -1")),"Unequip reports actual DEF loss");Check(Box("Equipment: DEF -1"),"Unequip message uses native message box");
  Command(p,96,10);Check(p.Eqs[2].ItemID==21013 && p.Inv[10].ItemID==0,"Double click equips");
  Add(p,21006,11,1);Command(p,11,11);Check(p.Eqs[2].ItemID==21006 && p.Inv[11].ItemID==21013,"Equip swap preserves previous outfit");Qty(p,11,1);
  var saved=p.Inv.InventoryDBData;var worn=p.Eqs.EqData;var q=NewPlayer();
  foreach(var kv in saved.Where(x=>x.Value[0]!=0)){Add(q,(ushort)kv.Value[0],kv.Key,(byte)kv.Value[2]);q.Inv[kv.Key].Damage=(byte)kv.Value[1];}
  foreach(var kv in worn.Where(x=>x.Value[0]!=0)){var i=new Item(dat.GetItemByID((ushort)kv.Value[0]));q.Eqs.Wear(kv.Key,i);}
  Check(q.Inv[11].ItemID==21013 && q.Eqs[2].ItemID==21006,"Inventory/equipment persistence snapshot roundtrip");
  p=NewPlayer();Add(p,45001,1,30);Command(p,10,1,5,2);Qty(p,1,25);Qty(p,2,5);Check(Packet(23,10,1,5,2),"Native split ack");
  Check(socket.Packets.Count(b=>b.Length>5&&b[4]==23&&b[5]==10)==1,"Move ack sent exactly once through socket");
  Add(p,43002,3,4);Command(p,10,1,25,3);Qty(p,1,25);Check(p.Inv[3].ItemID==43002,"Different occupied destination preserved");
  Add(p,45001,4,48);Command(p,10,1,25,4);Qty(p,1,23);Qty(p,4,50);Check(Packet(23,10,1,2,4),"Merge ack actual amount");
  Command(p,10,2,50,5);Qty(p,2,0);Qty(p,5,5);Check(Packet(23,10,2,5,5),"Move clamps source count");
  foreach(byte bad in new byte[]{0,51,255}) {p.Inv.MoveItem(bad,1,1);p.Inv.MoveItem(1,bad,1);checks++;}
  p.Inv.MoveItem(1,1,10);p.Inv.MoveItem(1,6,0);Qty(p,1,23);Qty(p,6,0);
  var removed=p.Inv.RemoveItem(1,3,false);Check(removed.Ammt==3,"Removed copy exact partial quantity");Qty(p,1,20);
  removed=p.Inv.RemoveItem(1,50,false);Check(removed.Ammt==20,"Removed copy clamps quantity");Qty(p,1,0);
  Add(p,24013,7,1);p.Inv.MoveItem(7,8,50);Check(p.Inv[8].ItemID==24013 && p.Inv[7].ItemID==0,"Nonstackable move preserves one item");Qty(p,8,1);
  p=NewPlayer();Add(p,32075,1,4);Command(p,15,1,1,0,0);Qty(p,1,3);Check(p.Eqs.CurHP==101,"Chocolate uses item-data recovery 100");Check(Packet(23,9,1,1),"Consume native quantity decrement");Check(Packet(23,15),"Successful use plays native consume sound");Check(socket.Packets.Count(b=>b.Skip(4).SequenceEqual(new byte[]{23,15}))==1,"Consume sound sent once");Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==23&&b[5]==208),"No incorrect 23:208 count update");
  p.Eqs.CurHP=1;Command(p,15,1,50,0,0);Qty(p,1,0);Check(p.Eqs.CurHP==301,"Overrequest heals only actual stock");Check(Packet(23,9,1,3),"Overrequest decrement is actual stock");
  Add(p,32075,1,2);p.Eqs.CurHP=p.Eqs.FullHP;Command(p,15,1,1,0,0);Qty(p,1,2);Check(!Packet(23,9,1,1),"No charge when HP full");Check(!Packet(23,15),"Failed use does not play success sound");
  p.Eqs.CurHP=1;Command(p,96,1);Qty(p,1,1);Check(p.Eqs.CurHP==101 && Packet(23,9,1,1),"Double-click shares recovery/quantity rules");Check(Packet(23,15),"Double-click use also plays consume sound");
  Add(p,32002,2,2);p.Eqs.CurSP=1;Command(p,15,2,1,0,0);Qty(p,2,1);Check(p.Eqs.CurSP==Math.Min(p.Eqs.FullSP,151),"Rice wine uses SP status26 and offset100");
  Add(p,45001,3,2);Command(p,96,3);Qty(p,3,2);Add(p,34026,4,1);Command(p,96,4);Qty(p,4,1);Check(p.Inv[4].ItemID==34026,"Unsupported EXP pill does not disappear as equipment");
  p.Eqs.CurHP=1;Command(p,15,1,1,0,1);Qty(p,1,1);Check(p.Eqs.CurHP==1,"Invalid ushort target cannot wrap to player");
  Command(p,15,1,1,4,0);Qty(p,1,1);Check(p.Eqs.CurHP==1,"Missing pet does not fall back to player");
  var pet=new Player.PlayerPetData{Slot=3,PetID=17003,PetName="Grape Mons",HP=10,MaxHP=300,SP=20,MaxSP=100,Level=2};
  p.PlayerPets.Add(3,pet);Check(p.RegisterClientPet(pet)&&pet.ClientSlot==1,"Fixture pet DB slot differs from client slot");Command(p,15,1,1,1,0);Qty(p,1,0);Check(pet.HP==110 && p.Eqs.CurHP==1,"Food targets client pet identity");Check(socket.Packets.Any(b=>b.Length>5&&b[4]==8&&b[5]==2),"Pet stats synchronized");Check(Packet(23,15),"Pet food plays consume sound");
  // Replay native additive bag/storage semantics, with AC30:8 storage clear.
  p=NewPlayer();Add(p,34014,4,10);var stored=new Item(dat.GetItemByID(45001));stored.Ammt=7;Check(p.Storage.AddItem(stored,5,false)==7,"Storage fixture insertion");
  int clientBagCount=10;int[] clientStore=new int[51];clientStore[49]=6;
  for(int reopen=0;reopen<20;reopen++){
   socket.Packets.Clear();p.OpenPropsKeeper();
   Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==23),"Open keeper must not resend additive bag contents");
   Check(socket.Packets.Count(b=>b.Skip(4).SequenceEqual(new byte[]{30,8}))==1,"Storage cache cleared exactly once");
   Check(socket.Packets.Count(b=>b.Length>5&&b[4]==30&&b[5]==1)==1,"One storage list only");
   Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==30&&b[5]!=1&&b[5]!=8),"No guessed storage variants");
   Check(socket.Packets[0].Skip(4).SequenceEqual(new byte[]{30,8})&&socket.Packets[1][4]==30&&socket.Packets[1][5]==1,"Clear before additive list");
   foreach(var packet in socket.Packets){
    if(packet[4]==30&&packet[5]==8){Array.Clear(clientStore,0,clientStore.Length);}
    if(packet[4]==30&&packet[5]==1){Check((packet.Length-6)%31==0,"Native storage list record size");for(int offset=6;offset<packet.Length;offset+=31){int slot=packet[offset];clientStore[slot]=Math.Min(50,clientStore[slot]+packet[offset+3]);}}
    if(packet[4]==23&&packet[5]==5){for(int offset=6;offset<packet.Length;offset+=31)clientBagCount+=packet[offset+3];}
   }
   Check(clientBagCount==10&&clientStore[5]==7&&clientStore[49]==0,"Reopening keeper does not accumulate or retain stale contents");
   Check(p.Inv[4].Ammt==10&&p.Storage[5].Ammt==7,"Open keeper preserves server inventory/storage");
  }
  p.Storage.RemoveItem(5,7,false);socket.Packets.Clear();p.OpenPropsKeeper();Check(Packet(30,8)&&Packet(30,1),"Empty vault clears stale contents with empty list");
  socket.Packets.Clear();p.SendSystemMessage("Equipment: DEF +1");Check(socket.Packets.Count==0,"Legacy GM messages emit no packets");Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==23&&b[5]==57),"Text never uses predefined notice opcode");
  foreach(ushort invalid in new ushort[]{28006,28007,28014}){
   Check(dat.GetItemByID(invalid)==null,"Invalid legacy item confirmed absent");
   Check(p.Inv.AddItem(invalid,1,false)==0,"Invalid ID grant rejected");
   var ghost=new Item(new PhxItemInfo{ItemID=invalid,ItemName=System.Text.Encoding.ASCII.GetBytes("Unknown")});
   Check(p.Inv.AddItem(ghost,9,false)==0 && p.Inv[9].ItemID==0,"Invalid object grant cannot block empty slot");
  }
  p=NewPlayer();Add(p,21013,1,1);Command(p,11,1);Command(p,12,2,9);
  Check(p.Inv[9].ItemID==21013 && p.Eqs[2].ItemID==0,"Captured double-click unequip request succeeds into empty cell 9");
  p=NewPlayer();Add(p,34014,4,10);Command(p,10,4,1,23);Qty(p,4,9);Qty(p,23,1);
  Check(socket.Packets.Count(b=>b.Length>5&&b[4]==23&&b[5]==10)==1,"Captured stack drag 04 01 17 processes once");
  for(int n=0;n<100;n++){p=NewPlayer();Add(p,45001,1,37);Add(p,45001,2,19);byte amount=(byte)(n%50+1);p.Inv.MoveItem(1,2,amount);Check(p.Inv[1].Ammt+p.Inv[2].Ammt==56,"Merge conservation "+n);Check(p.Inv[2].Ammt<=50,"Stack cap "+n);}
  p=NewPlayer();Add(p,21013,1,1);Command(p,11,1);Check(Box("Equipment: DEF +1"),"Stats in equipment message box");Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==2&&b[5]==4),"Equipment stats do not emit GM chat");
  foreach(var test in new[]{new[]{34014,5},new[]{34096,20},new[]{34097,26},new[]{34098,32},new[]{34119,10},new[]{34120,20},new[]{34121,40},new[]{34126,10}}){
   p=NewPlayer();pet=new Player.PlayerPetData{Slot=3,PetID=17003,PetName="Grape Mons",Amity=40,HP=300,MaxHP=300,SP=100,MaxSP=100,Level=2};p.PlayerPets.Add(3,pet);Check(p.RegisterClientPet(pet)&&pet.ClientSlot==1,"Amity fixture slot identity");Add(p,(ushort)test[0],18,4);
   Command(p,15,18,1,1,0);Check(pet.Amity==40+test[1]&&p.Inv[18].Ammt==3,"Real item DAT amity gain "+test[0]);
   Check(Packet(8,2,4,1,0,64,1,(byte)pet.Amity,0,0,0,0,0,0,0),"Amity uses native stat64 and client slot");Check(Packet(23,9,18,1)&&Packet(23,15),"Amity consumption and sound once");Check(Box("Grape Mons: Amity +"+test[1]+" ("+pet.Amity+"/100)"),"Actual amity gain in box");Check(pet.HP==300&&pet.SP==100,"Amity works at full HP/SP without changing them");
   pet.Amity=99;Command(p,15,18,3,1,0);Check(pet.Amity==100&&p.Inv[18].Ammt==2,"Cap100 consumes only one needed item");Check(Box("Grape Mons: Amity +1 (100/100)"),"Cap reports actual gain");
   Command(p,15,18,1,1,0);Check(pet.Amity==100&&p.Inv[18].Ammt==2&&!Packet(23,15),"Full amity does not consume/play success");
   pet.Amity=20;foreach(byte target in new byte[]{0,2,4}){Command(p,15,18,1,target,0);Check(pet.Amity==20&&p.Inv[18].Ammt==2,"Invalid/player target not fed");}
   p.Inv[18].isLocked=true;Command(p,15,18,1,1,0);Check(pet.Amity==20&&p.Inv[18].Ammt==2,"Locked amity item preserved");p.Inv[18].isLocked=false;
   Command(p,15,18,50,1,0);Check(pet.Amity==Math.Min(100,20+2*test[1])&&p.Inv[18].Ammt==0,"Requested quantity clamped to stock");
  }
  p=NewPlayer();pet=new Player.PlayerPetData{Slot=4,PetID=17003,PetName="Grape Mons",Amity=60,IsBattle=true,Level=2};p.PlayerPets.Add(4,pet);p.RegisterClientPet(pet);Add(p,34014,3,2);socket.Packets.Clear();Check(Game.PetRelated.PetAmityManager.FeedPet(p,34014)&&pet.Amity==65&&p.Inv[3].Ammt==1,"Feed command shares real amity and consumption logic");Add(p,30025,4,1);Check(!Game.PetRelated.PetAmityManager.FeedPet(p,30025)&&p.Inv[4].Ammt==1&&pet.Amity==65,"Star token is not falsely consumed as rice ball");
  p=NewPlayer();Add(p,34014,7,3);Add(p,45001,12,22);
  Bank(p,2,7);Check(p.Inv[7].Ammt==0&&p.Storage[1].Ammt==3&&p.Inv[12].Ammt==22,"Captured 30:2 07 deposits selected stack only");
  Bank(p,1,1);Check(p.Storage[1].ItemID==0&&p.Inv.GetItemCount(34014)==3,"Quick withdrawal adds exact stock once");
  p=NewPlayer();Add(p,34014,7,3);Add(p,45001,12,22);Bank(p,2,7,12);Check(p.Inv.FilledCount==0&&p.Storage.GetItemCount(34014)==3&&p.Storage.GetItemCount(45001)==22,"Multiple selected slots are not misread as quantity");
  Bank(p,1,1,2);Check(p.Storage.FilledCount==0&&p.Inv.GetItemCount(45001)==22,"Multiple withdrawals");
  p=NewPlayer();Add(p,34014,7,10);Bank(p,4,18,7);Check(p.Inv[7].ItemID==0&&p.Storage[18].Ammt==10,"Drag deposit destination then source");
  Bank(p,3,18,40);Check(p.Storage[18].ItemID==0&&p.Storage[40].Ammt==10&&p.Inv.FilledCount==0,"Storage drag never withdraws");
  Bank(p,5,23,40);Check(p.Inv[23].Ammt==10&&p.Storage.FilledCount==0,"Drag withdraw destination then source");
  for(int n=0;n<50;n++){Bank(p,4,40,23);Bank(p,5,23,40);}Check(p.Inv[23].Ammt==10,"Repeated transfers never inflate stock");
  p=NewPlayer();Add(p,34014,7,10);Store(p,34014,18,47);Bank(p,4,18,7);Check(p.Inv[7].Ammt==7&&p.Storage[18].Ammt==50,"Target merge only moves capacity");
  Bank(p,4,18,7);Check(p.Inv[7].Ammt==7&&p.Storage[18].Ammt==50,"Full target unchanged");
  Bank(p,2,7);Check(p.Inv[7].ItemID==0&&p.Storage.GetItemCount(34014)==57,"Automatic transfer uses empty slot after full stack");
  p=NewPlayer();Add(p,34014,1,5);Store(p,34014,1,48);for(byte i=2;i<=50;i++)Store(p,45001,i,50);Bank(p,2,1);Check(p.Inv[1].Ammt==5&&p.Storage[1].Ammt==48,"Full storage cannot partially consume source");
  p=NewPlayer();Store(p,34014,1,5);Add(p,34014,1,48);for(byte i=2;i<=50;i++)Add(p,45001,i,50);Bank(p,1,1);Check(p.Storage[1].Ammt==5&&p.Inv[1].Ammt==48,"Full bag cannot duplicate partial withdrawal");
  p=NewPlayer();Add(p,21013,7,1);p.Inv[7].Damage=19;Bank(p,4,18,7);Check(p.Storage[18].ItemID==21013&&p.Storage[18].Damage==19,"Equipment metadata survives deposit");Bank(p,5,23,18);Check(p.Inv[23].Damage==19&&p.Inv[23].Ammt==1,"Equipment metadata survives withdrawal");
  p=NewPlayer();Add(p,34014,7,10);Store(p,45001,18,4);Bank(p,4,18,7);Check(p.Inv[7].Ammt==10&&p.Storage[18].Ammt==4,"Different target item preserved");
  p.Inv[7].isLocked=true;Bank(p,2,7);Check(p.Inv[7].Ammt==10,"Locked source preserved");p.Inv[7].isLocked=false;p.Storage[20].isLocked=true;Bank(p,4,20,7);Check(p.Inv[7].Ammt==10&&p.Storage[20].ItemID==0,"Locked empty target preserved");
  Store(p,34014,25,4,3);Bank(p,4,25,7);Check(p.Inv[7].Ammt==10&&p.Storage[25].Ammt==4,"Different durability never merged");
  foreach(var request in new[]{new byte[]{0},new byte[]{51},new byte[0]})Bank(p,2,request);
  foreach(var request in new[]{new byte[]{0,7},new byte[]{51,7},new byte[]{1},new byte[]{1,7,3}})Bank(p,4,request);
  Check(p.Inv[7].Ammt==10,"Invalid packets preserve items");
  p=NewPlayer();Add(p,34014,7,10);Bank(p,2,7,7);Check(p.Storage.GetItemCount(34014)==10,"Duplicate selected slot processed once");Bank(p,3,1,1);Check(p.Storage[1].Ammt==10,"Same-slot storage move preserves stack");
  transport.Dispose();return checks+" inventory protocol/state checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[InventoryChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'),$ExpectDoubleMove.IsPresent)


