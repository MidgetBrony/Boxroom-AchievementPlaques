# Achievement Plaques — How to Use

## Install with BoxMate

1. Open BoxMate and refresh the catalogue.
2. Find **Achievement Plaques**.
3. Select the Stable channel and install it.
4. BoxMate will also install or update the required ModsPanel dependency.
5. Start BOXROOM.

## Place and configure a plaque

1. Open BOXROOM's placeables catalogue.
2. Select **Achievement Plaque**.
3. Place the plaque on a wall.
4. Aim at the committed plaque. BOXROOM displays native hints for:
   - **View Plaque** — opens the current award details.
   - **Configure Plaque** — changes the displayed game or achievement.
5. Choose **Configure Plaque**, then **Choose game**.
6. Search by part of a title if the library list is long.

The picker uses BOXROOM's complete Steam library. A game does not need to have an existing Achievement Plaques cache entry before it appears.

## Create a game-completion plaque

1. Choose a game.
2. If progress is already available, select **Game completion plaque**.
3. Otherwise select **Fetch completion and create plaque** and allow Steam to respond.
4. The plaque uses the game's BOXROOM cover art and shows unlocked achievements, total achievements, and completion percentage.

The backing tier updates automatically from the recorded percentage:

| Completion | Backing |
|---|---|
| Below 50% | Graphite brushed metal |
| 50–74.9% | Bronze |
| 75–99.9% | Silver |
| 100% | Gold with `MASTERED` inscription |

## Create an individual achievement plaque

1. Choose a game.
2. Select **Refresh individual achievements from Steam** the first time, or use the cached-achievement option when available.
3. Choose one of the unlocked achievements.
4. The plaque displays its Steam icon, achievement name, game, description, and local unlock date.

Steam can only return achievements and details that the game and the player's privacy settings expose.

## Get a Steam Web API key

The key is optional. Achievement Plaques first tries Steam's local cache and its keyless community feed.

1. Open **Mods → Achievement Plaques**.
2. Select **Get a personal Steam Web API key**, or open the key configuration and choose **Get a Steam Web API key**.
3. Steam's official registration page opens in your browser.
4. Sign in with the Steam account whose library you use in BOXROOM.
5. Enter `localhost` for the requested domain name and complete registration.
6. Copy the displayed key.
7. Return to BOXROOM, open **Optional Steam Web API fallback key**, paste it, and select **Save key**.

The key remains on the local computer in:

```text
BOXROOM\UserData\AchievementPlaques\config.json
```

Do not post or share the key. Clear the field and save if you want to remove it.

## Refresh data or open the cache

The Achievement Plaques ModsPanel section provides:

- **Reload Steam's local completion cache** — reloads achievement progress found on disk.
- **Open Achievement Plaques data and downloaded icons** — opens the mod's local data folder.
- **Open Steam's local achievement cache** — opens the Steam cache used by BOXROOM.

Plaque data and downloaded achievement icons are retained for offline display. Existing placed plaques are restored from the room save.

## Troubleshooting

### A game appears but has no achievement choices

- Confirm that the game actually supports Steam achievements.
- Refresh individual achievements from Steam.
- Check that Steam profile and game-details privacy allow the data to be read.
- Configure the optional Web API key if the community feed is unavailable.

### The completion count looks old

- Unlocks may not yet have reached Steam or BOXROOM's local cache.
- Use **Reload library and completion data** from the plaque configuration.
- Re-fetch the game after Steam has synchronized.

### Cover art is missing

- Let BOXROOM finish loading the game's cover art.
- Reopen or reconfigure the plaque after the library artwork cache has refreshed.

### The plaque cannot be configured

Finish placing it first. Preview objects are intentionally not configurable because they do not yet have a committed room-save entry.

### Where are the files?

Use **Open Folder** in ModsPanel. The default location is:

```text
BOXROOM\UserData\AchievementPlaques
```

