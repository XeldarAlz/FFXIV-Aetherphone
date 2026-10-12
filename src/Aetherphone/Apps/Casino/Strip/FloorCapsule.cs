using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Strip;

internal readonly record struct FloorCapsuleContent(string Id, FontAwesomeIcon Icon, Vector4 Tint, string Title,
    float Fraction, string Line, Vector4 LineInk);

internal static class FloorCapsule
{
    public const float Height = 64f;
    public const float Pad = 14f;
    public const float Tile = 36f;
    public const float BarHeight = 5f;
    public const float HalfPad = 12f;
    public const float HalfTile = 28f;

    private const float SurfaceMix = 0.16f;
    private const float StrokeAlpha = 0.55f;
    private const float TrackAlpha = 0.25f;
    private const float ChevronInset = 4f;
    private const float FitSlack = 0.5f;

    private static readonly TextStyle HalfTitleStyle = TextStyles.SubheadlineEmphasized;

    public static float HalfTextWidth(float width, float scale) =>
        MathF.Max(1f, width - (HalfPad * 2f + HalfTile + Metrics.Space.Sm) * scale);

    public static bool FitsHalf(in FloorCapsuleContent content, float width, float scale)
    {
        var textWidth = HalfTextWidth(width, scale) + FitSlack;
        return Typography.Measure(content.Title, HalfTitleStyle).X <= textWidth
               && (content.Line.Length == 0 || Typography.Measure(content.Line, TextStyles.Footnote).X <= textWidth);
    }

    public static float HalfHeight(in FloorCapsuleContent content, float scale) =>
        MathF.Max(Height * scale, HalfBlock(content, scale) + Metrics.Space.Sm * 2f * scale);

    public static bool Draw(ImDrawListPtr drawList, AppSkin ui, in FloorCapsuleContent content, Vector2 origin,
        float width, float scale, out float bottom)
    {
        var pad = Pad * scale;
        var tile = Tile * scale;
        var textWidth = MathF.Max(1f, width - pad * 2f - ChevronInset * scale - CoinArt.ValueGap * scale - tile
                                      - CoinArt.TextGap * scale);
        var bar = BarHeight * scale;
        var block = Tables.PitText.Height(content.Title, TextStyles.Headline, textWidth) + bar
                    + Metrics.Space.Xs * scale + Tables.PitText.Height(content.Line, TextStyles.Footnote, textWidth);
        var max = new Vector2(origin.X + width, origin.Y + MathF.Max(Height * scale, block + Metrics.Space.Sm * 2f * scale));
        bottom = max.Y;
        var hovered = Shell(drawList, ui, content, origin, max, scale, out var min, out var shown);
        var tileCenter = new Vector2(min.X + pad + tile * 0.5f, (min.Y + shown.Y) * 0.5f);
        CasinoArt.IconTileAt(drawList, tileCenter, tile, content.Tint, content.Icon);
        CasinoArt.Chevron(drawList, new Vector2(shown.X - pad - ChevronInset * scale, tileCenter.Y), ui.BodyInk);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var top = Tables.PitText.Draw(drawList, content.Title, TextStyles.Headline, ui.TitleInk, textLeft,
            tileCenter.Y - block * 0.5f, textWidth, false);
        CoinArt.Bar(drawList, new Vector2(textLeft, top), new Vector2(textLeft + textWidth, top + bar), content.Fraction,
            Palette.WithAlpha(ui.MutedInk, TrackAlpha), content.Tint);
        Tables.PitText.Draw(drawList, content.Line, TextStyles.Footnote, content.LineInk, textLeft,
            top + bar + Metrics.Space.Xs * scale, textWidth, false);
        return UiInteract.Click(origin, max, hovered);
    }

    public static bool DrawHalf(ImDrawListPtr drawList, AppSkin ui, in FloorCapsuleContent content, Vector2 origin,
        float width, float height, float scale)
    {
        var pad = HalfPad * scale;
        var tile = HalfTile * scale;
        var textWidth = HalfTextWidth(width, scale);
        var bar = BarHeight * scale;
        var block = HalfBlock(content, scale);
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = Shell(drawList, ui, content, origin, max, scale, out var min, out var shown);
        var tileCenter = new Vector2(min.X + pad + tile * 0.5f, (min.Y + shown.Y) * 0.5f);
        CasinoArt.IconTileAt(drawList, tileCenter, tile, content.Tint, content.Icon);
        var textLeft = tileCenter.X + tile * 0.5f + Metrics.Space.Sm * scale;
        var top = tileCenter.Y - block * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), content.Title, ui.TitleInk, HalfTitleStyle);
        top += Typography.LineHeight(HalfTitleStyle);
        CoinArt.Bar(drawList, new Vector2(textLeft, top), new Vector2(textLeft + textWidth, top + bar), content.Fraction,
            Palette.WithAlpha(ui.MutedInk, TrackAlpha), content.Tint);
        if (content.Line.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top + bar + Metrics.Space.Xs * scale), content.Line,
                content.LineInk, TextStyles.Footnote);
        }

        return UiInteract.Click(origin, max, hovered);
    }

    private static float HalfBlock(in FloorCapsuleContent content, float scale) =>
        Typography.LineHeight(HalfTitleStyle) + BarHeight * scale
        + (content.Line.Length > 0 ? Metrics.Space.Xs * scale + Typography.LineHeight(TextStyles.Footnote) : 0f);

    private static bool Shell(ImDrawListPtr drawList, AppSkin ui, in FloorCapsuleContent content, Vector2 origin,
        Vector2 max, float scale, out Vector2 min, out Vector2 shown)
    {
        var hovered = CasinoArt.PressCard(ImGui.GetID(content.Id), origin, max, out min, out shown);
        var radius = Height * scale * 0.5f;
        Squircle.Fill(drawList, min, shown, radius,
            ImGui.GetColorU32(Palette.Mix(ui.Palette.BackdropTop, content.Tint, SurfaceMix)));
        Squircle.Stroke(drawList, min, shown, radius, ImGui.GetColorU32(content.Tint with { W = StrokeAlpha }),
            MathF.Max(1f, scale));
        return hovered;
    }
}
