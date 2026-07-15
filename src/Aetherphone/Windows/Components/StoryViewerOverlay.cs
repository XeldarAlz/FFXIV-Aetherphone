using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

/// <summary>
/// Fullscreen story player: segmented progress, timed auto advance, tap left/right to step,
/// press and hold to pause, drag down to dismiss. Draws over the whole app like
/// <see cref="PhotoViewerOverlay"/> rather than routing, so the app returns early while it is active.
/// </summary>
internal readonly record struct StoryViewers(StoryViewerDto[] Items, int Total, bool Loading);

internal sealed class StoryViewerOverlay
{
    private const float SecondsPerStory = 5f;
    private const float SheetSmoothTime = 0.16f;
    private const float SheetHeightFraction = 0.58f;
    private const float SheetRowHeight = 46f;
    private const float RevealSmoothTime = 0.15f;
    private const float DismissDragDistance = 140f;
    private const float HoldPauseSeconds = 0.18f;
    private const float TapZoneFraction = 0.32f;
    private const float FooterInset = 16f;
    private const float FooterGap = 10f;
    private const float SeenPillHeight = 30f;
    private const float ScrimFadeHeight = 44f;
    private const float SeenHoverSmoothTime = 0.12f;

    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private Spring reveal;
    private StoryDto[] stories = Array.Empty<StoryDto>();
    private string authorLabel = string.Empty;
    private string? authorAvatarUrl;
    private bool canDelete;
    private int index;
    private float elapsed;
    private bool open;
    private bool holding;
    private double pressStartedAt;
    private Vector2 pressOrigin;
    private float dragOffset;
    private Action<StoryDto>? onSeen;
    private Action<StoryDto>? onDelete;
    private Action? onExhausted;
    private Func<StoryDto, StoryViewers>? viewersSource;
    private Spring sheetReveal;
    private Spring seenHover;
    private bool sheetOpen;

    public StoryViewerOverlay(RemoteImageCache images, LodestoneService lodestone)
    {
        this.images = images;
        this.lodestone = lodestone;
    }

    public bool Active => open || reveal.Value > 0.01f;
    public StoryDto? Current => index >= 0 && index < stories.Length ? stories[index] : null;

    public void Open(StoryDto[] items, string label, string? avatarUrl, Action<StoryDto> seen, bool mine = false,
        Action<StoryDto>? delete = null, Func<StoryDto, StoryViewers>? viewers = null, Action? exhausted = null)
    {
        stories = items;
        authorLabel = label;
        authorAvatarUrl = avatarUrl;
        canDelete = mine;
        onSeen = seen;
        onDelete = delete;
        viewersSource = viewers;
        onExhausted = exhausted;
        sheetOpen = false;
        sheetReveal = new Spring(0f);
        seenHover = new Spring(0f);
        index = FirstUnseen(items);
        elapsed = 0f;
        dragOffset = 0f;
        holding = false;
        open = true;
        ReportSeen();
    }

    public void Replace(StoryDto[] items)
    {
        if (!open)
        {
            return;
        }

        stories = items;
        if (items.Length == 0)
        {
            Close();
            return;
        }

        index = Math.Clamp(index, 0, items.Length - 1);
        ReportSeen();
    }

    public void Close()
    {
        open = false;
        holding = false;
        sheetOpen = false;
    }

    public void Reset()
    {
        Close();
        reveal = new Spring(0f);
        sheetReveal = new Spring(0f);
        seenHover = new Spring(0f);
        stories = Array.Empty<StoryDto>();
        onSeen = null;
        onDelete = null;
        onExhausted = null;
        viewersSource = null;
    }

