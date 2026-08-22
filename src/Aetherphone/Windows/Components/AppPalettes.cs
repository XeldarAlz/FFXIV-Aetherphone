using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal static class AppPalettes
{
    private const float BackdropTopLuminance = 0.016f;
    private const float BackdropBottomLuminance = 0.004f;

    private static readonly Vector4 GlassFill = new(1f, 1f, 1f, 0.05f);
    private static readonly Vector4 GlassStroke = new(1f, 1f, 1f, 0.06f);
    private static readonly Vector4 GlassField = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 DefaultHover = new(1f, 1f, 1f, 0.06f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 MutedBase = new(0.78f, 0.78f, 0.78f, 1f);

    public static AppPalette Tinted(Vector4 accent) => new()
    {
        Accent = accent,
        TitleInk = Palette.Mix(White, accent, 0.05f),
        BodyInk = Palette.WithAlpha(Palette.Mix(White, accent, 0.15f), 0.96f),
        MutedInk = Palette.WithAlpha(Palette.Mix(MutedBase, accent, 0.22f), 0.85f),
        HeaderInk = Palette.WithAlpha(Palette.Lighten(accent, 0.60f), 0.95f),
        HeadingInk = Palette.Mix(White, accent, 0.09f),
        BackdropTop = Palette.ShadeToLuminance(accent, BackdropTopLuminance),
        BackdropBottom = Palette.ShadeToLuminance(accent, BackdropBottomLuminance),
        BloomTop = Palette.WithAlpha(accent, 0.23f),
        BloomBottom = Palette.WithAlpha(Palette.Darken(accent, 0.37f), 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static AppPalette Neutral(Vector4 accent) => new()
    {
        Accent = accent,
        TitleInk = new(0.98f, 0.98f, 0.99f, 1f),
        BodyInk = new(0.90f, 0.90f, 0.92f, 0.96f),
        MutedInk = new(0.62f, 0.62f, 0.66f, 0.85f),
        HeaderInk = new(0.78f, 0.78f, 0.83f, 0.95f),
        HeadingInk = new(0.97f, 0.97f, 0.98f, 1f),
        BackdropTop = new(0.062f, 0.062f, 0.075f, 1f),
        BackdropBottom = new(0.015f, 0.015f, 0.020f, 1f),
        BloomTop = Palette.WithAlpha(accent, 0.10f),
        BloomBottom = Palette.WithAlpha(Palette.Darken(accent, 0.55f), 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    private static AppPalette For(string id) => Tinted(AppAccents.For(id));

    public static readonly AppPalette Health = For("health");
    public static readonly AppPalette Chirper = For("chirper");
    public static readonly AppPalette Market = For("market");
    public static readonly AppPalette Aethergram = For("aethergram");
    public static readonly AppPalette Velvet = For("velvet");
    public static readonly AppPalette Message = For("message");
    public static readonly AppPalette Venues = For("venues");
    public static readonly AppPalette Housing = For("housing");
    public static readonly AppPalette Muster = For("muster");
    public static readonly AppPalette YellowPages = For("yellowpages");
    public static readonly AppPalette Feedback = For("feedback");
    public static readonly AppPalette Polls = For("polls");
    public static readonly AppPalette Announcements = For("announcements");
    public static readonly AppPalette Activity = For("character");
    public static readonly AppPalette Dailies = For("dailies");
    public static readonly AppPalette Collections = For("collections");
    public static readonly AppPalette Coin = For("coin");
    public static readonly AppPalette Wallet = For("wallet");
    public static readonly AppPalette Inventory = For("inventory");
    public static readonly AppPalette AppStore = For("appstore");
    public static readonly AppPalette Photos = For("photos");
    public static readonly AppPalette Shortcuts = For("shortcuts");
    public static readonly AppPalette Timers = For("timers");
    public static readonly AppPalette Fishing = For("fishing");
    public static readonly AppPalette AetherStream = For("aetherstream");
    public static readonly AppPalette Hunts = For("hunts");

    public static readonly AppPalette Casino = new()
    {
        Accent = AccentRing.Emerald,
        TitleInk = new(0.949f, 0.937f, 0.902f, 1f),
        BodyInk = new(0.847f, 0.871f, 0.851f, 0.96f),
        MutedInk = new(0.561f, 0.627f, 0.588f, 0.85f),
        HeaderInk = Palette.WithAlpha(Palette.Lighten(AccentRing.Emerald, 0.60f), 0.95f),
        HeadingInk = new(0.949f, 0.937f, 0.902f, 1f),
        BackdropTop = new(0.043f, 0.082f, 0.071f, 1f),
        BackdropBottom = new(0.020f, 0.031f, 0.027f, 1f),
        BloomTop = Palette.WithAlpha(AccentRing.Emerald, 0.16f),
        BloomBottom = Palette.WithAlpha(Palette.Darken(AccentRing.Emerald, 0.37f), 0.06f),
        CardFill = new(0.063f, 0.102f, 0.086f, 0.92f),
        CardStroke = GlassStroke,
        FieldSurface = new(0.051f, 0.090f, 0.075f, 1f),
        HoverTint = new(1f, 1f, 1f, 0.05f),
    };

    public static readonly AppPalette News = Neutral(AppAccents.For("news"));
    public static readonly AppPalette Music = Neutral(AppAccents.For("music"));
    public static readonly AppPalette Calculator = Neutral(AppAccents.For("calculator"));
    public static readonly AppPalette Clock = Neutral(AppAccents.For("clock"));

    public static AppPalette JobsFor(Vector4 accent) => Tinted(accent);

    public static readonly Vector4 HealthWater = AccentRing.Cyan;
    public static readonly Vector4 HealthEnergy = AccentRing.Gold;
    public static readonly Vector4 HealthTeleport = AccentRing.Violet;

    public static readonly Vector4 HousingParchment = new(0.86f, 0.80f, 0.66f, 1f);
    public static readonly Vector4 HousingBrass = AccentRing.Gold;
    public static readonly Vector4 HousingResults = AccentRing.Orange;
    public static readonly Vector4 HousingClosed = AccentRing.Slate;

    public static AppPalette Notes(PhoneTheme theme) => new()
    {
        Accent = AppAccents.For("notes"),
        TitleInk = theme.TextStrong,
        BodyInk = theme.TextStrong,
        MutedInk = theme.TextMuted,
        HeaderInk = theme.TextMuted,
        HeadingInk = theme.TextStrong,
        BackdropTop = Palette.Mix(theme.AppBackground, theme.GroupedCard, 0.55f),
        BackdropBottom = theme.AppBackground,
        BloomTop = default,
        BloomBottom = default,
        CardFill = theme.GroupedCard,
        CardStroke = theme.Separator,
        FieldSurface = GlassField,
        HoverTint = Palette.WithAlpha(theme.TextStrong, 0.06f),
    };

    public static AppPalette Calendar(PhoneTheme theme) => new()
    {
        Accent = theme.Danger,
        TitleInk = theme.TextStrong,
        BodyInk = theme.TextStrong,
        MutedInk = theme.TextMuted,
        HeaderInk = theme.TextMuted,
        HeadingInk = theme.TextStrong,
        BackdropTop = Palette.Mix(theme.AppBackground, theme.GroupedCard, 0.55f),
        BackdropBottom = theme.AppBackground,
        BloomTop = default,
        BloomBottom = default,
        CardFill = theme.GroupedCard,
        CardStroke = theme.Separator,
        FieldSurface = GlassField,
        HoverTint = Palette.WithAlpha(theme.TextStrong, 0.06f),
    };
}
