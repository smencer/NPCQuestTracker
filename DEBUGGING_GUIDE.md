# Debugging Guide for NPC Quest Tracker

## How to Enable Debug Logging

I've added debug logging throughout the critical code paths. To see these logs:

### Step 1: Enable Verbose Logging in SMAPI

Edit your SMAPI configuration file:
- **Location**: Same folder as `StardewModdingAPI.exe`
- **File**: `StardewModdingAPI.config.json`

Find the `DeveloperMode` and `VerboseLogging` settings and set them to `true`:

```json
{
  "DeveloperMode": true,
  "VerboseLogging": "true"
}
```

### Step 2: Rebuild and Install Your Mod

```bash
dotnet build
```

Copy the files from `bin/Debug/net6.0/` to your Stardew Valley Mods folder.

### Step 3: Run the Game and Check Logs

1. Launch Stardew Valley through `StardewModdingAPI.exe`
2. Start or load a game
3. Press `M` to open the map
4. Check the SMAPI console window for debug messages

## What to Look For in the Logs

### 1. Map Opening Detection
Look for this message when you press M:
```
[NPCQuestTracker] DrawNPCMarkersOnMap called. NPC positions count: X
```

**If you DON'T see this:**
- The map isn't being detected as open
- Check that `isMapOpen` is being set correctly

### 2. NPC Position Tracking
Look for:
```
[NPCQuestTracker] Map offset: (X, Y)
```

**If the count is 0:**
- NPCs aren't being tracked
- Make sure you're in-game (not on the title screen)
- Wait a few seconds after loading the game

### 3. Reflection Errors
Look for warnings like:
```
[NPCQuestTracker] Failed to get map position for [NPC]: [Error message]
```

**If you see these:**
- The reflection method name might have changed in your version of Stardew Valley
- This is the most common issue - see "Fix #1" below

### 4. Drawing Confirmation
Look for:
```
[NPCQuestTracker] Drawing [NPC] marker at {X:XX Y:YY}
```

**If you see this but no markers appear:**
- Markers are being drawn but might be off-screen
- Check the coordinates - they should be within your screen bounds
- See "Fix #2" below

## Common Fixes

### Fix #1: Reflection Method Name Changed

The method `getMapPositionForLocation` might have a different name or signature in your version of Stardew Valley.

**To check available methods:**

Add this temporary code to line 278 in [ModEntry.cs](ModEntry.cs):

```csharp
// Log all available methods
var methods = mapPage.GetType().GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
foreach (var method in methods)
{
    if (method.Name.Contains("map") || method.Name.Contains("position"))
        Monitor.Log($"Available method: {method.Name}", LogLevel.Info);
}
```

Run the game and check which methods are available.

### Fix #2: Use Alternative Positioning Method

If reflection fails completely, you can use a manual coordinate mapping approach. Let me know if you need this fallback implementation.

### Fix #3: Check Texture Creation

Add this to the `CreateMarkerTextures()` method to verify textures are created:

```csharp
Monitor.Log($"Marker textures created - Device: {Game1.graphics.GraphicsDevice != null}", LogLevel.Info);
```

## Testing Checklist

- [ ] SMAPI console shows mod loaded successfully
- [ ] No red error messages in console at startup
- [ ] You're in-game (past the title screen)
- [ ] At least one NPC is visible in the world
- [ ] Map opens when pressing M
- [ ] `DrawNPCMarkersOnMap called` appears in console when map opens
- [ ] NPC positions count > 0
- [ ] No "Failed to get map position" warnings

## Quick Diagnostic Commands

You can add these temporary debug outputs to understand what's happening:

1. **Check if NPCs are being tracked** - Add to `UpdateNPCPositions()`:
```csharp
Monitor.Log($"Found {npcPositions.Count} NPCs", LogLevel.Info);
```

2. **Check map state** - Add to `OnUpdateTicked()`:
```csharp
if (e.Ticks % 60 == 0) // Every second
{
    Monitor.Log($"isMapOpen: {isMapOpen}, Menu type: {Game1.activeClickableMenu?.GetType().Name}", LogLevel.Debug);
}
```

## Next Steps

1. Run the game with the updated code
2. Open the map (press M)
3. Share the SMAPI console output with me
4. Look specifically for:
   - Any red ERROR messages
   - WARN messages about reflection failures
   - The debug messages I added

The logs will tell us exactly where the problem is!
