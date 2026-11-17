using System.Collections;
using MelonLoader;
using Newtonsoft.Json;
using SkiaSharp;
using SteamShelf;
using SteamShelf.ControlHints;
using SteamShelf.Placeables;
using SteamShelf.Save;
using UnityEngine;

namespace BoxroomAchievementPlaques;

internal enum PlaqueKind
{
    Unconfigured,
    GameCompletion,
    IndividualAchievement
}

[Serializable]
internal sealed class PlaqueSelection
{
    public PlaqueKind Kind = PlaqueKind.Unconfigured;
    public int AppId;
    public string GameName = "";
    public int Unlocked;
    public int Total;
    public float Percentage;
    public string AchievementApiName = "";
    public string AchievementName = "";
    public string AchievementDescription = "";
    public long UnlockTime;
    public string AchievementIconPath = "";

    internal static PlaqueSelection FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new PlaqueSelection();
        try { return JsonConvert.DeserializeObject<PlaqueSelection>(json) ?? new PlaqueSelection(); }
        catch { return new PlaqueSelection(); }
    }
}

internal static class PlaqueFactory
{
    internal static GameObject Create(PlaceableData data)
    {
        GameObject root = new("Achievement Plaque");
        root.SetActive(false);
        int layer = LayerMask.NameToLayer("Prop");
        root.layer = layer;

        PlacementTag tag = root.AddComponent<PlacementTag>();
        tag.PlaceableData = data;

        AchievementPlaque placeable = root.AddComponent<AchievementPlaque>();
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0f, -0.025f);
        collider.size = new Vector3(0.66f, 0.86f, 0.055f);

        Renderer backing = Cube(root.transform, "Award backing", new Vector3(0.60f, 0.80f, 0.025f), new Vector3(0f, 0f, -0.035f), PlaqueArt.Dark, metallicBacking: true);
        Cube(root.transform, "Frame top", new Vector3(0.68f, 0.045f, 0.065f), new Vector3(0f, 0.4225f, -0.018f), PlaqueArt.Frame);
        Cube(root.transform, "Frame bottom", new Vector3(0.68f, 0.045f, 0.065f), new Vector3(0f, -0.4225f, -0.018f), PlaqueArt.Frame);
        Cube(root.transform, "Frame left", new Vector3(0.045f, 0.80f, 0.065f), new Vector3(-0.3175f, 0f, -0.018f), PlaqueArt.Frame);
        Cube(root.transform, "Frame right", new Vector3(0.045f, 0.80f, 0.065f), new Vector3(0.3175f, 0f, -0.018f), PlaqueArt.Frame);

        Renderer art = Quad(root.transform, "Plaque artwork", new Vector3(0.36f, 0.52f, 1f), new Vector3(0f, 0.105f, -0.054f));
        Renderer label = Quad(root.transform, "Plaque inscription", new Vector3(0.52f, 0.17f, 1f), new Vector3(0f, -0.295f, -0.056f));
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;

        PlaceablePainter painter = root.AddComponent<PlaceablePainter>();
        painter.InitializeMaterials();
        painter.SetupAsForcedPlaceable();
        placeable.Initialize(backing, art, label);
        root.AddComponent<PlaqueInteractable>();
        root.SetActive(true);
        return root;
    }

    private static Renderer Cube(Transform parent, string name, Vector3 scale, Vector3 position, Color color, bool metallicBacking = false)
    {
        GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
        value.name = name;
        value.transform.SetParent(parent, false);
        value.transform.localScale = scale;
        value.transform.localPosition = position;
        UnityEngine.Object.Destroy(value.GetComponent<Collider>());
        Renderer renderer = value.GetComponent<Renderer>();
        renderer.material = metallicBacking ? PlaqueArt.BackingMaterial(color) : PlaqueArt.SolidMaterial(color);
        return renderer;
    }

    private static Renderer Quad(Transform parent, string name, Vector3 scale, Vector3 position)
    {
        GameObject value = GameObject.CreatePrimitive(PrimitiveType.Quad);
        value.name = name;
        value.transform.SetParent(parent, false);
        value.transform.localScale = scale;
        value.transform.localPosition = position;
        UnityEngine.Object.Destroy(value.GetComponent<Collider>());
        Renderer renderer = value.GetComponent<Renderer>();
        renderer.material = PlaqueArt.TextureMaterial();
        return renderer;
    }
}

internal sealed class AchievementPlaque : MonoBehaviour, IPlaceable
{
    private PlaceableSaveState state;
    private PlaqueSelection selection = new();
    private Renderer backing;
    private Renderer artwork;
    private Renderer inscription;
    private Texture2D artTexture;
    private Texture2D labelTexture;
    private int refreshVersion;

