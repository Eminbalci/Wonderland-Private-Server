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
using Game.QuestRelated;

public class TestRulesTrace
{
    public static void Run()
    {
        try {
            var player = new Player();
            player.Quests = new Dictionary<uint, PlayerQuest>();
            player.Quests[13188] = new PlayerQuest(13188, QuestState.Completed, 1);
            player.Quests[13189] = new PlayerQuest(13189, QuestState.InProgress, 1);

            var md = DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(12600);
            var pe0 = md.PreEvents[0];

            var method = typeof(PreEventInterpreter).GetMethod("GetApplicableRules",
                BindingFlags.NonPublic | BindingFlags.Static);

            var rules = (System.Collections.IEnumerable)method.Invoke(null, new object[] { player, (ushort)12600, pe0 });

            int idx = 0;
            foreach (var rule in rules)
            {
                var condsProp = rule.GetType().GetProperty("Conditions", BindingFlags.Public | BindingFlags.Instance);
                var actsProp = rule.GetType().GetProperty("Actions", BindingFlags.Public | BindingFlags.Instance);
                
                var conds = (System.Collections.IList)condsProp.GetValue(rule, null);
                var acts = actsProp != null ? (System.Collections.IList)actsProp.GetValue(rule, null) : null;
                
                bool allMatch = true;
                foreach (byte[] c in conds)
                {
                    bool m = Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, c);
                    if (!m) allMatch = false;
                }

                Console.WriteLine("Rule " + idx++ + ": CondCount=" + conds.Count + " ActCount=" + (acts != null ? acts.Count : 0) + " AllMatch=" + allMatch);
                if (acts != null)
                {
                    foreach (preEventSubSubEntry a in acts)
                    {
                        var arr = a.unknown.ToArray();
                        Console.WriteLine("    Act: target=" + BitConverter.ToUInt16(arr, 1) + " type=" + BitConverter.ToUInt16(arr, 3));
                    }
                }
            }
        } catch (Exception ex) {
            Console.WriteLine("Exception in Run: " + ex.ToString());
        }
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))
$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
[DataBase.GameDataBase]::GlobalInstance = New-Object DataBase.GameDataBase
[DataBase.GameDataBase]::GlobalInstance.EveDat = $mgr

[TestRulesTrace]::Run()
