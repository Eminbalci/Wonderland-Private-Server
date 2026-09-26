param([string]$BuildDirectory)
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','System.Data.SQLite.dll') | ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'Wonderland Private Server.exe'))
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
using DataFiles;
using Network;
using RCLibrary.Core.Networking;
public class RewardSocket : SocketClient {
 public List<byte[]> Packets;
 public override void SendPacket(IPacket p){Packets.Add(p.Buffer.ToArray());}
}
public static class QuietSkillLoginChecks {
 static int checks;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 public static string Run(string root){
  var asm=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Wonderland Private Server");
  var method=asm.GetTypes().Single(t=>t.Name=="WorldServer").GetMethod("CommenceLogin");
  var il=method.GetMethodBody().GetILAsByteArray();
  var ops=typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(System.Reflection.Emit.OpCode)).Select(f=>(System.Reflection.Emit.OpCode)f.GetValue(null)).ToDictionary(o=>unchecked((ushort)o.Value));
  var calls=new List<string>();
  for(int pos=0;pos<il.Length;){
   ushort value=il[pos++];if(value==0xfe)value=(ushort)(0xfe00|il[pos++]);var op=ops[value];
   if(op.OperandType==System.Reflection.Emit.OperandType.InlineMethod){var call=method.Module.ResolveMethod(BitConverter.ToInt32(il,pos));calls.Add(call.DeclaringType.Name+"."+call.Name);}
   switch(op.OperandType){
    case System.Reflection.Emit.OperandType.InlineNone:break;
    case System.Reflection.Emit.OperandType.ShortInlineI:case System.Reflection.Emit.OperandType.ShortInlineBrTarget:case System.Reflection.Emit.OperandType.ShortInlineVar:pos++;break;
    case System.Reflection.Emit.OperandType.InlineVar:pos+=2;break;
    case System.Reflection.Emit.OperandType.InlineI8:case System.Reflection.Emit.OperandType.InlineR:pos+=8;break;
    case System.Reflection.Emit.OperandType.InlineSwitch:pos+=4+4*BitConverter.ToInt32(il,pos);break;
    default:pos+=4;break;
   }
  }
  Check(!calls.Contains("SkillManager.SendAllSkills"),"Login replays learned/grade packets and sounds");
  Check(calls.Contains("SkillManager.InitializePlayerSkillsNoSend"),"Login restores saved skills silently");
  Check(calls.Count(c=>c=="Character.Send_5_3")==1,"Login sends one full skill snapshot");
  Check(calls.IndexOf("SkillManager.InitializePlayerSkillsNoSend")<calls.IndexOf("Character.Send_5_3"),"Skills loaded before snapshot");
  var dat=new PhxItemDat();dat.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult();
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   var socket=(RewardSocket)FormatterServices.GetUninitializedObject(typeof(RewardSocket));socket.Packets=new List<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
   foreach(var name in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
   typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
   var p=new Player(socket,dat);p.Body=BodyStyle.Big_Female;p.Head=5;p.Eqs.Element=Affinity.Fire;
   socket.Packets.Clear();SkillManager.InitializePlayerSkillsNoSend(p);
   Check(socket.Packets.Count==0,"Restoring existing skills emits no notifications");
   p.PlayerSkills.Single(s=>s.SkillID==12049).Grade=6;p.PlayerSkills.Single(s=>s.SkillID==12049).Exp=91;
   p.Send_5_3();p.Send(Tools.FromFormat("bb",5,4));
   Check(socket.Packets.Count==2&&socket.Packets[1][4]==5&&socket.Packets[1][5]==4,"Login refresh adds no learn/grade/proficiency notifications");
   var b=socket.Packets[0];int count=BitConverter.ToUInt16(b,66);var levels=new Dictionary<ushort,Tuple<byte,uint>>();
   for(int n=0;n<count;n++){int pos=68+n*7;levels[BitConverter.ToUInt16(b,pos)]=Tuple.Create(b[pos+2],BitConverter.ToUInt32(b,pos+3));}
   Check(levels[188].Item1==6&&levels[188].Item2==91,"Silent snapshot preserves Fire Dance grade and EXP");
   foreach(var id in new ushort[]{11016,11166,11056})Check(levels[SkillManager.GetSkill(id).SkillTableOrder].Item1==1,"Element skill remains learned: "+id);
   socket.Packets.Clear();p.Eqs.Int=15;p.Eqs.Wis=2;SkillManager.CheckAndUnlockProgressionSkills(p);
   Check(p.PlayerSkills.Any(s=>s.SkillID==11005),"New stat-qualified skill still unlocks");
   Check(socket.Packets.Any(x=>x[4]==8&&x[5]==1&&x[6]==110),"Genuine unlock still sends learned notification");
  }
  return checks+" compiled quiet skill login checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll','System.Xml.dll'))
[QuietSkillLoginChecks]::Run((Split-Path $PSScriptRoot -Parent))
