using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Quests;
using NPCQuestTracker.Models;
using NPCQuestTracker.Enums;
using NPCQuestTracker.Services;

namespace NPCQuestTracker
{
    public class ModEntry : Mod
    {
        // Services
        private QuestDetectionService questService;
        private NpcDiscoveryService npcDiscovery;
        private PositionService positionService;

        // Data - using PerScreen for multiplayer safety
        private PerScreen<Dictionary<string, NpcMarker>> npcMarkers;
        private PerScreen<Dictionary<long, FarmerMarker>> farmerMarkers;

        // Legacy data structures (will migrate away from these)
        private Dictionary<string, Vector2> npcPositions = new Dictionary<string, Vector2>();
        private Dictionary<string, QuestInfo> npcQuestInfo = new Dictionary<string, QuestInfo>();

        // Textures
        private Texture2D markerTexture;
        private Texture2D questMarkerTexture;
        private Texture2D completeMarkerTexture;

        // State
        private bool isMapOpen = false;
        private string hoveredNPC = null;
        private Event lastEvent = null;

        // Performance tracking
        private long lastNpcUpdate = 0;
        private long lastQuestUpdate = 0;
        private const int NPC_UPDATE_INTERVAL = 15; // ticks (~250ms)
        private const int QUEST_UPDATE_INTERVAL = 30; // ticks (~500ms) - quests change less frequently

        // List of trackable NPCs
        private readonly string[] trackableNPCs = new string[]
        {
            "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Dwarf", "Elliott",
            "Emily", "Evelyn", "George", "Gus", "Haley", "Harvey", "Jas", "Jodi",
            "Kent", "Krobus", "Leah", "Lewis", "Linus", "Marnie", "Maru", "Pam",
            "Penny", "Pierre", "Robin", "Sam", "Sebastian", "Shane", "Vincent",
            "Willy", "Wizard", "Sandy"
        };

        public override void Entry(IModHelper helper)
        {
            Monitor.Log("NPC Quest Tracker initializing...", LogLevel.Info);

            // Initialize services
            questService = new QuestDetectionService(Monitor);
            npcDiscovery = new NpcDiscoveryService(Monitor);
            positionService = new PositionService(helper, Monitor);

            // Initialize per-screen data for multiplayer safety
            npcMarkers = new PerScreen<Dictionary<string, NpcMarker>>(
                () => new Dictionary<string, NpcMarker>());
            farmerMarkers = new PerScreen<Dictionary<long, FarmerMarker>>(
                () => new Dictionary<long, FarmerMarker>());

            // Create marker textures
            CreateMarkerTextures();
            Monitor.Log("Marker textures created", LogLevel.Info);

            // Hook into events
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            // helper.Events.Display.Rendered += OnRendered;
            helper.Events.Display.RenderedActiveMenu += OnRendered;

            Monitor.Log("NPC Quest Tracker loaded successfully. Events registered.", LogLevel.Info);
        }

