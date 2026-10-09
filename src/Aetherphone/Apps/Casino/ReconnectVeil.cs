using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal static class ReconnectVeil
{
    private const float Dim = 0.55f;
    private const float PanelWidthFraction = 0.78f;

    private static readonly CasinoTextCache Texts = new();

    public static void Draw(ImDrawListPtr drawList, in Rect area, AppSkin ui, long heldRemainingMilliseconds,
        float scale)
    {
        Material.Veil(drawList, area.Min, area.Max, Dim);

        var title = Loc.T(L.Casino.ReconnectTitle);
        var body = heldRemainingMilliseconds > 0
            ? Texts.Duration(L.Casino.SeatHeldFor, SecondsOf(heldRemainingMilliseconds))
            : Loc.T(L.Casino.ReconnectHint);

        var width = area.Width * PanelWidthFraction;
        var pad = 16f * scale;
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var bodyBlock = Typography.MeasureWrappedBlock(body, TextStyles.Footnote, width - pad * 2f);
        var height = titleSize.Y + bodyBlock.Y + pad * 2f + 8f * scale;
        var center = area.Center;
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var rounding = Metrics.Radius.Widget * scale;
        Material.LiquidGlass(drawList, min, max, rounding, scale, GlassTone.Dark, 0f);

        var dotCenter = new Vector2(min.X + pad, min.Y + pad + titleSize.Y * 0.5f);
        drawList.AddCircleFilled(dotCenter, 3.2f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f + 0.45f * Pulse.Wave(Pulse.Breath))), 12);
        Typography.Draw(drawList, new Vector2(dotCenter.X + 10f * scale, min.Y + pad), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + titleSize.Y + 8f * scale), body,
            ui.MutedInk, TextStyles.Footnote, width - pad * 2f);
    }

    internal static int SecondsOf(long remainingMilliseconds)
    {
        return (int)((remainingMilliseconds + 999) / 1000);
    }
}