    public PlaceableSaveState SaveState => state;
    internal PlaqueSelection Selection => selection;
    internal bool IsCommitted => state != null && GetComponent<PlacementTag>()?.IsPreview != true;

    internal void Initialize(Renderer backingRenderer, Renderer artworkRenderer, Renderer inscriptionRenderer)
    {
        backing = backingRenderer;
        artwork = artworkRenderer;
        inscription = inscriptionRenderer;
        ApplyPlaceholder();
    }

    public void PopulateFromLoad(PlaceableSaveState saveState) => ApplyState(saveState);
    public void LinkSaveState(PlaceableSaveState saveState) => ApplyState(saveState);
    public void RefreshSaveStateReference(PlaceableSaveState newState) => ApplyState(newState);
    public void OnPlaced() => RefreshVisual();
    public void OnPickedUp() { }
    public void OnDeleted() { }

    internal void Configure(PlaqueSelection value)
    {
        selection = value ?? new PlaqueSelection();
        if (state != null) state.CustomData = JsonConvert.SerializeObject(selection);
        RefreshVisual();
    }

    private void ApplyState(PlaceableSaveState saveState)
    {
        state = saveState;
        selection = PlaqueSelection.FromJson(saveState?.CustomData);
        RefreshVisual();
    }

    private void Start()
    {
        ResolveRenderers();
        RefreshVisual();
    }

    private void ResolveRenderers()
    {
        backing ??= transform.Find("Award backing")?.GetComponent<Renderer>();
        artwork ??= transform.Find("Plaque artwork")?.GetComponent<Renderer>();
        inscription ??= transform.Find("Plaque inscription")?.GetComponent<Renderer>();
    }

    internal void RefreshVisual()
    {
        ResolveRenderers();
        int version = ++refreshVersion;
        if (selection.Kind == PlaqueKind.Unconfigured || selection.AppId <= 0)
        {
            ApplyPlaceholder();
            return;
        }
        ApplyTier(selection.Percentage);
        ApplyLabel();
        if (selection.Kind == PlaqueKind.IndividualAchievement)
        {
            string iconPath = selection.AchievementIconPath;
            if (!File.Exists(iconPath) && !string.IsNullOrWhiteSpace(selection.AchievementApiName))
                iconPath = PlaquePaths.IconFile(selection.AppId, selection.AchievementApiName);
            if (File.Exists(iconPath)) ApplyArt(File.ReadAllBytes(iconPath), square: true);
            else ApplyGeneratedArt("ACHIEVEMENT", selection.AchievementName, square: true);
            return;
        }
        RefreshGameArt(version);
    }

    private async void RefreshGameArt(int version)
    {
        try
        {
            SteamGameData game = await SteamLibrarySystem.GetGameAsync(selection.AppId);
            if (game == null || version != refreshVersion) return;
            await SteamLibrarySystem.EnsureBoxArtAsync(selection.AppId);
            if (!game.BoxArtLoaded || game.BoxArtBytes == null || game.BoxArtBytes.Length == 0)
                await BoxroomArtServer.TryFetchCoverAsync(game, CancellationToken.None);
            if (version != refreshVersion) return;
            if (game.BoxArtBytes != null && game.BoxArtBytes.Length > 0) ApplyArt(game.BoxArtBytes, square: false);
            else ApplyGeneratedArt("GAME", selection.GameName, square: false);
        }
        catch (Exception exception)
        {
            MelonLogger.Warning($"[Achievement Plaques] Cover refresh failed: {exception.Message}");
            ApplyGeneratedArt("GAME", selection.GameName, square: false);
        }
    }

    private void ApplyPlaceholder()
    {
        ApplyTier(0f);
        ApplyGeneratedArt("ACHIEVEMENT", "PLAQUE", square: true);
        SetLabel(PlaqueArt.RenderLabel("ACHIEVEMENT PLAQUE", "PLACE ON A WALL", "INTERACT TO CONFIGURE", false));
    }

    private void ApplyTier(float percentage)
    {
        Color color = percentage >= 99.999f ? PlaqueArt.Gold : percentage >= 75f ? PlaqueArt.Silver : percentage >= 50f ? PlaqueArt.Bronze : PlaqueArt.Dark;
        if (backing != null) PlaqueArt.SetBackingTier(backing.material, color);
    }

    private void ApplyLabel()
    {
        if (selection.Kind == PlaqueKind.GameCompletion)
        {
            string status = selection.Percentage >= 99.999f ? "MASTERED • 100%" : $"{selection.Percentage:0.#}% COMPLETE";
            SetLabel(PlaqueArt.RenderLabel(selection.GameName, $"{selection.Unlocked} / {selection.Total} ACHIEVEMENTS", status, selection.Percentage >= 99.999f));
            return;
        }

        string date = selection.UnlockTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(selection.UnlockTime).ToLocalTime().ToString("d MMM yyyy")
            : "UNLOCKED";
        SetLabel(PlaqueArt.RenderLabel(selection.AchievementName, selection.GameName, date, selection.Percentage >= 99.999f));
    }

