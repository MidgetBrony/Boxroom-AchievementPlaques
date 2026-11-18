using MelonLoader;
using ModsPanel;
using SteamShelf;
using UnityEngine;

namespace BoxroomAchievementPlaques;

internal static class PlaqueMenus
{
    private const string SteamWebApiKeyUrl = "https://steamcommunity.com/dev/apikey";
    private static string gameFilter = "";
    private static string pendingApiKey = "";

    internal static void ShowDetails(AchievementPlaque plaque)
    {
        if (plaque == null) return;
        PlaqueSelection value = plaque.Selection;
        if (value.Kind == PlaqueKind.Unconfigured)
        {
            ShowConfigure(plaque);
            return;
        }

        ModMenu menu = ModsUi.CreateMenu(Core.OwnerId + ".Details", value.Kind == PlaqueKind.GameCompletion ? value.GameName : value.AchievementName);
        menu.Eyebrow = value.Kind == PlaqueKind.GameCompletion ? "GAME COMPLETION" : "STEAM ACHIEVEMENT";
        if (value.Kind == PlaqueKind.GameCompletion)
        {
            menu.Subtitle = $"{value.Unlocked} / {value.Total} achievements • {value.Percentage:0.#}% complete";
            if (value.Percentage >= 99.999f) menu.AddLabel("MASTERED — every Steam achievement unlocked.");
        }
        else
        {
            menu.Subtitle = value.GameName;
            if (!string.IsNullOrWhiteSpace(value.AchievementDescription)) menu.AddLabel(value.AchievementDescription);
            if (value.UnlockTime > 0)
                menu.AddLabel("Unlocked " + DateTimeOffset.FromUnixTimeSeconds(value.UnlockTime).ToLocalTime().ToString("d MMMM yyyy"));
        }
        menu.AddButton("Configure plaque", () => ShowConfigure(plaque));
        menu.Show();
    }

    internal static void ShowConfigure(AchievementPlaque plaque)
    {
        if (plaque == null || !plaque.IsCommitted)
        {
            ModsUi.ShowToast("Place the plaque before configuring it.");
            return;
        }
        PlaqueSelection value = plaque.Selection;
        string subtitle = value.Kind == PlaqueKind.Unconfigured ? "Not configured" : value.GameName;
        int libraryCount = GetSteamGames().Count;
        ModMenu menu = ModsUi.CreateMenu(Core.OwnerId + ".Configure", "ACHIEVEMENT PLAQUE", subtitle);
        menu.Eyebrow = "PLAQUE DESIGN";
        menu.AddButton("Choose game", () => ShowGamePicker(plaque), $"{libraryCount} Steam games in BOXROOM's library")
            .AddButton("Set Steam Web API key", ShowApiKey, string.IsNullOrWhiteSpace(PlaqueConfig.Current.SteamWebApiKey) ? "Optional fallback for unavailable community data" : "Fallback configured")
            .AddButton("Reload library and completion data", () =>
            {
                AchievementProgressCache.Reload();
                if (value.AppId > 0 && TryResolveProgress(value.AppId, out AchievementProgress progress))
                {
                    value.Unlocked = progress.Unlocked;
                    value.Total = progress.Total;
                    value.Percentage = progress.Percentage;
                    plaque.Configure(value);
                }
                ModsUi.ShowToast("Steam library and completion data reloaded.");
                ShowConfigure(plaque);
            })
            .AddButton("Open cache folder", PlaquePaths.OpenDataFolder);
        menu.Show();
    }

    internal static void ShowApiKey()
    {
        pendingApiKey = PlaqueConfig.Current.SteamWebApiKey ?? "";
        ModMenu menu = ModsUi.CreateMenu(Core.OwnerId + ".ApiKey", "STEAM WEB API KEY");
        menu.Eyebrow = "OPTIONAL FALLBACK";
        menu.AddLabel("The mod tries Steam's keyless community achievement feed first. Your personal key is only a fallback and stays locally in UserData/AchievementPlaques/config.json.")
            .AddLabel("Need a key? Sign in on Steam's official registration page, use localhost for the domain name, then copy the displayed key back here.")
            .AddSpacer(52f)
            .AddButton("Get a Steam Web API key", OpenApiKeyRegistration, "OPEN STEAM'S OFFICIAL KEY PAGE")
            .AddPasswordInput("API key", () => pendingApiKey, value => pendingApiKey = value ?? "", "Paste your Steam Web API key")
            .AddButton("Save key", () =>
            {
                PlaqueConfig.Current.SteamWebApiKey = pendingApiKey.Trim();
                PlaqueConfig.Save();
                ModsUi.ShowToast(string.IsNullOrWhiteSpace(pendingApiKey) ? "Steam Web API key cleared." : "Steam Web API key saved locally.");
            });
        menu.Show();
    }

