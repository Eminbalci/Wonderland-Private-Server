using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Battle
{
    public static class AoeTargetResolver
    {
        public static byte GetOppositeRow(byte gridX)
        {
            switch (gridX)
            {
                case 1: return 2; // Back row enemy -> Front row enemy
                case 2: return 1; // Front row enemy -> Back row enemy
                case 3: return 4; // Front row player/pet -> Back row player
                case 4: return 3; // Back row player -> Front row player/pet
                default: return gridX;
            }
        }

        public static List<BattleFighter> ResolveTargets(
            BattleFighter primaryTarget,
            byte pattern,
            IEnumerable<BattleFighter> candidatePool)
        {
            var targets = new List<BattleFighter>();
            if (primaryTarget == null) return targets;

            targets.Add(primaryTarget);

            if (pattern <= 1)
            {
                return targets;
            }

            var candidates = (candidatePool ?? Enumerable.Empty<BattleFighter>())
                .Where(f => f != null && f != primaryTarget)
                .ToList();

            byte px = primaryTarget.GridX;
            byte py = primaryTarget.GridY;
            byte oppX = GetOppositeRow(px);

            switch (pattern)
            {
                case 2: // Column (2 slots: front & back in same column Y)
                    {
                        var colMate = candidates.FirstOrDefault(f => f.GridX == oppX && f.GridY == py);
                        if (colMate != null) targets.Add(colMate);
                        break;
                    }

                case 3: // Row (horizontal line along same row X, primary Y + adjacent Ys)
                    {
                        // Priority to immediate adjacent slots (py - 1, py + 1)
                        var left = candidates.FirstOrDefault(f => f.GridX == px && f.GridY == py - 1);
                        var right = candidates.FirstOrDefault(f => f.GridX == px && f.GridY == py + 1);
                        if (left != null) targets.Add(left);
                        if (right != null) targets.Add(right);

                        // If not full 3 slots and an adjacent was missing, check distance 2 on the side with space
                        if (targets.Count < 3)
                        {
                            var farLeft = candidates.FirstOrDefault(f => f.GridX == px && f.GridY == py - 2);
                            var farRight = candidates.FirstOrDefault(f => f.GridX == px && f.GridY == py + 2);
                            if (farLeft != null && !targets.Contains(farLeft)) targets.Add(farLeft);
                            if (targets.Count < 3 && farRight != null && !targets.Contains(farRight)) targets.Add(farRight);
                        }
                        break;
                    }

                case 4: // Cross / Sideways T shape (4 slots: row Y-1, Y+1, and opposite row same Y)
                    {
                        var oppCol = candidates.FirstOrDefault(f => f.GridX == oppX && f.GridY == py);
                        if (oppCol != null) targets.Add(oppCol);

                        var left = candidates.FirstOrDefault(f => f.GridX == px && f.GridY == py - 1);
                        var right = candidates.FirstOrDefault(f => f.GridX == px && f.GridY == py + 1);
                        if (left != null) targets.Add(left);
                        if (right != null) targets.Add(right);

                        // If any slot was empty, expand to opposite row adjacents to cover 4 targets if available
                        if (targets.Count < 4)
                        {
                            var oppLeft = candidates.FirstOrDefault(f => f.GridX == oppX && f.GridY == py - 1);
                            var oppRight = candidates.FirstOrDefault(f => f.GridX == oppX && f.GridY == py + 1);
                            if (oppLeft != null && !targets.Contains(oppLeft)) targets.Add(oppLeft);
                            if (targets.Count < 4 && oppRight != null && !targets.Contains(oppRight)) targets.Add(oppRight);
                        }
                        break;
                    }

                case 5: // 5 targets (Cross + adjacent)
                    {
                        // Primary column
                        var oppCol = candidates.FirstOrDefault(f => f.GridX == oppX && f.GridY == py);
                        if (oppCol != null) targets.Add(oppCol);

                        // Primary row neighbors
                        var left = candidates.FirstOrDefault(f => f.GridX == px && f.GridY == py - 1);
                        var right = candidates.FirstOrDefault(f => f.GridX == px && f.GridY == py + 1);
                        if (left != null) targets.Add(left);
                        if (right != null) targets.Add(right);

                        // Opposite row neighbors
                        var oppLeft = candidates.FirstOrDefault(f => f.GridX == oppX && f.GridY == py - 1);
                        var oppRight = candidates.FirstOrDefault(f => f.GridX == oppX && f.GridY == py + 1);
                        if (oppLeft != null && !targets.Contains(oppLeft)) targets.Add(oppLeft);
                        if (targets.Count < 5 && oppRight != null && !targets.Contains(oppRight)) targets.Add(oppRight);

                        // Fallback to remaining candidates sorted by grid distance
                        if (targets.Count < 5)
                        {
                            var remaining = candidates
                                .Where(c => !targets.Contains(c))
                                .OrderBy(c => Math.Abs(c.GridX - px) + Math.Abs(c.GridY - py))
                                .Take(5 - targets.Count);
                            targets.AddRange(remaining);
                        }
                        break;
                    }

                case 6: // 6 targets (3 columns x 2 rows centered on py)
                    {
                        var targetYs = new[] { py, (byte)(py - 1), (byte)(py + 1), (byte)(py - 2), (byte)(py + 2) }
                            .Where(y => y >= 1 && y <= 4)
                            .Distinct()
                            .ToList();

                        foreach (var y in targetYs)
                        {
                            var fPx = candidates.FirstOrDefault(f => f.GridX == px && f.GridY == y);
                            var fOpp = candidates.FirstOrDefault(f => f.GridX == oppX && f.GridY == y);
                            if (fPx != null && !targets.Contains(fPx) && targets.Count < 6) targets.Add(fPx);
                            if (fOpp != null && !targets.Contains(fOpp) && targets.Count < 6) targets.Add(fOpp);
                        }

                        // Fallback to remaining candidates if not yet 6
                        if (targets.Count < 6)
                        {
                            var remaining = candidates
                                .Where(c => !targets.Contains(c))
                                .OrderBy(c => Math.Abs(c.GridX - px) + Math.Abs(c.GridY - py))
                                .Take(6 - targets.Count);
                            targets.AddRange(remaining);
                        }
                        break;
                    }

                case 7: // Full Team / All targets
                case 8: // All targets
                default:
                    {
                        // Add all living candidate fighters
                        targets.AddRange(candidates);
                        break;
                    }
            }

            return targets;
        }
    }
}
