using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using wlo.pserver.core.Game.Enums;
using wlo.pserver.core;
using Network;
using Game;
using DataFiles;
using Wonderland_Private_Server.DataManagement.DataFiles;

namespace Network.ActionCodes
{
    public class ActiveCraftSession
    {
        public uint CharID { get; set; }
        public byte BenchIndex { get; set; }
        public ushort FormulaIndex { get; set; }
        public ushort ResultID { get; set; }
        public byte Count { get; set; }
        public bool IsFurniture { get; set; }
        public int TotalDurationSeconds { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<Tuple<ushort, int>> DeductedMaterials { get; set; } = new List<Tuple<ushort, int>>();
        public Timer Timer { get; set; }
    }

    public class AC64 : AC
    {
        public override int ID { get { return 64; } }

        private static readonly ConcurrentDictionary<string, ActiveCraftSession> _activeCrafts = new ConcurrentDictionary<string, ActiveCraftSession>();
        private static bool _initialized = false;

        static AC64()
        {
            Init();
        }

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            Game.Code.Tent.OnTentItemsSent += SyncActiveCraftsToPlayer;
        }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            if (r == null || p == null) return;
            p.SetPtr(6); // Skip packet header (4B), ActionCode (1B), SubCode (1B)

            switch (p.B)
            {
                case 1: Recv1(r, p); break; // Create new object tent (start manufacture)
                case 2: Recv2(r, p); break; // Continue / check build
                case 3: Recv3(r, p); break; // Cancel build
                default: DebugSystem.Write(DebugItemType.Error, $"[AC64] AC {p.A},{p.B} has not been coded"); break;
            }
        }

