using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

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

    private const float HoldSeconds = 4.0f;
    private const float SideMargin = 10f;
    private const float BannerHeight = 68f;
    private const float RestTopOffset = 44f;
    private const float HiddenGap = 8f;
    private const float CornerRadius = 24f;
    private const float DragSlop = 10f;
    private const float DismissDistance = 16f;
    private const float DismissVelocity = 700f;
    private const float DownwardGive = 26f;
    private const float DragReturnRate = 18f;
    private const float EnterFadeBoost = 1.8f;
    private const float LightBackdrop = 0.5f;
    private const int MaxQueued = 4;
    private const string TitleMarquee = "notificationbanner.title.";
    private const string BodyMarquee = "notificationbanner.body.";

    private readonly NotificationService notifications;
    private readonly Func<string?> currentAppId;
    private readonly Func<bool> phoneVisible;
    private readonly NotificationRouter router;
    private readonly Queue<PhoneNotification> pending = new();
    private Spring enter;
    private Spring exit;
    private PhoneNotification? active;
    private Stage stage = Stage.Idle;
    private float holdElapsed;
    private bool holdPaused;
    private bool dragging;
    private float dragStartY;
    private float dragOffset;
    private float dragLastY;
    private float dragVelocity;
    private float exitFromOffset;

    public NotificationBanner(NotificationService notifications, Func<string?> currentAppId, Func<bool> phoneVisible,
        NotificationRouter router)
    {
        this.notifications = notifications;
        this.currentAppId = currentAppId;
        this.phoneVisible = phoneVisible;
        this.router = router;
        notifications.Presented += OnPresented;
    }

    public bool IsVisible => stage != Stage.Idle;

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

        var bounds = CurrentBounds(screen, UiScale.Current, out _);
        return UiInteract.Hover(bounds.Min, bounds.Max);
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
                dragOffset += (0f - dragOffset) * MathF.Min(1f, deltaSeconds * DragReturnRate);
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
            enter.Step(1f, Motion.Appear, deltaSeconds);
            if (enter.IsResting(1f, 0.004f, 0.05f))
            {
                enter.SnapTo(1f);
                stage = Stage.Hold;
                holdElapsed = 0f;
            }

            return;
        }

        exit.Step(1f, Motion.Appear, deltaSeconds);
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

        var scale = UiScale.Current;
        var bounds = CurrentBounds(screen, scale, out var opacity);
        var hovered = stage != Stage.Exit && UiInteract.Hover(bounds.Min, bounds.Max);
        if (hovered || dragging)
        {
            holdPaused = true;
        }

        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        var tone = ToneFor(theme, bounds);
        Material.LiquidGlass(drawList, bounds.Min, bounds.Max, CornerRadius * scale, scale, tone, 0f, opacity);
        NotificationCard.DrawContent(drawList, bounds, notification, theme, scale, opacity, tone, true, TitleMarquee,
            BodyMarquee);
        drawList.PopClipRect();
        HandleGesture(notification, scale, hovered);
    }

    private GlassTone ToneFor(PhoneTheme theme, Rect bounds)
    {
        if (currentAppId() is null)
        {
            return GlassTone.Dark;
        }

        var brightness = WallpaperBackdrop.Brightness(bounds.Min, bounds.Max);
        if (brightness >= 0f)
        {
            return brightness >= LightBackdrop ? GlassTone.Light : GlassTone.Dark;
        }

        return Material.ToneFor(theme);
    }

    private void HandleGesture(PhoneNotification notification, float scale, bool hovered)
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
            UiInteract.CancelPendingTap();
            dragging = true;
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
            dragOffset = moved < 0f ? moved : Rubber(moved, scale);
            return;
        }

        dragging = false;
        if (MathF.Abs(mouse.Y - dragStartY) < DragSlop * scale)
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
            opacity = Math.Clamp(enter.Value * EnterFadeBoost, 0f, 1f);
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

    private void OnPresented(PhoneNotification notification)
    {
        if (!phoneVisible())
        {
            return;
        }

        if (currentAppId() == notification.AppId)
        {
            return;
        }

        if (active is { } showing && showing.StackKey == notification.StackKey && stage is Stage.Enter or Stage.Hold)
        {
            active = notification;
            holdElapsed = 0f;
            return;
        }

        RemoveQueuedGroup(notification.StackKey);
        if (pending.Count >= MaxQueued)
        {
            return;
        }

        pending.Enqueue(notification);
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

    public void Dispose() => notifications.Presented -= OnPresented;
}
