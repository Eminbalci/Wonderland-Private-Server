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
using System.Linq;
using Game;
using Game.DataFiles;
using Game.QuestRelated;

public class DebugPE
{
    public static void Run()
    {
        var player = new Player();
        player.Quests = new Dictionary<uint, PlayerQuest>();
        player.Quests[13188] = new PlayerQuest(13188, QuestState.Completed, 1);
        player.Quests[13189] = new PlayerQuest(13189, QuestState.InProgress, 1);

        var md = DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(12600);
        foreach (var pe in md.PreEvents)
        {
            var method = typeof(PreEventInterpreter).GetMethod("GetApplicableRules", BindingFlags.NonPublic | BindingFlags.Static);
            var rules = (System.Collections.IEnumerable)method.Invoke(null, new object[] { player, (ushort)12600, pe });
            foreach (var rule in rules)
            {
                var condsProp = rule.GetType().GetProperty("Conditions");
                var actsProp = rule.GetType().GetProperty("Actions");
                var conds = (List<byte[]>)condsProp.GetValue(rule, null);
                var acts = (List<preEventSubSubEntry>)actsProp.GetValue(rule, null);

                var evalMethod = typeof(PreEventInterpreter).GetMethod("EvaluateConditionBlock", BindingFlags.NonPublic | BindingFlags.Static);
                bool allMatch = conds.All(c => (bool)evalMethod.Invoke(null, new object[] { player, c }));

                if (acts != null)
                {
                    foreach (var a in acts)
                    {
                        var arr = a.unknown.ToArray();
                        ushort tc = BitConverter.ToUInt16(arr, 1);
                        ushort type = BitConverter.ToUInt16(arr, 3);
                        if (tc == 3)
                        {
                            Console.WriteLine("PE clickID=" + pe.clickID + " Target=3 type=" + type + " allMatch=" + allMatch + " condCount=" + conds.Count);
                        }
                    }
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

[DebugPE]::Run()
