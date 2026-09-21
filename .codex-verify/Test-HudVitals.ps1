param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'hud-vitals-bin'),[switch]$ExpectOld)
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
 static int checks; static PhxItemDat dat; static InventorySocket socket;
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

 static int HP(int level,int con,int bonus){return (int)Math.Round(Math.Pow(level,0.35)*con*2+level+con*2+180)+bonus;}
 static int SP(int level,int wis,int bonus){return (int)Math.Round(Math.Pow(level,0.3)*wis*3.2+level+wis*2+94)+bonus;}
 // Replay the native split:207/208 copy computed max into HUD; inventory reads updated base stats.
 static int[] Replay(Player p,int initialCon,int initialWis){
  int con=initialCon,wis=initialWis,hpBonus=0,spBonus=0,hudHP=-1,hudSP=-1,curHP=-1,curSP=-1;
  foreach(var b in Packets(socket)){
   if(b.Length!=16||b[4]!=8||b[5]!=1)continue;int v=BitConverter.ToInt32(b,8);
   switch(b[6]){
    case 29:con=v;break;case 33:wis=v;break;
    case 207:hpBonus=v;hudHP=HP(p.Level,con,hpBonus);break;
    case 208:spBonus=v;hudSP=SP(p.Level,wis,spBonus);break;
    case 25:curHP=v;break;case 26:curSP=v;break;
   }
  }
  return new[]{hudHP,HP(p.Level,con,hpBonus),hudSP,SP(p.Level,wis,spBonus),curHP,curSP};
 }
 public static string Run(string dataFile,bool old){
  dat=new PhxItemDat();Check(dat.Load(dataFile).GetAwaiter().GetResult(),"Data load");
  var p=NewPlayer();p.Con=0;p.Wis=1;p.SetLevel(7);p.CurHP=187;p.CurSP=109;socket.Packets=new ConcurrentQueue<byte[]>();p.Send8_1();var state=Replay(p,16,1);
  if(old){Check(state[0]==282&&state[1]==187&&state[4]==187,"Reproduce reported HUD187/282 versus inventory187/187");return "REPRODUCED: HUD187/282, inventory187/187 from old packet order.";}
  Check(state[0]==187&&state[1]==187&&state[4]==187,"Reported full HP case fixed in both displays");
  foreach(int con in new[]{0,16,35})foreach(int wis in new[]{1,20})foreach(bool equip in new[]{false,true}){
   p=NewPlayer();p.Con=(ushort)con;p.Wis=(ushort)wis;p.SetLevel(7);if(equip)p.Eqs[3].CopyFrom(new Item(dat.GetItemByID(11100)));p.CurHP=63;p.CurSP=0;
   socket.Packets=new ConcurrentQueue<byte[]>();p.Send8_1();state=Replay(p,con==0?16:0,wis==1?20:1);
   Check(state[0]==p.FullHP&&state[1]==p.FullHP,"HP maxima agree after CON/equipment change");
   Check(state[2]==p.FullSP&&state[3]==p.FullSP,"SP maxima agree after WIS change");
   Check(state[4]==63&&state[5]==0&&p.CurHP==63&&p.CurSP==0,"Synchronization does not heal/deplete actual vitals");
   Check(Packets(socket).Count(b=>b.Length==16&&b[4]==8&&b[5]==1&&b[6]==207)==1,"Single HP max update, no duplicate packets");
   p.Eqs[3].Clear();socket.Packets=new ConcurrentQueue<byte[]>();p.Send8_1();state=Replay(p,con,wis);
   Check(state[0]==p.FullHP&&state[1]==p.FullHP,"Unequip clears HP bonus in HUD and inventory");
  }
  transport.Dispose();return checks+" native HUD/inventory max-vitals replay checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[InventoryChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'),$ExpectOld.IsPresent)
