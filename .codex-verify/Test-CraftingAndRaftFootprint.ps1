$ErrorActionPreference = "Stop"

$repoRoot = "D:\GitHub\Wonderland-Private-Server"
$binDir = Join-Path $repoRoot "bin\Debug"

Add-Type -Path (Join-Path $binDir "PhoenixData.dll")
Add-Type -Path (Join-Path $binDir "RCLibrary.dll")
Add-Type -Path (Join-Path $binDir "wlo.pserver.core.dll")
[System.Reflection.Assembly]::LoadFrom((Join-Path $binDir "Wonderland Private Server.exe")) | Out-Null

Write-Host "=== TEST 1: Compound2.dat Loading & Recipe Deserialization ==="
$comp = New-Object Wonderland_Private_Server.DataManagement.DataFiles.cCompound2Dat
$compPath = Join-Path $repoRoot "Data\Compound2.dat"
$loaded = $comp.Load($compPath)
if (-not $loaded -or $comp.buildList.Count -ne 789) {
    throw "Failed to load Compound2.dat! Count: $($comp.buildList.Count)"
}
Write-Host "Compound2.dat loaded successfully: $($comp.buildList.Count) records."

# Check record 0 (header)
$rec0 = $comp.buildList[0]
if ($rec0.resultID -ne 0 -or $rec0.toolID -ne 0) {
    throw "Record 0 is not zeroed! resultID=$($rec0.resultID)"
}
Write-Host "Record 0 validated as zeroed header."

# Check recipe 526 (Stone Knife: mats 43002 x1, 45007 x1)
$rec526 = $comp.buildList[526]
if ($rec526.resultID -ne 38058 -or $rec526.materialID1 -ne 43002 -or $rec526.materialAmmt1 -ne 1 -or $rec526.materialID2 -ne 45007 -or $rec526.materialAmmt2 -ne 1) {
    throw "Recipe 526 mismatch! result=$($rec526.resultID), mat1=$($rec526.materialID1)x$($rec526.materialAmmt1), mat2=$($rec526.materialID2)x$($rec526.materialAmmt2)"
}
Write-Host "Recipe 526 validated: Result 38058, Tool $($rec526.toolID), Mats: 43002 x1, 45007 x1."

# Check recipe 744 (Raft: mats 45007 x5, 37132 x9)
$rec744 = $comp.buildList[744]
if ($rec744.resultID -ne 48010 -or $rec744.materialID1 -ne 45007 -or $rec744.materialAmmt1 -ne 5 -or $rec744.materialID2 -ne 37132 -or $rec744.materialAmmt2 -ne 9) {
    throw "Recipe 744 mismatch! result=$($rec744.resultID), mat1=$($rec744.materialID1)x$($rec744.materialAmmt1), mat2=$($rec744.materialID2)x$($rec744.materialAmmt2)"
}
Write-Host "Recipe 744 validated: Result 48010, Tool $($rec744.toolID), Mats: 45007 x5, 37132 x9."

Write-Host "`n=== TEST 2: 2D Inventory Grid & Raft Multi-Cell Footprint ==="
$itemDat = New-Object DataFiles.PhxItemDat
$loadTask = $itemDat.Load((Join-Path $repoRoot "Data\Item.dat"))
$loadTask.GetAwaiter().GetResult() | Out-Null
Write-Host "Item.dat loaded."

$dummyPlayer = New-Object Game.Player
$inv = New-Object Game.Code.Inventory($dummyPlayer, $itemDat)

# Verify Raft width and height
$w = $inv.GetItemWidth(48010)
$h = $inv.GetItemHeight(48010)
if ($w -ne 4 -or $h -ne 3) {
    throw "Raft dimensions incorrect! W=$w, H=$h (Expected 4x3)"
}
Write-Host "Raft (48010) dimensions confirmed: ${w}x${h} cells."

# Add Raft to inventory
$added = $inv.AddItem(48010, 1, $false)
if ($added -le 0) {
    throw "Failed to add Raft to inventory!"
}
Write-Host "Raft added to inventory."

# Verify that slots in 4x3 area are locked
$raftSlot = 0
for ($s = 1; $s -le 50; $s++) {
    if ($inv[$s].ItemID -eq 48010) {
        $raftSlot = $s
        break
    }
}
Write-Host "Raft root slot: $raftSlot"
$col = [Game.Code.Inventory]::GetCol($raftSlot)
$row = [Game.Code.Inventory]::GetRow($raftSlot)

