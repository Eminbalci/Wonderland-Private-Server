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

public class TestRules
{
    public static void Run()
    {
        var md = DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(12600);
        var rules = typeof(PreEventInterpreter).GetMethod("GroupSubEntriesIntoRules",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { md.PreEvents[0] }) as System.Collections.IEnumerable;

        int idx = 0;
        foreach (var r in rules)
        {
            var conds = r.GetType().GetField("Conditions").GetValue(r) as System.Collections.IList;
            var acts = r.GetType().GetField("Actions").GetValue(r) as System.Collections.IList;
            Console.WriteLine("Rule " + idx++ + ": CondCount=" + conds.Count + " ActCount=" + (acts != null ? acts.Count : 0));
            foreach (byte[] c in conds)
            {
                int w4 = BitConverter.ToUInt16(c, 7);
                int w5 = BitConverter.ToUInt16(c, 9);
                int cmp = w4 & 255;
                int val = (w4 >> 8) | (w5 << 8);
                Console.WriteLine("    Cond: b1=" + c[0] + " w1=" + BitConverter.ToUInt16(c, 1) + " w2=" + BitConverter.ToUInt16(c, 3) + " w3=" + BitConverter.ToUInt16(c, 5) + " cmp=" + cmp + " val=" + val);
            }
            if (acts != null)
            {
                foreach (preEventSubSubEntry a in acts)
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

[TestRules]::Run()
