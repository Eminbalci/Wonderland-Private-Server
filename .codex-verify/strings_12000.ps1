$path = if (Test-Path "D:/Game Private/WLRI/sty/12000.sty") { "D:/Game Private/WLRI/sty/12000.sty" } else { "WLRI/sty/12000.sty" }
$bytes = [System.IO.File]::ReadAllBytes($path)
Write-Host ("Length: " + $bytes.Length)
$str = ""
for ($i = 0; $i -lt $bytes.Length; $i++) {
    $b = $bytes[$i]
    if ($b -ge 32 -and $b -le 126) {
        $str += [char]$b
    } else {
        if ($str.Length -ge 3) {
            Write-Host $str
        }
        $str = ""
    }
}
