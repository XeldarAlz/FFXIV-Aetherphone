using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class HoverFx
{
    public const float DefaultSmoothTime = Motion.HoverLift;
    private static readonly Dictionary<uint, Spring> Springs = new();

    public static float Amount(string id, bool hovered, float smoothTime = DefaultSmoothTime)
    {
        var key = ImGui.GetID(id);
        Springs.TryGetValue(key, out var spring);
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        spring.Step(hovered ? 1f : 0f, smoothTime, deltaSeconds);
        Springs[key] = spring;
        return Math.Clamp(spring.Value, 0f, 1f);
    }
}
