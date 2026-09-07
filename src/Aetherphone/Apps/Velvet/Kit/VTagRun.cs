using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VTagRun
{
    private const float ItemGap = 10f;
    private const float LineGap = 4f;
    private const float HitPadX = 4f;
    private const float HitPadY = 2f;
    private const float UnderlineLift = 1f;

    private static readonly TextStyle LabelStyle = TextStyles.Footnote;

    public static float Height(string[] labels, float width, float scale)
    {
        if (labels.Length == 0)
        {
            return 0f;
        }

        var lines = 1;
        var cursor = 0f;
        for (var index = 0; index < labels.Length; index++)
        {
            var labelWidth = Typography.Measure(labels[index], LabelStyle).X;
            if (cursor > 0f && cursor + labelWidth > width)
            {
                lines++;
                cursor = 0f;
            }

            cursor += labelWidth + ItemGap * scale;
        }

        return lines * Typography.LineHeight(LabelStyle) + (lines - 1) * LineGap * scale;
    }

    public static int Draw(ImDrawListPtr drawList, Vector2 origin, float width, string[] labels, Vector4 ink,
        Vector4 hoverInk, float scale)
    {
        var lineHeight = Typography.LineHeight(LabelStyle);
        var cursor = 0f;
        var top = origin.Y;
        var clicked = -1;
        for (var index = 0; index < labels.Length; index++)
        {
            var label = labels[index];
            var labelWidth = Typography.Measure(label, LabelStyle).X;
            if (cursor > 0f && cursor + labelWidth > width)
            {
                cursor = 0f;
                top += lineHeight + LineGap * scale;
            }

            var min = new Vector2(origin.X + cursor, top);
            var hitMin = new Vector2(min.X - HitPadX * scale, min.Y - HitPadY * scale);
            var hitMax = new Vector2(min.X + labelWidth + HitPadX * scale, min.Y + lineHeight + HitPadY * scale);
            var hovered = UiInteract.Hover(hitMin, hitMax);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                var underlineY = min.Y + lineHeight - UnderlineLift * scale;
                drawList.AddLine(new Vector2(min.X, underlineY), new Vector2(min.X + labelWidth, underlineY),
                    hoverInk.Packed(), Metrics.Stroke.Hairline * scale);
            }

            Typography.Draw(drawList, min, label, hovered ? hoverInk : ink, LabelStyle);
            if (UiInteract.Click(hitMin, hitMax, hovered))
            {
                clicked = index;
            }

            cursor += labelWidth + ItemGap * scale;
        }

        return clicked;
    }
}
