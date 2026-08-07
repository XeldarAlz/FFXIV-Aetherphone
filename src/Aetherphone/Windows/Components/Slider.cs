using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Slider
{
    public readonly struct Result
    {
        public readonly float Value;
        public readonly bool Dragging;
        public readonly bool Released;
        public readonly Rect Track;

        public Result(float value, bool dragging, bool released, Rect track)
        {
            Value = value;
            Dragging = dragging;
            Released = released;
            Track = track;
        }
    }

    private const float TrackThickness = 4f;
    private const float KnobRadius = 7f;
    private const float KnobGrowth = 0.22f;
    private const float KnobSmoothTime = 0.12f;

    private static readonly Vector4 KnobInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 KnobShadow = new(0f, 0f, 0f, 0.28f);
    private static readonly Dictionary<string, Spring> Knobs = new(StringComparer.Ordinal);

    private static string activeId = string.Empty;
    private static float activeValue;
    private static int activeFrame = -1;

    public static Result Draw(string id, Rect row, float value, PhoneTheme theme, float leadingInset,
        float trailingInset)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var knobRadius = KnobRadius * scale;
        var trackLeft = row.Min.X + leadingInset;
        var trackRight = row.Max.X - trailingInset;
        var width = MathF.Max(trackRight - trackLeft, 1f);
        var hovered = UiInteract.Hover(new Vector2(trackLeft - knobRadius, row.Min.Y),
            new Vector2(trackRight + knobRadius, row.Max.Y));
        var dragging = Owns(id);
        var released = false;
        if (hovered && !dragging && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            activeId = id;
            dragging = true;
        }

        if (dragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                activeValue = Math.Clamp((ImGui.GetMousePos().X - trackLeft) / width, 0f, 1f);
                activeFrame = ImGui.GetFrameCount();
            }
            else
            {
                activeId = string.Empty;
                dragging = false;
                released = true;
            }
        }

        var current = dragging || released ? activeValue : Math.Clamp(value, 0f, 1f);
        var engaged = Engagement(id, hovered || dragging);
        var midY = row.Center.Y;
        var railMin = new Vector2(trackLeft, midY - TrackThickness * 0.5f * scale);
        var railMax = new Vector2(trackRight, midY + TrackThickness * 0.5f * scale);
        var railRadius = TrackThickness * 0.5f * scale;
        drawList.AddRectFilled(railMin, railMax, ImGui.GetColorU32(theme.ToggleOff), railRadius);
        var knobX = trackLeft + width * current;
        drawList.AddRectFilled(railMin, new Vector2(knobX, railMax.Y), ImGui.GetColorU32(theme.Accent), railRadius);
        var knobCenter = new Vector2(knobX, midY);
        var knobSize = knobRadius * (1f + KnobGrowth * engaged);
        drawList.AddCircleFilled(knobCenter + new Vector2(0f, 1.5f * scale), knobSize,
            ImGui.GetColorU32(KnobShadow), 24);
        drawList.AddCircleFilled(knobCenter, knobSize, ImGui.GetColorU32(KnobInk), 24);
        if (hovered || dragging)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return new Result(current, dragging, released,
            new Rect(new Vector2(trackLeft, row.Min.Y), new Vector2(trackRight, row.Max.Y)));
    }

    private static bool Owns(string id)
    {
        if (!string.Equals(activeId, id, StringComparison.Ordinal))
        {
            return false;
        }

        if (ImGui.GetFrameCount() - activeFrame <= 1)
        {
            return true;
        }

        activeId = string.Empty;
        return false;
    }

    private static float Engagement(string id, bool engaged)
    {
        var spring = Knobs.TryGetValue(id, out var stored) ? stored : new Spring(0f);
        var position = spring.Step(engaged ? 1f : 0f, KnobSmoothTime, MathF.Min(ImGui.GetIO().DeltaTime, 0.1f));
        Knobs[id] = spring;
        return Math.Clamp(position, 0f, 1f);
    }
}
