using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Strip;

internal readonly record struct LiveRoomView(string GameId, TileLabelFit Name, string Line, int Crowd, bool Live,
    float Countdown);

internal static class FloorRoomCard
{
    public const float MinHeight = 108f;
    public const float Pad = 10f;
    public const float Tile = 32f;
    public const float TileGap = 8f;
    public const float LineGap = 2f;
    public const float BarGap = 6f;
    public const float BarHeight = 3f;
    public const int NameLines = 2;

    private const float WashShare = 0.30f;
    private const float RimAlpha = 0.45f;
    private const float LineLift = 0.5f;
    private const float TrackAlpha = 0.10f;

    private static readonly Vector4 Night = new(0.071f, 0.051f, 0.102f, 1f);

    public static float Height(float nameBlock, float lineBlock, float scale) =>
        MathF.Max(MinHeight * scale,
            (Pad * 2f + Tile + TileGap + LineGap + BarGap + BarHeight) * scale + nameBlock + lineBlock);

    public static float TextWidth(float cardWidth, float scale) => MathF.Max(1f, cardWidth - Pad * 2f * scale);

    public static float LineBlock(int lines, float lineHeight) => Math.Max(1, lines) * lineHeight;

    public static bool Draw(ImDrawListPtr drawList, AppSkin ui, in LiveRoomView view, Rect card, float scale,
        out Rect pressed)
    {
        var hovered = CasinoArt.PressCard(ImGui.GetID("room"), card.Min, card.Max, out var min, out var max);
        pressed = new Rect(min, max);
        var tint = CasinoArt.TintOf(view.GameId);
        var radius = Metrics.Radius.Card * scale;
        Squircle.FillVerticalGradient(drawList, min, max, radius, ImGui.GetColorU32(Palette.Mix(Night, tint, WashShare)),
            ImGui.GetColorU32(Night));
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(tint with { W = hovered ? 0.8f : RimAlpha }),
            MathF.Max(1f, scale));
        var pad = Pad * scale;
        var tile = Tile * scale;
        var tileCenter = new Vector2(min.X + pad + tile * 0.5f, min.Y + pad + tile * 0.5f);
        CasinoArt.GameTile(drawList, view.GameId, tileCenter, tile);
        if (view.Live)
        {
            DrawCrowd(drawList, view.Crowd, new Vector2(max.X - pad, tileCenter.Y), tileCenter.X + tile * 0.5f, scale);
        }

        var left = min.X + pad;
        var top = tileCenter.Y + tile * 0.5f + TileGap * scale;
        var nameHeight = Typography.LineHeight(view.Name.Style);
        for (var line = 0; line < view.Name.Lines.Length; line++)
        {
            Typography.Draw(drawList, new Vector2(left, top), view.Name.Lines[line], ui.TitleInk, view.Name.Style);
            top += nameHeight;
        }

        var barTop = max.Y - pad - BarHeight * scale;
        var lineInk = Palette.Mix(tint, CasinoArt.White, LineLift);
        var lines = Typography.WrapText(view.Line, TextStyles.Footnote, TextWidth(card.Width, scale));
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var lineTop = barTop - BarGap * scale - lines.Length * lineHeight;
        for (var line = 0; line < lines.Length; line++)
        {
            Typography.Draw(drawList, new Vector2(left, lineTop), lines[line], view.Live ? lineInk : ui.BodyInk,
                TextStyles.Footnote);
            lineTop += lineHeight;
        }

        var barMin = new Vector2(left, barTop);
        var barMax = new Vector2(max.X - pad, barTop + BarHeight * scale);
        var barRadius = BarHeight * 0.5f * scale;
        Squircle.Fill(drawList, barMin, barMax, barRadius, ImGui.GetColorU32(CasinoArt.White with { W = TrackAlpha }));
        if (view.Live && view.Countdown > 0f)
        {
            Squircle.Fill(drawList, barMin, new Vector2(barMin.X + (barMax.X - barMin.X) * view.Countdown, barMax.Y),
                barRadius, ImGui.GetColorU32(lineInk));
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private static void DrawCrowd(ImDrawListPtr drawList, int crowd, Vector2 rightCenter, float floor, float scale)
    {
        var label = Games.Framework.GameNumber.Label(Math.Max(0, crowd));
        var style = TextStyles.FootnoteEmphasized;
        var size = Typography.Measure(label, style);
        var dot = CasinoArt.LiveDotRadius * scale;
        var textLeft = MathF.Max(floor + dot * 3f, rightCenter.X - size.X);
        var dotCenter = new Vector2(textLeft - Metrics.Space.Xs * scale * 0.8f - dot, rightCenter.Y);
        CasinoArt.LiveDot(drawList, dotCenter, scale, CasinoColors.LightA, true);
        Typography.Draw(drawList, new Vector2(textLeft, rightCenter.Y - size.Y * 0.5f), label, CasinoColors.InkTitle,
            style);
    }
}
