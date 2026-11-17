using System.Diagnostics;
using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;
using SteamShelf;
using UnityEngine;

namespace BoxroomAchievementPlaques;

internal sealed class PlaqueConfig
{
    public string SteamWebApiKey { get; set; } = "";

    [JsonIgnore]
    internal static PlaqueConfig Current { get; private set; } = new();

    [JsonIgnore]
    internal static string FilePath => Path.Combine(PlaquePaths.DataDirectory, "config.json");

    internal static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = JsonConvert.DeserializeObject<PlaqueConfig>(File.ReadAllText(FilePath)) ?? new PlaqueConfig();
        }
        catch (Exception exception)
        {
            MelonLogger.Warning($"[Achievement Plaques] Could not read config: {exception.Message}");
            Current = new PlaqueConfig();
        }
    }

    internal static void Save()
    {
        Directory.CreateDirectory(PlaquePaths.DataDirectory);
        File.WriteAllText(FilePath, JsonConvert.SerializeObject(Current, Formatting.Indented));
    }
}

internal static class PlaquePaths
{
    internal static string DataDirectory => Path.Combine(MelonEnvironment.UserDataDirectory, "AchievementPlaques");
    internal static string DetailsDirectory => Path.Combine(DataDirectory, "Achievements");
    internal static string IconDirectory => Path.Combine(DataDirectory, "Icons");
    internal static string SteamCacheFile { get; private set; } = "";

    internal static void SetSteamCacheFile(string path) => SteamCacheFile = path ?? "";

    internal static string DetailsFile(int appId) => Path.Combine(DetailsDirectory, appId + ".json");

    internal static string IconFile(int appId, string apiName)
    {
        string safe = new((apiName ?? "achievement").Where(character =>
            !Path.GetInvalidFileNameChars().Contains(character)).ToArray());
        return Path.Combine(IconDirectory, $"{appId}-{safe}.png");
    }

    internal static void OpenDataFolder()
    {
        Directory.CreateDirectory(DataDirectory);
        OpenFolder(DataDirectory);
    }

    internal static void OpenSteamCacheFolder()
    {
        if (string.IsNullOrWhiteSpace(SteamCacheFile) || !File.Exists(SteamCacheFile))
        {
            ModsPanel.ModsUi.ShowToast("Steam's achievement cache has not been found.");
            return;
        }
        OpenFolder(Path.GetDirectoryName(SteamCacheFile));
    }

    private static void OpenFolder(string folder)
    {
        try
        {
            string command = Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor
                ? "explorer.exe"
                : Application.platform is RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor ? "open" : "xdg-open";
            Process.Start(new ProcessStartInfo(command, folder) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"[Achievement Plaques] Could not open folder: {exception}");
            ModsPanel.ModsUi.ShowToast("The cache folder could not be opened.");
        }
    }
}

internal sealed class AchievementProgress
{
    public int AppId { get; set; }
    public int Unlocked { get; set; }
    public int Total { get; set; }
    public float Percentage { get; set; }
    public bool AllUnlocked { get; set; }
}

internal static class AchievementProgressCache
{
    private static readonly Dictionary<int, AchievementProgress> Values = new();
    internal static IReadOnlyDictionary<int, AchievementProgress> All => Values;

    internal static bool TryGet(int appId, out AchievementProgress progress) => Values.TryGetValue(appId, out progress);

