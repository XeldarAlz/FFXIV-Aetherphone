using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class LinkpearlSettingRows
{
    public const float PopoutOpacityMinimum = 0.5f;
    public const float IdleOpacityMinimum = 0.15f;
    public const float ScaleEpsilon = 0.01f;
    public const float SliderLabelWidth = 0.42f;

    private const float OpacityEpsilon = 0.002f;

    public static readonly float[] TextScaleChoices = { 0.8f, 0.9f, 1f, 1.15f, 1.3f, 1.5f };

    public static string PercentLabel(float value) =>
        string.Concat(MathF.Round(value * 100f).ToString(Loc.Culture), "%");

    public static float OpacitySlider(Rect row, float scale, string id, string label, float value, float minimum,
        PhoneTheme theme, out bool released)
    {
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        var labelWidth = row.Width * SliderLabelWidth;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f),
            Typography.FitText(label, labelWidth, TextStyles.BodyEmphasized), theme.TextStrong,
            TextStyles.BodyEmphasized);
        var span = 1f - minimum;
        var normalized = (Math.Clamp(value, minimum, 1f) - minimum) / span;
        var result = Slider.Draw(id, row, normalized, theme, labelWidth + Metrics.Space.Md * scale,
            Metrics.Space.Xs * scale);
        released = result.Released;
        var next = minimum + result.Value * span;
        return MathF.Abs(next - value) > OpacityEpsilon ? next : value;
    }
}
