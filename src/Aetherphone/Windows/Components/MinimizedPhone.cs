using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal sealed class MinimizedPhone : IDisposable
{
    private const float ShakeDuration = 0.55f;
    private const float ShakeFrequency = 44f;
    private const float ShakeAmplitude = 5f;

    private static readonly Vector4 BadgeTone = new(0.90f, 0.22f, 0.19f, 1f);

    private readonly NotificationService notifications;

    private float shake;

    public MinimizedPhone(NotificationService notifications)
    {
        this.notifications = notifications;
        notifications.Presented += OnPresented;
    }

    public bool IsShowing { get; set; }

    public bool Draw(Rect device, PhoneTheme theme, float delta)
    {
        if (shake > 0f)
        {
            shake = MathF.Max(0f, shake - delta);
        }

        var scale = ImGuiHelpers.GlobalScale;
        var offset = shake > 0f
            ? MathF.Sin(shake * ShakeFrequency) * ShakeAmplitude * scale * (shake / ShakeDuration)
            : 0f;
        var frame = new Rect(device.Min + new Vector2(offset, 0f), device.Max + new Vector2(offset, 0f));

        var dl = ImGui.GetForegroundDrawList();
        var body = frame.Inset(scale);
        var rounding = body.Width * 0.30f;
        var bezel = body.Width * 0.09f;
        var screen = body.Inset(bezel);
        var screenRounding = MathF.Max(rounding - bezel, 0f);

        dl.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(theme.BezelOuter), rounding);
        dl.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(theme.ScreenBase), screenRounding);
        dl.AddRect(body.Min, body.Max, ImGui.GetColorU32(theme.BezelRim), rounding);

        var radius = MathF.Min(screen.Width, screen.Height) * 0.32f;
        var clicked = LockButton.Draw(dl, screen.Center, radius, FontAwesomeIcon.Expand, true, theme);

        DrawBadge(dl, body, scale);

        if (ImGui.IsMouseHoveringRect(frame.Min, frame.Max))
        {
            ImGui.SetTooltip(Loc.T(L.Plugin.MaximizeHint));
        }

        return clicked;
    }

    private void DrawBadge(ImDrawListPtr dl, Rect body, float scale)
    {
        var unread = notifications.UnreadCount;
        if (unread <= 0)
        {
            return;
        }

        var label = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        var radius = body.Width * 0.22f;
        var center = new Vector2(body.Max.X - radius * 0.55f, body.Min.Y + radius * 0.55f);

        dl.AddCircleFilled(center, radius + 1.5f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)), 24);
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(BadgeTone), 24);
        Typography.DrawCentered(dl, center, label, new Vector4(1f, 1f, 1f, 1f), 0.7f, FontWeight.Bold);
    }

    private void OnPresented(PhoneNotification _)
    {
        if (IsShowing)
        {
            shake = ShakeDuration;
        }
    }

    public void Dispose() => notifications.Presented -= OnPresented;
}
