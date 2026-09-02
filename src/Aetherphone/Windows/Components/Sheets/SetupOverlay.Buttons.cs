using Aetherphone.Core;
using Aetherphone.Core.Onboarding;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed partial class SetupOverlay
{
    private static ButtonPose Animate(Rect rect, string key, bool hovered, bool pressed) =>
        MotionButton.Animate(rect, key, hovered, pressed);

    private static void DrawHalo(ImDrawListPtr drawList, Rect face, float radius, Vector4 tint, float strength) =>
        MotionButton.Halo(drawList, face, radius, tint, strength);

    private static void DrawRimLight(ImDrawListPtr drawList, Rect face, float radius, float strength) =>
        MotionButton.RimLight(drawList, face, radius, strength);

    private static bool Primary(ImDrawListPtr drawList, Rect rect, string text, float alpha, bool live,
        bool enabled = true)
    {
        if (alpha <= 0.001f)
        {
            return false;
        }

        UiAnchors.Report("setup.primary", rect);
        var hovered = live && enabled && UiInteract.Hover(rect.Min, rect.Max);
        MotionButton.Brand(drawList, rect, text, text, alpha, hovered, enabled, ink.Disabled, ink.DisabledText);
        return live && enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static bool Secondary(ImDrawListPtr drawList, Rect rect, string label, float alpha, bool live)
    {
        if (alpha <= 0.001f)
        {
            return false;
        }

        var hovered = live && UiInteract.Hover(rect.Min, rect.Max);
        MotionButton.Glass(drawList, rect, label, label, ink.Strong, glass, alpha, hovered);
        return live && UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}
