using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal enum MinimizedAction : byte
{
    None,
    Expand,
    Close,
}

internal sealed class MinimizedPhone : IDisposable
{
    private const float ShakeDuration = 0.55f;
    private const float ShakeFrequency = 44f;
    private const float ShakeAmplitude = 5f;
    private static readonly Vector4 BadgeTone = new(0.90f, 0.22f, 0.19f, 1f);
    private static readonly Vector4 CloseTint = new(1f, 1f, 1f, 0.16f);
    private static readonly Vector4 IconInk = new(1f, 1f, 1f, 1f);
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private NotificationShake shake = new(ShakeDuration, ShakeFrequency, ShakeAmplitude);

    public MinimizedPhone(NotificationService notifications, Configuration configuration)
    {
        this.notifications = notifications;
        this.configuration = configuration;
        notifications.Vibration += OnVibration;
    }

    public bool IsShowing { get; set; }

    public MinimizedAction Draw(Rect device, PhoneTheme theme, float delta)
    {
        var scale = UiScale.Global;
        var frame = device.Translate(new Vector2(shake.Advance(delta), 0f));
        var dl = ImGui.GetForegroundDrawList();
        var geometry = ChassisGeometry.Puck(frame.Inset(scale));
        DrawShell(dl, geometry, theme);
        var action = MinimizedAction.None;
        if (HoverButton.Circle(dl, "minimized.expand", geometry.Screen.Center, ExpandRadius(geometry.Screen),
                FontAwesomeIcon.Expand, theme.Accent, IconInk, delta, 1f, true, Loc.T(L.Plugin.MaximizeHint)))
        {
            action = MinimizedAction.Expand;
        }

        if (HoverButton.Circle(dl, "minimized.close", CloseCenter(geometry.Screen, scale),
                CloseRadius(geometry.Screen), FontAwesomeIcon.Times, CloseTint, theme.TextStrong, delta, 1f, true,
                Loc.T(L.Common.Close)))
        {
            action = MinimizedAction.Close;
        }

        DrawBadge(dl, geometry.Body, scale, 1f, notifications.UnreadCount);
        return action;
    }

    public static void DrawShell(ImDrawListPtr dl, in ChassisGeometry geometry, PhoneTheme theme)
    {
        DeviceChrome.DrawShell(dl, geometry, UiScale.Global, theme, 1f);
    }

    public static void DrawFace(ImDrawListPtr dl, in ChassisGeometry geometry, PhoneTheme theme, float scale,
        float alpha, int unread)
    {
        if (alpha <= 0.001f)
        {
            return;
        }

        var screen = geometry.Screen;
        HoverButton.CircleStatic(dl, screen.Center, ExpandRadius(screen), FontAwesomeIcon.Expand, theme.Accent, IconInk,
            alpha);
        HoverButton.CircleStatic(dl, CloseCenter(screen, scale), CloseRadius(screen), FontAwesomeIcon.Times, CloseTint,
            theme.TextStrong, alpha);
        DrawBadge(dl, geometry.Body, scale, alpha, unread);
    }

    private static float ExpandRadius(Rect screen) => MathF.Min(screen.Width, screen.Height) * 0.27f;

    private static float CloseRadius(Rect screen) => MathF.Min(screen.Width, screen.Height) * 0.15f;

    private static Vector2 CloseCenter(Rect screen, float scale)
    {
        var radius = CloseRadius(screen);
        var inset = radius + 5f * scale;
        return new Vector2(screen.Max.X - inset, screen.Min.Y + inset);
    }

    private static void DrawBadge(ImDrawListPtr dl, Rect body, float scale, float alpha, int unread)
    {
        if (unread <= 0)
        {
            return;
        }

        var label = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        var radius = body.Width * 0.17f;
        var center = new Vector2(body.Min.X + radius * 0.7f, body.Min.Y + radius * 0.7f);
        dl.AddCircleFilled(center, radius + 1.5f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f * alpha)), 24);
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(BadgeTone, alpha)), 24);
        var labelSize = Typography.Measure(label, 0.66f, FontWeight.Bold);
        Typography.Draw(dl, center - labelSize * 0.5f, label, new Vector4(1f, 1f, 1f, alpha), 0.66f, FontWeight.Bold);
    }

    private void OnVibration(PhoneNotification _)
    {
        if (IsShowing)
        {
            shake.Trigger();
        }
    }

    public void Dispose() => notifications.Vibration -= OnVibration;
}
