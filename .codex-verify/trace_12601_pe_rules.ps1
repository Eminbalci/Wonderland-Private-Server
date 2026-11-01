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
using System.Linq;
using Game;
using Game.DataFiles;
using Game.QuestRelated;

public static class Trace12601PE
{
    public static void Run()
    {
        var player = new Player();
        player.Quests = new Dictionary<uint, PlayerQuest>();
        player.Quests[15266] = new PlayerQuest(15266, QuestState.Completed, 1);
        player.Quests[15267] = new PlayerQuest(15267, QuestState.InProgress, 1);

        var md = DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(12601);
        var pe = md.PreEvents[2];

        var method = typeof(PreEventInterpreter).GetMethod("GetApplicableRules", BindingFlags.NonPublic | BindingFlags.Static);
        var rules = (System.Collections.IEnumerable)method.Invoke(null, new object[] { player, (ushort)12601, pe });
        int rIdx = 0;
        foreach (var rule in rules)
        {
            var condsProp = rule.GetType().GetProperty("Conditions");
            var actsProp = rule.GetType().GetProperty("Actions");
            var conds = (List<byte[]>)condsProp.GetValue(rule, null);
            var acts = (List<preEventSubSubEntry>)actsProp.GetValue(rule, null);

            var evalMethod = typeof(PreEventInterpreter).GetMethod("EvaluateConditionBlock", BindingFlags.NonPublic | BindingFlags.Static);
            int cIdx = 0;
            bool allMatch = true;
            foreach (var c in conds)
            {
                bool m = (bool)evalMethod.Invoke(null, new object[] { player, c });
                if (!m) allMatch = false;
                ushort q = BitConverter.ToUInt16(c, 1);
                ushort w2 = BitConverter.ToUInt16(c, 3);
                ushort w4 = BitConverter.ToUInt16(c, 7);
                Console.WriteLine("  Rule " + rIdx + " Cond " + cIdx++ + ": q=" + q + " w2=" + w2 + " w4=0x" + w4.ToString("X4") + " match=" + m);
            }
            Console.WriteLine("Rule " + rIdx++ + " AllMatch=" + allMatch);
            if (acts != null)
            {
                foreach (var a in acts)
                {
                    var arr = a.unknown.ToArray();
                    Console.WriteLine("    Act: target=" + BitConverter.ToUInt16(arr, 1) + " type=" + BitConverter.ToUInt16(arr, 3));
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

[Trace12601PE]::Run()
