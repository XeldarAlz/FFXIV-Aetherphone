using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class TableButton
{
    public const float CornerRadius = 14f;
    public const float LabelPad = 7f;

    private const float SubLabelAlpha = 0.78f;

    public static float LabelRoom(float width, float scale) => MathF.Max(1f, width - LabelPad * 2f * scale);

    public static bool Draw(ImDrawListPtr drawList, Rect rect, string label, string subLabel, in ControlInk ink,
        ButtonStyle style, bool enabled, string id)
    {
        var scale = UiScale.Current;
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ink, style, ButtonRole.Normal, enabled, hovered, ImGui.GetID(id), 1f,
            CornerRadius * scale);
        var area = face.Face;
        var room = LabelRoom(area.Width, scale);
        var labelStyle = subLabel.Length > 0 ? TextStyles.SubheadlineEmphasized : Button.LabelStyle(area.Height);
        var labelScale = Typography.FitScale(label, room, labelStyle.Scale,
            MathF.Min(labelStyle.Scale, TextStyles.FootnoteEmphasized.Scale), labelStyle.Weight);
        var shown = Typography.FitText(label, room, labelScale, labelStyle.Weight);
        var labelSize = Typography.Measure(shown, labelScale, labelStyle.Weight);
        if (subLabel.Length == 0)
        {
            Typography.Draw(drawList, area.Center - labelSize * 0.5f, shown, face.LabelInk, labelScale,
                labelStyle.Weight);
            return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
        }

        var subStyle = TextStyles.Footnote;
        var sub = Typography.FitText(subLabel, room, subStyle);
        var subSize = Typography.Measure(sub, subStyle);
        var top = area.Center.Y - (labelSize.Y + subSize.Y) * 0.5f;
        Typography.Draw(drawList, new Vector2(area.Center.X - labelSize.X * 0.5f, top), shown, face.LabelInk,
            labelScale, labelStyle.Weight);
        Typography.Draw(drawList, new Vector2(area.Center.X - subSize.X * 0.5f, top + labelSize.Y), sub,
            Palette.WithAlpha(face.LabelInk, face.LabelInk.W * SubLabelAlpha), subStyle);
        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}
