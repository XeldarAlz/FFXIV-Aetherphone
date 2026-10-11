using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Machines;

internal static class MachineText
{
    public const float LabelShrink = 0.7f;
    public const float DisplayShrink = 0.5f;

    public static TextStyle Fit(string text, float width, in TextStyle style, float minimumShare = LabelShrink)
    {
        var scale = Typography.FitScale(text, MathF.Max(1f, width), style.Scale, style.Scale * minimumShare,
            style.Weight);
        return scale >= style.Scale ? style : style with { Scale = scale };
    }
}