        private void CreateMarkerTextures()
        {
            // Create basic marker texture (white circle for tinting)
            markerTexture = new Texture2D(Game1.graphics.GraphicsDevice, 16, 16);
            Color[] colorData = new Color[16 * 16];
            for (int i = 0; i < colorData.Length; i++)
            {
                int x = i % 16;
                int y = i / 16;
                int dx = x - 8;
                int dy = y - 8;
                if (dx * dx + dy * dy <= 49) // Circle radius ~7
                    colorData[i] = Color.White; // White so it can be tinted with any color
                else
                    colorData[i] = Color.Transparent;
            }
            markerTexture.SetData(colorData);

            // Create quest marker texture (yellow circle)
            questMarkerTexture = new Texture2D(Game1.graphics.GraphicsDevice, 16, 16);
            Color[] questColorData = new Color[16 * 16];
            for (int i = 0; i < questColorData.Length; i++)
            {
                int x = i % 16;
                int y = i / 16;
                int dx = x - 8;
                int dy = y - 8;
                if (dx * dx + dy * dy <= 49)
                    questColorData[i] = new Color(255, 215, 0, 220);
                else
                    questColorData[i] = Color.Transparent;
            }
            questMarkerTexture.SetData(questColorData);

            // Create complete quest marker texture (green circle)
            completeMarkerTexture = new Texture2D(Game1.graphics.GraphicsDevice, 16, 16);
            Color[] completeColorData = new Color[16 * 16];
            for (int i = 0; i < completeColorData.Length; i++)
            {
                int x = i % 16;
                int y = i / 16;
                int dx = x - 8;
                int dy = y - 8;
                if (dx * dx + dy * dy <= 49)
                    completeColorData[i] = new Color(50, 255, 50, 220);
                else
                    completeColorData[i] = Color.Transparent;
            }
            completeMarkerTexture.SetData(completeColorData);
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            long currentTick = e.Ticks;

            // Update NPC positions every 15 ticks (~250ms)
            if (currentTick - lastNpcUpdate >= NPC_UPDATE_INTERVAL)
            {
                UpdateNPCPositions();
                lastNpcUpdate = currentTick;
            }

            // Update quest information less frequently (every 30 ticks ~500ms)
            // Quests change infrequently, so caching handles most updates
            if (currentTick - lastQuestUpdate >= QUEST_UPDATE_INTERVAL)
            {
                UpdateQuestInfo();
                UpdateQuestsWithService(); // NEW: Use the optimized service
                UpdateFarmers(); // Phase 5: Track multiplayer farmers
                lastQuestUpdate = currentTick;
            }

            if (this.lastEvent != null && Game1.CurrentEvent != null)
            {
                Monitor.Log($"Event: {Game1.CurrentEvent} ended.");
            }

            this.lastEvent = Game1.CurrentEvent;

            // Check if map is open
            bool wasMapOpen = isMapOpen;

            // Check if map is open (can be direct MapPage or GameMenu with MapPage tab)
            isMapOpen = false;
            if (Game1.activeClickableMenu is StardewValley.Menus.MapPage)
            {
                isMapOpen = true;
            }
            else if (Game1.activeClickableMenu is StardewValley.Menus.GameMenu gameMenu)
            {
                // Check if the active tab is the map
                isMapOpen = gameMenu.GetCurrentPage() is StardewValley.Menus.MapPage;
            }

            // foreach (Farmer farmer in Game1.getOnlineFarmers())
            // {
            //     Monitor.Log($"Farmer: {farmer.Name} - {farmer.UniqueMultiplayerID}", LogLevel.Info);
            // }

            // Log when map state changes
            if (isMapOpen != wasMapOpen)
            {
                Monitor.Log($"Map state changed: {isMapOpen}. Menu type: {Game1.activeClickableMenu?.GetType().FullName ?? "null"}", LogLevel.Info);
            }
        }

        private void UpdateNPCPositions()
        {
            // Clear legacy structures
            npcPositions.Clear();

            // Get current markers dictionary
            var currentMarkers = npcMarkers.Value;

            // Phase 3: Use dynamic NPC discovery instead of hardcoded list
            var discoveredNpcs = npcDiscovery.GetVillagers();

            // Remove NPCs that are no longer discovered
            var toRemove = new List<string>();
            foreach (var npcName in currentMarkers.Keys)
            {
                if (!discoveredNpcs.ContainsKey(npcName))
                {
                    toRemove.Add(npcName);
                }
            }
            foreach (var npcName in toRemove)
            {
                currentMarkers.Remove(npcName);
            }

            // Add or update NPCs
            foreach (var kvp in discoveredNpcs)
            {
                string npcName = kvp.Key;
                NpcMarker discoveredMarker = kvp.Value;

                if (currentMarkers.TryGetValue(npcName, out var existingMarker))
                {
                    // Update existing marker with fresh NPC instance and sprite
                    existingMarker.NpcInstance = discoveredMarker.NpcInstance;
                    existingMarker.Sprite = discoveredMarker.Sprite; // Phase 8: Copy sprite texture
                    existingMarker.UpdatePosition();
                }
                else
                {
                    // Add new marker (also needs position update)
                    currentMarkers[npcName] = discoveredMarker;
                    discoveredMarker.UpdatePosition();
                }

                // Update legacy structure
                var markerToUse = currentMarkers[npcName];
                if (markerToUse.NpcInstance != null)
                {
                    npcPositions[npcName] = markerToUse.TilePosition;
                }
            }
        }

