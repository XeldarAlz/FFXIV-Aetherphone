using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class Marquee
{
    private const float DwellSeconds = 0.6f;
    private const float Speed = 35f;
    private static readonly Dictionary<string, float> Elapsed = new(StringComparer.Ordinal);

    public static float DrawLeft(string id, string fullText, float boxLeft, float y, float maxWidth,
        in TextStyle style, Vector4 color, bool hovering) =>
        DrawLeft(ImGui.GetWindowDrawList(), id, fullText, boxLeft, y, maxWidth, style, color, hovering);

    public static float DrawLeft(ImDrawListPtr drawList, string id, string fullText, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 color, bool hovering)
    {
        var fullSize = Typography.Measure(fullText, style);
        if (fullSize.X <= maxWidth)
        {
            Elapsed.Remove(id);
            Typography.Draw(drawList, new Vector2(boxLeft, y), fullText, color, style);
            return fullSize.X;
        }

        var clipped = Typography.FitText(fullText, maxWidth, style);
        var clippedWidth = Typography.Measure(clipped, style).X;
        if (!hovering)
        {
            Elapsed.Remove(id);
            Typography.Draw(drawList, new Vector2(boxLeft, y), clipped, color, style);
            return clippedWidth;
        }

        var offset = Offset(id, fullSize.X - clippedWidth);
        var slack = 4f * ImGuiHelpers.GlobalScale;
        drawList.PushClipRect(new Vector2(boxLeft, y - slack), new Vector2(boxLeft + clippedWidth, y + fullSize.Y + slack),
            true);
        Typography.Draw(drawList, new Vector2(boxLeft - offset, y), fullText, color, style);
        drawList.PopClipRect();
        return clippedWidth;
    }

    public static float DrawLeftAuto(string id, string fullText, float boxLeft, float y, float maxWidth,
        in TextStyle style, Vector4 color) =>
        DrawLeftAuto(ImGui.GetWindowDrawList(), id, fullText, boxLeft, y, maxWidth, style, color);

    public static float DrawLeftAuto(ImDrawListPtr drawList, string id, string fullText, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 color)
    {
        var size = Typography.Measure(fullText, style);
        var hovering = ImGui.IsMouseHoveringRect(new Vector2(boxLeft, y),
            new Vector2(boxLeft + MathF.Min(size.X, maxWidth), y + size.Y));
        return DrawLeft(drawList, id, fullText, boxLeft, y, maxWidth, style, color, hovering);
    }

    public static void DrawRightAuto(string id, string fullText, float boxRight, float y, float maxWidth,
        in TextStyle style, Vector4 color) =>
        DrawRightAuto(ImGui.GetWindowDrawList(), id, fullText, boxRight, y, maxWidth, style, color);

    public static void DrawRightAuto(ImDrawListPtr drawList, string id, string fullText, float boxRight, float y,
        float maxWidth, in TextStyle style, Vector4 color)
    {
        var size = Typography.Measure(fullText, style);
        var hovering = ImGui.IsMouseHoveringRect(new Vector2(boxRight - MathF.Min(size.X, maxWidth), y),
            new Vector2(boxRight, y + size.Y));
        DrawRight(drawList, id, fullText, boxRight, y, maxWidth, style, color, hovering);
    }

    public static void DrawCenteredAuto(string id, string fullText, float centerX, float y, float maxWidth,
        in TextStyle style, Vector4 color) =>
        DrawCenteredAuto(ImGui.GetWindowDrawList(), id, fullText, centerX, y, maxWidth, style, color);

    public static void DrawCenteredAuto(ImDrawListPtr drawList, string id, string fullText, float centerX, float y,
        float maxWidth, in TextStyle style, Vector4 color)
    {
        var size = Typography.Measure(fullText, style);
        var clampedWidth = MathF.Min(size.X, maxWidth);
        var hovering = ImGui.IsMouseHoveringRect(new Vector2(centerX - clampedWidth * 0.5f, y),
            new Vector2(centerX + clampedWidth * 0.5f, y + size.Y));
        DrawCentered(drawList, id, fullText, centerX, y, maxWidth, style, color, hovering);
    }

    public static void DrawCentered(string id, string fullText, float centerX, float y, float maxWidth,
        in TextStyle style, Vector4 color, bool hovering) =>
        DrawCentered(ImGui.GetWindowDrawList(), id, fullText, centerX, y, maxWidth, style, color, hovering);

    public static void DrawCentered(ImDrawListPtr drawList, string id, string fullText, float centerX, float y,
        float maxWidth, in TextStyle style, Vector4 color, bool hovering)
    {
        var fullSize = Typography.Measure(fullText, style);
        if (fullSize.X <= maxWidth)
        {
            Elapsed.Remove(id);
            Typography.Draw(drawList, new Vector2(centerX - fullSize.X * 0.5f, y), fullText, color, style);
            return;
        }

        DrawLeft(drawList, id, fullText, centerX - maxWidth * 0.5f, y, maxWidth, style, color, hovering);
    }

    public static void DrawRight(string id, string fullText, float boxRight, float y, float maxWidth,
        in TextStyle style, Vector4 color, bool hovering) =>
        DrawRight(ImGui.GetWindowDrawList(), id, fullText, boxRight, y, maxWidth, style, color, hovering);

    public static void DrawRight(ImDrawListPtr drawList, string id, string fullText, float boxRight, float y,
        float maxWidth, in TextStyle style, Vector4 color, bool hovering)
    {
        var fullSize = Typography.Measure(fullText, style);
        if (fullSize.X <= maxWidth)
        {
            Elapsed.Remove(id);
            Typography.Draw(drawList, new Vector2(boxRight - fullSize.X, y), fullText, color, style);
            return;
        }

        var clipped = Typography.FitText(fullText, maxWidth, style);
        var clippedWidth = Typography.Measure(clipped, style).X;
        if (!hovering)
        {
            Elapsed.Remove(id);
            Typography.Draw(drawList, new Vector2(boxRight - clippedWidth, y), clipped, color, style);
            return;
        }

        var offset = Offset(id, fullSize.X - clippedWidth);
        var boxLeft = boxRight - clippedWidth;
        var slack = 4f * ImGuiHelpers.GlobalScale;
        drawList.PushClipRect(new Vector2(boxLeft, y - slack), new Vector2(boxRight, y + fullSize.Y + slack), true);
        Typography.Draw(drawList, new Vector2(boxLeft - offset, y), fullText, color, style);
        drawList.PopClipRect();
    }

    private static float Offset(string id, float overflow)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var travelSeconds = overflow / (Speed * scale);
        var cycle = DwellSeconds * 2f + travelSeconds * 2f;
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var elapsed = (Elapsed.TryGetValue(id, out var previous) ? previous : 0f) + deltaSeconds;
        elapsed %= cycle;
        Elapsed[id] = elapsed;

        if (elapsed < DwellSeconds)
        {
            return 0f;
        }

        if (elapsed < DwellSeconds + travelSeconds)
        {
            return (elapsed - DwellSeconds) * Speed * scale;
        }

        if (elapsed < DwellSeconds * 2f + travelSeconds)
        {
            return overflow;
        }

        return overflow - (elapsed - DwellSeconds * 2f - travelSeconds) * Speed * scale;
    }
}
