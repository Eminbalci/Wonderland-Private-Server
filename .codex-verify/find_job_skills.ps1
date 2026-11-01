$data = [System.IO.File]::ReadAllBytes('Data/Skill.dat')
$recordSize = 148
$total = [int]($data.Length / $recordSize)
for ($r = 0; $r -lt $total; $r++) {
    $offset = $r * $recordSize
    $rawSkillId = [System.BitConverter]::ToUInt16($data, $offset + 22)
    $skillId = ($rawSkillId -bxor 0x6EA0) - 4
    if ($skillId -ge 15238 -and $skillId -le 15250) {
        $nameBytes = New-Object byte[] 20
        for ($i = 0; $i -lt 20; $i++) { $nameBytes[$i] = $data[$offset + 1 + 19 - $i] }
        $name = [System.Text.Encoding]::ASCII.GetString($nameBytes).Trim([char]0, ' ')
        $rawSp = [System.BitConverter]::ToUInt16($data, $offset + 24)
        $sp = ($rawSp -bxor 0x6EA0) - 4
        Write-Output "$skillId | $name | SP: $sp"
    }
}
