$repo = (Get-Location).Path
$path = Join-Path $repo 'Data\Mark.dat'
$data = [System.IO.File]::ReadAllBytes($path)
$marks = @{}
for ($offset = 553; $offset + 553 -le $data.Length; $offset += 553) {
    $id = ([BitConverter]::ToUInt16($data, $offset + 256) -bxor 0x2774) - 7
    $flag = ([BitConverter]::ToUInt16($data, $offset + 258) -bxor 0x2774) - 7
    if ($id -ne 0) {
        $marks[[ushort]$id] = [ushort]$flag
    }
}
Write-Host ("15266 in Mark.dat: " + $marks.ContainsKey(15266) + " flag=" + $marks[15266])
Write-Host ("15267 in Mark.dat: " + $marks.ContainsKey(15267) + " flag=" + $marks[15267])
Write-Host ("13188 in Mark.dat: " + $marks.ContainsKey(13188) + " flag=" + $marks[13188])
Write-Host ("13189 in Mark.dat: " + $marks.ContainsKey(13189) + " flag=" + $marks[13189])
Write-Host ("12026 in Mark.dat: " + $marks.ContainsKey(12026) + " flag=" + $marks[12026])
Write-Host ("12027 in Mark.dat: " + $marks.ContainsKey(12027) + " flag=" + $marks[12027])
