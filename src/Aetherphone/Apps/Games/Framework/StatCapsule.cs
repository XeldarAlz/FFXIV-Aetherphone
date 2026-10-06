using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework;

internal static class StatCapsule
{
    private const float PadX = 10f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private static readonly TextStyle Style = TextStyles.FootnoteEmphasized;

    public static float Width(string countLabel, float scale) =>
        PadX * 2f + IconSize + IconGap + Typography.Measure(countLabel, Style).X / scale;

    public static void Draw(ImDrawListPtr drawList, Rect rect, FontAwesomeIcon icon, string countLabel,
        Vector4 iconInk, float scale)
    {
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var iconSize = IconSize * scale;
        var centerY = rect.Center.Y;
        var x = rect.Min.X + PadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(x + iconSize * 0.5f, centerY), icon, iconInk, iconSize);
        x += iconSize + IconGap * scale;
        Typography.Draw(drawList, new Vector2(x, centerY - Typography.LineHeight(Style) * 0.5f), countLabel,
            GamePalette.InkLight, Style);
    }
}
