using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ProgressBar
{
    public static void Draw(ImDrawListPtr drawList, float left, float right, float top, float height,
        double percentage, Vector4 color, TextStyle labelStyle, float labelGap, float trackAlpha = 0.18f)
    {
        var percentageLabel = ((int)Math.Round(percentage, MidpointRounding.AwayFromZero)) + "%";
        var labelSize = Typography.Measure(percentageLabel, labelStyle);
        var radius = height * 0.5f;
        var trackMax = new Vector2(right - labelSize.X - labelGap, top + height);
        var trackMin = new Vector2(left, top);
        Squircle.Fill(drawList, trackMin, trackMax, radius, ImGui.GetColorU32(Palette.WithAlpha(color, trackAlpha)));

        var fraction = (float)Math.Clamp(percentage / 100.0, 0.0, 1.0);
        if (fraction > 0f)
        {
            var fillMax = new Vector2(trackMin.X + (trackMax.X - trackMin.X) * fraction, trackMax.Y);
            Squircle.Fill(drawList, trackMin, fillMax, radius, ImGui.GetColorU32(color));
        }

        var labelPosition = new Vector2(right - labelSize.X, top + height * 0.5f - labelSize.Y * 0.5f);
        Typography.Draw(drawList, labelPosition, percentageLabel, color, labelStyle);
    }
}
