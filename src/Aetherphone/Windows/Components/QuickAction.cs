using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class QuickAction
{
    private const float LabelGap = 8f;
    private const float PressSmoothTime = 0.09f;
    private static readonly Dictionary<string, Spring> Scales = new(StringComparer.Ordinal);

    public static bool Draw(string id, Vector2 center, float radius, FontAwesomeIcon icon, Vector4 tint, string label,
        PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var hitMin = new Vector2(center.X - radius, center.Y - radius);
        var hitMax = new Vector2(center.X + radius, center.Y + radius + LabelGap * scale + labelSize.Y);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        if (!Scales.TryGetValue(id, out var spring))
        {
            spring = new Spring(1f);
        }

        var grow = spring.Step(pressed ? 0.90f :
            hovered ? 1.05f : 1f, PressSmoothTime, deltaSeconds);
        Scales[id] = spring;
        var drawnRadius = radius * grow;
        var fill = hovered ? Palette.Mix(tint, theme.TextStrong, 0.12f) : tint;
        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), drawnRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f)), 40);
        drawList.AddCircleFilled(center, drawnRadius, ImGui.GetColorU32(fill), 40);
        drawList.AddCircleFilled(center - new Vector2(0f, drawnRadius * 0.34f), drawnRadius * 0.56f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 32);
        ProgressRing.CenterIcon(center, icon, new Vector4(0.99f, 0.99f, 1f, 1f), drawnRadius * 0.80f);
        Typography.DrawCentered(new Vector2(center.X, center.Y + radius + LabelGap * scale + labelSize.Y * 0.5f), label,
            theme.TextMuted, TextStyles.Caption1);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
