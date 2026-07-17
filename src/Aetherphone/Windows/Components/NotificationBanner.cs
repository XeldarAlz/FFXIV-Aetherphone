using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal sealed class NotificationBanner : IDisposable
{
    private enum Stage
    {
        Idle,
        Enter,
        Hold,
        Exit,
    }

    private const float EnterSmoothTime = 0.16f;
    private const float HoldSeconds = 4.0f;
    private const float ExitSmoothTime = 0.12f;
    private const float SideMargin = 8f;
    private const float BannerHeight = 64f;
    private const float RestTopOffset = 40f;
    private const float HiddenGap = 8f;
    private const float CornerRadius = 22f;
    private const float Padding = 13f;
    private const float IconSize = 38f;
    private const float TextGap = 11f;
    private const float BodyOffset = 20f;
    private const float DragSlop = 5f;
    private const float DismissDistance = 16f;
    private const float DismissVelocity = 700f;
    private const float DownwardGive = 26f;
    private const int MaxQueued = 4;

    private readonly NotificationService notifications;
    private readonly Func<string?> currentAppId;
    private readonly NotificationRouter router;
    private readonly Queue<PhoneNotification> pending = new();
    private Spring enter;
    private Spring exit;
    private PhoneNotification? active;
    private Stage stage = Stage.Idle;
    private float holdElapsed;
    private bool holdPaused;
    private bool dragging;
    private bool dragMoved;
    private float dragStartY;
    private float dragOffset;
    private float dragLastY;
    private float dragVelocity;
    private float exitFromOffset;

    public NotificationBanner(NotificationService notifications, Func<string?> currentAppId, NotificationRouter router)
    {
        this.notifications = notifications;
        this.currentAppId = currentAppId;
        this.router = router;
        notifications.Presented += OnPresented;
    }

    public event Action? Shown;

    public bool CapturesPointer(Rect screen)
    {
        if (stage is Stage.Idle or Stage.Exit || active is null)
        {
            return false;
        }

        if (dragging)
        {
            return true;
        }

        var bounds = CurrentBounds(screen, ImGuiHelpers.GlobalScale, out _);
        return ImGui.IsMouseHoveringRect(bounds.Min, bounds.Max);
    }

    public void Advance(float deltaSeconds)
    {
        if (stage == Stage.Idle)
        {
            return;
        }

        if (stage == Stage.Hold)
        {
            if (!dragging)
            {
                dragOffset += (0f - dragOffset) * MathF.Min(1f, deltaSeconds * 18f);
            }

            if (holdPaused || dragging)
            {
                holdElapsed = 0f;
            }
            else
            {
                holdElapsed += deltaSeconds;
                if (holdElapsed >= HoldSeconds)
                {
                    BeginExit();
                }
            }

            holdPaused = false;
            return;
        }

        if (stage == Stage.Enter)
        {
            enter.Step(1f, EnterSmoothTime, deltaSeconds);
            if (enter.IsResting(1f, 0.004f, 0.05f))
            {
                enter.SnapTo(1f);
                stage = Stage.Hold;
                holdElapsed = 0f;
            }

            return;
        }

        exit.Step(1f, ExitSmoothTime, deltaSeconds);
        if (!exit.IsResting(1f, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
        {
            return;
        }

        exit.SnapTo(1f);
        if (pending.Count > 0)
        {
            BeginNext();
        }
        else
        {
            active = null;
            stage = Stage.Idle;
        }
    }

    public void Draw(Rect screen, PhoneTheme theme)
    {
        if (stage == Stage.Idle || active is not { } notification)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var bounds = CurrentBounds(screen, scale, out var opacity);
        var hovered = stage != Stage.Exit && ImGui.IsMouseHoveringRect(bounds.Min, bounds.Max);
        if (hovered || dragging)
        {
            holdPaused = true;
        }

        var dl = ImGui.GetForegroundDrawList();
        dl.PushClipRect(screen.Min, screen.Max, true);
        DrawCard(dl, notification, theme, bounds.Min, bounds.Max, scale, opacity, hovered || dragging);
        dl.PopClipRect();
        HandleGesture(notification, bounds, scale, hovered);
    }

    private void HandleGesture(PhoneNotification notification, Rect bounds, float scale, bool hovered)
    {
        if (stage != Stage.Hold)
        {
            return;
        }

        var mouse = ImGui.GetMousePos();
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (hovered && !dragging && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            dragMoved = false;
            dragStartY = mouse.Y;
            dragLastY = mouse.Y;
            dragVelocity = 0f;
        }

        if (!dragging)
        {
            return;
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (delta > 0f)
            {
                dragVelocity = (mouse.Y - dragLastY) / delta;
            }

            dragLastY = mouse.Y;
            var moved = mouse.Y - dragStartY;
            dragMoved = dragMoved || MathF.Abs(moved) > DragSlop * scale;
            dragOffset = moved < 0f ? moved : Rubber(moved, scale);
            return;
        }

        dragging = false;
        if (!dragMoved)
        {
            router.Open(notification);
            BeginExit();
            return;
        }

        if (dragOffset < -DismissDistance * scale || dragVelocity < -DismissVelocity * scale)
        {
            BeginExit();
        }
    }

    private static float Rubber(float pull, float scale)
    {
        var give = DownwardGive * scale;
        return give * pull / (pull + give * 2.4f);
    }

    private Rect CurrentBounds(Rect screen, float scale, out float opacity)
    {
        var height = BannerHeight * scale;
        var restTop = screen.Min.Y + RestTopOffset * scale;
        var hiddenTop = screen.Min.Y - height - HiddenGap * scale;
        float top;
        if (stage == Stage.Enter)
        {
            top = Easing.Lerp(hiddenTop, restTop, enter.Value);
            opacity = Math.Clamp(enter.Value * 1.8f, 0f, 1f);
        }
        else if (stage == Stage.Exit)
        {
            top = Easing.Lerp(restTop + exitFromOffset, hiddenTop, exit.Value);
            opacity = 1f - exit.Value;
        }
        else
        {
            top = restTop + dragOffset;
            opacity = 1f;
        }

        var min = new Vector2(screen.Min.X + SideMargin * scale, top);
        var max = new Vector2(screen.Max.X - SideMargin * scale, top + height);
        return new Rect(min, max);
    }

    private static void DrawCard(ImDrawListPtr dl, PhoneNotification notification, PhoneTheme theme, Vector2 min,
        Vector2 max, float scale, float opacity, bool hovered)
    {
        var rounding = CornerRadius * scale;
        Elevation.Floating(dl, min, max, rounding, scale, opacity);
        var cardColor = Palette.Mix(theme.GroupedCard, theme.TextStrong, hovered ? 0.11f : 0.06f);
        Squircle.Fill(dl, min, max, rounding, Color(Palette.WithAlpha(cardColor, 0.99f), opacity));
        var strokeColor = hovered
            ? Palette.WithAlpha(notification.Accent, 0.55f)
            : Palette.WithAlpha(theme.TextStrong, 0.10f);
        Squircle.Stroke(dl, min, max, rounding, Color(strokeColor, opacity), (hovered ? 1.5f : 1f) * scale);
        var iconExtent = IconSize * scale * 0.5f;
        var iconCenter = new Vector2(min.X + Padding * scale + iconExtent, (min.Y + max.Y) * 0.5f);
        var iconMin = new Vector2(iconCenter.X - iconExtent, iconCenter.Y - iconExtent);
        var iconMax = new Vector2(iconCenter.X + iconExtent, iconCenter.Y + iconExtent);
        Squircle.Fill(dl, iconMin, iconMax, iconExtent * 0.52f, Color(notification.Accent, opacity));
        var ink = Palette.WithAlpha(theme.TextStrong, opacity);
        if (!AppIconArt.TryDraw(dl, notification.AppId, iconCenter, IconSize * scale, ink,
                Palette.WithAlpha(notification.Accent, opacity)))
        {
            var initial = notification.Title.Length > 0 ? notification.Title.Substring(0, 1) : "?";
            Typography.DrawCentered(dl, iconCenter, initial, ink, 1.1f);
        }

        var textLeft = iconMax.X + TextGap * scale;
        var textRight = max.X - Padding * scale;
        var titleTop = min.Y + Padding * scale;
        var time = TimeText.Short(notification.ReceivedAt);
        var timeSize = Typography.Measure(time, 0.78f);
        Typography.Draw(dl, new Vector2(textRight - timeSize.X, titleTop + 1f * scale), time,
            Palette.WithAlpha(theme.TextMuted, opacity), 0.78f);
        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(textRight - timeSize.X - 6f * scale, max.Y), true);
        Typography.Draw(dl, new Vector2(textLeft, titleTop), notification.Title, ink, 0.94f, FontWeight.SemiBold);
        dl.PopClipRect();
        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(textRight, max.Y), true);
        Typography.Draw(dl, new Vector2(textLeft, titleTop + BodyOffset * scale), notification.Body,
            Palette.WithAlpha(theme.TextMuted, opacity), 0.88f);
        dl.PopClipRect();
    }

    private void OnPresented(PhoneNotification notification)
    {
        if (currentAppId() == notification.AppId)
        {
            return;
        }

        if (active is { } showing && showing.StackKey == notification.StackKey && stage is Stage.Enter or Stage.Hold)
        {
            active = notification;
            holdElapsed = 0f;
            Shown?.Invoke();
            return;
        }

        RemoveQueuedGroup(notification.StackKey);
        if (pending.Count >= MaxQueued)
        {
            return;
        }

        pending.Enqueue(notification);
        Shown?.Invoke();
        if (stage == Stage.Idle)
        {
            BeginNext();
        }
    }

    private void RemoveQueuedGroup(string stackKey)
    {
        var count = pending.Count;
        for (var index = 0; index < count; index++)
        {
            var queued = pending.Dequeue();
            if (queued.StackKey != stackKey)
            {
                pending.Enqueue(queued);
            }
        }
    }

    private void BeginNext()
    {
        active = pending.Dequeue();
        stage = Stage.Enter;
        holdElapsed = 0f;
        dragOffset = 0f;
        dragging = false;
        enter.SnapTo(0f);
    }

    private void BeginExit()
    {
        exitFromOffset = dragOffset;
        dragging = false;
        dragOffset = 0f;
        stage = Stage.Exit;
        exit.SnapTo(0f);
    }

    private static uint Color(Vector4 color, float opacity) => ImGui.GetColorU32(color with { W = color.W * opacity });
    public void Dispose() => notifications.Presented -= OnPresented;
}
