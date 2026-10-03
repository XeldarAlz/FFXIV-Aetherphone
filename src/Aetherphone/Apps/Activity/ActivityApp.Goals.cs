using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Activity;

internal sealed partial class ActivityApp
{
    private const float GoalRingRadius = 30f;
    private const float GoalRingThickness = 9f;
    private const float GoalControlTop = 18f;
    private const float GoalControlHeight = 56f;
    private const float GoalButtonRadius = 24f;
    private const float GoalButtonGlyph = 1.15f;
    private const float GoalButtonWashAlpha = 0.20f;
    private const float GoalDisabledAlpha = 0.35f;
    private const float GoalHeaderGap = 14f;

    private readonly Spring[] goalFills = new Spring[ActivityGoals.RingCount];
    private readonly CachedText[] goalValues = new CachedText[ActivityGoals.RingCount];
    private readonly CachedText[] goalPercents = new CachedText[ActivityGoals.RingCount];

    private static readonly string[] GoalMinusIds = { "character.goal.minus.0", "character.goal.minus.1", "character.goal.minus.2" };
    private static readonly string[] GoalPlusIds = { "character.goal.plus.0", "character.goal.plus.1", "character.goal.plus.2" };

    private void DrawGoals(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
            {
                cursorY = DrawGoalCard(drawList, new Vector2(origin.X, cursorY), width, ring, scale);
                cursorY += ActivityArt.TileGap * scale;
            }

            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Character.GoalsHint),
                ui.MutedInk, TextStyles.Footnote, width);
            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "character.goals.nav", Loc.T(L.Character.GoalsSection),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
    }

    private float DrawGoalCard(ImDrawListPtr drawList, Vector2 origin, float width, int ring, float scale)
    {
        var pad = ActivityArt.CardPad * scale;
        var ringRadius = GoalRingRadius * scale;
        var height = pad * 2f + ringRadius * 2f + GoalControlTop * scale + GoalControlHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Widget * scale, true);
        var tint = ActivityArt.Tint(ring);
        var today = tracker.Today;
        var fraction = ActivityGoals.Fraction(targets, today, ring);
        var ringCenter = new Vector2(origin.X + pad + ringRadius, origin.Y + pad + ringRadius);
        var shown = goalFills[ring].Step(fraction, Motion.Sheet, ActivityArt.FrameDelta());
        ActivityArt.Ring(drawList, ringCenter, ringRadius - GoalRingThickness * scale * 0.5f,
            GoalRingThickness * scale, shown, tint, true);
        ProgressRing.CenterIcon(drawList, ringCenter, ActivityArt.RingIcon(ring), tint, ringRadius * 0.5f);

        var textLeft = ringCenter.X + ringRadius + GoalHeaderGap * scale;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var percentHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = ringCenter.Y - (nameHeight + percentHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(Loc.T(RingNames[ring]), textWidth, TextStyles.Headline), tint, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
            Typography.FitText(GoalPercent(ring, fraction), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);

        var index = ActivityGoalSteps.IndexOf(ring, targets);
        var count = ActivityGoalSteps.Count(ring);
        var controlTop = origin.Y + pad + ringRadius * 2f + GoalControlTop * scale;
        var controlCenterY = controlTop + GoalControlHeight * scale * 0.5f;
        var buttonRadius = GoalButtonRadius * scale;
        var minusCenter = new Vector2(origin.X + pad + buttonRadius, controlCenterY);
        var plusCenter = new Vector2(max.X - pad - buttonRadius, controlCenterY);
        var delta = 0;
        if (GoalButton(minusCenter, buttonRadius, FontAwesomeIcon.Minus, tint, index > 0, GoalMinusIds[ring]))
        {
            delta = -1;
        }

        if (GoalButton(plusCenter, buttonRadius, FontAwesomeIcon.Plus, tint, index < count - 1, GoalPlusIds[ring]))
        {
            delta = 1;
        }

        var valueWidth = MathF.Max(1f, plusCenter.X - minusCenter.X - buttonRadius * 2f - pad);
        var value = Typography.FitText(GoalValue(ring), valueWidth, TextStyles.WidgetDisplayCompact);
        var valueSize = Typography.Measure(value, TextStyles.WidgetDisplayCompact);
        var unit = Typography.FitText(digest.Units[ring], valueWidth, TextStyles.FootnoteEmphasized);
        var unitSize = Typography.Measure(unit, TextStyles.FootnoteEmphasized);
        var blockTop = controlCenterY - (valueSize.Y + unitSize.Y) * 0.5f;
        var centerX = (minusCenter.X + plusCenter.X) * 0.5f;
        Typography.Draw(drawList, new Vector2(centerX - valueSize.X * 0.5f, blockTop), value, ui.TitleInk,
            TextStyles.WidgetDisplayCompact);
        Typography.Draw(drawList, new Vector2(centerX - unitSize.X * 0.5f, blockTop + valueSize.Y), unit, tint,
            TextStyles.FootnoteEmphasized);
        if (delta != 0)
        {
            targets = ActivityGoalSteps.With(ring, index + delta, targets);
            ActivityGoalSteps.Apply(configuration, targets);
            configuration.Save();
            UiFeedback.Play(UiSound.Tap);
        }

        return max.Y;
    }

    private bool GoalButton(Vector2 center, float radius, FontAwesomeIcon icon, Vector4 tint, bool enabled,
        string id)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hit = new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(center - hit, center + hit);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, pressed, PressFx.ControlPressedScale);
        var drawn = hit * grow;
        var alpha = enabled ? 1f : GoalDisabledAlpha;
        Material.ThemedGlass(drawList, center - drawn, center + drawn, radius * grow, UiScale.Current,
            ui.Palette.BackdropTop, alpha);
        var wash = hovered ? GoalButtonWashAlpha * 1.5f : GoalButtonWashAlpha;
        Squircle.Fill(drawList, center - drawn, center + drawn, radius * grow,
            ImGui.GetColorU32(Palette.WithAlpha(tint, wash * alpha)));
        ProgressRing.CenterIcon(drawList, center, icon, Palette.WithAlpha(tint, alpha),
            radius * GoalButtonGlyph * grow * 0.6f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(center - hit, center + hit, hovered);
    }

    private string GoalValue(int ring)
    {
        var key = ring switch
        {
            0 => (long)MathF.Round(targets.Levels * 10f),
            1 => targets.Duties,
            _ => targets.Gil,
        };
        if (goalValues[ring].IsCurrent(key))
        {
            return goalValues[ring].Value;
        }

        return goalValues[ring].Store(key, ring switch
        {
            0 => ActivityDigest.Levels(targets.Levels),
            1 => ActivityDigest.Number(targets.Duties),
            _ => ActivityDigest.Number(targets.Gil),
        });
    }

    private string GoalPercent(int ring, float fraction)
    {
        var percent = ActivityDigest.PercentValue(fraction);
        if (goalPercents[ring].IsCurrent(percent))
        {
            return goalPercents[ring].Value;
        }

        return goalPercents[ring].Store(percent, Loc.T(L.Character.TodayPercent, percent));
    }
}
