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

public static class SajitTester
{
    public static void Test()
    {
        var player = new Player();
        player.Quests = new Dictionary<uint, PlayerQuest>();

        Console.WriteLine("Initial state (No quests):");
        Console.WriteLine("ClickID 5 (Sajit sitting) visible: " + PreEventInterpreter.ShouldNpcBeVisible(player, 12601, 5));
        Console.WriteLine("ClickID 7 (Sajit standing) visible: " + PreEventInterpreter.ShouldNpcBeVisible(player, 12601, 7));

        Console.WriteLine("\nSetting Quest 15266 to InProgress Step 5 (Transformed to standing):");
        player.Quests[15266] = new PlayerQuest(15266, QuestState.InProgress, 5);
        Console.WriteLine("ClickID 5 (Sajit sitting) visible: " + PreEventInterpreter.ShouldNpcBeVisible(player, 12601, 5));
        Console.WriteLine("ClickID 7 (Sajit standing) visible: " + PreEventInterpreter.ShouldNpcBeVisible(player, 12601, 7));

        Console.WriteLine("\nSetting Quest 15266 to Completed, 15267 to InProgress Step 1 (Departed / Left):");
        player.Quests[15266] = new PlayerQuest(15266, QuestState.Completed, 1);
        player.Quests[15267] = new PlayerQuest(15267, QuestState.InProgress, 1);
        Console.WriteLine("ClickID 5 (Sajit sitting) visible: " + PreEventInterpreter.ShouldNpcBeVisible(player, 12601, 5));
        Console.WriteLine("ClickID 6 (Nadul) visible: " + PreEventInterpreter.ShouldNpcBeVisible(player, 12601, 6));
        Console.WriteLine("ClickID 7 (Sajit standing) visible: " + PreEventInterpreter.ShouldNpcBeVisible(player, 12601, 7));
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
[DataBase.GameDataBase]::GlobalInstance = New-Object DataBase.GameDataBase
[DataBase.GameDataBase]::GlobalInstance.EveDat = $mgr
[Game.DataFiles.SceneDataManager]::Initialize((Join-Path $repo 'Data'))
[SajitTester]::Test()
