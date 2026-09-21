param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'inventory-feedback-bin'),[switch]$ExpectDoubleMove)
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
 static void Command(Player p,byte ac,byte sub,params byte[] data){socket.Packets.Clear();var bytes=new byte[data.Length+6];bytes[0]=244;bytes[1]=68;bytes[2]=(byte)(data.Length+2);bytes[3]=(byte)((data.Length+2)>>8);bytes[4]=ac;bytes[5]=sub;Array.Copy(data,0,bytes,6,data.Length);p.ProcessSocket((IPacket)new RecievePacket(bytes));}
 public static string Run(string dataFile){
  dat=new PhxItemDat();Check(dat.Load(dataFile).GetAwaiter().GetResult(),"Data load");
  typeof(RCLibrary.Core.PathHelper).GetField("_cachedDataDir",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(dataFile)),".codex-verify/item-mall-fixture"));
  System.IO.Directory.SetCurrentDirectory(RCLibrary.Core.PathHelper.DataDirectory);
  handlerType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC34")).First(t=>t!=null);
  var handlers=(Dictionary<int,Network.ActionCodes.AC>)typeof(Network.ActionCodes.AC).GetField("AcList",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  handlers[34]=(Network.ActionCodes.AC)Activator.CreateInstance(handlerType);
  var p=NewPlayer();Command(p,34,1,1);
  Check(p.UserAcc.IM==968,"Old server deducts 32 for balance query");
  var reply=socket.Packets.Single(b=>b.Length>5&&b[4]==35&&b[5]==4);
  Check(BitConverter.ToUInt32(reply,6)+BitConverter.ToUInt32(reply,10)==0,"Old server sends zero balance to native checkout");
  Check(p.Inv.FilledCount==0,"Old server charged without delivering invalid item");
  transport.Dispose();return "REPRODUCED old runtime: 1000 -> 968 IM on balance query, native checkout balance 0, no item delivered";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[MallChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'))
