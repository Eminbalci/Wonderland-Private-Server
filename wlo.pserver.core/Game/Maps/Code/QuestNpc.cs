using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;

namespace Game.Maps
{
    /// <summary>
    /// Class for Quest related Npcs or Npcs that respond in game to interactions
    /// 
    /// Uses EvaluateQuestData to handle packets related to position and visibility per player
    /// 
    /// </summary>
    public class QuestNpc : InteractableObjects
    {
        public virtual string Name { get; set; }
        public virtual ushort Level { get; set; }
        public virtual uint HP { get; set; }
        public virtual byte Element { get; set; }
        public virtual uint TemplateID { get; set; } // Template ID for NPC definition lookup
        public ushort MapID { get; set; }

        public ushort SpawnX { get; set; }
        public ushort SpawnY { get; set; }
        public byte WalkBehavior { get; set; }
        public List<DataFiles.npcWalkStep> WalkSteps { get; set; } = new List<DataFiles.npcWalkStep>();
        public int CurStep { get; set; } = 0;
        public DateTime NextWalkTime { get; set; } = DateTime.MinValue;

        // Chest & Gathering Prop State
        public bool IsBroken { get; set; } = false;
        public DateTime RespawnTime { get; set; } = DateTime.MinValue;

        private static readonly Random _rng = new Random();
        private static readonly object _rngLock = new object();

        public static int NextRandom(int min, int max)
        {
            lock (_rngLock)
            {
                return _rng.Next(min, max);
            }
        }

        public static double NextRandomDouble(double min, double max)
        {
            lock (_rngLock)
            {
                return min + (_rng.NextDouble() * (max - min));
            }
        }

        public virtual void Update(DateTime now, GameMap map)
        {
            if (map == null || map.PlayersList == null || map.PlayersList.Count == 0) return;

            // Handle Gathering Nodes Respawning (e.g. Coconut, Wood, Ore)
            if (IsBroken)
            {
                if (now >= RespawnTime)
                {
                    IsBroken = false;
                    // Broadcast un-hide / respawn packet (AC 22:10 state 0, 0)
                    SendPacket respawnPkt = Tools.FromFormat("bbwbb", 22, 10, (ushort)this.CickID, (byte)0, (byte)0);
                    map.Broadcast(respawnPkt);
                    DebugSystem.Write($"[QuestNpc] Gathering node '{Name}' (ClickID: {this.CickID}) respawned on Map {map.MapID}");
                }
                return;
            }

            // Never move or animate static props, chests, or entities with invalid templates
            if (IsStaticNpc() || TemplateID == 0)
            {
                return;
            }

            if (NextWalkTime > now) return;

            try
            {
                // 1. Scripted path walking from dat file (behavior 5 or has explicit walksteps)
                if (WalkSteps != null && WalkSteps.Count > 0)
                {
                    var step = WalkSteps[CurStep % WalkSteps.Count];

                    SendPacket pkt = new SendPacket();
                    pkt.PackArray(new byte[] { 22, 2 });
                    pkt.Pack16(this.CickID);
                    pkt.Pack16((ushort)step.x);
                    pkt.Pack16((ushort)step.y);
                    pkt.Pack8(2); // walking speed

                    map.Broadcast(pkt);

                    this.X = (ushort)step.x;
                    this.Y = (ushort)step.y;

                    CurStep = (CurStep + 1) % WalkSteps.Count;
                    // Natural delay between path points (respect delay or 3-7s pause)
                    double delaySec = (step.delay > 0) ? Math.Max(2.0, (double)step.delay / 1000.0) : NextRandomDouble(3.5, 7.5);
                    NextWalkTime = now.AddSeconds(delaySec);
                }
                // 2. Random roaming ONLY for wild monsters on outdoor field maps (not towns/villages/pens)
                else if (IsWildMonster() && WalkBehavior == 4 && !IsVillageOrTownMap((int)map.MapID))
                {
                    int dx = NextRandom(-40, 41);
                    int dy = NextRandom(-40, 41);
                    int targetX = (int)this.X + dx;
                    int targetY = (int)this.Y + dy;

                    // Tight leash to prevent wandering through fences or obstacles (max 60px from spawn)
                    if (Math.Abs(targetX - this.SpawnX) > 60 || Math.Abs(targetY - this.SpawnY) > 60)
                    {
                        targetX = this.SpawnX + NextRandom(-20, 21);
                        targetY = this.SpawnY + NextRandom(-20, 21);
                    }

                    ushort finalX = (ushort)Math.Max(50, Math.Min(3000, targetX));
                    ushort finalY = (ushort)Math.Max(50, Math.Min(3000, targetY));

                    SendPacket pkt = new SendPacket();
                    pkt.PackArray(new byte[] { 22, 2 });
                    pkt.Pack16(this.CickID);
                    pkt.Pack16(finalX);
                    pkt.Pack16(finalY);
                    pkt.Pack8(2); // walking speed

                    map.Broadcast(pkt);

                    this.X = finalX;
                    this.Y = finalY;

                    double waitSec = NextRandomDouble(5.0, 10.0);
                    NextWalkTime = now.AddSeconds(waitSec);
                }
                else
                {
                    // Town NPCs, villagers, farm animals in pens, and static props remain at their positions
                    NextWalkTime = now.AddSeconds(300);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestNpc] Error in Update for ClickID {this.CickID}: {ex.Message}");
                NextWalkTime = now.AddSeconds(10);
            }
        }

