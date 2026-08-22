using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherphone.Windows.Components;

internal static class GameIconTile
{
    public static bool Draw(ImDrawListPtr drawList, ITextureProvider textures, uint iconId, Vector2 min, Vector2 max,
        float radius, float scale, uint backgroundColor = 0, float inset = 0f, bool cardShadow = false,
        bool flatShadow = false, bool edgeStroke = false, float edgeStrokeOpacity = 0.5f, bool requireIcon = false)
    {
        IDalamudTextureWrap? texture = null;
        if (iconId != 0)
        {
            var resolved = textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            if (resolved.Handle != 0)
            {
                texture = resolved;
            }
        }

        if (requireIcon && texture is null)
        {
            return false;
        }

        if (cardShadow)
        {
            Elevation.Card(drawList, min, max, radius, scale, 0.5f);
        }

        if (flatShadow)
        {
            var offset = new Vector2(0f, 3f * scale);
            drawList.AddRectFilled(min + offset, max + offset, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)),
                radius);
        }

        if (backgroundColor != 0)
        {
            Squircle.Fill(drawList, min, max, radius, backgroundColor);
        }

        if (texture is not null)
        {
            var imageMin = min + new Vector2(inset, inset);
            var imageMax = max - new Vector2(inset, inset);
            drawList.AddImageRounded(texture.Handle, imageMin, imageMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                radius - inset);
        }

        if (edgeStroke)
        {
            Material.EdgeSquircle(drawList, min, max, radius, scale, edgeStrokeOpacity);
        }

        return texture is not null;
    }
}
