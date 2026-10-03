using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Input;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class NotificationDeck
{
    public const float GroupGap = 14f;

    private const float CardGap = 10f;
    private const float StackOffsetY = 6f;
    private const float StackScaleStep = 0.94f;
    private const float StackAlphaStep = 0.78f;
    private const float MoreLabelHeight = 14f;
    private const float HeaderHeight = 32f;
    private const float HeaderPad = 6f;
    private const float RevealWidth = 84f;
    private const float RevealGap = 8f;
    private const float RevealOpenFraction = 0.5f;
    private const float RevealHitFraction = 0.6f;
    private const float SwipeRightClamp = 10f;
    private const float SwipeCommitFraction = 0.42f;
    private const float SlideOutOvershoot = 40f;
    private const float SlideRestDistance = 2f;
    private const float TapSlop = 10f;
    private const float FailedSwipeTapFraction = 0.15f;
    private const float DragAxisThreshold = 6f;
    private const float PillHeight = 26f;
    private const float PillPadX = 12f;
    private const float PillGap = 8f;
    private const float ChevronReach = 4f;
    private const float ChevronGap = 10f;
    private const float ChevronThickness = 1.6f;
    private const float HoverLift = 0.35f;
    private const float CardShadowOffset = 2f;
    private const float CardShadowAlpha = 0.24f;
    private const float CardEdgeAlpha = 0.7f;
    private const string TitleMarquee = "notificationdeck.title.";
    private const string BodyMarquee = "notificationdeck.body.";
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly float[] StackScales = { 1f, StackScaleStep, StackScaleStep * StackScaleStep };
    private static readonly float[] StackAlphas = { 1f, StackAlphaStep, StackAlphaStep * StackAlphaStep };

    private readonly NotificationService notifications;
    private readonly NotificationRouter router;
    private readonly Action? navigated;
    private readonly Dictionary<string, GroupState> states = new(StringComparer.Ordinal);
    private readonly List<string> staleKeys = new();
    private readonly List<Candidate> candidates = new();
    private readonly DragTracker drag = new();
    private Rect interactionBounds;
    private bool scrollGesture;
    private bool axisLocked;
    private bool hasDragTarget;
    private Candidate dragTarget;
    private float dragBase;
    private float swipeOffset;
    private bool slideActive;
    private bool slideRemoving;
    private Target slideTarget;
    private PhoneNotification? slideNotification;
    private float slideGoal;
    private Spring slide;
    private Rect clearButton;
    private bool clearButtonVisible;
    private bool overChild;

    public NotificationDeck(NotificationService notifications, NotificationRouter router, Action? navigated = null)
    {
        this.notifications = notifications;
        this.router = router;
        this.navigated = navigated;
    }

    public NotificationRouter Router => router;

    public bool DragActive => drag.Active;

    public bool Swiping => drag.Active && hasDragTarget && axisLocked && !scrollGesture;

    public void Reset()
    {
        drag.Cancel();
        hasDragTarget = false;
        swipeOffset = 0f;
        scrollGesture = false;
        axisLocked = false;
        slideActive = false;
        slideRemoving = false;
        slideNotification = null;
        clearButtonVisible = false;
        states.Clear();
    }

    public void Sync(NotificationGroups groups)
    {
        foreach (var state in states.Values)
        {
            state.Seen = false;
        }

        var list = groups.Groups;
        for (var index = 0; index < list.Count; index++)
        {
            var group = list[index];
            if (!states.TryGetValue(group.Key, out var state))
            {
                state = new GroupState();
                states[group.Key] = state;
            }

            if (group.Count < 2)
            {
                state.Expanded = false;
            }

            state.MoreLabel = group.Count > 1 ? Loc.T(L.Notifications.More, group.HiddenCount) : string.Empty;
            state.Seen = true;
        }

        staleKeys.Clear();
        foreach (var pair in states)
        {
            if (!pair.Value.Seen)
            {
                staleKeys.Add(pair.Key);
            }
        }

        for (var index = 0; index < staleKeys.Count; index++)
        {
            states.Remove(staleKeys[index]);
        }

        if (slideActive && !slideRemoving)
        {
            slideActive = false;
            clearButtonVisible = false;
        }
    }

    public float Height(NotificationGroup group, float scale)
    {
        var progress = states.TryGetValue(group.Key, out var state) ? state.Expand.Value : 0f;
        return BlockHeight(group.Count, progress, scale);
    }

    public bool UpdateDrag(float scale, float deltaSeconds, KineticScroller? scroller)
    {
        if (!drag.Active)
        {
            return false;
        }

        var delta = drag.Delta;
        if (!axisLocked && (MathF.Abs(delta.X) >= DragAxisThreshold * scale ||
            MathF.Abs(delta.Y) >= DragAxisThreshold * scale))
        {
            axisLocked = true;
            scrollGesture = MathF.Abs(delta.Y) > MathF.Abs(delta.X);
        }

        if (scrollGesture)
        {
            scroller?.Move(ImGui.GetMousePos().Y, deltaSeconds);
            swipeOffset = dragBase;
        }
        else if (hasDragTarget)
        {
            swipeOffset = Math.Clamp(dragBase + delta.X, -dragTarget.Width, SwipeRightClamp * scale);
        }

        return true;
    }

    public void BeginFrame(Rect bounds)
    {
        interactionBounds = bounds;
        candidates.Clear();
        overChild = false;
    }

    public void EndFrame(float scale, bool interactive, KineticScroller? scroller)
    {
        if (interactive && clearButtonVisible && slideActive && !slideRemoving && !drag.Active &&
            UiInteract.ClickedOutside(clearButton.Min, clearButton.Max, false))
        {
            slideGoal = 0f;
        }

        HandleGesture(scale, interactive, scroller);
    }

    public void Advance(float delta)
    {
        foreach (var state in states.Values)
        {
            var target = state.Expanded ? 1f : 0f;
            state.Expand.Step(target, Motion.Island, delta);
            if (state.Expand.IsResting(target, TransitionTiming.RestPositionEpsilon,
                    TransitionTiming.RestVelocityEpsilon))
            {
                state.Expand.SnapTo(target);
            }

            var pressTarget = state.Pressed ? Motion.PressScaleCard : 1f;
            state.Press.Step(pressTarget, state.Pressed ? Motion.PressIn : Motion.Release, delta);
            state.Pressed = false;
        }

        if (!slideActive)
        {
            return;
        }

        slide.Step(slideGoal, Motion.Release, delta);
        if (slideRemoving)
        {
            if (slide.Value <= slideGoal + SlideRestDistance)
            {
                PerformRemoval();
                slideActive = false;
                slideRemoving = false;
            }

            return;
        }

        if (slideGoal == 0f && slide.IsResting(0f, 0.4f, 2f))
        {
            slide.SnapTo(0f);
            slideActive = false;
            clearButtonVisible = false;
        }
    }

    public void ClearGroup(NotificationGroup group)
    {
        router.Acknowledge(group.Newest);
        notifications.RemoveGroup(group.Key);
    }

    public void DrawGroup(ImDrawListPtr drawList, NotificationGroup group, Vector2 origin, float width,
        in NotificationDeckStyle style, float scale, float opacity, bool interactive)
    {
        if (!states.TryGetValue(group.Key, out var state))
        {
            return;
        }

        var progress = state.Expand.Value;
        var count = group.Count;
        var cardHeight = NotificationCard.Height * scale;
        var layers = group.VisibleLayers;
        var collapsedHit = progress < 0.5f;
        var stacked = count > 1;
        var blockBottom = origin.Y + CollapsedHeight(count, scale);
        var press = state.Press.Value;
        for (var index = count - 1; index >= 0; index--)
        {
            var collapsedRect = CollapsedRect(index, layers, origin, width, cardHeight, scale);
            var expandedTop = origin.Y + HeaderHeight * scale + index * (cardHeight + CardGap * scale);
            var expandedRect = new Rect(new Vector2(origin.X, expandedTop),
                new Vector2(origin.X + width, expandedTop + cardHeight));
            var rect = stacked ? Lerp(collapsedRect, expandedRect, progress) : collapsedRect;
            var layerAlpha = float.Lerp(CollapsedAlpha(index, layers), 1f, progress);
            var glassAlpha = layerAlpha * opacity;
            if (glassAlpha <= 0.01f)
            {
                continue;
            }

            var contentAlpha = index == 0 ? opacity : progress * opacity;
            var hittable = interactive && (collapsedHit ? index == 0 : progress > 0.5f);
            var actsOnGroup = collapsedHit && stacked;
            var notification = group.Items[index];
            var candidate = new Candidate(rect, actsOnGroup, group.Key, notification.Id, width, notification,
                actsOnGroup);
            var slideOffset = SlideFor(candidate);
            if (slideOffset < -1f && hittable)
            {
                DrawClearAction(drawList, rect, slideOffset, candidate, style, scale, glassAlpha, interactive);
            }

            var drawRect = rect.Translate(new Vector2(slideOffset, 0f));
            if (index == 0 && collapsedHit && press < 1f)
            {
                drawRect = Shrink(drawRect, press);
            }

            if (style.Glass)
            {
                NotificationCard.DrawGlass(drawList, drawRect, scale, glassAlpha, style.Tone);
            }
            else
            {
                DrawSolidCard(drawList, drawRect, style, scale, layerAlpha, opacity);
            }

            if (contentAlpha > 0.01f)
            {
                NotificationCard.DrawContent(drawList, drawRect, notification, style.Ink, style.MutedInk, scale,
                    contentAlpha, false, TitleMarquee, BodyMarquee);
            }

            if (!hittable)
            {
                continue;
            }

            var hitRect = actsOnGroup
                ? new Rect(drawRect.Min, new Vector2(drawRect.Max.X, MathF.Max(drawRect.Max.Y, blockBottom)))
                : drawRect;
            candidates.Add(candidate with { Rect = hitRect });
        }

        if (!stacked)
        {
            return;
        }

        if (progress < 0.99f)
        {
            DrawMoreLabel(drawList, state.MoreLabel, origin, width, layers, cardHeight, style, scale,
                (1f - progress) * opacity);
        }

        if (progress > 0.01f)
        {
            var header = new Rect(origin, new Vector2(origin.X + width, origin.Y + HeaderHeight * scale));
            DrawHeader(drawList, group, state, header, style, scale, progress * opacity,
                interactive && state.Expanded && progress > 0.5f);
        }
    }

    private static void DrawSolidCard(ImDrawListPtr drawList, Rect rect, in NotificationDeckStyle style, float scale,
        float layerAlpha, float opacity)
    {
        var rounding = NotificationCard.Rounding * scale;
        var shadow = new Vector2(0f, CardShadowOffset * scale);
        drawList.AddRectFilled(rect.Min + shadow, rect.Max + shadow,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, CardShadowAlpha * layerAlpha * opacity)), rounding);
        var fill = Palette.Mix(style.LayerFill, style.CardFill, layerAlpha);
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(fill with { W = opacity }));
        Material.EdgeSquircle(drawList, rect.Min, rect.Max, rounding, scale, CardEdgeAlpha * layerAlpha * opacity);
    }

    private void DrawMoreLabel(ImDrawListPtr drawList, string label, Vector2 origin, float width, int layers,
        float cardHeight, in NotificationDeckStyle style, float scale, float alpha)
    {
        if (label.Length == 0 || alpha <= 0.01f)
        {
            return;
        }

        var top = origin.Y + cardHeight + (layers - 1) * StackOffsetY * scale;
        var center = new Vector2(origin.X + width * 0.5f, top + MoreLabelHeight * scale * 0.5f);
        var muted = style.MutedInk;
        Typography.DrawCentered(drawList, center, label, Palette.WithAlpha(muted, muted.W * alpha),
            TextStyles.Caption2);
    }

    private void DrawHeader(ImDrawListPtr drawList, NotificationGroup group, GroupState state, Rect rect,
        in NotificationDeckStyle style, float scale, float alpha, bool interactive)
    {
        var pad = HeaderPad * scale;
        var lessLabel = Loc.T(L.Notifications.ShowLess);
        var lessSize = Typography.Measure(lessLabel, TextStyles.Footnote);
        var lessPos = new Vector2(rect.Max.X - pad - lessSize.X, rect.Center.Y - lessSize.Y * 0.5f);
        var accent = Palette.WithAlpha(style.Accent, alpha);
        Typography.Draw(drawList, lessPos, lessLabel, accent, TextStyles.Footnote);
        var reach = ChevronReach * scale;
        var chevronTip = new Vector2(lessPos.X - ChevronGap * scale, rect.Center.Y - 1f * scale);
        var chevronColor = ImGui.GetColorU32(accent);
        drawList.AddLine(new Vector2(chevronTip.X - reach, chevronTip.Y + reach), chevronTip, chevronColor,
            ChevronThickness * scale);
        drawList.AddLine(chevronTip, new Vector2(chevronTip.X + reach, chevronTip.Y + reach), chevronColor,
            ChevronThickness * scale);

        var pillLabel = Loc.T(L.Notifications.ClearAll);
        var pillSize = Typography.Measure(pillLabel, TextStyles.FootnoteEmphasized);
        var pillHeight = PillHeight * scale;
        var pillMax = new Vector2(chevronTip.X - reach - PillGap * scale, rect.Center.Y + pillHeight * 0.5f);
        var pillMin = new Vector2(pillMax.X - pillSize.X - PillPadX * scale * 2f, rect.Center.Y - pillHeight * 0.5f);
        var pillHovered = interactive && UiInteract.Hover(pillMin, pillMax);
        Material.LiquidGlass(drawList, pillMin, pillMax, pillHeight * 0.5f, scale, style.Tone, 0f, alpha);
        Typography.DrawCentered(drawList, (pillMin + pillMax) * 0.5f, pillLabel, Palette.WithAlpha(style.Ink, alpha),
            TextStyles.FootnoteEmphasized);

        var titleMaxWidth = MathF.Max(1f, pillMin.X - PillGap * scale - (rect.Min.X + pad));
        var title = Typography.FitText(group.Newest.Title, titleMaxWidth, TextStyles.FootnoteEmphasized);
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var muted = style.MutedInk;
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, rect.Center.Y - titleSize.Y * 0.5f), title,
            Palette.WithAlpha(muted, muted.W * alpha), TextStyles.FootnoteEmphasized);
        if (!interactive)
        {
            return;
        }

        if (pillHovered)
        {
            overChild = true;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(pillMin, pillMax, pillHovered))
        {
            UiFeedback.Play(UiSound.Tap);
            ClearGroup(group);
            return;
        }

        var headerHovered = !pillHovered && UiInteract.Hover(rect.Min, rect.Max);
        if (headerHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, headerHovered))
        {
            state.Expanded = false;
        }
    }

    private void DrawClearAction(ImDrawListPtr drawList, Rect rect, float slideOffset, in Candidate candidate,
        in NotificationDeckStyle style, float scale, float alpha, bool interactive)
    {
        var revealWidth = RevealWidth * scale;
        var revealed = MathF.Min(-slideOffset, revealWidth);
        var gap = RevealGap * scale;
        var buttonMin = new Vector2(rect.Max.X - revealed + gap, rect.Min.Y);
        var buttonMax = rect.Max;
        var buttonWidth = buttonMax.X - buttonMin.X;
        if (buttonWidth <= 1f)
        {
            return;
        }

        var progress = Math.Clamp(revealed / revealWidth, 0f, 1f);
        var rounding = MathF.Min(NotificationCard.Rounding * scale, buttonWidth * 0.5f);
        var hovered = interactive && progress >= RevealHitFraction && !drag.Active &&
                      UiInteract.Hover(buttonMin, buttonMax);
        var fill = hovered ? Palette.Mix(style.Danger, White, HoverLift * 0.5f) : style.Danger;
        Squircle.Fill(drawList, buttonMin, buttonMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(fill, progress * alpha)));
        var label = Typography.FitText(Loc.T(L.Notifications.Clear), MathF.Max(1f, buttonWidth - gap * 2f),
            TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList, (buttonMin + buttonMax) * 0.5f, label,
            Palette.WithAlpha(White, progress * alpha), TextStyles.FootnoteEmphasized);
        clearButton = new Rect(buttonMin, buttonMax);
        clearButtonVisible = progress >= RevealHitFraction;
        if (!interactive)
        {
            return;
        }

        if (hovered)
        {
            overChild = true;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(buttonMin, buttonMax, hovered))
        {
            SlideOut(candidate, scale);
        }
    }

    private void HandleGesture(float scale, bool interactive, KineticScroller? scroller)
    {
        if (!drag.Active && interactive && !overChild &&
            UiInteract.Hover(interactionBounds.Min, interactionBounds.Max))
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!UiInteract.Hover(candidate.Rect.Min, candidate.Rect.Max))
                {
                    continue;
                }

                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    SlideOut(candidate, scale);
                    break;
                }

                if (drag.Begin(candidate.Rect))
                {
                    BeginDrag(candidate, scroller);
                }

                break;
            }
        }

        if (drag.Active && hasDragTarget && !axisLocked && states.TryGetValue(dragTarget.Key, out var pressed))
        {
            pressed.Pressed = true;
        }

        if (!drag.Released(out var totalDelta, out _))
        {
            return;
        }

        if (scrollGesture)
        {
            scroller?.Release();
            scrollGesture = false;
            axisLocked = false;
            if (hasDragTarget && dragBase < 0f)
            {
                StartSlide(dragTarget, -RevealWidth * scale, false, dragBase);
            }

            hasDragTarget = false;
            return;
        }

        axisLocked = false;
        ResolveGesture(totalDelta, scale, scroller);
    }

    private void SlideOut(in Candidate candidate, float scale)
    {
        if (slideActive && slideRemoving && slideTarget.Matches(candidate))
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        StartSlide(candidate, -(candidate.Width + SlideOutOvershoot * scale), true, SlideFor(candidate));
        hasDragTarget = false;
    }

    private void BeginDrag(in Candidate candidate, KineticScroller? scroller)
    {
        if (slideActive && slideRemoving && slideTarget.Matches(candidate))
        {
            drag.Cancel();
            return;
        }

        UiInteract.CancelPendingTap();
        dragTarget = candidate;
        hasDragTarget = true;
        dragBase = slideActive && slideTarget.Matches(candidate) ? slide.Value : 0f;
        if (slideActive && slideTarget.Matches(candidate))
        {
            slideActive = false;
            clearButtonVisible = false;
        }

        swipeOffset = dragBase;
        scroller?.Press(ImGui.GetMousePos().Y);
        scrollGesture = false;
        axisLocked = false;
    }

    private void ResolveGesture(Vector2 totalDelta, float scale, KineticScroller? scroller)
    {
        if (!hasDragTarget)
        {
            return;
        }

        var candidate = dragTarget;
        hasDragTarget = false;
        scroller?.CancelGesture();
        var total = dragBase + totalDelta.X;
        var width = candidate.Width;
        var revealWidth = RevealWidth * scale;
        var slop = TapSlop * scale;
        if (total <= -width * SwipeCommitFraction)
        {
            UiFeedback.Play(UiSound.Tap);
            StartSlide(candidate, -(width + SlideOutOvershoot * scale), true, swipeOffset);
            return;
        }

        if (dragBase < 0f && MathF.Abs(totalDelta.X) < slop && MathF.Abs(totalDelta.Y) < slop)
        {
            StartSlide(candidate, 0f, false, swipeOffset);
            return;
        }

        if (total <= -revealWidth * RevealOpenFraction)
        {
            StartSlide(candidate, -revealWidth, false, swipeOffset);
            return;
        }

        var tapped = dragBase == 0f && MathF.Abs(totalDelta.Y) < slop &&
                     MathF.Abs(totalDelta.X) < width * FailedSwipeTapFraction;
        if (tapped)
        {
            swipeOffset = 0f;
            HandleTap(candidate);
            return;
        }

        StartSlide(candidate, 0f, false, swipeOffset);
    }

    private void StartSlide(in Candidate candidate, float goal, bool removing, float from)
    {
        slideTarget = Target.Of(candidate);
        slideNotification = removing ? candidate.Notification : null;
        slideGoal = goal;
        slideRemoving = removing;
        slide.SnapTo(from);
        slideActive = true;
        swipeOffset = 0f;
        clearButtonVisible = false;
    }

    private void HandleTap(in Candidate candidate)
    {
        if (candidate.ExpandsOnTap)
        {
            if (states.TryGetValue(candidate.Key, out var state))
            {
                state.Expanded = true;
                UiFeedback.Play(UiSound.Tap);
            }

            return;
        }

        router.Open(candidate.Notification);
        navigated?.Invoke();
    }

    private void PerformRemoval()
    {
        if (slideNotification is { } dismissed)
        {
            router.Acknowledge(dismissed);
            slideNotification = null;
        }

        if (slideTarget.IsGroup)
        {
            notifications.RemoveGroup(slideTarget.Key);
        }
        else
        {
            notifications.Remove(slideTarget.Id);
        }
    }

    private float SlideFor(in Candidate candidate)
    {
        if (drag.Active && hasDragTarget && !scrollGesture && Target.Of(dragTarget).Matches(candidate))
        {
            return swipeOffset;
        }

        if (drag.Active && hasDragTarget && scrollGesture && Target.Of(dragTarget).Matches(candidate))
        {
            return dragBase;
        }

        if (slideActive && slideTarget.Matches(candidate))
        {
            return slide.Value;
        }

        return 0f;
    }

    private static Rect Shrink(Rect rect, float factor)
    {
        var center = rect.Center;
        var half = (rect.Max - rect.Min) * (0.5f * factor);
        return new Rect(center - half, center + half);
    }

    private static Rect CollapsedRect(int index, int layers, Vector2 origin, float width, float cardHeight,
        float scale)
    {
        var layer = Math.Clamp(Math.Min(index, layers - 1), 0, StackScales.Length - 1);
        if (layer == 0)
        {
            return new Rect(origin, origin + new Vector2(width, cardHeight));
        }

        var factor = StackScales[layer];
        var layerWidth = width * factor;
        var layerHeight = cardHeight * factor;
        var bottom = origin.Y + cardHeight + layer * StackOffsetY * scale;
        var left = origin.X + (width - layerWidth) * 0.5f;
        return new Rect(new Vector2(left, bottom - layerHeight), new Vector2(left + layerWidth, bottom));
    }

    private static float CollapsedAlpha(int index, int layers)
    {
        if (index >= layers || index >= StackAlphas.Length)
        {
            return 0f;
        }

        return StackAlphas[index];
    }

    private static Rect Lerp(in Rect from, in Rect to, float amount) =>
        new(Vector2.Lerp(from.Min, to.Min, amount), Vector2.Lerp(from.Max, to.Max, amount));

    private static float CollapsedHeight(int count, float scale)
    {
        var layers = NotificationGroups.VisibleLayers(count);
        var height = NotificationCard.Height + Math.Max(0, layers - 1) * StackOffsetY;
        if (count > 1)
        {
            height += MoreLabelHeight;
        }

        return height * scale;
    }

    private static float ExpandedHeight(int count, float scale) =>
        (HeaderHeight + count * NotificationCard.Height + (count - 1) * CardGap) * scale;

    private static float BlockHeight(int count, float progress, float scale)
    {
        if (count <= 1)
        {
            return NotificationCard.Height * scale;
        }

        return float.Lerp(CollapsedHeight(count, scale), ExpandedHeight(count, scale), progress);
    }

    private sealed class GroupState
    {
        public Spring Expand;
        public Spring Press;
        public bool Expanded;
        public bool Pressed;
        public bool Seen;
        public string MoreLabel = string.Empty;

        public GroupState()
        {
            Press.SnapTo(1f);
        }
    }

    private readonly record struct Target(bool IsGroup, string Key, long Id)
    {
        public static Target Of(in Candidate candidate) => new(candidate.IsGroup, candidate.Key, candidate.Id);

        public bool Matches(in Candidate candidate)
        {
            if (IsGroup != candidate.IsGroup)
            {
                return false;
            }

            return IsGroup ? string.Equals(Key, candidate.Key, StringComparison.Ordinal) : Id == candidate.Id;
        }
    }

    private readonly record struct Candidate(
        Rect Rect,
        bool IsGroup,
        string Key,
        long Id,
        float Width,
        PhoneNotification Notification,
        bool ExpandsOnTap);
}