        public static bool IsVillageOrTownMap(int mapId)
        {
            // Kelan Village, Welling Village, Holy Village, Kyoto, Chang'an, Rome, Cornwall, South Pole, etc.
            if (mapId == 10000 || mapId == 10010 || mapId == 60001) return true;
            if (mapId >= 10001 && mapId <= 10036) return true; // Kelan interiors and residential
            if (mapId >= 12000 && mapId <= 12030) return true; // Welling Village
            if (mapId >= 14000 && mapId <= 14030) return true; // Holy Village
            if (mapId >= 16000 && mapId <= 16030) return true; // Kyoto
            if (mapId >= 18000 && mapId <= 18030) return true; // Chang'an
            return false;
        }

        public bool IsWildMonster()
        {
            if (this.TemplateID == 0)
                return false;

            string lower = (Name ?? "").ToLower().Trim();

            // 1. Shops, services, keepers, doctors, hotels, signposts are NEVER monsters
            if (lower.Contains("shop") || lower.Contains("store") || lower.Contains("market") ||
                lower.Contains("keep") || lower.Contains("storage") || lower.Contains("bank") ||
                lower.Contains("exchanger") || lower.Contains("doctor") || lower.Contains("witch") ||
                lower.Contains("clinic") || lower.Contains("hotel") || lower.Contains("inn") ||
                lower.Contains("guidepost") || lower.Contains("signpost") || lower.Contains("statue") ||
                lower.Contains("pig") || this.TemplateID == 17400)
            {
                return false;
            }

            // 2. Non-monster template ID ranges in WLO:
            // 10000-12999: Story characters, Recruitable companions, Sailors
            // 13000-13999: Shops (Props Shop, Weapon Shop, Armor Shop)
            // 14000-14999: Human Villagers, Townspeople, Guards, Elders
            // 19000-24999: Props, Gathering nodes, Chests, Furniture
            // 25000+: Story cutscene actors
            if (TemplateID < 17000 || TemplateID >= 18000)
            {
                return false;
            }

            // 3. Domestic pets or friendly animals in 17000-17999 range
            if (TemplateID == 17400) // Kelan Village Pigs
            {
                return false;
            }

            // 4. Friendly human / citizen / town NPC keywords
            if (lower.Contains("villager") || lower.Contains("citizen") || lower.Contains("resident") ||
                lower.Contains("grandma") || lower.Contains("grandmother") || lower.Contains("grandfather") ||
                lower.Contains("elder") || lower.Contains("mayor") || lower.Contains("chief") ||
                lower.Contains("guard") || lower.Contains("soldier") || lower.Contains("knight") ||
                lower.Contains("merchant") || lower.Contains("vendor") || lower.Contains("trader") ||
                lower.Contains("peddler") || lower.Contains("innkeeper") || lower.Contains("waitress") ||
                lower.Contains("nurse") || lower.Contains("doctor") || lower.Contains("priest") ||
                lower.Contains("monk") || lower.Contains("clerk") || lower.Contains("sailor") ||
                lower.Contains("captain") || lower.Contains("chef") || lower.Contains("cook") ||
                lower.Contains("maid") || lower.Contains("blacksmith") || lower.Contains("carpenter") ||
                lower.Contains("hunter") || lower.Contains("miner") || lower.Contains("guide") ||
                lower.Contains("girl") || lower.Contains("boy") || lower.Contains("kid") ||
                lower.Contains("child") || lower.Contains("man") || lower.Contains("woman") ||
                lower.Contains("lady") || lower.Contains("sir") || lower.Contains("robinson"))
            {
                return false;
            }

            // 5. Authentic roaming monsters in WLO 17000-17999 range
            return (TemplateID >= 17000 && TemplateID <= 17999);
        }

