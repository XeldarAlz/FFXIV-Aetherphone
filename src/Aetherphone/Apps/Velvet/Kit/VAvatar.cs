using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VAvatar
{
    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, PhoneTheme theme, string name,
        string world, string? avatarUrl, RemoteImageCache images, LodestoneService lodestone, int presence = -1,
        Vector4? ring = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        AvatarView.DrawRemote(drawList, center, radius, theme, name, world, avatarUrl, images, lodestone, 0.9f, 44, 1f);
        if (ring.HasValue)
        {
            drawList.AddCircle(center, radius + 1.5f * scale, ring.Value.Packed(), 48, Metrics.Stroke.Ring * scale);
        }

        if (presence > VelvetPresence.Offline)
        {
            var offset = radius * 0.72f;
            VBadge.Dot(drawList, new Vector2(center.X + offset, center.Y + offset), VelvetTheme.PresenceColor(presence));
        }
    }
}
