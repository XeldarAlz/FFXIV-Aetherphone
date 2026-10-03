using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Feedback;

internal sealed partial class FeedbackApp
{
    private const float BadgeRadiusUnits = 46f;
    private const float HaloGrowth = 1.45f;
    private const float HaloAlpha = 0.16f;
    private const float CheckThicknessUnits = 5f;
    private const float CheckDelay = 0.12f;
    private const float CheckSmoothTime = 0.2f;
    private const float ContentDelay = 0.2f;
    private const float ContentRise = 14f;
    private const float BadgeTopFraction = 0.26f;
    private const float TitleGap = 26f;
    private const float LineGap = 8f;
    private const float ActionGap = 28f;
    private const float ActionSpacing = 10f;
    private const float MaxTextWidth = 300f;

    private FeedbackCategory sentCategory = FeedbackCategory.Other;
    private float thanksElapsed;
    private Spring thanksBadge;
    private Spring thanksHalo;
    private Spring thanksCheck;
    private Spring thanksContent;

    private void BeginThanks()
    {
        thanksElapsed = 0f;
        thanksBadge.SnapTo(0f);
        thanksHalo.SnapTo(0f);
        thanksCheck.SnapTo(0f);
        thanksContent.SnapTo(0f);
    }

    private void DrawSent(Rect area)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        thanksElapsed += delta;
        var badge = Math.Clamp(thanksBadge.Step(1f, Motion.Sheet, delta), 0f, 1f);
        var halo = Math.Clamp(thanksHalo.Step(1f, Motion.Island, delta), 0f, 1f);
        var check = thanksElapsed < CheckDelay ? 0f : Math.Clamp(thanksCheck.Step(1f, CheckSmoothTime, delta), 0f, 1f);
        var content = thanksElapsed < ContentDelay
            ? 0f
            : Math.Clamp(thanksContent.Step(1f, Motion.Appear, delta), 0f, 1f);

        var drawList = ImGui.GetWindowDrawList();
        ref readonly var kind = ref FeedbackKinds.Of(sentCategory);
        var centerX = area.Center.X;
        var radius = BadgeRadiusUnits * scale;
        var badgeCenter = new Vector2(centerX, area.Min.Y + area.Height * BadgeTopFraction + radius);
        drawList.AddCircleFilled(badgeCenter, radius * (1f + (HaloGrowth - 1f) * halo),
            ImGui.GetColorU32(Palette.WithAlpha(kind.Tint, HaloAlpha * (1f - halo * 0.5f))), 64);
        var surface = IconTile.Surface(kind.Tint);
        Squircle.FillCircleVerticalGradient(drawList, badgeCenter, radius * badge,
            ImGui.GetColorU32(Palette.Lighten(surface, 0.18f)), ImGui.GetColorU32(Palette.Darken(surface, 0.1f)));
        FeedbackArt.Check(drawList, badgeCenter, radius * badge, check, AccentRing.Ink,
            CheckThicknessUnits * scale * badge);

        var rise = (1f - content) * ContentRise * scale;
        var textWidth = MathF.Min(area.Width - AppSurface.SidePadding * 2f * scale, MaxTextWidth * scale);
        var cursorY = badgeCenter.Y + radius + TitleGap * scale + rise;
        cursorY = Typography.DrawWrappedCentered(drawList, Loc.T(L.Feedback.ThankYou), TextStyles.Title2,
            ui.TitleInk with { W = content }, new Vector2(centerX, cursorY), textWidth);
        cursorY = Typography.DrawWrappedCentered(drawList, Loc.T(kind.Thanks), TextStyles.Subheadline,
            ui.MutedInk with { W = ui.MutedInk.W * content }, new Vector2(centerX, cursorY + LineGap * scale),
            textWidth);

        if (content < 0.6f)
        {
            return;
        }

        var actionWidth = textWidth;
        var actionHeight = SendPillHeight * scale;
        var top = cursorY + ActionGap * scale;
        var primary = new Rect(new Vector2(centerX - actionWidth * 0.5f, top),
            new Vector2(centerX + actionWidth * 0.5f, top + actionHeight));
        if (!store.HistoryVisible)
        {
            if (FeedbackArt.PressPill(ui, primary, Loc.T(L.Feedback.Done), true, "feedback.sent.done"))
            {
                router.Pop();
            }

            return;
        }

        if (FeedbackArt.PressPill(ui, primary, Loc.T(L.Feedback.ViewYourFeedback), true, "feedback.sent.view"))
        {
            router.Push(FeedbackRoute.History);
            return;
        }

        var secondaryTop = primary.Max.Y + ActionSpacing * scale;
        var secondary = new Rect(new Vector2(primary.Min.X, secondaryTop),
            new Vector2(primary.Max.X, secondaryTop + actionHeight));
        if (ui.GhostButton(secondary, Loc.T(L.Feedback.Done)))
        {
            router.Pop();
        }
    }
}
