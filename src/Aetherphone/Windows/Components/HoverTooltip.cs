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
    private static readonly List<PendingLabel> pending = new();
    private static int frame = -1;

    private readonly struct PendingLabel
    {
        public readonly Rect Target;
        public readonly string Label;
        public readonly float Alpha;
        public readonly HoverLabelSide Side;

        public PendingLabel(Rect target, string label, float alpha, HoverLabelSide side)
        {
            Target = target;
            Label = label;
            Alpha = alpha;
            Side = side;
        }
    }

    public static void Show(Rect target, string label, HoverLabelSide side = HoverLabelSide.Below)
    {
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        Show(Key(target), target, label, side);
    }

    public static void Show(string id, Rect target, string label, HoverLabelSide side = HoverLabelSide.Below)
    {
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        var hovered = UiInteract.Hover(target.Min, target.Max);
        var eased = Step(id, hovered);
        if (eased <= 0.001f)
        {
            return;
        }

        Sync();
        pending.Add(new PendingLabel(target, label, eased, side));
    }

    public static void Enqueue(Rect target, string label, float alpha, HoverLabelSide side)
    {
        if (alpha <= 0.001f || string.IsNullOrEmpty(label))
        {
            return;
        }

        Sync();
        pending.Add(new PendingLabel(target, label, alpha, side));
    }

    public static void Flush()
    {
        Sync();
        if (pending.Count == 0)
        {
            return;
        }

        var dl = ImGui.GetForegroundDrawList();
        for (var index = 0; index < pending.Count; index++)
        {
            var label = pending[index];
            DrawLabel(dl, label.Target, label.Label, label.Alpha, label.Side);
        }

        pending.Clear();
    }

    private static void Sync()
    {
        var current = ImGui.GetFrameCount();
        if (current == frame)
        {
            return;
        }

        frame = current;
        pending.Clear();
    }

    private static void DrawLabel(ImDrawListPtr dl, Rect target, string label, float alpha, HoverLabelSide side)
    {
        if (alpha <= 0.001f || string.IsNullOrEmpty(label))
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var style = TextStyles.SubheadlineEmphasized;
        var padX = 11f * scale;
        var padY = 7f * scale;
        var margin = 8f * scale;
        var windowLeft = ImGui.GetWindowPos().X;
        var left = windowLeft + ImGui.GetWindowContentRegionMin().X + margin;
        var right = windowLeft + ImGui.GetWindowContentRegionMax().X - margin;
        var maxTextWidth = right - left - 2f * padX;
        var lineSize = Typography.Measure(label, style);
        var wrapped = maxTextWidth > 0f && lineSize.X > maxTextWidth;
        var textSize = wrapped ? Typography.MeasureWrappedBlock(label, style, maxTextWidth) : lineSize;
        var width = textSize.X + 2f * padX;
        var height = textSize.Y + 2f * padY;
        var gap = 7f * scale;
        var rise = (1f - alpha) * 4f * scale;
        var centerY = side == HoverLabelSide.Above
            ? target.Min.Y - gap - height * 0.5f + rise
            : target.Max.Y + gap + height * 0.5f - rise;
        var centerX = right - left > width
            ? Math.Clamp(target.Center.X, left + width * 0.5f, right - width * 0.5f)
            : (left + right) * 0.5f;
        var center = new Vector2(centerX, centerY);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var rounding = MathF.Min(height, lineSize.Y + 2f * padY) * 0.5f;
        Elevation.Floating(dl, min, max, rounding, scale, alpha);
        Squircle.Fill(dl, min, max, rounding, ImGui.GetColorU32(new Vector4(0.10f, 0.10f, 0.12f, 0.96f * alpha)));
        Material.EdgeSquircle(dl, min, max, rounding, scale, alpha);
        var color = new Vector4(0.96f, 0.96f, 0.98f, alpha);
        if (wrapped)
        {
            Typography.DrawWrappedCentered(dl, center, label, color, style, maxTextWidth);
        }
        else
        {
            Typography.DrawCentered(dl, center, label, color, style);
        }
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
