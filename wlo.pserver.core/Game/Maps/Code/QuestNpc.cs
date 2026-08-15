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
                // 2. Random walking ONLY if explicitly flagged in dat file (WalkBehavior == 4)
                else if (WalkBehavior == 4 && !IsStaticNpc())
                {
                    int dx = NextRandom(-120, 121);
                    int dy = NextRandom(-120, 121);
                    int targetX = (int)this.X + dx;
                    int targetY = (int)this.Y + dy;

                    // If drifted too far from spawn origin, steer back towards spawn
                    if (Math.Abs(targetX - this.SpawnX) > 220 || Math.Abs(targetY - this.SpawnY) > 220)
                    {
                        targetX = this.SpawnX + NextRandom(-40, 41);
                        targetY = this.SpawnY + NextRandom(-40, 41);
                    }

                    ushort finalX = (ushort)Math.Max(50, Math.Min(3000, targetX));
                    ushort finalY = (ushort)Math.Max(50, Math.Min(3000, targetY));

                    SendPacket pkt = new SendPacket();
                    pkt.PackArray(new byte[] { 22, 2 });
                    pkt.Pack16(this.CickID);
                    pkt.Pack16(finalX);
                    pkt.Pack16(finalY);
                    pkt.Pack8(3); // speed

                    map.Broadcast(pkt);

                    this.X = finalX;
                    this.Y = finalY;

                    double waitSec = NextRandomDouble(4.0, 9.0);
                    NextWalkTime = now.AddSeconds(waitSec);
                }
                else
                {
                    // Static NPC (WalkBehavior == 0 or other) - do not move
                    NextWalkTime = now.AddSeconds(60);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestNpc] Error in Update for ClickID {this.CickID}: {ex.Message}");
                NextWalkTime = now.AddSeconds(10);
            }
        }

        private bool IsStaticNpc()
        {
            string lower = (Name ?? "").ToLower();
            if (lower.Contains("portal") || lower.Contains("bank") || lower.Contains("atm") || lower.Contains("guide") || lower.Contains("captain") || this.CickID == 10)
                return true;
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

                string dialogueText = "Hello, traveller! Beautiful day, isn't it? Let me know if you need anything.";
                string lowerName = (Name ?? "").ToLower();

                if (lowerName.Contains("cat") || TemplateID == 11000)
                {
                    dialogueText = "Meow~ (The fluffy Persian cat purrs contentedly as you pet it.)";
                }
                else if (lowerName.Contains("dog") || lowerName.Contains("shiba"))
                {
                    dialogueText = "Woof! (The dog wags its tail happily.)";
                }
                else if (this.CickID == 10 || lowerName.Contains("captain"))
                {
                    dialogueText = "Welcome aboard! Speak to the crew members if you need any assistance on the ship.";
                }
                else if (lowerName.Contains("bank") || lowerName.Contains("atm"))
                {
                    dialogueText = $"Welcome to WLO Bank!\nYour Inventory Gold: {src.Gold} gold.";
                }
                else if (lowerName.Contains("grandma"))
                {
                    dialogueText = "Ah... Çok hastayım. Eğer Bick'in evinden bana Kara İlaç (Black Medicine) getirebilirsen çok sevinirim...";
                }
                else if (lowerName.Contains("mary"))
                {
                    dialogueText = "Merhaba! En sevdiğim Saç Bandımı (Headband) ormanda kaybettim. Onu bulup bana getirebilir misin?";
                }
                else if (lowerName.Contains("niss"))
                {
                    dialogueText = "İmdat! Bu kafese canavarlar tarafından kilitlendim... Lütfen beni kurtar!";
                }

                // Send AC 52 Sub 1 Dialogue packet
                src.Send(Tools.FromFormat("bbws", 52, 1, (ushort)1, dialogueText));

                // Send response to release client movement lock
                src.Send(Tools.FromFormat("bb", 20, 8));
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestNpc] Error in Interact: {ex.Message}");
                src.Send(Tools.FromFormat("bb", 20, 8));
            }
        }
        public override void Interact(Player src, byte? answer = null)
        {
            DebugSystem.Write($"[QuestNpc] Interact called. NPC Info: Name='{Name}', ID={CickID}, Level={Level}, HP={HP}, Element={Element}");
            // Send response to release client movement lock (e.g. Close Dialog / End Interaction)
            // Packet 20, 8 seems to be a generic "End" or "Cancel" from AC20.Recv8/Recv6 source code.
            src.Send(Tools.FromFormat("bb", 20, 8));
        }

        public override void Interact(Player src, byte? answer = null, params Code.ShoppingCart[] items)
        {
            DebugSystem.Write($"[QuestNpc] Interact called (Items={items?.Length}). NPC Info: Name='{Name}', ID={CickID}, Level={Level}, HP={HP}, Element={Element}");
            src.Send(Tools.FromFormat("bb", 20, 8));
        }
    }
}
