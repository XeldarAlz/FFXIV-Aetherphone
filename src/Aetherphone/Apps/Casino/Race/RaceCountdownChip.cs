using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal static class RaceCountdownChip
{
    private const float RingRadius = 10f;
    private const float RingInset = 6f;

    public static void Draw(ImDrawListPtr drawList, Rect rect, bool wide, long remainingMs, int windowSeconds,
        Vector4 accent, float scale)
    {
        if (rect.Width <= 0f || windowSeconds <= 0)
        {
            return;
        }

        var radius = rect.Height * 0.5f;
        Material.LiquidGlass(drawList, rect.Min, rect.Max, radius, scale, GlassTone.Dark, 0f);
        var seconds = (int)((Math.Max(0, remainingMs) + 999) / 1000);
        var ring = MathF.Min(RingRadius * scale, radius - 3f * scale);
        var ringCenter = wide
            ? new Vector2(rect.Min.X + RingInset * scale + ring, rect.Center.Y)
            : rect.Center;
        TurnTimerRing.Draw(drawList, ringCenter, ring, remainingMs, windowSeconds, accent, scale);
        if (!wide)
        {
            Typography.DrawCentered(drawList, ringCenter, GameNumber.Label(seconds), CasinoColors.InkTitle,
                TextStyles.FootnoteEmphasized);
            return;
        }

        var style = TextStyles.SubheadlineEmphasized;
        var left = ringCenter.X + ring + RingInset * scale;
        var room = MathF.Max(1f, rect.Max.X - radius * 0.5f - left);
        var text = Typography.FitText(TimeText.Duration(seconds), room, style);
        var size = Typography.Measure(text, style);
        var ink = TurnTimerRing.IsUrgent(remainingMs, windowSeconds) ? CasinoColors.MoneyHighlight : CasinoColors.InkTitle;
        Typography.Draw(drawList, new Vector2(left + (room - size.X) * 0.5f, rect.Center.Y - size.Y * 0.5f), text, ink,
            style);
    }
}
