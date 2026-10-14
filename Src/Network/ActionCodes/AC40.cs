using System;
using Game;
using Game.Crafting;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 40: Item Synthesis, Alchemy & Equipment Forging Protocol.
    /// Handles combining raw materials via Alchemy and equipment durability repair/forge upgrades.
    /// </summary>
    public class AC40 : AC
    {
        public override int ID => 40;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC40] Synthesis/Forging action from {c.CharName}: SubCode={subCode}");

            try
            {
                switch (subCode)
                {
                    case 1: // Synthesis / Alchemy Composition
                        ProcessSynthesis(c, p, subCode);
                        break;

                    case 2: // Equipment Durability Repair
                        ProcessRepair(c, p, subCode);
                        break;

                    case 3: // Equipment Forging Upgrade
                        ProcessUpgrade(c, p, subCode);
                        break;

                    default:
                        SendPacket defResp = new SendPacket();
                        defResp.Pack8((byte)ID);
                        defResp.Pack8(subCode);
                        defResp.Pack8(1);
                        c.Send(defResp);
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void ProcessSynthesis(Player c, RecievePacket p, byte subCode)
        {
            byte slot1 = 0;
            byte slot2 = 0;

            if (p.Buffer.Length >= 8)
            {
                slot1 = p.Unpack8();
                slot2 = p.Unpack8();
            }

            ushort item1 = c.Inv.GetItemIdAtSlot(slot1);
            ushort item2 = c.Inv.GetItemIdAtSlot(slot2);

            bool synthesized = false;
            ushort resultItem = 0;

            if (item1 > 0 && item2 > 0)
            {
                var recipe = AlchemyManager.FindRecipe(item1, item2);
                if (recipe != null)
                {
                    c.Inv.RemoveItemAtSlot(slot1, 1);
                    c.Inv.RemoveItemAtSlot(slot2, 1);
                    c.Inv.AddItem(recipe.OutputItem, 1);
                    resultItem = recipe.OutputItem;
                    synthesized = true;
                }
            }

            SendPacket resp = new SendPacket();
            resp.Pack8((byte)ID);
            resp.Pack8(subCode);
            resp.Pack8(synthesized ? (byte)1 : (byte)0);
            resp.Pack16(resultItem);
            c.Send(resp);
        }

        private void ProcessRepair(Player c, RecievePacket p, byte subCode)
        {
            byte equipSlot = 0;
            ushort hammerItemId = 25001; // Default Repair Hammer

            if (p.Buffer != null && p.Buffer.Length >= 8)
            {
                equipSlot = p.Unpack8();
            }

            bool repaired = false;
            if (ExperimentalFeaturesManager.Instance.EnableEquipmentForge)
            {
                repaired = ExperimentalFeaturesManager.Instance.EquipmentForge.RepairEquipment(c, equipSlot, hammerItemId);
            }

            SendPacket resp = new SendPacket();
            resp.Pack8((byte)ID);
            resp.Pack8(subCode);
            resp.Pack8(equipSlot);
            resp.Pack8(repaired ? (byte)1 : (byte)0);
            c.Send(resp);
        }

        private void ProcessUpgrade(Player c, RecievePacket p, byte subCode)
        {
            byte equipSlot = 0;
            ushort catalystItemId = 27001; // Forging Catalyst

            if (p.Buffer != null && p.Buffer.Length >= 8)
            {
                equipSlot = p.Unpack8();
            }

            byte successCode = 0;
            byte newTier = 0;

            if (ExperimentalFeaturesManager.Instance.EnableEquipmentForge)
            {
                var result = ExperimentalFeaturesManager.Instance.EquipmentForge.ForgeUpgrade(c, equipSlot, catalystItemId);
                newTier = result.NewTier;
                switch (result.Outcome)
                {
                    case ForgeOutcome.Success: successCode = 1; break;
                    case ForgeOutcome.FailureNoLoss: successCode = 2; break;
                    case ForgeOutcome.FailureDowngrade: successCode = 3; break;
                    case ForgeOutcome.Broken: successCode = 4; break;
                }
            }

            SendPacket resp = new SendPacket();
            resp.Pack8((byte)ID);
            resp.Pack8(subCode);
            resp.Pack8(equipSlot);
            resp.Pack8(successCode);
            resp.Pack8(newTier);
            c.Send(resp);
        }
    }
}
