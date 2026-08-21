using System;
using System.Collections.Generic;

namespace Game.QuestRelated
{
    public class QuestRequirementItem
    {
        public ushort ItemID { get; set; }
        public int Amount { get; set; }
        public string ItemName { get; set; }

        public QuestRequirementItem(ushort itemId, int amount, string itemName = "")
        {
            ItemID = itemId;
            Amount = amount;
            ItemName = itemName;
        }
    }

    public class QuestReward
    {
        public int Gold { get; set; }
        public uint Exp { get; set; }
        public List<Tuple<ushort, int>> Items { get; set; } = new List<Tuple<ushort, int>>();
        public uint CompanionPetID { get; set; }
        public string CompanionName { get; set; }

        public QuestReward(int gold = 0, uint exp = 0, uint companionId = 0, string companionName = "")
        {
            Gold = gold;
            Exp = exp;
            CompanionPetID = companionId;
            CompanionName = companionName;
        }

        public QuestReward AddItem(ushort itemId, int count = 1)
        {
            Items.Add(new Tuple<ushort, int>(itemId, count));
            return this;
        }
    }

    /// <summary>
    /// Represents an individual stage/step in a multi-stage quest (e.g. Talk to NPC A -> Talk to NPC B -> Return to NPC A).
    /// </summary>
    public class QuestStep
    {
        public int StepIndex { get; set; } = 1;
        public uint TargetNpcTemplateID { get; set; }
        public string TargetNpcPattern { get; set; }
        public QuestType StepType { get; set; } = QuestType.Dialogue;

        public string PromptDialogue { get; set; }
        public string InProgressDialogue { get; set; }
        public string CompleteDialogue { get; set; }

        public List<QuestRequirementItem> RequiredItems { get; set; } = new List<QuestRequirementItem>();
        public List<Tuple<ushort, int>> GrantItemsOnStep { get; set; } = new List<Tuple<ushort, int>>();
        public QuestReward StepReward { get; set; }

        public uint BattleMonsterID { get; set; }
        public string BattleMonsterName { get; set; }

        public QuestStep(int stepIndex, string npcPattern, QuestType stepType = QuestType.Dialogue)
        {
            StepIndex = stepIndex;
            TargetNpcPattern = npcPattern;
            StepType = stepType;
        }
    }

    public class QuestDefinition
    {
        public uint QuestID { get; set; }
        public ushort MapID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public QuestType Type { get; set; }
        public ushort RequiredLevel { get; set; }
        public uint NpcTemplateID { get; set; }
        public string NpcNamePattern { get; set; }

        public string Category { get; set; } = "🏝️ Storyline & Area";
        public string AreaName { get; set; } = "Unknown";
        public uint InProgressMarkID { get; set; }
        public uint CompletedMarkID { get; set; }
        public List<uint> AllLinkedMarkIDs { get; set; } = new List<uint>();

        public List<QuestRequirementItem> RequiredItems { get; set; } = new List<QuestRequirementItem>();
        public QuestReward Reward { get; set; } = new QuestReward();

        public string IntroDialogue { get; set; }
        public string InProgressDialogue { get; set; }
        public string CompleteDialogue { get; set; }
        public string AlreadyCompletedDialogue { get; set; }

        public uint BattleMonsterID { get; set; }
        public string BattleMonsterName { get; set; }

        /// <summary>
        /// Quests that MUST be completed before this quest can be accepted.
        /// </summary>
        public List<uint> PrerequisiteQuestIDs { get; set; } = new List<uint>();

        /// <summary>
        /// NPC ClickIDs on the source map that will disappear (AC 19:2) for this player upon quest completion.
        /// </summary>
        public List<ushort> DespawnNpcClickIDs { get; set; } = new List<ushort>();

        /// <summary>
        /// NPC TemplateIDs that will disappear (AC 19:2) upon quest completion.
        /// </summary>
        public List<uint> DespawnNpcTemplateIDs { get; set; } = new List<uint>();

        /// <summary>
        /// Target MapID where the NPC will relocate/spawn after quest completion.
        /// </summary>
        public uint RelocateToMapID { get; set; }

        /// <summary>
        /// Ordered sequence of multi-NPC steps. If empty, legacy single-NPC properties are used.
        /// </summary>
        public List<QuestStep> Steps { get; set; } = new List<QuestStep>();

        public QuestDefinition(uint id, string title, string npcPattern, QuestType type = QuestType.ItemCollection)
        {
            QuestID = id;
            Title = title;
            NpcNamePattern = npcPattern;
            Type = type;
        }

        public QuestDefinition AddPrerequisiteQuest(uint questId)
        {
            if (!PrerequisiteQuestIDs.Contains(questId))
            {
                PrerequisiteQuestIDs.Add(questId);
            }
            return this;
        }

        public QuestDefinition AddStep(QuestStep step)
        {
            if (step != null)
            {
                step.StepIndex = Steps.Count + 1;
                Steps.Add(step);
            }
            return this;
        }
    }
}
