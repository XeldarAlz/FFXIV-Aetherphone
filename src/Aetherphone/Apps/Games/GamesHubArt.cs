using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal static class GamesHubArt
{
    public const float SectionHeight = CardSectionHeader.HeightUnits;
    private const float SeeAllChevron = 11f;
    private const float SeeAllGap = 4f;

    public static float ButtonWidth(string label, float height) =>
        Typography.Measure(label, Button.LabelStyle(height)).X + height;

    public static bool Section(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, string title,
        string action, string id)
    {
        var scale = UiScale.Current;
        var height = CardSectionHeader.HeightUnits * scale;
        var centerY = top + height * 0.5f;
        var actionWidth = 0f;
        var clicked = false;
        if (action.Length > 0)
        {
            var labelSize = Typography.Measure(action, TextStyles.Body);
            var chevron = SeeAllChevron * scale;
            actionWidth = labelSize.X + SeeAllGap * scale + chevron;
            var min = new Vector2(left + width - actionWidth, top);
            var max = new Vector2(left + width, top + height);
            var hovered = UiInteract.Hover(min, max);
            var ink = hovered ? Palette.Mix(ui.Accent, ui.TitleInk, 0.25f) : ui.Accent;
            Typography.Draw(drawList, new Vector2(min.X, centerY - labelSize.Y * 0.5f), action, ink, TextStyles.Body);
            PhoneIcon.Draw(drawList, new Vector2(max.X - chevron * 0.5f, centerY), PhoneIcons.ChevronRight, ink,
                chevron);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            clicked = UiInteract.Click(min, max, hovered);
            ReportAnchor(id, new Rect(min, max));
        }

        var reserve = actionWidth > 0f ? actionWidth + Metrics.Space.Md * scale : 0f;
        CardSectionHeader.Draw(drawList, new Vector2(left, top), width, title, ui.TitleInk, reserve);
        return clicked;
    }

    public static void ReportAnchor(string key, Rect rect)
    {
        if (!Core.Onboarding.UiAnchors.Recording)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var top = MathF.Max(rect.Min.Y, drawList.GetClipRectMin().Y);
        var bottom = MathF.Min(rect.Max.Y, drawList.GetClipRectMax().Y);
        if (bottom <= top)
        {
            return;
        }

        Core.Onboarding.UiAnchors.Report(key, new Rect(new Vector2(rect.Min.X, top), new Vector2(rect.Max.X, bottom)));
    }
}
