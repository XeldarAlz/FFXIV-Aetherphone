using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal enum HoverLabelSide : byte
{
    Below,
    Above,
}

internal static class HoverButton
{
    private const float HoverSmoothTime = 0.11f;
    private const float GrowAmount = 0.14f;
    private static readonly Dictionary<string, Spring> springs = new();

    public static bool Circle(ImDrawListPtr dl, string id, Vector2 center, float radius, FontAwesomeIcon icon,
        Vector4 tint, Vector4 ink, float delta, float alpha, bool interactive, string? label = null,
        HoverLabelSide side = HoverLabelSide.Below)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var min = new Vector2(center.X - radius, center.Y - radius);
        var max = new Vector2(center.X + radius, center.Y + radius);
        var hovered = interactive && ImGui.IsMouseHoveringRect(min, max);
        var eased = Step(id, hovered, delta);
        var grow = 1f + GrowAmount * eased;
        var scaledRadius = radius * grow;
        var fill = Palette.Lighten(tint, 0.10f * eased);
        var circleAlpha = Math.Clamp(tint.W * (1f + 0.7f * eased), 0f, 1f) * alpha;
        dl.AddCircleFilled(center, scaledRadius, ImGui.GetColorU32(Palette.WithAlpha(fill, circleAlpha)), 40);
        if (eased > 0.001f)
        {
            dl.AddCircle(center, scaledRadius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f * eased * alpha)), 40,
                1.2f * scale);
        }

        ProgressRing.CenterIcon(dl, center, icon, Palette.WithAlpha(ink, alpha), scaledRadius * 0.9f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (label is not null && eased > 0.02f)
        {
            var bounds = new Rect(new Vector2(center.X - scaledRadius, center.Y - scaledRadius),
                new Vector2(center.X + scaledRadius, center.Y + scaledRadius));
            HoverTooltip.DrawLabel(dl, bounds, label, eased * alpha, side);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static void CircleStatic(ImDrawListPtr dl, Vector2 center, float radius, FontAwesomeIcon icon, Vector4 tint,
        Vector4 ink, float alpha)
    {
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, tint.W * alpha)), 40);
        ProgressRing.CenterIcon(dl, center, icon, Palette.WithAlpha(ink, alpha), radius * 0.9f);
    }

    private static float Step(string id, bool hovered, float delta)
    {
        if (!springs.TryGetValue(id, out var spring))
        {
            spring = default;
        }

        spring.Step(hovered ? 1f : 0f, HoverSmoothTime, delta);
        springs[id] = spring;
        return Math.Clamp(spring.Value, 0f, 1f);
    }
}
