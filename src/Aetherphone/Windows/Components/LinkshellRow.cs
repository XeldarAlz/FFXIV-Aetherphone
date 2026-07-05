using System.Numerics;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class LinkshellRow
{
    private const float Height = 64f;

    public static bool Draw(LinkshellChannel channel, string label, LinkshellThread? thread, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + Height * scale);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);

        var dl = ImGui.GetWindowDrawList();
        if (hovered)
        {
            var hlMin = new Vector2(min.X + 6f * scale, min.Y + 3f * scale);
            var hlMax = new Vector2(max.X - 6f * scale, max.Y - 3f * scale);
            Squircle.Fill(dl, hlMin, hlMax, 12f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, pressed ? 0.10f : 0.05f)));
        }

        var avatarRadius = 21f * scale;
        var avatarCenter = new Vector2(min.X + 14f * scale + avatarRadius, min.Y + Height * scale * 0.5f);
        var tint = channel.IsCrossWorld ? new Vector4(0.40f, 0.56f, 0.92f, 1f) : theme.Accent;
        AvatarView.Draw(dl, avatarCenter, avatarRadius, tint, Initials.Of(label), 1.2f, AvatarHandle.Disabled, 32);

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var textRight = max.X - 14f * scale;
        var last = thread?.Last;
        var unread = thread?.Unread ?? 0;
        var hasUnread = unread > 0;

        if (last is not null)
        {
            var time = NotificationCard.RelativeTime(thread!.LastActivity);
            var timeSize = Typography.Measure(time, TextStyles.Caption1);
            Typography.Draw(new Vector2(textRight - timeSize.X, min.Y + 13f * scale), time, hasUnread ? theme.Accent : theme.TextMuted, TextStyles.Caption1);
        }

        var titleY = last is null ? min.Y + Height * scale * 0.5f - Typography.Measure(label, TextStyles.Headline).Y * 0.5f : min.Y + 11f * scale;
        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(textRight - 40f * scale, max.Y), true);
        Typography.Draw(new Vector2(textLeft, titleY), label, theme.TextStrong, TextStyles.Headline);
        dl.PopClipRect();

        var previewRight = textRight;
        if (hasUnread)
        {
            var count = unread > 99 ? "99+" : unread.ToString();
            var countSize = Typography.Measure(count, TextStyles.Caption1);
            var badgeHeight = 18f * scale;
            var badgeWidth = MathF.Max(countSize.X + 12f * scale, badgeHeight);
            var badgeCenterY = min.Y + 42f * scale;
            var badgeMin = new Vector2(textRight - badgeWidth, badgeCenterY - badgeHeight * 0.5f);
            var badgeMax = new Vector2(textRight, badgeCenterY + badgeHeight * 0.5f);
            Squircle.Fill(dl, badgeMin, badgeMax, badgeHeight * 0.5f, ImGui.GetColorU32(theme.Accent));
            Typography.DrawCentered((badgeMin + badgeMax) * 0.5f, count, new Vector4(1f, 1f, 1f, 1f), TextStyles.Caption1);
            previewRight = badgeMin.X - 8f * scale;
        }

        if (last is not null)
        {
            var preview = Preview(last);
            dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(previewRight, max.Y), true);
            Typography.Draw(new Vector2(textLeft, min.Y + 34f * scale), preview, theme.TextMuted, TextStyles.Subheadline);
            dl.PopClipRect();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Height * scale));

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static string Preview(ChatLine last)
    {
        if (last.Author is { } author)
        {
            return $"{FirstName(author.Name)}: {last.Text}";
        }

        return last.Text;
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name.Substring(0, space) : name;
    }
}
