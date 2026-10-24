using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wlo.pserver.core.Game.Enums;
using wlo.pserver.core;
using Network;
using Game;
using Wonderland_Private_Server.DataManagement.DataFiles;

namespace Network.ActionCodes
{
    public class AC64 : AC
    {
        public override int ID { get { return 64; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            if (r == null || p == null) return;
            p.SetPtr(6); // Skip packet header (4B), ActionCode (1B), SubCode (1B)

            switch (p.B)
            {
                case 1: Recv1(r, p); break; // Create new object tent (start manufacture)
                case 2: Recv2(r, p); break; // Continue build
                case 3: Recv3(r, p); break; // Stop build
                default: DebugSystem.Write(DebugItemType.Error, $"[AC64] AC {p.A},{p.B} has not been coded"); break;
            }
        }

        private void Recv1(Player r, RecievePacket p)
        {
            try
            {
                if (r.Tent == null) return;

                ushort benchIndex = p.Unpack16();
                ushort formulaIndex = p.Unpack16();

                DebugSystem.Write(DebugItemType.Error, $"[AC64] Recv1: BenchIndex={benchIndex}, FormulaIndex={formulaIndex} for {r.CharName}");

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

                DebugSystem.Write(DebugItemType.Error, $"[AC64] Crafting ResultID={formula.resultID}, ToolID={formula.toolID}, Mats: [({formula.materialID1}x{formula.materialAmmt1}), ({formula.materialID2}x{formula.materialAmmt2}), ({formula.materialID3}x{formula.materialAmmt3}), ({formula.materialID4}x{formula.materialAmmt4}), ({formula.materialID5}x{formula.materialAmmt5})]");

                // Check material requirements
                bool hasMats = true;
                if (formula.materialID1 > 0 && formula.materialAmmt1 > 0 && r.Inv.GetItemCount(formula.materialID1) < formula.materialAmmt1) hasMats = false;
                if (formula.materialID2 > 0 && formula.materialAmmt2 > 0 && r.Inv.GetItemCount(formula.materialID2) < formula.materialAmmt2) hasMats = false;
                if (formula.materialID3 > 0 && formula.materialAmmt3 > 0 && r.Inv.GetItemCount(formula.materialID3) < formula.materialAmmt3) hasMats = false;
                if (formula.materialID4 > 0 && formula.materialAmmt4 > 0 && r.Inv.GetItemCount(formula.materialID4) < formula.materialAmmt4) hasMats = false;
                if (formula.materialID5 > 0 && formula.materialAmmt5 > 0 && r.Inv.GetItemCount(formula.materialID5) < formula.materialAmmt5) hasMats = false;

                if (!hasMats)
                {
                    SendSystemMsg(r, "Yetersiz malzeme!");
                    return;
                }

                // Deduct materials safely across inventory slots
                if (formula.materialID1 > 0 && formula.materialAmmt1 > 0) r.Inv.RemoveItem(formula.materialID1, formula.materialAmmt1);
                if (formula.materialID2 > 0 && formula.materialAmmt2 > 0) r.Inv.RemoveItem(formula.materialID2, formula.materialAmmt2);
                if (formula.materialID3 > 0 && formula.materialAmmt3 > 0) r.Inv.RemoveItem(formula.materialID3, formula.materialAmmt3);
                if (formula.materialID4 > 0 && formula.materialAmmt4 > 0) r.Inv.RemoveItem(formula.materialID4, formula.materialAmmt4);
                if (formula.materialID5 > 0 && formula.materialAmmt5 > 0) r.Inv.RemoveItem(formula.materialID5, formula.materialAmmt5);

                byte count = formula.ammtRecv > 0 ? formula.ammtRecv : (byte)1;
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

                if (isFurniture)
                {
                    r.Tent.PlaceItem(outId, 42, 42, 0, 0);
                    r.Tent.SendTentItemsToPlayer(r);
                    cGlobal.gCharacterDataBase?.SaveTentData(r);

                    Send64_1(r, (byte)benchIndex, outId);
                    Send64_2(r, (byte)benchIndex);
                    Send64_4(r, (byte)benchIndex);
                }
                else
                {
                    r.Inv.AddItem(outId, count);
                    Send64_9(r);
                }

                // Play crafting sparkles effect AC 5:5
                r.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, r.CharID, (ushort)60018));
                SendSystemMsg(r, $"Uretim tamamlandi! (Item ID: {outId})");
                r.SaveCharacterData();
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
                byte nkey = p.Unpack8();
                Send64_2(r, nkey);
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
                byte nkey = p.Unpack8();
                Send64_4(r, nkey);
            }
            catch (Exception t)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC64] Error in Recv3: {t.Message}");
            }
        }

        private static void Send64_1(Player p, byte nkey, ushort itemId)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 64, 1 });
            s.Pack8(nkey);
            s.Pack16(itemId);
            s.Pack32(0);
            s.Pack8(1);
            p.Send(s);
        }

        private static void Send64_2(Player p, byte nkey)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 64, 2 });
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
