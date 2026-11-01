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

public static class TestVerifyMonk
{
    public static void Run()
    {
        var player = new Player();
        player.Quests = new Dictionary<uint, PlayerQuest>();

        ushort initAnim;
        byte initFacing;
        bool hasPose = PreEventInterpreter.GetNpcInitialPose(player, 12600, 3, out initAnim, out initFacing);
        Console.WriteLine(string.Format("PreEventInterpreter.GetNpcInitialPose(12600, 3): hasPose={0}, initAnim={1}, initFacing={2}", hasPose, initAnim, initFacing));

        ushort idleFrame = Game.Maps.QuestNpc.GetIdleAnimationFrame(14729);
        ushort state = idleFrame;
        byte stance = 0;
        if (hasPose)
        {
            state = (ushort)((idleFrame & 0xFF) | (initFacing << 8));
            stance = (byte)initAnim;
        }

        Console.WriteLine(string.Format("Resulting State=0x{0:X4}, Stance={1}", state, stance));
        if (stance == 0 && state == 0x01FF)
        {
            Console.WriteLine("SUCCESS: Bangkok Monk correctly has Stance=0 and State=0x01FF (facing 1, idle 0xFF)!");
        }
        else
        {
            throw new Exception("FAILURE: Unexpected state or stance!");
        }
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
[DataBase.GameDataBase]::GlobalInstance = New-Object DataBase.GameDataBase
[DataBase.GameDataBase]::GlobalInstance.EveDat = $mgr

[TestVerifyMonk]::Run()
