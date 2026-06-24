using System.Numerics;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PlayBadge
{
    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, bool playing)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.95f)), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f)), 32, 1.5f);

        var ink = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
        var size = radius * 0.46f;
        if (playing)
        {
            MediaGlyph.Pause(drawList, center, size, ink);
            return;
        }

        MediaGlyph.Play(drawList, center + new Vector2(size * 0.12f, 0f), size, ink);
    }
}
