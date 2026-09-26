param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'login-gold-bin'),[switch]$ExpectOld)
$ErrorActionPreference='Stop'
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll')|ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
$code=@"
using System;
using System.Linq;
using Game;
public static class LoginGoldChecks {
 static int checks;
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;}
 public static string Run(bool old){
  var c=new Character();c.Slot=2;c.CharName="Mizaki";c.TotalExp=12345;c.SetGold(448);
  var record=c.ToArray().ToArray();
  if(old){Check(BitConverter.ToUInt32(record,24+record[1])==0,"Old selection must reproduce Gold 0");Check(c.Gold==448,"Real gold remains 448");return "REPRODUCED: saved Gold 448 serializes as 0 in native login gold field.";}
  foreach(var name in new[]{"A","Mizaki","CharacterLong"})foreach(var slot in new byte[]{1,2})foreach(var gold in new[]{0,448,464,999999}){
   c.CharName=name;c.Slot=slot;c.SetGold(gold);record=c.ToArray().ToArray();int n=record[1];
   Check(record[0]==slot&&n==name.Length,"Slot and name preserved");
   Check(record.Length==54+n,"Native record boundary unchanged");
   Check(BitConverter.ToUInt32(record,n+20)==12345,"EXP remains in native EXP DWORD");
   Check(BitConverter.ToUInt32(record,n+24)==gold,"Gold in native Gold DWORD");
   Check(c.Gold==gold&&c.TotalExp==12345,"Serialization never mutates currency or EXP");
  }
  var first=new Character();first.Slot=1;first.CharName="First";first.TotalExp=98;first.SetGold(17);
  c.Slot=2;c.CharName="Mizaki";c.SetGold(448);var data=first.ToArray().Concat(c.ToArray()).ToArray();int pos=0;
  foreach(var expected in new[]{17,448}){Check(BitConverter.ToUInt32(data,pos+24+data[pos+1])==expected,"Two-slot parser reads correct character gold");pos+=54+data[pos+1];}
  Check(pos==data.Length,"Two native records consume exact packet length");
  return checks+" login gold protocol checks passed";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll'))
[LoginGoldChecks]::Run($ExpectOld.IsPresent)
