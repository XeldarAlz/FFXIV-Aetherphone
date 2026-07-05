using System.Numerics;
using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class BackButton
{
    private const float PressSmoothTime = 0.08f;
    private static readonly Dictionary<string, Spring> Scales = new(StringComparer.Ordinal);

    public static bool Draw(string id, Vector2 center, float radius, Vector4 chevronInk, bool hovered, float scale,
        bool shadow = false)
    {
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        if (!Scales.TryGetValue(id, out var spring))
        {
            spring = new Spring(1f);
        }

        var grow = spring.Step(pressed ? 0.82f : 1f, PressSmoothTime, deltaSeconds);
        Scales[id] = spring;
        var drawList = ImGui.GetWindowDrawList();
        var reach = radius * 0.5f * grow;
        var thickness = 2.4f * scale;
        var tip = new Vector2(center.X - reach * 0.4f, center.Y);
        var upper = new Vector2(tip.X + reach, tip.Y - reach);
        var lower = new Vector2(tip.X + reach, tip.Y + reach);
        if (shadow)
        {
            var offset = new Vector2(0f, 1f * scale);
            var shadowInk = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f));
            drawList.AddLine(upper + offset, tip + offset, shadowInk, thickness);
            drawList.AddLine(tip + offset, lower + offset, shadowInk, thickness);
        }

        var ink = ImGui.GetColorU32(hovered ? chevronInk : chevronInk with { W = chevronInk.W * 0.88f });
        drawList.AddLine(upper, tip, ink, thickness);
        drawList.AddLine(tip, lower, ink, thickness);
        var cap = thickness * 0.5f;
        drawList.AddCircleFilled(upper, cap, ink, 8);
        drawList.AddCircleFilled(tip, cap, ink, 8);
        drawList.AddCircleFilled(lower, cap, ink, 8);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
