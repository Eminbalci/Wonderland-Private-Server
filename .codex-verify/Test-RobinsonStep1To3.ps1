$repoRoot = "D:\GitHub\Wonderland-Private-Server"

$phoenixDataDll = "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$phoenixCoreDll = "$repoRoot\Phoenix.Core\bin\Debug\net462\Phoenix.Core.dll"
$wloCoreDll = "$repoRoot\Wlo.Core\bin\Debug\net462\Wlo.Core.dll"
$rcLibraryDll = "$repoRoot\RCLibrary\bin\Debug\net462\RCLibrary.dll"
$pserverCoreDll = "$repoRoot\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"

Add-Type -Path $phoenixDataDll
Add-Type -Path $phoenixCoreDll
Add-Type -Path $wloCoreDll
Add-Type -Path $rcLibraryDll
Add-Type -Path $pserverCoreDll

$code = @"
using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using Network;
using Game;
using Game.PlayerRelated;
using Game.QuestRelated;
using Game.Maps;
using Game.DataFiles;
using RCLibrary.Core.Networking;

public class LoginTestSocket2 : SocketClient {
    public override void SendPacket(IPacket p) {}
}

public static class TestRobinsonEventTransitions {
    public static void Run(string repoRoot) {
        var loader = new EveManager();
        loader.LoadFile(repoRoot + @"\Data\eve.Emg");
        var mapData = loader.GetMapData(10035);
        var ev19 = mapData.Events.Find(e => e.clickID == 19);

        var socket = (LoginTestSocket2)FormatterServices.GetUninitializedObject(typeof(LoginTestSocket2));
        socket.m_IncomingPackets = new ConcurrentQueue<IPacket>();
        var dat = new DataFiles.PhxItemDat();
        dat.Load(repoRoot + @"\Data\itemDat.wpdat").Wait();
        var player = new Player(socket, dat);
        player.CharName = "biroduzbir";

        var gmap = new GameMap();
        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gmap, (uint)10035);

        var findBranchMethod = typeof(EveEventInterpreter).GetMethod("FindBranch", BindingFlags.Static | BindingFlags.NonPublic);

        // Step 1: Initial state before chest (no quests)
        player.Quests = new System.Collections.Generic.Dictionary<uint, PlayerQuest>();
        var branch0 = findBranchMethod.Invoke(null, new object[] { player, gmap, ev19, (byte)0, (ushort)0, (ushort)0, null }) as EventSubEntry;
        Console.WriteLine("Step 0 (Initial): subIndex=" + (branch0 != null ? branch0.subIndex.ToString() : "NULL"));

        // Step 1: Chest opened -> Quest 12046 Step 1 (InProgress)
        player.Quests[12046] = new PlayerQuest(12046, QuestState.InProgress, 1);
        var branch1 = findBranchMethod.Invoke(null, new object[] { player, gmap, ev19, (byte)0, (ushort)0, (ushort)0, branch0 }) as EventSubEntry;
        Console.WriteLine("Step 1 (After chest opened, exclude branch0): subIndex=" + (branch1 != null ? branch1.subIndex.ToString() : "NULL"));

        // Step 2: Dialogue finished -> Quest 12046 Completed, Quest 12047 Step 1 (InProgress)
        player.Quests[12046] = new PlayerQuest(12046, QuestState.Completed, 1);
        player.Quests[12047] = new PlayerQuest(12047, QuestState.InProgress, 1);
        var branch2 = findBranchMethod.Invoke(null, new object[] { player, gmap, ev19, (byte)0, (ushort)0, (ushort)0, branch1 }) as EventSubEntry;
        Console.WriteLine("Step 2 (After dialogue finished, exclude branch1): subIndex=" + (branch2 != null ? branch2.subIndex.ToString() : "NULL"));
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies @(
    $phoenixDataDll,
    $phoenixCoreDll,
    $wloCoreDll,
    $rcLibraryDll,
    $pserverCoreDll,
    "System.Core.dll"
)

[TestRobinsonEventTransitions]::Run($repoRoot)