        private void UpdateQuestInfo()
        {
            // Clear legacy structure
            npcQuestInfo.Clear();

            if (Game1.player.questLog == null)
                return;

            // Get current markers
            var currentMarkers = npcMarkers.Value;

            // Phase 3: Iterate over dynamically discovered NPCs instead of hardcoded list
            foreach (string npcName in currentMarkers.Keys)
            {
                var questInfo = new QuestInfo();

                // NEW: Clear quest data in marker
                if (currentMarkers.TryGetValue(npcName, out var marker))
                {
                    marker.ActiveQuests.Clear();
                    marker.QuestState = QuestState.NoQuest;
                }

                foreach (Quest quest in Game1.player.questLog)
                {
                    // Check if quest involves this NPC
                    if (QuestInvolvesNPC(quest, npcName))
                    {
                        questInfo.HasQuest = true;

                        if (quest.completed.Value)
                        {
                            questInfo.ReadyToTurnIn = true;
                            questInfo.Quests.Add(quest.questTitle + " (Complete!)");

                            // NEW: Update marker
                            if (marker != null)
                            {
                                marker.QuestState = QuestState.ReadyToTurnIn;
                                marker.ActiveQuests.Add(QuestData.FromQuest(quest, npcName));
                            }
                        }
                        else
                        {
                            questInfo.Quests.Add(quest.questTitle + " (In Progress)");

                            // NEW: Update marker
                            if (marker != null && marker.QuestState != QuestState.ReadyToTurnIn)
                            {
                                marker.QuestState = QuestState.ActiveQuest;
                                marker.ActiveQuests.Add(QuestData.FromQuest(quest, npcName));
                            }
                        }
                    }
                }

                if (questInfo.HasQuest)
                {
                    npcQuestInfo[npcName] = questInfo;
                }
            }
        }

        private void UpdateQuestsWithService()
        {
            if (Game1.player?.questLog == null)
                return;

            // Use the new optimized quest detection service
            questService.UpdateQuestStates(npcMarkers.Value, Game1.player.questLog);
        }

        private void UpdateFarmers()
        {
            var currentFarmers = farmerMarkers.Value;

            // Get all online farmers
            foreach (Farmer farmer in Game1.getOnlineFarmers())
            {
                if (farmer?.currentLocation == null)
                    continue;

                long farmerId = farmer.UniqueMultiplayerID;

                if (currentFarmers.TryGetValue(farmerId, out var marker))
                {
                    // Update existing farmer marker
                    marker.UpdatePosition();

                    // Decrement draw delay
                    if (marker.DrawDelay > 0)
                        marker.DrawDelay--;
                }
                else
                {
                    // Create new farmer marker
                    currentFarmers[farmerId] = new FarmerMarker(farmer);
                    currentFarmers[farmerId].UpdatePosition();
                }
            }

            // Remove offline farmers
            var onlineIds = Game1.getOnlineFarmers()
                .Select(f => f.UniqueMultiplayerID)
                .ToHashSet();
            var toRemove = currentFarmers.Keys
                .Where(id => !onlineIds.Contains(id))
                .ToList();
            foreach (var id in toRemove)
            {
                currentFarmers.Remove(id);
            }

            Monitor.Log($"Tracking {currentFarmers.Count} farmers", LogLevel.Trace);
        }

