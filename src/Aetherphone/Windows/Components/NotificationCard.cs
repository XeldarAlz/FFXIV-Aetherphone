using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class NotificationCard
{
    private const float Height = 64f;
    private const float Gap = 8f;

    public static void Draw(PhoneNotification notification, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + Height * scale);

        var dl = ImGui.GetWindowDrawList();
        Squircle.Fill(dl, min, max, 14f * scale, ImGui.GetColorU32(theme.GroupedCard));
        Material.EdgeSquircle(dl, min, max, 14f * scale, scale);
        dl.AddCircleFilled(new Vector2(min.X + 18f * scale, min.Y + 23f * scale), 5f * scale, ImGui.GetColorU32(notification.Accent), 16);

        var textLeft = min.X + 32f * scale;
        var textRight = max.X - 14f * scale;

        Typography.Draw(new Vector2(textLeft, min.Y + 12f * scale), notification.Title, theme.TextStrong, 1f, FontWeight.SemiBold);

        var time = RelativeTime(notification.ReceivedAt);
        var timeSize = Typography.Measure(time, 0.82f);
        Typography.Draw(new Vector2(textRight - timeSize.X, min.Y + 14f * scale), time, theme.TextMuted, 0.82f);

        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(textRight, max.Y), true);
        Typography.Draw(new Vector2(textLeft, min.Y + 35f * scale), notification.Body, theme.TextMuted, 0.92f);
        dl.PopClipRect();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (Height + Gap) * scale));
    }

    internal static string RelativeTime(DateTime time)
    {
        var delta = DateTime.Now - time;
        if (delta.TotalMinutes < 1)
        {
            return "now";
        }

        if (delta.TotalHours < 1)
        {
            return $"{(int)delta.TotalMinutes}m";
        }

        if (delta.TotalDays < 1)
        {
            return $"{(int)delta.TotalHours}h";
        }

        return $"{(int)delta.TotalDays}d";
    }
}
