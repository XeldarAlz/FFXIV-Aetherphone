using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Contacts;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class ContactRow
{
    public static bool Draw(Rect row, FriendEntry friend, PhoneTheme theme, LodestoneService lodestone)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (hovered)
        {
            var highlightMin = new Vector2(row.Min.X - 8f * scale, row.Min.Y + 2f * scale);
            var highlightMax = new Vector2(row.Max.X + 8f * scale, row.Max.Y - 2f * scale);
            var alpha = pressed ? 0.10f : 0.05f;
            Squircle.Fill(dl, highlightMin, highlightMax, 9f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        }

        var avatarRadius = 17f * scale;
        var avatarCenter = new Vector2(row.Min.X + avatarRadius, row.Center.Y);
        var baseColor = friend.Online ? theme.Accent : theme.SurfaceMuted;
        AvatarView.Draw(dl, avatarCenter, avatarRadius, baseColor, Initials.Of(friend.Name), 0.95f,
            lodestone.Avatar(friend.Name, friend.WorldName), 32);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var nameColor = friend.Online ? theme.TextStrong : Palette.WithAlpha(theme.TextStrong, 0.5f);
        Typography.Draw(new Vector2(textLeft, row.Min.Y + 9f * scale), friend.Name, nameColor, TextStyles.Headline);
        var subtitle = Subtitle(friend);
        var subtitleRight = row.Max.X - (friend.Online ? 24f * scale : 8f * scale);
        dl.PushClipRect(new Vector2(textLeft, row.Min.Y), new Vector2(subtitleRight, row.Max.Y), true);
        Typography.Draw(new Vector2(textLeft, row.Min.Y + 30f * scale), subtitle, theme.TextMuted,
            TextStyles.Subheadline);
        dl.PopClipRect();
        if (friend.Online)
        {
            var dotCenter = new Vector2(row.Max.X - 7f * scale, row.Center.Y);
            dl.AddCircleFilled(dotCenter, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.ToggleOn, 0.22f)), 20);
            dl.AddCircleFilled(dotCenter, 5f * scale, ImGui.GetColorU32(theme.ToggleOn), 16);
            dl.AddCircleFilled(dotCenter - new Vector2(1.4f * scale, 1.4f * scale), 1.6f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f)), 8);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static string Subtitle(FriendEntry friend)
    {
        if (!friend.Online)
        {
            return friend.WorldName;
        }

        return friend.Location.Length > 0 ? $"{friend.WorldName} · {friend.Location}" : friend.WorldName;
    }
}
