param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'combat-vitals-bin'),[switch]$ExpectOld)
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
 public ConcurrentQueue<byte[]> Packets;
 public override void SendPacket(IPacket p){Packets.Enqueue(p.Buffer.ToArray());}
}
public static class InventoryChecks {
 static Socket transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
 static int checks; static PhxItemDat dat; static Type handlerType; static InventorySocket socket;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static Player NewPlayer(){
  socket=(InventorySocket)FormatterServices.GetUninitializedObject(typeof(InventorySocket));
  socket.Packets=new ConcurrentQueue<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  foreach(var name in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
  typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
  var p=new Player(socket,dat);p.Eqs.Con=50;p.Eqs.Wis=30;p.Eqs.CurHP=1;p.Eqs.CurSP=1;return p;
 }

 static byte[][] Packets(InventorySocket s){return s.Packets.ToArray();}
 static int Stat(InventorySocket s,byte id){var p=Packets(s).LastOrDefault(b=>b.Length==16&&b[4]==8&&b[5]==1&&b[6]==id);return p==null?-999:BitConverter.ToInt32(p,8);}
 static void Invoke(string name,Game.Battle.ActiveBattle battle){typeof(Game.Battle.PvEBattleManager).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{battle});}
 static void WaitExit(InventorySocket s){Check(System.Threading.SpinWait.SpinUntil(()=>Stat(s,26)!=-999,5000),"Combat exit vitals arrived");}
 static void VerifyExit(Player p,InventorySocket s){
  WaitExit(s);var ps=Packets(s);int map=Array.FindLastIndex(ps,b=>b.Length==7&&b[4]==6&&b[5]==2&&b[6]==0);int hp=Array.FindLastIndex(ps,b=>b.Length==16&&b[4]==8&&b[5]==1&&b[6]==25);
  Check(map>=0&&hp>map,"Vitals synchronized after normal map mode");
  Check(Stat(s,25)==p.CurHP&&Stat(s,26)==p.CurSP,"Returned map HP/SP match server");
 }
 public static string Run(string dataFile,bool old){
  dat=new PhxItemDat();Check(dat.Load(dataFile).GetAwaiter().GetResult(),"Data load");
  var p=NewPlayer();p.Con=0;p.Wis=1;p.SetLevel(7);p.CurHP=63;p.CurSP=109;var s=socket;
  Check(p.FullHP==187&&p.FullSP==109,"Mizaki level 7 maximum vitals fixture");
  p.Send8_1();
  if(old){Check(Stat(s,25)==187&&p.CurHP==63,"Old overworld HP says187 while actual HP63");transport.Dispose();return "REPRODUCED: overworld packet HP187, server/combat HP63.";}
  Check(Stat(s,25)==63&&Stat(s,26)==109,"Mizaki actual current vitals on map");
  foreach(var pair in new[]{new[]{63,109},new[]{187,109},new[]{1,1},new[]{63,0}}){
   p.CurHP=pair[0];p.CurSP=pair[1];s.Packets=new ConcurrentQueue<byte[]>();p.Send8_1();
   Check(Stat(s,25)==pair[0]&&Stat(s,26)==pair[1],"Stat refresh preserves actual HP/SP");
   var b=new Game.Battle.ActiveBattle();b.AttackingPlayers.Add(p);Invoke("InitializeAndStartBattle",b);b.CancelTurnTimer();
   var packet=Packets(s).Single(x=>x.Length>35&&x[4]==11&&x[5]==250);
   Check(BitConverter.ToInt32(packet,22)==187&&BitConverter.ToUInt16(packet,26)==109,"Combat maximum HP/SP");
   Check(BitConverter.ToInt32(packet,28)==pair[0]&&BitConverter.ToUInt16(packet,32)==pair[1],"Combat initial packet matches map current HP/SP");
   Check(b.Attackers[0].CurHP==pair[0]&&b.Attackers[0].CurSP==pair[1]&&p.CurHP==pair[0]&&p.CurSP==pair[1],"Entering combat never subtracts or restores vitals");
  }
  p.CurHP=0;p.CurSP=0;p.Send8_1();Check(p.CurHP==0&&p.CurSP==0&&Stat(s,25)==0&&Stat(s,26)==0,"Zero HP/SP survive reads and synchronization");
  p.Send8_1(true);Check(p.CurHP==187&&p.CurSP==109&&Stat(s,25)==187&&Stat(s,26)==109,"Explicit level-up restoration preserved");
  p.CurHP=0;p.CurSP=0;p.FillHP();p.FillSP();Check(p.CurHP==187&&p.CurSP==109,"Explicit beginner/rest restoration preserved");
  // Drive the real item-use handler and check both packet values and stored values.
  handlerType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC23")).First(t=>t!=null);
  handlerType.Assembly.GetType("System.cGlobal").GetField("ItemDatManager",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,dat);
  var handlers=(Dictionary<int,Network.ActionCodes.AC>)typeof(Network.ActionCodes.AC).GetField("AcList",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);handlers[23]=(Network.ActionCodes.AC)Activator.CreateInstance(handlerType);
  p.CurHP=63;p.Inv.AddItem(32075,2,false);s.Packets=new ConcurrentQueue<byte[]>();p.ProcessSocket((IPacket)new RecievePacket(new byte[]{244,68,6,0,23,15,1,1,0,0}));
  Check(p.CurHP==163&&Stat(s,25)==163&&p.Inv.GetItemCount(32075)==1,"Food restores actual 100 HP and map displays163 instead of187");
  p.CurSP=0;p.Inv.AddItem(32002,2,false);s.Packets=new ConcurrentQueue<byte[]>();p.ProcessSocket((IPacket)new RecievePacket(new byte[]{244,68,6,0,23,15,2,1,0,0}));
  Check(p.CurSP==109&&Stat(s,26)==109&&p.Inv.GetItemCount(32002)==1,"Food works from zero SP without implicit refill");
  foreach(var end in new[]{"EndBattleFlee","EndBattleVictory","EndBattleDefeat"}){
   p=NewPlayer();s=socket;p.CharID=1;p.SetLevel(20);p.CurHP=63;p.CurSP=0;
   var other=NewPlayer();var os=socket;other.CharID=2;other.SetLevel(20);other.CurHP=41;other.CurSP=17;
   var battle=new Game.Battle.ActiveBattle();battle.AttackingPlayers.Add(p);battle.DefendingPlayers.Add(other);Invoke("BuildFighters",battle);
   Invoke(end,battle);VerifyExit(p,s);VerifyExit(other,os);
   Check(p.CurSP==0&&other.CurSP==17,"Exiting combat does not refill consumed SP: "+end);
   if(end=="EndBattleFlee")Check(p.CurHP==63&&other.CurHP==41,"Flee preserves both participants current HP");
   if(end=="EndBattleVictory")Check(p.CurHP==63&&other.CurHP==Math.Max(10,other.FullHP/2),"Victory preserves winner HP and intended loser recovery");
   if(end=="EndBattleDefeat")Check(p.CurHP==Math.Max(10,p.FullHP/2)&&other.CurHP==41,"Defeat preserves intended recovery and defender HP");
  }
  transport.Dispose();return checks+" combat/map vitals checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[InventoryChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'),$ExpectOld.IsPresent)
