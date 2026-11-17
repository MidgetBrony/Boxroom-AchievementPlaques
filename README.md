# Achievement Plaques for BOXROOM

Achievement Plaques adds native wall-mounted awards for your Steam games. Display overall completion using the game's cover art, or create a plaque for an individual unlocked achievement.

## Features

- Uses BOXROOM's full Steam library rather than only previously cached games.
- Game-completion plaques show cover art, unlocked/total achievements, and completion percentage.
- Individual plaques show the Steam achievement icon, name, game, description, and unlock date.
- Completion-tier backings use a brushed metallic finish:
  - **Graphite:** below 50%
  - **Bronze:** 50–74.9%
  - **Silver:** 75–99.9%
  - **Gold / Mastered:** 100%
- Plaques save with the room and refresh their presentation when loaded.
- Native BOXROOM control hints show the view and configure interactions.
- Achievement details and downloaded icons are cached for offline display.
- ModsPanel shortcuts open both the plaque cache and Steam's local achievement cache.

## Quick start

1. Install **Achievement Plaques** through [BoxMate](https://github.com/MidgetBrony/BoxMate).
2. Start BOXROOM and open the placeables catalogue.
3. Choose **Achievement Plaque** and place it on a wall.
4. Point at the placed plaque and use the displayed **Configure Plaque** control hint.
5. Choose a Steam game.
6. Create either a **Game completion plaque** or select an unlocked individual achievement.

For API-key setup, troubleshooting, data sources, and detailed controls, see the [complete HOW-TO guide](docs/HOW-TO.md).

## Steam Web API key

Most players do not need a key. The mod tries Steam's local cache and keyless community feed first. If those cannot provide a game's achievement data, a personal Steam Web API key can be used as a fallback.

Open **Mods → Achievement Plaques → Get a personal Steam Web API key**, or visit [Steam's official Web API key page](https://steamcommunity.com/dev/apikey). Sign in, use `localhost` for the domain name, copy the displayed key, then paste it into **Optional Steam Web API fallback key**. The key is stored only in `UserData/AchievementPlaques/config.json`.

## Manual installation

1. Install MelonLoader 0.7.0 or later for BOXROOM.
2. Install ModsPanel 2.7.5 or later.
3. Copy `Boxroom_AchievementPlaques.dll` into BOXROOM's `Mods` folder.

BoxMate installs the required ModsPanel dependency automatically.

## Data sources and limitations

The picker enumerates BOXROOM's Steam library. Achievement progress comes from Steam's local `achievement_progress.json`, Steam's keyless community achievement feed, or the optional personal Web API key fallback. A game must expose Steam achievements, and Steam profile/privacy settings can limit remotely available data.

Cached configuration, achievement details, and icons live under `UserData/AchievementPlaques`. Use the mod's **Open Folder** button for quick access.

## Build

```powershell
dotnet build -c Release -p:GamePath="D:\SteamLibrary\steamapps\common\My Game Room"
```

Compilation, packaging, and installation do not prove wall placement, visual presentation, interaction flow, or save/load behavior. Those require an in-game check.

