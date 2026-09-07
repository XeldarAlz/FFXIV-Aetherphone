using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ConversationAvatar
{
    private const float GroupGlyphFactor = 0.95f;
    private const float RemoteFit = 0.95f;
    private const int Segments = 32;

    public static void Draw(ImDrawListPtr drawList, ConversationDto item, Vector2 center, float radius,
        PhoneTheme theme, AppSkin ui, RemoteImageCache images, LodestoneService lodestone)
    {
        if (item.IsGroup)
        {
            DrawGroup(drawList, center, radius, DirectMessagesTitle(item), item.AvatarUrl, theme, ui, images,
                lodestone);
            return;
        }

        AvatarView.DrawRemote(drawList, center, radius, theme, DirectMessagesTitle(item), string.Empty,
            item.OtherAvatarUrl, images, lodestone, RemoteFit, Segments, 1f, Frames.Of(item.FrameId));
    }

    public static void DrawGroup(ImDrawListPtr drawList, Vector2 center, float radius, string title,
        string? avatarUrl, PhoneTheme theme, AppSkin ui, RemoteImageCache images, LodestoneService lodestone)
    {
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            AvatarView.DrawRemote(drawList, center, radius, theme, title, string.Empty, avatarUrl, images, lodestone,
                RemoteFit, Segments);
            return;
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.FieldSurface), Segments);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Users, ui.MutedInk, radius * GroupGlyphFactor);
    }

    private static string DirectMessagesTitle(ConversationDto item) => ConversationTitle.Of(item);
}
