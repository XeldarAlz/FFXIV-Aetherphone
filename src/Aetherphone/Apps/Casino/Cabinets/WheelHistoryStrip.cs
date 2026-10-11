using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class WheelHistoryStrip
{
    public static readonly TextStyle CaptionStyle = TextStyles.FootnoteEmphasized;
    public static readonly TextStyle ChipStyle = TextStyles.FootnoteEmphasized;
    public static readonly TextStyle LatestStyle = TextStyles.SubheadlineEmphasized;

    private const float OlderFillAlpha = 0.30f;
    private const float OlderInkFloor = 0.6f;
    private const float LatestRingAlpha = 0.9f;

    public static void Draw(ImDrawListPtr drawList, Rect strip, WheelRecentRail rail, float pop, float scale)
    {
        var count = rail.Count;
        if (count == 0 || strip.Height <= 0f)
        {
            return;
        }

        var caption = Loc.T(L.Casino.WheelLastSpins);
        var layout = WheelHistoryLayout.Compute(strip, Typography.Measure(caption, CaptionStyle).X,
            WidestLabel(ChipStyle), WidestLabel(LatestStyle), count, scale);
        DrawCaption(drawList, layout.Caption, caption);
        for (var index = layout.Visible - 1; index >= 0; index--)
        {
            var spot = rail.SpotAt(index);
            if (index == 0)
            {
                DrawLatest(drawList, layout.Chip(0), spot, pop, scale);
                continue;
            }

            DrawOlder(drawList, layout.Chip(index), spot, layout.Fade(index));
        }
    }

    private static float WidestLabel(in TextStyle style)
    {
        var widest = 0f;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            widest = MathF.Max(widest, Typography.Measure(WheelRingArt.SpotLabel(spot), style).X);
        }

        return widest;
    }

    private static void DrawCaption(ImDrawListPtr drawList, Rect box, string caption)
    {
        var shown = Typography.FitText(caption, box.Width, CaptionStyle);
        var size = Typography.Measure(shown, CaptionStyle);
        Typography.Draw(drawList, new Vector2(box.Min.X, box.Center.Y - size.Y * 0.5f), shown, StageText.Body,
            CaptionStyle);
    }

    private static void DrawLatest(ImDrawListPtr drawList, Rect chip, int spot, float pop, float scale)
    {
        var center = chip.Center;
        var half = new Vector2(chip.Width, chip.Height) * 0.5f * pop;
        var min = center - half;
        var max = center + half;
        var fill = WheelRingArt.SpotColors[spot];
        Squircle.Fill(drawList, min, max, half.Y, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, min, max, half.Y,
            ImGui.GetColorU32(Palette.WithAlpha(CasinoColors.InkTitle, LatestRingAlpha)), 1.5f * scale);
        if (pop < 0.6f)
        {
            return;
        }

        Typography.DrawCentered(drawList, center, WheelRingArt.SpotLabel(spot), WheelRingArt.InkOn(fill),
            LatestStyle);
    }

    private static void DrawOlder(ImDrawListPtr drawList, Rect chip, int spot, float fade)
    {
        var radius = chip.Height * 0.5f;
        var color = WheelRingArt.SpotColors[spot];
        Squircle.Fill(drawList, chip.Min, chip.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(color,
            OlderFillAlpha * fade)));
        var ink = WheelRingArt.SpotTextInks[spot];
        Typography.DrawCentered(drawList, chip.Center, WheelRingArt.SpotLabel(spot),
            ink with { W = MathF.Max(OlderInkFloor, fade) }, ChipStyle);
    }
}
