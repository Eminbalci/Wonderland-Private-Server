using System;

namespace Game.QuestRelated
{
    public class PlayerQuest
    {
        public uint QuestID { get; set; }
        public QuestState State { get; set; }
        public int Step { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public PlayerQuest()
        {
        }

        public PlayerQuest(uint questId, QuestState state = QuestState.InProgress, int step = 1)
        {
            QuestID = questId;
            State = state;
            Step = step;
            StartedAt = DateTime.UtcNow;
            if (state == QuestState.Completed)
            {
                CompletedAt = DateTime.UtcNow;
            }
        }
    }
}