        public bool IsHumanNpc()
        {
            string lower = (Name ?? "").ToLower().Trim();
            return lower.Contains("villager") || lower.Contains("citizen") || lower.Contains("resident") ||
                   lower.Contains("grandma") || lower.Contains("elder") || lower.Contains("mayor") ||
                   lower.Contains("guard") || lower.Contains("soldier") || lower.Contains("knight") ||
                   lower.Contains("merchant") || lower.Contains("sailor") || lower.Contains("captain") ||
                   lower.Contains("maid") || lower.Contains("girl") || lower.Contains("boy") ||
                   lower.Contains("man") || lower.Contains("woman") || lower.Contains("robinson") ||
                   lower.Contains("burke") || lower.Contains("peter") || lower.Contains("john") ||
                   lower.Contains("natasha") || lower.Contains("breillat");
        }

        public bool IsStaticNpc()
        {
            if (this.TemplateID == 0)
                return true;

            // Wild monsters and human NPCs are never static props
            if (IsWildMonster() || IsHumanNpc())
                return false;

            // Prop / chest / object template ID ranges in WLO:
            // 12000-12999: containers, crates, beach wreckage props
            // 25000-35000: static map props & mechanisms
            if ((this.TemplateID >= 12000 && this.TemplateID <= 12999) ||
                (this.TemplateID >= 25000 && this.TemplateID <= 35000))
            {
                return true;
            }

            string lower = (Name ?? "").ToLower().Trim();

            if (lower.Contains("chest") || lower.Contains("box") || lower.Contains("crate") ||
                lower.Contains("barrel") || lower.Contains("pot") || lower.Contains("machine") ||
                lower.Contains("wood") || lower.Contains("stone") || lower.Contains("clay") ||
                lower.Contains("mine") || lower.Contains("herb") || lower.Contains("tree") ||
                lower.Contains("door") || lower.Contains("switch") || lower.Contains("lever") ||
                lower.Contains("cabinet") || lower.Contains("desk") || lower.Contains("bed") ||
                lower.Contains("chair") || lower.Contains("stove") || lower.Contains("grass") ||
                lower.Contains("flower") || lower.Contains("shell") || lower.Contains("mushroom") ||
                lower.Contains("ore") || lower.Contains("statue") || lower.Contains("fountain") ||
                lower.Contains("sign") || lower.Contains("well") || lower.Contains("grave") ||
                lower.Contains("cart") || lower.Contains("boat") || lower.Contains("wreck") ||
                lower.Contains("tent") || lower.Contains("fence") || lower.Contains("portal") ||
                lower.Contains("warp") || lower.Contains("prop") || lower.Contains("object") ||
                lower.Contains("game machine") || lower.Contains("coconut") || lower.Contains("driftwood") ||
                lower.Contains("bamboo") || lower.Contains("iron ore") || lower.Contains("copper ore"))
            {
                return true;
            }

            if (string.IsNullOrEmpty(lower) || lower.StartsWith("npc_0") || lower.StartsWith("unknown") || lower.StartsWith("·s"))
                return true;

            return false;
        }

        public virtual bool HasPlayerDoneQuest(Player src)
        {
            return false;
        }

        public virtual void EvaluateQuestData(Player src)
        {
        }

