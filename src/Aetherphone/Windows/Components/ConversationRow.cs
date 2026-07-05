using System.Numerics;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class ConversationRow
{
    private const float Height = 64f;

    public static bool Draw(Conversation conversation, PhoneTheme theme, LodestoneService lodestone)
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
            var hlMin = new Vector2(min.X + Metrics.Space.Xs * scale, min.Y + 3f * scale);
            var hlMax = new Vector2(max.X - Metrics.Space.Xs * scale, max.Y - 3f * scale);
            Squircle.Fill(dl, hlMin, hlMax, Metrics.Radius.Md * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, pressed ? 0.10f : 0.05f)));
        }

        var avatarRadius = 21f * scale;
        var avatarCenter = new Vector2(min.X + 14f * scale + avatarRadius, min.Y + Height * scale * 0.5f);
        AvatarView.Draw(dl, avatarCenter, avatarRadius, theme.Accent, Initials.Of(conversation.Contact), 1.2f,
            lodestone.Avatar(conversation.Contact, conversation.World), 32);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textRight = max.X - 14f * scale;
        var hasUnread = conversation.Unread > 0;
        var time = NotificationCard.RelativeTime(conversation.LastActivity);
        var timeSize = Typography.Measure(time, TextStyles.Caption1);
        Typography.Draw(new Vector2(textRight - timeSize.X, min.Y + 13f * scale), time,
            hasUnread ? theme.Accent : theme.TextMuted, TextStyles.Caption1);
        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(textRight - timeSize.X - 8f * scale, max.Y), true);
        Typography.Draw(new Vector2(textLeft, min.Y + 11f * scale), conversation.Contact, theme.TextStrong,
            TextStyles.Headline);
        dl.PopClipRect();
        var previewRight = textRight;
        if (hasUnread)
        {
            var label = conversation.Unread > 99 ? "99+" : conversation.Unread.ToString();
            var labelSize = Typography.Measure(label, TextStyles.Caption1);
            var badgeHeight = 18f * scale;
            var badgeWidth = MathF.Max(labelSize.X + 12f * scale, badgeHeight);
            var badgeCenterY = min.Y + 42f * scale;
            var badgeMin = new Vector2(textRight - badgeWidth, badgeCenterY - badgeHeight * 0.5f);
            var badgeMax = new Vector2(textRight, badgeCenterY + badgeHeight * 0.5f);
            Squircle.Fill(dl, badgeMin, badgeMax, badgeHeight * 0.5f, ImGui.GetColorU32(theme.Accent));
            Typography.DrawCentered((badgeMin + badgeMax) * 0.5f, label, new Vector4(1f, 1f, 1f, 1f),
                TextStyles.Caption1);
            previewRight = badgeMin.X - 8f * scale;
        }

        var preview = conversation.Last?.Text ?? string.Empty;
        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(previewRight, max.Y), true);
        Typography.Draw(new Vector2(textLeft, min.Y + 34f * scale), preview, theme.TextMuted, TextStyles.Subheadline);
        dl.PopClipRect();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Height * scale));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
