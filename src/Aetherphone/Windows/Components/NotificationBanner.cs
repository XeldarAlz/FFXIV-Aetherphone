using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
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

    private const float EnterSmoothTime = 0.30f;
    private const float HoldSeconds = 4.2f;
    private const float ExitSmoothTime = 0.20f;

    private const float SideMargin = 8f;
    private const float BannerHeight = 64f;
    private const float RestTopOffset = 40f;
    private const float HiddenGap = 8f;
    private const float CornerRadius = 22f;
    private const float Padding = 13f;
    private const float IconSize = 38f;
    private const float TextGap = 11f;
    private const float BodyOffset = 20f;
    private const int MaxQueued = 4;

    private readonly NotificationService notifications;
    private readonly Queue<PhoneNotification> pending = new();
    private Spring slide;

    private PhoneNotification? active;
    private Stage stage = Stage.Idle;
    private float holdElapsed;

    public NotificationBanner(NotificationService notifications)
    {
        this.notifications = notifications;
        notifications.Presented += OnPresented;
    }

    public void Advance(float deltaSeconds)
    {
        if (stage == Stage.Idle)
        {
            return;
        }

        if (stage == Stage.Hold)
        {
            holdElapsed += deltaSeconds;
            if (holdElapsed >= HoldSeconds)
            {
                BeginExit();
            }

            return;
        }

        var smoothTime = stage == Stage.Enter ? EnterSmoothTime : ExitSmoothTime;
        slide.Step(1f, smoothTime, deltaSeconds);
        if (!slide.IsResting(1f, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
        {
            return;
        }

        slide.SnapTo(1f);
        if (stage == Stage.Enter)
        {
            stage = Stage.Hold;
            holdElapsed = 0f;
        }
        else if (pending.Count > 0)
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
        var height = BannerHeight * scale;
        var restTop = screen.Min.Y + RestTopOffset * scale;
        var hiddenTop = screen.Min.Y - height - HiddenGap * scale;

        float top;
        float opacity;
        if (stage == Stage.Enter)
        {
            top = Lerp(hiddenTop, restTop, slide.Value);
            opacity = Math.Clamp(slide.Value * 1.8f, 0f, 1f);
        }
        else if (stage == Stage.Exit)
        {
            top = Lerp(restTop, hiddenTop, slide.Value);
            opacity = 1f - slide.Value;
        }
        else
        {
            top = restTop;
            opacity = 1f;
        }

        var min = new Vector2(screen.Min.X + SideMargin * scale, top);
        var max = new Vector2(screen.Max.X - SideMargin * scale, top + height);

        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(screen.Min, screen.Max, true);
        DrawCard(dl, notification, theme, min, max, scale, opacity);
        dl.PopClipRect();
    }

    private static void DrawCard(ImDrawListPtr dl, PhoneNotification notification, PhoneTheme theme, Vector2 min, Vector2 max, float scale, float opacity)
    {
        var rounding = CornerRadius * scale;

        Elevation.Floating(dl, min, max, rounding, scale, opacity);

        var cardColor = Palette.Mix(theme.GroupedCard, theme.TextStrong, 0.06f);
        Squircle.Fill(dl, min, max, rounding, Color(Palette.WithAlpha(cardColor, 0.99f), opacity));
        Squircle.Stroke(dl, min, max, rounding, Color(Palette.WithAlpha(theme.TextStrong, 0.10f), opacity), scale);

        var iconExtent = IconSize * scale * 0.5f;
        var iconCenter = new Vector2(min.X + Padding * scale + iconExtent, (min.Y + max.Y) * 0.5f);
        var iconMin = new Vector2(iconCenter.X - iconExtent, iconCenter.Y - iconExtent);
        var iconMax = new Vector2(iconCenter.X + iconExtent, iconCenter.Y + iconExtent);
        Squircle.Fill(dl, iconMin, iconMax, iconExtent * 0.52f, Color(notification.Accent, opacity));

        var ink = Palette.WithAlpha(theme.TextStrong, opacity);
        if (!AppIconArt.TryDraw(notification.AppId, iconCenter, IconSize * scale, ink, Palette.WithAlpha(notification.Accent, opacity)))
        {
            var initial = notification.Title.Length > 0 ? notification.Title.Substring(0, 1) : "?";
            Typography.DrawCentered(iconCenter, initial, ink, 1.1f);
        }

        var textLeft = iconMax.X + TextGap * scale;
        var textRight = max.X - Padding * scale;
        var titleTop = min.Y + Padding * scale;

        var time = NotificationCard.RelativeTime(notification.ReceivedAt);
        var timeSize = Typography.Measure(time, 0.78f);
        Typography.Draw(new Vector2(textRight - timeSize.X, titleTop + 1f * scale), time, Palette.WithAlpha(theme.TextMuted, opacity), 0.78f);

        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(textRight - timeSize.X - 6f * scale, max.Y), true);
        Typography.Draw(new Vector2(textLeft, titleTop), notification.Title, ink, 0.94f, FontWeight.SemiBold);
        dl.PopClipRect();

        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(textRight, max.Y), true);
        Typography.Draw(new Vector2(textLeft, titleTop + BodyOffset * scale), notification.Body, Palette.WithAlpha(theme.TextMuted, opacity), 0.88f);
        dl.PopClipRect();
    }

    private void OnPresented(PhoneNotification notification)
    {
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

    private void BeginNext()
    {
        active = pending.Dequeue();
        stage = Stage.Enter;
        holdElapsed = 0f;
        slide.SnapTo(0f);
    }

    private void BeginExit()
    {
        stage = Stage.Exit;
        slide.SnapTo(0f);
    }

    private static uint Color(Vector4 color, float opacity) => ImGui.GetColorU32(color with { W = color.W * opacity });

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    public void Dispose() => notifications.Presented -= OnPresented;
}