        public override void Interact(Player src)
        {
            try
            {
                string lowerName = (Name ?? "").ToLower();

                // --- 0.0 WILD MONSTER / OVERWORLD MOB CLICK (Immediate PvE Combat Trigger) ---
                if (this.IsWildMonster())
                {
                    src.Send(Tools.FromFormat("bb", 20, 8));
                    string mobName = this.Name;
                    if (string.IsNullOrEmpty(mobName) || mobName.Equals("Npc", StringComparison.OrdinalIgnoreCase) || mobName.StartsWith("unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        mobName = Game.Battle.PvEBattleManager.ResolveMonsterName(this.TemplateID);
                    }
                    Battle.PvEBattleManager.StartPvEBattle(src, (ushort)this.CickID, mobName, Math.Max(1, (int)this.Level), Math.Max(50, (int)this.HP), this.TemplateID);
                    DebugSystem.Write($"[QuestNpc] Started PvE battle for monster '{mobName}' (ClickID {this.CickID}, TID {this.TemplateID}, Lv.{this.Level}) with {src.CharName}");
                    return;
                }

                // --- 0.1 PRIMARY: Native eve.emg event resolution for all NPCs, Quests, Dialogues & Props ---
                // Directly dispatches official EveEventInterpreter bytecode engine from Eve.emg
                if (src.CurMap is GameMap gmap && EveEventInterpreter.TryExecute(src, gmap, (ushort)this.CickID))
                {
                    return;
                }

                // --- 0.1 PROPS KEEPER (Item Storage Vault) ---
                if (lowerName.Contains("props keep") || lowerName.Contains("storage") || this.TemplateID == 14134)
                {
                    src.OpenPropsKeeper();
                    DebugSystem.Write($"[QuestNpc] Opened Character Props Keeper Item Storage for {src.CharName}");
                    return;
                }

                // --- 0.15 STOCK KEEPER / BANK (Money / Gold Bank) ---
                if (lowerName.Contains("stock keep") || lowerName.Contains("bank") || lowerName.Contains("vault") || lowerName.Contains("exchanger") || this.TemplateID == 14181 || this.TemplateID == 14157)
                {
                    src.OpenMoneyBank();
                    DebugSystem.Write($"[QuestNpc] Opened Character Money Bank for {src.CharName}");
                    return;
                }

                // --- 0.2 WITCH DOCTOR / CLINIC (ClickID 22 / TemplateID 14151 / Full Heal & Memory Point) ---
                // Verified from witchdoctor.pcapng (Frames 18-49)
                if (lowerName.Contains("doctor") || lowerName.Contains("witch") || lowerName.Contains("clinic") || this.TemplateID == 14151)
                {
                    src.Send(Tools.FromFormat("bbb", 6, 2, 1)); // Lock movementent

                    // Step 1: Send Choice Menu (Choice ID 3: 1=Heal, 2=Save Memory Point, 3=Cancel)
                    SendPacket cPkt = new SendPacket();
                    cPkt.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 6, 3, (byte)this.CickID, 0, 0, 0, 0, 0, 0, 3, 0, 1 });

                    src.OnDialogueChoice = (choice) =>
                    {
                        DebugSystem.Write($"[QuestNpc] Witch Doctor choice 0x{choice:X} ({choice}) from {src.CharName}");
                        if (choice == 0x1E || choice == 1) // Option 1: Full Heal HP/SP
                        {
                            if (src.Eqs != null)
                            {
                                src.Eqs.CurHP = src.Eqs.FullHP;
                                src.Eqs.CurSP = src.Eqs.FullSP;
                                src.Eqs.Send8_1(true);
                            }
                            src.Send(Tools.FromFormat("bbd", 5, 18, (uint)src.CharID));
                            src.Send(Tools.FromFormat("bbd", 31, 2, (uint)0xFFFFFFFF));
                            src.Send(Tools.FromFormat("bb", 20, 9));
                            src.Send(Tools.FromFormat("bb", 20, 8));
                            src.Send(Tools.FromFormat("bb", 5, 4));
                            src.SendSystemMessage($"✨ [{Name}]: HP and SP fully restored!");
                        }
                        else if (choice == 0x1F || choice == 2) // Option 2: Save Respawn / Memory Point
                        {
                            try
                            {
                                DataBase.CharacterDataBase.GlobalInstance?.ExecuteNonQuery(
                                    $"UPDATE characters SET location_map = '{src.CurMap?.MapID ?? 12000}', location_x = '{src.CurX}', location_y = '{src.CurY}' WHERE charID = '{src.CharID}';");
                            }
                            catch { }

                            // TalkID 0x0379B6 ("Memory point saved!")
                            SendPacket savePkt = new SendPacket();
                            savePkt.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 1, 3, (byte)this.CickID, 0, 1, 0, 0, 0, 0, 0xB6, 0x79, 0x03 });
                            src.Send(savePkt);
                            src.Send(Tools.FromFormat("bbb", 5, 21, (byte)1));
                            src.Send(Tools.FromFormat("bb", 20, 10)); // Fanfare music
                            src.Send(Tools.FromFormat("bb", 20, 8));
                            src.Send(Tools.FromFormat("bb", 5, 4));
                            src.SendSystemMessage($"💾 [{Name}]: Memory point saved at Map {src.CurMap?.MapID} pos({src.CurX},{src.CurY})!");
                        }
                        else // Option 3: Cancel
                        {
                            src.Send(Tools.FromFormat("bb", 31, 7));
                            src.Send(Tools.FromFormat("bb", 20, 9));
                            src.Send(Tools.FromFormat("bb", 20, 8));
                            src.Send(Tools.FromFormat("bb", 5, 4));
                        }
                    };