    /// <param name="suspended">
    /// Holds the story on screen and ignores input while something modal is above the viewer, such as
    /// the delete confirmation. Without it the timer keeps running underneath and can advance off, or
    /// close, the very story being acted on.
    /// </param>
    public void Draw(Rect area, PhoneTheme theme, bool suspended = false)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
        var eased = Math.Clamp(reveal.Value, 0f, 1f);
        if (eased <= 0.01f)
        {
            if (!open)
            {
                stories = Array.Empty<StoryDto>();
                onSeen = null;
                onDelete = null;
                onExhausted = null;
            }

            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var dim = 0.97f * eased * (1f - Math.Clamp(dragOffset / (DismissDragDistance * 2f), 0f, 0.45f));
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)));

        var story = Current;
        if (story is null)
        {
            Typography.DrawCentered(area.Center, Loc.T(L.Common.Loading), new Vector4(1f, 1f, 1f, 0.7f));
            return;
        }

        sheetReveal.Step(sheetOpen ? 1f : 0f, SheetSmoothTime, delta);
        if (open && !suspended && !sheetOpen)
        {
            HandleInput(area, delta, images.Get(story.MediaUrl) is not null);
        }

        var shift = new Vector2(0f, dragOffset * scale);
        var contentTop = area.Min.Y + theme.TopZoneHeight * scale;
        var stage = new Rect(new Vector2(area.Min.X, contentTop + 44f * scale) + shift,
            new Vector2(area.Max.X, area.Max.Y - 16f * scale) + shift);
        DrawImage(drawList, stage, story, scale);
        DrawFooter(drawList, stage, story, scale, delta);
        DrawProgress(drawList, new Rect(new Vector2(area.Min.X + 12f * scale, contentTop + 8f * scale) + shift,
            new Vector2(area.Max.X - 12f * scale, contentTop + 11f * scale) + shift), scale);
        DrawHeader(new Rect(new Vector2(area.Min.X + 12f * scale, contentTop + 18f * scale) + shift,
            new Vector2(area.Max.X - 12f * scale, contentTop + 42f * scale) + shift), theme, story, scale);
        DrawViewersSheet(area, theme, story, scale);
    }

    /// <summary>
    /// Stacks the caption above the seen pill against the bottom of the stage under one scrim, so the two
    /// cannot land on top of each other the way separately bottom-anchored blocks did.
    /// </summary>
    private void DrawFooter(ImDrawListPtr drawList, Rect stage, StoryDto story, float scale, float delta)
    {
        var showSeen = ShowSeenPill;
        var hasCaption = story.Caption.Length > 0;
        if (!showSeen && !hasCaption)
        {
            return;
        }

        var inset = FooterInset * scale;
        var captionWidth = stage.Width - inset * 2f;
        var captionHeight = hasCaption
            ? Typography.MeasureWrappedBlock(story.Caption, TextStyles.Body, captionWidth).Y
            : 0f;
        var seenHeight = showSeen ? SeenPillHeight * scale : 0f;
        var gap = hasCaption && showSeen ? FooterGap * scale : 0f;
        var bottom = stage.Max.Y - inset;
        var top = MathF.Max(stage.Min.Y + inset, bottom - captionHeight - gap - seenHeight);
        var scrimTop = MathF.Max(stage.Min.Y, top - ScrimFadeHeight * scale);
        Squircle.FillVerticalGradient(drawList, new Vector2(stage.Min.X, scrimTop), stage.Max,
            Metrics.Radius.Md * scale, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f)),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.78f)));
        if (hasCaption)
        {
            Typography.DrawWrappedCentered(new Vector2(stage.Center.X, top), story.Caption,
                new Vector4(1f, 1f, 1f, 0.96f), TextStyles.Body, captionWidth);
        }

        if (showSeen)
        {
            DrawSeenPill(drawList, new Vector2(stage.Min.X + inset, bottom - seenHeight), story, seenHeight, scale,
                delta);
        }
    }

    // The eye and count only exist on your own story: the server reports ViewCount as zero to anyone
    // who is not the author, so this would silently read "0" for everyone else.
    private bool ShowSeenPill => canDelete && viewersSource is not null;

    /// <summary>
    /// The chip sits on an arbitrary photo, so hover has to read against both a bright and a dark backdrop:
    /// the fill deepens, a hairline ring fades in, and the ink lifts to full white together.
    /// </summary>
    private void DrawSeenPill(ImDrawListPtr drawList, Vector2 origin, StoryDto story, float height, float scale,
        float delta)
    {
        var label = Loc.Plural(L.Story.SeenBy, story.ViewCount);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var padding = 12f * scale;
        var iconWidth = 11f * scale;
        var iconGap = 7f * scale;
        var max = new Vector2(origin.X + padding * 2f + iconWidth + iconGap + size.X, origin.Y + height);
        var radius = height * 0.5f;
        var centerY = origin.Y + height * 0.5f;
        var hovered = UiInteract.Hover(origin, max);
        seenHover.Step(hovered ? 1f : 0f, SeenHoverSmoothTime, delta);
        var hover = Math.Clamp(seenHover.Value, 0f, 1f);
        var press = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 0.1f : 0f;
        Squircle.Fill(drawList, origin, max, radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f + 0.25f * hover + press)));
        if (hover > 0.001f)
        {
            Squircle.Stroke(drawList, origin, max, radius,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.3f * hover)), 1f * scale);
        }

        var ink = new Vector4(1f, 1f, 1f, 0.9f + 0.1f * hover);
        AppSkin.Icon(new Vector2(origin.X + padding + iconWidth * 0.5f, centerY), FontAwesomeIcon.Eye.ToIconString(),
            ink, 0.8f);
        Typography.Draw(new Vector2(origin.X + padding + iconWidth + iconGap, centerY - size.Y * 0.5f), label, ink,
            TextStyles.FootnoteEmphasized);
        if (UiInteract.HoverClick(origin, max))
        {
            sheetOpen = true;
        }
    }

    private void DrawViewersSheet(Rect area, PhoneTheme theme, StoryDto story, float scale)
    {
        var reveal = Math.Clamp(sheetReveal.Value, 0f, 1f);
        if (reveal <= 0.01f || viewersSource is null)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.5f * reveal)));
        if (UiInteract.HoverClick(area.Min, area.Max))
        {
            sheetOpen = false;
        }

        var height = area.Height * SheetHeightFraction;
        var top = area.Max.Y - height * Easing.EaseOutQuint(reveal);
        var panel = new Rect(new Vector2(area.Min.X, top), area.Max);
        var rounding = Metrics.Radius.Lg * scale;
        Squircle.Fill(drawList, panel.Min, new Vector2(panel.Max.X, panel.Max.Y + rounding), rounding,
            ImGui.GetColorU32(theme.Surface));
        drawList.AddRectFilled(new Vector2(panel.Center.X - 18f * scale, panel.Min.Y + 8f * scale),
            new Vector2(panel.Center.X + 18f * scale, panel.Min.Y + 11f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f)), 2f * scale);

        var viewers = viewersSource(story);
        var headerY = panel.Min.Y + 26f * scale;
        Typography.DrawCentered(new Vector2(panel.Center.X, headerY),
            Loc.Plural(L.Story.SeenBy, story.ViewCount), theme.TextStrong, TextStyles.Headline);
        var listRect = new Rect(new Vector2(panel.Min.X, headerY + 18f * scale), panel.Max);
        if (viewers.Items.Length == 0)
        {
            Typography.DrawCentered(new Vector2(panel.Center.X, listRect.Min.Y + 40f * scale),
                Loc.T(viewers.Loading ? L.Common.Loading : L.Story.NoViewers), theme.TextMuted,
                TextStyles.Subheadline);
            return;
        }

        using (AppSurface.Begin(listRect))
        {
            for (var index = 0; index < viewers.Items.Length; index++)
            {
                DrawViewerRow(viewers.Items[index], theme, scale);
            }

            if (viewers.Items.Length < viewers.Total)
            {
                Typography.DrawCentered(
                    new Vector2(listRect.Center.X, ImGui.GetCursorScreenPos().Y + 14f * scale),
                    Loc.T(L.Story.ViewersTrimmed, viewers.Items.Length, viewers.Total), theme.TextMuted,
                    TextStyles.Caption1);
                ImGui.Dummy(new Vector2(0f, 30f * scale));
            }
        }
    }

    private void DrawViewerRow(StoryViewerDto viewer, PhoneTheme theme, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = SheetRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var radius = 16f * scale;
        var center = new Vector2(origin.X + radius + 4f * scale, origin.Y + height * 0.5f);
        var name = SocialIdentity.Name(viewer.DisplayName, viewer.Handle);
        AvatarView.DrawRemote(drawList, center, radius, theme, name, string.Empty, viewer.AvatarUrl, images, lodestone,
            0.8f, 28);
        var left = center.X + radius + 10f * scale;
        var nameSize = Typography.Measure(name, TextStyles.Subheadline);
        Typography.Draw(new Vector2(left, center.Y - nameSize.Y * 0.5f), name, theme.TextStrong,
            TextStyles.Subheadline);
        var stamp = TimeText.Short(viewer.ViewedAtUnix);
        var stampSize = Typography.Measure(stamp, TextStyles.Caption1);
        Typography.Draw(new Vector2(origin.X + width - stampSize.X - 6f * scale, center.Y - stampSize.Y * 0.5f), stamp,
            theme.TextMuted, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void HandleInput(Rect area, float delta, bool imageReady)
    {
        var hovering = ImGui.IsMouseHoveringRect(area.Min, area.Max);
        if (hovering && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressStartedAt = ImGui.GetTime();
            pressOrigin = ImGui.GetIO().MousePos;
        }

        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var heldFor = ImGui.GetTime() - pressStartedAt;
        if (down && hovering)
        {
            var travel = ImGui.GetIO().MousePos.Y - pressOrigin.Y;
            dragOffset = MathF.Max(0f, travel / ImGuiHelpers.GlobalScale);
            holding = heldFor >= HoldPauseSeconds && dragOffset < 8f;
        }
        else
        {
            holding = false;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            var wasDrag = dragOffset >= DismissDragDistance;
            var wasTap = heldFor < HoldPauseSeconds && dragOffset < 8f;
            dragOffset = 0f;
            if (wasDrag)
            {
                Close();
                return;
            }

            if (wasTap && hovering)
            {
                var x = ImGui.GetIO().MousePos.X;
                if (x <= area.Min.X + area.Width * TapZoneFraction)
                {
                    Step(-1);
                }
                else if (x >= area.Max.X - area.Width * TapZoneFraction)
                {
                    Step(1);
                }
            }
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Close();
            return;
        }

        if (holding || !imageReady)
        {
            return;
        }

        elapsed += delta;
        if (elapsed >= SecondsPerStory)
        {
            Step(1);
        }
    }

    private void Step(int direction)
    {
        var next = index + direction;
        if (next < 0)
        {
            elapsed = 0f;
            return;
        }

        if (next >= stories.Length)
        {
            Close();
            onExhausted?.Invoke();
            return;
        }

        index = next;
        elapsed = 0f;
        ReportSeen();
    }

    private void ReportSeen()
    {
        if (Current is { } story)
        {
            onSeen?.Invoke(story);
        }
    }

    private void DrawImage(ImDrawListPtr drawList, Rect stage, StoryDto story, float scale)
    {
        var rounding = Metrics.Radius.Md * scale;
        var texture = images.Get(story.MediaUrl);
        if (texture is null)
        {
            Squircle.Fill(drawList, stage.Min, stage.Max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)));
            Typography.DrawCentered(stage.Center,
                Loc.T(images.Failed(story.MediaUrl) ? L.Common.ImageFailed : L.Common.Loading),
                new Vector4(1f, 1f, 1f, 0.7f), TextStyles.Subheadline);
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, stage.Width, stage.Height);
        drawList.AddImageRounded(texture.Handle, stage.Min, stage.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (!ContentModeration.IsInReview(story.ScanStatus))
        {
            return;
        }

        Squircle.Fill(drawList, stage.Min, stage.Max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)));
        var center = stage.Center;
        AppSkin.Icon(drawList, new Vector2(center.X, center.Y - 26f * scale), FontAwesomeIcon.Hourglass.ToIconString(),
            new Vector4(1f, 1f, 1f, 0.92f), 1.6f);
        Typography.DrawCentered(drawList, center, Loc.T(L.Moderation.InReview), new Vector4(1f, 1f, 1f, 0.95f),
            TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + 22f * scale),
            Loc.T(L.Moderation.InReviewHint), new Vector4(1f, 1f, 1f, 0.75f), TextStyles.Footnote);
    }

    private void DrawProgress(ImDrawListPtr drawList, Rect bar, float scale)
    {
        if (stories.Length == 0)
        {
            return;
        }

        var gap = 3f * scale;
        var slot = (bar.Width - gap * (stories.Length - 1)) / stories.Length;
        var rounding = bar.Height * 0.5f;
        for (var slotIndex = 0; slotIndex < stories.Length; slotIndex++)
        {
            var left = bar.Min.X + (slot + gap) * slotIndex;
            var right = left + slot;
            drawList.AddRectFilled(new Vector2(left, bar.Min.Y), new Vector2(right, bar.Max.Y),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.28f)), rounding);
            var fill = slotIndex < index ? 1f
                : slotIndex > index ? 0f
                : Math.Clamp(elapsed / SecondsPerStory, 0f, 1f);
            if (fill <= 0f)
            {
                continue;
            }

            drawList.AddRectFilled(new Vector2(left, bar.Min.Y),
                new Vector2(left + slot * fill, bar.Max.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)),
                rounding);
        }
    }

    private void DrawHeader(Rect row, PhoneTheme theme, StoryDto story, float scale)
    {
        var radius = 12f * scale;
        var center = new Vector2(row.Min.X + radius, row.Center.Y);
        AvatarView.DrawRemote(ImGui.GetWindowDrawList(), center, radius, theme, authorLabel, string.Empty,
            authorAvatarUrl, images, lodestone, 0.8f, 24);
        var left = center.X + radius + 9f * scale;
        var nameSize = Typography.Measure(authorLabel, TextStyles.SubheadlineEmphasized);
        Typography.Draw(new Vector2(left, row.Center.Y - nameSize.Y * 0.5f), authorLabel,
            new Vector4(1f, 1f, 1f, 0.98f), TextStyles.SubheadlineEmphasized);
        Typography.Draw(new Vector2(left + nameSize.X + 8f * scale, row.Center.Y - nameSize.Y * 0.5f + 1f * scale),
            TimeText.Short(story.CreatedAtUnix), new Vector4(1f, 1f, 1f, 0.6f), TextStyles.Footnote);

        var hit = new Vector2(14f * scale, 14f * scale);
        var closeCenter = new Vector2(row.Max.X - 10f * scale, row.Center.Y);
        AppSkin.Icon(closeCenter, FontAwesomeIcon.Times.ToIconString(), new Vector4(1f, 1f, 1f, 0.9f), 1.1f);
        if (UiInteract.HoverClick(closeCenter - hit, closeCenter + hit))
        {
            Close();
        }

        if (!canDelete)
        {
            return;
        }

        var deleteCenter = new Vector2(closeCenter.X - 32f * scale, row.Center.Y);
        AppSkin.Icon(deleteCenter, FontAwesomeIcon.Trash.ToIconString(), new Vector4(1f, 1f, 1f, 0.82f), 1f);
        if (UiInteract.HoverClick(deleteCenter - hit, deleteCenter + hit))
        {
            onDelete?.Invoke(story);
        }
    }

    private static int FirstUnseen(StoryDto[] items)
    {
        for (var itemIndex = 0; itemIndex < items.Length; itemIndex++)
        {
            if (!items[itemIndex].Seen)
            {
                return itemIndex;
            }
        }

        return 0;
    }
}
