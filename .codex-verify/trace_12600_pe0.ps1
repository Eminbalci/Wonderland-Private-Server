$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$code = @"
using System;
using System.Collections.Generic;
using Game;
using Game.DataFiles;
using Game.QuestRelated;

public static class Trace12600PE0
{
    public static void Run()
    {
        var player = new Player();
        player.Quests = new Dictionary<uint, PlayerQuest>();
        player.Quests[13188] = new PlayerQuest(13188, QuestState.Completed, 1);
        player.Quests[13189] = new PlayerQuest(13189, QuestState.InProgress, 1);

        var md = DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(12600);
        var pe0 = md.PreEvents[0];
        Console.WriteLine("PE0 clickID: " + pe0.clickID + " subentry1: " + pe0.subentry1.Count);

        for (int i = 0; i < pe0.subentry1.Count; i++)
        {
            var sub = pe0.subentry1[i];
            bool match = Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, sub.unknown.ToArray());
            Console.WriteLine("Sub " + i + ": match=" + match + " acts=" + (sub.subentry2 != null ? sub.subentry2.Count : 0));
        }

        Console.WriteLine("ShouldNpcBeVisible(12600, 3) = " + PreEventInterpreter.ShouldNpcBeVisible(player, 12600, 3));
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
[DataBase.GameDataBase]::GlobalInstance = New-Object DataBase.GameDataBase
[DataBase.GameDataBase]::GlobalInstance.EveDat = $mgr

[Trace12600PE0]::Run()
