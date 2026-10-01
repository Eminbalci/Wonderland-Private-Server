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

public class LoginTestSocket : SocketClient {
    public override void SendPacket(IPacket p) {}
}

public static class TestRobinsonEvent {
    public static void Run(string repoRoot) {
        var loader = new EveManager();
        loader.LoadFile(repoRoot + @"\Data\eve.Emg");
        var mapData = loader.GetMapData(10035);
        var ev19 = mapData.Events.Find(e => e.clickID == 19);

        var socket = (LoginTestSocket)FormatterServices.GetUninitializedObject(typeof(LoginTestSocket));
        socket.m_IncomingPackets = new ConcurrentQueue<IPacket>();
        var dat = new DataFiles.PhxItemDat();
        dat.Load(repoRoot + @"\Data\itemDat.wpdat").Wait();
        var player = new Player(socket, dat);
        player.CharName = "biroduzbir";

        var gmap = new GameMap();
        typeof(GameMap).GetField("m_mapid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gmap, (uint)10035);

        var findBranchMethod = typeof(EveEventInterpreter).GetMethod("FindBranch", BindingFlags.Static | BindingFlags.NonPublic);

        player.Quests = new System.Collections.Generic.Dictionary<uint, PlayerQuest>();
        player.Quests[12046] = new PlayerQuest(12046, QuestState.Completed, 1);
        player.Quests[12047] = new PlayerQuest(12047, QuestState.InProgress, 1);

        var branch = findBranchMethod.Invoke(null, new object[] { player, gmap, ev19, (byte)0, (ushort)0, (ushort)0, null }) as EventSubEntry;
        Console.WriteLine("Branch found with 12046 Completed & 12047 InProgress: subIndex=" + (branch != null ? branch.subIndex.ToString() : "NULL"));
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

[TestRobinsonEvent]::Run($repoRoot)
