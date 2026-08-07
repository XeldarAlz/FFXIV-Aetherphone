using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class HintIcon
{
    public static void Draw(Vector2 center, string hint, PhoneTheme theme, float scale)
    {
        var iconSize = Metrics.Size.HintIconHeight * scale;
        var hitRadius = new Vector2(iconSize * 0.65f, iconSize * 0.65f);
        var hitRect = new Rect(center - hitRadius, center + hitRadius);

        ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), center, FontAwesomeIcon.QuestionCircle, theme.TextMuted,
            iconSize);
        HoverTooltip.Show(hitRect, hint);
    }
}
