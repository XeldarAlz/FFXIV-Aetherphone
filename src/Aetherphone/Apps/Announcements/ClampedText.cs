using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Announcements;

internal sealed class ClampedText
{
    private const string Ellipsis = "…";

    private string source = string.Empty;
    private float width = -1f;
    private float fontKey = -1f;
    private int lineLimit = -1;
    private string[] lines = Array.Empty<string>();

    public string[] Get(string text, in TextStyle style, float maxWidth, int maxLines, float currentFontKey)
    {
        if (ReferenceEquals(text, source) && width == maxWidth && fontKey == currentFontKey && lineLimit == maxLines)
        {
            return lines;
        }

        source = text;
        width = maxWidth;
        fontKey = currentFontKey;
        lineLimit = maxLines;
        lines = Clamp(text, style, maxWidth, maxLines);
        return lines;
    }

    private static string[] Clamp(string text, in TextStyle style, float maxWidth, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0f || maxLines <= 0)
        {
            return Array.Empty<string>();
        }

        var wrapped = Typography.WrapText(text, style, maxWidth);
        if (wrapped.Length <= maxLines)
        {
            return wrapped;
        }

        var trimmed = new string[maxLines];
        Array.Copy(wrapped, trimmed, maxLines);
        trimmed[maxLines - 1] = Typography.FitText(trimmed[maxLines - 1] + Ellipsis, maxWidth, style);
        return trimmed;
    }
}
