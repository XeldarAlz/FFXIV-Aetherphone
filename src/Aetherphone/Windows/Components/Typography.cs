using System.Numerics;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class Typography
{
    public static Vector2 Measure(string text, float scale = 1f) => Measure(text, scale, FontWeight.Regular);
    public static Vector2 Measure(string text, in TextStyle style) => Measure(text, style.Scale, style.Weight);

    public static Vector2 Measure(string text, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            return ImGui.CalcTextSize(text);
        }
    }

    public static void Draw(Vector2 position, string text, Vector4 color, float scale = 1f) =>
        Draw(position, text, color, scale, FontWeight.Regular);

    public static void Draw(Vector2 position, string text, Vector4 color, in TextStyle style) =>
        Draw(position, text, color, style.Scale, style.Weight);

    public static void Draw(Vector2 position, string text, Vector4 color, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            ImGui.SetCursorScreenPos(position);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }
        }
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float scale = 1f) =>
        Draw(drawList, position, text, color, scale, FontWeight.Regular);

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float scale,
        FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), position, ImGui.GetColorU32(color), text);
        }
    }

    public static void DrawCentered(Vector2 center, string text, Vector4 color, float scale = 1f) =>
        DrawCentered(center, text, color, scale, FontWeight.Regular);

    public static void DrawCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        float scale = 1f) =>
        DrawCentered(drawList, center, text, color, scale, FontWeight.Regular);

    public static void DrawCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color, float scale,
        FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            var size = ImGui.CalcTextSize(text);
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), center - size * 0.5f, ImGui.GetColorU32(color),
                text);
        }
    }

    public static void DrawCentered(Vector2 center, string text, Vector4 color, in TextStyle style) =>
        DrawCentered(center, text, color, style.Scale, style.Weight);

    public static void DrawCentered(Vector2 center, string text, Vector4 color, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            var size = ImGui.CalcTextSize(text);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }
        }
    }
}