    internal static void OpenApiKeyRegistration()
    {
        Application.OpenURL(SteamWebApiKeyUrl);
        ModsUi.ShowToast("Opened Steam's Web API key registration page.");
    }

    private static List<SteamGameData> GetSteamGames()
    {
        return SteamLibrarySystem.GetLibraryGames()
            .Where(game => game != null && game.AppId > 0 &&
                (string.IsNullOrWhiteSpace(game.AppType) || game.AppType == "game" || game.AppType == "demo"))
            .GroupBy(game => game.AppId)
            .Select(group => group.First())
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ShowGamePicker(AchievementPlaque plaque)
    {
        List<SteamGameData> library = GetSteamGames();
        List<SteamGameData> games = library
            .Where(game => string.IsNullOrWhiteSpace(gameFilter) ||
                (game.Name ?? game.AppId.ToString()).Contains(gameFilter, StringComparison.OrdinalIgnoreCase))
            .Take(60)
            .ToList();

        ModMenu menu = ModsUi.CreateMenu(Core.OwnerId + ".Games", "CHOOSE A GAME", $"{library.Count} Steam games");
        menu.Eyebrow = "ACHIEVEMENT PLAQUE";
        menu.AddTextInput("Find game", () => gameFilter, value => gameFilter = value ?? "", "Type part of a title")
            .AddButton("Apply search", () => ShowGamePicker(plaque));
        foreach (SteamGameData game in games)
        {
            string name = string.IsNullOrWhiteSpace(game.Name) ? $"Steam App {game.AppId}" : game.Name;
            bool hasProgress = TryResolveProgress(game.AppId, out AchievementProgress progress);
            string detail = hasProgress
                ? $"{progress.Unlocked}/{progress.Total} • {progress.Percentage:0.#}%"
                : "Completion will be fetched when selected";
            menu.AddButton(name, () => ShowGameOptions(plaque, game, name), detail);
        }
        if (games.Count == 0)
            menu.AddLabel(library.Count == 0 ? "BOXROOM's Steam library is not ready yet." : "No Steam games match that search.");
        else if (games.Count == 60)
            menu.AddLabel("Showing the first 60 matches. Narrow the search to find another title.");
        menu.Show();
    }

    private static void ShowGameOptions(AchievementPlaque plaque, SteamGameData game, string gameName)
    {
        bool hasProgress = TryResolveProgress(game.AppId, out AchievementProgress progress);
        string subtitle = hasProgress
            ? $"{progress.Unlocked}/{progress.Total} • {progress.Percentage:0.#}% complete"
            : "Achievement completion not cached";
        ModMenu menu = ModsUi.CreateMenu(Core.OwnerId + ".Game", gameName, subtitle);
        menu.Eyebrow = "PLAQUE TYPE";

        if (hasProgress)
        {
            menu.AddButton("Game completion plaque", () => ApplyCompletionPlaque(plaque, progress, gameName),
                "Uses the game's cover art and completion-tier backing");
        }
        else
        {
            menu.AddButton("Fetch completion and create plaque", () => FetchCompletion(plaque, game.AppId, gameName),
                "Retrieves this game's achievement totals from Steam");
        }

        List<SteamAchievement> allCached = AchievementDetailsCache.Load(game.AppId);
        List<SteamAchievement> unlockedCached = allCached.Where(value => value.Achieved).ToList();
        if (unlockedCached.Count > 0)
        {
            AchievementProgress cachedProgress = ProgressFromAchievements(game.AppId, allCached);
            menu.AddButton("Choose cached individual achievement", () => ShowAchievementPicker(plaque, cachedProgress, gameName, unlockedCached),
                $"{unlockedCached.Count} unlocked achievements cached");
        }
        menu.AddButton("Refresh individual achievements from Steam", () => FetchAchievements(plaque, game.AppId, gameName),
            "Uses Steam Community first; Web API key is an optional fallback");
        menu.Show();
    }

    private static void ApplyCompletionPlaque(AchievementPlaque plaque, AchievementProgress progress, string gameName)
    {
        plaque.Configure(new PlaqueSelection
        {
            Kind = PlaqueKind.GameCompletion,
            AppId = progress.AppId,
            GameName = gameName,
            Unlocked = progress.Unlocked,
            Total = progress.Total,
            Percentage = progress.Percentage
        });
        ModsUi.CloseMenu();
        ModsUi.ShowToast(progress.AllUnlocked ? "Gold mastered plaque created." : "Game completion plaque created.");
    }

    private static void FetchCompletion(AchievementPlaque plaque, int appId, string gameName)
    {
        ModsUi.ShowToast("Fetching achievement completion from Steam…");
        MelonCoroutines.Start(SteamAchievementApi.Fetch(appId, (values, error) =>
        {
            if (values == null)
            {
                ModsUi.ShowToast(error ?? "Steam achievement lookup failed.", 7f);
                return;
            }
            AchievementProgress progress = ProgressFromAchievements(appId, values);
            if (progress.Total == 0)
            {
                ModsUi.ShowToast("Steam reports that this game has no achievements.");
                return;
            }
            ApplyCompletionPlaque(plaque, progress, gameName);
        }));
    }

    private static void FetchAchievements(AchievementPlaque plaque, int appId, string gameName)
    {
        ModsUi.ShowToast("Fetching achievements from Steam…");
        MelonCoroutines.Start(SteamAchievementApi.Fetch(appId, (values, error) =>
        {
            if (values == null)
            {
                ModsUi.ShowToast(error ?? "Steam achievement lookup failed.", 7f);
                return;
            }
            AchievementProgress progress = ProgressFromAchievements(appId, values);
            List<SteamAchievement> unlocked = values.Where(value => value.Achieved).OrderByDescending(value => value.UnlockTime).ToList();
            if (unlocked.Count == 0)
            {
                ModsUi.ShowToast(progress.Total == 0
                    ? "Steam reports that this game has no achievements."
                    : "No achievements have been unlocked for this game.");
                return;
            }
            ShowAchievementPicker(plaque, progress, gameName, unlocked);
        }));
    }

    private static bool TryResolveProgress(int appId, out AchievementProgress progress)
    {
        if (AchievementProgressCache.TryGet(appId, out progress) && progress.Total > 0) return true;
        List<SteamAchievement> cached = AchievementDetailsCache.Load(appId);
        if (cached.Count > 0)
        {
            progress = ProgressFromAchievements(appId, cached);
            return progress.Total > 0;
        }
        progress = null;
        return false;
    }

    private static AchievementProgress ProgressFromAchievements(int appId, IReadOnlyCollection<SteamAchievement> achievements)
    {
        int total = achievements?.Count ?? 0;
        int unlocked = achievements?.Count(value => value.Achieved) ?? 0;
        return new AchievementProgress
        {
            AppId = appId,
            Unlocked = unlocked,
            Total = total,
            Percentage = total == 0 ? 0f : unlocked * 100f / total,
            AllUnlocked = total > 0 && unlocked == total
        };
    }

    private static void ShowAchievementPicker(AchievementPlaque plaque, AchievementProgress progress, string gameName, List<SteamAchievement> achievements)
    {
        ModMenu menu = ModsUi.CreateMenu(Core.OwnerId + ".Achievements", "CHOOSE AN ACHIEVEMENT", gameName);
        menu.Eyebrow = "UNLOCKED ACHIEVEMENTS";
        foreach (SteamAchievement achievement in achievements.Take(100))
        {
            string name = string.IsNullOrWhiteSpace(achievement.Name) ? achievement.ApiName : achievement.Name;
            string detail = achievement.UnlockTime > 0
                ? "Unlocked " + DateTimeOffset.FromUnixTimeSeconds(achievement.UnlockTime).ToLocalTime().ToString("d MMM yyyy")
                : achievement.Description;
            menu.AddButton(name, () => SelectAchievement(plaque, progress, gameName, achievement), detail);
        }
        menu.Show();
    }

    private static void SelectAchievement(AchievementPlaque plaque, AchievementProgress progress, string gameName, SteamAchievement achievement)
    {
        void Apply(string iconPath)
        {
            plaque.Configure(new PlaqueSelection
            {
                Kind = PlaqueKind.IndividualAchievement,
                AppId = progress.AppId,
                GameName = gameName,
                Unlocked = progress.Unlocked,
                Total = progress.Total,
                Percentage = progress.Percentage,
                AchievementApiName = achievement.ApiName,
                AchievementName = string.IsNullOrWhiteSpace(achievement.Name) ? achievement.ApiName : achievement.Name,
                AchievementDescription = achievement.Description,
                UnlockTime = achievement.UnlockTime,
                AchievementIconPath = iconPath ?? ""
            });
            ModsUi.CloseMenu();
            ModsUi.ShowToast("Individual achievement plaque created.");
        }

        string existing = PlaquePaths.IconFile(progress.AppId, achievement.ApiName);
        if (File.Exists(existing)) { Apply(existing); return; }
        MelonCoroutines.Start(SteamAchievementApi.DownloadIcon(progress.AppId, achievement, Apply));
    }
}
