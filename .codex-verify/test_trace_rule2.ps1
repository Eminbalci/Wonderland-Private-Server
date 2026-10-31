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
using System.Collections.Generic;
using Game;
using Game.DataFiles;
using Game.QuestRelated;

public static class TraceRules
{
    public static void Run()
    {
        var player = new Player();
        player.Quests = new Dictionary<uint, PlayerQuest>();
        player.Quests[15266] = new PlayerQuest(15266, QuestState.Completed, 1);
        player.Quests[15267] = new PlayerQuest(15267, QuestState.InProgress, 1);

        var md = DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(12601);
        var pe3 = md.PreEvents[2]; // ClickID 3

        byte[] cond2 = pe3.subentry1[2].unknown.ToArray();
        byte[] cond3 = pe3.subentry1[3].unknown.ToArray();

        Console.WriteLine("Cond2 (w2=2) matches: " + Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cond2));
        Console.WriteLine("Cond3 (w2=1) matches: " + Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cond3));
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
[DataBase.GameDataBase]::GlobalInstance = New-Object DataBase.GameDataBase
[DataBase.GameDataBase]::GlobalInstance.EveDat = $mgr

[TraceRules]::Run()