    private void ApplyGeneratedArt(string eyebrow, string title, bool square)
    {
        ApplyArt(PlaqueArt.RenderArtwork(eyebrow, title, square), square);
    }

    private void ApplyArt(byte[] bytes, bool square)
    {
        if (artwork == null || bytes == null || bytes.Length == 0) return;
        Texture2D next = new(2, 2, TextureFormat.RGBA32, false);
        if (!next.LoadImage(bytes, false)) { UnityEngine.Object.Destroy(next); return; }
        next.wrapMode = TextureWrapMode.Clamp;
        next.filterMode = FilterMode.Bilinear;
        Texture2D previous = artTexture;
        artTexture = next;
        artwork.material.mainTexture = next;
        artwork.transform.localScale = square ? new Vector3(0.38f, 0.38f, 1f) : new Vector3(0.36f, 0.52f, 1f);
        if (previous != null && previous != next) UnityEngine.Object.Destroy(previous);
    }

    private void SetLabel(byte[] bytes)
    {
        if (inscription == null) return;
        Texture2D next = new(2, 2, TextureFormat.RGBA32, false);
        if (!next.LoadImage(bytes, false)) { UnityEngine.Object.Destroy(next); return; }
        next.wrapMode = TextureWrapMode.Clamp;
        next.filterMode = FilterMode.Bilinear;
        Texture2D previous = labelTexture;
        labelTexture = next;
        inscription.material.mainTexture = next;
        if (previous != null && previous != next) UnityEngine.Object.Destroy(previous);
    }

    // Do not destroy the live face textures here. ObjectPlacer clones a source
    // placeable before destroying it, and the new object initially shares those
    // texture references. Destroying them with the source blackens the clone.
}

internal sealed class PlaqueInteractable : MonoBehaviour, IInteractable, IHoverHintInteractable
{
    private HintHandle viewHint;
    private HintHandle configureHint;

    public void OnInteract()
    {
        AchievementPlaque plaque = GetComponent<AchievementPlaque>();
        if (plaque != null) PlaqueMenus.ShowDetails(plaque);
    }

    public void OnAlternateInteract()
    {
        AchievementPlaque plaque = GetComponent<AchievementPlaque>();
        if (plaque != null) PlaqueMenus.ShowConfigure(plaque);
    }

    public void OnHoverEnter()
    {
        OnHoverExit();
        viewHint = new HintHandle("AchievementPlaques.View", new ControlHint
        {
            actionLabel = "View Plaque",
            bindingKeys = new[] { "Primary" },
            priority = 2
        });
        configureHint = new HintHandle("AchievementPlaques.Configure", new ControlHint
        {
            actionLabel = "Configure Plaque",
            bindingKeys = new[] { "Secondary" },
            priority = 3
        });
    }

    public void OnHoverExit()
    {
        viewHint?.Dispose();
        configureHint?.Dispose();
        viewHint = null;
        configureHint = null;
    }

    private void OnDestroy() => OnHoverExit();
}

internal static class PlaqueArt
{
    private const int BackingTextureSize = 256;
    private static readonly Dictionary<string, Texture2D> BackingTextures = new();

    internal static readonly Color Frame = new(0.14f, 0.17f, 0.20f, 1f);
    internal static readonly Color Dark = new(0.27f, 0.28f, 0.29f, 1f);
    internal static readonly Color Bronze = new(0.48f, 0.26f, 0.10f, 1f);
    internal static readonly Color Silver = new(0.60f, 0.64f, 0.68f, 1f);
    internal static readonly Color Gold = new(0.82f, 0.62f, 0.18f, 1f);

