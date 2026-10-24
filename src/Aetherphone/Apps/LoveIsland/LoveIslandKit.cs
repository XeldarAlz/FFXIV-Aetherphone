using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal static class LoveIslandKit
{
    public const float PadX = FeedCell.PadX;
    public const float CardPad = 14f;
    public const float CardGap = 10f;
    public const float CardRadius = Metrics.Radius.Grouped;
    public const float EyebrowGap = 6f;
    public const float TitleGap = 4f;
    public const float ButtonRowGap = 12f;
    public const float ButtonGap = 8f;
    public const float ButtonHeight = Button.RegularHeight;
    public const float HeroAspect = 0.56f;
    public const float HeroMaxHeight = 240f;
    public const float ThumbOversample = 2f;
    public const string Separator = " · ";
    public const string TwitchHostPrefix = "https://twitch.tv/";

    public static readonly TextStyle EyebrowStyle = TextStyles.FootnoteEmphasized;
    public static readonly TextStyle CardTitleStyle = TextStyles.Headline;
    public static readonly TextStyle CardBodyStyle = TextStyles.Subheadline;
    public static readonly TextStyle PageTitleStyle = TextStyles.LargeTitle;
    public static readonly TextStyle PageSubStyle = TextStyles.Subheadline;
    public static readonly Vector4 White = MediaOverlay.White;
    public static readonly Vector4 OffPillFill = new(1f, 1f, 1f, 0.08f);
    public static readonly Vector4 OffPillInk = new(1f, 1f, 1f, 0.72f);
    public static readonly Vector4 OffPillStroke = new(1f, 1f, 1f, 0.14f);
    public static readonly Vector4 Night = new(0.020f, 0.005f, 0.012f, 1f);
    public static readonly TextStyle PillStyle = new(0.72f, FontWeight.SemiBold);

    private const float GlassTopShade = 0.80f;
    private const float GlassBottomShade = 0.92f;
    private const float GlassAlpha = 0.50f;
    private const float GlassBottomExtra = 0.12f;
    private const float GlassHoverAlpha = 0.10f;
    private const float GlassStrokeAlpha = 0.14f;
    private const float GlassEdgeAlpha = 0.16f;
    private const float FeaturedTopShade = 0.58f;
    private const float FeaturedAlpha = 0.70f;
    private const float FeaturedBloomReach = 16f;
    private const float FeaturedBloomAlpha = 0.22f;
    private const double FeaturedBreath = 3200.0;
    private const float PillDotSpace = 13f;
    private const float PillBloomReach = 6f;
    private const float CtaGlintPeak = 0.32f;

    private static readonly Vector2 Upward = new(0f, -1f);

    public static bool PressableCard(ImDrawListPtr drawList, AppSkin ui, Rect rest, string id, float scale,
        bool interactive, out Rect card, bool featured = false)
    {
        var hovered = interactive && UiInteract.Hover(rest.Min, rest.Max);
        var eased = HoverFx.Amount(id, hovered);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id, pressed, PressFx.CardPressedScale);
        var factor = (1f + Motion.HoverLiftCard * eased) * press;
        var half = rest.Size * 0.5f * factor;
        card = new Rect(rest.Center - half, rest.Center + half);
        var rounding = CardRadius * scale * factor;
        if (featured)
        {
            FeaturedCard(drawList, card.Min, card.Max, rounding, ui.Accent, scale, eased);
        }
        else
        {
            GlassCard(drawList, card.Min, card.Max, rounding, ui.Accent, scale, eased);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rest.Min, rest.Max, hovered);
    }

    public static void GlassCard(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 accent,
        float scale, float hover = 0f)
    {
        var top = Palette.Mix(accent, Night, GlassTopShade) with { W = GlassAlpha + GlassHoverAlpha * hover };
        var bottom = Palette.Mix(accent, Night, GlassBottomShade) with { W = GlassAlpha + GlassBottomExtra };
        Squircle.FillVerticalGradient(drawList, min, max, rounding, ImGui.GetColorU32(top), ImGui.GetColorU32(bottom));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(EclipseFx.Rose(accent, 0.35f) with { W = GlassStrokeAlpha + 0.12f * hover }), 1f);
        Squircle.StrokeDirectional(drawList, min, max, rounding,
            ImGui.GetColorU32(White with { W = GlassEdgeAlpha + 0.10f * hover }), 1f * scale, Upward, 2.5f);
    }

    public static void FeaturedCard(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 accent,
        float scale, float hover = 0f)
    {
        var breath = EclipseFx.Breath(FeaturedBreath);
        EclipseFx.Bloom(drawList, min, max, rounding, FeaturedBloomReach * scale,
            accent with { W = FeaturedBloomAlpha + 0.14f * breath + 0.10f * hover });
        var top = Palette.Mix(accent, Night, FeaturedTopShade) with { W = FeaturedAlpha };
        var bottom = Palette.Mix(accent, Night, GlassBottomShade) with { W = FeaturedAlpha };
        Squircle.FillVerticalGradient(drawList, min, max, rounding, ImGui.GetColorU32(top), ImGui.GetColorU32(bottom));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(EclipseFx.Rose(accent, 0.30f) with { W = 0.30f + 0.10f * hover }), 1f);
        Squircle.StrokeDirectional(drawList, min, max, rounding, ImGui.GetColorU32(White with { W = 0.28f }),
            1f * scale, Upward, 2.5f);
        EclipseFx.OrbitBorder(drawList, min, max, rounding, EclipseFx.Rose(accent, 0.55f) with { W = 0.95f }, scale);
    }

    public static float Eyebrow(ImDrawListPtr drawList, Vector2 position, string text, Vector4 ink, float maxWidth)
    {
        Typography.Draw(drawList, position, Typography.FitText(text, maxWidth, EyebrowStyle), ink, EyebrowStyle);
        return Typography.LineHeight(EyebrowStyle);
    }

    public static float StagePill(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 accent, float scale) =>
        MediaOverlay.Pill(drawList, topLeft, label, Palette.WithAlpha(accent, 0.22f), Palette.Lighten(accent, 0.42f),
            scale, stroke: Palette.WithAlpha(Palette.Lighten(accent, 0.2f), 0.40f));

    public static float OffPill(ImDrawListPtr drawList, Vector2 topLeft, string label, float scale) =>
        MediaOverlay.Pill(drawList, topLeft, label, OffPillFill, OffPillInk, scale, stroke: OffPillStroke);

    public static float LivePillWidth(string label, float scale) =>
        Typography.Measure(label, PillStyle).X + MediaOverlay.PillPadX * 2f * scale + PillDotSpace * scale;

    public static float LivePill(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 accent, float scale)
    {
        var size = Typography.Measure(label, PillStyle);
        var height = MediaOverlay.PillHeight * scale;
        var width = LivePillWidth(label, scale);
        var max = topLeft + new Vector2(width, height);
        var rounding = height * 0.5f;
        var padX = MediaOverlay.PillPadX * scale;
        EclipseFx.Bloom(drawList, topLeft, max, rounding, PillBloomReach * scale,
            accent with { W = 0.30f + 0.20f * EclipseFx.Breath(EclipseFx.BeaconPeriod) });
        Squircle.FillHorizontalGradient(drawList, topLeft, max, rounding,
            ImGui.GetColorU32(Palette.Lighten(accent, 0.12f)), ImGui.GetColorU32(Palette.Darken(accent, 0.18f)));
        Squircle.StrokeDirectional(drawList, topLeft, max, rounding, ImGui.GetColorU32(White with { W = 0.40f }),
            1f * scale, Upward, 2f);
        EclipseFx.Beacon(drawList, new Vector2(topLeft.X + padX + 2f * scale, topLeft.Y + height * 0.5f),
            2.8f * scale, White, scale);
        Typography.Draw(drawList, new Vector2(topLeft.X + padX + PillDotSpace * scale,
            topLeft.Y + (height - size.Y) * 0.5f), label, White, PillStyle);
        return width;
    }

    public static bool CtaButton(ImDrawListPtr drawList, Rect rect, string label, in ControlInk ink, bool prominent,
        string id)
    {
        var clicked = Button.Draw(drawList, rect, label, ink, prominent ? ButtonStyle.Prominent : ButtonStyle.Gray,
            id: id);
        if (prominent)
        {
            EclipseFx.Glint(drawList, rect.Min, rect.Max, rect.Height * 0.5f, CtaGlintPeak, GlintOffset(id));
        }

        return clicked;
    }

    public static float GlintOffset(string id)
    {
        var hash = 17u;
        for (var index = 0; index < id.Length; index++)
        {
            hash = hash * 31u + id[index];
        }

        return hash % 97u / 97f;
    }

    public static float PillHeight(float scale) => MediaOverlay.PillHeight * scale;

    public static void CoverImage(ImDrawListPtr drawList, RemoteImageCache images, string? url, Rect frame,
        float rounding, Vector4 accent)
    {
        var texture = string.IsNullOrEmpty(url) ? null : images.Sized(url, MathF.Max(frame.Width, frame.Height) * ThumbOversample);
        if (texture is null)
        {
            Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(accent, 0.05f), 0.65f)),
                ImGui.GetColorU32(Palette.WithAlpha(Palette.Darken(accent, 0.55f), 0.85f)));
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, frame.Width, frame.Height);
        if (rounding <= 0f)
        {
            drawList.AddImage(texture.Handle, frame.Min, frame.Max, uv0, uv1, 0xFFFFFFFFu);
            return;
        }

        Squircle.FillImage(drawList, frame.Min, frame.Max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
    }

    public static bool Portrait(ImDrawListPtr drawList, RemoteImageCache images, string? url, Vector2 center,
        float radius, Vector4 accent, string monogram, Vector4 ink)
    {
        var texture = string.IsNullOrEmpty(url) ? null : images.Sized(url, radius * 2f * ThumbOversample);
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        if (texture is null)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.28f)), 48);
            Typography.DrawCentered(drawList, center, monogram, ink, TextStyles.SubheadlineEmphasized);
            return false;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        Squircle.FillImage(drawList, min, max, radius, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        return true;
    }

    public static string LinkLabel(string kind) => kind switch
    {
        "website" => Loc.T(L.Venues.Website),
        "discord" => Loc.T(L.Venues.Discord),
        "twitch" => Loc.T(L.LoveIsland.LinkTwitch),
        "youtube" => Loc.T(L.LoveIsland.LinkYouTube),
        "bluesky" => Loc.T(L.LoveIsland.LinkBluesky),
        "x" => Loc.T(L.LoveIsland.LinkX),
        "kofi" => Loc.T(L.LoveIsland.LinkKofi),
        "patreon" => Loc.T(L.LoveIsland.LinkPatreon),
        "partner" => Loc.T(L.LoveIsland.LinkPartner),
        _ => kind,
    };

    public static bool TryParseHex(string? hex, out Vector4 color)
    {
        color = default;
        if (string.IsNullOrEmpty(hex))
        {
            return false;
        }

        var span = hex.AsSpan();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span.Slice(1);
        }

        if (span.Length != 6 || !int.TryParse(span, System.Globalization.NumberStyles.HexNumber, null, out var packed))
        {
            return false;
        }

        color = new Vector4(((packed >> 16) & 0xFF) / 255f, ((packed >> 8) & 0xFF) / 255f, (packed & 0xFF) / 255f, 1f);
        return true;
    }

    public static string TwitchChannelName(string channel)
    {
        const string host = "twitch.tv/";
        var span = channel.AsSpan().Trim();
        var hostIndex = span.IndexOf(host, StringComparison.OrdinalIgnoreCase);
        if (hostIndex >= 0)
        {
            span = span.Slice(hostIndex + host.Length);
        }

        var end = span.IndexOfAny('/', '?', '#');
        if (end >= 0)
        {
            span = span.Slice(0, end);
        }

        span = span.TrimStart('@');
        return span.Length == channel.Length ? channel : span.ToString();
    }

    public static string StripThe(string district)
    {
        const string prefix = "The ";
        return district.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? district.Substring(prefix.Length) : district;
    }
}

internal sealed class ClampedLines
{
    private const string Ellipsis = "…";

    private string source = string.Empty;
    private float width = -1f;
    private float fontKey = -1f;
    private int lineLimit = -1;
    private string[] lines = Array.Empty<string>();

    public string[] Get(string text, in TextStyle style, float maxWidth, int maxLines, float currentFontKey)
    {
        if (ReferenceEquals(text, source) && width == maxWidth && fontKey == currentFontKey && lineLimit == maxLines)
        {
            return lines;
        }

        source = text;
        width = maxWidth;
        fontKey = currentFontKey;
        lineLimit = maxLines;
        lines = Clamp(text, style, maxWidth, maxLines);
        return lines;
    }

    private static string[] Clamp(string text, in TextStyle style, float maxWidth, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0f || maxLines <= 0)
        {
            return Array.Empty<string>();
        }

        var wrapped = Typography.WrapText(text, style, maxWidth);
        if (wrapped.Length <= maxLines)
        {
            return wrapped;
        }

        var trimmed = new string[maxLines];
        Array.Copy(wrapped, trimmed, maxLines);
        trimmed[maxLines - 1] = Typography.FitText(trimmed[maxLines - 1] + Ellipsis, maxWidth, style);
        return trimmed;
    }
}
