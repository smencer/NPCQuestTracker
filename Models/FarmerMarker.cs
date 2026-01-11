using Microsoft.Xna.Framework;
using StardewValley;

namespace NPCQuestTracker.Models
{
    public class FarmerMarker
    {
        public long FarmerId { get; set; }
        public string FarmerName { get; set; }
        public Farmer FarmerInstance { get; set; }
        public Vector2 TilePosition { get; set; }
        public GameLocation Location { get; set; }
        public int Layer { get; set; } = 7; // Farmers on top layer
        public Vector2? CachedMapPosition { get; set; }
        public bool IsLocalPlayer { get; set; }
        public int DrawDelay { get; set; } = 0; // For location transitions

        public FarmerMarker(Farmer farmer)
        {
            FarmerInstance = farmer;
            FarmerId = farmer.UniqueMultiplayerID;
            FarmerName = farmer.Name;
            IsLocalPlayer = farmer.IsLocalPlayer;
        }

        public void UpdatePosition()
        {
            if (FarmerInstance?.currentLocation != null)
            {
                var oldLocation = Location;
                TilePosition = FarmerInstance.Tile;
                Location = FarmerInstance.currentLocation;
                CachedMapPosition = null;

                // Reset draw delay on location change
                if (oldLocation != null && oldLocation != Location)
                    DrawDelay = 15; // 15 ticks delay
            }
        }

        public Color GetMarkerColor()
        {
            return IsLocalPlayer
                ? new Color(0, 255, 0, 255)      // Bright green for local
                : new Color(255, 100, 100, 220); // Red for other farmers
        }
    }
}