    internal static Material SolidMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
        Material material = new(shader);
        SetMaterialColor(material, color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.22f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.42f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.42f);
        return material;
    }

    internal static Material BackingMaterial(Color color)
    {
        Material material = SolidMaterial(Color.white);
        SetBackingTier(material, color);
        return material;
    }

    internal static void SetBackingTier(Material material, Color color)
    {
        if (material == null) return;

        Texture2D texture = GetBackingTexture(color);
        SetMaterialColor(material, Color.white);
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.58f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.58f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.58f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.12f, 0.12f, 0.12f, 1f));
            if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", texture);
        }
    }

    private static Texture2D GetBackingTexture(Color color)
    {
        string key = ColorUtility.ToHtmlStringRGB(color);
        if (BackingTextures.TryGetValue(key, out Texture2D existing) && existing != null) return existing;

        Texture2D texture = new(BackingTextureSize, BackingTextureSize, TextureFormat.RGBA32, false)
        {
            name = $"Achievement Plaque Brushed Backing {key}",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color32[] pixels = new Color32[BackingTextureSize * BackingTextureSize];

        for (int y = 0; y < BackingTextureSize; y++)
        {
            float vertical = (y + 0.5f) / BackingTextureSize;
            for (int x = 0; x < BackingTextureSize; x++)
            {
                float horizontal = (x + 0.5f) / BackingTextureSize;
                float sweepCenter = 0.16f + vertical * 0.48f;
                float sweepDistance = (horizontal - sweepCenter) / 0.14f;
                float sweep = Mathf.Exp(-(sweepDistance * sweepDistance));
                float secondaryDistance = (horizontal - (0.84f - vertical * 0.14f)) / 0.20f;
                float secondary = Mathf.Exp(-(secondaryDistance * secondaryDistance)) * 0.07f;
                float brush = Mathf.Sin(vertical * Mathf.PI * 142f + Mathf.Sin(horizontal * Mathf.PI * 9f)) * 0.022f;
                float grain = (BackingNoise(x, y) - 0.5f) * 0.035f;
                float edge = Mathf.Pow(Mathf.Abs(horizontal - 0.5f) * 2f, 1.7f) * 0.08f;
                float brightness = 0.88f + sweep * 0.20f + secondary + brush + grain - edge;
                Color swept = Color.Lerp(color, Color.white, sweep * 0.16f);
                Color pixel = new(
                    Mathf.Clamp01(swept.r * brightness),
                    Mathf.Clamp01(swept.g * brightness),
                    Mathf.Clamp01(swept.b * brightness),
                    1f);
                pixels[y * BackingTextureSize + x] = pixel;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        BackingTextures[key] = texture;
        return texture;
    }

    private static float BackingNoise(int x, int y)
    {
        uint value = (uint)(x * 374761393 + y * 668265263);
        value = (value ^ (value >> 13)) * 1274126177u;
        return (value & 1023u) / 1023f;
    }

    internal static Material TextureMaterial()
    {
        Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        return new Material(shader) { color = Color.white };
    }

    internal static void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    internal static byte[] RenderLabel(string title, string line2, string line3, bool gold)
    {
        const int width = 900, height = 280;
        using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKCanvas canvas = new(bitmap);
        SKColor accent = gold ? new SKColor(238, 190, 66) : new SKColor(202, 207, 212);
        canvas.Clear(new SKColor(12, 14, 17));
        using SKPaint border = new() { Color = accent, Style = SKPaintStyle.Stroke, StrokeWidth = 8, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(14, 14, width - 14, height - 14), 10, 10, border);
        DrawCentered(canvas, title, 72, 64, accent, true, width - 70);
        DrawCentered(canvas, line2, 156, 38, SKColors.White, false, width - 70);
        DrawCentered(canvas, line3, 222, 34, accent, true, width - 70);
        return Encode(bitmap);
    }

    internal static byte[] RenderArtwork(string eyebrow, string title, bool square)
    {
        int width = square ? 512 : 600;
        int height = square ? 512 : 900;
        using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(new SKColor(19, 27, 35));
        using SKPaint border = new() { Color = new SKColor(209, 171, 72), Style = SKPaintStyle.Stroke, StrokeWidth = 12, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(24, 24, width - 24, height - 24), 18, 18, border);
        DrawCentered(canvas, eyebrow, height * 0.34f, 30, new SKColor(209, 171, 72), true, width - 70);
        DrawCentered(canvas, title, height * 0.52f, square ? 54 : 48, SKColors.White, true, width - 75);
        return Encode(bitmap);
    }

    internal static Sprite CreateCatalogueIcon()
    {
        byte[] bytes = RenderArtwork("ACHIEVEMENT", "PLAQUE", true);
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(bytes, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
    }

    private static void DrawCentered(SKCanvas canvas, string value, float y, float size, SKColor color, bool bold, float maxWidth)
    {
        value = string.IsNullOrWhiteSpace(value) ? " " : value.Trim();
        using SKTypeface typeface = SKTypeface.FromFamilyName("Arial", bold ? SKFontStyle.Bold : SKFontStyle.Normal) ?? SKTypeface.Default;
        using SKFont font = new(typeface, size);
        using SKPaint paint = new() { Color = color, IsAntialias = true };
        float measured = font.MeasureText(value);
        if (measured > maxWidth && measured > 0f) font.Size *= maxWidth / measured;
        measured = font.MeasureText(value);
        SKFontMetrics metrics = font.Metrics;
        float baseline = y - (metrics.Ascent + metrics.Descent) * 0.5f;
        canvas.DrawText(value, (canvas.DeviceClipBounds.Width - measured) * 0.5f, baseline, SKTextAlign.Left, font, paint);
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 95);
        return data.ToArray();
    }
}