                    src.Send(cPkt);
                    return;
                }

                // --- 0.3 PET HOTEL / PET KEEPER (TemplateID 14182, 14152, or keyword "pet hotel", "pet keep") ---
                if (lowerName.Contains("pet hotel") || lowerName.Contains("pet keep") || lowerName.Contains("hotel") || this.TemplateID == 14182 || this.TemplateID == 14152)
                {
                    src.OpenPetHotel();
                    DebugSystem.Write($"[QuestNpc] Opened Pet Hotel for {src.CharName}");
                    return;
                }

                // --- 0.4 PROPS SHOP & WEAPON/ARMOR SHOPS (ClickID 23 / TemplateID 13007, 13006, 13005) ---
                // Verified from propsshop.pcapng (Frames 02-31)
                if (lowerName.Contains("props shop") || lowerName.Contains("weapon shop") || lowerName.Contains("armor shop") ||
                    this.TemplateID == 13007 || this.TemplateID == 13006 || this.TemplateID == 13005)
                {
                    src.Send(Tools.FromFormat("bbb", 6, 2, 1)); // Lock movement

                    byte initialChoiceId = (byte)((this.TemplateID == 13006 || lowerName.Contains("weapon")) ? 9 : 5);
                    byte secondChoiceId = (byte)((this.TemplateID == 13006 || lowerName.Contains("weapon")) ? 8 : 6);
                    uint shopCatalogId = (this.TemplateID == 13006 || lowerName.Contains("weapon")) ? 0x0001FB84u : 0x0001FB85u;

                    // Step 1: Initial Prompt (Choice ID 5: Yes / No)
                    SendPacket cPkt1 = new SendPacket();
                    cPkt1.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 6, 3, (byte)this.CickID, 0, 0, 0, 0, 0, 0, initialChoiceId, 0, 1 });

                    src.OnDialogueChoice = (choice1) =>
                    {
                        DebugSystem.Write($"[QuestNpc] Shop Step 1 choice: 0x{choice1:X} ({choice1}) for '{Name}'");
                        if (choice1 == 0x1E || choice1 == 1) // "Yes" -> Opens 2-option Buy / Sell menu
                        {
                            // Step 2: Merchant Choice Prompt (Choice ID 6: 2 options: Option 1 = Buy, Option 2 = Sell)
                            SendPacket cPkt2 = new SendPacket();
                            cPkt2.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 6, 3, (byte)this.CickID, 0, 0, 0, 0, 0, 0, secondChoiceId, 0, 2 });

                            src.OnDialogueChoice = (choice2) =>
                            {
                                DebugSystem.Write($"[QuestNpc] Shop Step 2 (Buy/Sell) choice: 0x{choice2:X} ({choice2}) for '{Name}'");
                                src.Send(Tools.FromFormat("bb", 20, 8)); // Close dialog
                                src.Send(Tools.FromFormat("bbdb", 35, 12, shopCatalogId, 0)); // AC 35:12 Open Shop UI
                                src.Send(Tools.FromFormat("bb", 5, 4));
                            };

                            src.Send(cPkt2);
                        }
                        else // "No" (0x1F / 2) -> Cancel / Farewell
                        {
                            src.Send(Tools.FromFormat("bb", 27, 3));
                            src.Send(Tools.FromFormat("bb", 20, 9));
                            src.Send(Tools.FromFormat("bb", 20, 8));
                            src.Send(Tools.FromFormat("bb", 5, 4));
                        }
                    };

                    src.Send(cPkt1);
                    return;
                }

                string dialogueText = string.Empty;

                if (this.IsStaticNpc())
                {
                    // Static Chest, Crate, Barrel, Ore Vein, Herb, or Gathering Prop
                    if (this.IsBroken)
                    {
                        src.SendSystemMessage("📦 This node/chest is currently empty and will respawn soon.");
                        src.Send(Tools.FromFormat("bb", 20, 8));
                        src.Send(Tools.FromFormat("bb", 5, 4));
                        return;
                    }

                    // Play chest open animation (AC 22:1 or AC 22:10)
                    SendPacket anim = Tools.FromFormat("bbwb", 22, 1, (ushort)this.CickID, (byte)1);
                    src.Send(anim);
                    src.CurMap?.Broadcast(anim);

                    this.IsBroken = true;
                    this.RespawnTime = DateTime.Now.AddSeconds(ChestDropManager.DefaultRespawnSeconds);

                    // Roll authentic loot from ChestDropManager
                    var drop = ChestDropManager.RollDrop((uint)(src.CurMap?.MapID ?? 0), this.Name);
                    if (drop != null && drop.ItemID > 0)
                    {
                        src.Inv?.AddItem(drop.ItemID, drop.Count);
                        src.Send(new SendPacket(src.Inv?.GetAC23_5()));
                        string itemName = Game.Battle.MonsterDropManager.ResolveItemName(drop.ItemID) ?? drop.ItemName;
                        src.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Obtain {itemName}"));
                        src.Send(Tools.FromFormat("bb", 20, 10)); // Fanfare
                        src.SendSystemMessage($"🎁 Opened '{this.Name}' and obtained {drop.Count}x {itemName}!");
                        DebugSystem.Write($"[QuestNpc] Player {src.CharName} opened static chest/prop '{this.Name}' (ClickID: {this.CickID}) and received {drop.Count}x {itemName} (#{drop.ItemID}).");
                    }

                    src.Send(Tools.FromFormat("bb", 20, 8));
                    src.Send(Tools.FromFormat("bb", 5, 4));
                    return;
                }

                // 1.7 Handle Storage / Bank / Exchanger Keepers
                if (lowerName.Contains("keep") || lowerName.Contains("storage") || lowerName.Contains("bank") || 
                    lowerName.Contains("exchanger") || lowerName.Contains("stock") || this.TemplateID == 14134 || this.TemplateID == 14181 || this.TemplateID == 14157)
                {
                    ushort actionType = (lowerName.Contains("stock") || this.TemplateID == 14181 || this.TemplateID == 14157) ? (ushort)9 : (ushort)4;
                    SendPacket sysPkt = new SendPacket();
                    sysPkt.Pack8(20);
                    sysPkt.Pack8(1);
                    sysPkt.Pack8(0); sysPkt.Pack8(0); sysPkt.Pack8(0);
                    sysPkt.Pack8(1);
                    sysPkt.Pack8(7);
                    sysPkt.Pack16(actionType);
                    sysPkt.Pack8(0);
                    sysPkt.Pack8(0); sysPkt.Pack8(0); sysPkt.Pack8(0); sysPkt.Pack8(0);
                    src.Send(sysPkt);
                    src.SendSystemMessage($"🏦 [{Name}]: Storage vault opened.");
                    DebugSystem.Write($"[QuestNpc] Handled Storage/Keeper interaction for '{Name}' (ClickID: {this.CickID}) with {src.CharName}");
                    return;
                }

                // Generic NPC fallback: direct AC 20 Sub 1 dialogue with authentic Talk.dat ID
                uint talkId = ResolveTalkIdForNpc(src);

                SendPacket step1 = BuildDialogueStep((byte)this.CickID, talkId, step: 1, portraitType: 3);
                src.Send(step1);

                src.OnInteractionComplete = () =>
                {
                    src.Send(Tools.FromFormat("bb", 20, 8));
                    src.Send(Tools.FromFormat("bb", 5, 4));
                };

                if (string.IsNullOrEmpty(dialogueText))
                {
                    dialogueText = ExtractDialogueFromTalkDat(talkId);
                }

                src.SendSystemMessage($"💬 {Name}: {dialogueText}");
                DebugSystem.Write($"[QuestNpc] Sent authentic dialogue window for '{Name}' (ClickID: {this.CickID}, TalkID: 0x{talkId:X}): '{dialogueText}'");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestNpc] Error in Interact: {ex.Message}");
                src.Send(Tools.FromFormat("bb", 20, 8));
            }
        }

        private static string ExtractDialogueFromTalkDat(uint talkId)
        {
            try
            {
                string text = DataBase.GameDataBase.GlobalInstance?.TalkDat?.GetDialogue(talkId);
                if (!string.IsNullOrEmpty(text)) return text;
            }
            catch
            {
            }
            return "Hello!";
        }

        private uint ResolveTalkIdForNpc(Player src)
        {
            // 1. Dynamic extraction from authentic Eve.emg binary data
            try
            {
                if (src?.CurMap != null)
                {
                    var mapData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)src.CurMap.MapID);
                    if (mapData != null)
                    {
                        var npcEntry = mapData.Npclist?.FirstOrDefault(n => n.clickId == this.CickID);
                        if (npcEntry != null && npcEntry.Events != null && npcEntry.Events.Count > 0 && mapData.Events != null)
                        {
                            foreach (var evId in npcEntry.Events)
                            {
                                var ev = mapData.Events.FirstOrDefault(e => e.clickID == evId);
                                if (ev?.SubEntry != null)
                                {
                                    foreach (var sub in ev.SubEntry)
                                    {
                                        if (sub?.SubEntry == null) continue;
                                        foreach (var op in sub.SubEntry)
                                        {
                                            if (op.DialogPtr == 2 && op.dialog2 == 1 && op.dialog3 > 0)
                                            {
                                                return ((uint)op.dialog1 << 16) | (uint)op.dialog3;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Also check direct clickID event in mapData.Events
                        if (mapData.Events != null)
                        {
                            var ev = mapData.Events.FirstOrDefault(e => e.clickID == this.CickID);
                            if (ev?.SubEntry != null)
                            {
                                foreach (var sub in ev.SubEntry)
                                {
                                    if (sub?.SubEntry == null) continue;
                                    foreach (var op in sub.SubEntry)
                                    {
                                        if (op.DialogPtr == 2 && op.dialog2 == 1 && op.dialog3 > 0)
                                        {
                                            return ((uint)op.dialog1 << 16) | (uint)op.dialog3;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            // 2. Default Authentic Kelan Village Greeting
            return 0x21284C; // "Welcome to Kelan Village."
        }

        private static SendPacket BuildDialogueStep(byte clickId, uint talkId, byte step, byte portraitType)
        {
            SendPacket dlgPkt = new SendPacket();
            dlgPkt.Pack8(20);                                 // [0] AC
            dlgPkt.Pack8(1);                                  // [1] SubCode
            dlgPkt.Pack8(0); dlgPkt.Pack8(0); dlgPkt.Pack8(0); // [2-4] session padding
            dlgPkt.Pack8(step);                               // [5] step
            dlgPkt.Pack8(1);                                  // [6] fixed
            dlgPkt.Pack8(portraitType);                       // [7] portrait 3=NPC, 7=Player
            dlgPkt.Pack8(clickId);                            // [8] npc click id
            dlgPkt.Pack8(0);                                  // [9] padding
            dlgPkt.Pack8(1); dlgPkt.Pack8(0); dlgPkt.Pack8(0); dlgPkt.Pack8(0); // [10-13] 4-byte flags
            dlgPkt.Pack8(0);                                  // [14] padding
            dlgPkt.Pack8((byte)(talkId & 0xFF));              // [15] TalkID LSB
            dlgPkt.Pack8((byte)((talkId >> 8) & 0xFF));       // [16] TalkID MID
            dlgPkt.Pack8((byte)((talkId >> 16) & 0xFF));      // [17] TalkID MSB
            return dlgPkt;
        }

        public override void Interact(Player src, byte? answer = null)
        {
            DebugSystem.Write($"[QuestNpc] Interact called. NPC Info: Name='{Name}', ID={CickID}, Level={Level}, HP={HP}, Element={Element}");
            src.Send(Tools.FromFormat("bb", 20, 8));
        }

        public override void Interact(Player src, byte? answer = null, params Code.ShoppingCart[] items)
        {
            DebugSystem.Write($"[QuestNpc] Interact called (Items={items?.Length}). NPC Info: Name='{Name}', ID={CickID}, Level={Level}, HP={HP}, Element={Element}");
            src.Send(Tools.FromFormat("bb", 20, 8));
        }
    }
}