        private bool QuestInvolvesNPC(Quest quest, string npcName)
        {
            // Check quest description and title for NPC name
            string lowerNPCName = npcName.ToLower();
            
            if (quest.questTitle != null && quest.questTitle.ToLower().Contains(lowerNPCName))
                return true;
            
            if (quest.questDescription != null && quest.questDescription.ToLower().Contains(lowerNPCName))
                return true;

            // Check specific quest types
            if (quest is SocializeQuest socializeQuest)
            {
                return socializeQuest.whoToGreet != null && 
                       socializeQuest.whoToGreet.Any(name => name.Equals(npcName, StringComparison.OrdinalIgnoreCase));
            }

            if (quest is ItemDeliveryQuest deliveryQuest)
            {
                return deliveryQuest.target.Value != null && 
                       deliveryQuest.target.Value.Equals(npcName, StringComparison.OrdinalIgnoreCase);
            }

            if (quest is LostItemQuest lostItemQuest)
            {
                return lostItemQuest.npcName != null && 
                       string.Equals(lostItemQuest.npcName.Value, npcName, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || isMapOpen)
                return;

            // Draw markers in the world view (only for NPCs with quests)
            foreach (var kvp in npcPositions)
            {
                string npcName = kvp.Key;
                Vector2 tilePos = kvp.Value;

                // Skip NPCs without quests
                if (!npcQuestInfo.ContainsKey(npcName))
                    continue;

                NPC npc = Game1.getCharacterFromName(npcName);
                if (npc == null || npc.currentLocation != Game1.currentLocation)
                    continue;

                // Get screen position
                Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, tilePos * 64f);

                // Choose marker based on quest status
                Texture2D marker = npcQuestInfo[npcName].ReadyToTurnIn ? completeMarkerTexture : questMarkerTexture;

                // Draw marker above NPC
                e.SpriteBatch.Draw(
                    marker,
                    screenPos + new Vector2(16, -32),
                    null,
                    Color.White,
                    0f,
                    new Vector2(8, 8),
                    2f,
                    SpriteEffects.None,
                    1f
                );
            }
        }

        private void OnRendered(object sender, RenderedActiveMenuEventArgs e)
        {
            if (!Context.IsWorldReady || !isMapOpen)
                return;

            // Get the MapPage (either directly or from GameMenu)
            StardewValley.Menus.MapPage mapPage = null;

            if (Game1.activeClickableMenu is StardewValley.Menus.MapPage directMapPage)
            {
                mapPage = directMapPage;
            }
            else if (Game1.activeClickableMenu is StardewValley.Menus.GameMenu gameMenu)
            {
                mapPage = gameMenu.GetCurrentPage() as StardewValley.Menus.MapPage;
            }

            if (mapPage == null)
            {
                Monitor.Log($"Map page is null even though isMapOpen is true. Menu type: {Game1.activeClickableMenu?.GetType().Name ?? "null"}", LogLevel.Warn);
                return;
            }

            // Draw NPC markers on map
            DrawNPCMarkersOnMap(e.SpriteBatch, mapPage);

            // Draw tooltip if hovering over NPC
            if (hoveredNPC != null)
            {
                DrawNpcTooltip(e.SpriteBatch);
            }
        }

        private void DrawNPCMarkersOnMap(SpriteBatch b, StardewValley.Menus.MapPage mapPage)
        {
            hoveredNPC = null;

            // Get current NPC markers
            var currentMarkers = npcMarkers.Value;

            // Get map bounds (the rectangle defining the map area on screen)
            // This is where the actual map texture is rendered, not the menu position
            Rectangle mapBounds = mapPage.mapBounds;
            int mapX = mapBounds.X;
            int mapY = mapBounds.Y;

            // End the current SpriteBatch and start a new one without depth sorting
            // This ensures our markers draw on top of the menu
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);

            // Phase 7: Sort NPCs by layer for proper rendering order
            var sortedNpcs = currentMarkers.OrderBy(kvp => kvp.Value.Layer).ToList();

            // Phase 8: Track positions to detect stacking
            var positionCounts = new Dictionary<string, int>();

