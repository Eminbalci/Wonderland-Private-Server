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

public static class Test12600Npc3
{
    public static void Run()
    {
        var player = new Player();
        player.Quests = new Dictionary<uint, PlayerQuest>();

        Console.WriteLine("--- Initial state (No quests) ---");
        Console.WriteLine("NPC 3 visible: " + PreEventInterpreter.ShouldNpcBeVisible(player, 12600, 3));

        Console.WriteLine("\n--- After Event 20 Branch 3: Quest 13188 InProgress Step 10 ---");
        player.Quests[13188] = new PlayerQuest(13188, QuestState.InProgress, 10);
        Console.WriteLine("NPC 3 visible: " + PreEventInterpreter.ShouldNpcBeVisible(player, 12600, 3));

        Console.WriteLine("\n--- After turning in 3 items: Quest 13188 Completed, Quest 13189 InProgress Step 1 ---");
        player.Quests[13188] = new PlayerQuest(13188, QuestState.Completed, 1);
        player.Quests[13189] = new PlayerQuest(13189, QuestState.InProgress, 1);
        Console.WriteLine("NPC 3 visible: " + PreEventInterpreter.ShouldNpcBeVisible(player, 12600, 3));
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
[DataBase.GameDataBase]::GlobalInstance = New-Object DataBase.GameDataBase
[DataBase.GameDataBase]::GlobalInstance.EveDat = $mgr

[Test12600Npc3]::Run()
