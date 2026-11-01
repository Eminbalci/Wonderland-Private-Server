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
using System.Reflection;
using Game;
using Game.DataFiles;
using Game.Maps;
using Game.QuestRelated;

public static class TestImmortalXuE2E
{
    public static void Run()
    {
        try
        {
            var player = new Player();
            player.Quests = new Dictionary<uint, PlayerQuest>();
            player.Quests[13188] = new PlayerQuest(13188, QuestState.Completed, 20);
            player.Quests[13189] = new PlayerQuest(13189, QuestState.InProgress, 1);
            player.Quests[50333] = new PlayerQuest(50333, QuestState.InProgress, 1);
            player.Eqs.SetLevel(100);

            var md12625 = DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(12625);
            var ev0_12625 = md12625.Events[0];

            MethodInfo findBranchMethod = typeof(EveEventInterpreter)
                .GetMethod("FindBranch", BindingFlags.NonPublic | BindingFlags.Static);

            object resultBranch = findBranchMethod.Invoke(null, new object[] { player, null, ev0_12625, (byte)0, (ushort)0, (ushort)0, null });
            EventSubEntry selected = (EventSubEntry)resultBranch;
            int selectedIndex = ev0_12625.SubEntry.IndexOf(selected);
            Console.WriteLine("Map 12625 Event 0 Selected Branch Index: " + selectedIndex);

            if (selectedIndex != 6)
            {
                throw new Exception("FAILURE: Expected Branch 6 (Quest Offer), but got Branch " + selectedIndex);
            }
            Console.WriteLine("SUCCESS: Accession Official correctly selected Branch 6 (Trial Quest Offer)!");

            // Step 2: Before accepting Quest 15090, test Immortal Xu visibility on Map 12626
            bool visBefore = PreEventInterpreter.ShouldNpcBeVisible(player, 12626, 1);
            Console.WriteLine("Map 12626 Immortal Xu visibility BEFORE accepting trial: " + visBefore);
            if (visBefore)
            {
                throw new Exception("FAILURE: Immortal Xu should be hidden before player accepts trial!");
            }

            // Step 3: Player accepts trial -> Quest 15090 Step 1 is granted
            player.Quests[15090] = new PlayerQuest(15090, QuestState.InProgress, 1);
            Console.WriteLine("\n--- Player accepts Quest 15090 (Step 1) ---");

            // Step 4: After accepting Quest 15090, test Immortal Xu visibility on Map 12626
            bool visAfter = PreEventInterpreter.ShouldNpcBeVisible(player, 12626, 1);
            Console.WriteLine("Map 12626 Immortal Xu visibility AFTER accepting trial: " + visAfter);
            if (!visAfter)
            {
                throw new Exception("FAILURE: Immortal Xu must be visible after player accepts trial!");
            }
            Console.WriteLine("SUCCESS: The Immortal Xu correctly spawns and is visible on Map 12626!");

            // Step 5: After accepting Quest 15090, Accession Official on Map 12625 should now select Branch 0 (In-Progress)
            object resultBranchAfter = findBranchMethod.Invoke(null, new object[] { player, null, ev0_12625, (byte)0, (ushort)0, (ushort)0, null });
            EventSubEntry selectedAfter = (EventSubEntry)resultBranchAfter;
            int selectedIndexAfter = ev0_12625.SubEntry.IndexOf(selectedAfter);
            Console.WriteLine("Map 12625 Event 0 Selected Branch Index AFTER accepting trial: " + selectedIndexAfter);
            if (selectedIndexAfter != 0)
            {
                throw new Exception("FAILURE: Expected Branch 0 (In-Progress), but got Branch " + selectedIndexAfter);
            }
            Console.WriteLine("SUCCESS: Accession Official correctly gives in-progress dialogue once quest is active!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception in Run: " + ex.ToString());
            throw;
        }
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
[DataBase.GameDataBase]::GlobalInstance = New-Object DataBase.GameDataBase
[DataBase.GameDataBase]::GlobalInstance.EveDat = $mgr

[TestImmortalXuE2E]::Run()