            foreach (var kvp in sortedNpcs)
            {
                string npcName = kvp.Key;
                NpcMarker npcMarker = kvp.Value;

                if (npcMarker.NpcInstance == null || npcMarker.Location == null)
                    continue;

                // Phase 4: Use PositionService with reflection + fallback
                Vector2? mapPixelPositionNullable = positionService.GetMapPosition(
                    npcMarker.Location,
                    (int)npcMarker.TilePosition.X,
                    (int)npcMarker.TilePosition.Y,
                    mapPage
                );

                if (!mapPixelPositionNullable.HasValue)
                    continue;

                // WorldMapManager returns coordinates relative to the map texture's origin
                // Add the map bounds position to convert to screen coordinates
                Vector2 mapRelativePosition = mapPixelPositionNullable.Value;
                Vector2 mapPixelPosition = new Vector2(
                    mapX + mapRelativePosition.X,
                    mapY + mapRelativePosition.Y
                );

                // Phase 8: Handle stacked NPCs by spreading them out slightly
                string posKey = $"{(int)mapPixelPosition.X}_{(int)mapPixelPosition.Y}";
                if (!positionCounts.ContainsKey(posKey))
                    positionCounts[posKey] = 0;

                int stackIndex = positionCounts[posKey];
                positionCounts[posKey]++;

                // Spread NPCs in a small circle if multiple are at same location
                if (stackIndex > 0)
                {
                    float angle = (stackIndex * 60f) * (float)Math.PI / 180f; // 60 degree increments
                    float radius = 20f + (stackIndex / 6) * 15f; // Expand radius every 6 NPCs
                    mapPixelPosition.X += (float)Math.Cos(angle) * radius;
                    mapPixelPosition.Y += (float)Math.Sin(angle) * radius;
                }

                // Phase 8: Draw portrait sprite (skip NPCs without sprites)
                if (npcMarker.Sprite == null)
                    continue;

                // Get sprite crop rectangle
                Rectangle spriteRect = npcMarker.GetSpriteSourceRect();

                // Calculate scale to fit within ~32×30 pixels (aspect-ratio aware)
                float scale = spriteRect.Width > spriteRect.Height
                    ? 32f / spriteRect.Width
                    : 30f / spriteRect.Height;

                // Calculate final size
                int iconWidth = (int)(spriteRect.Width * scale);
                int iconHeight = (int)(spriteRect.Height * scale);

                // Create destination rectangle (centered on position)
                Rectangle destRect = new Rectangle(
                    (int)(mapPixelPosition.X - iconWidth / 2),
                    (int)(mapPixelPosition.Y - iconHeight / 2),
                    iconWidth,
                    iconHeight
                );

                // Draw the sprite portrait
                b.Draw(
                    npcMarker.Sprite,
                    destRect,
                    spriteRect,
                    Color.White, // No color tint on portrait
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    1f
                );

                // Draw quest status icon if applicable
                if (npcMarker.HasQuest())
                {
                    DrawQuestIcon(b, mapPixelPosition, npcMarker.QuestState);
                }

                // Check if mouse is hovering (adjust bounds to match sprite size)
                Rectangle markerBounds = new Rectangle(
                    (int)(mapPixelPosition.X - iconWidth / 2),
                    (int)(mapPixelPosition.Y - iconHeight / 2),
                    iconWidth,
                    iconHeight
                );
                if (markerBounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
                {
                    hoveredNPC = npcName;
                }
            }

            // Phase 5: Draw farmer markers
            foreach (var farmerMarker in farmerMarkers.Value.Values)
            {
                // Skip if draw delay is active (location transition)
                if (farmerMarker.DrawDelay > 0)
                    continue;

                // Get farmer position on map
                Vector2? farmerMapPos = positionService.GetMapPosition(
                    farmerMarker.Location,
                    (int)farmerMarker.TilePosition.X,
                    (int)farmerMarker.TilePosition.Y,
                    mapPage
                );

                if (!farmerMapPos.HasValue)
                    continue;

                Vector2 farmerPos = farmerMapPos.Value;

                // Add map screen offset
                farmerPos.X += mapX;
                farmerPos.Y += mapY;

                // Draw farmer marker (use quest marker texture with farmer color)
                Color farmerColor = farmerMarker.GetMarkerColor();
                b.Draw(
                    questMarkerTexture, // Reuse existing texture
                    farmerPos,
                    null,
                    farmerColor,
                    0f,
                    new Vector2(8, 8),
                    3.5f, // Slightly larger than NPCs
                    SpriteEffects.None,
                    1f // Draw last (on top)
                );

                // Draw farmer name
                string farmerName = farmerMarker.IsLocalPlayer ? $"{farmerMarker.FarmerName} (You)" : farmerMarker.FarmerName;
                Vector2 nameSize = Game1.tinyFont.MeasureString(farmerName);
                Vector2 namePos = new Vector2(farmerPos.X - nameSize.X / 2, farmerPos.Y - nameSize.Y - 20);

                // Draw name shadow
                b.DrawString(Game1.tinyFont, farmerName, namePos + new Vector2(1, 1), Color.Black, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);
                // Draw name
                b.DrawString(Game1.tinyFont, farmerName, namePos, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);
            }

            // End our SpriteBatch and restore the original one
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
        }

