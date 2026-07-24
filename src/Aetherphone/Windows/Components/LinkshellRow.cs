using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal enum LinkshellRowAction
{
    None,
    Open,
    ToggleMute,
}

internal static class LinkshellRow
{
    private const float Height = 64f;
    private static readonly Vector4 NeutralTint = new(1f, 1f, 1f, 0.14f);
    private static readonly Vector4 MutedInk = new(1f, 1f, 1f, 1f);

    public static LinkshellRowAction Draw(LinkshellChannel channel, string label, LinkshellThread? thread, bool muted,
        PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + Height * scale);
        var dl = ImGui.GetWindowDrawList();

        var bellRadius = 15f * scale;
        var bellCenter = new Vector2(max.X - 22f * scale, min.Y + Height * scale * 0.5f);
        var bellMin = bellCenter - new Vector2(bellRadius, bellRadius);
        var bellMax = bellCenter + new Vector2(bellRadius, bellRadius);
        var bellHovered = ImGui.IsMouseHoveringRect(bellMin, bellMax);

        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var rowActive = hovered && !bellHovered;
        var pressed = rowActive && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (hovered)
        {
            var hlMin = new Vector2(min.X + 6f * scale, min.Y + 3f * scale);
            var hlMax = new Vector2(max.X - 6f * scale, max.Y - 3f * scale);
            Squircle.Fill(dl, hlMin, hlMax, 12f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, pressed ? 0.10f : 0.05f)));
        }

        var avatarRadius = 21f * scale;
        var avatarCenter = new Vector2(min.X + 14f * scale + avatarRadius, min.Y + Height * scale * 0.5f);
        var tint = channel.IsCrossWorld ? new Vector4(0.40f, 0.56f, 0.92f, 1f) : theme.Accent;
        AvatarView.Draw(dl, avatarCenter, avatarRadius, tint, Initials.Of(label), 1.2f, AvatarHandle.Disabled, 32);
        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var textRight = bellMin.X - 10f * scale;
        var last = thread?.Last;
        var unread = thread?.Unread ?? 0;
        var hasUnread = unread > 0;
        if (last is not null)
        {
            var time = TimeText.Short(thread!.LastActivity);
            var timeSize = Typography.Measure(time, TextStyles.Caption1);
            Typography.Draw(new Vector2(textRight - timeSize.X, min.Y + 13f * scale), time,
                hasUnread && !muted ? theme.Accent : theme.TextMuted, TextStyles.Caption1);
        }

        var titleSize = Typography.Measure(label, TextStyles.Headline);
        var titleY = last is null
            ? min.Y + Height * scale * 0.5f - titleSize.Y * 0.5f
            : min.Y + 11f * scale;
        var titleMaxWidth = textRight - 4f * scale - textLeft;
        var titleHovering = ImGui.IsMouseHoveringRect(new Vector2(textLeft, titleY),
            new Vector2(textLeft + titleMaxWidth, titleY + titleSize.Y));
        Marquee.DrawLeft("linkshellrow.title." + channel.Key, label, textLeft, titleY,
            titleMaxWidth, TextStyles.Headline, theme.TextStrong, titleHovering);

        var previewRight = textRight;
        if (hasUnread)
        {
            var badgeCenterY = min.Y + 42f * scale;
            if (muted)
            {
                var dotCenter = new Vector2(textRight - 4f * scale, badgeCenterY);
                dl.AddCircleFilled(dotCenter, 3.5f * scale, ImGui.GetColorU32(theme.TextMuted), 12);
                previewRight = dotCenter.X - 8f * scale;
            }
            else
            {
                var count = unread > 99 ? "99+" : unread.ToString();
                var countSize = Typography.Measure(count, TextStyles.Caption1);
                var badgeHeight = 18f * scale;
                var badgeWidth = MathF.Max(countSize.X + 12f * scale, badgeHeight);
                var badgeMin = new Vector2(textRight - badgeWidth, badgeCenterY - badgeHeight * 0.5f);
                var badgeMax = new Vector2(textRight, badgeCenterY + badgeHeight * 0.5f);
                Squircle.Fill(dl, badgeMin, badgeMax, badgeHeight * 0.5f, ImGui.GetColorU32(theme.Accent));
                Typography.DrawCentered((badgeMin + badgeMax) * 0.5f, count, new Vector4(1f, 1f, 1f, 1f),
                    TextStyles.Caption1);
                previewRight = badgeMin.X - 8f * scale;
            }
        }

        if (last is not null)
        {
            var preview = Preview(last);
            var previewY = min.Y + 34f * scale;
            var previewMaxWidth = previewRight - textLeft;
            var previewSize = Typography.Measure(preview, TextStyles.Subheadline);
            var previewHovering = ImGui.IsMouseHoveringRect(new Vector2(textLeft, previewY),
                new Vector2(textLeft + previewMaxWidth, previewY + previewSize.Y));
            Marquee.DrawLeft("linkshellrow.preview." + channel.Key, preview, textLeft, previewY,
                previewMaxWidth, TextStyles.Subheadline, theme.TextMuted, previewHovering);
        }

        var bellTint = muted ? theme.Accent : NeutralTint;
        var bellInk = muted ? MutedInk : theme.TextStrong;
        var muteClicked = HoverButton.Circle(dl, "ls.mute:" + channel.Key, bellCenter, bellRadius,
            muted ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell, bellTint, bellInk,
            ImGui.GetIO().DeltaTime, 1f, true, Loc.T(muted ? L.Messages.Unmute : L.Messages.Mute),
            HoverLabelSide.Above);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Height * scale));
        if (muteClicked)
        {
            return LinkshellRowAction.ToggleMute;
        }

        if (rowActive)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                return LinkshellRowAction.Open;
            }
        }

        return LinkshellRowAction.None;
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
