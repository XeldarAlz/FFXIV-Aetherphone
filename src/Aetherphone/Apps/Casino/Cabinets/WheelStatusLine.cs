using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class WheelStatusLine
{
    public const string ClockReference = "0:00";

    public static readonly TextStyle LabelStyle = TextStyles.SubheadlineEmphasized;
    public static readonly TextStyle ClockStyle = TextStyles.SubheadlineEmphasized;
    public static readonly TextStyle CountStyle = TextStyles.SubheadlineEmphasized;

    private static readonly Vector4 GlyphInk = CasinoColors.InkBody;

    private LabelSlot crowdHint;

    public void Draw(ImDrawListPtr drawList, Rect ribbon, in WheelStatus status, int crowd, float scale)
    {
        if (ribbon.Height <= 0f)
        {
            return;
        }

        var label = Loc.T(status.Label);
        var clock = status.HasClock ? TimeText.Duration(status.Seconds) : string.Empty;
        var count = crowd > 0 ? GameNumber.Label(crowd) : string.Empty;
        var labelWidth = Typography.Measure(label, LabelStyle).X;
        var clockWidth = clock.Length > 0
            ? MathF.Max(Typography.Measure(clock, ClockStyle).X, Typography.Measure(ClockReference, ClockStyle).X)
            : 0f;
        var countWidth = count.Length > 0 ? Typography.Measure(count, CountStyle).X : 0f;
        var layout = WheelStatusLayout.Compute(WheelStatusLayout.RowOf(ribbon, scale), labelWidth, clockWidth,
            countWidth, scale);
        StageText.Capsule(drawList, layout.Phase.Min, layout.Phase.Max);
        drawList.AddCircleFilled(layout.Dot, WheelStatusLayout.DotRadius * scale, ImGui.GetColorU32(DotInk(status)),
            12);
        DrawLabel(drawList, layout.Label, label, labelWidth);
        if (layout.HasClock)
        {
            DrawClock(drawList, layout.Clock, clock, status.Urgent);
        }

        if (layout.HasCrowd)
        {
            DrawCrowd(drawList, layout, count, crowd, scale);
        }
    }

    private static Vector4 DotInk(in WheelStatus status)
    {
        var breath = 0.55f + 0.45f * Pulse.Wave(Pulse.Calm);
        return status.Kind switch
        {
            WheelStatusKind.Open => CasinoColors.LightA with { W = breath },
            WheelStatusKind.Closed or WheelStatusKind.Spinning => CasinoColors.Money,
            WheelStatusKind.Next => CasinoColors.InkBody,
            _ => CasinoColors.InkMuted with { W = breath },
        };
    }

    private static void DrawLabel(ImDrawListPtr drawList, Rect box, string label, float measured)
    {
        var textScale = LabelStyle.Scale;
        var shown = label;
        if (!StageText.Fits(measured, box.Width))
        {
            textScale = Typography.FitScale(label, box.Width, LabelStyle.Scale, TextStyles.Footnote.Scale,
                LabelStyle.Weight);
            shown = Typography.FitText(label, box.Width, textScale, LabelStyle.Weight);
        }

        var size = Typography.Measure(shown, textScale, LabelStyle.Weight);
        Typography.Draw(drawList, new Vector2(box.Min.X, box.Center.Y - size.Y * 0.5f), shown, StageText.Strong,
            textScale, LabelStyle.Weight);
    }

    private static void DrawClock(ImDrawListPtr drawList, Rect box, string clock, bool urgent)
    {
        var ink = urgent ? TurnTimerRing.Warn with { W = 0.6f + 0.4f * Pulse.Wave(Pulse.Fast) } : StageText.Strong;
        var size = Typography.Measure(clock, ClockStyle);
        Typography.Draw(drawList, new Vector2(box.Max.X - size.X, box.Center.Y - size.Y * 0.5f), clock, ink,
            ClockStyle);
    }

    private void DrawCrowd(ImDrawListPtr drawList, in WheelStatusLayout layout, string count, int crowd, float scale)
    {
        StageText.Capsule(drawList, layout.Crowd.Min, layout.Crowd.Max);
        PhoneIcon.Draw(drawList, layout.Glyph, PhoneIcons.Users, GlyphInk, WheelStatusLayout.GlyphBox * scale);
        var size = Typography.Measure(count, CountStyle);
        Typography.Draw(drawList, new Vector2(layout.Count.Min.X, layout.Count.Center.Y - size.Y * 0.5f), count,
            StageText.Strong, CountStyle);
        if (UiInteract.Hover(layout.Crowd.Min, layout.Crowd.Max))
        {
            HoverTooltip.Show(layout.Crowd, crowdHint.Get(L.Casino.WheelAtTheWheel, crowd));
        }
    }
}
