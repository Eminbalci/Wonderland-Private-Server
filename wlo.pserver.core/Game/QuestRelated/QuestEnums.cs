using System;

namespace Game.QuestRelated
{
    public enum QuestState : byte
    {
        NotStarted = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3
    }

    public enum QuestType : byte
    {
        Dialogue = 0,
        ItemCollection = 1,
        MonsterBattle = 2,
        Delivery = 3
    }
}
