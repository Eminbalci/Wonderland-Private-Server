using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class HouseAssistantProfile
    {
        public uint OwnerCharId { get; set; }
        public string OwnerName { get; set; }
        public string AssistantName { get; set; }
        public ushort ModelId { get; set; }
        public string WelcomeGreeting { get; set; }
        public bool AutoCookEnabled { get; set; }
        public bool AutoCraftEnabled { get; set; }
        public int ServicedMealsCount { get; set; }
        public DateTime RegisteredAt { get; set; }

        public HouseAssistantProfile(uint ownerId, string ownerName, string maidName = "Amelia", ushort model = 24001)
        {
            OwnerCharId = ownerId;
            OwnerName = ownerName ?? "Master";
            AssistantName = maidName;
            ModelId = model;
            WelcomeGreeting = "Welcome back, Master! May your stay be comforting.";
            AutoCookEnabled = true;
            AutoCraftEnabled = false;
            ServicedMealsCount = 0;
            RegisteredAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Tent Housing Housekeeper, Maid & Butler Automation Assistant Engine (AC 62 / Spec 87).
    /// Provides customizable tent servants, automated meal cooking on low player vitality, and visitor greeting protocols.
    /// </summary>
    public class TentMaidButlerEngine
    {
        private readonly ConcurrentDictionary<uint, HouseAssistantProfile> m_assistants = new ConcurrentDictionary<uint, HouseAssistantProfile>();
        private readonly object m_maidLock = new object();

        public int RegisteredAssistantCount => m_assistants.Count;

        public IReadOnlyCollection<HouseAssistantProfile> GetAssistants() => m_assistants.Values.ToList().AsReadOnly();

        public HouseAssistantProfile GetOrCreateAssistant(Player owner)
        {
            if (owner == null) return null;
            return m_assistants.GetOrAdd(owner.CharID, id => new HouseAssistantProfile(id, owner.CharName));
        }

        public bool ConfigureAssistant(Player owner, string name, ushort modelId, string greeting)
        {
            if (owner == null) return false;

            try
            {
                lock (m_maidLock)
                {
                    var ast = GetOrCreateAssistant(owner);
                    if (!string.IsNullOrWhiteSpace(name)) ast.AssistantName = name.Trim();
                    if (modelId > 0) ast.ModelId = modelId;
                    if (!string.IsNullOrWhiteSpace(greeting)) ast.WelcomeGreeting = greeting.Trim();

                    DebugSystem.Write($"[TentMaidButlerEngine] {owner.CharName} configured Housekeeper '{ast.AssistantName}' (Model #{ast.ModelId})");

                    // S->C AC 62:80 Assistant Sync Frame
                    SendPacket p = new SendPacket();
                    p.Pack8(62);
                    p.Pack8(80);
                    p.Pack16(ast.ModelId);
                    p.PackString(ast.AssistantName);
                    p.PackString(ast.WelcomeGreeting);
                    p.Pack8((byte)(ast.AutoCookEnabled ? 1 : 0));
                    owner.Send(p);

                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[TentMaidButlerEngine] ConfigureAssistant error: {ex.Message}");
                return false;
            }
        }

        public bool TriggerAutoCook(Player owner)
        {
            if (owner == null) return false;

            try
            {
                lock (m_maidLock)
                {
                    var ast = GetOrCreateAssistant(owner);
                    if (!ast.AutoCookEnabled) return false;

                    ast.ServicedMealsCount++;
                    ushort cookedFoodId = 10001; // Restorative Roasted Stew
                    owner.Inv?.AddItem(cookedFoodId, 1);

                    DebugSystem.Write($"[TentMaidButlerEngine] Housekeeper '{ast.AssistantName}' prepared a warm meal for {owner.CharName} (Total Serviced: {ast.ServicedMealsCount})");

                    // S->C AC 62:81 Auto-Cook Meal Prepared
                    SendPacket p = new SendPacket();
                    p.Pack8(62);
                    p.Pack8(81);
                    p.Pack16(cookedFoodId);
                    p.Pack16((ushort)ast.ServicedMealsCount);
                    p.PackString($"{ast.AssistantName}: 'I prepared a warm meal for your journey, Master!'");
                    owner.Send(p);

                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[TentMaidButlerEngine] TriggerAutoCook error: {ex.Message}");
                return false;
            }
        }
    }
}
