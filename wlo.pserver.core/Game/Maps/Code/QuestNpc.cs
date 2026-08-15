using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
