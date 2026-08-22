using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class InlineBadge
{
    private const float PaddingX = 5f;
    private const float Height = 16f;
    private const float Rounding = 4f;
    private const float FillAlpha = 0.22f;

    public static float Width(string label, float scale, float minWidth = 0f) =>
        MathF.Max(minWidth, Typography.Measure(label, TextStyles.Caption2).X + PaddingX * 2f * scale);

    public static float Draw(ImDrawListPtr drawList, float left, float centerY, string label, Vector4 ink,
        float scale, float minWidth = 0f)
    {
        var width = Width(label, scale, minWidth);
        var height = Height * scale;
        var min = new Vector2(left, centerY - height * 0.5f);
        var max = new Vector2(left + width, centerY + height * 0.5f);

        Squircle.Fill(drawList, min, max, Rounding * scale, ImGui.GetColorU32(Palette.WithAlpha(ink, FillAlpha)));
        Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, centerY), label, ink, TextStyles.Caption2);
        return width;
    }
}
