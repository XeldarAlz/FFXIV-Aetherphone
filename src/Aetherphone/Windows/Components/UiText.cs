using System;
using System.Numerics;
using System.Text;

namespace Aetherphone.Windows.Components;

internal static class UiText
{
    public static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";

    public static float WrappedCentered(float centerX, float top, string text, float maxWidth, Vector4 color,
        float scale, float fontScale)
    {
        var lineHeight = Typography.Measure("Ay", fontScale).Y + 3f * scale;
        var paragraphs = text.Split('\n');
        var line = new StringBuilder();
        var y = top;
        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
        {
            var words = paragraphs[paragraphIndex].TrimEnd('\r').Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                y += lineHeight;
                continue;
            }

            line.Clear();
            for (var index = 0; index < words.Length; index++)
            {
                var candidate = line.Length == 0 ? words[index] : $"{line} {words[index]}";
                if (line.Length > 0 && Typography.Measure(candidate, fontScale).X > maxWidth)
                {
                    Typography.DrawCentered(new Vector2(centerX, y + lineHeight * 0.5f), line.ToString(), color, fontScale);
                    y += lineHeight;
                    line.Clear();
                    line.Append(words[index]);
                }
                else
                {
                    line.Clear();
                    line.Append(candidate);
                }
            }

            if (line.Length > 0)
            {
                Typography.DrawCentered(new Vector2(centerX, y + lineHeight * 0.5f), line.ToString(), color, fontScale);
                y += lineHeight;
            }
        }

        return y - top;
    }
}
