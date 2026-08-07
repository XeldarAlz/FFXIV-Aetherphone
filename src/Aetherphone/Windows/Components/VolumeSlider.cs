using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class VolumeSlider
{
    private const float IconColumn = 20f;
    private const float IconHeight = 14f;
    private const float ReadoutColumn = 42f;
    private const float QuietLevel = 0.5f;

    public static Slider.Result Draw(string id, Rect row, float value, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var gap = Metrics.Space.Md * scale;
        var result = Slider.Draw(id, row, value, theme, IconColumn * scale + gap, ReadoutColumn * scale + gap);
        ProgressRing.CenterIcon(ImGui.GetWindowDrawList(),
            new Vector2(row.Min.X + IconColumn * 0.5f * scale, row.Center.Y), LevelIcon(result.Value),
            theme.TextMuted, IconHeight * scale);
        DrawReadout(row, result.Value, theme);
        return result;
    }

    private static void DrawReadout(Rect row, float value, PhoneTheme theme)
    {
        var text = $"{(int)MathF.Round(value * 100f)}%";
        var size = Typography.Measure(text, TextStyles.Body);
        Typography.Draw(new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f), text, theme.TextMuted,
            TextStyles.Body);
    }

    private static FontAwesomeIcon LevelIcon(float value)
    {
        if (value <= 0.001f)
        {
            return FontAwesomeIcon.VolumeMute;
        }

        return value < QuietLevel ? FontAwesomeIcon.VolumeDown : FontAwesomeIcon.VolumeUp;
    }
}