        private void DrawNpcTooltip(SpriteBatch b)
        {
            // Build tooltip text
            string tooltipText = hoveredNPC;

            // Add quest info if NPC has quests
            if (npcQuestInfo.ContainsKey(hoveredNPC))
            {
                var questInfo = npcQuestInfo[hoveredNPC];

                tooltipText += "\n";
                if (questInfo.ReadyToTurnIn)
                    tooltipText += "✓ Quest Ready!\n";
                else
                    tooltipText += "! Active Quest\n";

                tooltipText += "\nQuests:\n";
                foreach (string quest in questInfo.Quests)
                {
                    tooltipText += $"• {quest}\n";
                }
            }

            // Draw tooltip
            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();

            Vector2 textSize = Game1.smallFont.MeasureString(tooltipText);
            int width = (int)textSize.X + 32;
            int height = (int)textSize.Y + 32;

            // Background
            StardewValley.Menus.IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                mouseX + 20,
                mouseY + 20,
                width,
                height,
                Color.White,
                1f,
                false
            );

            // Text
            b.DrawString(
                Game1.smallFont,
                tooltipText,
                new Vector2(mouseX + 36, mouseY + 36),
                Game1.textColor
            );
        }

        /// <summary>
        /// Draw quest status icon overlay on NPC portrait
        /// </summary>
        private void DrawQuestIcon(SpriteBatch b, Vector2 markerPosition, QuestState questState)
        {
            // Only draw icon for active or ready quests
            if (questState == QuestState.NoQuest)
                return;

            // Quest icon from mouseCursors (exclamation mark)
            // Source: Rectangle(403, 496, 5, 14) from NPCMapLocations reference
            Rectangle questIconRect = new Rectangle(403, 496, 5, 14);

            // Position icon at top-right of marker
            Vector2 iconPos = new Vector2(
                markerPosition.X + 12,  // Offset to right
                markerPosition.Y - 18   // Offset above marker
            );

            // Color based on quest state
            Color iconColor = questState == QuestState.ReadyToTurnIn
                ? Color.LightGreen
                : Color.Yellow;

            b.Draw(
                Game1.mouseCursors,
                iconPos,
                questIconRect,
                iconColor,
                0f,
                Vector2.Zero,
                1.8f, // Scale from reference implementation
                SpriteEffects.None,
                1f
            );
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || !isMapOpen || e.Button != SButton.MouseLeft)
                return;

            if (hoveredNPC != null && npcQuestInfo.ContainsKey(hoveredNPC))
            {
                // Show detailed quest information
                var questInfo = npcQuestInfo[hoveredNPC];
                string message = $"{hoveredNPC} has {questInfo.Quests.Count} active quest(s)";
                if (questInfo.ReadyToTurnIn)
                    message += " - Ready to turn in!";
                
                Game1.addHUDMessage(new HUDMessage(message, 2));
            }
        }
    }

    public class QuestInfo
    {
        public bool HasQuest { get; set; }
        public bool ReadyToTurnIn { get; set; }
        public List<string> Quests { get; set; } = new List<string>();
    }
}
