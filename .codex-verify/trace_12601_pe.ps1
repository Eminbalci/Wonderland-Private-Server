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
using System.Linq;
using Game;
using Game.DataFiles;
using Game.QuestRelated;

public static class Trace12601
{
    public static void Run(Game.DataFiles.EveManager eveDat)
    {
        var player = new Player();
        player.Quests = new Dictionary<uint, PlayerQuest>();

        var mapData = eveDat != null ? eveDat.GetMapData(12601) : null;
        if (mapData == null) { Console.WriteLine("mapData is null!"); return; }

        Console.WriteLine("Map 12601 PreEvents count: " + mapData.PreEvents.Count);
        int peIdx = 0;
        foreach (var pe in mapData.PreEvents)
        {
            Console.WriteLine(string.Format("\n--- PreEvent #{0} (clickID={1}, subentries={2}) ---", peIdx++, pe.clickID, pe.subentry1 != null ? pe.subentry1.Count : 0));
            if (pe.subentry1 == null) continue;
            int subIdx = 0;
            foreach (var sub in pe.subentry1)
            {
                byte[] cBytes = sub.unknown != null ? sub.unknown.ToArray() : new byte[0];
                bool match = Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, cBytes);
                Console.WriteLine(string.Format("  Sub #{0}: CondMatch={1}, CondBytes={2}", subIdx++, match, BitConverter.ToString(cBytes)));
                if (sub.subentry2 != null)
                {
                    foreach (var act in sub.subentry2)
                    {
                        byte[] aBytes = act.unknown != null ? act.unknown.ToArray() : new byte[0];
                        Console.WriteLine(string.Format("     Act: {0}", BitConverter.ToString(aBytes)));
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
[Game.DataFiles.SceneDataManager]::Initialize((Join-Path $repo 'Data'))
[Trace12601]::Run($mgr)
