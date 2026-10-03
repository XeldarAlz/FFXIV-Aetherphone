using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;

namespace Aetherphone.Windows.Widgets;

internal static class AdventureWidgetArt
{
    public static bool IsLoggedIn => Plugin.ClientState.IsLoggedIn;

    public static bool GameIcon(ImDrawListPtr drawList, uint iconId, Vector2 min, Vector2 max, float radius,
        in WidgetInk ink)
    {
        if (iconId == 0 || ink.Opacity <= 0f)
        {
            return false;
        }

        var texture = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
        if (texture.Handle == 0)
        {
            return false;
        }

        drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One,
            ImGui.GetColorU32(ink.ImageTint), radius);
        return true;
    }

    public static void IconBadge(ImDrawListPtr drawList, in WidgetInk ink, Vector2 center, float diameter,
        FontAwesomeIcon icon, Vector4 accent)
    {
        var tint = ink.Accent(accent);
        drawList.AddCircleFilled(center, diameter * 0.5f, ImGui.GetColorU32(tint with { W = tint.W * 0.2f }), 32);
        ProgressRing.CenterIcon(drawList, center, icon, tint, diameter * 0.5f);
    }
}
