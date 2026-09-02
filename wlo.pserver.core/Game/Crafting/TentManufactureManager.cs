using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.Crafting
{
    public class ManufactureRecipe
    {
        public string WorkbenchName { get; set; }
        public ushort InputItem1 { get; set; }
        public byte Count1 { get; set; }
        public ushort InputItem2 { get; set; }
        public byte Count2 { get; set; }
        public ushort OutputItem { get; set; }
        public string OutputName { get; set; }

        public ManufactureRecipe(string bench, ushort in1, byte c1, ushort in2, byte c2, ushort outIt, string outName)
        {
            WorkbenchName = bench;
            InputItem1 = in1;
            Count1 = c1;
            InputItem2 = in2;
            Count2 = c2;
            OutputItem = outIt;
            OutputName = outName;
        }
    }

    public static class TentManufactureManager
    {
        private static readonly List<ManufactureRecipe> _recipes = new List<ManufactureRecipe>();

        static TentManufactureManager()
        {
            // Forge & Anvil Recipes
            _recipes.Add(new ManufactureRecipe("Forge", 27020, 2, 27022, 1, 46005, "Refined Iron Ingot"));
            _recipes.Add(new ManufactureRecipe("Anvil", 46005, 2, 27001, 1, 21001, "Iron Longsword"));
            _recipes.Add(new ManufactureRecipe("Anvil", 46005, 3, 27001, 2, 21010, "Heavy Iron Armor"));

            // Loom & Sewing Machine Recipes
            _recipes.Add(new ManufactureRecipe("Loom", 30013, 3, 0, 0, 30014, "Fine Silk Cloth"));
            _recipes.Add(new ManufactureRecipe("Sewing Machine", 30014, 2, 30015, 1, 22005, "Mage Robe"));

            // Kiln & Potting Recipes
            _recipes.Add(new ManufactureRecipe("Kiln", 27024, 2, 27001, 1, 48010, "Ceramic Vase"));
            _recipes.Add(new ManufactureRecipe("Kiln", 27024, 3, 27020, 1, 48011, "Refined Brick"));
        }

        public static bool Manufacture(Player player, string workbench, ushort in1, byte c1, ushort in2, byte c2)
        {
            if (player == null) return false;

            var match = _recipes.FirstOrDefault(r => 
                r.WorkbenchName.Equals(workbench, StringComparison.OrdinalIgnoreCase) &&
                r.InputItem1 == in1 && r.Count1 <= c1 &&
                r.InputItem2 == in2 && r.Count2 <= c2);

            if (match == null)
            {
                SendSystemMsg(player, $"No valid {workbench} recipe found for these materials.");
                return false;
            }

            if (!player.Inv.RemoveItemById(in1, match.Count1))
            {
                SendSystemMsg(player, "Not enough materials for first ingredient!");
                return false;
            }

            if (match.InputItem2 > 0 && match.Count2 > 0)
            {
                if (!player.Inv.RemoveItemById(in2, match.Count2))
                {
                    player.Inv.AddItem(in1, match.Count1); // Refund
                    SendSystemMsg(player, "Not enough materials for second ingredient!");
                    return false;
                }
            }

            player.Inv.AddItem(match.OutputItem, 1);

            // Play tent crafting effect
            SendPacket p = new SendPacket();
            p.Pack8(5);
            p.Pack8(5);
            p.Pack32(player.CharID);
            p.Pack16(60018); // Crafting sparkles
            player.CurMap?.Broadcast(p);

            SendSystemMsg(player, $"[{workbench} Manufacturing] Crafted 1x {match.OutputName}!");
            DebugSystem.Write($"[TentManufacture] Player {player.CharName} crafted {match.OutputName} at {workbench}.");
            return true;
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
