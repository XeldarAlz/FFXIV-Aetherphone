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
    public const float DeckPad = 16f;
    public const float ButtonGap = 8f;

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
        var fitted = FitScale(number, tile.Width * 0.82f, style);
        var size = Typography.Measure(number, fitted, style.Weight);
        var ink = !settled ? CasinoColors.InkMuted : losing ? CasinoColors.Loss : CasinoColors.InkTitle;
        Typography.Draw(drawList, tile.Center - size * 0.5f, number, ink, fitted, style.Weight);
    }

    public static float FitScale(string text, float width, in TextStyle style)
    {
        var size = Typography.Measure(text, style);
        return size.X <= width || size.X <= 0f ? style.Scale : style.Scale * width / size.X;
    }

    public static void Line(ImDrawListPtr drawList, string text, Vector2 center, float width, Vector4 ink,
        in TextStyle style)
    {
        var fitted = Typography.FitText(text, width, style);
        Typography.DrawCentered(drawList, center, fitted, ink, style);
    }

    public static void LeftLine(ImDrawListPtr drawList, string text, Vector2 topLeft, float width, Vector4 ink,
        in TextStyle style)
    {
        Typography.Draw(drawList, topLeft, Typography.FitText(text, width, style), ink, style);
    }

    public static Rect DeckPrimary(Rect deck, float secondaryWidth, float scale)
    {
        var pad = DeckPad * scale;
        var height = Button.LargeHeight * scale;
        var top = deck.Max.Y - pad - height;
        var right = deck.Max.X - pad - (secondaryWidth > 0f ? secondaryWidth + ButtonGap * scale : 0f);
        return new Rect(new Vector2(deck.Min.X + pad, top), new Vector2(right, top + height));
    }

    public static Rect DeckSecondary(Rect deck, float width, float scale)
    {
        var pad = DeckPad * scale;
        var height = Button.LargeHeight * scale;
        var top = deck.Max.Y - pad - height;
        return new Rect(new Vector2(deck.Max.X - pad - width, top), new Vector2(deck.Max.X - pad, top + height));
    }

    public static float SecondaryWidth(string label)
    {
        return label.Length == 0 ? 0f : Button.WidthFor(label, ButtonSize.Large);
    }

    public static float DeckCaption(ImDrawListPtr drawList, Rect deck, string text, float scale)
    {
        var pad = DeckPad * scale;
        var top = deck.Min.Y + pad * 0.75f;
        LeftLine(drawList, text, new Vector2(deck.Min.X + pad, top), deck.Width - pad * 2f, CasinoColors.InkTitle,
            TextStyles.SubheadlineEmphasized);
        return top + Typography.LineHeight(TextStyles.SubheadlineEmphasized);
    }

    public static void StateLine(ImDrawListPtr drawList, string text, Vector2 center, float width, Vector4 ink,
        float scale)
    {
        var style = TextStyles.Title2;
        var fitted = Typography.FitText(text, width, style);
        Typography.DrawCentered(drawList, center + new Vector2(0f, 1.5f * scale), fitted,
            new Vector4(0f, 0f, 0f, 0.55f), style);
        Typography.DrawCentered(drawList, center, fitted, ink, style);
    }

    public static void Status(ImDrawListPtr drawList, string text, Vector2 center, float width)
    {
        Line(drawList, text, center, width, CasinoColors.InkBody, TextStyles.Subheadline);
    }
}
