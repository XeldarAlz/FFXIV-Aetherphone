using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Hunts;

internal static class ProgressBar
{
    public static void Draw(ImDrawListPtr drawList, float left, float right, float top, float height,
        double? percentage, double? fillPercentage, Vector4 color, TextStyle labelStyle, float labelGap,
        float trackAlpha = 0.18f)
    {
        var percentageLabel = percentage is { } known
            ? ((int)Math.Round(known, MidpointRounding.AwayFromZero)) + "%"
            : "?";
        var labelSize = Typography.Measure(percentageLabel, labelStyle);
        var radius = height * 0.5f;
        var trackMax = new Vector2(right - labelSize.X - labelGap, top + height);
        var trackMin = new Vector2(left, top);
        Squircle.Fill(drawList, trackMin, trackMax, radius, ImGui.GetColorU32(Palette.WithAlpha(color, trackAlpha)));

        var fraction = fillPercentage is { } value ? (float)Math.Clamp(value / 100.0, 0.0, 1.0) : 0f;
        if (fraction > 0f)
        {
            var fillMax = new Vector2(trackMin.X + (trackMax.X - trackMin.X) * fraction, trackMax.Y);
            Squircle.Fill(drawList, trackMin, fillMax, radius, ImGui.GetColorU32(color));
        }

        var labelPosition = new Vector2(right - labelSize.X, top + height * 0.5f - labelSize.Y * 0.5f);
        Typography.Draw(drawList, labelPosition, percentageLabel, color, labelStyle);
    }
}
