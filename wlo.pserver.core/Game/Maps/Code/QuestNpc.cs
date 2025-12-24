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

                // Captain NPC - Start Quest 1
                if (this.CickID == 10)
                {
                    DebugSystem.Write($"[QuestNpc] Captain clicked! Starting Quest 1");

                    // Send quest dialog packet (AC 52 is typically for quest/dialog)
                    // Format: AC, Sub, QuestID, DialogID, etc.
                    // This is a placeholder - adjust based on actual packet structure
                    try
                    {
                        // Simple approach: Send a message packet
                        src.Send(Tools.FromFormat("bbws", 52, 1, (ushort)1, "Quest 1 started! Find the crew members."));
                        DebugSystem.Write($"[QuestNpc] Quest 1 start packet sent");
                    }
                    catch (Exception questEx)
                    {
                        DebugSystem.Write($"[QuestNpc] Failed to send quest packet: {questEx.Message}");
                    }
                }

                // Send response to release client movement lock
                src.Send(Tools.FromFormat("bb", 20, 8));
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestNpc] Error in Interact: {ex.Message}");
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
