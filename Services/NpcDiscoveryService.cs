using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using NPCQuestTracker.Models;

namespace NPCQuestTracker.Services
{
    public class NpcDiscoveryService
    {
        private readonly IMonitor monitor;
        private readonly HashSet<string> excludedNpcs;

        public NpcDiscoveryService(IMonitor monitor)
        {
            this.monitor = monitor;
            this.excludedNpcs = new HashSet<string>();
            InitializeExclusions();
        }

        /// <summary>
        /// Dynamically discover all trackable NPCs in the game world
        /// </summary>
        public Dictionary<string, NpcMarker> GetVillagers()
        {
            var markers = new Dictionary<string, NpcMarker>();

            // Scan all loaded locations
            foreach (GameLocation location in Game1.locations)
            {
                AddNpcsFromLocation(location, markers);
            }

            // Scan farm buildings (barns, coops, etc.)
            var farm = Game1.getFarm();
            if (farm != null)
            {
                foreach (var building in farm.buildings)
                {
                    if (building.indoors.Value != null)
                    {
                        AddNpcsFromLocation(building.indoors.Value, markers);
                    }
                }
            }

            monitor.Log($"Discovered {markers.Count} trackable NPCs", LogLevel.Trace);
            return markers;
        }

        private void AddNpcsFromLocation(GameLocation location, Dictionary<string, NpcMarker> markers)
        {
            if (location?.characters == null)
                return;

            foreach (NPC npc in location.characters)
            {
                if (ShouldTrackNpc(npc) && !markers.ContainsKey(npc.Name))
                {
                    var marker = new NpcMarker(npc);

                    // Phase 8: Load sprite texture for portrait rendering
                    try
                    {
                        if (npc.Sprite?.Texture != null)
                        {
                            marker.Sprite = npc.Sprite.Texture;
                            marker.CropOffset = 0; // Default crop offset - can be customized per NPC if needed
                        }
                    }
                    catch (Exception ex)
                    {
                        monitor.Log($"Could not load sprite for {npc.Name}: {ex.Message}", LogLevel.Warn);
                        marker.Sprite = null; // Will fall back to dot rendering
                    }

                    markers[npc.Name] = marker;
                }
            }
        }

        private bool ShouldTrackNpc(NPC npc)
        {
            if (npc == null || string.IsNullOrEmpty(npc.Name))
                return false;

            // Exclude based on name
            if (excludedNpcs.Contains(npc.Name))
                return false;

            // Exclude invisible NPCs
            if (npc.IsInvisible)
                return false;

            // Must be a villager (social NPCs)
            if (!npc.IsVillager)
                return false;

            // Exclude horses (optional - can be made configurable)
            if (npc is Horse)
                return false;

            // Exclude children (optional - can be made configurable)
            if (npc is Child)
                return false;

            return true;
        }

        private void InitializeExclusions()
        {
            // NPCs that should not be tracked even if they appear as villagers
            excludedNpcs.Add("Henchman");
            excludedNpcs.Add("Bouncer");
            excludedNpcs.Add("Mister Qi"); // Often appears but not trackable
            excludedNpcs.Add("Birdie"); // Special event NPC

            // Add any other problematic NPCs here
            // Note: Most event-specific NPCs will be filtered by isVillager() check
        }

        /// <summary>
        /// Add an NPC name to the exclusion list
        /// </summary>
        public void ExcludeNpc(string npcName)
        {
            if (!string.IsNullOrEmpty(npcName))
            {
                excludedNpcs.Add(npcName);
                monitor.Log($"Added {npcName} to exclusion list", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Remove an NPC name from the exclusion list
        /// </summary>
        public void IncludeNpc(string npcName)
        {
            if (!string.IsNullOrEmpty(npcName))
            {
                excludedNpcs.Remove(npcName);
                monitor.Log($"Removed {npcName} from exclusion list", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Get count of currently excluded NPCs
        /// </summary>
        public int GetExclusionCount()
        {
            return excludedNpcs.Count;
        }
    }
}
