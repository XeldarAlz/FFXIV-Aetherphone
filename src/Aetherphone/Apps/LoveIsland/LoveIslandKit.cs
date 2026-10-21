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

    public static readonly TextStyle EyebrowStyle = TextStyles.FootnoteEmphasized;
    public static readonly TextStyle CardTitleStyle = TextStyles.Headline;
    public static readonly TextStyle CardBodyStyle = TextStyles.Subheadline;
    public static readonly TextStyle PageTitleStyle = TextStyles.LargeTitle;
    public static readonly TextStyle PageSubStyle = TextStyles.Subheadline;
    public static readonly Vector4 White = MediaOverlay.White;
    public static readonly Vector4 OffPillFill = new(1f, 1f, 1f, 0.10f);
    public static readonly Vector4 OffPillInk = new(1f, 1f, 1f, 0.72f);

    public static bool PressableCard(ImDrawListPtr drawList, AppSkin ui, Rect rest, string id, float scale,
        bool interactive, out Rect card)
    {
        var hovered = interactive && UiInteract.Hover(rest.Min, rest.Max);
        var eased = HoverFx.Amount(id, hovered);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id, pressed, PressFx.CardPressedScale);
        var factor = (1f + Motion.HoverLiftCard * eased) * press;
        var half = rest.Size * 0.5f * factor;
        card = new Rect(rest.Center - half, rest.Center + half);
        ui.Card(drawList, card.Min, card.Max, CardRadius * scale * factor);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rest.Min, rest.Max, hovered);
    }

    public static float Eyebrow(ImDrawListPtr drawList, Vector2 position, string text, Vector4 ink, float maxWidth)
    {
        Typography.Draw(drawList, position, Typography.FitText(text, maxWidth, EyebrowStyle), ink, EyebrowStyle);
        return Typography.LineHeight(EyebrowStyle);
    }

    public static float StagePill(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 accent, float scale) =>
        MediaOverlay.Pill(drawList, topLeft, label, Palette.WithAlpha(accent, 0.22f), Palette.Lighten(accent, 0.35f),
            scale);

    public static float OffPill(ImDrawListPtr drawList, Vector2 topLeft, string label, float scale) =>
        MediaOverlay.Pill(drawList, topLeft, label, OffPillFill, OffPillInk, scale);

    public static float LivePill(ImDrawListPtr drawList, Vector2 topLeft, string label, float scale) =>
        MediaOverlay.LivePill(drawList, topLeft, label, scale);

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
