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

            // Domestic animals in villages / pens
            if (lower.Contains("pig") || lower.Contains("cow") || lower.Contains("sheep") || 
                lower.Contains("chicken") || lower.Contains("duck") || lower.Contains("goat") ||
                lower.Contains("horse") || lower.Contains("dog") || lower.Contains("cat") ||
                lower.Contains("shiba") || lower.Contains("mary") || lower.Contains("lina") ||
                lower.Contains("roca") || lower.Contains("niss") || lower.Contains("sam") ||
                lower.Contains("fred") || lower.Contains("eliza") || lower.Contains("clive"))
            {
                return false;
            }

            // Friendly human / citizen / town NPC keywords
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

            // Explicit static prop names, ground items, and gathering nodes
            if (lower.Contains("chest") || lower.Contains("box") || lower.Contains("crate") ||
                lower.Contains("barrel") || lower.Contains("pot") || lower.Contains("machine") ||
                lower.Contains("wood") || lower.Contains("driftwood") || lower.Contains("stone") || 
                lower.Contains("clay") || lower.Contains("mine") || lower.Contains("herb") || 
                lower.Contains("tree") || lower.Contains("coconut") || lower.Contains("fruit") ||
                lower.Contains("ore") || lower.Contains("flower") || lower.Contains("grass") ||
                lower.Contains("seed") || lower.Contains("leaf") || lower.Contains("sea water") ||
                lower.Contains("water") || lower.Contains("bamboo") || lower.Contains("vine") ||
                lower.Contains("kelp") || lower.Contains("mushroom") || lower.Contains("salt") ||
                lower.Contains("rice") || lower.Contains("meat") || lower.Contains("shell") ||
                lower.Contains("door") || lower.Contains("switch") || lower.Contains("lever") ||
                lower.Contains("statue") || lower.Contains("sign") || lower.Contains("well") ||
                lower.Contains("tent") || lower.Contains("portal") || lower.Contains("warp"))
            {
                return false;
            }

            // Monster templates in WLO (17000 - 17999, e.g. Jellies, Wolves, Beetles, Snails, Boars)
            if ((TemplateID >= 17000 && TemplateID <= 17999) || 
                Game.Battle.MonsterDropManager.MonsterLootTables.ContainsKey(TemplateID))
                return true;

            // Known monster keywords
            if (lower.Contains("monster") || lower.Contains("wolf") || lower.Contains("snail") ||
                lower.Contains("spider") || lower.Contains("snake") || lower.Contains("bat") ||
                lower.Contains("treant") || lower.Contains("tiger") || lower.Contains("bear") ||
                lower.Contains("beetle") || lower.Contains("eagle") || lower.Contains("shark") ||
                lower.Contains("crab") || lower.Contains("jellyfish") || lower.Contains("jelly") ||
                lower.Contains("slime") || lower.Contains("boar") || lower.Contains("golem") ||
                lower.Contains("spirit") || lower.Contains("ghost") || lower.Contains("scorpion") ||
                lower.Contains("wasp") || lower.Contains("bee") || lower.Contains("flower monster") ||
                lower.Contains("plant"))
            {
                return true;
            }

            return false;
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

                // --- 0.1 PRIMARY: Fully dynamic native eve.dat / eve.emg event resolution ---
                // Resolves NPC ClickID -> MapObjectEntries.Events -> EventsinMapEntries
                // Directly pulls all authentic chest drops (e.g. Map 10036 chests #32074, #32075), gathering items (Coconuts #41066),
                // dialogues, multi-step quests, companions, and warp events directly from eve.dat.
                if (src.CurMap is GameMap gmap && EveEventInterpreter.TryExecute(src, gmap, (ushort)this.CickID))
                {
                    return;
                }

                // --- 0.1 PROPS KEEPER (Character-bound Storage Vault across all maps) ---
                // Verified from propskeeper.pcapng (Frames 04-10) — Global ID 0x00019898 ensures identical shared items across all maps
                if (lowerName.Contains("props keep") || lowerName.Contains("keeper") || lowerName.Contains("keep") || 
                    lowerName.Contains("storage") || lowerName.Contains("bank") || lowerName.Contains("vault") ||
                    lowerName.Contains("exchanger") || lowerName.Contains("stock") ||
                    this.TemplateID == 14134 || this.TemplateID == 14181 || this.TemplateID == 14157)
                {
                    src.OpenPropsKeeper();
                    DebugSystem.Write($"[QuestNpc] Opened Character Props Keeper Vault for {src.CharName}");
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



                string dialogueText;

                // Ship Captain (Map 10024-10028 or TemplateID 10002 or Name contains Captain)
                // Triggers official storm animation cutscene (ilkgorevinanimasyonlukisimlari.pcapng Frames 1834-1941)
                if (lowerName.Contains("captain") || lowerName.Contains("kaptan") || this.TemplateID == 10002 ||
                    (src.CurMap?.MapID >= 10024 && src.CurMap?.MapID <= 10028 && (this.CickID == 10 || lowerName.Contains("captain"))))
                {
                    // Lock player movement
                    src.Send(Tools.FromFormat("bbb", 6, 2, 1));
                    src.QueueData.Clear();

                    // Step 2 (Captain) — TalkID 0x0175AD (30125: "Dark clouds on horizon...")
                    SendPacket s2 = new SendPacket();
                    s2.PackArray(new byte[] { 20, 1, 0, 0, 0, 2, 1, 3, (byte)this.CickID, 0, 1, 0, 0, 0, 0, 0xAD, 0x75, 0x01 });
                    src.QueueData.Enqueue(s2);

                    src.OnInteractionComplete = () =>
                    {
                        // Trigger official storm cutscene (AC 186 Sub 12)
                        SendPacket stormPkt = new SendPacket();
                        stormPkt.PackArray(new byte[] { 186, 12, 1, 0, 0, 0, 0 });
                        src.Send(stormPkt);
                        DebugSystem.Write($"[QuestNpc] Triggered Storm Animation Cutscene (AC 186:12) for {src.CharName}");
                    };

                    // Step 1 (Captain) — TalkID 0x0175AC (30124: "Voyage is going smoothly...")
                    SendPacket s1 = new SendPacket();
                    s1.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 1, 3, (byte)this.CickID, 0, 1, 0, 0, 0, 0, 0xAC, 0x75, 0x01 });
                    src.Send(s1);
                    return;
                }

                // Map 10036 (Robinson Beach) Raft Chest (ClickID: 7) - Official PCAP Cutscene & Robinson Recruit (Frames 0919-0960)
                if (src.CurMap?.MapID == 10036 && this.CickID == 7)
                {
                    // Frame 0920: Prop anim + Raft item grant + lock
                    SendPacket openPkt = new SendPacket();
                    openPkt.PackArray(new byte[] { 22, 1, 7, 0, 1 });
                    src.Send(openPkt);

                    // Frame 0920: Add Raft Item (48016)
                    src.Inv.AddItem(48016, 1);

                    SendPacket questFlag = new SendPacket();
                    questFlag.PackArray(new byte[] { 24, 1, 0x0E, 0x2F, 1 });
                    src.Send(questFlag);

                    src.Send(Tools.FromFormat("bbb", 6, 2, 1));
                    src.Send(Tools.FromFormat("bb", 20, 10));

                    // Cutscene dialogue queue (Frames 0922-0955)
                    // TalkIDs verified from Talk.dat (direct byte offset, reversed text)
                    // 0x063ED2 = "This is a deserted island. How did you end up here?"
                    // 0x0640DB = "I think you must have been shipwrecked and drifted to this island..."
                    // 0x06423D = "It has been 28 years since I drifted to this island."
                    // 0x063D59 = next robinson dialogue (sequential)
                    // 0x063F4A = next player dialogue
                    src.QueueData.Clear();

                    // Step 2 (Player response) — talkId 0x063F4A
                    SendPacket s2 = new SendPacket();
                    s2.PackArray(new byte[] { 20, 1, 0, 0, 0, 2, 1, 7, 0, 0, 1, 0, 0, 0, 0, 0x4A, 0x3F, 0x06 });
                    src.QueueData.Enqueue(s2);

                    // Step 3 (Robinson) — talkId 0x0640DB
                    SendPacket s3 = new SendPacket();
                    s3.PackArray(new byte[] { 20, 1, 0, 0, 0, 3, 1, 3, 1, 0, 1, 0, 0, 0, 0, 0xDB, 0x40, 0x06 });
                    src.QueueData.Enqueue(s3);

                    // Step 4 (Robinson) — talkId 0x06423D
                    SendPacket s4 = new SendPacket();
                    s4.PackArray(new byte[] { 20, 1, 0, 0, 0, 4, 1, 3, 1, 0, 1, 0, 0, 0, 0, 0x3D, 0x42, 0x06 });
                    src.QueueData.Enqueue(s4);

                    // Step 5 (Robinson) — talkId 0x0408DC (ship/island context)
                    SendPacket s5 = new SendPacket();
                    s5.PackArray(new byte[] { 20, 1, 0, 0, 0, 5, 1, 3, 1, 0, 1, 0, 0, 0, 0, 0xDC, 0x08, 0x04 });
                    src.QueueData.Enqueue(s5);

                    // Step 6 (Burke Tiger) — talkId 0x032B3E "Here is some fruit cake for you to give her."
                    SendPacket s6 = new SendPacket();
                    s6.PackArray(new byte[] { 20, 1, 0, 0, 0, 6, 1, 3, 8, 0, 1, 0, 0, 0, 0, 0x3E, 0x2B, 0x03 });
                    src.QueueData.Enqueue(s6);

                    src.OnInteractionComplete = () =>
                    {
                        // Quest 82 (AC 24:5)
                        SendPacket q82 = new SendPacket();
                        q82.PackArray(new byte[] { 24, 5, 0x52, 0, 1 });
                        src.Send(q82);

                        // Robinson join fanfare (AC 22:10)
                        SendPacket fanfare = new SendPacket();
                        fanfare.PackArray(new byte[] { 22, 10, 1, 0, 0xFF, 0xFF });
                        src.Send(fanfare);

                        // Recruits Robinson into party (Pet ID 12178)
                        QuestRelated.QuestManager.SendCompanionReward(src, 12178, "Robinson");

                        // Active battle stance (AC 19 Sub 1)
                        SendPacket battleStance = new SendPacket();
                        battleStance.PackArray(new byte[] { 19, 1, 0x92, 0x2F, 0, 0 });
                        src.Send(battleStance);

                        // Quest 889 (AC 24:5)
                        SendPacket q889 = new SendPacket();
                        q889.PackArray(new byte[] { 24, 5, 0x79, 0x03, 1 });
                        src.Send(q889);

                        // Unlock player
                        src.Send(Tools.FromFormat("bb", 20, 8));
                        src.Send(Tools.FromFormat("bb", 5, 4));
                    };

                    // Step 1 (Robinson intro) — talkId 0x063ED2 "This is a deserted island. How did you end up here?"
                    SendPacket s1 = new SendPacket();
                    s1.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 1, 3, 1, 0, 1, 0, 0, 0, 0, 0xD2, 0x3E, 0x06 });
                    src.Send(s1);
                    return;
                }



                // 1.5 Handle Shopkeeper NPCs (Props Shop, Weapon Shop, Armor Shop, etc.)
                if (lowerName.Contains("shop") || lowerName.Contains("sho") || lowerName.Contains("vendor") || 
                    lowerName.Contains("merchant") || lowerName.Contains("trader") || lowerName.Contains("grocer") || 
                    lowerName.Contains("blacksmith") || (this.TemplateID >= 13000 && this.TemplateID <= 13999))
                {
                    src.Send(Tools.FromFormat("bb", 20, 8));
                    src.Send(Tools.FromFormat("bb", 5, 4));
                    src.SendSystemMessage($"🏪 [{Name}]: Welcome to my shop, traveler! Feel free to browse our wares.");
                    DebugSystem.Write($"[QuestNpc] Handled Shop NPC interaction for '{Name}' (ClickID: {this.CickID}) with {src.CharName}");
                    return;
                }

                // 1.6 Handle Witch Doctor / Doctor / Clinic (Free full HP & SP healing)
                if (lowerName.Contains("doctor") || lowerName.Contains("witch") || lowerName.Contains("nurse") || 
                    lowerName.Contains("healer") || lowerName.Contains("clinic") || this.TemplateID == 14151)
                {
                    if (src.Eqs != null)
                    {
                        src.Eqs.CurHP = src.Eqs.FullHP;
                        src.Eqs.CurSP = src.Eqs.FullSP;
                        src.Eqs.Send8_1(true);
                    }
                    src.Send(Tools.FromFormat("bb", 20, 8));
                    src.Send(Tools.FromFormat("bb", 5, 4));
                    src.SendSystemMessage($"✨ [{Name}]: HP and SP fully restored! (HP: {src.Eqs?.CurHP}/{src.Eqs?.FullHP}, SP: {src.Eqs?.CurSP}/{src.Eqs?.FullSP})");
                    DebugSystem.Write($"[QuestNpc] Witch Doctor '{Name}' healed {src.CharName} to full HP/SP.");
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





                // 1. Map 10027 Arcade Machines: Whack-a-Mole (Machine a / ClickID 6) & Woodcutting (Machine b / ClickID 7)
                if (src.CurMap?.MapID == 10027 && (this.CickID == 6 || this.CickID == 7))
                {
                    src.QueueData.Clear();
                    byte minigameType = 3; // Type 3 = Whack-a-Mole (ClickID 6 / Game Machine a)
                    uint seed = 0x012AF8;

                    if (this.CickID == 7)
                    {
                        minigameType = 4; // Type 4 = Woodcutting / Archery (ClickID 7 / Game Machine b)
                        seed = 0x012710;
                    }

                    src.OnMinigameWon = () =>
                    {
                        src.SendSystemMessage("🎉 Congratulations! You achieved victory in the arcade minigame!");
                    };
                    src.OnMinigameLost = () =>
                    {
                        src.SendSystemMessage("❌ Minigame ended. Try again!");
                    };

                    SendPacket startPkt = new SendPacket();
                    startPkt.PackArray(new byte[] { 57, 1, minigameType });
                    startPkt.Pack8((byte)(seed & 0xFF));
                    startPkt.Pack8((byte)((seed >> 8) & 0xFF));
                    startPkt.Pack8((byte)((seed >> 16) & 0xFF));
                    src.Send(startPkt);

                    SendPacket lockPkt = new SendPacket();
                    lockPkt.PackArray(new byte[] { 20, 9 });
                    src.Send(lockPkt);

                    DebugSystem.Write($"[QuestNpc] Launched Arcade Minigame (Type: {minigameType}, Seed: 0x{seed:X}) for {src.CharName}");
                    return;
                }

                // 2. Map 10036 / Robinson Beach: Burke the Tiger (ClickID 8, TemplateID 11019)
                // TemplateID check omitted: DB override may set it to 0 — MapID+ClickID uniquely identifies Burke
                if (src.CurMap?.MapID == 10036 && this.CickID == 8)
                {
                    src.QueueData.Clear();
                    src.Send(Tools.FromFormat("bbb", 6, 2, 1));
                    src.Send(Tools.FromFormat("bb", 20, 10));

                    // Burke dialogue step — uses portrait 7 (animal/pet) style
                    SendPacket burkePkt = BuildDialogueStep((byte)this.CickID, 0x032B3E, step: 1, portraitType: 7);
                    src.OnInteractionComplete = () =>
                    {
                        src.Send(Tools.FromFormat("bb", 20, 8));
                        src.Send(Tools.FromFormat("bb", 5, 4));
                    };
                    src.Send(burkePkt);
                    DebugSystem.Write($"[QuestNpc] Burke (Map 10036 ClickID 8) interaction handled for {src.CharName}");
                    return;
                }
                // 5. Map 10027 / Ship Cabin: Breillat Bartender (ClickID 5, TemplateID 10000)
                // 5. Map 10027 / Ship Cabin: Breillat Bartender (ClickID 5, TemplateID 10000)
                // Confirmed from eve.Emg: Map 10027 ClickID 5 = Breillat
                else if (src.CurMap?.MapID == 10027 && this.CickID == 5)
                {
                    src.QueueData.Clear();
                    // Direct dialogue — no cutscene preamble
                    // talkId 0x03776E verified from PCAP: 2xreddemvoucher... Frame 1517
                    // raw: 00 00 00 01 01 03 05 00 01 00 00 00 00 6E 77 03
                    SendPacket step1 = BuildDialogueStep((byte)this.CickID, 0x03776E, step: 1, portraitType: 3);

                    // talkId 0x0577EC verified from PCAP: 2xreddemvoucher... Frame 1709
                    // raw: 00 00 00 01 01 03 05 00 01 00 00 00 00 EC 77 05
                    SendPacket step2 = BuildDialogueStep((byte)this.CickID, 0x0577EC, step: 2, portraitType: 3);
                    src.QueueData.Enqueue(step2);

                    src.OnInteractionComplete = () =>
                    {
                        src.Send(Tools.FromFormat("bb", 20, 8));
                        src.Send(Tools.FromFormat("bb", 5, 4));
                    };
                    src.Send(step1);
                    return;
                }

                // 4. Check if NPC matches registered QuestManager handler
                if (QuestRelated.QuestManager.TryHandleNpcQuest(src, Name, TemplateID, out string questDialogue))
                {
                    dialogueText = questDialogue;

                    // If Niss / Quest Battle NPC, trigger battle if in progress
                    var qDef = QuestRelated.QuestManager.FindQuestForNpc(Name, TemplateID);
                    if (qDef != null && qDef.Type == QuestRelated.QuestType.MonsterBattle && qDef.BattleMonsterID > 0)
                    {
                        if (src.Quests.TryGetValue(qDef.QuestID, out var pq) && pq.State == QuestRelated.QuestState.InProgress)
                        {
                            src.Send(Tools.FromFormat("bb", 20, 8));
                            // Start PvE battle vs Quest Monster
                            Battle.PvEBattleManager.StartPvEBattle(src, this.CickID, qDef.BattleMonsterName ?? "Wolf Guard", npcLv: 10, npcHp: 250);
                            return;
                        }
                    }
                }
                else if (this.TemplateID == 14091 || (src.CurMap?.MapID == 10001 && this.CickID == 1))
                {
                    if (src.Quests == null) src.Quests = new Dictionary<uint, QuestRelated.PlayerQuest>();
                    if (!src.Quests.ContainsKey(10035))
                    {
                        src.Quests[10035] = new QuestRelated.PlayerQuest(10035, QuestRelated.QuestState.InProgress, 1);
                        QuestRelated.QuestManager.SavePlayerQuest(src, 10035);
                        QuestRelated.QuestManager.SendQuestUpdate(src, 10035, QuestRelated.QuestState.InProgress, 1);
                    }
                    dialogueText = "Greetings, traveler. The stars whisper of a great destiny awaiting you across the world. Step outside into the village square and receive the celestial Space Tent I have prepared for your journeys!";
                }
                else if (this.TemplateID == 14161 || this.TemplateID == 14162 || (src.CurMap?.MapID == 10000 && this.CickID == 2))
                {
                    dialogueText = "Greetings! I am Roca, daughter of the Chief. Are you ready for an exciting adventure across the islands?";
                }
                else if (this.TemplateID == 14049 || (src.CurMap?.MapID == 10000 && this.CickID == 3))
                {
                    dialogueText = "Hello there! Isn't this village wonderful? The breeze from the sea feels so refreshing.";
                }
                else if (this.TemplateID == 14118 || this.TemplateID == 14119)
                {
                    dialogueText = "Halt! Keep peace in the village. If you travel into the wilderness, be well prepared for monsters.";
                }
                else if (this.TemplateID == 14013)
                {
                    dialogueText = "Welcome to our town! If you need anything, don't hesitate to ask around.";
                }
                else if (this.TemplateID == 14005)
                {
                    dialogueText = "Hey! Have you seen any strange creatures outside? Stay safe out there!";
                }
                else if (this.TemplateID == 14140)
                {
                    dialogueText = "The flowers in the village are blooming beautifully today!";
                }
                else if (this.TemplateID == 19020 || this.TemplateID == 19021)
                {
                    dialogueText = "[Signpost]: East -> Harbor & Beach | West -> Village Square | North -> Chieftain's Manor";
                }
                else if (this.TemplateID == 11003)
                {
                    dialogueText = "Woof! (The loyal Shiba inu wags its tail happily.)";
                }
                else
                {
                    dialogueText = "Hello, traveller! Beautiful day, isn't it? Let me know if you need anything.";
                }

                // Brelliat Swap & Transformation Dialogue (Official PCAP 'brelliatlayerdegistirdim.pcapng')
                // Validated directly by MapID, ClickID and NPC TemplateID without name string dependency
                if (this.CickID == 5 && (src.CurMap?.MapID == 10000 || src.CurMap?.MapID == 60001))
                {
                    src.QueueData.Clear();

                    // Step 1: Lock player (AC 6 Sub 2) + Flag (AC 24 Sub 1) + Init (AC 20 Sub 10)
                    src.Send(Tools.FromFormat("bbb", 6, 2, 1));
                    SendPacket fPkt = new SendPacket();
                    fPkt.PackArray(new byte[] { 24, 1, 0x6E, 0xC3, 1 });
                    src.Send(fPkt);
                    src.Send(Tools.FromFormat("bb", 20, 10));

                    // Step 1 Dialog (TalkID 0x0777B0)
                    SendPacket step1 = BuildDialogueStep((byte)this.CickID, 0x0777B0, step: 1, portraitType: 3);
                    
                    // Step 2 Dialog (TalkID 0x0777B1)
                    SendPacket step2 = BuildDialogueStep((byte)this.CickID, 0x0777B1, step: 2, portraitType: 7);
                    src.QueueData.Enqueue(step2);

                    // Step 3 Dialog (TalkID 0x0777B2: "Would you like to swap places with me?")
                    SendPacket step3 = BuildDialogueStep((byte)this.CickID, 0x0777B2, step: 3, portraitType: 3);
                    src.QueueData.Enqueue(step3);

                    // Step 4 Choice Dialog (0x1E = Yes, 0x1F = No)
                    SendPacket step4Choice = new SendPacket();
                    step4Choice.PackArray(new byte[] { 20, 1, 0, 0, 0, 4, 6, 3, (byte)this.CickID, 0, 0, 0, 0, 0, 0, 1, 0, 7 });
                    src.QueueData.Enqueue(step4Choice);

                    // Handle choice when client submits (AC 20 Sub 9)
                    src.OnDialogueChoice = (choice) =>
                    {
                        if (choice == 0x1E) // YES (Evet) -> Transform into Brelliat!
                        {
                            src.QueueData.Clear();

                            // Confirmation dialog (TalkID 0x0977B4)
                            SendPacket stepYes = BuildDialogueStep((byte)this.CickID, 0x0977B4, step: 1, portraitType: 3);
                            src.Send(stepYes);

                            // Trigger Transformation visual effect & Model ID Swap (Model 22206 / 0x56BE)
                            src.OnInteractionComplete = () =>
                            {
                                // Quest / Flag Update (AC 24 Sub 5)
                                SendPacket qPkt = new SendPacket();
                                qPkt.PackArray(new byte[] { 24, 5, 0x45, 0, 1 });
                                src.Send(qPkt);

                                // Particle FX Animation (AC 22 Sub 10)
                                SendPacket fx = new SendPacket();
                                fx.PackArray(new byte[] { 22, 10, (byte)this.CickID, 0, 0xFF, 0xFF });
                                src.Send(fx);
                                src.CurMap?.Broadcast(fx);

                                // Transform Player Character Model to Brelliat (AC 5 Sub 1)
                                src.TransformedModelID = 22206; // 0x56BE
                                SendPacket transformPkt = new SendPacket();
                                transformPkt.PackArray(new byte[] { 5, 1 });
                                transformPkt.Pack32(src.CharID);
                                transformPkt.Pack16(22206);
                                src.Send(transformPkt);
                                src.CurMap?.Broadcast(transformPkt);

                                // Step 5 Final Dialogue (TalkID 0x0977B6)
                                SendPacket step5 = BuildDialogueStep((byte)this.CickID, 0x0977B6, step: 5, portraitType: 7);
                                src.Send(step5);

                                src.OnInteractionComplete = () =>
                                {
                                    src.Send(Tools.FromFormat("bb", 20, 8));
                                    src.Send(Tools.FromFormat("bb", 5, 4));
                                };

                                src.SendSystemMessage("🐴 [Brelliat]: ✨ Dönüşüm tamamlandı! Brelliat ile yer değiştirdiniz ve Brelliat'a dönüştünüz!");
                            };
                        }
                        else // NO (Hayır)
                        {
                            src.QueueData.Clear();
                            SendPacket stepNo = BuildDialogueStep((byte)this.CickID, 0x0A76F0, step: 1, portraitType: 3);
                            src.Send(stepNo);

                            src.OnInteractionComplete = () =>
                            {
                                src.Send(Tools.FromFormat("bb", 20, 8));
                                src.Send(Tools.FromFormat("bb", 5, 4));
                            };
                            src.SendSystemMessage("🐴 [Brelliat]: Kişneme! Belki başka bir zaman!");
                        }
                    };

                    src.Send(step1);
                    DebugSystem.Write($"[QuestNpc] Started Brelliat swap/transformation dialogue sequence for {src.CharName}");
                    return;
                }

                // 2. Captain Prologue Dialogue Sequence (100% Byte-for-Byte from official PCAP Frame 0310-0328)
                if (src.CurMap?.MapID == 10017 && (this.CickID == 10 || this.CickID == 4 || this.CickID == 11))
                {
                    // Frame 0314: Step 2 AC 20:1 payload from PCAP
                    SendPacket step2 = new SendPacket();
                    step2.PackArray(new byte[] { 20, 1, 0, 0, 0, 2, 1, 3, (byte)this.CickID, 0, 1, 0, 0, 0, 0, 0xAD, 0x75, 0x01 });
                    src.QueueData.Enqueue(step2);

                    // Frame 0318: Step 3 AC 20:1 payload from PCAP (Shipwreck blackout / choice)
                    SendPacket step3 = new SendPacket();
                    step3.PackArray(new byte[] { 20, 1, 0, 0, 0, 3, 5, 0, 0, 0, 2, 0x7B, 0, 0, 0, 0, 0, 0 });
                    src.QueueData.Enqueue(step3);

                    src.OnInteractionComplete = () =>
                    {
                        // Frame 0328: Warp to Map 10036 (X: 1038, Y: 2235)
                        QuestRelated.QuestManager.SendQuestUpdate(src, 74, QuestRelated.QuestState.Completed);
                        src.QueueData.Clear();
                        src.PendingBeachCutscene = true;
                        var warp = new WarpData() { DstMap = 10036, DstX_Axis = 1038, DstY_Axis = 2235 };
                        src.CurMap?.Teleport(TeleportType.CmD, src, 0, warp);
                    };

                    // Frame 0310: Step 1 AC 20:1 payload from PCAP
                    SendPacket step1 = new SendPacket();
                    step1.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 1, 3, (byte)this.CickID, 0, 1, 0, 0, 0, 0, 0xAC, 0x75, 0x01 });
                    src.Send(step1);
                    DebugSystem.Write($"[QuestNpc] Sent exact PCAP Captain prologue dialogue sequence for '{Name}'.");
                    return;
                }
                // Interactive Lootable Map Props (Crate / Chest / Barrel - Official PCAPs 'crateatiklayipstatedegistiripcikolatakazanma.pcapng' & 'digersandiklaritoplama.pcapng')
                if ((src.CurMap?.MapID == 10036 || src.CurMap?.MapID == 10000) && (this.CickID == 3 || this.CickID == 5 || this.CickID == 6))
                {
                    src.QueueData.Clear();

                    // Step 1: Change Prop State to Opened (AC 22 Sub 1: 16 01 [ClickID] 01) or flash particle (AC 22 Sub 10)
                    if (this.CickID == 6)
                    {
                        SendPacket fx = new SendPacket();
                        fx.PackArray(new byte[] { 22, 10, (byte)this.CickID, 0, 0xFF, 0xFF });
                        src.Send(fx);
                        src.CurMap?.Broadcast(fx);
                    }
                    else
                    {
                        SendPacket statePkt = new SendPacket();
                        statePkt.PackArray(new byte[] { 22, 1, (byte)this.CickID, 0, 1 });
                        src.Send(statePkt);
                        src.CurMap?.Broadcast(statePkt);
                    }

                    // Step 2: Lock movement (AC 6 Sub 2) + Init sound (AC 20 Sub 11 & 10)
                    src.Send(Tools.FromFormat("bbb", 6, 2, 1));
                    src.Send(Tools.FromFormat("bb", 20, 11));
                    src.Send(Tools.FromFormat("bb", 20, 10));

                    // Step 3: Configure authentic rewards and notices based on ClickID
                    uint questId = 70;
                    ushort itemId = 32001; // Chocolate
                    string itemName = "Chocolate";
                    byte[] noticeBytes = new byte[] { 23, 6, 0x4B, 0x7D, 0x08 };

                    if (this.CickID == 5)
                    {
                        questId = 76;
                        itemId = 32005; // Healing Herb / Coconut
                        itemName = "Coconut";
                        noticeBytes = new byte[] { 23, 6, 0x49, 0x7D, 0x0C };
                    }
                    else if (this.CickID == 6)
                    {
                        questId = 74;
                        itemId = 32010; // Gold Coins / Special Starter Item
                        itemName = "Gold Coins";
                        noticeBytes = new byte[] { 23, 6, 0x6A, 0xA0, 0x01 };
                    }

                    SendPacket notice = new SendPacket();
                    notice.PackArray(noticeBytes);
                    src.QueueData.Enqueue(notice);

                    src.OnInteractionComplete = () =>
                    {
                        // Progress Quest
                        QuestRelated.QuestManager.SendQuestUpdate(src, questId, QuestRelated.QuestState.Completed);

                        // Award item to inventory
                        if (src.Inv != null)
                        {
                            src.Inv.AddItem(itemId, 1);
                            src.SendSystemMessage($"🎁 You opened the chest/crate and obtained 1x {itemName}!");
                        }

                        src.Send(Tools.FromFormat("bb", 20, 8));
                        src.Send(Tools.FromFormat("bb", 5, 4));
                    };

                    src.Send(notice);
                    DebugSystem.Write($"[QuestNpc] Player {src.CharName} opened prop (ClickID: {this.CickID}) and received {itemName}.");
                    return;
                }
                else if (((src.CurMap?.MapID == 10036 || src.CurMap?.MapID == 10000) && (this.CickID == 1 || this.CickID == 7)) || this.TemplateID == 12178 || this.TemplateID == 14044)
                {
                    src.QueueData.Clear();

                    // Check if player has already completed Robinson's initial talk
                    bool isReadyToJoin = (src.Quests != null && src.Quests.ContainsKey(82));

                    if (isReadyToJoin)
                    {
                        // Official PCAP 'robinsonunpetolarakbattlemodundaeklenmesiveharitadanyokolmasi.pcapng'
                        // Step 1: Active Animation (AC 22 Sub 1) + Notice Banner (AC 23 Sub 6) + Dialogue Init
                        SendPacket statePkt = new SendPacket();
                        statePkt.PackArray(new byte[] { 22, 1, (byte)this.CickID, 0, 1 });
                        src.Send(statePkt);

                        SendPacket bannerPkt = new SendPacket();
                        bannerPkt.PackArray(new byte[] { 23, 6, 0x90, 0xBB, 0x01 });
                        src.Send(bannerPkt);

                        src.Send(Tools.FromFormat("bbb", 6, 2, 1));
                        src.Send(Tools.FromFormat("bb", 20, 10));

                        // Multi-stage dialogues (0x034F83, 0x036F06, 0x036F07, 0x036F08, 0x036F09, 0x036F0A, 0x032B3E, 0x036F0B)
                        uint[] robTalks = new uint[] { 0x034F83, 0x036F06, 0x036F07, 0x036F08, 0x036F09, 0x036F0A, 0x032B3E, 0x036F0B };
                        for (int i = 1; i < robTalks.Length; i++)
                        {
                            SendPacket step = BuildDialogueStep((byte)this.CickID, robTalks[i], step: (byte)(i + 1), portraitType: 3);
                            src.QueueData.Enqueue(step);
                        }

                        src.OnInteractionComplete = () =>
                        {
                            // Despawn Particle Animation (AC 22 Sub 10)
                            SendPacket fx = new SendPacket();
                            fx.PackArray(new byte[] { 22, 10, (byte)this.CickID, 0, 0xFF, 0xFF });
                            src.Send(fx);
                            src.CurMap?.Broadcast(fx);

                            // Despawn Robinson NPC from the map (AC 19 Sub 1: 13 01 92 2F 00 00)
                            SendPacket despawnPkt = new SendPacket();
                            despawnPkt.PackArray(new byte[] { 19, 1, 0x92, 0x2F, 0, 0 });
                            src.Send(despawnPkt);
                            src.CurMap?.Broadcast(despawnPkt);

                            // Add Robinson as Battle Companion / Pet (AC 15 Sub 1 / Template 12178)
                            QuestRelated.QuestManager.SendCompanionReward(src, 12178, "Robinson");

                            // Advance Quests 82 and 889
                            QuestRelated.QuestManager.SendQuestUpdate(src, 82, QuestRelated.QuestState.Completed);
                            QuestRelated.QuestManager.SendQuestUpdate(src, 889, QuestRelated.QuestState.Completed);

                            src.Send(Tools.FromFormat("bb", 20, 8));
                            src.Send(Tools.FromFormat("bb", 5, 4));
                            src.SendSystemMessage("🌟 Robinson Crusoe joined your party as a battle companion!");
                        };

                        SendPacket firstStep = BuildDialogueStep((byte)this.CickID, robTalks[0], step: 1, portraitType: 3);
                        src.Send(firstStep);
                        DebugSystem.Write($"[QuestNpc] Dispatched Robinson pet recruitment sequence for {src.CharName}");
                        return;
                    }
                    else
                    {
                        // Official PCAP 'robinsonlakonusma.pcapng'
                        // No AC6 Sub2 lock — direct dialogue like Captain handler
                        // Step 1: talkId 0x054F5D (Frame 0350 | 5D 4F 05)
                        SendPacket step1 = BuildDialogueStep((byte)this.CickID, 0x054F5D, step: 1, portraitType: 3);

                        // Step 2: talkId 0x054F5E (Frame 0596 | 5E 4F 05)
                        SendPacket step2 = BuildDialogueStep((byte)this.CickID, 0x054F5E, step: 2, portraitType: 3);
                        src.QueueData.Enqueue(step2);

                        // Step 3: talkId 0x054F5F (Frame 0670 | 5F 4F 05)
                        SendPacket step3 = BuildDialogueStep((byte)this.CickID, 0x054F5F, step: 3, portraitType: 3);
                        src.QueueData.Enqueue(step3);

                        // Step 4: Choice packet (type=6) — raw from PCAP Frame 0282
                        // 00 00 00 01 06 03 01 00 00 00 00 00 00 01 00 03
                        SendPacket step4Choice = new SendPacket();
                        step4Choice.PackArray(new byte[] { 20, 1, 0, 0, 0, 4, 6, 3, (byte)this.CickID, 0, 0, 0, 0, 0, 0, 1, 0, 3 });
                        src.QueueData.Enqueue(step4Choice);

                        src.OnDialogueChoice = (choice) =>
                        {
                            src.QueueData.Clear();

                            // Step 5: talkId 0x054F60 (Frame 0768 | 60 4F 05)
                            SendPacket step5 = BuildDialogueStep((byte)this.CickID, 0x054F60, step: 1, portraitType: 3);
                            src.Send(step5);

                            // Step 6: talkId 0x054F61 (Frame 0817 | 61 4F 05)
                            SendPacket step6 = BuildDialogueStep((byte)this.CickID, 0x054F61, step: 2, portraitType: 3);
                            src.QueueData.Enqueue(step6);

                            // Update Quest 77 & 82
                            QuestRelated.QuestManager.SendQuestUpdate(src, 77, QuestRelated.QuestState.InProgress, 1);
                            if (src.Quests == null) src.Quests = new Dictionary<uint, QuestRelated.PlayerQuest>();
                            src.Quests[82] = new QuestRelated.PlayerQuest(82, QuestRelated.QuestState.InProgress, 1);

                            src.OnInteractionComplete = () =>
                            {
                                src.Send(Tools.FromFormat("bb", 20, 8));
                                src.Send(Tools.FromFormat("bb", 5, 4));
                            };
                        };

                        src.Send(step1);
                        DebugSystem.Write($"[QuestNpc] Sent Robinson quest intro dialogue for {src.CharName}");
                        return;
                    }
                }
                else if (((src.CurMap?.MapID == 10036 || src.CurMap?.MapID == 10000) && this.CickID == 1) || this.TemplateID == 10727)
                {
                    src.QueueData.Clear();

                    // Lock player (AC 6 Sub 2)
                    src.Send(Tools.FromFormat("bbb", 6, 2, 1));

                    // Step 1: Dialogue 0x002EE0 (PCAP Frame 0043)
                    SendPacket step1 = new SendPacket();
                    step1.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 5, 0, 0, 0, 2, 0xE0, 0x2E, 0, 0, 0, 0, 0 });

                    // Step 2: Dialogue 0x002B03 (PCAP Frame 0482)
                    SendPacket step2 = new SendPacket();
                    step2.PackArray(new byte[] { 20, 1, 0, 0, 0, 2, 5, 0, 0, 0, 1, 0x03, 0x2B, 0, 0, 0, 0, 0 });
                    src.QueueData.Enqueue(step2);

                    src.OnInteractionComplete = () =>
                    {
                        // Add Monkey as Battle Companion / Pet (AC 15 Sub 1 / Template 10727)
                        QuestRelated.QuestManager.SendCompanionReward(src, 10727, "Monkey");

                        // Despawn Monkey NPC from map (AC 19 Sub 1: 13 01 0A 43 00 00)
                        SendPacket despawnPkt = new SendPacket();
                        despawnPkt.PackArray(new byte[] { 19, 1, 0x0A, 0x43, 0, 0 });
                        src.Send(despawnPkt);
                        src.CurMap?.Broadcast(despawnPkt);

                        // Particle Flash FX on Monkey (AC 22 Sub 10)
                        SendPacket fx = new SendPacket();
                        fx.PackArray(new byte[] { 22, 10, (byte)this.CickID, 0, 0xFF, 0xFF });
                        src.Send(fx);
                        src.CurMap?.Broadcast(fx);

                        // Complete Quest 2 (AC 24 Sub 5: 02 00 01)
                        QuestRelated.QuestManager.SendQuestUpdate(src, 2, QuestRelated.QuestState.Completed);

                        src.Send(Tools.FromFormat("bb", 20, 8));
                        src.Send(Tools.FromFormat("bb", 5, 4));
                        src.SendSystemMessage("🐒 The cute Monkey joined your party as a battle companion!");
                    };

                    src.Send(step1);
                    DebugSystem.Write($"[QuestNpc] Sent exact PCAP Monkey recruitment sequence for '{Name}'.");
                    return;
                }
                else
                {
                    // Generic NPC fallback: direct AC 20 Sub 1 dialogue
                    uint talkId = (this.CickID % 2 == 0) ? (uint)0x0175B9 : (uint)0x0175BA;

                    SendPacket step1 = BuildDialogueStep((byte)this.CickID, talkId, step: 1, portraitType: 3);
                    src.Send(step1);

                    src.OnInteractionComplete = () =>
                    {
                        src.Send(Tools.FromFormat("bb", 20, 8));
                        src.Send(Tools.FromFormat("bb", 5, 4));
                    };

                    src.SendSystemMessage($"💬 {Name}: {dialogueText}");
                    DebugSystem.Write($"[QuestNpc] Sent authentic dialogue window for '{Name}' (ClickID: {this.CickID}, TalkID: 0x{talkId:X}): '{dialogueText}'");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestNpc] Error in Interact: {ex.Message}");
                src.Send(Tools.FromFormat("bb", 20, 8));
            }
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
