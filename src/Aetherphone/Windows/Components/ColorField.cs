using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ColorField
{
    public readonly struct Result
    {
        public readonly HsvColor Color;
        public readonly bool Dragging;
        public readonly bool Released;

        public Result(HsvColor color, bool dragging, bool released)
        {
            Color = color;
            Dragging = dragging;
            Released = released;
        }
    }

    private readonly struct Drag
    {
        public readonly bool Active;
        public readonly bool Released;
        public readonly Vector2 Position;

        public Drag(bool active, bool released, Vector2 position)
        {
            Active = active;
            Released = released;
            Position = position;
        }
    }

    private const float RailHeight = 20f;
    private const float HandleRadius = 10f;
    private const float HandleRing = 3f;
    private const int HueStops = 6;
    private const int CornerSegments = 8;
    private const int CircleSegments = 32;

    private static readonly Vector4 HandleInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 HandleShadow = new(0f, 0f, 0f, 0.35f);

    private static string activeId = string.Empty;
    private static bool activeIsRail;
    private static int activeFrame = -1;

    public static Result Draw(string id, Rect area, HsvColor color, PhoneTheme theme, uint maskColor)
    {
        var scale = UiScale.Current;
        var railHeight = RailHeight * scale;
        var gap = Metrics.Space.Md * scale;
        var square = new Rect(area.Min, new Vector2(area.Max.X, area.Max.Y - railHeight - gap));
        var rail = new Rect(new Vector2(area.Min.X, area.Max.Y - railHeight), area.Max);
        var shadeDrag = Track(id, false, square);
        var hueDrag = Track(id, true, rail);
        var picked = color;
        if (shadeDrag.Active || shadeDrag.Released)
        {
            picked = new HsvColor(picked.Hue, shadeDrag.Position.X, 1f - shadeDrag.Position.Y);
        }

        if (hueDrag.Active || hueDrag.Released)
        {
            picked = new HsvColor(hueDrag.Position.X, picked.Saturation, picked.Value);
        }

        var drawList = ImGui.GetWindowDrawList();
        DrawShades(drawList, square, picked.Hue, Metrics.Radius.Md * scale, maskColor, theme, scale);
        DrawHues(drawList, rail, railHeight * 0.5f, maskColor, theme, scale);
        DrawHandle(drawList,
            new Vector2(square.Min.X + picked.Saturation * square.Width,
                square.Min.Y + (1f - picked.Value) * square.Height),
            picked.ToRgb(), HandleRadius * scale, scale);
        DrawHandle(drawList, new Vector2(rail.Min.X + picked.Hue * rail.Width, rail.Center.Y),
            new HsvColor(picked.Hue, 1f, 1f).ToRgb(), railHeight * 0.5f, scale);
        return new Result(picked, shadeDrag.Active || hueDrag.Active, shadeDrag.Released || hueDrag.Released);
    }

    private static void DrawShades(ImDrawListPtr drawList, Rect area, float hue, float radius, uint maskColor,
        PhoneTheme theme, float scale)
    {
        var pure = ImGui.GetColorU32(new HsvColor(hue, 1f, 1f).ToRgb());
        var white = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
        var clear = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f));
        var black = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f));
        drawList.AddRectFilledMultiColor(area.Min, area.Max, white, pure, pure, white);
        drawList.AddRectFilledMultiColor(area.Min, area.Max, clear, clear, black, black);
        MaskCorners(drawList, area, radius, maskColor);
        Edge(drawList, area, radius, theme, scale);
    }

    private static void DrawHues(ImDrawListPtr drawList, Rect rail, float radius, uint maskColor, PhoneTheme theme,
        float scale)
    {
        var stopWidth = rail.Width / HueStops;
        for (var stop = 0; stop < HueStops; stop++)
        {
            var left = rail.Min.X + stopWidth * stop;
            var leading = ImGui.GetColorU32(new HsvColor(stop / (float)HueStops, 1f, 1f).ToRgb());
            var trailing = ImGui.GetColorU32(new HsvColor((stop + 1) / (float)HueStops, 1f, 1f).ToRgb());
            drawList.AddRectFilledMultiColor(new Vector2(left, rail.Min.Y), new Vector2(left + stopWidth, rail.Max.Y),
                leading, trailing, trailing, leading);
        }

        MaskCorners(drawList, rail, radius, maskColor);
        Edge(drawList, rail, radius, theme, scale);
    }

    private static void Edge(ImDrawListPtr drawList, Rect area, float radius, PhoneTheme theme, float scale)
    {
        drawList.AddRect(area.Min, area.Max, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.12f)), radius,
            ImDrawFlags.RoundCornersAll, Metrics.Stroke.Hairline * scale);
    }

    private static void MaskCorners(ImDrawListPtr drawList, Rect area, float radius, uint maskColor)
    {
        Wedge(drawList, area.Min, new Vector2(area.Min.X + radius, area.Min.Y + radius), radius, MathF.PI,
            MathF.PI * 1.5f, maskColor);
        Wedge(drawList, new Vector2(area.Max.X, area.Min.Y), new Vector2(area.Max.X - radius, area.Min.Y + radius),
            radius, MathF.PI * 1.5f, MathF.Tau, maskColor);
        Wedge(drawList, area.Max, new Vector2(area.Max.X - radius, area.Max.Y - radius), radius, 0f, MathF.PI * 0.5f,
            maskColor);
        Wedge(drawList, new Vector2(area.Min.X, area.Max.Y), new Vector2(area.Min.X + radius, area.Max.Y - radius),
            radius, MathF.PI * 0.5f, MathF.PI, maskColor);
    }

    private static void Wedge(ImDrawListPtr drawList, Vector2 corner, Vector2 center, float radius, float from,
        float to, uint color)
    {
        drawList.PathLineTo(corner);
        drawList.PathArcTo(center, radius, from, to, CornerSegments);
        drawList.PathFillConvex(color);
    }

    private static void DrawHandle(ImDrawListPtr drawList, Vector2 center, Vector4 color, float radius, float scale)
    {
        drawList.AddCircleFilled(center, radius + HandleRing * scale, ImGui.GetColorU32(HandleShadow),
            CircleSegments);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(HandleInk), CircleSegments);
        drawList.AddCircleFilled(center, radius - HandleRing * scale, ImGui.GetColorU32(color), CircleSegments);
    }

    private static Drag Track(string id, bool rail, Rect area)
    {
        var hovered = UiInteract.Hover(area.Min, area.Max);
        var owns = Owns(id, rail);
        if (hovered && !owns && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            activeId = id;
            activeIsRail = rail;
            owns = true;
        }

        if (hovered || owns)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!owns)
        {
            return default;
        }

        var mouse = ImGui.GetMousePos();
        var position = new Vector2(Math.Clamp((mouse.X - area.Min.X) / MathF.Max(area.Width, 1f), 0f, 1f),
            Math.Clamp((mouse.Y - area.Min.Y) / MathF.Max(area.Height, 1f), 0f, 1f));
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            activeFrame = ImGui.GetFrameCount();
            return new Drag(true, false, position);
        }

        activeId = string.Empty;
        return new Drag(false, true, position);
    }

    private static bool Owns(string id, bool rail)
    {
        if (activeIsRail != rail || !string.Equals(activeId, id, StringComparison.Ordinal))
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
}
