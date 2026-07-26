using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class AppHeader
{
    public const float Height = Metrics.Size.Header;

    public static void Draw(in PhoneContext context, string title, Action? onBack = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var theme = context.Theme;
        var rowCenterY = content.Min.Y + Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, rowCenterY), title, theme.TextStrong, 1.15f,
            FontWeight.SemiBold);
        var hitMin = new Vector2(content.Min.X, content.Min.Y);
        var hitMax = new Vector2(content.Min.X + 44f * scale, content.Min.Y + Height * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        var center = new Vector2(content.Min.X + 13f * scale, rowCenterY);
        var clicked = BackButton.Draw("appheader.back", center, 15f * scale, theme.Accent, hovered, scale);
        if (!clicked)
        {
            return;
        }

        if (onBack is not null)
        {
            onBack();
        }
        else
        {
            context.Navigation.Back();
        }
    }

    public static void DrawBackOnly(in PhoneContext context, Action? onBack = null, float topInset = 20f)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var theme = context.Theme;
        var center = new Vector2(content.Min.X + 20f * scale, content.Min.Y + topInset * scale);
        var radius = 17f * scale;
        var hitMin = center - new Vector2(20f * scale);
        var hitMax = center + new Vector2(20f * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        ImGui.GetWindowDrawList().AddCircleFilled(center, radius,
            ImGui.GetColorU32(theme.GroupedCard with { W = hovered ? 0.92f : 0.74f }), 28);
        var clicked = BackButton.Draw("appheader.compactBack", center, 15f * scale, theme.TextStrong, hovered,
            scale, true);
        if (!clicked)
        {
            return;
        }

        if (onBack is not null)
        {
            onBack();
        }
        else
        {
            context.Navigation.Back();
        }
    }

    public static void DrawTitleWithReserve(Rect area, string id, string title, float rightReserve, Vector4 color,
        float scale)
    {
        var rowCenterY = area.Min.Y + Height * scale * 0.5f;
        var leftLimit = area.Min.X + 44f * scale;
        var rightLimit = area.Max.X - rightReserve;
        var maxWidth = MathF.Max(1f, rightLimit - leftLimit);
        var titleSize = Typography.Measure(title, 1.15f, FontWeight.SemiBold);
        var clampedWidth = MathF.Min(titleSize.X, maxWidth);
        var titleX = leftLimit + (maxWidth - clampedWidth) * 0.5f;
        var titleY = rowCenterY - titleSize.Y * 0.5f;
        Marquee.DrawLeftAuto(id, title, titleX, titleY, maxWidth, new TextStyle(1.15f, FontWeight.SemiBold), color);
    }
}
