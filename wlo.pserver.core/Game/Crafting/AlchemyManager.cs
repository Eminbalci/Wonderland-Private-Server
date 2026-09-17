using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Network;

namespace Game.Crafting
{
    public class AlchemyRecipe
    {
        public ushort InputItem1 { get; set; }
        public ushort InputItem2 { get; set; }
        public ushort OutputItem { get; set; }
        public string OutputName { get; set; } = string.Empty;
        public double SuccessRate { get; set; } = 80.0;

        public AlchemyRecipe(ushort in1, ushort in2, ushort outIt, string outName, double rate = 80.0)
        {
            InputItem1 = in1;
            InputItem2 = in2;
            OutputItem = outIt;
            OutputName = outName;
            SuccessRate = rate;
        }
    }

    public static class AlchemyManager
    {
        private static readonly List<AlchemyRecipe> _recipes = new List<AlchemyRecipe>();
        private static readonly Random _rng = new Random();
        private static readonly object _lock = new object();
        private static string ConfigPath => RCLibrary.Core.PathHelper.GetDataFilePath("alchemy_recipes.txt");

        static AlchemyManager()
        {
            LoadFromDatabase();
        }

        public static void InitializeRecipes()
        {
            lock (_lock)
            {
                _recipes.Clear();

                // 1. Basic Woodcraft Recipes
                _recipes.Add(new AlchemyRecipe(27001, 27001, 48001, "Wooden Plank", 90.0)); // Wood + Wood -> Plank
                _recipes.Add(new AlchemyRecipe(27002, 27001, 48002, "Hardwood Plank", 85.0)); // Pine + Wood -> Hardwood Plank
                _recipes.Add(new AlchemyRecipe(27005, 27001, 27015, "Wooden Bow", 75.0)); // Vine + Wood -> Bow

                // 2. Metals & Ores Synthesis
                _recipes.Add(new AlchemyRecipe(27020, 27001, 46005, "Refined Metal", 75.0)); // Dark Stone + Wood -> Refined Metal
                _recipes.Add(new AlchemyRecipe(46005, 27001, 21001, "Bronze Sword", 70.0)); // Metal + Wood -> Sword
                _recipes.Add(new AlchemyRecipe(46005, 46005, 21010, "Iron Armor", 65.0)); // Metal + Metal -> Armor

                // 3. Potions & Herbs Synthesis
                _recipes.Add(new AlchemyRecipe(28014, 28006, 30201, "Small HP Potion", 90.0)); // Fruit + Apple -> Small HP
                _recipes.Add(new AlchemyRecipe(30201, 28015, 30202, "Medium HP Potion", 80.0)); // Small HP + Magic Leaf -> Med HP
                _recipes.Add(new AlchemyRecipe(30202, 28015, 30204, "Large HP Potion", 70.0)); // Med HP + Magic Leaf -> Large HP
                _recipes.Add(new AlchemyRecipe(30205, 28012, 30203, "Small SP Potion", 85.0)); // Antidote + Mushroom -> Small SP

                // 4. Food & Cooking Synthesis
                _recipes.Add(new AlchemyRecipe(30001, 27001, 28020, "Roast Meat", 90.0)); // Meat + Wood -> Roast Meat
                _recipes.Add(new AlchemyRecipe(30002, 27001, 28021, "Roast Pork", 90.0)); // Pork + Wood -> Roast Pork
                _recipes.Add(new AlchemyRecipe(30003, 27001, 28022, "Grilled Seafood", 90.0)); // Crab + Wood -> Seafood

                // 5. Leather & Tailoring Synthesis
                _recipes.Add(new AlchemyRecipe(30015, 30016, 22001, "Leather Boots", 75.0)); // Pelt + Leather -> Boots
                _recipes.Add(new AlchemyRecipe(30016, 30013, 22005, "Leather Vest", 70.0)); // Leather + Silk -> Vest
                _recipes.Add(new AlchemyRecipe(30018, 30013, 22010, "Tiger Fur Coat", 60.0)); // Tiger Fur + Silk -> Coat

                // Load official binary recipes from Compound2.dat and Compound.dat
                string c2 = RCLibrary.Core.PathHelper.GetDataFilePath("Compound2.dat");
                if (File.Exists(c2)) { LoadFromCompoundDat(c2); }

                string c1 = RCLibrary.Core.PathHelper.GetDataFilePath("Compound.dat");
                if (File.Exists(c1)) { LoadFromCompoundDat(c1); }
            }
        }

