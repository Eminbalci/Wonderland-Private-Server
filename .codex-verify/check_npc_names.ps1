$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }
[Game.DataFiles.SceneDataManager]::Initialize((Join-Path $repo 'Data'))
Write-Host ("NPC 14729 name: " + [Game.QuestRelated.QuestManager]::GetNpcName(14729))
for ($i = 14720; $i -le 14740; $i++) {
    $n = [Game.QuestRelated.QuestManager]::GetNpcName($i)
    if ($n -ne "NPC") {
        Write-Host ("TID $i : $n")
    }
}
