param([string]$BuildDirectory=(Join-Path $PSScriptRoot 'inventory-login-bin'),[switch]$ExpectOriginalFailure)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','System.Data.SQLite.dll')|ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
$fixture=Join-Path $PSScriptRoot 'login-regression.db'
$originalHash=(Get-FileHash -LiteralPath $fixture).Hash
$dat=[DataFiles.PhxItemDat]::new()
[void]$dat.Load((Join-Path $repo 'Data/itemDat.wpdat')).GetAwaiter().GetResult()
function New-Database {
 $db=[DataBase.CharacterDataBase]::new();$db.ItemDat=$dat
 [RCLibrary.Core.DataBase].GetField('DBFile',[Reflection.BindingFlags]'NonPublic,Instance').SetValue($db,$fixture)
 return $db
}
if($ExpectOriginalFailure){
 $db=New-Database
 try{[void]$db.GetCharacterData(4510001)}catch{
  if($_.Exception.ToString() -match 'SetBeginnerOutfit'){ 'REPRODUCED original NullReferenceException in SetBeginnerOutfit on character-list load';exit 0 }
  throw
 }
 throw 'Original failure did not reproduce'
}
$code=@"
using System;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using Network;
using Game;
using RCLibrary.Core.Networking;
public class LoginTestSocket : SocketClient { public override void SendPacket(IPacket p) {} }
public static class LoginTestPlayer {
 public static Player Create(DataFiles.PhxItemDat dat) {
  var socket=(LoginTestSocket)FormatterServices.GetUninitializedObject(typeof(LoginTestSocket));
  socket.m_IncomingPackets=new ConcurrentQueue<IPacket>();return new Player(socket,dat);
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll'))
$script:checks=0
function Check($ok,$message){if(!$ok){throw $message};$script:checks++}
$db=New-Database
$characters=$db.GetDataTable('SELECT charID,slot FROM characters ORDER BY charID')
foreach($row in $characters.Rows){
 $id=[uint32]$row.charID
 $expected=$db.GetDataTable("SELECT itemID,pos,dmg FROM inventory WHERE charID=$id AND storID=1 AND itemID<>0")
 $char=$db.GetCharacterData($id)
 Check ($null -ne $char -and $char.CharID -eq $id) "Character list failed for $id"
 Check ($char.WornCount -eq $expected.Rows.Count) "Character list invented/lost equipment for $id"
 $p=[LoginTestPlayer]::Create($dat);$p.Slot=[byte]$row.slot;$p.UserAcc.DataBaseID=[uint32]($id-10000-4500000*([int]$row.slot-1));$p.Tent.IsDirty=$false
 Check ($db.GetCharacterData($id,[ref]$p)) "Cached selection failed for $id"
 Check ($p.Eqs.WornCount -eq $expected.Rows.Count) "Cached selection invented/lost equipment for $id"
 $fresh=New-Database;$q=[LoginTestPlayer]::Create($dat);$q.Slot=[byte]$row.slot;$q.UserAcc.DataBaseID=$p.UserAcc.DataBaseID;$q.Tent.IsDirty=$false
 Check ($fresh.GetCharacterData($id,[ref]$q)) "Uncached selection failed for $id"
 Check ($q.Eqs.WornCount -eq $expected.Rows.Count) "Uncached selection invented/lost equipment for $id"
 foreach($eq in $expected.Rows){foreach($c in @($char,$p,$q)){Check ($c[[byte]$eq.pos].ItemID -eq $eq.itemID -and $c[[byte]$eq.pos].Damage -eq $eq.dmg) "Saved equipment mismatch $id"}}
}
$mizaki=$db.GetCharacterData(4510001)
Check ($mizaki.WornCount -eq 0) 'Mizaki must remain unequipped'
$bag=$db.GetDataTable('SELECT itemID,pos,qty FROM inventory WHERE charID=4510001 AND storID=0 AND itemID IN (21013,24013) ORDER BY itemID')
Check ($bag.Rows.Count -eq 2 -and $bag.Rows[0].pos -eq 11 -and $bag.Rows[1].pos -eq 12) 'Recovered outfit must stay in bag'
$new=[LoginTestPlayer]::Create($dat);$new.Body=$mizaki.Body;$new.Head=$mizaki.Head;$new.SetBeginnerOutfit()
Check ($new[2].ItemID -eq 21013 -and $new[5].ItemID -eq 24013) 'New-character starter outfit must still work'
[System.Data.SQLite.SQLiteConnection]::ClearAllPools()
[GC]::Collect();[GC]::WaitForPendingFinalizers()
Check ((Get-FileHash -LiteralPath $fixture).Hash -eq $originalHash) 'Character loading modified database'
"PASS: $script:checks character-list, cached/uncached selection and new-character outfit checks; fixture DB unchanged."