        public static void LoadFromCompoundDat(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            try
            {
                byte[] data = File.ReadAllBytes(filePath);
                int recordSize = 65;
                int total = data.Length / recordSize;
                int loaded = 0;

                lock (_lock)
                {
                    for (int i = 0; i < total; i++)
                    {
                        int ptr = i * recordSize;
                        if (ptr + recordSize > data.Length) break;

                        ushort resultId = (ushort)((BitConverter.ToUInt16(data, ptr) ^ 0xFBBC) - 3);
                        ushort in1 = (ushort)((BitConverter.ToUInt16(data, ptr + 11) ^ 0xFBBC) - 3);
                        ushort in2 = (ushort)((BitConverter.ToUInt16(data, ptr + 14) ^ 0xFBBC) - 3);

                        if (resultId > 0 && in1 > 0 && in2 > 0)
                        {
                            if (!_recipes.Any(r => (r.InputItem1 == in1 && r.InputItem2 == in2 && r.OutputItem == resultId) ||
                                                   (r.InputItem1 == in2 && r.InputItem2 == in1 && r.OutputItem == resultId)))
                            {
                                string name = Battle.MonsterDropManager.ItemNameResolver?.Invoke(resultId) ?? $"Item #{resultId}";
                                _recipes.Add(new AlchemyRecipe(in1, in2, resultId, name, 85.0));
                                loaded++;
                            }
                        }
                    }
                }
                DebugSystem.Write($"[AlchemyManager] Loaded {loaded} authentic recipes from {Path.GetFileName(filePath)} (Total recipes: {_recipes.Count})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AlchemyManager] Error loading {filePath}: {ex.Message}");
            }
        }

        public static AlchemyRecipe FindRecipe(ushort item1, ushort item2)
        {
            lock (_lock)
            {
                return _recipes.FirstOrDefault(r =>
                    (r.InputItem1 == item1 && r.InputItem2 == item2) ||
                    (r.InputItem1 == item2 && r.InputItem2 == item1));
            }
        }

