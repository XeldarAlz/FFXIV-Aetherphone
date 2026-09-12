using Aetherphone.Core.GameChat;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class GameChatTiles
{
    public const float TileFillAlpha = 0.22f;
    private const float TileRoundingFactor = 0.32f;
    private const float TileGlyphFactor = 0.52f;
    private const float PortraitMonogramScale = 1.2f;
    private const int PortraitSegments = 32;

    public static string GlyphFor(ChatTab tab)
    {
        var category = ChannelCategory.System;
        var mixed = false;
        var resolved = false;
        for (var index = 0; index < tab.Channels.Count; index++)
        {
            if (!GameChannels.TryByKey(tab.Channels[index], out var channel))
            {
                continue;
            }

            if (!resolved)
            {
                category = channel.Category;
                resolved = true;
                continue;
            }

            if (channel.Category != category)
            {
                mixed = true;
                break;
            }
        }

        if (!resolved || mixed)
        {
            return PhoneIcons.Hash;
        }

        return GlyphFor(category);
    }

    public static string GlyphFor(ChannelCategory category) => category switch
    {
        ChannelCategory.Local => PhoneIcons.MapPin,
        ChannelCategory.Group => PhoneIcons.Users,
        ChannelCategory.Community => PhoneIcons.Shield,
        ChannelCategory.Linkshell => PhoneIcons.Link,
        ChannelCategory.CrossWorld => PhoneIcons.World,
        ChannelCategory.Direct => PhoneIcons.User,
        _ => PhoneIcons.InfoCircle,
    };

    public static void DrawTile(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 tint, string glyph)
    {
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        Squircle.Fill(drawList, min, max, radius * 2f * TileRoundingFactor,
            ImGui.GetColorU32(Palette.WithAlpha(tint, TileFillAlpha)));
        PhoneIcon.Draw(drawList, center, glyph, tint, radius * 2f * TileGlyphFactor);
    }

    public static void DrawAvatar(ImDrawListPtr drawList, Vector2 center, float radius, InboxRow row,
        LodestoneService lodestone, Vector4 accent)
    {
        if (row.Tab is { } tab)
        {
            DrawTile(drawList, center, radius, row.Tint, GlyphFor(tab));
            return;
        }

        AvatarView.Draw(drawList, center, radius, accent, Initials.Of(row.Title), PortraitMonogramScale,
            lodestone.Avatar(row.Title, row.World, radius * 2f), PortraitSegments);
    }
}
