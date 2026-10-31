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

public static class ConditionTester
{
    public static void Run()
    {
        var player = new Player();
        player.Quests = new Dictionary<uint, PlayerQuest>();

        // PreEvent #3 Sub[0] bytes:
        // 05 A2 3B 01 00 01 00 05 05 00 00 00 02 00 00 00 00 00 00 00 00
        byte[] cond0 = new byte[] { 0x05, 0xA2, 0x3B, 0x01, 0x00, 0x01, 0x00, 0x05, 0x05, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        // PreEvent #3 Sub[1] bytes:
        // 05 A3 3B 01 00 01 00 05 00 00 00 00 00 00 00 00 00 00 00 00 00
        byte[] cond1 = new byte[] { 0x05, 0xA3, 0x3B, 0x01, 0x00, 0x01, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        Console.WriteLine("--- Test 1: No quests ---");
        Console.WriteLine("Cond0 matches: " + Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cond0));
        Console.WriteLine("Cond1 matches: " + Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cond1));

        Console.WriteLine("\n--- Test 2: Quest 15266 InProgress Step 1 ---");
        player.Quests[15266] = new PlayerQuest(15266, QuestState.InProgress, 1);
        Console.WriteLine("Cond0 matches: " + Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cond0));
        Console.WriteLine("Cond1 matches: " + Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cond1));

        Console.WriteLine("\n--- Test 3: Quest 15266 InProgress Step 2 ---");
        player.Quests[15266] = new PlayerQuest(15266, QuestState.InProgress, 2);
        Console.WriteLine("Cond0 matches: " + Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cond0));
        Console.WriteLine("Cond1 matches: " + Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cond1));

        Console.WriteLine("\n--- Test 4: Quest 15266 InProgress Step 5 ---");
        player.Quests[15266] = new PlayerQuest(15266, QuestState.InProgress, 5);
        Console.WriteLine("Cond0 matches: " + Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cond0));
        Console.WriteLine("Cond1 matches: " + Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cond1));
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))
[ConditionTester]::Run()