        public static bool CompoundItems(Player player, byte slot1, byte slot2)
        {
            if (player == null) return false;

            ushort item1 = player.Inv.GetItemIdAtSlot(slot1);
            ushort item2 = player.Inv.GetItemIdAtSlot(slot2);

            if (item1 == 0 || item2 == 0)
            {
                SendSystemMsg(player, "Please select two valid items from your inventory to compound!");
                return false;
            }

            // Remove ingredients
            player.Inv.RemoveItemAtSlot(slot1, 1);
            player.Inv.RemoveItemAtSlot(slot2, 1);

            AlchemyRecipe matchedRecipe = null;
            lock (_lock)
            {
                matchedRecipe = _recipes.FirstOrDefault(r => 
                    (r.InputItem1 == item1 && r.InputItem2 == item2) ||
                    (r.InputItem1 == item2 && r.InputItem2 == item1));
            }

            double roll = _rng.NextDouble() * 100.0;
            double successRate = matchedRecipe != null ? matchedRecipe.SuccessRate : 40.0;

            if (matchedRecipe != null && roll <= successRate)
            {
                // Compound SUCCESS!
                player.Inv.AddItem(matchedRecipe.OutputItem, 1);

                // Play Compound Animation & Effect
                SendPacket pEffect = new SendPacket();
                pEffect.Pack8(5);
                pEffect.Pack8(5);
                pEffect.Pack32(player.CharID);
                pEffect.Pack16(60020); // Synthesis light effect
                player.CurMap?.Broadcast(pEffect);

                SendSystemMsg(player, $"[Synthesis Success!] You successfully compounded {matchedRecipe.OutputName}!");
                DebugSystem.Write($"[Alchemy] Player {player.CharName} compounded {matchedRecipe.OutputName} (#{matchedRecipe.OutputItem}).");
                return true;
            }
            else
            {
                // Compound Failed -> Ash / Charcoal (#27008)
                ushort failItem = 27008;
                player.Inv.AddItem(failItem, 1);

                SendSystemMsg(player, "[Synthesis Failed!] The materials turned into charcoal.");
                DebugSystem.Write($"[Alchemy] Player {player.CharName} failed compounding #{item1} + #{item2}.");
                return false;
            }
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

        public static void VerifyTable()
        {
            try
            {
                bool needsRecreate = false;
                try
                {
                    var testDt = RCLibrary.Core.DataBase.Query("SELECT item1_id FROM alchemy_recipes LIMIT 1;");
                    if (testDt == null) needsRecreate = true;
                }
                catch
                {
                    needsRecreate = true;
                }

                if (needsRecreate)
                {
                    RCLibrary.Core.DataBase.Execute("DROP TABLE IF EXISTS alchemy_recipes;");
                }

                RCLibrary.Core.DataBase.Execute(@"CREATE TABLE IF NOT EXISTS alchemy_recipes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    item1_id INT NOT NULL,
                    item2_id INT NOT NULL,
                    output_id INT NOT NULL,
                    output_name TEXT,
                    success_rate REAL DEFAULT 100.0
                );");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[Alchemy] Error verifying alchemy_recipes table: {ex.Message}");
            }
        }

        public static void LoadFromFile() => LoadFromDatabase();

        public static void LoadFromDatabase()
        {
            try
            {
                VerifyTable();
                var dt = RCLibrary.Core.DataBase.Query("SELECT * FROM alchemy_recipes;");

                if (dt == null || dt.Rows.Count == 0)
                {
                    // Table empty: seed recipes
                    InitializeRecipes();
                    SaveToDatabase();
                    dt = RCLibrary.Core.DataBase.Query("SELECT * FROM alchemy_recipes;");
                }

                lock (_lock)
                {
                    _recipes.Clear();
                    if (dt != null)
                    {
                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            ushort in1 = Convert.ToUInt16(row["item1_id"]);
                            ushort in2 = Convert.ToUInt16(row["item2_id"]);
                            ushort outIt = Convert.ToUInt16(row["output_id"]);
                            string outName = row["output_name"]?.ToString() ?? "";
                            double rate = Convert.ToDouble(row["success_rate"]);

                            _recipes.Add(new AlchemyRecipe(in1, in2, outIt, outName, rate));
                        }
                    }
                }

                DebugSystem.Write($"[Alchemy] Loaded {_recipes.Count} recipes from SQLite database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[Alchemy] Error loading recipes from database: {ex.Message}");
            }
        }

        public static void SaveToDatabase()
        {
            try
            {
                VerifyTable();
                lock (_lock)
                {
                    RCLibrary.Core.DataBase.Execute("DELETE FROM alchemy_recipes;");
                    foreach (var r in _recipes)
                    {
                        string sql = $"INSERT INTO alchemy_recipes (item1_id, item2_id, output_id, output_name, success_rate) VALUES ({r.InputItem1}, {r.InputItem2}, {r.OutputItem}, '{r.OutputName.Replace("'", "''")}', {r.SuccessRate});";
                        RCLibrary.Core.DataBase.Execute(sql);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[Alchemy] Error saving recipes to database: {ex.Message}");
            }
        }

        public static void SaveToFile()
        {
            SaveToDatabase();
        }
    }
}
