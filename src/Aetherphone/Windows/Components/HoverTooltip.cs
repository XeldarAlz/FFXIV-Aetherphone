using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class HoverTooltip
{
    private const float SmoothTime = 0.11f;
    private const float MaxFrameSeconds = 0.1f;
    private static readonly Dictionary<string, Spring> springs = new();

    public static void Show(Rect target, string label, HoverLabelSide side = HoverLabelSide.Below) =>
        Show(Key(target), target, label, side);

    public static void Show(string id, Rect target, string label, HoverLabelSide side = HoverLabelSide.Below)
    {
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        var hovered = ImGui.IsMouseHoveringRect(target.Min, target.Max);
        var eased = Step(id, hovered);
        if (eased <= 0.001f)
        {
            return;
        }

        DrawLabel(ImGui.GetForegroundDrawList(), target, label, eased, side);
    }

    public static void DrawLabel(ImDrawListPtr dl, Rect target, string label, float alpha, HoverLabelSide side)
    {
        if (alpha <= 0.001f || string.IsNullOrEmpty(label))
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        const float textScale = 0.7f;
        var textSize = Typography.Measure(label, textScale, FontWeight.SemiBold);
        var padX = 9f * scale;
        var padY = 5f * scale;
        var width = textSize.X + 2f * padX;
        var height = textSize.Y + 2f * padY;
        var gap = 7f * scale;
        var rise = (1f - alpha) * 4f * scale;
        var centerY = side == HoverLabelSide.Above
            ? target.Min.Y - gap - height * 0.5f + rise
            : target.Max.Y + gap + height * 0.5f - rise;
        var center = new Vector2(target.Center.X, centerY);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var rounding = height * 0.5f;
        Elevation.Floating(dl, min, max, rounding, scale, alpha);
        Squircle.Fill(dl, min, max, rounding, ImGui.GetColorU32(new Vector4(0.10f, 0.10f, 0.12f, 0.96f * alpha)));
        Material.EdgeSquircle(dl, min, max, rounding, scale, alpha);
        Typography.DrawCentered(dl, center, label, new Vector4(0.96f, 0.96f, 0.98f, alpha), textScale,
            FontWeight.SemiBold);
    }

    private static float Step(string id, bool hovered)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        if (!springs.TryGetValue(id, out var spring))
        {
            spring = default;
        }

        spring.Step(hovered ? 1f : 0f, SmoothTime, delta);
        var value = Math.Clamp(spring.Value, 0f, 1f);
        if (!hovered && value <= 0.001f)
        {
            springs.Remove(id);
            return 0f;
        }

        springs[id] = spring;
        return value;
    }

    private static string Key(Rect target)
    {
        var x = (int)MathF.Round(target.Center.X);
        var y = (int)MathF.Round(target.Center.Y);
        return string.Concat("t:", x.ToString(), ":", y.ToString());
    }
}
