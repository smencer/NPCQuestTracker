using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;
using NPCQuestTracker.Models;
using NPCQuestTracker.Enums;

namespace NPCQuestTracker.Services
{
    public class QuestDetectionService
    {
        private readonly IMonitor monitor;
        private Dictionary<string, QuestState> cachedQuestStates = new Dictionary<string, QuestState>();
        private long lastQuestLogHash = 0;

        public QuestDetectionService(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        public void UpdateQuestStates(Dictionary<string, NpcMarker> npcMarkers, IList<Quest> questLog)
        {
            if (questLog == null)
                return;

            // Check if quest log changed
            long currentHash = GetQuestLogHash(questLog);
            if (currentHash == lastQuestLogHash && cachedQuestStates.Count > 0)
            {
                // Use cached states - quest log hasn't changed
                ApplyCachedStates(npcMarkers);
                return;
            }

            lastQuestLogHash = currentHash;
            cachedQuestStates.Clear();

            // Clear all quest data
            foreach (var marker in npcMarkers.Values)
            {
                marker.ActiveQuests.Clear();
                marker.QuestState = QuestState.NoQuest;
            }

            // Process each quest once
            foreach (Quest quest in questLog)
            {
                ProcessQuest(quest, npcMarkers);
            }

            // Cache the results
            foreach (var marker in npcMarkers.Values)
            {
                if (marker.ActiveQuests.Count > 0)
                {
                    cachedQuestStates[marker.Name] = marker.QuestState;
                }
            }

            monitor.Log($"Quest detection updated: {cachedQuestStates.Count} NPCs with quests", LogLevel.Trace);
        }

        private void ProcessQuest(Quest quest, Dictionary<string, NpcMarker> npcMarkers)
        {
            List<string> targetNpcs = GetQuestTargets(quest);

            foreach (string npcName in targetNpcs)
            {
                if (npcMarkers.TryGetValue(npcName, out var marker))
                {
                    var questData = QuestData.FromQuest(quest, npcName);
                    marker.ActiveQuests.Add(questData);

                    // Update quest state - ReadyToTurnIn takes priority
                    var oldState = marker.QuestState;
                    if (quest.completed.Value)
                    {
                        marker.QuestState = QuestState.ReadyToTurnIn;
                    }
                    else if (marker.QuestState != QuestState.ReadyToTurnIn)
                    {
                        marker.QuestState = QuestState.ActiveQuest;
                    }

                    // Update layer if quest state changed (Phase 7)
                    if (oldState != marker.QuestState)
                    {
                        marker.UpdatePosition(); // This will recalculate layer
                    }
                }
            }
        }

        private List<string> GetQuestTargets(Quest quest)
        {
            var targets = new List<string>();

            switch (quest)
            {
                case ItemDeliveryQuest deliveryQuest:
                    if (!string.IsNullOrEmpty(deliveryQuest.target.Value))
                        targets.Add(deliveryQuest.target.Value);
                    break;

                case LostItemQuest lostItemQuest:
                    if (!string.IsNullOrEmpty(lostItemQuest.npcName?.Value))
                        targets.Add(lostItemQuest.npcName.Value);
                    break;

                case SocializeQuest socializeQuest:
                    if (socializeQuest.whoToGreet != null)
                    {
                        foreach (var npcName in socializeQuest.whoToGreet)
                        {
                            if (!string.IsNullOrEmpty(npcName))
                                targets.Add(npcName);
                        }
                    }
                    break;

                case FishingQuest fishingQuest:
                    // Fishing quests typically have a target NPC to turn in to
                    if (!string.IsNullOrEmpty(fishingQuest.target.Value))
                        targets.Add(fishingQuest.target.Value);
                    // Also check description for NPC mentions
                    targets.AddRange(FindNpcMentionsInText(fishingQuest.questDescription));
                    break;

                case SlayMonsterQuest slayQuest:
                    // Slay quests have a target NPC to report to
                    if (!string.IsNullOrEmpty(slayQuest.target.Value))
                        targets.Add(slayQuest.target.Value);
                    // Also check description
                    targets.AddRange(FindNpcMentionsInText(slayQuest.questDescription));
                    break;

                case ResourceCollectionQuest resourceQuest:
                    // Resource collection quests have a target NPC
                    if (!string.IsNullOrEmpty(resourceQuest.target.Value))
                        targets.Add(resourceQuest.target.Value);
                    // Also check description
                    targets.AddRange(FindNpcMentionsInText(resourceQuest.questDescription));
                    break;

                default:
                    // Generic quest - check title and description for NPC mentions
                    targets.AddRange(FindNpcMentionsInText(quest.questTitle));
                    targets.AddRange(FindNpcMentionsInText(quest.questDescription));
                    break;
            }

            // Remove duplicates
            return targets.Distinct().ToList();
        }

        private List<string> FindNpcMentionsInText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var mentions = new List<string>();
            string lowerText = text.ToLower();

            // Get all villagers to check against
            var allNpcs = GetAllKnownNpcNames();

            foreach (string npcName in allNpcs)
            {
                if (lowerText.Contains(npcName.ToLower()))
                {
                    mentions.Add(npcName);
                }
            }

            return mentions;
        }

        private HashSet<string> GetAllKnownNpcNames()
        {
            var names = new HashSet<string>();

            // Add all NPCs from all locations
            foreach (GameLocation location in Game1.locations)
            {
                foreach (NPC npc in location.characters)
                {
                    if (!string.IsNullOrEmpty(npc.Name))
                        names.Add(npc.Name);
                }
            }

            // Add NPCs from farm buildings
            var farm = Game1.getFarm();
            if (farm != null)
            {
                foreach (var building in farm.buildings)
                {
                    if (building.indoors.Value != null)
                    {
                        foreach (NPC npc in building.indoors.Value.characters)
                        {
                            if (!string.IsNullOrEmpty(npc.Name))
                                names.Add(npc.Name);
                        }
                    }
                }
            }

            return names;
        }

        private long GetQuestLogHash(IList<Quest> questLog)
        {
            // Create hash based on quest count, IDs, and completion status
            long hash = questLog.Count;
            foreach (var quest in questLog)
            {
                hash = hash * 31 + quest.id.Value.GetHashCode();
                hash = hash * 31 + (quest.completed.Value ? 1 : 0);
            }
            return hash;
        }

        private void ApplyCachedStates(Dictionary<string, NpcMarker> npcMarkers)
        {
            // Clear all quest states first
            foreach (var marker in npcMarkers.Values)
            {
                marker.QuestState = QuestState.NoQuest;
            }

            // Apply cached states
            foreach (var kvp in cachedQuestStates)
            {
                if (npcMarkers.TryGetValue(kvp.Key, out var marker))
                {
                    marker.QuestState = kvp.Value;
                }
            }
        }
    }
}
