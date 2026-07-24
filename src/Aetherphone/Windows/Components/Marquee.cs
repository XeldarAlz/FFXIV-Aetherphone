using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Marquee
{
    private const float DwellSeconds = 0.6f;
    private const float Speed = 35f;
    private static readonly Dictionary<string, float> Elapsed = new(StringComparer.Ordinal);

    public static float DrawLeft(string id, string fullText, float boxLeft, float y, float maxWidth,
        in TextStyle style, Vector4 color, bool hovering)
    {
        var fullSize = Typography.Measure(fullText, style);
        if (fullSize.X <= maxWidth)
        {
            Elapsed.Remove(id);
            Typography.Draw(new Vector2(boxLeft, y), fullText, color, style);
            return fullSize.X;
        }

        var clipped = Typography.FitText(fullText, maxWidth, style);
        var clippedWidth = Typography.Measure(clipped, style).X;
        if (!hovering)
        {
            Elapsed.Remove(id);
            Typography.Draw(new Vector2(boxLeft, y), clipped, color, style);
            return clippedWidth;
        }

        var offset = Offset(id, fullSize.X - clippedWidth);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(new Vector2(boxLeft, y - 4f), new Vector2(boxLeft + clippedWidth, y + fullSize.Y + 4f),
            true);
        Typography.Draw(new Vector2(boxLeft - offset, y), fullText, color, style);
        drawList.PopClipRect();
        return clippedWidth;
    }

    public static float DrawLeftAuto(string id, string fullText, float boxLeft, float y, float maxWidth,
        in TextStyle style, Vector4 color)
    {
        var size = Typography.Measure(fullText, style);
        var hovering = ImGui.IsMouseHoveringRect(new Vector2(boxLeft, y),
            new Vector2(boxLeft + MathF.Min(size.X, maxWidth), y + size.Y));
        return DrawLeft(id, fullText, boxLeft, y, maxWidth, style, color, hovering);
    }

    public static void DrawRightAuto(string id, string fullText, float boxRight, float y, float maxWidth,
        in TextStyle style, Vector4 color)
    {
        var size = Typography.Measure(fullText, style);
        var hovering = ImGui.IsMouseHoveringRect(new Vector2(boxRight - MathF.Min(size.X, maxWidth), y),
            new Vector2(boxRight, y + size.Y));
        DrawRight(id, fullText, boxRight, y, maxWidth, style, color, hovering);
    }

    public static void DrawCenteredAuto(string id, string fullText, float centerX, float y, float maxWidth,
        in TextStyle style, Vector4 color)
    {
        var size = Typography.Measure(fullText, style);
        var clampedWidth = MathF.Min(size.X, maxWidth);
        var hovering = ImGui.IsMouseHoveringRect(new Vector2(centerX - clampedWidth * 0.5f, y),
            new Vector2(centerX + clampedWidth * 0.5f, y + size.Y));
        DrawCentered(id, fullText, centerX, y, maxWidth, style, color, hovering);
    }

    public static void DrawCentered(string id, string fullText, float centerX, float y, float maxWidth,
        in TextStyle style, Vector4 color, bool hovering)
    {
        var fullSize = Typography.Measure(fullText, style);
        if (fullSize.X <= maxWidth)
        {
            Elapsed.Remove(id);
            Typography.Draw(new Vector2(centerX - fullSize.X * 0.5f, y), fullText, color, style);
            return;
        }

        DrawLeft(id, fullText, centerX - maxWidth * 0.5f, y, maxWidth, style, color, hovering);
    }

    public static void DrawRight(string id, string fullText, float boxRight, float y, float maxWidth,
        in TextStyle style, Vector4 color, bool hovering)
    {
        var fullSize = Typography.Measure(fullText, style);
        if (fullSize.X <= maxWidth)
        {
            Elapsed.Remove(id);
            Typography.Draw(new Vector2(boxRight - fullSize.X, y), fullText, color, style);
            return;
        }

        var clipped = Typography.FitText(fullText, maxWidth, style);
        var clippedWidth = Typography.Measure(clipped, style).X;
        if (!hovering)
        {
            Elapsed.Remove(id);
            Typography.Draw(new Vector2(boxRight - clippedWidth, y), clipped, color, style);
            return;
        }

        var offset = Offset(id, fullSize.X - clippedWidth);
        var boxLeft = boxRight - clippedWidth;
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(new Vector2(boxLeft, y - 4f), new Vector2(boxRight, y + fullSize.Y + 4f), true);
        Typography.Draw(new Vector2(boxLeft - offset, y), fullText, color, style);
        drawList.PopClipRect();
    }

    private static float Offset(string id, float overflow)
    {
        var travelSeconds = overflow / Speed;
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
            return (elapsed - DwellSeconds) * Speed;
        }

        if (elapsed < DwellSeconds * 2f + travelSeconds)
        {
            return overflow;
        }

        return overflow - (elapsed - DwellSeconds * 2f - travelSeconds) * Speed;
    }
}
