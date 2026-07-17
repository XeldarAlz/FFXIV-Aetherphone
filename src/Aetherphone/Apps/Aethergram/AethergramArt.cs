using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

/// <summary>
/// Aethergram's signature bespoke artwork: the multi-stop "story" gradient ring drawn around
/// avatars that have a live story. Pure drawing; state and layout live in <see cref="AethergramApp"/>.
/// </summary>
internal static class AethergramArt
{
    private static readonly Vector4[] RingStops =
    [
        new(1f, 0.863f, 0.502f, 1f), new(0.969f, 0.435f, 0.216f, 1f), new(0.882f, 0.188f, 0.424f, 1f),
        new(0.514f, 0.227f, 0.706f, 1f),
    ];

    private static readonly Vector4 SeenRing = new(1f, 1f, 1f, 0.28f);

    public static void StoryRing(ImDrawListPtr drawList, Vector2 center, float radius, float scale, bool unseen) =>
        StoryRingArt.Sweep(drawList, center, radius, scale, unseen, RingStops, SeenRing);
}
