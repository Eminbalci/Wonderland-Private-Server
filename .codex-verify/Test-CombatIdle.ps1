param([string]$BuildDirectory)
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','System.Data.SQLite.dll') | ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
$code=@"
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Net.Sockets;
using Game;
using Game.Maps;
using Game.Code;
using Game.DataFiles;
using Game.QuestRelated;
using Game.SkillRelated;
using Game.Battle;
using System.Threading;
using System.Diagnostics;
using DataFiles;
using Network;
using RCLibrary.Core.Networking;
public class IdleSocket : SocketClient {
 public ConcurrentQueue<byte[]> Packets;
 public ManualResetEvent Released;
 public Stopwatch Clock;
 public long ReleaseMs;
 public override void SendPacket(IPacket p){var b=p.Buffer.ToArray();Packets.Enqueue(b);if(b.Length>=6&&b[4]==20&&b[5]==8){ReleaseMs=Clock.ElapsedMilliseconds;Released.Set();}}
}
public static class CombatIdleChecks {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 public static string Run(string root){
  var dat=new PhxItemDat();dat.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult();
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   var socket=(IdleSocket)FormatterServices.GetUninitializedObject(typeof(IdleSocket));socket.Packets=new ConcurrentQueue<byte[]>();socket.Released=new ManualResetEvent(false);socket.Clock=new Stopwatch();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
   foreach(var name in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
   typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
   var p=new Player(socket,dat);var battle=new ActiveBattle();battle.AttackingPlayers.Add(p);
   battle.Attackers.Add(new BattleFighter{PlayerRef=p,OwnerID=p.CharID,FighterType=BattleFighterType.Player,GridX=4,GridY=2,CurHP=100,MaxHP=100});
   var flags=BindingFlags.Static|BindingFlags.NonPublic;
   socket.Clock.Start();typeof(PvEBattleManager).GetMethod("EndBattleVictory",flags).Invoke(null,new object[]{battle});
   Check(socket.Released.WaitOne(3000),"Victory releases movement");
   Check(socket.ReleaseMs<1000,"Victory added idle delay: "+socket.ReleaseMs+"ms (expected no extra 1300ms)");
   var packets=socket.Packets.ToArray();int close=Array.FindIndex(packets,b=>b[4]==11&&b[5]==0);int release=Array.FindIndex(packets,b=>b[4]==20&&b[5]==8);
   Check(close>=0&&release>close,"Close precedes movement release");
   Check(packets.Count(b=>b[4]==11&&b[5]==12)==1,"Victory outcome sent once");
   Check(battle.IsFinished,"Battle marked complete");
   var timing=typeof(PvEBattleManager).GetMethod("GetAnimationDelayMs",flags);
   foreach(var row in new[]{new[]{10001,1300},new[]{0,1300},new[]{11016,3000},new[]{25221,3000}})
    Check((int)timing.Invoke(null,new object[]{(ushort)row[0],"attack"})==row[1],"Shorter action budget "+row[0]);
   Check((int)timing.Invoke(null,new object[]{(ushort)10008,"catch"})==6000,"Capture result/return budget preserved");
   Check((int)timing.Invoke(null,new object[]{(ushort)60041,"flee"})==2500,"Escape return budget preserved");
   return checks+" compiled combat idle checks passed; victory release="+socket.ReleaseMs+"ms";
  }
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll','System.Xml.dll'))
[CombatIdleChecks]::Run((Split-Path $PSScriptRoot -Parent))
