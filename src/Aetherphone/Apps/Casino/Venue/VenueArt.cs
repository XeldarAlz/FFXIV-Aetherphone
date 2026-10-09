using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal static class VenueArt
{
    public const float SignHeight = 30f;
    public const float SignGap = 8f;
    public const float TileRounding = 26f;
    public const float RowHeight = 30f;
    public const float RowPad = 12f;
    public const float LineGap = 4f;
    private const string CaptionMarquee = "venue.deck.caption";

    public static float DrawSign(ImDrawListPtr drawList, CasinoSign sign, Rect area, float phase, float scale)
    {
        var height = CasinoSigns.HeightToFit(sign, area.Width * 0.8f, SignHeight * scale);
        var center = new Vector2(area.Center.X, area.Min.Y + height * 0.5f + 2f * scale);
        var flicker = 0.82f + 0.18f * MathF.Sin(phase * 3.1f);
        CasinoSigns.Draw(drawList, sign, center, height, CasinoColors.LightA, flicker);
        return area.Min.Y + height + SignGap * scale;
    }

    public static void DrawNumberTile(ImDrawListPtr drawList, Rect tile, string number, bool settled, bool losing,
        float glow, float phase, float scale)
    {
        var rounding = TileRounding * scale;
        var top = settled && !losing ? Palette.Mix(CasinoColors.FeltTop, CasinoColors.Money, 0.10f * glow)
            : CasinoColors.FeltTop;
        Squircle.FillVerticalGradient(drawList, tile.Min, tile.Max, rounding,
            ImGui.GetColorU32(top with { W = 0.92f }), ImGui.GetColorU32(CasinoColors.FeltBottom with { W = 0.92f }));
        if (settled && !losing && glow > 0f)
        {
            CasinoLights.BulbChase(drawList, tile, rounding, scale, phase, CasinoLights.BulbPitch, CasinoColors.Money,
                CasinoColors.LightA, glow);
        }
        else
        {
            Squircle.Stroke(drawList, tile.Min, tile.Max, rounding,
                ImGui.GetColorU32(CasinoColors.LightB with { W = 0.35f }), 1.5f * scale);
        }

        var style = TextStyles.LargeTitle;
        var fitted = Typography.FitScale(number, tile.Width * 0.82f, style.Scale, TextStyles.Footnote.Scale,
            style.Weight);
        var size = Typography.Measure(number, fitted, style.Weight);
        var ink = !settled ? CasinoColors.InkMuted : losing ? CasinoColors.Loss : CasinoColors.InkTitle;
        Typography.Draw(drawList, tile.Center - size * 0.5f, number, ink, fitted, style.Weight);
    }

    public static void Line(ImDrawListPtr drawList, string text, Vector2 center, float width, Vector4 ink,
        in TextStyle style)
    {
        var resolved = StageText.Resolve(StageTextRole.Status, style);
        var fitted = Typography.FitText(text, width, resolved);
        var shadow = MathF.Max(1f, StageText.ShadowOffset * UiScale.Current);
        Typography.DrawCentered(drawList, center + new Vector2(0f, shadow), fitted,
            StageText.Shadow with { W = StageText.Shadow.W * ink.W }, resolved);
        Typography.DrawCentered(drawList, center, fitted, ink, resolved);
    }

    public static void LeftLine(ImDrawListPtr drawList, string text, Vector2 topLeft, float width, Vector4 ink,
        in TextStyle style)
    {
        Typography.Draw(drawList, topLeft, Typography.FitText(text, width, style), ink, style);
    }

    public static float DeckCaption(ImDrawListPtr drawList, Rect deck, string text, float scale)
    {
        var row = DeckActions.Row(deck, scale);
        var lineHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var above = DeckActions.Above(row, lineHeight, scale);
        StageText.Label(drawList, above.Center, text, above.Width, CaptionMarquee, TextStyles.SubheadlineEmphasized,
            false);
        return above.Max.Y;
    }
}
