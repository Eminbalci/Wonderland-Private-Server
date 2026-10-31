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

        var m = typeof(PreEventInterpreter).GetMethod("GroupSubEntriesIntoRules", BindingFlags.NonPublic | BindingFlags.Static);
        var rules = m.Invoke(null, new object[] { pe3 }) as System.Collections.IList;
        Console.WriteLine("Grouped rules count: " + rules.Count);
        for (int r = 0; r < rules.Count; r++)
        {
            var rule = rules[r];
            var condProp = rule.GetType().GetProperty("Conditions");
            var actProp = rule.GetType().GetProperty("Actions");
            var conditions = condProp.GetValue(rule, null) as List<byte[]>;
            var acts = actProp.GetValue(rule, null) as System.Collections.IList;
            int actCount = acts != null ? acts.Count : 0;
            bool match = true;
            for (int ci = 0; ci < conditions.Count; ci++)
            {
                bool cMatch = Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, conditions[ci]);
                Console.WriteLine("Rule " + r + " Cond " + ci + " matches: " + cMatch);
                if (!cMatch) match = false;
            }
            Console.WriteLine("Rule " + r + " overall match: " + match + ", Actions count: " + actCount);
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
