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
using DataFiles;
using Network;
using RCLibrary.Core.Networking;
public class RewardSocket : SocketClient {
 public List<byte[]> Packets;
 public override void SendPacket(IPacket p){Packets.Add(p.Buffer.ToArray());}
}
public static class CharacterStuntChecks {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 public static string Run(string root){
  var dat=new PhxItemDat();dat.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult();
  using(var transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
   var socket=(RewardSocket)FormatterServices.GetUninitializedObject(typeof(RewardSocket));socket.Packets=new List<byte[]>();socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();
   foreach(var name in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,true);
   typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(socket,transport);
   var p=new Player(socket,dat);
   uint[][] ids={new uint[]{11075},new uint[]{15041,12036},new uint[]{15038,11076,15039,11182},new uint[]{11078,12053,15040,15060,12051,12049,11077,11183}};
   for(byte body=1;body<=4;body++)for(byte head=0;head<ids[body-1].Length;head++){
    uint id=ids[body-1][head];p.Body=(BodyStyle)body;p.Head=head;p.Eqs.Body=p.Body;p.Eqs.Head=head;p.Eqs.Element=Affinity.Fire;
    SkillManager.InitializePlayerSkillsNoSend(p);
    Check(SkillManager.GetStarterStuntSkill(body,head)==id,"Native Body/Head mapping "+body+"/"+head);
    var stunt=p.PlayerSkills.Single(s=>s.SkillID==id);Check(stunt.Grade==1,"New character starts with own stunt");
    Check(p.PlayerSkills.Any(s=>s.SkillID==11016),"Element skills retained");
    stunt.Grade=3;stunt.Exp=27;
    socket.Packets.Clear();p.Send_5_3();
    var packet=socket.Packets.Single(b=>b[4]==5&&b[5]==3);int count=BitConverter.ToUInt16(packet,66);int found=0;
    for(int n=0;n<count;n++){int pos=68+7*n;if(BitConverter.ToUInt16(packet,pos)!=188)continue;found++;Check(packet[pos+2]==3&&BitConverter.ToUInt32(packet,pos+3)==27,"Native stunt slot restores grade and EXP");}
    Check(found==1,"Exactly one visible character stunt slot (188)");
    socket.Packets.Clear();SkillManager.SendAllSkills(p);
    Check(socket.Packets.Any(b=>b[4]==5&&b[5]==12&&BitConverter.ToUInt16(b,6)==15003&&b[8]==3),"Grade update uses native stunt alias");
    Check(socket.Packets.Any(b=>b[4]==5&&b[5]==11&&BitConverter.ToUInt32(b,6)==15003),"Proficiency update uses native stunt alias");
    Check(p.PlayerSkills.Single(s=>s.SkillID==id).Exp==27,"Packets do not mutate real combat skill");
    socket.Packets.Clear();SkillManager.AddSkillExp(p,id,300);
    Check(stunt.SkillID==id&&stunt.Grade==4&&stunt.Exp==27,"Combat EXP progresses real skill");
    Check(socket.Packets.Any(b=>b[4]==5&&b[5]==12&&BitConverter.ToUInt16(b,6)==15003&&b[8]==4),"Combat grade-up updates visible stunt");
   }
   // Grade and proficiency survive a real database save/load in an isolated fixture.
   var db=new DataBase.CharacterDataBase();
   p.CharID=777777;p.Body=BodyStyle.Big_Female;p.Head=5;p.Eqs.Body=p.Body;p.Eqs.Head=5;
   SkillManager.UnlockSkill(p,12049,6,91);
   SkillManager.InitializePlayerSkillsNoSend(p);
   Check(p.PlayerSkills.Single(s=>s.SkillID==12049).Grade==6&&p.PlayerSkills.Single(s=>s.SkillID==12049).Exp==91,"Saved Fire Dance progress survives login");
   var rows=db.GetDataTable("SELECT skillID,grade,exp FROM character_skills WHERE charID="+p.CharID);
   Check(rows.Rows.Count==1&&Convert.ToUInt32(rows.Rows[0]["skillID"])==12049,"Database retains actual skill ID");
   p.Head=0;db.ExecuteNonQuery("DELETE FROM character_skills WHERE charID="+p.CharID);SkillManager.InitializePlayerSkillsNoSend(p);
   Check(p.PlayerSkills.Any(s=>s.SkillID==11078)&&!p.PlayerSkills.Any(s=>s.SkillID==12049),"Head zero character gets its native stunt");
  }
  return checks+" character stunt mapping, native packet, progression and persistence checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll','System.Xml.dll'))
[CharacterStuntChecks]::Run((Split-Path $PSScriptRoot -Parent))
