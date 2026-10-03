using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PressFx
{
    public const float DefaultPressedScale = Motion.PressScaleControl;
    public const float ControlPressedScale = Motion.PressScaleControl;
    public const float IconPressedScale = Motion.PressScaleControl;
    public const float CardPressedScale = Motion.PressScaleCard;
    public const float PressSmoothTime = Motion.PressIn;
    public const float ReleaseSmoothTime = Motion.Release;
    private static readonly Dictionary<uint, Spring> Springs = new();

    public static float Scale(string id, bool pressed, float pressedScale = DefaultPressedScale) =>
        Press(ImGui.GetID(id), pressed, pressedScale);

    public static float Scale(uint key, bool pressed, float pressedScale = DefaultPressedScale) =>
        Press(key, pressed, pressedScale);

    public static float Press(string id, bool pressed, float pressedScale) =>
        Press(ImGui.GetID(id), pressed, pressedScale);

    public static float Press(uint key, bool pressed, float pressedScale) =>
        Toward(key, pressed ? pressedScale : 1f, pressed ? PressSmoothTime : ReleaseSmoothTime);

    public static float Toward(string id, float target) => Toward(ImGui.GetID(id), target);

    public static float Toward(uint key, float target)
    {
        var pressingIn = Springs.TryGetValue(key, out var spring) ? target < spring.Value : target < 1f;
        return Toward(key, target, pressingIn ? PressSmoothTime : ReleaseSmoothTime);
    }

    private static float Toward(uint key, float target, float smoothTime)
    {
        if (!Springs.TryGetValue(key, out var spring))
        {
            spring = new Spring(1f);
        }

        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        spring.Step(target, smoothTime, deltaSeconds);
        Springs[key] = spring;
        return spring.Value;
    }
}
