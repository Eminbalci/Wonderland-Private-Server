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

            // 0. Handle Chest & Gathering Prop Respawning
            if (IsBroken)
            {
                if (now >= RespawnTime)
                {
                    IsBroken = false;
                    // Broadcast restoration animation (Frame 0 / Closed / Intact)
                    SendPacket restoreAnim = new SendPacket();
                    restoreAnim.PackArray(new byte[] { 22, 1, (byte)this.CickID, 0, 0 });
                    map.Broadcast(restoreAnim);
                    DebugSystem.Write($"[QuestNpc] Prop/Chest '{Name}' (ClickID: {this.CickID}) respawned and restored on Map {map.MapID}");
                }
                return;
            }

            // Never move static props, chests, or entities with invalid templates
            if (IsStaticNpc() || TemplateID == 0)
            {
                NextWalkTime = now.AddSeconds(300);
                return;
            }
            if (NextWalkTime > now) return;

            try
            {
                // 1. Scripted path walking from dat file (behavior 5 or has walksteps)
                if (WalkSteps != null && WalkSteps.Count > 0)
                {
                    var step = WalkSteps[CurStep % WalkSteps.Count];

                    SendPacket pkt = new SendPacket();
                    pkt.PackArray(new byte[] { 22, 2 });
                    pkt.Pack16(this.CickID);
                    pkt.Pack16((ushort)step.x);
                    pkt.Pack16((ushort)step.y);
                    pkt.Pack8(3); // speed

                    map.Broadcast(pkt);

                    this.X = (ushort)step.x;
                    this.Y = (ushort)step.y;

                    CurStep = (CurStep + 1) % WalkSteps.Count;
                    double delaySec = Math.Max(1.0, (double)step.delay / 1000.0);
                    NextWalkTime = now.AddSeconds(delaySec);
                }
                // 2. Random roaming ONLY for wild monsters (WalkBehavior == 4)
                else if (IsWildMonster() && (WalkBehavior == 4 || (TemplateID >= 16000 && TemplateID <= 19500)))
                {
                    int dx = NextRandom(-100, 101);
                    int dy = NextRandom(-100, 101);
                    int targetX = (int)this.X + dx;
                    int targetY = (int)this.Y + dy;

                    // If drifted too far from spawn origin, steer back towards spawn
                    if (Math.Abs(targetX - this.SpawnX) > 180 || Math.Abs(targetY - this.SpawnY) > 180)
                    {
                        targetX = this.SpawnX + NextRandom(-30, 31);
                        targetY = this.SpawnY + NextRandom(-30, 31);
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

                    double waitSec = NextRandomDouble(3.5, 8.0);
                    NextWalkTime = now.AddSeconds(waitSec);
                }
                else
                {
                    // Town NPCs, villagers, and static props do not wander randomly
                    NextWalkTime = now.AddSeconds(300);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestNpc] Error in Update for ClickID {this.CickID}: {ex.Message}");
                NextWalkTime = now.AddSeconds(10);
            }
        }

        public bool IsWildMonster()
        {
            if (IsStaticNpc() || this.TemplateID == 0)
                return false;

            string lower = (Name ?? "").ToLower().Trim();

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

            // Monster templates in WLO
            if (TemplateID >= 17000 && TemplateID <= 19500)
                return true;

            // Known monster keywords
            if (lower.Contains("monster") || lower.Contains("wolf") || lower.Contains("snail") ||
                lower.Contains("spider") || lower.Contains("snake") || lower.Contains("bat") ||
                lower.Contains("treant") || lower.Contains("tiger") || lower.Contains("bear") ||
                lower.Contains("beetle") || lower.Contains("eagle") || lower.Contains("shark") ||
                lower.Contains("crab") || lower.Contains("jellyfish") || lower.Contains("slime") ||
                lower.Contains("boar") || lower.Contains("golem") || lower.Contains("spirit") ||
                lower.Contains("ghost") || lower.Contains("scorpion") || lower.Contains("wasp") ||
                lower.Contains("bee") || lower.Contains("flower monster") || lower.Contains("plant"))
            {
                return true;
            }

            return false;
        }

        public bool IsStaticNpc()
        {
            if (this.TemplateID == 0)
                return true;

            // Props and chests on Newbie Beach (Map 10036: ClickID 6, ClickID 7, etc.)
            if (this.CickID == 6 || this.CickID == 7 || this.CickID == 10)
                return true;

            string lower = (Name ?? "").ToLower().Trim();
            if (string.IsNullOrEmpty(lower) || lower.StartsWith("npc_0") || lower.StartsWith("unknown"))
                return true;

            if (lower.Contains("portal") || lower.Contains("bank") || lower.Contains("atm") || 
                lower.Contains("guide") || lower.Contains("captain") ||
                lower.Contains("chest") || lower.Contains("crate") || lower.Contains("box") || 
                lower.Contains("treas") || lower.Contains("cask") || lower.Contains("barrel") || 
                lower.Contains("coconut") || lower.Contains("tree") || lower.Contains("bush") || 
                lower.Contains("cabinet") || lower.Contains("shelf") || lower.Contains("door") || 
                lower.Contains("gate") || lower.Contains("statue") || lower.Contains("table") || 
                lower.Contains("chair") || lower.Contains("bed") || lower.Contains("furn") || 
                lower.Contains("rock") || lower.Contains("stone") || lower.Contains("pot") || 
                lower.Contains("well") || lower.Contains("sign") || lower.Contains("tent") ||
                lower.Contains("flower") || lower.Contains("grass") ||
                lower.Contains("prop") || lower.Contains("obj") || lower.Contains("wood") ||
                lower.Contains("basket") || lower.Contains("sack") || lower.Contains("bag") ||
                lower.Contains("campfire") || lower.Contains("fire") || lower.Contains("furnace") ||
                lower.Contains("mine") || lower.Contains("ore") || lower.Contains("vein"))
            {
                return true;
            }
            return false;
        }

        public virtual void EvaluateQuestData(Player src)
        {
        }

        public override void Interact(Player src)
        {
            try
            {
                DebugSystem.Write($"[QuestNpc] Interaction with ClickID: {this.CickID}. NPC Info: Name='{Name}', Level={Level}, HP={HP}, Element={Element}");

                string dialogueText;
                string lowerName = (Name ?? "").ToLower();

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
                    src.QueueData.Clear();

                    // Step 2 (Player)
                    SendPacket s2 = new SendPacket();
                    s2.PackArray(new byte[] { 20, 1, 0, 0, 0, 2, 1, 7, 0, 0, 1, 0, 0, 0, 0, 0x06, 0x6F, 0x03 });
                    src.QueueData.Enqueue(s2);

                    // Step 3 (Player)
                    SendPacket s3 = new SendPacket();
                    s3.PackArray(new byte[] { 20, 1, 0, 0, 0, 3, 1, 7, 0, 0, 1, 0, 0, 0, 0, 0x07, 0x6F, 0x03 });
                    src.QueueData.Enqueue(s3);

                    // Step 5 (Robinson)
                    SendPacket s5 = new SendPacket();
                    s5.PackArray(new byte[] { 20, 1, 0, 0, 0, 5, 1, 3, 1, 0, 1, 0, 0, 0, 0, 0x08, 0x6F, 0x03 });
                    src.QueueData.Enqueue(s5);

                    // Step 6 (Robinson)
                    SendPacket s6 = new SendPacket();
                    s6.PackArray(new byte[] { 20, 1, 0, 0, 0, 6, 1, 3, 1, 0, 1, 0, 0, 0, 0, 0x09, 0x6F, 0x03 });
                    src.QueueData.Enqueue(s6);

                    // Step 7 (Player)
                    SendPacket s7 = new SendPacket();
                    s7.PackArray(new byte[] { 20, 1, 0, 0, 0, 7, 1, 7, 0, 0, 1, 0, 0, 0, 0, 0x0A, 0x6F, 0x03 });
                    src.QueueData.Enqueue(s7);

                    // Step 8 (Burke Tiger)
                    SendPacket s8 = new SendPacket();
                    s8.PackArray(new byte[] { 20, 1, 0, 0, 0, 8, 1, 3, 8, 0, 1, 0, 0, 0, 0, 0x3E, 0x2B, 0x03 });
                    src.QueueData.Enqueue(s8);

                    // Step 9 (Robinson)
                    SendPacket s9 = new SendPacket();
                    s9.PackArray(new byte[] { 20, 1, 0, 0, 0, 9, 1, 3, 1, 0, 1, 0, 0, 0, 0, 0x0B, 0x6F, 0x03 });
                    src.QueueData.Enqueue(s9);

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

                        // Unlock player (PCAP Frame 0960)
                        src.Send(Tools.FromFormat("bb", 20, 8));
                        src.Send(Tools.FromFormat("bb", 5, 4));
                    };

                    // Step 1 dialogue
                    SendPacket s1 = new SendPacket();
                    s1.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 1, 3, 1, 0, 1, 0, 0, 0, 0, 0x83, 0x4F, 0x03 });
                    src.Send(s1);
                    return;
                }

                // 1. Handle Map Props / Gathering Objects (Chests, Coconut trees, Cabinets, Bushes)
                if (lowerName.Contains("chest") || lowerName.Contains("treas") || lowerName.Contains("cask") || 
                    lowerName.Contains("coconut") || lowerName.Contains("cabinet") || lowerName.Contains("shelf") || lowerName.Contains("bick") ||
                    lowerName.Contains("ore") || lowerName.Contains("mine") || lowerName.Contains("crate") || lowerName.Contains("box"))
                {
                    // Check if chest/prop is currently broken / waiting to respawn
                    if (this.IsBroken)
                    {
                        int remainSec = Math.Max(1, (int)(this.RespawnTime - DateTime.Now).TotalSeconds);
                        SendPacket emptyNotice = Tools.FromFormat("bbbs", 23, 57, 0, $"Empty... Respawns in {remainSec}s.");
                        src.Send(emptyNotice);

                        src.Send(Tools.FromFormat("bb", 20, 8));
                        src.Send(Tools.FromFormat("bb", 5, 4));
                        return;
                    }

                    // Roll authentic drop from Map / Category Loot Table
                    var drop = ChestDropManager.RollDrop(src.CurMap?.MapID ?? 0, this.Name);

                    // Play prop open / break animation (AC 22 Sub 1) and broadcast to map
                    SendPacket anim = new SendPacket();
                    anim.PackArray(new byte[] { 22, 1, (byte)this.CickID, 0, 1 });
                    src.Send(anim);
                    src.CurMap?.Broadcast(anim);

                    // Add item to inventory
                    src.Inv.AddItem(drop.ItemID, drop.Count);

                    // Display authentic loot notification prompt (AC 23 Sub 57)
                    SendPacket notice = Tools.FromFormat("bbbs", 23, 57, 0, $"Obtain {drop.ItemName}");
                    src.Send(notice);

                    // Mark as broken and set respawn timer
                    this.IsBroken = true;
                    this.RespawnTime = DateTime.Now.AddSeconds(ChestDropManager.DefaultRespawnSeconds);

                    // Release movement lock
                    src.Send(Tools.FromFormat("bb", 20, 8));
                    src.Send(Tools.FromFormat("bb", 5, 4));
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
                    src.Send(Tools.FromFormat("bb", 20, 8));
                    src.Send(Tools.FromFormat("bb", 5, 4));
                    src.SendSystemMessage($"🏦 [{Name}]: Welcome! Your valuables, gold, and items are safely stored in our vault.");
                    DebugSystem.Write($"[QuestNpc] Handled Storage/Keeper interaction for '{Name}' (ClickID: {this.CickID}) with {src.CharName}");
                    return;
                }

                // 1.8 Handle Monster / Wild Creature clicks (Trigger PvE Battle!)
                // Only trigger battle for genuine wild monsters (TemplateID 17000-17999, slimes, wolves, etc.)
                bool isMonster = Game.Battle.MonsterDropManager.MonsterLootTables.ContainsKey(this.TemplateID) ||
                    (this.TemplateID >= 17000 && this.TemplateID <= 17999) ||
                    lowerName.Contains("jelly") || lowerName.Contains("delicate") || lowerName.Contains("slime") ||
                    lowerName.Contains("wolf") || lowerName.Contains("snake") || lowerName.Contains("beetle") ||
                    lowerName.Contains("spider") || lowerName.Contains("boar") || lowerName.Contains("crab") || 
                    lowerName.Contains("gargoyle") || lowerName.Contains("bat");

                // Ensure friendly NPCs, companions, dogs/cats, statues, and items are NEVER flagged as monsters
                if (lowerName.Contains("villager") || lowerName.Contains("guard") || lowerName.Contains("shiba") || 
                    lowerName.Contains("dog") || lowerName.Contains("cat") || lowerName.Contains("mary") || 
                    lowerName.Contains("jack") || lowerName.Contains("lina") || lowerName.Contains("roca") || 
                    lowerName.Contains("noa") || lowerName.Contains("statue") || lowerName.Contains("sword") || 
                    lowerName.Contains("comb") || lowerName.Contains("doll") || lowerName.Contains("emilie") || 
                    lowerName.Contains("guidepost") || lowerName.Contains("boll") || lowerName.Contains("lou") ||
                    this.TemplateID == 11003 || this.TemplateID == 11000 || this.TemplateID == 19020 || this.TemplateID == 19026 ||
                    this.TemplateID == 19048 || this.TemplateID == 19074 || this.TemplateID == 14005 || this.TemplateID == 14013 ||
                    this.TemplateID == 14030 || this.TemplateID == 14049 || this.TemplateID == 14052 || this.TemplateID == 14063 ||
                    this.TemplateID == 14118 || this.TemplateID == 14140 || this.TemplateID == 14141 || this.TemplateID == 14144 ||
                    this.TemplateID == 14153 || this.TemplateID == 14161 || this.TemplateID == 14162 || this.TemplateID == 25020)
                {
                    isMonster = false;
                }

                if (isMonster)
                {
                    src.Send(Tools.FromFormat("bb", 20, 8));
                    Battle.PvEBattleManager.StartPvEBattle(src, (ushort)this.CickID, this.Name, Math.Max(1, (int)this.Level), Math.Max(50, (int)this.HP), this.TemplateID);
                    DebugSystem.Write($"[QuestNpc] Started PvE battle for monster '{Name}' (Lv.{Level} HP.{HP} TID.{TemplateID}) with {src.CharName}");
                    return;
                }

                // 2. Check if NPC matches any registered Quest
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
                else if (lowerName.Contains("roca") || TemplateID == 14161 || TemplateID == 14162)
                {
                    dialogueText = "Greetings! I am Roca, daughter of the Chief. Are you ready for an exciting adventure across the islands?";
                }
                else if (lowerName.Contains("lina") || TemplateID == 14049)
                {
                    dialogueText = "Hello there! Isn't this village wonderful? The breeze from the sea feels so refreshing.";
                }
                else if (lowerName.Contains("guard") || TemplateID == 14118)
                {
                    dialogueText = "Halt! Keep peace in the village. If you travel into the wilderness, be well prepared for monsters.";
                }
                else if (lowerName.Contains("mary") || TemplateID == 14013)
                {
                    dialogueText = "Welcome to our town! If you need anything, don't hesitate to ask around.";
                }
                else if (lowerName.Contains("jack") || TemplateID == 14005)
                {
                    dialogueText = "Hey! Have you seen any strange creatures outside? Stay safe out there!";
                }
                else if (lowerName.Contains("emilie") || TemplateID == 14140)
                {
                    dialogueText = "The flowers in the village are blooming beautifully today!";
                }
                else if (lowerName.Contains("guidepost") || TemplateID == 19020)
                {
                    dialogueText = "[Signpost]: East -> Harbor & Beach | West -> Village Square | North -> Chieftain's Manor";
                }
                else if (lowerName.Contains("cat") || TemplateID == 11000)
                {
                    dialogueText = "Meow~ (The fluffy Persian cat purrs contentedly as you pet it.)";
                }
                else if (lowerName.Contains("dog") || lowerName.Contains("shiba") || TemplateID == 11003)
                {
                    dialogueText = "Woof! (The loyal Shiba inu wags its tail happily.)";
                }
                else if (lowerName.Contains("villager"))
                {
                    dialogueText = "Good day, traveler! Enjoy your stay in our peaceful village.";
                }
                else
                {
                    dialogueText = "Hello, traveller! Beautiful day, isn't it? Let me know if you need anything.";
                }

                src.QueueData.Clear();

                // 2. Captain Prologue Dialogue Sequence (100% Byte-for-Byte from official PCAP Frame 0310-0328)
                if (lowerName.Contains("captain") || this.CickID == 10 || this.CickID == 4 || this.CickID == 11)
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
                else if (lowerName.Contains("robinson") || this.CickID == 1)
                {
                    // Authentic Robinson Island Dialogue (PCAP Frames 0849-0869)
                    src.QueueData.Clear();

                    // Step 2
                    SendPacket step2 = new SendPacket();
                    step2.PackArray(new byte[] { 20, 1, 0, 0, 0, 2, 1, 3, (byte)this.CickID, 0, 1, 0, 0, 0, 0, 0x5E, 0x4F, 0x05 });
                    src.QueueData.Enqueue(step2);

                    // Step 3
                    SendPacket step3 = new SendPacket();
                    step3.PackArray(new byte[] { 20, 1, 0, 0, 0, 3, 1, 3, (byte)this.CickID, 0, 1, 0, 0, 0, 0, 0x5F, 0x4F, 0x05 });
                    src.QueueData.Enqueue(step3);

                    // Step 4
                    SendPacket step4 = new SendPacket();
                    step4.PackArray(new byte[] { 20, 1, 0, 0, 0, 4, 1, 3, (byte)this.CickID, 0, 1, 0, 0, 0, 0, 0x60, 0x4F, 0x05 });
                    src.QueueData.Enqueue(step4);

                    // Step 5
                    SendPacket step5 = new SendPacket();
                    step5.PackArray(new byte[] { 20, 1, 0, 0, 0, 5, 1, 3, (byte)this.CickID, 0, 1, 0, 0, 0, 0, 0x61, 0x4F, 0x05 });
                    src.QueueData.Enqueue(step5);

                    src.OnInteractionComplete = () =>
                    {
                        // Release lock & enable movement (PCAP Frame 0869)
                        src.Send(Tools.FromFormat("bb", 20, 8));
                        src.Send(Tools.FromFormat("bb", 5, 4));
                    };

                    // Step 1: Send movement lock + Step 1 dialogue
                    src.Send(Tools.FromFormat("bbb", 6, 2, 1));
                    SendPacket rob1 = new SendPacket();
                    rob1.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 1, 3, (byte)this.CickID, 0, 1, 0, 0, 0, 0, 0x5D, 0x4F, 0x05 });
                    src.Send(rob1);
                    DebugSystem.Write($"[QuestNpc] Sent authentic Robinson dialogue sequence for '{Name}'.");
                    return;
                }
                else if (lowerName.Contains("monkey"))
                {
                    SendPacket step1 = BuildDialogueStep((byte)this.CickID, 0x050E5D, step: 1, portraitType: 3); // "Oh? It's a Monkey!"
                    SendPacket step2 = Tools.FromFormat("bbb", 22, 10, (byte)1);
                    src.QueueData.Enqueue(step2);

                    src.OnInteractionComplete = () =>
                    {
                        QuestRelated.QuestManager.SendCompanionReward(src, 10727, "Monkey");
                    };

                    src.Send(step1);
                    DebugSystem.Write($"[QuestNpc] Sent Monkey dialogue sequence for '{Name}'.");
                    return;
                }
                else
                {
                    // Generic NPC: release movement lock and send clean chat message
                    src.Send(Tools.FromFormat("bb", 20, 8));
                    src.Send(Tools.FromFormat("bb", 5, 4));
                    src.SendSystemMessage($"💬 {Name}: {dialogueText}");
                    DebugSystem.Write($"[QuestNpc] Handled interaction for '{Name}' (ClickID: {this.CickID}): '{dialogueText}'");
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
