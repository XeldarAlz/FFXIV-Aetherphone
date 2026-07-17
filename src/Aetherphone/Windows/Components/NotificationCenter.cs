using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Input;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal sealed class NotificationCenter
{
    private const float CardGap = 12f;
    private const float GroupGap = 14f;
    private const float PeekOffsetY = 8f;
    private const float PeekInsetX = 9f;
    private const int MaxPeek = 2;
    private const float HeaderHeight = 28f;
    private const float ClearBarHeight = 40f;
    private const float ListTopPad = 8f;
    private const float EmptyHeight = 72f;
    private const float WheelStep = 48f;
    private const float SwipeMaxReveal = 96f;
    private const float SwipeRightClamp = 10f;
    private const float SwipeCommitFraction = 0.42f;
    private const float TapSlop = 6f;
    private const float ExpandSmoothTime = 0.26f;
    private const float SwipeSmoothTime = 0.18f;
    private readonly NotificationService notifications;
    private readonly NotificationRouter router;
    private readonly Action? navigated;
    private readonly DragTracker drag = new();
    private readonly List<Group> groups = new();
    private readonly Dictionary<string, Group> groupLookup = new();
    private readonly Stack<Group> groupPool = new();
    private readonly Dictionary<string, GroupState> states = new();
    private readonly List<string> staleKeys = new();
    private readonly List<Candidate> candidates = new();
    private Rect interactionBounds;
    private float scrollY;
    private long dragId;
    private string dragKey = string.Empty;
    private bool dragGroup;
    private float dragWidth;
    private PhoneNotification? dragNotification;
    private float swipeOffset;
    private bool animActive;
    private bool animRemoving;
    private long animId;
    private string animKey = string.Empty;
    private bool animGroup;
    private Spring animOffset;
    private float animTarget;

    public NotificationCenter(NotificationService notifications, NotificationRouter router, Action? navigated = null)
    {
        this.notifications = notifications;
        this.router = router;
        this.navigated = navigated;
    }

    public void Reset()
    {
        drag.Cancel();
        dragNotification = null;
        swipeOffset = 0f;
        scrollY = 0f;
        animActive = false;
        states.Clear();
        groups.Clear();
        groupLookup.Clear();
        candidates.Clear();
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawCore(ImGui.GetWindowDrawList(), body, context.Theme, scale, 16f * scale, 1f, true);
    }

    public void DrawOverlay(ImDrawListPtr dl, Rect area, PhoneTheme theme, float opacity, bool interactive)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawCore(dl, area, theme, scale, 0f, opacity, interactive);
    }

    public float MeasureHeight(float scale)
    {
        BuildGroups();
        if (groups.Count == 0)
        {
            return EmptyHeight * scale;
        }

        return (ClearBarHeight + ListTopPad) * scale + ContentHeight(scale);
    }

    private void DrawCore(ImDrawListPtr dl, Rect body, PhoneTheme theme, float scale, float inset, float opacity,
        bool interactive)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        BuildGroups();
        if (groups.Count == 0)
        {
            Typography.DrawCentered(dl, body.Center, Loc.T(L.Notifications.Empty),
                Palette.WithAlpha(theme.TextMuted, opacity));
        }
        else
        {
            var clearBar = new Rect(body.Min, new Vector2(body.Max.X, body.Min.Y + ClearBarHeight * scale));
            DrawClearAll(dl, clearBar, theme, scale, opacity, interactive);
            var listArea = new Rect(new Vector2(body.Min.X + inset, clearBar.Max.Y + ListTopPad * scale),
                new Vector2(body.Max.X - inset, body.Max.Y));
            DrawList(dl, theme, listArea, scale, opacity, interactive);
        }

        AdvanceAnimations(delta);
    }

    private void BuildGroups()
    {
        for (var index = 0; index < groups.Count; index++)
        {
            groupPool.Push(groups[index]);
        }

        groups.Clear();
        groupLookup.Clear();
        var recent = notifications.Recent;
        for (var index = recent.Count - 1; index >= 0; index--)
        {
            var notification = recent[index];
            if (!groupLookup.TryGetValue(notification.StackKey, out var group))
            {
                group = groupPool.Count > 0 ? groupPool.Pop() : new Group();
                group.Reset(notification);
                groupLookup[notification.StackKey] = group;
                groups.Add(group);
            }

            group.Items.Add(notification);
        }

        SyncStates();
    }

    private void SyncStates()
    {
        foreach (var state in states.Values)
        {
            state.Seen = false;
        }

        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            if (!states.TryGetValue(group.Key, out var state))
            {
                state = new GroupState();
                states[group.Key] = state;
            }

            if (group.Items.Count < 2)
            {
                state.Expanded = false;
            }

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
    }

    private void AdvanceAnimations(float delta)
    {
        foreach (var state in states.Values)
        {
            var target = state.Expanded ? 1f : 0f;
            state.Expand.Step(target, ExpandSmoothTime, delta);
            if (state.Expand.IsResting(target, TransitionTiming.RestPositionEpsilon,
                    TransitionTiming.RestVelocityEpsilon))
            {
                state.Expand.SnapTo(target);
            }
        }

        if (!animActive)
        {
            return;
        }

        animOffset.Step(animTarget, SwipeSmoothTime, delta);
        if (animRemoving)
        {
            if (animOffset.Value <= animTarget + 2f)
            {
                PerformRemoval();
                animActive = false;
            }

            return;
        }

        if (animOffset.IsResting(0f, 0.4f, 2f))
        {
            animOffset.SnapTo(0f);
            animActive = false;
        }
    }

    private void PerformRemoval()
    {
        if (animGroup)
        {
            notifications.RemoveGroup(animKey);
        }
        else
        {
            notifications.Remove(animId);
        }
    }

    private void DrawClearAll(ImDrawListPtr dl, Rect bar, PhoneTheme theme, float scale, float opacity,
        bool interactive)
    {
        var label = Loc.T(L.Notifications.ClearAll);
        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var padX = 14f * scale;
        var pillHeight = 26f * scale;
        var pillMax = new Vector2(bar.Max.X, bar.Center.Y + pillHeight * 0.5f);
        var pillMin = new Vector2(pillMax.X - textSize.X - padX * 2f, bar.Center.Y - pillHeight * 0.5f);
        var hovered = interactive && ImGui.IsMouseHoveringRect(pillMin, pillMax);
        var fill = hovered ? theme.Surface : theme.GroupedCard;
        Squircle.Fill(dl, pillMin, pillMax, pillHeight * 0.5f,
            ImGui.GetColorU32(fill with { W = fill.W * opacity }));
        Material.EdgeSquircle(dl, pillMin, pillMax, pillHeight * 0.5f, scale, opacity);
        Typography.Draw(dl, new Vector2(pillMin.X + padX, bar.Center.Y - textSize.Y * 0.5f), label,
            Palette.WithAlpha(theme.Accent, opacity), TextStyles.FootnoteEmphasized.Scale,
            TextStyles.FootnoteEmphasized.Weight);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            notifications.Clear();
            Reset();
        }
    }

    private float ContentHeight(float scale)
    {
        var total = 0f;
        for (var index = 0; index < groups.Count; index++)
        {
            if (!states.TryGetValue(groups[index].Key, out var state))
            {
                continue;
            }

            total += BlockHeight(groups[index].Items.Count, state.Expand.Value, scale) + GroupGap * scale;
        }

        return total;
    }

    private void DrawList(ImDrawListPtr dl, PhoneTheme theme, Rect listArea, float scale, float opacity,
        bool interactive)
    {
        if (drag.Active)
        {
            swipeOffset = Math.Clamp(drag.Delta.X, -SwipeMaxReveal * scale, SwipeRightClamp * scale);
        }

        var maxScroll = MathF.Max(0f, ContentHeight(scale) - listArea.Height);
        if (interactive && !drag.Active && listArea.Contains(ImGui.GetMousePos()))
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                scrollY -= wheel * WheelStep * scale;
            }
        }

        scrollY = Math.Clamp(scrollY, 0f, maxScroll);
        interactionBounds = listArea;
        candidates.Clear();
        dl.PushClipRect(listArea.Min, listArea.Max, true);
        var y = listArea.Min.Y - scrollY;
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            if (!states.TryGetValue(group.Key, out var state))
            {
                continue;
            }

            var progress = state.Expand.Value;
            var blockHeight = BlockHeight(group.Items.Count, progress, scale);
            if (y + blockHeight >= listArea.Min.Y && y <= listArea.Max.Y)
            {
                DrawGroup(dl, group, state, new Vector2(listArea.Min.X, y), listArea.Width, progress, theme, scale,
                    opacity, interactive);
            }

            y += blockHeight + GroupGap * scale;
        }

        dl.PopClipRect();
        HandleGesture(scale, interactive);
    }

    private void DrawGroup(ImDrawListPtr dl, Group group, GroupState state, Vector2 blockOrigin, float width,
        float progress, PhoneTheme theme, float scale, float opacity, bool interactive)
    {
        var itemCount = group.Items.Count;
        var cardHeight = NotificationCard.Height * scale;
        var groupTargeted = TryGroupSlide(group.Key, out var groupSlide);
        for (var index = itemCount - 1; index >= 0; index--)
        {
            var alpha = LayoutAlpha(index, progress) * opacity;
            if (alpha <= 0.01f)
            {
                continue;
            }

            var offsetY = float.Lerp(CollapsedY(index, scale), ExpandedY(index, scale), progress);
            var insetX = float.Lerp(CollapsedInset(index, scale), 0f, progress);
            var cardTop = blockOrigin.Y + offsetY;
            var rect = new Rect(new Vector2(blockOrigin.X + insetX, cardTop),
                new Vector2(blockOrigin.X + width - insetX, cardTop + cardHeight));
            var isGroupCard = !state.Expanded && itemCount > 1 && index == 0;
            var hittable = interactive && IsInteractive(state, index);
            var slide = groupTargeted ? groupSlide : OffsetFor(group.Items[index].Id);
            if (slide < 0f && hittable)
            {
                DrawDeleteAffordance(dl, rect, slide, theme, scale, alpha);
            }

            var drawRect = new Rect(rect.Min + new Vector2(slide, 0f), rect.Max + new Vector2(slide, 0f));
            var shadow = index == 0 ? 0.6f : 0.6f * progress;
            NotificationCard.DrawBase(dl, drawRect, group.Items[index], theme, scale, alpha, shadow);
            if (isGroupCard)
            {
                NotificationCard.DrawCountBadge(dl, NotificationCard.BadgeAnchor(drawRect, scale), itemCount, theme,
                    scale, alpha * (1f - progress));
            }

            if (hittable)
            {
                candidates.Add(new Candidate(drawRect, isGroupCard, group.Key, group.Items[index].Id, width,
                    group.Items[index]));
            }
        }

        if (itemCount > 1 && progress > 0.01f)
        {
            var headerRect = new Rect(blockOrigin,
                new Vector2(blockOrigin.X + width, blockOrigin.Y + HeaderHeight * scale));
            DrawHeader(dl, headerRect, group.Items[0].Title, theme, scale, progress * opacity);
            if (interactive && state.Expanded && progress > 0.5f &&
                ImGui.IsMouseHoveringRect(headerRect.Min, headerRect.Max))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    state.Expanded = false;
                }
            }
        }
    }

    private void DrawHeader(ImDrawListPtr dl, Rect rect, string title, PhoneTheme theme, float scale, float opacity)
    {
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        Typography.Draw(dl, new Vector2(rect.Min.X + 6f * scale, rect.Center.Y - titleSize.Y * 0.5f), title,
            Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.FootnoteEmphasized.Scale,
            TextStyles.FootnoteEmphasized.Weight);
        var label = Loc.T(L.Notifications.ShowLess);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        var labelPos = new Vector2(rect.Max.X - 6f * scale - labelSize.X, rect.Center.Y - labelSize.Y * 0.5f);
        Typography.Draw(dl, labelPos, label, Palette.WithAlpha(theme.Accent, opacity), TextStyles.Footnote.Scale,
            TextStyles.Footnote.Weight);
        var chevronTip = new Vector2(labelPos.X - 10f * scale, rect.Center.Y - 1f * scale);
        var reach = 4f * scale;
        var color = ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, opacity));
        dl.AddLine(new Vector2(chevronTip.X - reach, chevronTip.Y + reach), chevronTip, color, 1.6f * scale);
        dl.AddLine(chevronTip, new Vector2(chevronTip.X + reach, chevronTip.Y + reach), color, 1.6f * scale);
    }

    private static void DrawDeleteAffordance(ImDrawListPtr dl, Rect rect, float slide, PhoneTheme theme, float scale,
        float opacity)
    {
        var revealLeft = rect.Max.X + slide;
        if (revealLeft >= rect.Max.X - 1f)
        {
            return;
        }

        var rounding = 16f * scale;
        Squircle.Fill(dl, new Vector2(revealLeft, rect.Min.Y), rect.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, opacity)));
        var center = new Vector2((revealLeft + rect.Max.X) * 0.5f, rect.Center.Y);
        var available = rect.Max.X - revealLeft;
        var reach = MathF.Min(7f * scale, available * 0.28f);
        if (reach <= 1f)
        {
            return;
        }

        var color = ImGui.GetColorU32(Palette.WithAlpha(new Vector4(1f, 1f, 1f, 1f), opacity));
        dl.AddLine(new Vector2(center.X - reach, center.Y - reach), new Vector2(center.X + reach, center.Y + reach),
            color, 2f * scale);
        dl.AddLine(new Vector2(center.X - reach, center.Y + reach), new Vector2(center.X + reach, center.Y - reach),
            color, 2f * scale);
    }

    private void HandleGesture(float scale, bool interactive)
    {
        if (!drag.Active && interactive)
        {
            var mouse = ImGui.GetMousePos();
            if (interactionBounds.Contains(mouse))
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    var candidate = candidates[index];
                    if (!candidate.Rect.Contains(mouse))
                    {
                        continue;
                    }

                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (drag.Begin(candidate.Rect))
                    {
                        BeginDrag(candidate);
                    }

                    break;
                }
            }
        }

        if (drag.Released(out var totalDelta, out _))
        {
            ResolveGesture(totalDelta, scale);
        }
    }

    private void BeginDrag(in Candidate candidate)
    {
        if (animActive && animGroup == candidate.IsGroup &&
            (candidate.IsGroup ? animKey == candidate.Key : animId == candidate.Id))
        {
            animActive = false;
        }

        dragGroup = candidate.IsGroup;
        dragKey = candidate.Key;
        dragId = candidate.Id;
        dragWidth = candidate.Width;
        dragNotification = candidate.Notification;
        swipeOffset = 0f;
    }

    private void ResolveGesture(Vector2 totalDelta, float scale)
    {
        var slop = TapSlop * scale;
        if (MathF.Abs(totalDelta.X) < slop && MathF.Abs(totalDelta.Y) < slop)
        {
            HandleTap();
            dragNotification = null;
            return;
        }

        var commit = totalDelta.X <= -dragWidth * SwipeCommitFraction;
        animGroup = dragGroup;
        animKey = dragKey;
        animId = dragId;
        animRemoving = commit;
        animTarget = commit ? -(dragWidth + 40f * scale) : 0f;
        animOffset.SnapTo(swipeOffset);
        animActive = true;
        swipeOffset = 0f;
        dragNotification = null;
    }

    private void HandleTap()
    {
        if (dragGroup)
        {
            if (states.TryGetValue(dragKey, out var state))
            {
                state.Expanded = true;
            }

            return;
        }

        if (dragNotification is { } notification)
        {
            router.Open(notification);
            navigated?.Invoke();
        }
    }

    private bool TryGroupSlide(string key, out float slide)
    {
        if (drag.Active && dragGroup && dragKey == key)
        {
            slide = swipeOffset;
            return true;
        }

        if (animActive && animGroup && animKey == key)
        {
            slide = animOffset.Value;
            return true;
        }

        slide = 0f;
        return false;
    }

    private float OffsetFor(long id)
    {
        if (drag.Active && !dragGroup && id == dragId)
        {
            return swipeOffset;
        }

        if (animActive && !animGroup && id == animId)
        {
            return animOffset.Value;
        }

        return 0f;
    }

    private static bool IsInteractive(GroupState state, int index) => state.Expanded || index == 0;

    private static float LayoutAlpha(int index, float progress)
    {
        var collapsed = index switch
        {
            0 => 1f,
            1 => 0.55f,
            2 => 0.30f,
            _ => 0f,
        };
        return float.Lerp(collapsed, 1f, progress);
    }

    private static float CollapsedY(int index, float scale) => MathF.Min(index, MaxPeek) * PeekOffsetY * scale;
    private static float CollapsedInset(int index, float scale) => MathF.Min(index, MaxPeek) * PeekInsetX * scale;

    private static float ExpandedY(int index, float scale) =>
        HeaderHeight * scale + index * (NotificationCard.Height + CardGap) * scale;

    private static float BlockHeight(int itemCount, float progress, float scale)
    {
        var collapsed = (NotificationCard.Height + MathF.Min(itemCount - 1, MaxPeek) * PeekOffsetY) * scale;
        var expanded = (HeaderHeight + itemCount * NotificationCard.Height + (itemCount - 1) * CardGap) * scale;
        return float.Lerp(collapsed, expanded, progress);
    }

    private sealed class Group
    {
        public string Key = string.Empty;
        public readonly List<PhoneNotification> Items = new();

        public void Reset(PhoneNotification first)
        {
            Key = first.StackKey;
            Items.Clear();
        }
    }

    private sealed class GroupState
    {
        public Spring Expand;
        public bool Expanded;
        public bool Seen;
    }

    private readonly record struct Candidate(
        Rect Rect,
        bool IsGroup,
        string Key,
        long Id,
        float Width,
        PhoneNotification Notification);
}