        private void Recv1(Player r, RecievePacket p)
        {
            try
            {
                if (r == null || r.Tent == null) return;

                ushort benchIndex = p.Unpack16();
                ushort formulaIndex = p.Unpack16();
                byte requestedBatchCount = 1;
                if (p.GetPtr() < p.Count)
                {
                    byte b = p.Unpack8();
                    if (b > 0) requestedBatchCount = b;
                }

                DebugSystem.Write(DebugItemType.Error, $"[AC64] Recv1: BenchIndex={benchIndex}, FormulaIndex={formulaIndex}, BatchCount={requestedBatchCount} for {r.CharName}");

                cBuildElement formula = null;
                if (cGlobal.gCompoundDat != null && cGlobal.gCompoundDat.buildList != null && formulaIndex < cGlobal.gCompoundDat.buildList.Count)
                {
                    formula = cGlobal.gCompoundDat.buildList[formulaIndex];
                }

                if (formula == null || formula.resultID == 0)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[AC64] Unknown recipe formulaIndex={formulaIndex}");
                    SendSystemMsg(r, "Bilinmeyen tarif!");
                    return;
                }

                string key = $"{r.CharID}_{benchIndex}";
                if (_activeCrafts.TryGetValue(key, out var existingSession))
                {
                    if (DateTime.UtcNow < existingSession.EndTime)
                    {
                        SendSystemMsg(r, "Bu alet su anda uretim yapiyor!");
                        return;
                    }
                    else
                    {
                        CompleteCraft(existingSession);
                    }
                }

                DebugSystem.Write(DebugItemType.Error, $"[AC64] Crafting ResultID={formula.resultID}, ToolID={formula.toolID}, BuildTime={formula.buildTime}m, Mats: [({formula.materialID1}x{formula.materialAmmt1}), ({formula.materialID2}x{formula.materialAmmt2}), ({formula.materialID3}x{formula.materialAmmt3}), ({formula.materialID4}x{formula.materialAmmt4}), ({formula.materialID5}x{formula.materialAmmt5})]");

                // Check material requirements
                bool hasMats = true;
                if (formula.materialID1 > 0 && formula.materialAmmt1 > 0 && r.Inv.GetItemCount(formula.materialID1) < formula.materialAmmt1 * requestedBatchCount) hasMats = false;
                if (formula.materialID2 > 0 && formula.materialAmmt2 > 0 && r.Inv.GetItemCount(formula.materialID2) < formula.materialAmmt2 * requestedBatchCount) hasMats = false;
                if (formula.materialID3 > 0 && formula.materialAmmt3 > 0 && r.Inv.GetItemCount(formula.materialID3) < formula.materialAmmt3 * requestedBatchCount) hasMats = false;
                if (formula.materialID4 > 0 && formula.materialAmmt4 > 0 && r.Inv.GetItemCount(formula.materialID4) < formula.materialAmmt4 * requestedBatchCount) hasMats = false;
                if (formula.materialID5 > 0 && formula.materialAmmt5 > 0 && r.Inv.GetItemCount(formula.materialID5) < formula.materialAmmt5 * requestedBatchCount) hasMats = false;

                if (!hasMats)
                {
                    SendSystemMsg(r, "Yetersiz malzeme!");
                    return;
                }

                // Deduct materials safely across inventory slots
                var deductedMats = new List<Tuple<ushort, int>>();
                if (formula.materialID1 > 0 && formula.materialAmmt1 > 0)
                {
                    int ammt = formula.materialAmmt1 * requestedBatchCount;
                    r.Inv.RemoveItem(formula.materialID1, (byte)ammt);
                    deductedMats.Add(Tuple.Create(formula.materialID1, ammt));
                }
                if (formula.materialID2 > 0 && formula.materialAmmt2 > 0)
                {
                    int ammt = formula.materialAmmt2 * requestedBatchCount;
                    r.Inv.RemoveItem(formula.materialID2, (byte)ammt);
                    deductedMats.Add(Tuple.Create(formula.materialID2, ammt));
                }
                if (formula.materialID3 > 0 && formula.materialAmmt3 > 0)
                {
                    int ammt = formula.materialAmmt3 * requestedBatchCount;
                    r.Inv.RemoveItem(formula.materialID3, (byte)ammt);
                    deductedMats.Add(Tuple.Create(formula.materialID3, ammt));
                }
                if (formula.materialID4 > 0 && formula.materialAmmt4 > 0)
                {
                    int ammt = formula.materialAmmt4 * requestedBatchCount;
                    r.Inv.RemoveItem(formula.materialID4, (byte)ammt);
                    deductedMats.Add(Tuple.Create(formula.materialID4, ammt));
                }
                if (formula.materialID5 > 0 && formula.materialAmmt5 > 0)
                {
                    int ammt = formula.materialAmmt5 * requestedBatchCount;
                    r.Inv.RemoveItem(formula.materialID5, (byte)ammt);
                    deductedMats.Add(Tuple.Create(formula.materialID5, ammt));
                }

                byte outputCount = (byte)(requestedBatchCount * (formula.ammtRecv > 0 ? formula.ammtRecv : 1));
                ushort outId = formula.resultID;

                var itemData = cGlobal.ItemDatManager?.GetItemByID(outId);
                bool isFurniture = false;
                if (itemData != null)
                {
                    // Item types: 29 = Manufacture_Tool, 30 = Tent_Decoration
                    // Multi-cell furniture appliances go to tent floor.
                    // Vehicles (39, e.g. Raft 48010), tools (w=1, h=1), materials, and equipment go to inventory bag.
                    if ((itemData.ItemType == 29 || itemData.ItemType == 30) && itemData.ItemType != 39 && (itemData.cellwidth > 1 || itemData.cellheight > 1))
                    {
                        isFurniture = true;
                    }
                }

                // In Compound2.dat, formula.buildTime is in minutes.
                int durationSeconds = formula.buildTime > 0 ? (int)formula.buildTime * 60 * requestedBatchCount : 0;

                // Immediate craft for 0-duration recipes
                if (durationSeconds == 0)
                {
                    if (isFurniture)
                    {
                        PlaceFurnitureInTent(r, outId, itemData);
                        Send64_1(r, (byte)benchIndex, outId, 0, requestedBatchCount);
                        Send64_2(r, (byte)benchIndex);
                        Send64_4(r, (byte)benchIndex);
                    }
                    else
                    {
                        r.Inv.AddItem(outId, outputCount);
                        Send64_9(r);
                    }

                    r.CurMap?.Broadcast(Tools.FromFormat("bbd", 23, 122, r.CharID));
                    string itemName = GetItemDisplayName(itemData, outId);
                    SendSystemMsg(r, $"Uretim tamamlandi! ({itemName})");
                    r.SaveCharacterData();
                    return;
                }

                // Timed manufacturing craft
                var session = new ActiveCraftSession
                {
                    CharID = r.CharID,
                    BenchIndex = (byte)benchIndex,
                    FormulaIndex = formulaIndex,
                    ResultID = outId,
                    Count = outputCount,
                    IsFurniture = isFurniture,
                    TotalDurationSeconds = durationSeconds,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddSeconds(durationSeconds),
                    DeductedMaterials = deductedMats
                };

                _activeCrafts[key] = session;

                Send64_1(r, (byte)benchIndex, outId, (uint)durationSeconds, requestedBatchCount);
                Send64_2(r, (byte)benchIndex);

                string startingName = GetItemDisplayName(itemData, outId);
                SendSystemMsg(r, $"Uretim basladi: {startingName} ({durationSeconds} saniye)");

                session.Timer = new Timer(_ =>
                {
                    try
                    {
                        CompleteCraft(session);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[AC64] Error in craft timer callback: {ex.Message}");
                    }
                }, null, durationSeconds * 1000, Timeout.Infinite);
            }
            catch (Exception t)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC64] Error in Recv1: {t.Message}");
            }
        }

        private void Recv2(Player r, RecievePacket p)
        {
            try
            {
                byte nkey = 0;
                if (p.GetPtr() < p.Count)
                {
                    nkey = p.Unpack8();
                }

                string key = $"{r.CharID}_{nkey}";
                if (_activeCrafts.TryGetValue(key, out var session))
                {
                    if (DateTime.UtcNow >= session.EndTime)
                    {
                        CompleteCraft(session);
                    }
                    else
                    {
                        uint remaining = (uint)Math.Max(1, (session.EndTime - DateTime.UtcNow).TotalSeconds);
                        Send64_1(r, session.BenchIndex, session.ResultID, remaining, session.Count);
                        Send64_2(r, session.BenchIndex);
                    }
                }
                else
                {
                    Send64_2(r, nkey);
                }
            }
            catch (Exception t)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC64] Error in Recv2: {t.Message}");
            }
        }

        private void Recv3(Player r, RecievePacket p)
        {
            try
            {
                byte nkey = 0;
                if (p.GetPtr() < p.Count)
                {
                    nkey = p.Unpack8();
                }

                string key = $"{r.CharID}_{nkey}";
                if (_activeCrafts.TryRemove(key, out var session))
                {
                    session.Timer?.Dispose();

                    // Refund materials on cancel
                    if (session.DeductedMaterials != null && session.DeductedMaterials.Count > 0)
                    {
                        foreach (var mat in session.DeductedMaterials)
                        {
                            r.Inv.AddItem(mat.Item1, (byte)mat.Item2);
                        }
                    }

                    Send64_3(r, nkey);
                    SendSystemMsg(r, "Uretim iptal edildi. Malzemeler iade edildi.");
                }
                else
                {
                    Send64_3(r, nkey);
                }
            }
            catch (Exception t)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC64] Error in Recv3: {t.Message}");
            }
        }

        public static void CompleteCraft(ActiveCraftSession session)
        {
            if (session == null) return;
            string key = $"{session.CharID}_{session.BenchIndex}";
            if (!_activeCrafts.TryRemove(key, out var active))
            {
                return; // Already completed or cancelled
            }
            active.Timer?.Dispose();

            Player p = cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.FirstOrDefault(x => x != null && x.CharID == session.CharID);

            if (p == null)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC64] Player {session.CharID} offline when craft completed (ResultID={session.ResultID})");
                return;
            }

            var itemData = cGlobal.ItemDatManager?.GetItemByID(session.ResultID);
            string itemName = GetItemDisplayName(itemData, session.ResultID);

            if (session.IsFurniture)
            {
                PlaceFurnitureInTent(p, session.ResultID, itemData);
                Send64_4(p, session.BenchIndex);
            }
            else
            {
                p.Inv.AddItem(session.ResultID, session.Count);
                Send64_4(p, session.BenchIndex);
                Send64_9(p);
            }

            // Play golden sparkles level-up / craft completion effect AC 23:122
            p.CurMap?.Broadcast(Tools.FromFormat("bbd", 23, 122, p.CharID));
            SendSystemMsg(p, $"Uretim tamamlandi! ({itemName})");
            p.SaveCharacterData();
        }

        public static void SyncActiveCraftsToPlayer(Player player)
        {
            if (player == null) return;
            try
            {
                foreach (var kvp in _activeCrafts)
                {
                    var session = kvp.Value;
                    if (session.CharID == player.CharID)
                    {
                        if (DateTime.UtcNow >= session.EndTime)
                        {
                            CompleteCraft(session);
                        }
                        else
                        {
                            uint remaining = (uint)Math.Max(1, (session.EndTime - DateTime.UtcNow).TotalSeconds);
                            Send64_1(player, session.BenchIndex, session.ResultID, remaining, session.Count);
                            Send64_2(player, session.BenchIndex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC64] Error in SyncActiveCraftsToPlayer: {ex.Message}");
            }
        }

        private static void PlaceFurnitureInTent(Player r, ushort itemId, PhxItemInfo itemData)
        {
            if (r == null || r.Tent == null) return;
            int targetX = 42;
            int targetY = 42;
            int itemW = itemData != null && itemData.cellwidth > 0 ? itemData.cellwidth : 1;
            int itemH = itemData != null && itemData.cellheight > 0 ? itemData.cellheight : 1;

            if (r.Tent.IsAreaOccupied(targetX, targetY, itemW, itemH, 0))
            {
                bool found = false;
                for (int ty = 38; ty <= 50 && !found; ty += 2)
                {
                    for (int tx = 29; tx <= 44 && !found; tx += 2)
                    {
                        if (r.Tent.IsInFloorBounds(tx, ty, itemW, itemH, 0) && !r.Tent.IsAreaOccupied(tx, ty, itemW, itemH, 0))
                        {
                            targetX = tx;
                            targetY = ty;
                            found = true;
                        }
                    }
                }
            }

            r.Tent.PlaceItem(itemId, targetX, targetY, 0, 0);
            r.Tent.SendTentItemsToPlayer(r);
            cGlobal.gCharacterDataBase?.SaveTentData(r);
        }

        private static string GetItemDisplayName(PhxItemInfo itemData, ushort itemId)
        {
            if (itemData != null && itemData.ItemName != null && itemData.ItemName.Length > 0)
            {
                string raw = Encoding.ASCII.GetString(itemData.ItemName).TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(raw)) return raw;
            }
            return $"#{itemId}";
        }

        private static void Send64_1(Player p, byte nkey, ushort itemId, uint durationSeconds, byte count)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 64, 1 });
            s.Pack8(nkey);
            s.Pack16(itemId);
            s.Pack32(durationSeconds);
            s.Pack8(count > 0 ? count : (byte)1);
            p.Send(s);
        }

        private static void Send64_2(Player p, byte nkey)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 64, 2 });
            s.Pack8(nkey);
            p.Send(s);
        }

        private static void Send64_3(Player p, byte nkey)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 64, 3 });
            s.Pack8(nkey);
            p.Send(s);
        }

        private static void Send64_4(Player p, byte nkey)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 64, 4 });
            s.Pack8(nkey);
            p.Send(s);
        }

        private static void Send64_9(Player p)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 64, 9 });
            p.Send(s);
        }

        private static void SendSystemMsg(Player p, string msg)
        {
            if (p == null || string.IsNullOrEmpty(msg)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(msg);
            p.Send(s);
        }
    }
}
