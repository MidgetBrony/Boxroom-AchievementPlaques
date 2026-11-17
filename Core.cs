using HarmonyLib;
using MelonLoader;
using ModsPanel;
using SteamShelf;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using UnityEngine;

[assembly: MelonInfo(typeof(BoxroomAchievementPlaques.Core), "Achievement Plaques", "0.1.8", "MidgetBrony")]
[assembly: MelonGame("NestedLoop", "BOXROOM")]

namespace BoxroomAchievementPlaques;

public sealed class Core : MelonMod
{
    internal const string PlaceableId = "BoxroomAchievementPlaques_WallPlaque";
    internal const string OwnerId = "MidgetBrony.AchievementPlaques";
    internal static PlaceableData Data;
    internal static Sprite Icon;

    public override void OnInitializeMelon()
    {
        PlaqueConfig.Load();
        AchievementProgressCache.Reload();

        Data = ScriptableObject.CreateInstance<PlaceableData>();
        Data.ID = PlaceableId;
        Data.DisplayName = "Achievement Plaque";
        Data.DisplayDescription = "Display a game's completion or one unlocked Steam achievement.";
        Data.IsLoadedFromMod = true;
        Data.PlacementType = PlacementType.WallMounted;
        Data.ModScale = 1f;
        Data.SetToolType(EToolType.Placeable);
        Icon = PlaqueArt.CreateCatalogueIcon();

        ModsPanelApi.RegisterSection(OwnerId, "Achievement Plaques", 115).Clear()
            .AddButton("api-key", "Optional Steam Web API fallback key", "Configure", PlaqueMenus.ShowApiKey)
            .AddButton("get-api-key", "Get a personal Steam Web API key", "Open Steam", PlaqueMenus.OpenApiKeyRegistration)
            .AddButton("refresh", "Reload Steam's local completion cache", "Reload", () =>
            {
                AchievementProgressCache.Reload();
                ModsUi.ShowToast($"Achievement cache reloaded: {AchievementProgressCache.All.Count} games.");
            })
            .AddButton("open-cache", "Open Achievement Plaques data and downloaded icons", "Open Folder", PlaquePaths.OpenDataFolder)
            .AddButton("open-steam-cache", "Open Steam's local achievement cache", "Open Steam Cache", PlaquePaths.OpenSteamCacheFolder)
            .AddLabel("help", "The picker uses BOXROOM's full Steam library. Completion comes from Steam's local cache or the keyless Steam Community feed; a personal Web API key is only an optional fallback.");

        LoggerInstance.Msg($"Registered Achievement Plaques 0.1.8; local completion cache contains {AchievementProgressCache.All.Count} games.");
    }

    public override void OnDeinitializeMelon()
    {
        ModsPanelApi.UnregisterSection(OwnerId);
        if (Icon != null)
        {
            Texture texture = Icon.texture;
            UnityEngine.Object.Destroy(Icon);
            if (texture != null) UnityEngine.Object.Destroy(texture);
        }
    }
}

[HarmonyPatch(typeof(ModsPlaceableLoader), nameof(ModsPlaceableLoader.LoadPlaceableData))]
internal static class LoadPlaqueDataPatch
{
    private static void Postfix(ref Task<List<PlaceableData>> __result) => __result = Append(__result);

    private static async Task<List<PlaceableData>> Append(Task<List<PlaceableData>> original)
    {
        List<PlaceableData> result = await original ?? new List<PlaceableData>();
        if (Core.Data != null && !result.Any(value => value != null && value.ID == Core.PlaceableId))
            result.Add(Core.Data);
        return result;
    }
}

[HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.InstantiatePlaceableAsync))]
internal static class InstantiatePlaquePatch
{
    private static bool Prefix(PlaceableData data, ref Task<GameObject> __result)
    {
        if (data == null || data.ID != Core.PlaceableId) return true;
        __result = Task.FromResult(PlaqueFactory.Create(data));
        return false;
    }
}

[HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.GetSpriteAsync))]
internal static class PlaqueSpritePatch
{
    private static bool Prefix(PlaceableData data, ref Task<Sprite> __result)
    {
        if (data == null || data.ID != Core.PlaceableId) return true;
        __result = Task.FromResult(Core.Icon);
        return false;
    }
}
