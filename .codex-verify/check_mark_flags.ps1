$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$code = @"
using System;
using Game.PlayerRelated;

public static class CheckMarks
{
    public static void Run()
    {
        ushort flag;
        Console.WriteLine("15266: " + NotebookManager.TryGetCompletionFlag(15266, out flag) + " flag=" + flag);
        Console.WriteLine("15267: " + NotebookManager.TryGetCompletionFlag(15267, out flag) + " flag=" + flag);
        Console.WriteLine("13188: " + NotebookManager.TryGetCompletionFlag(13188, out flag) + " flag=" + flag);
        Console.WriteLine("13189: " + NotebookManager.TryGetCompletionFlag(13189, out flag) + " flag=" + flag);
    }
}
"@

Add-Type -TypeDefinition $code -ReferencedAssemblies ($refs + @('System', 'System.Core'))
[CheckMarks]::Run()
