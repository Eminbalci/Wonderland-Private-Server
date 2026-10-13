# Item Material Chemistry and Alchemy Classification Matrix

## 1. Executive Summary & Alchemy Classification Architecture

In Wonderland Online, item synthesis and crafting depend on a strict classification taxonomy embedded in the client engine. Every craftable item, alchemical ingredient, and natural resource has a primary material category ID (`MaterialType`) assigned in `Item.dat` and evaluated by client subroutine `FUN_0049f998` (RVA `0x0049f998`, line 512074 in [`aLogin_decompiled.c`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c)).

This classification system governs:
1. **The Compounding Engine (`Form_Compound_1`):** Item combinations in the alchemy pot evaluate input material types to calculate resulting item rank, grade, and failure byproducts.
2. **Tent Manufacturing Machines:** Workbenches, Furnaces, Ovens, and Sewing Machines enforce category constraints from `Compound.dat` and `Compound2.Dat`.
3. **Resource Gathering Nodes:** Mining, woodcutting, fishing, and farm harvests map directly to these material classes.

---

## 2. Exhaustive 70-Category Material Taxonomy (`FUN_0049f998`)

| ID (Dec / Hex) | Material Class (EN) | Category Domain | Typical In-Game Items / Applications |
|---|---|---|---|
| `1` (`0x01`) | **Flower** | Flora / Organic | Fresh Flower, Wild Rose, Chrysanthemum (Dyes, Perfumes) |
| `2` (`0x02`) | **Grass** | Flora / Organic | Wild Grass, Straw, Weed (Ropes, Thatch, Weaving) |
| `3` (`0x03`) | **Wood** | Forestry | Pine, Oak, Cedar, Lauan Wood (Carpentry, Boats, Planks) |
| `4` (`0x04`) | **Bamboo** | Forestry | Bamboo Shoot, Green Bamboo (Weaving, Fishing Rods) |
| `5` (`0x05`) | **Leaf** | Flora / Organic | Broadleaf, Mint Leaf (Herbal teas, poultices) |
| `6` (`0x06`) | **Herb** | Alchemy Herbal | Ginseng, Wolfberry, Healing Herb (HP/SP Potions) |
| `7` (`0x07`) | **Veggie** | Agriculture | Cabbage, Carrot, Turnip (Cooking recipes) |
| `8` (`0x08`) | **Fruit** | Agriculture | Apple, Watermelon, Banana (Cooking, Juices, Feed) |
| `9` (`0x09`) | **Mushroom** | Foraging | Wild Fungus, Poison Mushroom, Matsutake (Potions, Broths) |
| `10` (`0x0A`) | **Grain** | Agriculture | Wheat, Barley, Rice (Bread, Noodles, Flour) |
| `11` (`0x0B`) | **Meat** | Fauna / Culinary | Pork, Beef, Mutton, Animal Flesh (Cooking, Jerky) |
| `12` (`0x0C`) | **Poultry** | Fauna / Culinary | Chicken, Duck meat, Bird wing (Roasting, Stock) |
| `13` (`0x0D`) | **Seafood** | Marine / Aquatic | Crab, Lobster, Shrimp, Octopus (Seafood Platters) |
| `14` (`0x0E`) | **Fish** | Marine / Aquatic | Salmon, Eel, Carp, Trout, Tuna (Cooking, Sashimi) |
| `15` (`0x0F`) | **Shellfish** | Marine / Aquatic | Clam, Oyster, Conch (Shellcraft, Cooking, Pearl base) |
| `16` (`0x10`) | **Salt** | Culinary Mineral | Coarse Salt, Sea Salt (Seasoning, Preservatives) |
| `17` (`0x11`) | **Sugar** | Culinary Organic | Sugar Cane, Brown Sugar (Confectionery, Candy) |
| `18` (`0x12`) | **Sauce** | Culinary Compound | Soy Sauce, Vinegar, Fermented Paste (Cooking) |
| `19` (`0x13`) | **Water** | Liquid Universal | Spring Water, Pure Water, Distilled Water (Solvent) |
| `30` (`0x1E`) | **Diamond** | Precious Gem | Rough Diamond, Cut Diamond (High-tier Jewelcraft) |
| `31` (`0x1F`) | **Crystal** | Precious Gem | Quartz, Blue Crystal, Amethyst (Magic Focus, Staves) |
| `32` (`0x20`) | **Mercury** | Liquid Metal | Liquid Quicksilver (High-tier Alchemy, Reagents) |
| `33` (`0x21`) | **Silver** | Precious Metal | Silver Ore, Silver Ingot (Silverware, Jewelry) |
| `34` (`0x22`) | **Stone** | Geological | Granite, Slate, Flint, Cobblestone (Masonry, Tools) |
| `35` (`0x23`) | **Magnet** | Magnetic Ore | Lodestone, Magnet Stone (Compass, Navigation) |
| `36` (`0x24`) | **Gold** | Precious Metal | Gold Sand, Gold Ingot (High-tier Jewelry, Currency) |
| `37` (`0x25`) | **Copper** | Base Metal | Copper Ore, Bronze Bar (Armor, Utensils, Pipes) |
| `42` (`0x2A`) | **Iron** | Base Metal | Iron Ore, Steel Bar, Pig Iron (Weapons, Plates) |
| `43` (`0x2B`) | **Red Clay** | Ceramic Soil | High-iron ceramic clay (Pottery, Brickmaking) |
| `44` (`0x2C`) | **Yellow Clay** | Ceramic Soil | Standard loam clay (Oven construction, Tiles) |
| `45` (`0x2D`) | **Black Clay** | Ceramic Soil | Organic-rich dark clay (High-density ceramics) |
| `46` (`0x2E`) | **White Clay** | Ceramic Soil | Kaolin clay (Porcelain, Fine China) |
| `47` (`0x2F`) | **Grey Clay** | Ceramic Soil | Alluvial grey mud (Crucibles, Foundry molds) |
| `48` (`0x30`) | **Dry Clay** | Ceramic Soil | Dehydrated powder clay (Binding agent) |
| `49` (`0x31`) | **Soft Clay** | Ceramic Soil | Malleable clay (Sculpting, Initial molding) |
| `50` (`0x32`) | **Feather** | Fauna Derivative | Down Feather, Eagle Feather (Arrows, Quill, Pillow) |
| `51` (`0x33`) | **Fur** | Fauna Derivative | Wolf Fur, Bear Fur, Rabbit Hair (Warm Garments) |
| `52` (`0x34`) | **Bone** | Fauna Derivative | Beast Bone, Dragon Bone (Bone Weapons, Needles) |
| `53` (`0x35`) | **Scale** | Fauna Derivative | Fish Scale, Serpent Scale, Dragon Scale (Scale Mail) |
| `54` (`0x36`) | **Leather** | Fauna Derivative | Tanned Hide, Thick Leather (Boots, Belts, Armor) |
| `55` (`0x37`) | **Feces** | Organic Waste | Animal Droppings, Bat Guano (Fertilizer, Alchemy Fail) |
| `56` (`0x38`) | **Secrets** | Mystery Catalyst | Secret Scroll, Mystery Catalyst (Special Transformations) |
| `57` (`0x39`) | **Alcohol** | Solvent Organic | Rice Wine, Grain Alcohol, Pure Spirits (Combustion, Tonic) |
| `58` (`0x3A`) | **Nylon** | Synthetic Polymer | Synthetic Thread, Nylon Rope (Industrial Fabrics) |
| `59` (`0x3B`) | **Crude** | Petrochemical | Crude Petroleum (Refinery input) |
| `60` (`0x3C`) | **Ref Oil** | Petrochemical | Refined Fuel Oil, Lubricant (Engine Fuel, Lanterns) |
| `61` (`0x3D`) | **Gas** | Petrochemical | Combustible Gas, Propane (High-temp Furnace) |
| `62` (`0x3E`) | **Rubber** | Polymer Organic | Latex, Vulcanized Rubber (Tires, Insulation) |
| `63` (`0x3F`) | **Tar** | Petrochemical | Pitch, Asphalt (Sealant, Waterproofing) |
| `64` (`0x40`) | **Glass** | Ceramic Amorphous | Silica Glass, Lens, Bottle (Optics, Containers) |
| `65` (`0x41`) | **Plastic** | Synthetic Polymer | Molded Resin, Plastic Sheets (Modern Appliances) |
| `68` (`0x44`) | **Poison** | Chemical Toxin | Venom Sac, Scorpion Toxin, Belladonna (Poison Darts) |
| `69` (`0x45`) | **Acid** | Chemical Reactant | Sulfuric Acid, Nitric Acid (Metal Etching, Batteries) |
| `70` (`0x46`) | **Alkali** | Chemical Reactant | Quicklime, Lye (Soap making, Neutralization) |
| `71` (`0x47`) | **Chemical** | Industrial Compound | Industrial Reagents, Synthetic Compounds |
| `72` (`0x48`) | **Explosive** | Energetic Material | Black Powder, Dynamite, TNT (Excavation, Cannon) |
| `74` (`0x4A`) | **Fuel** | Combustible | Firewood, Coal, Charcoal (Smelting, Campfire) |
| `101` (`0x65`) | **Drinkable** | Consumable Flag | Liquid consumable by player/companion |
| `102` (`0x66`) | **Can't Drink** | Consumable Flag | Toxic, corrosive, or non-potable liquid |

---

## 3. Compounding Formula Mechanics & Failure Logic

In the compounding engine:
1. **Matching Categories:** If `MaterialType(Item1) == MaterialType(Item2)`, the compounding engine synthesizes higher-tier items within that same material tree based on formula:
   $$\text{TargetRank} = \left\lfloor \frac{\text{Rank}_1 + \text{Rank}_2}{2} \right\rfloor + \Delta_{\text{Alchemy}}$$
2. **Cross-Category Synthesis:** When mixing complementary materials (e.g. `Wood` + `Iron` $\to$ Weapons/Tools, `Herb` + `Water` $\to$ Potions), the combination is cross-referenced with `Compound.dat` / `Compound2.Dat`.
3. **Failure Outcomes:** When a recipe fails or ingredients are incompatible, the result falls back to base degradation items:
   - Category 55: **Feces** (Organic failure)
   - Item ID 34002: **Ash / Cinders** (Thermal / pyrolytic failure)
   - Low-tier byproduct: **Garbage**
