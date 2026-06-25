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

        var avatarRadius = 16f * scale;
        var avatarCenter = new Vector2(row.Min.X + avatarRadius, row.Center.Y);
        var baseColor = friend.Online ? theme.Accent : theme.SurfaceMuted;
        AvatarView.Draw(dl, avatarCenter, avatarRadius, baseColor, Initials.Of(friend.Name), 0.95f, lodestone.Avatar(friend.Name, friend.WorldName), 32);

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var nameColor = friend.Online ? theme.TextStrong : Palette.WithAlpha(theme.TextStrong, 0.5f);
        Typography.Draw(new Vector2(textLeft, row.Min.Y + 9f * scale), friend.Name, nameColor, 1f, FontWeight.SemiBold);

        var subtitle = Subtitle(friend);
        var subtitleRight = row.Max.X - (friend.Online ? 22f * scale : 8f * scale);
        dl.PushClipRect(new Vector2(textLeft, row.Min.Y), new Vector2(subtitleRight, row.Max.Y), true);
        Typography.Draw(new Vector2(textLeft, row.Min.Y + 30f * scale), subtitle, theme.TextMuted, 0.85f);
        dl.PopClipRect();

        if (friend.Online)
        {
            dl.AddCircleFilled(new Vector2(row.Max.X - 7f * scale, row.Center.Y), 5f * scale, ImGui.GetColorU32(theme.ToggleOn), 16);
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
