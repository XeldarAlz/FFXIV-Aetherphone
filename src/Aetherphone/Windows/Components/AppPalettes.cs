using System.Numerics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal static class AppPalettes
{
    private static readonly Vector4 GlassFill = new(1f, 1f, 1f, 0.05f);
    private static readonly Vector4 GlassStroke = new(1f, 1f, 1f, 0.06f);
    private static readonly Vector4 GlassField = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 DefaultHover = new(1f, 1f, 1f, 0.06f);

    public static readonly AppPalette Chirper = new()
    {
        Accent = AppAccents.For("chirper"),
        TitleInk = new(0.96f, 0.98f, 1f, 1f),
        BodyInk = new(0.85f, 0.90f, 0.97f, 0.96f),
        MutedInk = new(0.64f, 0.73f, 0.85f, 0.85f),
        HeaderInk = new(0.66f, 0.83f, 0.99f, 0.95f),
        HeadingInk = new(0.92f, 0.96f, 1f, 1f),
        BackdropTop = new(0.06f, 0.13f, 0.28f, 1f),
        BackdropBottom = new(0.02f, 0.04f, 0.10f, 1f),
        BloomTop = new(0.16f, 0.52f, 0.92f, 0.24f),
        BloomBottom = new(0.10f, 0.30f, 0.60f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Market = new()
    {
        Accent = AppAccents.For("market"),
        TitleInk = new(0.99f, 0.97f, 0.93f, 1f),
        BodyInk = new(0.95f, 0.90f, 0.84f, 0.96f),
        MutedInk = new(0.78f, 0.72f, 0.62f, 0.85f),
        HeaderInk = new(0.99f, 0.88f, 0.65f, 0.95f),
        HeadingInk = new(0.99f, 0.92f, 0.78f, 1f),
        BackdropTop = new(0.10f, 0.09f, 0.18f, 1f),
        BackdropBottom = new(0.04f, 0.03f, 0.08f, 1f),
        BloomTop = new(0.85f, 0.55f, 0.15f, 0.24f),
        BloomBottom = new(0.40f, 0.25f, 0.08f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Aethergram = new()
    {
        Accent = AppAccents.For("aethergram"),
        TitleInk = new(0.99f, 0.96f, 0.94f, 1f),
        BodyInk = new(0.95f, 0.87f, 0.85f, 0.96f),
        MutedInk = new(0.83f, 0.68f, 0.66f, 0.85f),
        HeaderInk = new(0.99f, 0.76f, 0.62f, 0.95f),
        HeadingInk = new(0.99f, 0.92f, 0.88f, 1f),
        BackdropTop = new(0.29f, 0.09f, 0.14f, 1f),
        BackdropBottom = new(0.08f, 0.03f, 0.04f, 1f),
        BloomTop = new(0.99f, 0.53f, 0.24f, 0.22f),
        BloomBottom = new(0.87f, 0.27f, 0.52f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Velvet = new()
    {
        Accent = AppAccents.For("velvet"),
        TitleInk = new(0.99f, 0.95f, 0.97f, 1f),
        BodyInk = new(0.93f, 0.85f, 0.90f, 0.96f),
        MutedInk = new(0.78f, 0.66f, 0.76f, 0.85f),
        HeaderInk = new(0.99f, 0.72f, 0.82f, 0.95f),
        HeadingInk = new(0.99f, 0.95f, 0.97f, 1f),
        BackdropTop = new(0.34f, 0.06f, 0.19f, 1f),
        BackdropBottom = new(0.05f, 0.02f, 0.09f, 1f),
        BloomTop = new(0.82f, 0.16f, 0.42f, 0.26f),
        BloomBottom = new(0.42f, 0.10f, 0.44f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Phone = new()
    {
        Accent = AppAccents.For("phone"),
        TitleInk = new(0.96f, 0.99f, 0.97f, 1f),
        BodyInk = new(0.86f, 0.94f, 0.89f, 0.96f),
        MutedInk = new(0.64f, 0.80f, 0.70f, 0.85f),
        HeaderInk = new(0.58f, 0.95f, 0.72f, 0.95f),
        HeadingInk = new(0.94f, 0.99f, 0.96f, 1f),
        BackdropTop = new(0.05f, 0.19f, 0.10f, 1f),
        BackdropBottom = new(0.02f, 0.04f, 0.03f, 1f),
        BloomTop = new(0.20f, 0.78f, 0.35f, 0.20f),
        BloomBottom = new(0.10f, 0.40f, 0.20f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Messenger = new()
    {
        Accent = AppAccents.For("dm"),
        TitleInk = new(0.96f, 0.98f, 1f, 1f),
        BodyInk = new(0.86f, 0.90f, 0.97f, 0.96f),
        MutedInk = new(0.64f, 0.71f, 0.82f, 0.85f),
        HeaderInk = new(0.62f, 0.80f, 0.99f, 0.95f),
        HeadingInk = new(0.94f, 0.97f, 1f, 1f),
        BackdropTop = new(0.05f, 0.11f, 0.22f, 1f),
        BackdropBottom = new(0.02f, 0.03f, 0.06f, 1f),
        BloomTop = new(0.20f, 0.48f, 0.92f, 0.22f),
        BloomBottom = new(0.10f, 0.24f, 0.52f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Message = new()
    {
        Accent = AppAccents.For("message"),
        TitleInk = new(1f, 0.98f, 0.95f, 1f),
        BodyInk = new(0.95f, 0.89f, 0.82f, 0.96f),
        MutedInk = new(0.80f, 0.71f, 0.60f, 0.85f),
        HeaderInk = new(0.98f, 0.80f, 0.55f, 0.95f),
        HeadingInk = new(0.99f, 0.95f, 0.89f, 1f),
        BackdropTop = new(0.17f, 0.11f, 0.06f, 1f),
        BackdropBottom = new(0.05f, 0.03f, 0.02f, 1f),
        BloomTop = new(0.78f, 0.52f, 0.24f, 0.22f),
        BloomBottom = new(0.42f, 0.26f, 0.10f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Friends = new()
    {
        Accent = AppAccents.For("friends"),
        TitleInk = new(0.96f, 0.99f, 0.97f, 1f),
        BodyInk = new(0.86f, 0.93f, 0.89f, 0.96f),
        MutedInk = new(0.64f, 0.78f, 0.71f, 0.85f),
        HeaderInk = new(0.62f, 0.94f, 0.80f, 0.95f),
        HeadingInk = new(0.94f, 0.99f, 0.96f, 1f),
        BackdropTop = new(0.05f, 0.18f, 0.14f, 1f),
        BackdropBottom = new(0.02f, 0.05f, 0.05f, 1f),
        BloomTop = new(0.20f, 0.68f, 0.52f, 0.22f),
        BloomBottom = new(0.10f, 0.36f, 0.32f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Venues = new()
    {
        Accent = AppAccents.For("venues"),
        TitleInk = new(0.99f, 0.96f, 0.98f, 1f),
        BodyInk = new(0.96f, 0.92f, 0.97f, 0.98f),
        MutedInk = new(0.83f, 0.76f, 0.87f, 0.94f),
        HeaderInk = new(0.99f, 0.74f, 0.86f, 0.97f),
        HeadingInk = new(0.99f, 0.95f, 0.98f, 1f),
        BackdropTop = new(0.17f, 0.06f, 0.24f, 1f),
        BackdropBottom = new(0.05f, 0.02f, 0.09f, 1f),
        BloomTop = new(0.90f, 0.24f, 0.52f, 0.24f),
        BloomBottom = new(0.36f, 0.14f, 0.52f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Feedback = new()
    {
        Accent = AppAccents.For("feedback"),
        TitleInk = new(0.96f, 0.99f, 0.98f, 1f),
        BodyInk = new(0.85f, 0.93f, 0.91f, 0.96f),
        MutedInk = new(0.64f, 0.78f, 0.74f, 0.85f),
        HeaderInk = new(0.60f, 0.95f, 0.87f, 0.95f),
        HeadingInk = new(0.96f, 0.99f, 0.98f, 1f),
        BackdropTop = new(0.05f, 0.20f, 0.18f, 1f),
        BackdropBottom = new(0.02f, 0.06f, 0.08f, 1f),
        BloomTop = new(0.08f, 0.66f, 0.55f, 0.24f),
        BloomBottom = new(0.06f, 0.35f, 0.40f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette KupoAi = new()
    {
        Accent = AppAccents.For("kupoai"),
        TitleInk = new(0.97f, 0.96f, 1f, 1f),
        BodyInk = new(0.90f, 0.88f, 0.97f, 0.96f),
        MutedInk = new(0.71f, 0.68f, 0.85f, 0.85f),
        HeaderInk = new(0.80f, 0.72f, 1f, 0.95f),
        HeadingInk = new(0.97f, 0.96f, 1f, 1f),
        BackdropTop = new(0.14f, 0.10f, 0.26f, 1f),
        BackdropBottom = new(0.04f, 0.03f, 0.09f, 1f),
        BloomTop = new(0.66f, 0.50f, 0.98f, 0.24f),
        BloomBottom = new(0.30f, 0.20f, 0.60f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Dev = new()
    {
        Accent = AppAccents.For("dev"),
        TitleInk = new(0.96f, 0.97f, 1f, 1f),
        BodyInk = new(0.87f, 0.89f, 0.98f, 0.96f),
        MutedInk = new(0.66f, 0.69f, 0.84f, 0.85f),
        HeaderInk = new(0.74f, 0.78f, 1f, 0.95f),
        HeadingInk = new(0.95f, 0.96f, 1f, 1f),
        BackdropTop = new(0.10f, 0.11f, 0.26f, 1f),
        BackdropBottom = new(0.03f, 0.03f, 0.08f, 1f),
        BloomTop = new(0.42f, 0.46f, 0.98f, 0.22f),
        BloomBottom = new(0.20f, 0.22f, 0.55f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Polls = new()
    {
        Accent = AppAccents.For("polls"),
        TitleInk = new(0.97f, 0.96f, 1f, 1f),
        BodyInk = new(0.89f, 0.87f, 0.97f, 0.96f),
        MutedInk = new(0.71f, 0.68f, 0.85f, 0.85f),
        HeaderInk = new(0.80f, 0.72f, 0.99f, 0.95f),
        HeadingInk = new(0.95f, 0.93f, 1f, 1f),
        BackdropTop = new(0.13f, 0.10f, 0.28f, 1f),
        BackdropBottom = new(0.03f, 0.02f, 0.09f, 1f),
        BloomTop = new(0.56f, 0.44f, 0.96f, 0.22f),
        BloomBottom = new(0.28f, 0.18f, 0.55f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette News = new()
    {
        Accent = new(0.28f, 0.28f, 0.34f, 1f),
        TitleInk = new(0.96f, 0.96f, 0.97f, 1f),
        BodyInk = new(0.85f, 0.85f, 0.88f, 0.96f),
        MutedInk = new(0.60f, 0.60f, 0.66f, 0.85f),
        HeaderInk = new(0.72f, 0.72f, 0.80f, 0.95f),
        HeadingInk = new(0.92f, 0.92f, 0.95f, 1f),
        BackdropTop = new(0.07f, 0.07f, 0.10f, 1f),
        BackdropBottom = new(0.03f, 0.03f, 0.05f, 1f),
        BloomTop = new(0.28f, 0.28f, 0.34f, 0.14f),
        BloomBottom = new(0.10f, 0.10f, 0.14f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Dailies = new()
    {
        Accent = AppAccents.For("dailies"),
        TitleInk = new(0.95f, 0.99f, 0.97f, 1f),
        BodyInk = new(0.88f, 0.96f, 0.92f, 0.96f),
        MutedInk = new(0.68f, 0.82f, 0.76f, 0.82f),
        HeaderInk = new(0.72f, 0.94f, 0.84f, 0.95f),
        HeadingInk = new(0.95f, 0.99f, 0.97f, 1f),
        BackdropTop = new(0.04f, 0.14f, 0.10f, 1f),
        BackdropBottom = new(0.02f, 0.04f, 0.035f, 1f),
        BloomTop = new(0.24f, 0.70f, 0.52f, 0.22f),
        BloomBottom = new(0.12f, 0.42f, 0.32f, 0f),
        CardFill = new(1f, 1f, 1f, 0.05f),
        CardStroke = new(1f, 1f, 1f, 0.07f),
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Timers = new()
    {
        Accent = AppAccents.For("timers"),
        TitleInk = new(0.99f, 0.97f, 0.94f, 1f),
        BodyInk = new(0.94f, 0.90f, 0.85f, 0.96f),
        MutedInk = new(0.78f, 0.72f, 0.64f, 0.82f),
        HeaderInk = new(0.96f, 0.80f, 0.52f, 0.95f),
        HeadingInk = new(0.99f, 0.97f, 0.94f, 1f),
        BackdropTop = new(0.15f, 0.09f, 0.02f, 1f),
        BackdropBottom = new(0.03f, 0.02f, 0.015f, 1f),
        BloomTop = new(0.92f, 0.54f, 0.10f, 0.20f),
        BloomBottom = new(0.50f, 0.28f, 0.05f, 0f),
        CardFill = new(1f, 1f, 1f, 0.05f),
        CardStroke = new(1f, 1f, 1f, 0.07f),
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Calculator = new()
    {
        Accent = new(1.00f, 0.62f, 0.10f, 1f),
        TitleInk = new(0.98f, 0.98f, 0.99f, 1f),
        BodyInk = new(0.90f, 0.90f, 0.92f, 0.96f),
        MutedInk = new(0.62f, 0.62f, 0.66f, 0.85f),
        HeaderInk = new(0.80f, 0.80f, 0.84f, 0.95f),
        HeadingInk = new(0.98f, 0.98f, 0.99f, 1f),
        BackdropTop = new(0.05f, 0.05f, 0.06f, 1f),
        BackdropBottom = new(0.01f, 0.01f, 0.015f, 1f),
        BloomTop = new(0.30f, 0.20f, 0.05f, 0.10f),
        BloomBottom = new(0.10f, 0.07f, 0.02f, 0f),
        CardFill = GlassFill,
        CardStroke = GlassStroke,
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

    public static readonly AppPalette Clock = new()
    {
        Accent = new(1.00f, 0.58f, 0.00f, 1f),
        TitleInk = new(0.98f, 0.98f, 0.99f, 1f),
        BodyInk = new(0.90f, 0.90f, 0.93f, 0.96f),
        MutedInk = new(0.60f, 0.61f, 0.66f, 0.85f),
        HeaderInk = new(0.72f, 0.73f, 0.80f, 0.95f),
        HeadingInk = new(0.96f, 0.96f, 0.98f, 1f),
        BackdropTop = new(0.06f, 0.06f, 0.08f, 1f),
        BackdropBottom = new(0.01f, 0.01f, 0.02f, 1f),
        BloomTop = new(0.30f, 0.22f, 0.10f, 0.10f),
        BloomBottom = new(0.10f, 0.08f, 0.04f, 0f),
        CardFill = new(1f, 1f, 1f, 0.06f),
        CardStroke = new(1f, 1f, 1f, 0.08f),
        FieldSurface = GlassField,
        HoverTint = DefaultHover,
    };

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
