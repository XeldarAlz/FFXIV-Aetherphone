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

    public static Slider.Result Draw(string id, Rect row, float value, PhoneTheme theme, float maximum = 1f,
        FontAwesomeIcon? icon = null)
    {
        var result = DrawTrack(id, row, value / maximum, theme, icon);
        var scaled = result.Value * maximum;
        DrawReadout(row, $"{(int)MathF.Round(scaled * 100f)}%", theme);
        return new Slider.Result(scaled, result.Dragging, result.Released, result.Track);
    }

    public static Slider.Result DrawStepped(string id, Rect row, float value, PhoneTheme theme, FontAwesomeIcon icon,
        IReadOnlyList<string> readouts)
    {
        var result = DrawTrack(id, row, value, theme, icon);
        var step = (int)MathF.Round(result.Value * (readouts.Count - 1));
        DrawReadout(row, readouts[step], theme);
        return result;
    }

    private static Slider.Result DrawTrack(string id, Rect row, float value, PhoneTheme theme, FontAwesomeIcon? icon)
    {
        var scale = UiScale.Current;
        var gap = Metrics.Space.Md * scale;
        var result = Slider.Draw(id, row, value, theme, IconColumn * scale + gap, ReadoutColumn * scale + gap);
        ProgressRing.CenterIcon(ImGui.GetWindowDrawList(),
            new Vector2(row.Min.X + IconColumn * 0.5f * scale, row.Center.Y), icon ?? LevelIcon(result.Value),
            theme.TextMuted, IconHeight * scale);
        return result;
    }

    private static void DrawReadout(Rect row, string text, PhoneTheme theme)
    {
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
