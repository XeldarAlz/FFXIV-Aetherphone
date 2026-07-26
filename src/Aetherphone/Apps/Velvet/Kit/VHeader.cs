using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VHeader
{
    public const float Height = 42f;

    public static bool Root(Rect area, string title, PhoneTheme theme, int bellBadge)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var midY = area.Min.Y + Height * scale * 0.5f;
        var fittedTitle = Typography.FitText(title, area.Width - 44f * scale, TextStyles.Title3);
        var titleSize = Typography.Measure(fittedTitle, TextStyles.Title3);
        Typography.Draw(new Vector2(area.Min.X + 4f * scale, midY - titleSize.Y * 0.5f), fittedTitle,
            VelvetTheme.TitleInk, TextStyles.Title3);
        var bellCenter = new Vector2(area.Max.X - 20f * scale, midY);
        var clicked = AppSkin.IconButton(bellCenter, 16f * scale, FontAwesomeIcon.Bell.ToIconString(),
            VelvetTheme.TitleInk, AppSkin.Transparent, 0.9f, theme, Loc.T(L.Velvet.Activity), HoverLabelSide.Below);
        if (bellBadge > 0)
        {
            VBadge.Count(drawList, new Vector2(bellCenter.X + 16f * scale, bellCenter.Y - 9f * scale), bellBadge);
        }

        return clicked;
    }

    public static bool Push(Rect area, string title, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var midY = area.Min.Y + Height * scale * 0.5f;
        var center = new Vector2(area.Min.X + 16f * scale, midY);
        var hitMin = new Vector2(area.Min.X, area.Min.Y);
        var hitMax = new Vector2(area.Min.X + 46f * scale, area.Min.Y + Height * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        var back = BackButton.Draw("velvet.back", center, 15f * scale, VelvetTheme.TitleInk, hovered, scale,
            shadow: true);
        var fittedTitle = Typography.FitText(title, area.Width - 92f * scale, TextStyles.Title3);
        var titleSize = Typography.Measure(fittedTitle, TextStyles.Title3);
        Typography.Draw(new Vector2(area.Center.X - titleSize.X * 0.5f, midY - titleSize.Y * 0.5f), fittedTitle,
            VelvetTheme.TitleInk, TextStyles.Title3);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return back;
    }
}
