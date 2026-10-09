using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal static class AethergramArt
{
    public static void StoryRing(ImDrawListPtr drawList, Vector2 center, float radius, float scale, bool unseen) =>
        StoryRingArt.Sweep(drawList, center, radius, scale, unseen, AethergramInk.StoryRings, AethergramInk.SeenRing);
}