$slaveCount = 0
for ($r = 0; $r -lt 3; $r++) {
    for ($c = 0; $c -lt 4; $c++) {
        $slotNum = [Game.Code.Inventory]::GetSlot($col + $c, $row + $r)
        $item = $inv[$slotNum]
        if ($slotNum -eq $raftSlot) {
            if ($item.ItemID -ne 48010 -or $item.isLocked) {
                throw "Master slot $slotNum corrupted!"
            }
        } else {
            if ($item.Parent -ne $raftSlot -or -not $item.isLocked) {
                throw "Slave slot $slotNum not locked! Parent=$($item.Parent), isLocked=$($item.isLocked)"
            }
            $slaveCount++
        }
    }
}
if ($slaveCount -ne 11) {
    throw "Expected 11 locked slave slots, found $slaveCount!"
}
Write-Host "All 11 slave cells in 4x3 grid locked successfully to root slot $raftSlot."

# Verify collision prevention: CanFitItem must reject placing another item into slave slots
$fitCheck = $inv.CanFitItem(43002, [byte]($raftSlot + 1))
if ($fitCheck) {
    throw "CanFitItem failed! Item was allowed inside locked Raft footprint slot!"
}
Write-Host "Collision protection verified: adjacent overlapping slots strictly rejected."

Write-Host "`n=== TEST 3: AC 64:1 Crafting Packet Processing ==="
$cGlobalType = [System.AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetTypes() } | Where-Object { $_.Name -eq "cGlobal" } | Select-Object -First 1
$cGlobalType.GetField("gCompoundDat", [System.Reflection.BindingFlags]"Public,Static").SetValue($null, $comp)
$cGlobalType.GetField("ItemDatManager", [System.Reflection.BindingFlags]"Public,Static").SetValue($null, $itemDat)

$player = New-Object Game.Player
$player.CharName = "CraftTest"
$craftInv = New-Object Game.Code.Inventory($player, $itemDat)
$player.GetType().GetField("m_inv", [System.Reflection.BindingFlags]"NonPublic,Instance").SetValue($player, $craftInv)

$tent = New-Object Game.Code.Tent($player)
$player.GetType().GetField("m_tent", [System.Reflection.BindingFlags]"NonPublic,Instance").SetValue($player, $tent)

# Add required crafting materials: 43002 x1 (Iron Nail) and 45007 x1 (Lauann Wood)
$player.Inv.AddItem(43002, 1, $false) | Out-Null
$player.Inv.AddItem(45007, 1, $false) | Out-Null

if ($player.Inv.GetItemCount(43002) -lt 1 -or $player.Inv.GetItemCount(45007) -lt 1) {
    throw "Player inventory missing setup materials!"
}
Write-Host "Player inventory prepared with 43002 x1 and 45007 x1."

# Build AC 64:1 packet from live client capture:
# Hex: F4 44 1A 00 40 01 02 00 0E 02 01 00 00 00 00 00 00 02 06 01 07 01 00 00 00 00 00 00 00 00
# benchIndex = 2 (ushort: 02 00), formulaIndex = 526 (ushort: 0E 02)
$pktBytes = [byte[]]@(
    0xF4, 0x44, 0x1A, 0x00, 0x40, 0x01,
    0x02, 0x00, 0x0E, 0x02, 0x01,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x02, 0x06, 0x01, 0x07, 0x01,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
)
$rxPkt = New-Object Network.RecievePacket (,$pktBytes)

$ac64 = New-Object Network.ActionCodes.AC64
$ac64.ProcessPkt($player, $rxPkt)

# Verify materials consumed
if ($player.Inv.GetItemCount(43002) -ne 0 -or $player.Inv.GetItemCount(45007) -ne 0) {
    throw "Materials were not consumed during crafting!"
}
Write-Host "Materials 43002 and 45007 consumed successfully."

# Verify crafted item 38058 was added to inventory
if ($player.Inv.GetItemCount(38058) -ne 1) {
    throw "Crafted item 38058 was not delivered to player inventory!"
}
Write-Host "Crafted item 38058 (Stone Knife) successfully delivered to player inventory!"

Write-Host "`nALL CRAFTING & RAFT FOOTPRINT TESTS PASSED EMPIRICALLY!"