    internal static void Reload()
    {
        Values.Clear();
        string path = FindCacheFile();
        PlaquePaths.SetSteamCacheFile(path);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        try
        {
            JObject root = JObject.Parse(File.ReadAllText(path));
            if (root["mapCache"] is not JArray entries) return;
            foreach (JToken token in entries)
            {
                if (token is not JArray pair || pair.Count < 2 || pair[1] is not JObject item) continue;
                int appId = item.Value<int?>("appid") ?? pair[0].Value<int>();
                if (appId <= 0) continue;
                Values[appId] = new AchievementProgress
                {
                    AppId = appId,
                    Unlocked = item.Value<int?>("unlocked") ?? 0,
                    Total = item.Value<int?>("total") ?? 0,
                    Percentage = item.Value<float?>("percentage") ?? 0f,
                    AllUnlocked = item.Value<bool?>("all_unlocked") ?? false
                };
            }
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"[Achievement Plaques] Could not parse '{path}': {exception.Message}");
        }
    }

    private static string FindCacheFile()
    {
        string steamRoot = FindSteamRoot();
        if (string.IsNullOrWhiteSpace(steamRoot)) return "";

        string userdata = Path.Combine(steamRoot, "userdata");
        if (!Directory.Exists(userdata)) return "";

        if (SteamClient.IsValid)
        {
            string direct = Path.Combine(userdata, SteamClient.SteamId.AccountId.ToString(), "config", "librarycache", "achievement_progress.json");
            if (File.Exists(direct)) return direct;
        }

        return Directory.GetDirectories(userdata)
            .Select(directory => Path.Combine(directory, "config", "librarycache", "achievement_progress.json"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault() ?? "";
    }

    private static string FindSteamRoot()
    {
        foreach (string candidate in SteamRootCandidates())
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                string normalized = candidate.Replace('/', Path.DirectorySeparatorChar);
                if (File.Exists(Path.Combine(normalized, "appcache", "appinfo.vdf"))) return normalized;
            }
            catch { }
        }
        return "";
    }

    private static IEnumerable<string> SteamRootCandidates()
    {
        string compat = Environment.GetEnvironmentVariable("STEAM_COMPAT_CLIENT_INSTALL_PATH");
        if (!string.IsNullOrWhiteSpace(compat)) yield return compat;

        string userHome = Environment.GetEnvironmentVariable("HOME");
        if (Application.platform is RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor)
        {
            if (!string.IsNullOrWhiteSpace(userHome))
            {
                yield return Path.Combine(userHome, ".local", "share", "Steam");
                yield return Path.Combine(userHome, ".steam", "steam");
                yield return Path.Combine(userHome, ".steam", "root");
                yield return Path.Combine(userHome, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
            }
        }
        else if (Application.platform is RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor)
        {
            if (!string.IsNullOrWhiteSpace(userHome))
                yield return Path.Combine(userHome, "Library", "Application Support", "Steam");
        }
        else
        {
            yield return ReadRegistryString("HKEY_CURRENT_USER\\Software\\Valve\\Steam", "SteamPath");
            yield return ReadRegistryString("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Valve\\Steam", "InstallPath");
            yield return ReadRegistryString("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "InstallPath");
            yield return "C:\\Program Files (x86)\\Steam";
            yield return "C:\\Program Files\\Steam";
        }

        string fromInstallDirectory = null;
        try
        {
            string install = SteamApps.AppInstallDir();
            DirectoryInfo parent = string.IsNullOrWhiteSpace(install) ? null : Directory.GetParent(install.TrimEnd('/', '\\'));
            if (parent?.Parent?.Parent != null) fromInstallDirectory = parent.Parent.Parent.FullName;
        }
        catch { }
        if (!string.IsNullOrWhiteSpace(fromInstallDirectory)) yield return fromInstallDirectory;
    }

    private static string ReadRegistryString(string keyName, string valueName)
    {
        try
        {
            Type registry = Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry");
            MethodInfo getValue = registry?.GetMethod("GetValue", new[] { typeof(string), typeof(string), typeof(object) });
            return getValue?.Invoke(null, new object[] { keyName, valueName, null }) as string;
        }
        catch { return null; }
    }
}

internal sealed class SteamAchievement
{
    public string ApiName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Achieved { get; set; }
    public long UnlockTime { get; set; }
    public string IconUrl { get; set; } = "";
    public string GrayIconUrl { get; set; } = "";
}

internal static class AchievementDetailsCache
{
    internal static List<SteamAchievement> Load(int appId)
    {
        try
        {
            string path = PlaquePaths.DetailsFile(appId);
            return File.Exists(path)
                ? JsonConvert.DeserializeObject<List<SteamAchievement>>(File.ReadAllText(path)) ?? new List<SteamAchievement>()
                : new List<SteamAchievement>();
        }
        catch { return new List<SteamAchievement>(); }
    }

    internal static void Save(int appId, List<SteamAchievement> achievements)
    {
        Directory.CreateDirectory(PlaquePaths.DetailsDirectory);
        File.WriteAllText(PlaquePaths.DetailsFile(appId), JsonConvert.SerializeObject(achievements, Formatting.Indented));
    }
}
