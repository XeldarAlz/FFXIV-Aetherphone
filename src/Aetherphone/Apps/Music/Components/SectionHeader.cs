using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music.Components;

internal static class SectionHeader
{
    private const float BottomGap = 8f;
    private const float FirstGap = 4f;
    private const float ChevronGap = 6f;
    private const float ChevronBox = 14f;
    private const float ChevronScale = 0.7f;

    public static bool Draw(AppSkin ui, string title, bool tappable, float inset = MusicUi.Inset,
        float topGap = MusicUi.SectionGap)
    {
        var scale = UiScale.Current;
        var firstOnPage = ImGui.GetCursorPosY() <= ImGui.GetStyle().WindowPadding.Y + 1f;
        ImGui.Dummy(new Vector2(0f, (firstOnPage ? MathF.Min(topGap, FirstGap) : topGap) * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X + inset * scale;
        var chevronSpace = tappable ? (ChevronGap + ChevronBox) * scale : 0f;
        var available = MathF.Max(1f, width - inset * 2f * scale - chevronSpace);
        var fitted = Typography.FitText(title, available, TextStyles.Title3);
        var size = Typography.Measure(fitted, TextStyles.Title3);
        var drawList = ImGui.GetWindowDrawList();
        var min = new Vector2(left, origin.Y);
        var max = new Vector2(left + size.X + chevronSpace, origin.Y + size.Y);
        var hovered = tappable && UiInteract.Hover(min, max);
        var ink = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) ? ui.MutedInk : ui.Palette.HeadingInk;
        Typography.Draw(drawList, min, fitted, ink, TextStyles.Title3);
        if (tappable)
        {
            var chevronCenter = new Vector2(left + size.X + (ChevronGap + ChevronBox * 0.5f) * scale,
                origin.Y + size.Y * 0.5f);
            AppSkin.Icon(drawList, chevronCenter, IconGlyph.Of(FontAwesomeIcon.ChevronRight), ui.MutedInk,
                ChevronScale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, size.Y + BottomGap * scale));
        return tappable && UiInteract.Click(min, max, hovered);
    }
}
