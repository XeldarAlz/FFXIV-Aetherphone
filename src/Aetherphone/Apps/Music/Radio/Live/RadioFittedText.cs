using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Music.Radio.Live;

internal sealed class RadioFittedText
{
    private string? source;
    private float width = -1f;
    private float styleScale;
    private int fontGeneration = -1;
    private string fitted = string.Empty;

    public string Fit(string text, float maxWidth, in TextStyle style)
    {
        var generation = Plugin.Fonts.Generation;
        if (ReferenceEquals(source, text) && MathF.Abs(width - maxWidth) < 0.5f && styleScale == style.Scale
            && fontGeneration == generation)
        {
            return fitted;
        }

        source = text;
        width = maxWidth;
        styleScale = style.Scale;
        fontGeneration = generation;
        fitted = Typography.FitText(text, MathF.Max(1f, maxWidth), style);
        return fitted;
    }
}
