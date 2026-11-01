$path = "wlo.pserver.core\Game\Maps\Map.cs"
$content = [System.IO.File]::ReadAllText($path)

$target = @"
            var currentWarpLoc = mapData?.WarpLoc;
            if (currentWarpLoc != null && currentWarpLoc.Count > 0)
            {
                // 3. Priority A: Geometric reverse matching based on player stepping position (px, py)
                if (px > 0 && py > 0)
                {
                    DataFiles.WarpInfo bestWarp = null;
                    double bestDist = 999999;

                    foreach (var w in currentWarpLoc)
                    {
                        if (w.mapID == 0) continue;
                        var dstMapData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(w.mapID);
                        if (dstMapData != null && dstMapData.WarpLoc != null)
                        {
                            foreach (var revW in dstMapData.WarpLoc)
                            {
                                if (revW.mapID == MapID)
                                {
                                    double dist = Math.Sqrt(Math.Pow(px - (int)revW.x, 2) + Math.Pow(py - (int)revW.y, 2));
                                    if (dist < bestDist)
                                    {
                                        bestDist = dist;
                                        bestWarp = w;
                                    }
                                }
                            }
                        }
                    }

                    if (bestWarp != null && bestDist < 600 && bestWarp.mapID > 0)
                    {
                        dstMap = bestWarp.mapID;
                        dstX = (ushort)bestWarp.x;
                        dstY = (ushort)bestWarp.y;
                        DebugSystem.Write($"[Portal] Geometric reverse match: Map {MapID} pos({px},{py}) -> Map {dstMap} ({dstX},{dstY}) dist={bestDist:F1}");
                        return true;
                    }
                }

                // 4. Priority B: Direct match on clickID == portalID (Authentic Eve.emg Portal Click ID)
                foreach (var w in currentWarpLoc)
                {
                    if (w.clickID == portalID && w.mapID > 0)
                    {
                        dstMap = w.mapID;
                        dstX = (ushort)w.x;
                        dstY = (ushort)w.y;
                        DebugSystem.Write($"[Portal] Eve.emg 'clickID' match: Map {MapID} portal {portalID} -> Map {dstMap} ({dstX},{dstY})");
                        return true;
                    }
                }
"@

$replacement = @"
            var currentWarpLoc = mapData?.WarpLoc;
            if (currentWarpLoc != null && currentWarpLoc.Count > 0)
            {
                // 3. Priority A: Direct match on clickID == portalID (Authentic Eve.emg Portal Click ID)
                // When client sends portalID, it directly corresponds to the portal's clickID in Eve.emg.
                foreach (var w in currentWarpLoc)
                {
                    if (w.clickID == portalID && w.mapID > 0)
                    {
                        dstMap = w.mapID;
                        dstX = (ushort)w.x;
                        dstY = (ushort)w.y;
                        DebugSystem.Write($"[Portal] Eve.emg 'clickID' match: Map {MapID} portal {portalID} -> Map {dstMap} ({dstX},{dstY})");
                        return true;
                    }
                }

                // 4. Priority B: Geometric reverse matching based on player stepping position (px, py)
                // Only used as fallback when portal clickID is not matched.
                if (px > 0 && py > 0)
                {
                    DataFiles.WarpInfo bestWarp = null;
                    double bestDist = 999999;

                    foreach (var w in currentWarpLoc)
                    {
                        if (w.mapID == 0) continue;
                        var dstMapData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(w.mapID);
                        if (dstMapData != null && dstMapData.WarpLoc != null)
                        {
                            foreach (var revW in dstMapData.WarpLoc)
                            {
                                if (revW.mapID == MapID)
                                {
                                    double dist = Math.Sqrt(Math.Pow(px - (int)revW.x, 2) + Math.Pow(py - (int)revW.y, 2));
                                    if (dist < bestDist)
                                    {
                                        bestDist = dist;
                                        bestWarp = w;
                                    }
                                }
                            }
                        }
                    }

                    if (bestWarp != null && bestDist < 600 && bestWarp.mapID > 0)
                    {
                        dstMap = bestWarp.mapID;
                        dstX = (ushort)bestWarp.x;
                        dstY = (ushort)bestWarp.y;
                        DebugSystem.Write($"[Portal] Geometric reverse match: Map {MapID} pos({px},{py}) -> Map {dstMap} ({dstX},{dstY}) dist={bestDist:F1}");
                        return true;
                    }
                }
"@

$targetLf = $target.Replace("`r`n", "`n")
$replacementLf = $replacement.Replace("`r`n", "`n")
$contentLf = $content.Replace("`r`n", "`n")

if ($contentLf.Contains($targetLf)) {
    $contentLf = $contentLf.Replace($targetLf, $replacementLf)
    if ($content.Contains("`r`n")) {
        $contentLf = $contentLf.Replace("`n", "`r`n")
    }
    [System.IO.File]::WriteAllText($path, $contentLf, [System.Text.Encoding]::UTF8)
    Write-Host "Map.cs portal priority patched successfully"
} else {
    Write-Host "Target in Map.cs not found"
}
