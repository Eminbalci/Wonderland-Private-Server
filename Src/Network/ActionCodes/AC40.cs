using System;
using Game;
using Game.Crafting;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 40: Item Synthesis & Alchemy Composition Protocol.
    /// Handles combining raw materials and compound items into crafted products via Alchemy.
    /// </summary>
    public class AC40 : AC
    {
        public override int ID => 40;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            byte slot1 = 0;
            byte slot2 = 0;

            if (p.Buffer.Length >= 8)
            {
                slot1 = p.Unpack8();
                slot2 = p.Unpack8();
            }

            DebugSystem.Write($"[AC40] Alchemy synthesis from {c.CharName}: SubCode={subCode}, Slot1={slot1}, Slot2={slot2}");

            try
            {
                ushort item1 = c.Inv.GetItemIdAtSlot(slot1);
                ushort item2 = c.Inv.GetItemIdAtSlot(slot2);

                bool synthesized = false;
                ushort resultItem = 0;

                if (item1 > 0 && item2 > 0)
                {
                    // Use AlchemyManager to resolve synthesis
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
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
