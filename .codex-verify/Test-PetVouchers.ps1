param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'storage-bin'))
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
 static int checks; static PhxItemDat dat; static InventorySocket socket;
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
 public static string Run(string root){
  dat=new PhxItemDat();Check(dat.Load(System.IO.Path.Combine(root,"Data/itemDat.wpdat")).GetAwaiter().GetResult(),"Item data loaded");
  Game.DataFiles.SceneDataManager.Initialize(System.IO.Path.Combine(root,"Data"));
  var asm=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetType("Network.ActionCodes.AC23")!=null);
  asm.GetType("System.cGlobal").GetField("ItemDatManager",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,dat);
  var handlers=(Dictionary<int,Network.ActionCodes.AC>)typeof(Network.ActionCodes.AC).GetField("AcList",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  handlers[23]=(Network.ActionCodes.AC)Activator.CreateInstance(asm.GetType("Network.ActionCodes.AC23"));
  var config=System.IO.Path.Combine(root,"Data/pet_vouchers.json");
  Game.PetRelated.PetVoucherManager.Load(dat,config);
  var entries=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Game.PetRelated.PetVoucherManager.Entry[]>(System.IO.File.ReadAllText(config));
  var raw=System.IO.File.ReadAllBytes(@"D:/Game Private/WLRI/data/Item.dat");
  var native=new Dictionary<ushort,ushort>();
  for(int i=0;i<raw.Length;i+=451){
   int kind=((raw[i+15]^0x9a)-9)&255,special=((raw[i+47]^0x9a)-9)&255;
   ushort id=(ushort)((BitConverter.ToUInt16(raw,i+16)^0xefc3)-9);
   ushort pet=(ushort)((BitConverter.ToUInt16(raw,i+413)^0xefc3)-9);
   if(kind==21&&special==146&&pet!=0)native.Add(id,pet);
  }
  Check(entries.Length==native.Count,"All native voucher mappings covered");
  int supported=0;
  foreach(var entry in entries){
   Check(native[entry.item_id]==entry.pet_id,"Exact client pet mapping "+entry.item_id);
   if(dat.GetItemByID(entry.item_id)==null)continue;
   supported++;
   Check(Game.DataFiles.SceneDataManager.GetNpcBaseStats(entry.pet_id)!=null,"Supported pet data exists");
   var p=NewPlayer();Add(p,entry.item_id,7,1);Add(p,34038,1,1);Command(p,96,7);
   Check(p.PlayerPets.Count==1,"Voucher recruits one pet "+entry.item_id);
   var pet=p.PlayerPets.Values.Single();
   Check(pet.PetID==entry.pet_id&&pet.ClientSlot==1&&pet.Slot==1,"Correct pet and roster identity");
   Check(pet.HP==pet.MaxHP&&pet.SP==pet.MaxSP&&pet.Level==1,"Initialized full pet vitals");
   Check(p.Inv[7].ItemID==0&&p.Inv[1].Ammt==1,"Only one voucher consumed");
   Check(Packet(23,9,7,1)&&Packet(23,15),"Native consume and success sound");
   Check(socket.Packets.Count(b=>b.Length>5&&b[4]==15&&b[5]==1)==1,"Exactly one native recruit");
   Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==23&&(b[5]==5||b[5]==6)),"No additive inventory replay");
   Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==2&&b[5]==4),"No GM chat");
   Check(Box(pet.PetName+" joined your party."),"Success in message box");
   Command(p,96,7);Check(p.PlayerPets.Count==1&&!Packet(23,15),"Repeated empty use no duplicate pet");
   Add(p,entry.item_id,8,1);Command(p,96,8);
   Check(p.Inv[8].Ammt==1&&p.PlayerPets.Count==1&&!Packet(23,15),"Duplicate pet retains voucher");
   Check(Box("This pet is already in your party. Voucher retained."),"Duplicate explanation");
  }
  foreach(var mode in new[]{"full","locked","combat","quantity","target","malformed","hole","repeat"}){
   var p=NewPlayer();Add(p,30194,7,1);
   if(mode=="full"||mode=="hole"){
    foreach(byte slot in mode=="full"?new byte[]{1,2,3,4}:new byte[]{1,3,4}){
     var pet=new Player.PlayerPetData{Slot=slot,PetID=(uint)(17000+slot),PetName="Fixture",Level=1};p.PlayerPets.Add(slot,pet);p.RegisterClientPet(pet);
    }
   }
   if(mode=="locked")p.Inv[7].isLocked=true;
   var battles=(Dictionary<uint,Game.Battle.ActiveBattle>)typeof(Game.Battle.PvEBattleManager).GetField("_activeBattles",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
   if(mode=="combat")battles.Add(p.CharID,new Game.Battle.ActiveBattle());
   try{
    if(mode=="quantity")Command(p,15,7,2,0,0);
    else if(mode=="target")Command(p,15,7,1,1,0);
    else if(mode=="malformed")Command(p,96,7,1);
    else if(mode=="repeat"){
     socket.Packets.Clear();System.Threading.Tasks.Parallel.For(0,12,n=>Game.PetRelated.PetVoucherManager.TryRedeem(p,7));
    }
    else Command(p,96,7);
   }finally{if(mode=="combat")battles.Remove(p.CharID);}
   if(mode=="hole"||mode=="repeat"){
    Check(p.Inv[7].ItemID==0&&p.PlayerPets.Values.Count(x=>x.PetID==17215)==1,"Exactly one Crane "+mode);
    if(mode=="hole")Check(p.PlayerPets[2].ClientSlot==4,"Server roster hole differs from client slot");
   }else Check(p.Inv[7].Ammt==1&&!Packet(23,15),"Voucher retained: "+mode);
  }
  var fallback=NewPlayer();Add(fallback,30194,2,1);Command(fallback,15,2,1,0,0);
  Check(fallback.PlayerPets.Values.Single().PetID==17215&&fallback.Inv[2].Ammt==0,"Use command shares redemption");
  foreach(ushort id in new ushort[]{30002,34014,27025}){
   var p=NewPlayer();Add(p,id,2,1);Command(p,96,2);Check(p.PlayerPets.Count==0&&p.Inv[2].Ammt==1,"Unrelated item is not a pet voucher "+id);
  }
  // Missing item data must never turn a stale inventory record into a free pet.
  var missing=entries.First(e=>dat.GetItemByID(e.item_id)==null);
  var ghost=NewPlayer();ghost.Inv[5].CopyFrom(new PhxItemInfo{ItemID=missing.item_id,ItemName=System.Text.Encoding.ASCII.GetBytes("Missing voucher")});ghost.Inv[5].Ammt=1;
  Command(ghost,96,5);Check(ghost.Inv[5].Ammt==1&&ghost.PlayerPets.Count==0,"Missing definition retained");
  Check(Box("This pet has no available data. Voucher retained."),"Missing data explanation");
  // Actual database roundtrip, isolated from the live character database.
  var fixture=System.IO.Path.Combine(root,".codex-verify/pet-voucher-persistence.db");
  System.IO.File.Copy(System.IO.Path.Combine(root,".codex-verify/login-regression.db"),fixture,true);
  RCLibrary.Core.DataBase.DefaultDBFile=fixture;
  new DataBase.GameDataBase().VerifySetup();
  var db=new DataBase.CharacterDataBase();db.ItemDat=dat;
  var persistent=NewPlayer();persistent.Slot=2;persistent.UserAcc.DataBaseID=1;persistent.Tent.IsDirty=false;
  Check(db.GetCharacterData(4510001,ref persistent),"Load fixture character");
  persistent.PlayerPets.Clear();for(byte i=1;i<=50;i++)persistent.Inv[i].Clear();
  Add(persistent,30194,7,1);Command(persistent,96,7);
  var savedPets=db.GetDataTable("SELECT petID FROM character_pets WHERE charID=4510001 AND isHotel=0");
  Check(savedPets.Rows.Count==1&&Convert.ToInt32(savedPets.Rows[0]["petID"])==17215,"Crane persisted by redeem");
  var savedVoucher=db.GetDataTable("SELECT * FROM inventory WHERE charID=4510001 AND storID=0 AND itemID=30194");
  Check(savedVoucher.Rows.Count==0,"Consumed voucher removed from database");
  var gameDb=new DataBase.GameDataBase();gameDb.ItemDat=dat;
  var reloaded=NewPlayer();reloaded.Slot=2;reloaded.UserAcc.DataBaseID=1;gameDb.LoadFinalData(reloaded);
  Check(reloaded.PlayerPets.Values.Count(x=>x.PetID==17215)==1&&reloaded.Inv.GetItemCount(30194)==0,"Reload preserves pet and consumed voucher");
  DataBase.CharacterDataBase.GlobalInstance=null;
  transport.Dispose();return checks+" pet voucher checks passed; "+entries.Length+" native mappings audited; "+supported+" available vouchers redeemed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll','System.Web.Extensions.dll','System.Data.dll','System.Xml.dll'))
[InventoryChecks]::Run((Split-Path $PSScriptRoot -Parent))
