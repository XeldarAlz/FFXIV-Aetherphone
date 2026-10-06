using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games;

internal static class StatCapsule
{
    private const float PadX = 10f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float SegmentGap = 10f;
    private static readonly TextStyle Style = TextStyles.FootnoteEmphasized;

    public static float Width(string countLabel, string bestLabel, float scale)
    {
        var width = (PadX * 2f + IconSize + IconGap) * scale + Typography.Measure(countLabel, Style).X;
        if (bestLabel.Length > 0)
        {
            width += (SegmentGap + IconSize + IconGap) * scale + Typography.Measure(bestLabel, Style).X;
        }

        return width / scale;
    }

    public static void Draw(ImDrawListPtr drawList, Rect rect, FontAwesomeIcon icon, string countLabel,
        Vector4 iconInk, string bestLabel, Vector4 accent, float scale)
    {
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var iconSize = IconSize * scale;
        var centerY = rect.Center.Y;
        var textTop = centerY - Typography.LineHeight(Style) * 0.5f;
        var x = rect.Min.X + PadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(x + iconSize * 0.5f, centerY), icon, iconInk, iconSize);
        x += iconSize + IconGap * scale;
        Typography.Draw(drawList, new Vector2(x, textTop), countLabel, GamePalette.InkLight, Style);
        if (bestLabel.Length == 0)
        {
            return;
        }

        x += Typography.Measure(countLabel, Style).X + SegmentGap * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(x + iconSize * 0.5f, centerY), FontAwesomeIcon.Trophy, accent,
            iconSize);
        x += iconSize + IconGap * scale;
        Typography.Draw(drawList, new Vector2(x, textTop), bestLabel, GamePalette.InkLight, Style);
    }
}
