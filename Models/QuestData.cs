using StardewValley.Quests;

namespace NPCQuestTracker.Models
{
    public class QuestData
    {
        public string QuestId { get; set; }
        public string QuestTitle { get; set; }
        public string QuestType { get; set; }
        public bool IsComplete { get; set; }
        public string TargetNpc { get; set; }

        public static QuestData FromQuest(Quest quest, string targetNpc)
        {
            return new QuestData
            {
                QuestId = quest.id.Value,
                QuestTitle = quest.questTitle,
                QuestType = quest.GetType().Name,
                IsComplete = quest.completed.Value,
                TargetNpc = targetNpc
            };
        }

        public string GetDisplayText()
        {
            string status = IsComplete ? "(Complete!)" : "(In Progress)";
            return $"{QuestTitle} {status}";
        }
    }
}
