using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.Radio.Live;

internal sealed class RadioWrappedText
{
    private string? source;
    private float width = -1f;
    private float styleScale;
    private int fontGeneration = -1;

    public string[] Lines { get; private set; } = Array.Empty<string>();

    public float LineHeight { get; private set; }

    public float Height => Lines.Length * LineHeight;

    public void Wrap(string text, float maxWidth, in TextStyle style)
    {
        var generation = Plugin.Fonts.Generation;
        if (ReferenceEquals(source, text) && MathF.Abs(width - maxWidth) < 0.5f && styleScale == style.Scale
            && fontGeneration == generation)
        {
            return;
        }

        source = text;
        width = maxWidth;
        styleScale = style.Scale;
        fontGeneration = generation;
        Lines = Typography.WrapText(text, style, MathF.Max(1f, maxWidth));
        LineHeight = Typography.LineHeight(style);
    }

    public float Draw(ImDrawListPtr drawList, Vector2 topLeft, Vector4 color, in TextStyle style)
    {
        for (var index = 0; index < Lines.Length; index++)
        {
            Typography.Draw(drawList, new Vector2(topLeft.X, topLeft.Y + index * LineHeight), Lines[index], color,
                style);
        }

        return Height;
    }
}
