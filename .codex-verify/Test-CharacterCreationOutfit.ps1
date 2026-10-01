$repoRoot = "D:\GitHub\Wonderland-Private-Server"
$binDir = "$repoRoot\bin\Debug"

$env:PATH = "$binDir;$binDir\x86;$binDir\x64;" + $env:PATH
Set-Location $binDir

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
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using Network;
using Game;
using Game.PlayerRelated;
using RCLibrary.Core.Networking;

public class LoginTestSocket : SocketClient {
    public override void SendPacket(IPacket p) {}
}

public static class TestOutfitRunner {
    public static void Run(string datPath, string dbPath) {
        // Initialize SQLite path
        var dbField = typeof(RCLibrary.Core.DataBase).GetField("DBFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (dbField != null) dbField.SetValue(null, dbPath);

        var dat = new DataFiles.PhxItemDat();
        dat.Load(datPath).Wait();
        Console.WriteLine("Loaded items from dat: " + dat.GetItemList().Count);

        var socket = (LoginTestSocket)FormatterServices.GetUninitializedObject(typeof(LoginTestSocket));
        socket.m_IncomingPackets = new ConcurrentQueue<IPacket>();
        var player = new Player(socket, dat);

        player.Body = BodyStyle.Big_Male;
        player.Head = 0; // Daniel

        player.SetBeginnerOutfit();
        Console.WriteLine("Daniel equipped count: " + player.Eqs.WornCount);

        var bodyEquip = player[2];
        var shoeEquip = player[5];
        Console.WriteLine("Slot 2 (Body): " + bodyEquip.ItemID + " " + bodyEquip.Name.Trim('\0'));
        Console.WriteLine("Slot 5 (Shoes): " + shoeEquip.ItemID + " " + shoeEquip.Name.Trim('\0'));

        if (bodyEquip.ItemID != 21004 || shoeEquip.ItemID != 24004) {
            throw new Exception("Daniel outfit failed! Expected 21004 & 24004, got " + bodyEquip.ItemID + " & " + shoeEquip.ItemID);
        }

        StarterPackManager.LoadFromDatabase();
        StarterPackManager.DeliverToPlayer(player, false);
        int bagCount = 0;
        for (byte s = 1; s <= 50; s++) {
            var it = player.Inv[s];
            if (it != null && it.ItemID > 0) {
                bagCount++;
                Console.WriteLine("Bag " + s + ": " + it.ItemID + " (" + it.Name.Trim('\0') + ") x" + it.Ammt);
            }
        }
        Console.WriteLine("Delivered starter items in bag: " + bagCount);
        if (bagCount < 8) {
            throw new Exception("Starter pack missing items! Expected at least 8 items, got " + bagCount);
        }

        Console.WriteLine("VERIFICATION SUCCESSFUL: Daniel equipped beginner clothes and received all starter items!");
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

[TestOutfitRunner]::Run("$repoRoot\Data\itemDat.wpdat", "$binDir\Data\ServerDataBase.db")
