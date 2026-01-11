using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using NPCQuestTracker.Enums;

namespace NPCQuestTracker.Models
{
    public class NpcMarker
    {
        public string Name { get; set; }
        public NPC NpcInstance { get; set; }
        public Vector2 TilePosition { get; set; }
        public GameLocation Location { get; set; }
        public QuestState QuestState { get; set; }
        public List<QuestData> ActiveQuests { get; set; }
        public int Layer { get; private set; } = 4; // Default NPC layer

        // Sprite rendering (Phase 8: Portrait sprites)
        public Texture2D? Sprite { get; set; }           // The NPC's character sprite texture
        public Rectangle SpriteSourceRect { get; set; }  // Custom crop area for the head/face
        public int CropOffset { get; set; } = 0;         // Vertical offset for cropping

        // Cached for performance
        public Vector2? CachedMapPosition { get; set; }
        public bool IsVisible { get; set; }
        public long LastUpdate { get; set; }
        public bool IsOutdoors { get; set; }

        public NpcMarker(NPC npc)
        {
            NpcInstance = npc;
            Name = npc.Name;
            ActiveQuests = new List<QuestData>();
            QuestState = QuestState.NoQuest;
            LastUpdate = Game1.ticks;
            IsVisible = true;
        }

        public void UpdatePosition()
        {
            if (NpcInstance?.currentLocation != null)
            {
                TilePosition = NpcInstance.Tile;
                Location = NpcInstance.currentLocation;
                LastUpdate = Game1.ticks;
                CachedMapPosition = null; // Invalidate cache

                // Determine if location is outdoors
                IsOutdoors = Location.IsOutdoors;

                // Update layer based on location and quest status
                UpdateLayer();
            }
        }

        private void UpdateLayer()
        {
            // Layer system (0-7):
            // - Outdoor NPCs: 4-7
            // - Indoor NPCs: 0-3
            // - Quest/birthday NPCs: +1 to base layer

            int baseLayer = IsOutdoors ? 6 : 2;

            // Quest NPCs get priority (higher layer)
            if (QuestState != QuestState.NoQuest)
                baseLayer++;

            Layer = baseLayer;
        }

        public Color GetMarkerColor()
        {
            return QuestState switch
            {
                QuestState.ReadyToTurnIn => new Color(50, 255, 50, 220), // Green
                QuestState.ActiveQuest => new Color(255, 215, 0, 220),   // Yellow
                _ => new Color(100, 150, 255, 200)                        // Blue
            };
        }

        public bool HasQuest()
        {
            return QuestState != QuestState.NoQuest;
        }

        public bool IsReadyToTurnIn()
        {
            return QuestState == QuestState.ReadyToTurnIn;
        }

        /// <summary>
        /// Get the source rectangle for cropping the NPC sprite to show just the head/face
        /// </summary>
        public Rectangle GetSpriteSourceRect()
        {
            // If custom rect specified, use it
            if (SpriteSourceRect != Rectangle.Empty)
                return SpriteSourceRect;

            // Default: 16×15 pixel crop at configurable Y offset
            // This extracts the NPC's head from their character sprite
            return new Rectangle(0, CropOffset, 16, 15);
        }
    }
}
