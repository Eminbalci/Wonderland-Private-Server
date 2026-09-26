param([Parameter(Mandatory=$true)][string]$BuildDirectory,[switch]$ExpectOld)
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','Wonderland Private Server.exe') | ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
$code=@"
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
public class PointSocket:SocketClient {
 public List<byte[]> Packets=new List<byte[]>();
 public override void SendPacket(IPacket p){Packets.Add(p.Buffer.ToArray());}
}
public static class PointHpChecks {
 static int checks;static PhxItemDat dat;static PointSocket socket;
 static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
 static Player New(Socket transport){
  socket=(PointSocket)FormatterServices.GetUninitializedObject(typeof(PointSocket));socket.Packets=new List<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  foreach(var n in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
  typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
  var p=new Player(socket,dat);p.UserAcc.GMlvl=1;p.SetLevel(11);p.Str=101;p.Con=1;p.Wis=0;p.Int=0;p.Agi=0;p.SkillPoints=9937;p.CurHP=130;p.CurSP=74;socket.Packets.Clear();return p;
 }
 static int Stat(byte id){var b=socket.Packets.LastOrDefault(x=>x.Length==16&&x[4]==8&&x[5]==1&&x[6]==id);return b==null?-1:BitConverter.ToInt32(b,8);}
 static byte[] native;
 static int Offset(int address){
  int pe=BitConverter.ToInt32(native,60),opt=pe+24,rva=address-BitConverter.ToInt32(native,opt+28),sections=opt+BitConverter.ToUInt16(native,pe+20);
  for(int i=0;i<BitConverter.ToUInt16(native,pe+6);i++){int sec=sections+i*40,va=BitConverter.ToInt32(native,sec+12),size=Math.Max(BitConverter.ToInt32(native,sec+8),BitConverter.ToInt32(native,sec+16));if(rva>=va&&rva<va+size)return BitConverter.ToInt32(native,sec+20)+rva-va;}throw new Exception("Native address unmapped");
 }
 static void Bytes(int address,params byte[] expected){Check(native.Skip(Offset(address)).Take(expected.Length).SequenceEqual(expected),"Native evidence changed at "+address.ToString("x"));}
 static int[] Replay(Character p,IEnumerable<byte[]> packets){
  int con=0,wis=0,hpExtra=0,spExtra=0,hp=0,sp=0,curHP=0,curSP=0;
  foreach(var b in packets){
   if(b.Length>68&&b[4]==5&&b[5]==3){int trailer=68+7*BitConverter.ToUInt16(b,66);hpExtra=BitConverter.ToUInt16(b,trailer);spExtra=BitConverter.ToUInt16(b,trailer+2);}
   if(b.Length!=16||b[4]!=8||b[5]!=1)continue;int v=BitConverter.ToInt32(b,8);
   switch(b[6]){case 29:con=v;break;case 33:wis=v;break;
    case 207:hp=(int)Math.Round(Math.Pow(p.Level,.35)*con*2+p.Level+con*2+180)+v+hpExtra;break;
    case 208:sp=(int)Math.Round(Math.Pow(p.Level,.3)*wis*3.2+p.Level+wis*2+94)+v+spExtra;break;
    case 25:curHP=v;break;case 26:curSP=v;break;}
  }
  return new[]{hp,sp,curHP,curSP,hpExtra,spExtra};
 }
 static List<MethodBase> Calls(MethodInfo method){
  var result=new List<MethodBase>();var il=method.GetMethodBody().GetILAsByteArray();
  var ops=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>unchecked((ushort)o.Value));
  for(int pos=0;pos<il.Length;){ushort value=il[pos++];if(value==0xfe)value=(ushort)(0xfe00|il[pos++]);var op=ops[value];if(op.OperandType==OperandType.InlineMethod)result.Add(method.Module.ResolveMethod(BitConverter.ToInt32(il,pos)));
   switch(op.OperandType){case OperandType.InlineNone:break;case OperandType.ShortInlineI:case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineVar:pos++;break;case OperandType.InlineVar:pos+=2;break;case OperandType.InlineI8:case OperandType.InlineR:pos+=8;break;case OperandType.InlineSwitch:pos+=4+4*BitConverter.ToInt32(il,pos);break;default:pos+=4;break;}
  }return result;
 }
 static void Chat(Player p,string text){
  socket.Packets.Clear();var data=System.Text.Encoding.ASCII.GetBytes(text);var bytes=new byte[data.Length+6];bytes[0]=244;bytes[1]=68;bytes[2]=(byte)(data.Length+2);bytes[4]=2;bytes[5]=2;Array.Copy(data,0,bytes,6,data.Length);
  var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Network.ActionCodes.AC02")).First(t=>t!=null);var packet=new RecievePacket(bytes);packet.SetPtr(5);type.GetMethod("ProcessPkt").Invoke(Activator.CreateInstance(type),new object[]{p,packet});
 }
 public static string Run(string root,bool old){
  native=System.IO.File.ReadAllBytes(@"D:\Game Private\WLRI\aLogin.exe");
  // Installed client's login parser writes the two post-skill WORDs to bonus fields.
  Bytes(0x4385b6,0x66,0x89,0x83,0x94,0x1f,0,0);Bytes(0x4385db,0x66,0x89,0x83,0x96,0x1f,0,0);
  Bytes(0x417593,0x66,0x8b,0x83,0x94,0x1f,0,0);Bytes(0x36e6eb,0x0f,0xb7,0x55,0x0c,0x03,0xc2);
  dat=new PhxItemDat();Check(dat.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult(),"Item data loaded");
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   var p=New(transport);p.Send_5_3();p.Send8_1(false);var state=Replay(p,socket.Packets);
   if(old){Check(p.FullHP==198&&state[0]==10135&&state[2]==130&&state[4]==9937,"Reproduce screenshot: HP130/10135, POINT9937, real max198");return "REPRODUCED installed build: POINT9937 is interpreted as HP bonus; native field replay gives HP130/10135 while server max=198.";}
   Check(state[0]==198&&state[2]==130,"Reported HP130/10135 corrected to130/198");
   foreach(int count in new[]{0,1,4,12})foreach(ushort points in new ushort[]{0,1,9937,65535})foreach(byte potential in new byte[]{0,17}){
    p=New(transport);p.SkillPoints=points;p.Potential=potential;
    for(int n=0;n<count;n++)p.PlayerSkills.Add(new PlayerSkill{SkillID=11016,Grade=3,Exp=(uint)n});
    socket.Packets.Clear();p.Send_5_3();p.Send8_1(false);state=Replay(p,socket.Packets);
    var snapshot=socket.Packets.Single(b=>b[4]==5&&b[5]==3);Check(BitConverter.ToUInt16(snapshot,66)==count,"Skill count preserved");
    Check(snapshot.Length==76+7*count,"Packet size preserved");
    Check(state[4]==0&&state[5]==0,"POINT/Potential never become HP/SP bonus");
    Check(state[0]==p.FullHP&&state[1]==p.FullSP,"Login maxima match authoritative server");
    Check(state[2]==130&&state[3]==74&&p.CurHP==130&&p.CurSP==74,"Login synchronization preserves wounded vitals");
    Check(Stat(38)==points&&Stat(37)==potential,"POINT/Potential synchronized with separate stat IDs");
   }
   var packets=new List<byte[]>();var c=new Character(pkt=>packets.Add(pkt.Buffer.ToArray()),dat);c.SetLevel(11);c.Con=1;c.Wis=0;c.SkillPoints=9937;c.Potential=17;c.CurHP=130;c.CurSP=74;packets.Clear();c.Send_5_3();c.Send8_1(false);state=Replay(c,packets);
   Check(state[0]==c.FullHP&&state[1]==c.FullSP&&state[4]==0&&state[5]==0,"Base Character snapshot uses same corrected bonus fields");
   foreach(var command in new[]{":points",":sp",":statpoint",":statpoints"})foreach(var entry in new[]{new[]{0,10000},new[]{65530,20},new[]{65535,65535},new[]{13,0}}){
    p=New(transport);p.SkillPoints=(ushort)entry[0];Chat(p,command+" "+entry[1]);int expected=Math.Min(65535,entry[0]+entry[1]);
    Check(p.SkillPoints==expected&&Stat(38)==expected,"Cheat updates POINT without overflow: "+command);
    Check(p.CurHP==130&&p.CurSP==74&&p.FullHP==198&&p.FullSP==105,"Cheat preserves HP/SP and maxima");
    Check(socket.Packets.Count(b=>b.Length==16&&b[4]==8&&b[5]==1)==1,"Cheat only sends POINT stat");
    Check(!socket.Packets.Any(b=>b.Length>5&&b[4]==5&&b[5]==3),"Cheat does not send login snapshot");
   }
   p=New(transport);p.UserAcc.GMlvl=0;p.SkillPoints=13;Chat(p,":points 100");Check(p.SkillPoints==13&&Stat(38)==-1,"Non-GM cannot grant points");
   var asm=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Wonderland Private Server");
   var gui=asm.GetTypes().Select(t=>t.GetMethod("btnGiveStatPoints_Click",BindingFlags.Instance|BindingFlags.NonPublic)).First(m=>m!=null);var calls=Calls(gui);
   Check(!calls.Any(m=>m.Name=="Send_5_3"||m.Name=="Send8_1"),"Compiled GUI grant cannot reload or heal player");
   Check(calls.Any(m=>m.Name=="SendStat")&&calls.Any(m=>m.Name=="WritePlayer"),"Compiled GUI synchronizes POINT and retains save");
   Check(calls.Any(m=>m.DeclaringType==typeof(Math)&&m.Name=="Min"),"GUI caps grant before narrowing to ushort");
  }
  return checks+" cheat point, login trailer, native field replay and boundary checks passed (no live UI execution).";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies (@($refs | Where-Object {$_ -notlike '*.exe'})+@('System.Core.dll'))
[PointHpChecks]::Run((Split-Path $PSScriptRoot -Parent),$ExpectOld.IsPresent)
