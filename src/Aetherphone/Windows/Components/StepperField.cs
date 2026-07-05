using System.Numerics;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class StepperField
{
    public static void Draw(AppSkin ui, Rect rect, string valueText, float scale, Action onDecrement,
        Action onIncrement)
    {
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale);
        var chevronWidth = 34f * scale;
        var leftRect = new Rect(rect.Min, new Vector2(rect.Min.X + chevronWidth, rect.Max.Y));
        var rightRect = new Rect(new Vector2(rect.Max.X - chevronWidth, rect.Min.Y), rect.Max);
        if (DrawChevron(ui, drawList, leftRect, "<", scale))
        {
            onDecrement();
        }

        if (DrawChevron(ui, drawList, rightRect, ">", scale))
        {
            onIncrement();
        }

        Typography.DrawCentered(drawList, rect.Center, valueText, ui.TitleInk, TextStyles.Headline.Scale,
            TextStyles.Headline.Weight);
    }

    private static bool DrawChevron(AppSkin ui, ImDrawListPtr drawList, Rect rect, string chevron, float scale)
    {
        if (ImGui.IsMouseHoveringRect(rect.Min, rect.Max))
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.HoverTint));
        }

        Typography.DrawCentered(drawList, rect.Center, chevron, ui.MutedInk, TextStyles.Headline.Scale,
            TextStyles.Headline.Weight);
        return UiInteract.HoverClick(rect.Min, rect.Max);
    }
}
