param([Parameter(Mandatory=$true)][string]$BuildDirectory,[switch]$ExpectOld)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path.Replace('\','/')
$refs=@('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll','System.Data.SQLite.dll') | ForEach-Object {Join-Path $BuildDirectory $_}
foreach($a in $refs){[void][Reflection.Assembly]::LoadFrom($a)}
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'Wonderland Private Server.exe'))
[RCLibrary.Core.DataBase]::LoadGlobalConfig()
[RCLibrary.Core.DataBase]::DefaultServType=[RCLibrary.Core.DataBaseTypes]::Sqlite
$db=Join-Path $PSScriptRoot ('fixture-'+[Guid]::NewGuid().ToString('N')+'.db')
$connection=New-Object System.Data.SQLite.SQLiteConnection ('Data Source='+$db+';Version=3;')
$connection.Open()
$command=$connection.CreateCommand()
$command.CommandText="CREATE TABLE monster_drops(id INTEGER PRIMARY KEY,monster_tid INTEGER,monster_pattern TEXT,item_id INTEGER,item_name TEXT,min_count INTEGER,max_count INTEGER,drop_rate REAL); INSERT INTO monster_drops VALUES(1,11012,NULL,30002,'Pork Meat',1,1,35); INSERT INTO monster_drops VALUES(2,17799,NULL,30004,'Tiger Meat',1,1,35); INSERT INTO monster_drops VALUES(3,17001,NULL,30201,'Small HP Potion',1,1,35); INSERT INTO monster_drops VALUES(4,17797,NULL,41093,'Native snake loot',1,1,18);"
[void]$command.ExecuteNonQuery()
$command.Dispose()
$connection.Dispose()
[RCLibrary.Core.DataBase]::DefaultDBFile=$db
[ RCLibrary.Core.PathHelper ].GetField('_cachedDataDir',[Reflection.BindingFlags]'Static,NonPublic').SetValue($null,"$root/Data")
$code=@"
using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using System.Collections.Concurrent;using System.Runtime.Serialization;using System.Net.Sockets;using System.Threading.Tasks;
using DataFiles;using Game;using Game.Code;using Game.Battle;using Network;using RCLibrary.Core.Networking;
public class DropSocket:SocketClient {
 public List<byte[]> Packets=new List<byte[]>();
 public override void SendPacket(IPacket p){lock(Packets){var b=p.Buffer;for(int o=0;o+4<=b.Length;){int n=BitConverter.ToUInt16(b,o+2);Packets.Add(b.Skip(o+4).Take(n).ToArray());o+=n+4;}}}
}
public class DropMap:GameMap {
 public DropMap(){m_mapid=11185;}
 public void Info(Player p){SendMapInfo(p);}
 public int DropCount {get {var x=typeof(GameMap).GetField("ItemsDropped",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(this);return (int)x.GetType().GetProperty("Count").GetValue(x,null);}}
 public void Drain(){var q=(Queue<Task>)typeof(GameMap).GetField("QueuedTasks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(this);while(q.Count>0){var t=q.Dequeue();t.RunSynchronously();if(t.IsFaulted)throw t.Exception;}}
}
public static class DropChecks {
 static int checks;static PhxItemDat dat;static Socket transport=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);static uint nextId=990100;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static Player Make(DropMap map,out DropSocket s){
  s=(DropSocket)FormatterServices.GetUninitializedObject(typeof(DropSocket));s.Packets=new List<byte[]>();s.m_IncomingPackets=new ConcurrentQueue<IPacket>();
  foreach(var n in new[]{"is_recieving","is_sending"})typeof(Client3).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,true);
  typeof(Client3).GetField("m_Socket",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,transport);
  var p=new Player(s,dat);p.Slot=1;p.UserAcc.DataBaseID=nextId++;p.CharName="Drop fixture";p.CurMap=map;p.CurX=300;p.CurY=300;map.PlayersList.Add(p);return p;
 }
 static void Add(Player p,ushort id,byte slot,byte amount){var i=new Item(dat.GetItemByID(id));i.Ammt=amount;Check(p.Inv.AddItem(i,slot,false)==amount,"fixture insert "+id);}
 static void Input(Player p,byte sub,params byte[] data){var bytes=new byte[data.Length+6];bytes[0]=244;bytes[1]=68;bytes[2]=(byte)(data.Length+2);bytes[4]=23;bytes[5]=sub;Array.Copy(data,0,bytes,6,data.Length);p.ProcessSocket(new RecievePacket(bytes));}
 static bool Is(byte[] p,int ac,int sub){return p.Length>=2&&p[0]==ac&&p[1]==sub;}
 static List<int> Slots(DropSocket socket){var result=new List<int>();foreach(var p in socket.Packets.Where(x=>Is(x,23,4))){Check((p.Length-2)%15==0,"Native ground records have 15 bytes");for(int o=2;o<p.Length;o+=15){Check(p[o]==3,"Ground entity type");result.Add(BitConverter.ToUInt16(p,o+1));}}return result;}
 static Dictionary<uint,HashSet<ushort>> Native(string root){var b=File.ReadAllBytes(root+"/Data/Npc.dat");var d=new Dictionary<uint,HashSet<ushort>>();for(int o=138;o+138<=b.Length;o+=138){uint id=(ushort)((BitConverter.ToUInt16(b,o+12)^0x5209)-1);if(id==0)continue;var ids=new HashSet<ushort>();for(int k=0;k<5;k++){ushort i=(ushort)((BitConverter.ToUInt16(b,o+64+k*2)^0x5209)-1);if(i>0&&i<65000)ids.Add(i);}d[id]=ids;}return d;}
 public static string Run(string root,bool old){
  dat=new PhxItemDat();Check(dat.Load(root+"/Data/itemDat.wpdat").GetAwaiter().GetResult(),"item data");
  var host=AppDomain.CurrentDomain.GetAssemblies().First(asm=>asm.GetType("Network.ActionCodes.AC23")!=null);host.GetType("System.cGlobal").GetField("ItemDatManager",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,dat);
  MonsterDropManager.ItemNameResolver=id=>{var x=dat.GetItemByID(id);return x==null?null:System.Text.Encoding.ASCII.GetString(x.ItemName).Trim('\0');};
  if(old){bool money=false;for(int i=0;i<500;i++)money|=MonsterDropManager.RollDrops(17799,"Boss",100).Any(x=>x.ItemID==30004);Check(money,"old fallback awards quest money");var m=new DropMap();DropSocket s;var p=Make(m,out s);Add(p,46041,1,1);Input(p,3,1,1,1);bool fault=false;try{m.Drain();}catch(AggregateException e){fault=e.InnerExceptions.Any(x=>x is ArgumentOutOfRangeException);}Check(fault,"old map drop crashes");return "REPRODUCED: quest money fallback and ground-drop index crash.";}
  var native=Native(root);int verified=0;
  foreach(var pair in MonsterDropManager.MonsterLootTables){Check(native.ContainsKey(pair.Key),"native monster ID "+pair.Key);foreach(var e in pair.Value){Check(native[pair.Key].Contains(e.ItemID),"native loot ID "+pair.Key+":"+e.ItemID);verified++;}}
  Check(verified>2000,"genuine loot retained");
  Check(MonsterDropManager.LevelBracketLootTables.Count==0&&MonsterDropManager.PatternLootTables.Count==0,"guessed fallbacks removed");
  foreach(uint tid in new uint[]{11012,11066,17799,65500})for(int i=0;i<200;i++)Check(MonsterDropManager.RollDrops(tid,"wolf tiger boar",100).Count==0,"no fabricated voucher or quest drops "+tid);
  var table=MonsterDropManager.MonsterLootTables[17797];var save=new List<MonsterDropEntry>(table);table.Clear();table.Add(new MonsterDropEntry(30002,"Voucher",1,1,100));
  for(int i=0;i<200;i++)Check(MonsterDropManager.RollDrops(17797,"Snake",99).Count==0,"runtime edit cannot bypass native IDs");
  table.Clear();table.Add(new MonsterDropEntry(41093,"Name ignored",1,1,0));for(int i=0;i<200;i++)Check(MonsterDropManager.RollDrops(17797,"Snake",99).Count==0,"zero rate stays disabled");
  table[0].DropRatePercent=100;int granted=0;for(int i=0;i<200;i++){var drops=MonsterDropManager.RollDrops(17797,"Snake",99);foreach(var d in drops){Check(d.ItemID==41093&&d.ItemName==MonsterDropManager.ResolveItemName(41093),"real name and item");granted++;}}Check(granted>0,"valid native drops still awarded");table.Clear();table.AddRange(save);
  MonsterDropManager.LoadFromDatabase();Check(MonsterDropManager.MonsterLootTables[11012].Count==0,"reload does not reintroduce fake seed");
  var map=new DropMap();DropSocket a,b;var p1=Make(map,out a);var p2=Make(map,out b);Add(p1,46041,1,3);a.Packets.Clear();b.Packets.Clear();Input(p1,3,1,2,1);
  Check(p1.Inv[1].Ammt==1&&map.DropCount==2,"drop transfers exact amount");var slots=Slots(a);Check(slots.Count==2&&slots.Distinct().Count()==2,"distinct ground slots");Check(Slots(b).SequenceEqual(slots),"peer sees same ground items");Check(a.Packets.Count(x=>Is(x,23,9))==1,"one inventory decrement");
  a.Packets.Clear();b.Packets.Clear();Input(p2,2,(byte)slots[0]);Check(p2.Inv.GetItemCount(46041)==1&&map.DropCount==1,"pickup belongs to actual picker");Check(a.Packets.Any(x=>Is(x,23,2)&&x[4]==0)&&b.Packets.Any(x=>Is(x,23,2)&&x[4]==1),"pickup result and peer removal: ids="+p1.CharID+","+p2.CharID+" A="+string.Join(";",a.Packets.Select(x=>BitConverter.ToString(x)))+" B="+string.Join(";",b.Packets.Select(x=>BitConverter.ToString(x))));Input(p2,2,(byte)slots[0]);Check(p2.Inv.GetItemCount(46041)==1,"duplicate pickup cannot duplicate item");
  p2.CurX=1000;Input(p2,2,(byte)slots[1]);Check(map.DropCount==1,"distant pickup denied");p2.CurX=300;
  a.Packets.Clear();map.Info(p1);Check(Slots(a).SequenceEqual(new[]{slots[1]}),"map reload contains remaining dropped item");
  Input(p2,2,(byte)slots[1]);Check(map.DropCount==0&&p2.Inv.GetItemCount(46041)==2,"remaining pickup");
  Input(p1,3,1,2,1);Input(p1,3,1,0,1);Input(p1,3,0,1,1);Input(p1,3,51,1,1);Check(map.DropCount==0&&p1.Inv[1].Ammt==1,"invalid quantities and slots preserve bag");
  p1.Inv[1].isLocked=true;Input(p1,3,1,1,1);Check(map.DropCount==0,"locked item cannot drop");p1.Inv[1].isLocked=false;
  Add(p1,30004,2,1);a.Packets.Clear();Input(p1,3,2,1,1);Check(map.DropCount==0&&p1.Inv[2].Ammt==1,"quest item waits for destroy confirmation");Check(a.Packets.Any(x=>Is(x,23,212)),"native destroy prompt preserved");
  Input(p1,124,2,1,2);Check(p1.Inv[2].ItemID==0,"native confirmation can discard quest item");
  // Full bag retains the ground record, then a free slot allows one pickup.
  var fullMap=new DropMap();DropSocket fs,ds;var full=Make(fullMap,out fs);var donor=Make(fullMap,out ds);for(byte i=1;i<=50;i++)Add(full,34014,i,50);Add(donor,46041,1,1);ds.Packets.Clear();Input(donor,3,1,1,1);byte ground=(byte)Slots(ds).Single();Input(full,2,ground);Check(fullMap.DropCount==1&&full.Inv.GetItemCount(46041)==0,"full bag preserves ground item");full.Inv.RemoveItem((byte)50,(byte)50,false);Input(full,2,ground);Check(fullMap.DropCount==0&&full.Inv.GetItemCount(46041)==1,"pickup after room is available");
  // Native IDs, click aliases and pending respawns cannot collide with drops.
  var nm=new DropMap();DropSocket ns;var np=Make(nm,out ns);nm.GroundItemList.Add(new MapGroundItem{Slot=1,ClickID=4,ItemID=41093,X=300,Y=300});Add(np,46041,1,1);ns.Packets.Clear();Input(np,3,1,1,1);Check(Slots(ns).Single()==2,"drop skips native IDs and aliases");Input(np,2,1);Check(np.Inv.GetItemCount(41093)==1&&nm.GroundItemList[0].IsPickedUp,"native pickup works");Input(np,2,1);Check(np.Inv.GetItemCount(41093)==1,"native duplicate denied");
  nm.GroundItemList[0].RespawnTime=DateTime.Now.AddSeconds(-1);ns.Packets.Clear();nm.Process();Check(!nm.GroundItemList[0].IsPickedUp&&Slots(ns).Contains(1),"native respawn packet");
  nm.GroundItemList.Add(new MapGroundItem{Slot=5,ClickID=5,ItemID=41093,X=300,Y=300});nm.GroundItemList.Add(new MapGroundItem{Slot=6,ClickID=6,ItemID=41093,X=300,Y=300});Input(np,2,6);int nativeCount=np.Inv.GetItemCount(41093);Input(np,2,6);Check(np.Inv.GetItemCount(41093)==nativeCount&&!nm.GroundItemList[1].IsPickedUp,"duplicate native pickup cannot take adjacent node");
  // Concurrent attempts serialize, awarding a single item.
  var cm=new DropMap();DropSocket c1,c2;var cp1=Make(cm,out c1);var cp2=Make(cm,out c2);Add(cp1,46041,1,1);c1.Packets.Clear();Input(cp1,3,1,1,1);byte cs=(byte)Slots(c1).Single();Parallel.Invoke(()=>cm.onItemPickup(cp1,cs),()=>cm.onItemPickup(cp2,cs));Check(cm.DropCount==0&&cp1.Inv.GetItemCount(46041)+cp2.Inv.GetItemCount(46041)==1,"concurrent pickup conservation");
  // Equipment metadata survives transfer.
  Add(cp1,21013,2,1);cp1.Inv[2].Damage=7;cp1.Inv[2].Forge=8;c1.Packets.Clear();Input(cp1,3,2,1,1);cs=(byte)Slots(c1).Single();Input(cp2,2,cs);var eq=Enumerable.Range(1,50).Select(i=>cp2.Inv[(byte)i]).Single(i=>i.ItemID==21013);Check(eq.Damage==7&&eq.Forge==8,"equipment retains damage and forging");
  var cap=new DropMap();DropSocket caps;var capp=Make(cap,out caps);for(int i=1;i<=255;i++)cap.GroundItemList.Add(new MapGroundItem{Slot=(byte)i,ClickID=(ushort)i,ItemID=41093});Add(capp,46041,1,2);Input(capp,3,1,2,1);Check(cap.DropCount==0&&capp.Inv[1].Ammt==2,"full map rejects whole drop without loss");
  transport.Dispose();return "PASS: "+checks+" loot/ground checks; "+verified+" configured native loot entries retained; native client replay pending.";
 }
}
"@
Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs+@('System.Core.dll','System.Data.dll','System.Xml.dll'))
$before=(Get-FileHash -LiteralPath $db).Hash
$result=[DropChecks]::Run($root,$ExpectOld.IsPresent)
if((Get-FileHash -LiteralPath $db).Hash -ne $before){throw "Fixture database changed during loot loading or packet checks"}
$result
"PASS: fixture database hash unchanged."
