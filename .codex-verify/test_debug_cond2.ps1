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

        var condBytes = pe3.subentry1[2].unknown.ToArray();
        var condition = new EventSubEntry {
            unknownbyte1 = condBytes[0], unknownword1 = BitConverter.ToUInt16(condBytes, 1),
            unknownword2 = BitConverter.ToUInt16(condBytes, 3), unknownword3 = BitConverter.ToUInt16(condBytes, 5),
            unknownword4 = BitConverter.ToUInt16(condBytes, 7), unknownword5 = BitConverter.ToUInt16(condBytes, 9),
            unknownword6 = BitConverter.ToUInt16(condBytes, 11)
        };

        int value = (condition.unknownword4 >> 8) | (condition.unknownword5 << 8) |
            ((condition.unknownword6 & 255) << 24);
        int comparison = condition.unknownword4 & 255;
        Console.WriteLine("q=" + condition.unknownword1 + " w2=" + condition.unknownword2 + " cmp=" + comparison + " val=" + value);

        PlayerQuest quest = null;
        bool hasQuest = player.Quests != null && player.Quests.TryGetValue(condition.unknownword1, out quest);
        bool active = hasQuest && (quest.State == QuestState.InProgress || quest.State == QuestState.Completed);
        Console.WriteLine("hasQuest=" + hasQuest + ", active=" + active + ", quest.State=" + (quest != null ? quest.State.ToString() : "null"));
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
[DataBase.GameDataBase]::GlobalInstance = New-Object DataBase.GameDataBase
[DataBase.GameDataBase]::GlobalInstance.EveDat = $mgr

[TraceRules]::Run()
