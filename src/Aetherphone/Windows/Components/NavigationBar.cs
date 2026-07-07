using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal readonly struct NavigationBarPalette
{
    public readonly Vector4 TitleInk;
    public readonly Vector4 Chevron;
    public readonly Vector4 BarFill;
    public readonly Vector4 Hairline;

    public NavigationBarPalette(Vector4 titleInk, Vector4 chevron, Vector4 barFill, Vector4 hairline)
    {
        TitleInk = titleInk;
        Chevron = chevron;
        BarFill = barFill;
        Hairline = hairline;
    }

    public static NavigationBarPalette From(PhoneTheme theme) =>
        new(theme.TextStrong, theme.Accent, theme.AppBackground, theme.Separator);

    public static NavigationBarPalette From(AppSkin ui) =>
        new(ui.TitleInk, ui.Accent, ui.Theme.AppBackground, ui.Theme.Separator);
}

internal static class NavigationBar
{
    public const float InlineHeight = Metrics.Size.Header;
    public const float LargeTitleBand = 46f;
    private const float CollapseDistance = 34f;
    private const float StretchLimit = 0.06f;
    private static readonly TextStyle LargeTitleStyle = new(1.72f, FontWeight.Bold);
    private static readonly TextStyle InlineTitleStyle = new(1.15f, FontWeight.SemiBold);

    public static float Height(float scale, bool largeTitle = true) =>
        (InlineHeight + (largeTitle ? LargeTitleBand : 0f)) * scale;

    public static bool Draw(in PhoneContext context, string title, float scrollY,
        in NavigationBarPalette palette, bool showBack = true, float trailingReserve = 0f)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var drawList = ImGui.GetWindowDrawList();
        var barBottom = content.Min.Y + InlineHeight * scale;
        var progress = Math.Clamp(scrollY / (CollapseDistance * scale), 0f, 1f);
        DrawBarMaterial(drawList, content, barBottom, palette, scale, progress);
        DrawLargeTitle(drawList, content, title, palette, scale, scrollY, progress, trailingReserve);
        DrawInlineTitle(content, title, palette, barBottom, progress, trailingReserve, scale);
        return showBack && DrawBack(context, content, palette, barBottom, scale);
    }

    private static void DrawBarMaterial(ImDrawListPtr drawList, Rect content, float barBottom,
        in NavigationBarPalette palette, float scale, float progress)
    {
        if (progress <= 0.02f)
        {
            return;
        }

        var alpha = Easing.SmoothStep(progress);
        var fill = palette.BarFill with { W = palette.BarFill.W * 0.94f * alpha };
        var min = new Vector2(content.Min.X - 20f * scale, content.Min.Y - 48f * scale);
        drawList.AddRectFilled(min, new Vector2(content.Max.X + 20f * scale, barBottom), ImGui.GetColorU32(fill));
        drawList.AddLine(new Vector2(content.Min.X - 20f * scale, barBottom),
            new Vector2(content.Max.X + 20f * scale, barBottom),
            ImGui.GetColorU32(palette.Hairline with { W = palette.Hairline.W * alpha }), 1f * scale);
    }

    private static void DrawLargeTitle(ImDrawListPtr drawList, Rect content, string title,
        in NavigationBarPalette palette, float scale, float scrollY, float progress, float trailingReserve)
    {
        var alpha = 1f - Easing.SmoothStep(Math.Clamp(progress * 1.25f, 0f, 1f));
        if (alpha <= 0.01f)
        {
            return;
        }

        var stretch = scrollY < 0f ? MathF.Min(StretchLimit, -scrollY / (420f * scale)) : 0f;
        var style = new TextStyle(LargeTitleStyle.Scale * (1f + stretch), LargeTitleStyle.Weight);
        var top = content.Min.Y + InlineHeight * scale + 2f * scale - scrollY;
        drawList.PushClipRect(new Vector2(content.Min.X, content.Min.Y),
            new Vector2(content.Max.X, content.Min.Y + Height(scale)), true);
        var fitted = Typography.FitText(title, content.Width - trailingReserve - 4f * scale, style);
        Typography.Draw(drawList, new Vector2(content.Min.X + 2f * scale, top), fitted,
            palette.TitleInk with { W = palette.TitleInk.W * alpha }, style);
        drawList.PopClipRect();
    }

    private static void DrawInlineTitle(Rect content, string title, in NavigationBarPalette palette, float barBottom,
        float progress, float trailingReserve, float scale)
    {
        var alpha = Easing.SmoothStep(Math.Clamp((progress - 0.45f) / 0.55f, 0f, 1f));
        if (alpha <= 0.01f)
        {
            return;
        }

        var rise = (1f - alpha) * 6f * scale;
        var center = new Vector2(content.Center.X, (content.Min.Y + barBottom) * 0.5f + rise);
        var maxWidth = content.Width - MathF.Max(trailingReserve, 44f * scale) * 2f;
        Typography.DrawCentered(center, Typography.FitText(title, maxWidth, InlineTitleStyle),
            palette.TitleInk with { W = palette.TitleInk.W * alpha }, InlineTitleStyle);
    }

    private static bool DrawBack(in PhoneContext context, Rect content, in NavigationBarPalette palette,
        float barBottom, float scale)
    {
        var hitMin = new Vector2(content.Min.X, content.Min.Y);
        var hitMax = new Vector2(content.Min.X + 44f * scale, barBottom);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        var center = new Vector2(content.Min.X + 13f * scale, (content.Min.Y + barBottom) * 0.5f);
        if (!BackButton.Draw("navbar.back", center, 15f * scale, palette.Chevron, hovered, scale))
        {
            return false;
        }

        context.Navigation.Back();
        return true;
    }

    public static Rect TrailingSlot(Rect content, float scale, float width)
    {
        var barBottom = content.Min.Y + InlineHeight * scale;
        return new Rect(new Vector2(content.Max.X - width, content.Min.Y),
            new Vector2(content.Max.X, barBottom));
    }
}
