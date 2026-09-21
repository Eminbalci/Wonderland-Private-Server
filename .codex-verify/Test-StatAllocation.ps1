param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'stat-allocation-bin'),[switch]$ExpectOld)
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
using Game.SkillRelated;
using Network;
using RCLibrary.Core.Networking;
public class StatSocket : SocketClient {
 public List<byte[]> Packets;
 public override void SendPacket(IPacket p){Packets.Add(p.Buffer.ToArray());}
}
public static class StatChecks {
 static Socket transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
 static int checks; static PhxItemDat dat; static StatSocket socket;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static Player NewPlayer(){
  socket=(StatSocket)FormatterServices.GetUninitializedObject(typeof(StatSocket));
  socket.Packets=new List<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  foreach(var name in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
  typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
  var p=new Player(socket,dat);p.Element=Affinity.Fire;p.SetLevel(9);p.Str=0;p.Con=0;p.Int=16;p.Wis=1;p.Agi=0;p.SkillPoints=3;p.CurHP=100;p.CurSP=50;
  SkillManager.CheckAndUnlockProgressionSkillsNoSend(p);socket.Packets.Clear();return p;
 }
 static void Command(Player p,params byte[] data){
  socket.Packets.Clear();var bytes=new byte[data.Length+6];bytes[0]=244;bytes[1]=68;bytes[2]=(byte)(data.Length+2);bytes[4]=8;bytes[5]=1;Array.Copy(data,0,bytes,6,data.Length);
  var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC08")).First(t=>t!=null);
  type.GetMethod("ProcessPkt").Invoke(Activator.CreateInstance(type),new object[]{p,new RecievePacket(bytes)});
 }
 static void Allocate(Player p,byte stat,uint amount){var bytes=new List<byte>{0,1,stat};bytes.AddRange(BitConverter.GetBytes(amount));Command(p,bytes.ToArray());}
 static int Stat(byte id){var b=socket.Packets.LastOrDefault(x=>x.Length==16&&x[4]==8&&x[5]==1&&x[6]==id);return b==null?-999:BitConverter.ToInt32(b,8);}
 static void Synced(Player p,int hp,int sp){
  Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==5&&b[5]==3),"No login snapshot overwrites stat updates");
  Check(Stat(38)==p.SkillPoints,"POINT matches server");
  foreach(var v in new[]{new[]{28,(int)p.Str},new[]{29,(int)p.Con},new[]{27,(int)p.Int},new[]{33,(int)p.Wis},new[]{30,(int)p.Agi}})Check(Stat((byte)v[0])==v[1],"Base stat "+v[0]+" matches server");
  Check(p.CurHP==hp&&p.CurSP==sp&&Stat(25)==hp&&Stat(26)==sp,"Allocation preserves current HP/SP");
  int con=16,wis=0,hudHP=0,hudSP=0;
  foreach(var b in socket.Packets){if(b.Length!=16||b[4]!=8||b[5]!=1)continue;int value=BitConverter.ToInt32(b,8);
   switch(b[6]){case 29:con=value;break;case 33:wis=value;break;
    case 207:hudHP=(int)Math.Round(Math.Pow(p.Level,.35)*con*2+p.Level+con*2+180)+value;break;
    case 208:hudSP=(int)Math.Round(Math.Pow(p.Level,.3)*wis*3.2+p.Level+wis*2+94)+value;break;}
  }
  Check(hudHP==p.FullHP&&hudSP==p.FullSP,"Native HUD max replay agrees with inventory/server");
 }
 public static string Run(string dataFile,bool old){
  dat=new PhxItemDat();Check(dat.Load(dataFile).GetAwaiter().GetResult(),"Item data loaded");
  var p=NewPlayer();p.CurHP=189;p.CurSP=109;Allocate(p,33,3);
  Check(p.Wis==4&&p.Int==16&&p.Con==0&&p.Agi==0&&p.SkillPoints==0,"Mizaki request updates correct stat and spends exactly three points");
  Check(p.PlayerSkills.Any(s=>s.SkillID==11005),"WIS allocation unlocks Fire Blast");
  if(old){
   Check(p.CurSP==136,"Old path refills SP to136");
   int snapshot=socket.Packets.FindLastIndex(b=>b.Length>5&&b[4]==5&&b[5]==3);
   int stat=socket.Packets.FindLastIndex(b=>b.Length==16&&b[4]==8&&b[5]==1);
   Check(snapshot>stat,"Unlock sends login snapshot AFTER correct stat synchronization");
   return "REPRODUCED: correct server POINT=0/WIS=4; SP refilled to136; later AC5:3 overwrites client state.";
  }
  Synced(p,189,109);
  Check(socket.Packets.Any(b=>b.Length==9&&b[4]==5&&b[5]==12&&BitConverter.ToUInt16(b,6)==11005),"New skill sent incrementally");
  int count=p.PlayerSkills.Count;socket.Packets.Clear();SkillManager.CheckAndUnlockProgressionSkills(p);
  Check(p.PlayerSkills.Count==count&&!socket.Packets.Any(b=>b.Length>5&&b[4]==5&&b[5]==12),"Refresh does not duplicate learned skill");
  foreach(byte id in new byte[]{28,29,27,33,30})foreach(uint amount in new uint[]{1,3}){
   p=NewPlayer();Allocate(p,id,amount);Check(p.SkillPoints==3-amount,"Exact point cost");Synced(p,100,50);
   Check(p.Str==(id==28?amount:0)&&p.Con==(id==29?amount:0)&&p.Int==16+(id==27?amount:0)&&p.Wis==1+(id==33?amount:0)&&p.Agi==(id==30?amount:0),"Only requested stat changes");
  }
  p=NewPlayer();p.SkillPoints=10;Command(p,0,5,28,1,0,0,0,29,2,0,0,0,27,3,0,0,0,33,1,0,0,0,30,3,0,0,0);
  Check(p.Str==1&&p.Con==2&&p.Int==19&&p.Wis==2&&p.Agi==3&&p.SkillPoints==0,"Native multi-stat batch applied exactly once");Synced(p,100,50);
  p=NewPlayer();Allocate(p,33,4);Check(p.SkillPoints==3&&p.Wis==1&&p.CurHP==100&&p.CurSP==50,"Insufficient points preserve stats and vitals");
  p=NewPlayer();p.Wis=4;socket.Packets.Clear();SkillManager.CheckAndUnlockProgressionSkills(p);
  Check(p.PlayerSkills.Any(s=>s.SkillID==11005)&&!socket.Packets.Any(b=>b.Length>5&&b[4]==5&&b[5]==3),"Standalone progression unlock also avoids login snapshot");
  transport.Dispose();return checks+" stat allocation/progression/native HUD checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[StatChecks]::Run((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat'),$ExpectOld.IsPresent)
