using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class PitText
{
    public static float Height(string text, in TextStyle style, float maxWidth)
    {
        if (text.Length == 0)
        {
            return 0f;
        }

        return Typography.WrapText(text, style, maxWidth).Length * Typography.LineHeight(style);
    }

    public static float Draw(ImDrawListPtr drawList, string text, in TextStyle style, Vector4 ink, float left,
        float top, float maxWidth, bool centered)
    {
        if (text.Length == 0)
        {
            return top;
        }

        var lines = Typography.WrapText(text, style, maxWidth);
        var lineHeight = Typography.LineHeight(style);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var x = centered ? left + (maxWidth - Typography.Measure(line, style).X) * 0.5f : left;
            Typography.Draw(drawList, new Vector2(x, top + index * lineHeight), line, ink, style);
        }

        return top + lines.Length * lineHeight;
    }
}
