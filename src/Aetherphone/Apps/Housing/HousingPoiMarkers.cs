using Aetherphone.Core.Housing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace Aetherphone.Apps.Housing;

internal static class HousingPoiMarkers
{
    private const float IconSize = 16f;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, uint iconId, float scale)
    {
        if (iconId == 0)
        {
            return;
        }

        var lookup = new GameIconLookup
        {
            IconId = iconId,
        };

        var texture = Plugin.TextureProvider.GetFromGameIcon(lookup).GetWrapOrDefault();
        if (texture is null)
        {
            return;
        }

        var half = IconSize * scale * 0.5f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        drawList.AddImage(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFF);
    }
}
