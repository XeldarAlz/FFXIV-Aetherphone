using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class Toggle
{
    private const float SmoothTime = 0.13f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 KnobShadow = new(0f, 0f, 0f, 0.18f);
    private static readonly Dictionary<string, Spring> Knobs = new(StringComparer.Ordinal);

    public static bool Draw(string id, Rect bounds, bool value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var result = value;
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(bounds.Min, bounds.Max, hovered))
        {
            result = !value;
        }

        var progress = Animate(id, value ? 1f : 0f);
        var dl = ImGui.GetWindowDrawList();
        var track = Palette.Mix(theme.ToggleOff, theme.ToggleOn, progress);
        dl.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(track), bounds.Height * 0.5f);
        var knobRadius = bounds.Height * 0.5f - 2f * scale;
        var inset = knobRadius + 2f * scale;
        var knobX = (bounds.Min.X + inset) + (bounds.Max.X - inset - (bounds.Min.X + inset)) * progress;
        var knobCenter = new Vector2(knobX, bounds.Center.Y);
        dl.AddCircleFilled(knobCenter + new Vector2(0f, 1f * scale), knobRadius, ImGui.GetColorU32(KnobShadow), 32);
        dl.AddCircleFilled(knobCenter, knobRadius, ImGui.GetColorU32(White), 32);
        return result;
    }

    private static float Animate(string id, float target)
    {
        if (!Knobs.TryGetValue(id, out var spring))
        {
            spring = new Spring(target);
        }

        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var position = spring.Step(target, SmoothTime, deltaSeconds);
        Knobs[id] = spring;
        return Math.Clamp(position, 0f, 1f);
    }
}
