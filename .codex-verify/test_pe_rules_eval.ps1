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

        var rules = PreEventInterpreter.GetApplicableRules(player, 12601, pe3);
        Console.WriteLine("Applicable rules count: " + rules.Count);
        for (int r = 0; r < rules.Count; r++)
        {
            var rule = rules[r];
            bool match = System.Linq.Enumerable.All(rule.Conditions, c => Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, c));
            int actCount = rule.Actions != null ? rule.Actions.Count : 0;
            Console.WriteLine("Rule " + r + " matches: " + match + ", Actions count: " + actCount);
            if (rule.Conditions != null)
            {
                for (int ci = 0; ci < rule.Conditions.Count; ci++)
                {
                    bool cMatch = Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, rule.Conditions[ci]);
                    Console.WriteLine("  Cond " + ci + " matches: " + cMatch);
                }
            }
        }
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
[DataBase.GameDataBase]::GlobalInstance = New-Object DataBase.GameDataBase
[DataBase.GameDataBase]::GlobalInstance.EveDat = $mgr

[TraceRules]::Run()
