using System.Collections;
using MelonLoader;
using Newtonsoft.Json.Linq;
using Steamworks;
using UnityEngine.Networking;
using System.Xml.Linq;

namespace BoxroomAchievementPlaques;

internal static class SteamAchievementApi
{
    internal static IEnumerator Fetch(int appId, Action<List<SteamAchievement>, string> completed)
    {
        if (!SteamClient.IsValid)
        {
            completed?.Invoke(null, "Steam is not initialized.");
            yield break;
        }

        ulong steamId = SteamClient.SteamId.Value;
        List<SteamAchievement> communityValues = null;
        string communityError = null;
        yield return FetchCommunity(appId, steamId, (values, error) =>
        {
            communityValues = values;
            communityError = error;
        });
        if (communityValues != null)
        {
            AchievementDetailsCache.Save(appId, communityValues);
            completed?.Invoke(communityValues, null);
            yield break;
        }

        string key = PlaqueConfig.Current.SteamWebApiKey?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(key))
        {
            completed?.Invoke(null, (communityError ?? "Steam Community achievement data is unavailable.") +
                " Add a personal Steam Web API key to enable the fallback lookup.");
            yield break;
        }

        string playerUrl = "https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/" +
            $"?key={UnityWebRequest.EscapeURL(key)}&steamid={steamId}&appid={appId}&l=english";
        string schemaUrl = "https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/" +
            $"?key={UnityWebRequest.EscapeURL(key)}&appid={appId}&l=english";

        string playerJson = null;
        string error = null;
        yield return Get(playerUrl, (text, failure) => { playerJson = text; error = failure; });
        if (playerJson == null)
        {
            completed?.Invoke(null, error ?? "Steam did not return achievement data.");
            yield break;
        }

        List<SteamAchievement> values;
        try
        {
            JObject root = JObject.Parse(playerJson);
            bool success = root["playerstats"]?.Value<bool?>("success") ?? false;
            if (!success)
            {
                completed?.Invoke(null, root["playerstats"]?.Value<string>("error") ?? "Steam profile achievement data is unavailable.");
                yield break;
            }
            values = (root["playerstats"]?["achievements"] as JArray ?? new JArray())
                .Select(item => new SteamAchievement
                {
                    ApiName = item.Value<string>("apiname") ?? "",
                    Name = item.Value<string>("name") ?? item.Value<string>("apiname") ?? "Achievement",
                    Description = item.Value<string>("description") ?? "",
                    Achieved = item.Value<int?>("achieved") == 1,
                    UnlockTime = item.Value<long?>("unlocktime") ?? 0
                }).ToList();
        }
        catch (Exception exception)
        {
            completed?.Invoke(null, "Could not parse Steam achievement data: " + exception.Message);
            yield break;
        }

        string schemaJson = null;
        yield return Get(schemaUrl, (text, _) => schemaJson = text);
        if (schemaJson != null)
        {
            try
            {
                JArray schema = JObject.Parse(schemaJson)["game"]?["availableGameStats"]?["achievements"] as JArray;
                Dictionary<string, JToken> byName = schema?.Where(item => item["name"] != null)
                    .ToDictionary(item => item.Value<string>("name"), StringComparer.Ordinal) ?? new Dictionary<string, JToken>();
                foreach (SteamAchievement value in values)
                {
                    if (!byName.TryGetValue(value.ApiName, out JToken item)) continue;
                    if (string.IsNullOrWhiteSpace(value.Name)) value.Name = item.Value<string>("displayName") ?? value.ApiName;
                    if (string.IsNullOrWhiteSpace(value.Description)) value.Description = item.Value<string>("description") ?? "";
                    value.IconUrl = item.Value<string>("icon") ?? "";
                    value.GrayIconUrl = item.Value<string>("icongray") ?? "";
                }
            }
            catch (Exception exception)
            {
                MelonLogger.Warning($"[Achievement Plaques] Achievement schema could not be parsed: {exception.Message}");
            }
        }

        AchievementDetailsCache.Save(appId, values);
        completed?.Invoke(values, null);
    }

    private static IEnumerator FetchCommunity(int appId, ulong steamId, Action<List<SteamAchievement>, string> completed)
    {
        string url = $"https://steamcommunity.com/profiles/{steamId}/stats/{appId}?xml=1&l=english";
        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 30;
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            completed?.Invoke(null, $"Steam Community request failed ({request.responseCode}).");
            yield break;
        }

        try
        {
            XDocument document = XDocument.Parse(request.downloadHandler.text);
            XElement root = document.Root;
            string error = root?.Element("error")?.Value;
            if (!string.IsNullOrWhiteSpace(error))
            {
                completed?.Invoke(null, error.Trim());
                yield break;
            }

            string privacy = root?.Element("privacyState")?.Value;
            if (!string.IsNullOrWhiteSpace(privacy) && !string.Equals(privacy, "public", StringComparison.OrdinalIgnoreCase))
            {
                completed?.Invoke(null, "Steam Community achievement data is private.");
                yield break;
            }

            List<SteamAchievement> values = root?.Element("achievements")?.Elements("achievement")
                .Select(element =>
                {
                    long.TryParse(element.Element("unlockTimestamp")?.Value, out long unlockTime);
                    return new SteamAchievement
                    {
                        ApiName = element.Element("apiname")?.Value ?? "",
                        Name = element.Element("name")?.Value ?? element.Element("apiname")?.Value ?? "Achievement",
                        Description = element.Element("description")?.Value ?? "",
                        Achieved = unlockTime > 0,
                        UnlockTime = unlockTime,
                        IconUrl = element.Element("iconClosed")?.Value ?? "",
                        GrayIconUrl = element.Element("iconOpen")?.Value ?? ""
                    };
                })
                .Where(value => !string.IsNullOrWhiteSpace(value.ApiName))
                .ToList() ?? new List<SteamAchievement>();
            completed?.Invoke(values, null);
        }
        catch (Exception exception)
        {
            completed?.Invoke(null, "Steam Community achievement XML could not be parsed: " + exception.Message);
        }
    }

    internal static IEnumerator DownloadIcon(int appId, SteamAchievement achievement, Action<string> completed)
    {
        string destination = PlaquePaths.IconFile(appId, achievement.ApiName);
        if (File.Exists(destination))
        {
            completed?.Invoke(destination);
            yield break;
        }
        if (string.IsNullOrWhiteSpace(achievement.IconUrl))
        {
            completed?.Invoke("");
            yield break;
        }

        using UnityWebRequest request = UnityWebRequest.Get(achievement.IconUrl);
        request.timeout = 30;
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success || request.downloadHandler.data == null)
        {
            MelonLogger.Warning($"[Achievement Plaques] Icon download failed: {request.error}");
            completed?.Invoke("");
            yield break;
        }

        Directory.CreateDirectory(PlaquePaths.IconDirectory);
        File.WriteAllBytes(destination, request.downloadHandler.data);
        completed?.Invoke(destination);
    }

    private static IEnumerator Get(string url, Action<string, string> completed)
    {
        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 30;
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            string message = request.responseCode == 401 || request.responseCode == 403
                ? "Steam rejected the Web API key."
                : $"Steam request failed ({request.responseCode}): {request.error}";
            completed?.Invoke(null, message);
            yield break;
        }
        completed?.Invoke(request.downloadHandler.text, null);
    }
}
