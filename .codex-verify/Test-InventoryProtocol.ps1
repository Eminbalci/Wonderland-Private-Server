param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'inventory-bin'))
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
 static int checks; static PhxItemDat dat; static object handler; static Type handlerType; static InventorySocket socket;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static Player NewPlayer(){
  socket=(InventorySocket)FormatterServices.GetUninitializedObject(typeof(InventorySocket));
  socket.Packets=new List<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  var p=new Player(socket,dat);p.Eqs.Con=50;p.Eqs.Wis=30;p.Eqs.CurHP=1;p.Eqs.CurSP=1;return p;
 }
 static void Add(Player p,ushort id,byte slot,byte count){var item=new InvItem();item.CopyFrom(dat.GetItemByID(id));item.Ammt=count;Check(p.Inv.AddItem(item,slot,false)==count,"Fixture insertion "+id);}
 static void Command(Player p,byte sub,params byte[] data){socket.Packets.Clear();var bytes=new byte[data.Length+6];bytes[0]=244;bytes[1]=68;bytes[2]=(byte)(data.Length+2);bytes[4]=23;bytes[5]=sub;Array.Copy(data,0,bytes,6,data.Length);handlerType.GetMethod("ProcessPkt").Invoke(handler,new object[]{p,new RecievePacket(bytes)});}
 static bool Packet(params byte[] payload){return socket.Packets.Any(b=>b.Skip(4).SequenceEqual(payload));}
 static void Qty(Player p,byte slot,int count){Check(p.Inv[slot].Ammt==count,"Quantity at "+slot+" expected "+count+" got "+p.Inv[slot].Ammt);}
 public static string Run(string dataFile){
  dat=new PhxItemDat();Check(dat.Load(dataFile).GetAwaiter().GetResult(),"Data load");
  (handlerType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC23")).First(t=>t!=null)).Assembly.GetType("System.cGlobal").GetField("ItemDatManager",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,dat);
  handler=Activator.CreateInstance(handlerType);var p=NewPlayer();Add(p,21013,8,1);Command(p,11,8);Check(p.Eqs[2].ItemID==21013 && p.Inv[8].ItemID==0,"Equip transfers ownership");Check(Packet(23,17,8,8),"Native equip ack");
  Command(p,11,8);Check(p.Eqs[2].ItemID==21013,"Repeated equip preserves item");Check(!Packet(23,17,8,8),"Repeated equip no false ack");
  Add(p,45001,9,3);Command(p,12,2,9);Check(p.Eqs[2].ItemID==21013 && p.Inv[9].ItemID==45001,"Occupied unequip rejected atomically");
  Command(p,12,2,10);Check(p.Eqs[2].ItemID==0 && p.Inv[10].ItemID==21013,"Unequip transfers to bag");Qty(p,10,1);Check(Packet(23,16,2,10),"Native unequip ack");
  Command(p,96,10);Check(p.Eqs[2].ItemID==21013 && p.Inv[10].ItemID==0,"Double click equips");
  Add(p,21006,11,1);Command(p,11,11);Check(p.Eqs[2].ItemID==21006 && p.Inv[11].ItemID==21013,"Equip swap preserves previous outfit");Qty(p,11,1);
  var saved=p.Inv.InventoryDBData;var worn=p.Eqs.EqData;var q=NewPlayer();
  foreach(var kv in saved.Where(x=>x.Value[0]!=0)){Add(q,(ushort)kv.Value[0],kv.Key,(byte)kv.Value[2]);q.Inv[kv.Key].Damage=(byte)kv.Value[1];}
  foreach(var kv in worn.Where(x=>x.Value[0]!=0)){var i=new Item(dat.GetItemByID((ushort)kv.Value[0]));q.Eqs.Wear(kv.Key,i);}
  Check(q.Inv[11].ItemID==21013 && q.Eqs[2].ItemID==21006,"Inventory/equipment persistence snapshot roundtrip");
  p=NewPlayer();Add(p,45001,1,30);Command(p,10,1,5,2);Qty(p,1,25);Qty(p,2,5);Check(Packet(23,10,1,5,2),"Native split ack");
  Add(p,43002,3,4);Command(p,10,1,25,3);Qty(p,1,25);Check(p.Inv[3].ItemID==43002,"Different occupied destination preserved");
  Add(p,45001,4,48);Command(p,10,1,25,4);Qty(p,1,23);Qty(p,4,50);Check(Packet(23,10,1,2,4),"Merge ack actual amount");
  Command(p,10,2,50,5);Qty(p,2,0);Qty(p,5,5);Check(Packet(23,10,2,5,5),"Move clamps source count");
  foreach(byte bad in new byte[]{0,51,255}) {p.Inv.MoveItem(bad,1,1);p.Inv.MoveItem(1,bad,1);checks++;}
  p.Inv.MoveItem(1,1,10);p.Inv.MoveItem(1,6,0);Qty(p,1,23);Qty(p,6,0);
  var removed=p.Inv.RemoveItem(1,3,false);Check(removed.Ammt==3,"Removed copy exact partial quantity");Qty(p,1,20);
  removed=p.Inv.RemoveItem(1,50,false);Check(removed.Ammt==20,"Removed copy clamps quantity");Qty(p,1,0);
  Add(p,24013,7,1);p.Inv.MoveItem(7,8,50);Check(p.Inv[8].ItemID==24013 && p.Inv[7].ItemID==0,"Nonstackable move preserves one item");Qty(p,8,1);
  p=NewPlayer();Add(p,32075,1,4);Command(p,15,1,1,0,0);Qty(p,1,3);Check(p.Eqs.CurHP==101,"Chocolate uses item-data recovery 100");Check(Packet(23,9,1,1),"Consume native quantity decrement");Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==23&&b[5]==208),"No incorrect 23:208 count update");
  p.Eqs.CurHP=1;Command(p,15,1,50,0,0);Qty(p,1,0);Check(p.Eqs.CurHP==301,"Overrequest heals only actual stock");Check(Packet(23,9,1,3),"Overrequest decrement is actual stock");
  Add(p,32075,1,2);p.Eqs.CurHP=p.Eqs.FullHP;Command(p,15,1,1,0,0);Qty(p,1,2);Check(!Packet(23,9,1,1),"No charge when HP full");
  p.Eqs.CurHP=1;Command(p,96,1);Qty(p,1,1);Check(p.Eqs.CurHP==101 && Packet(23,9,1,1),"Double-click shares recovery/quantity rules");
  Add(p,32002,2,2);p.Eqs.CurSP=1;Command(p,15,2,1,0,0);Qty(p,2,1);Check(p.Eqs.CurSP==Math.Min(p.Eqs.FullSP,151),"Rice wine uses SP status26 and offset100");
  Add(p,45001,3,2);Command(p,96,3);Qty(p,3,2);Add(p,34026,4,1);Command(p,96,4);Qty(p,4,1);Check(p.Inv[4].ItemID==34026,"Unsupported EXP pill does not disappear as equipment");
  p.Eqs.CurHP=1;Command(p,15,1,1,0,1);Qty(p,1,1);Check(p.Eqs.CurHP==1,"Invalid ushort target cannot wrap to player");
  Command(p,15,1,1,4,0);Qty(p,1,1);Check(p.Eqs.CurHP==1,"Missing pet does not fall back to player");
  var pet=new Player.PlayerPetData{Slot=3,PetID=17003,PetName="Grape Mons",HP=10,MaxHP=300,SP=20,MaxSP=100,Level=2};
  p.PlayerPets.Add(3,pet);Check(p.RegisterClientPet(pet)&&pet.ClientSlot==1,"Fixture pet DB slot differs from client slot");Command(p,15,1,1,1,0);Qty(p,1,0);Check(pet.HP==110 && p.Eqs.CurHP==1,"Food targets client pet identity");Check(socket.Packets.Any(b=>b.Length>5&&b[4]==8&&b[5]==2),"Pet stats synchronized");
  for(int n=0;n<100;n++){p=NewPlayer();Add(p,45001,1,37);Add(p,45001,2,19);byte amount=(byte)(n%50+1);p.Inv.MoveItem(1,2,amount);Check(p.Inv[1].Ammt+p.Inv[2].Ammt==56,"Merge conservation "+n);Check(p.Inv[2].Ammt<=50,"Stack cap "+n);}
  return checks+" inventory protocol/state checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[InventoryChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'))


