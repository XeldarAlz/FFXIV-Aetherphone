using System.Numerics;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Equalizer
{
    public static void Draw(ImDrawListPtr drawList, Vector2 center, float scale, float maxHeight, float clock,
        Vector4 color, float alpha, bool animate)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var barWidth = 3f * scale;
        var gap = 2.6f * scale;
        var packed = ImGui.GetColorU32(Palette.WithAlpha(color, alpha));
        for (var bar = 0; bar < 3; bar++)
        {
            var phase = clock * 6f + bar * 1.35f;
            var amplitude = animate ? 0.35f + 0.65f * MathF.Abs(MathF.Sin(phase)) : 0.42f;
            var height = maxHeight * amplitude;
            var left = center.X + (bar - 1) * (barWidth + gap) - barWidth * 0.5f;
            drawList.AddRectFilled(new Vector2(left, center.Y - height * 0.5f),
                new Vector2(left + barWidth, center.Y + height * 0.5f), packed, barWidth * 0.4f);
        }
    }
}
