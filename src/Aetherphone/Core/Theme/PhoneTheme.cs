using System.Numerics;

namespace Aetherphone.Core.Theme;

internal sealed class PhoneTheme
{
    public required Vector4 BezelOuter { get; init; }

    public required Vector4 BezelRim { get; init; }

    public required Vector4 ScreenBase { get; init; }

    public required string LightWallpaperId { get; init; }

    public required string DarkWallpaperId { get; init; }

    public required Vector4 AppBackground { get; init; }

    public required Vector4 GroupedCard { get; init; }

    public required Vector4 Separator { get; init; }

    public required Vector4 ToggleOn { get; init; }

    public required Vector4 ToggleOff { get; init; }

    public required Vector4 Surface { get; init; }

    public required Vector4 SurfaceMuted { get; init; }

    public required Vector4 TextStrong { get; init; }

    public required Vector4 TextMuted { get; init; }

    public required Vector4 Accent { get; init; }

    public required Vector4 Danger { get; init; }

    public required float DeviceRounding { get; init; }

    public required float BezelThickness { get; init; }

    public required float ScreenRounding { get; init; }

    public required float TopZoneHeight { get; init; }

    public required float BottomZoneHeight { get; init; }

    public required float SidePadding { get; init; }

    public static PhoneTheme From(Vector4 accent, string lightWallpaperId, string darkWallpaperId) => new()
    {
        BezelOuter = new Vector4(0.03f, 0.03f, 0.04f, 1f),
        BezelRim = new Vector4(0.22f, 0.22f, 0.26f, 1f),
        ScreenBase = new Vector4(0.06f, 0.06f, 0.10f, 1f),
        LightWallpaperId = lightWallpaperId,
        DarkWallpaperId = darkWallpaperId,
        AppBackground = new Vector4(0.055f, 0.055f, 0.075f, 1f),
        GroupedCard = new Vector4(0.110f, 0.110f, 0.125f, 1f),
        Separator = new Vector4(0.34f, 0.34f, 0.37f, 0.5f),
        ToggleOn = new Vector4(0.204f, 0.780f, 0.349f, 1f),
        ToggleOff = new Vector4(0.24f, 0.24f, 0.26f, 1f),
        Surface = new Vector4(0.13f, 0.13f, 0.18f, 0.92f),
        SurfaceMuted = new Vector4(0.22f, 0.22f, 0.28f, 0.65f),
        TextStrong = new Vector4(0.97f, 0.97f, 0.98f, 1f),
        TextMuted = new Vector4(0.56f, 0.56f, 0.60f, 1f),
        Accent = accent,
        Danger = new Vector4(0.92f, 0.30f, 0.34f, 1f),
        DeviceRounding = 46f,
        BezelThickness = 9f,
        ScreenRounding = 38f,
        TopZoneHeight = 48f,
        BottomZoneHeight = 30f,
        SidePadding = 16f,
    };

    public static PhoneTheme Default { get; } = From(new Vector4(0.55f, 0.45f, 0.95f, 1f), "DuskLight", "DuskDark");
}
