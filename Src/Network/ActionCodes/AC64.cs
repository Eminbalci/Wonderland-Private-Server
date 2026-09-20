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

                byte benchIndex = p.Unpack8();
                ushort formulaIndex = p.Unpack16();

                DebugSystem.Write(DebugItemType.Error, $"[AC64] Recv1: BenchIndex={benchIndex}, FormulaIndex={formulaIndex} for {r.CharName}");

                cBuildElement formula = null;
                if (cGlobal.gCompoundDat != null && cGlobal.gCompoundDat.buildList != null && formulaIndex < cGlobal.gCompoundDat.buildList.Count)
                {
                    formula = cGlobal.gCompoundDat.buildList[formulaIndex];
                }

                if (formula != null)
                {
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

                    // Deduct materials
                    if (formula.materialID1 > 0 && formula.materialAmmt1 > 0) r.Inv.RemoveItemById(formula.materialID1, formula.materialAmmt1);
                    if (formula.materialID2 > 0 && formula.materialAmmt2 > 0) r.Inv.RemoveItemById(formula.materialID2, formula.materialAmmt2);
                    if (formula.materialID3 > 0 && formula.materialAmmt3 > 0) r.Inv.RemoveItemById(formula.materialID3, formula.materialAmmt3);
                    if (formula.materialID4 > 0 && formula.materialAmmt4 > 0) r.Inv.RemoveItemById(formula.materialID4, formula.materialAmmt4);
                    if (formula.materialID5 > 0 && formula.materialAmmt5 > 0) r.Inv.RemoveItemById(formula.materialID5, formula.materialAmmt5);

                    byte count = formula.ammtRecv > 0 ? formula.ammtRecv : (byte)1;
                    ushort outId = formula.resultID;

                    if (CheckSpecialTool(outId))
                    {
                        r.Inv.AddItem(outId, count);
                        Send64_9(r);
                    }
                    else
                    {
                        r.Tent.PlaceItem(outId, 42, 42, 0, 0);
                        r.Tent.SendTentItemsToPlayer(r);
                        cGlobal.gCharacterDataBase?.SaveTentData(r);

                        Send64_1(r, benchIndex, outId);
                        Send64_2(r, benchIndex);
                        Send64_4(r, benchIndex);
                    }

                    // Play crafting sparkles effect AC 5:5
                    r.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, r.CharID, (ushort)60018));
                    SendSystemMsg(r, $"Uretim tamamlandi! (Item ID: {outId})");
                    r.SaveCharacterData();
                }
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

        private static bool CheckSpecialTool(ushort itemId)
        {
            switch (itemId)
            {
                case 38004: // Great Driver
                case 38031: // Brand Iron
                case 38035: // Rope Saw
                case 38036: // Wooden Saw
                case 38037: // Whetstone
                case 38040: // Needle
                case 38054: // Pliers
                case 38058: // Stone Knife
                    return true;
                default:
                    return false;
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
