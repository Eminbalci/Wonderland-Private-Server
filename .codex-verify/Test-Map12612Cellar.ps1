param([string]$BuildDirectory = "bin/Debug")
$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path

$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$code = @"
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.Serialization;
using Game;
using Game.Maps;
using Game.DataFiles;
using Game.QuestRelated;
using Network;
using RCLibrary.Core.Networking;

public class CellarSocket : SocketClient {
    public List<byte[]> Packets = new List<byte[]>();
    public CellarSocket() {
        m_IncomingPackets = new System.Collections.Concurrent.ConcurrentQueue<IPacket>();
    }
    public override void SendPacket(IPacket packet) {
        Packets.Add(packet.Buffer);
    }
}

public class CellarTestMap : GameMap {
    public CellarTestMap(ushort id) { m_mapid = id; }
    protected override void Warp_Out(byte portalID, Player p, WarpData to, bool toTent = false) { p.CancelInteraction(); }
    protected override void Warp_In(TeleportType type, Player p, WarpData from = null, byte portalID = 0) {
        p.CurMap = this;
        p.CurX = from != null ? from.DstX_Axis : (ushort)0;
        p.CurY = from != null ? from.DstY_Axis : (ushort)0;
    }
}

public static class CellarTester {
    static MapManager maps;
    static GameMap GetMap(ushort id) {
        if (maps == null) maps = new MapManager();
        var cache = (Dictionary<ushort, GameMap>)typeof(MapManager).GetField("_mapCache", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(maps);
        GameMap m;
        if (!cache.TryGetValue(id, out m)) {
            m = new CellarTestMap(id);
            cache[id] = m;
        }
        return m;
    }

    public static string Run(string root) {
        try {
            var eve = new EveManager();
            eve.LoadFile(root + "/Data/Eve.EMG");
            DataBase.GameDataBase.GlobalInstance = (DataBase.GameDataBase)FormatterServices.GetUninitializedObject(typeof(DataBase.GameDataBase));
            typeof(DataBase.GameDataBase).GetProperty("EveDat").SetValue(DataBase.GameDataBase.GlobalInstance, eve);

            var sock = new CellarSocket();
            var items = new global::DataFiles.PhxItemDat();
            var p = new Player(sock, items);
            p.CharName = "test";
            p.Quests = new Dictionary<uint, PlayerQuest>();

            var map12612 = GetMap(12612);
            GetMap(12617);
            p.CurMap = map12612;
            p.CurX = 1342;
            p.CurY = 515;

            // 1. Initial state: before interaction
            int propStateBefore = 0;
            p.NativePropStates.TryGetValue(3, out propStateBefore);

            // 2. First click NPC 3 (Cellar Entrance): executes Event 4, unlocks entrance
            bool ok1 = EveEventInterpreter.TryExecute(p, map12612, 3);
            int propStateAfter = 0;
            p.NativePropStates.TryGetValue(3, out propStateAfter);

            // 3. Second click on NPC 3 directly (now opened with state 1) -> should execute Event 5 and warp to Map 12617
            bool okNpcAgain = EveEventInterpreter.TryExecute(p, map12612, 3);
            ushort mapAfterNpcAgain = (ushort)(p.CurMap != null ? p.CurMap.MapID : 0);
            ushort posXAfterNpcAgain = p.CurX;
            ushort posYAfterNpcAgain = p.CurY;

            // 4. Return to Map 12612, unlock again, then test Entry 2 (AC 20:8)
            p.CurMap = map12612;
            p.CurX = 1342;
            p.CurY = 515;
            p.NativePropStates.Clear();
            bool okReopen = EveEventInterpreter.TryExecute(p, map12612, 3);
            bool okEntry = EveEventInterpreter.TryExecuteEntry(p, map12612, 2);
            ushort mapAfterEntry = (ushort)(p.CurMap != null ? p.CurMap.MapID : 0);
            ushort posXAfterEntry = p.CurX;
            ushort posYAfterEntry = p.CurY;

            return string.Format("PropBefore={0}, Try1={1}, PropAfter={2}, TryNpcAgain={3}, MapAfterNpcAgain={4} ({5},{6}), Reopen={7}, TryEntry={8}, MapAfterEntry={9} ({10},{11})",
                propStateBefore, ok1, propStateAfter, okNpcAgain, mapAfterNpcAgain, posXAfterNpcAgain, posYAfterNpcAgain,
                okReopen, okEntry, mapAfterEntry, posXAfterEntry, posYAfterEntry);
        } catch (Exception ex) {
            return "ERROR: " + ex.ToString();
        }
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System.Core.dll'))
[CellarTester]::Run($repo)
