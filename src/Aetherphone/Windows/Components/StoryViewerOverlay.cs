using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
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
internal sealed class StoryViewerOverlay
{
    private const float SecondsPerStory = 5f;
    private const float RevealSmoothTime = 0.15f;
    private const float DismissDragDistance = 140f;
    private const float HoldPauseSeconds = 0.18f;
    private const float TapZoneFraction = 0.32f;

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

    public StoryViewerOverlay(RemoteImageCache images, LodestoneService lodestone)
    {
        this.images = images;
        this.lodestone = lodestone;
    }

    public bool Active => open || reveal.Value > 0.01f;
    public StoryDto? Current => index >= 0 && index < stories.Length ? stories[index] : null;

    public void Open(StoryDto[] items, string label, string? avatarUrl, Action<StoryDto> seen, bool deletable = false,
        Action<StoryDto>? delete = null, Action? exhausted = null)
    {
        stories = items;
        authorLabel = label;
        authorAvatarUrl = avatarUrl;
        canDelete = deletable;
        onSeen = seen;
        onDelete = delete;
        onExhausted = exhausted;
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

        if (open && !suspended)
        {
            HandleInput(area, delta, images.Get(story.MediaUrl) is not null);
        }

        var shift = new Vector2(0f, dragOffset * scale);
        var contentTop = area.Min.Y + theme.TopZoneHeight * scale;
        var stage = new Rect(new Vector2(area.Min.X, contentTop + 44f * scale) + shift,
            new Vector2(area.Max.X, area.Max.Y - 16f * scale) + shift);
        DrawImage(drawList, stage, story, scale);
        DrawCaption(drawList, stage, story, scale);
        DrawProgress(drawList, new Rect(new Vector2(area.Min.X + 12f * scale, contentTop + 8f * scale) + shift,
            new Vector2(area.Max.X - 12f * scale, contentTop + 11f * scale) + shift), scale);
        DrawHeader(new Rect(new Vector2(area.Min.X + 12f * scale, contentTop + 18f * scale) + shift,
            new Vector2(area.Max.X - 12f * scale, contentTop + 42f * scale) + shift), theme, story, scale);
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
                Loc.T(images.Failed(story.MediaUrl) ? L.Aethergram.ImageFailed : L.Common.Loading),
                new Vector4(1f, 1f, 1f, 0.7f), TextStyles.Subheadline);
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, stage.Width, stage.Height);
        drawList.AddImageRounded(texture.Handle, stage.Min, stage.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
    }

    private void DrawCaption(ImDrawListPtr drawList, Rect stage, StoryDto story, float scale)
    {
        if (story.Caption.Length == 0)
        {
            return;
        }

        var width = stage.Width - 32f * scale;
        var height = Typography.MeasureWrapped(story.Caption, width, TextStyles.Body.Scale, TextStyles.Body.Weight);
        var bandTop = stage.Max.Y - height - 30f * scale;
        drawList.AddRectFilled(new Vector2(stage.Min.X, bandTop - 12f * scale), new Vector2(stage.Max.X, stage.Max.Y),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f)));
        Typography.DrawWrappedCentered(drawList, new Vector2(stage.Center.X, bandTop + height * 0.5f), story.Caption,
            new Vector4(1f, 1f, 1f, 0.96f), TextStyles.Body, width);
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
