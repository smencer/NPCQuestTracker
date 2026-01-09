using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.WorldMaps;
using NPCQuestTracker.Models;

namespace NPCQuestTracker.Services
{
    public class PositionService
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private bool useReflection = true;
        private IReflectedMethod getMapPositionMethod;
        private readonly Dictionary<string, MapCoordinates> locationMappings;
        private bool reflectionInitialized = false;

        public PositionService(IModHelper helper, IMonitor monitor)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.locationMappings = new Dictionary<string, MapCoordinates>();
            InitializeFallbackMappings();
        }

        /// <summary>
        /// Get map position for an NPC using WorldMapManager API with fallbacks
        /// </summary>
        public Vector2? GetMapPosition(GameLocation location, int tileX, int tileY, object mapPage)
        {
            if (location == null)
                return null;

            // Try the official WorldMapManager API first (Stardew 1.6+)
            try
            {
                Point tile = new Point(tileX, tileY);
                MapAreaPositionWithContext? mapAreaPos = WorldMapManager.GetPositionData(location, tile);

                if (mapAreaPos != null)
                {
                    // GetMapPixelPosition returns the actual pixel coordinates on the map
                    Vector2 pixel = mapAreaPos.Value.GetMapPixelPosition();
                    return pixel;
                }
            }
            catch (Exception ex)
            {
                monitor.Log($"WorldMapManager API failed: {ex.Message}", LogLevel.Debug);
            }

            // Try reflection as fallback
            if (useReflection)
            {
                var reflectionResult = TryGetMapPositionViaReflection(location, tileX, tileY, mapPage);
                if (reflectionResult.HasValue)
                    return reflectionResult;
            }

            // Final fallback to manual mapping
            return GetFallbackPosition(location, tileX, tileY);
        }

        private Vector2? TryGetMapPositionViaReflection(GameLocation location, int tileX, int tileY, object mapPage)
        {
            // Initialize reflection on first use
            if (!reflectionInitialized && mapPage != null)
            {
                try
                {
                    getMapPositionMethod = helper.Reflection.GetMethod(mapPage, "getMapPositionForLocation");
                    reflectionInitialized = true;
                    monitor.Log("Successfully initialized reflection for map positioning", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    monitor.Log($"Failed to initialize reflection: {ex.Message}. Switching to fallback mode.", LogLevel.Warn);
                    useReflection = false;
                    return null;
                }
            }

            // Try to invoke reflection method
            if (getMapPositionMethod != null)
            {
                try
                {
                    string locationName = location.NameOrUniqueName ?? location.Name;
                    Vector2 result = getMapPositionMethod.Invoke<Vector2>(locationName, tileX, tileY);
                    return result;
                }
                catch (Exception ex)
                {
                    monitor.Log($"Reflection call failed for {location.Name}: {ex.Message}. Using fallback.", LogLevel.Trace);
                    // Don't disable reflection entirely, just use fallback for this call
                }
            }

            return null;
        }

        private Vector2? GetFallbackPosition(GameLocation location, int tileX, int tileY)
        {
            string locationName = location.NameOrUniqueName ?? location.Name;

            // Handle special cases first

            // Building interiors - use parent location position
            if (location.GetParentLocation() != null)
            {
                return GetParentLocationPosition(location);
            }

            // Check if we have a mapping for this location
            if (locationMappings.TryGetValue(locationName, out var coords))
            {
                // Calculate pixel position on map
                float mapX = coords.MapX + (tileX * coords.PixelsPerTileX);
                float mapY = coords.MapY + (tileY * coords.PixelsPerTileY);
                return new Vector2(mapX, mapY);
            }

            // Try common location name patterns
            if (locationName.Contains("Town"))
                return GetLocationPosition("Town", tileX, tileY);
            if (locationName.Contains("Farm"))
                return GetLocationPosition("Farm", tileX, tileY);
            if (locationName.Contains("Beach"))
                return GetLocationPosition("Beach", tileX, tileY);

            // Most houses are in Town
            if (locationName.EndsWith("House") || locationName.Contains("House"))
                return GetLocationPosition("Town", 0, 0);

            // Specific locations in Town
            if (locationName.Contains("JojaMart") || locationName.Contains("CommunityCenter"))
                return GetLocationPosition("Town", 0, 0);
            if (locationName.Contains("Sewer") || locationName.Contains("Trailer"))
                return GetLocationPosition("Town", 0, 0);
            if (locationName.Contains("Saloon") || locationName.Contains("Blacksmith"))
                return GetLocationPosition("Town", 0, 0);
            if (locationName.Contains("Hospital") || locationName.Contains("Clinic"))
                return GetLocationPosition("Town", 0, 0);
            if (locationName.Contains("AnimalShop") || locationName.Contains("FishShop"))
                return GetLocationPosition("Town", 0, 0);

            // Specific locations in Mountain
            if (locationName.Contains("Mine") || locationName.Contains("SkullCave"))
                return GetLocationPosition("Mountain", 0, 0);
            if (locationName.Contains("ScienceHouse") || locationName.Contains("Tent"))
                return GetLocationPosition("Mountain", 0, 0);
            if (locationName.Contains("AdventureGuild"))
                return GetLocationPosition("Mountain", 0, 0);

            // NPC rooms and specific interiors
            if (locationName.Contains("SeedShop") || locationName.Contains("PierreRoom"))
                return GetLocationPosition("Town", 0, 0);
            if (locationName.Contains("Room") && !locationName.Contains("Mushroom")) // Most NPC rooms (SebastianRoom, HarveyRoom, etc.)
                return GetLocationPosition("Town", 0, 0);
            if (locationName.Contains("Trailer") || locationName.Contains("ManorHouse"))
                return GetLocationPosition("Town", 0, 0);

            // Only log at Trace level so it doesn't spam
            monitor.Log($"No fallback mapping for location: {locationName}", LogLevel.Trace);
            return null;
        }

        private Vector2? GetParentLocationPosition(GameLocation location)
        {
            // Try to get parent location
            GameLocation parent = location.GetParentLocation();
            if (parent != null)
            {
                // Return parent location's base position
                return GetFallbackPosition(parent, 0, 0);
            }

            // If no parent, try to determine location type
            string locationName = location.NameOrUniqueName ?? location.Name;

            // Common building patterns
            if (locationName.Contains("Coop") || locationName.Contains("Barn") ||
                locationName.Contains("Shed") || locationName.Contains("Cabin"))
            {
                return GetLocationPosition("Farm", 0, 0);
            }

            return null;
        }

        private Vector2? GetLocationPosition(string locationName, int tileX, int tileY)
        {
            if (locationMappings.TryGetValue(locationName, out var coords))
            {
                float mapX = coords.MapX + (tileX * coords.PixelsPerTileX);
                float mapY = coords.MapY + (tileY * coords.PixelsPerTileY);
                return new Vector2(mapX, mapY);
            }
            return null;
        }

        private void InitializeFallbackMappings()
        {
            // Coordinates scaled for the 880×680 displayed map
            // Base 300×180 texture is scaled up ~3x (880/300 = 2.93x, 680/180 = 3.78x)
            // Reflection returns coordinates in displayed scale, so we match that

            const float SCALE = 3f;
            const float TILE_SCALE = 1.5f; // Pixels per tile on the displayed map

            // Town (center of Pelican Town)
            locationMappings["Town"] = new MapCoordinates
            {
                MapX = 160 * SCALE,
                MapY = 90 * SCALE,
                PixelsPerTileX = TILE_SCALE,
                PixelsPerTileY = TILE_SCALE
            };

            // Farm (varies by farm type, using standard farm)
            locationMappings["Farm"] = new MapCoordinates
            {
                MapX = 100 * SCALE,
                MapY = 45 * SCALE,
                PixelsPerTileX = TILE_SCALE * 0.8f,
                PixelsPerTileY = TILE_SCALE * 0.8f
            };

            // Beach
            locationMappings["Beach"] = new MapCoordinates
            {
                MapX = 200 * SCALE,
                MapY = 162 * SCALE,
                PixelsPerTileX = TILE_SCALE,
                PixelsPerTileY = TILE_SCALE
            };

            // Mountain
            locationMappings["Mountain"] = new MapCoordinates
            {
                MapX = 187 * SCALE,
                MapY = 45 * SCALE,
                PixelsPerTileX = TILE_SCALE,
                PixelsPerTileY = TILE_SCALE
            };

            // Railroad
            locationMappings["Railroad"] = new MapCoordinates
            {
                MapX = 237 * SCALE,
                MapY = 25 * SCALE,
                PixelsPerTileX = TILE_SCALE,
                PixelsPerTileY = TILE_SCALE
            };

            // Forest (Cindersap Forest)
            locationMappings["Forest"] = new MapCoordinates
            {
                MapX = 75 * SCALE,
                MapY = 112 * SCALE,
                PixelsPerTileX = TILE_SCALE,
                PixelsPerTileY = TILE_SCALE
            };

            // BusStop
            locationMappings["BusStop"] = new MapCoordinates
            {
                MapX = 62 * SCALE,
                MapY = 87 * SCALE,
                PixelsPerTileX = TILE_SCALE,
                PixelsPerTileY = TILE_SCALE
            };

            // Desert (Calico Desert)
            locationMappings["Desert"] = new MapCoordinates
            {
                MapX = 25 * SCALE,
                MapY = 87 * SCALE,
                PixelsPerTileX = TILE_SCALE,
                PixelsPerTileY = TILE_SCALE
            };

            // Woods (Secret Woods)
            locationMappings["Woods"] = new MapCoordinates
            {
                MapX = 50 * SCALE,
                MapY = 125 * SCALE,
                PixelsPerTileX = TILE_SCALE,
                PixelsPerTileY = TILE_SCALE
            };

            // Backwoods
            locationMappings["Backwoods"] = new MapCoordinates
            {
                MapX = 87 * SCALE,
                MapY = 20 * SCALE,
                PixelsPerTileX = TILE_SCALE,
                PixelsPerTileY = TILE_SCALE
            };

            monitor.Log($"Initialized {locationMappings.Count} fallback location mappings", LogLevel.Debug);
        }

        /// <summary>
        /// Add or update a custom location mapping
        /// </summary>
        public void AddLocationMapping(string locationName, float mapX, float mapY, float pixelsPerTileX = 0.5f, float pixelsPerTileY = 0.5f)
        {
            locationMappings[locationName] = new MapCoordinates
            {
                MapX = mapX,
                MapY = mapY,
                PixelsPerTileX = pixelsPerTileX,
                PixelsPerTileY = pixelsPerTileY
            };
            monitor.Log($"Added/updated mapping for {locationName}", LogLevel.Debug);
        }

        /// <summary>
        /// Check if reflection is currently working
        /// </summary>
        public bool IsReflectionActive()
        {
            return useReflection && reflectionInitialized;
        }

        /// <summary>
        /// Force switch to fallback mode
        /// </summary>
        public void DisableReflection()
        {
            useReflection = false;
            monitor.Log("Reflection manually disabled, using fallback positioning", LogLevel.Info);
        }
    }
}
