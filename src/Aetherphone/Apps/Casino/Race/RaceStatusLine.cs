using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal static class RaceStatusLine
{
    public const int UrgentSeconds = 10;

    public static int SecondsLeft(long remainingMs) => (int)((Math.Max(0, remainingMs) + 999) / 1000);

    public static void Draw(ImDrawListPtr drawList, in CasinoStageLayout stage, string label, int seconds, bool live,
        bool urgent, float scale)
    {
        var clock = TimeText.Duration(seconds);
        var clockStyle = TextStyles.Headline;
        var layout = RaceStatusLayout.Compute(stage, Typography.Measure(clock, clockStyle).X, scale);
        if (!layout.Visible)
        {
            return;
        }

        StageText.Capsule(drawList, layout.Capsule.Min, layout.Capsule.Max);
        var dotInk = live ? CasinoColors.LightA : CasinoColors.InkMuted;
        var glow = live ? 0.55f + 0.45f * Pulse.Wave(urgent ? Pulse.Fast : Pulse.Calm) : 1f;
        drawList.AddCircleFilled(layout.Dot, RaceStatusLayout.DotRadius * scale,
            ImGui.GetColorU32(dotInk with { W = glow }), 12);
        var centerY = layout.Capsule.Center.Y;
        RaceLine.DrawLeft(drawList, layout.Label.Min.X, centerY, label, layout.Label.Width,
            TextStyles.SubheadlineEmphasized, TextStyles.FootnoteEmphasized, StageText.Strong);
        RaceLine.DrawRight(drawList, layout.Clock.Max.X, centerY, clock, layout.Clock.Width, clockStyle, clockStyle,
            urgent ? CasinoColors.LightA : StageText.Strong);
    }
}
