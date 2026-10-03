using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AppHeader
{
    public const float Height = Metrics.Size.Header;

    private const float GlassReserveThreshold = 0.01f;

    private static bool BarHover(Vector2 min, Vector2 max) =>
        !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(min, max);
    private const float ButtonGlyphScale = 0.62f;
    private const float BackPadX = 6f;
    private const float BackHitWidth = 44f;
    private const float BackLabelGap = 4f;
    private const float BackLabelMaxFraction = 0.4f;
    private const float BackPressedAlpha = 0.55f;
    private const float ChevronWidthFactor = 0.55f;
    private const float ChevronThickness = 2.2f;
    private const float EdgeFillAlpha = 0.94f;
    private const float InlineTitleMargin = 8f;

    public static void Draw(in PhoneContext context, string title, Action? onBack = null)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        var rowCenterY = content.Min.Y + Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, rowCenterY), title, context.Theme.TextStrong, 1.15f,
            FontWeight.SemiBold);
        DrawBack(context, onBack, scale, rowCenterY);
    }

    public static void Draw(in PhoneContext context, string id, string title, float rightReserve,
        Action? onBack = null)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        DrawTitleWithReserve(content, id, title, rightReserve, context.Theme.TextStrong, scale);
        DrawBack(context, onBack, scale, content.Min.Y + Height * scale * 0.5f);
    }

    public static bool DrawBack(Rect content, float scale, string id, Vector4 color)
    {
        var rowCenterY = content.Min.Y + Height * scale * 0.5f;
        var hitMax = new Vector2(content.Min.X + 44f * scale, content.Min.Y + Height * scale);
        var hovered = UiInteract.Hover(content.Min, hitMax);
        var center = new Vector2(content.Min.X + 15f * scale, rowCenterY);
        return BackButton.Draw(id, center, 15f * scale, color, hovered, scale);
    }

    public static Rect BackRect(Rect content, float scale) =>
        new(content.Min, new Vector2(content.Min.X + BackHitWidth * scale, content.Min.Y + Height * scale));

    private static void DrawBack(in PhoneContext context, Action? onBack, float scale, float rowCenterY)
    {
        var content = context.Content;
        var hit = BackRect(content, scale);
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        var center = new Vector2(content.Min.X + 13f * scale, rowCenterY);
        var clicked = BackButton.Draw("appheader.back", center, 15f * scale, context.Theme.Accent, hovered, scale);
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

    public static void DrawNavBar(Rect area, string id, string title, Vector4 ink, Action? onBack,
        float rightReserve = 0f)
    {
        var scale = UiScale.Current;
        var rowCenterY = area.Min.Y + Height * scale * 0.5f;
        if (rightReserve > 0f)
        {
            DrawTitleWithReserve(area, id + ".title", title, rightReserve, ink, scale, TextStyles.Title3);
        }
        else
        {
            var fitted = Typography.FitText(title, area.Width - 96f * scale, TextStyles.Title3);
            Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), fitted, ink, TextStyles.Title3);
        }

        if (onBack is null)
        {
            return;
        }

        var hitMax = new Vector2(area.Min.X + 46f * scale, area.Min.Y + Height * scale);
        var hovered = UiInteract.Hover(area.Min, hitMax);
        var center = new Vector2(area.Min.X + 17f * scale, rowCenterY);
        if (BackButton.Draw(id, center, 15f * scale, ink, hovered, scale))
        {
            onBack();
        }
    }

    public static void DrawTitleWithReserve(Rect area, string id, string title, float rightReserve, Vector4 color,
        float scale, TextStyle? style = null, float leftReserve = 44f)
    {
        var titleStyle = style ?? new TextStyle(1.15f, FontWeight.SemiBold);
        var rowCenterY = area.Min.Y + Height * scale * 0.5f;
        var leftLimit = area.Min.X + leftReserve * scale;
        var rightLimit = area.Max.X - rightReserve;
        var maxWidth = MathF.Max(1f, rightLimit - leftLimit);
        var titleSize = Typography.Measure(title, titleStyle);
        var clampedWidth = MathF.Min(titleSize.X, maxWidth);
        var titleX = leftLimit + (maxWidth - clampedWidth) * 0.5f;
        var titleY = rowCenterY - titleSize.Y * 0.5f;
        Marquee.DrawLeftAuto(id, title, titleX, titleY, maxWidth, titleStyle, color);
    }

    public static NavBarFrame BeginLargeTitle(in PhoneContext context, bool reserveInlineRow = true)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        var titleBandTop = reserveInlineRow ? NavBarMetrics.InlineHeight * scale : 0f;
        var inset = titleBandTop + (NavBarMetrics.BandHeight + NavBarMetrics.TitleGap) * scale;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + inset), content.Max);
        AppSurface.ArmNavBar(body.Min.Y, inset);
        return new NavBarFrame(content, body, scale, titleBandTop);
    }

    public static Rect LargeTitleButtonRect(in NavBarFrame frame, int index, int count)
    {
        var scale = frame.Scale;
        var radius = Metrics.Size.GlassButton * scale * 0.5f;
        var center = new Vector2(NavBarMetrics.ButtonCenterX(frame.Content.Max.X, index, count, scale),
            frame.Content.Min.Y + NavBarMetrics.InlineHeight * scale * 0.5f);
        return new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
    }

    public static int EndLargeTitle(in NavBarFrame frame, in PhoneContext context, string id, string title,
        in NavBarStyle style, ReadOnlySpan<NavBarButton> buttons, string backTitle = "", Action? onBack = null)
    {
        var scrollY = AppSurface.NavBarConsumed ? AppSurface.NavBarScrollY : 0f;
        AppSurface.DisarmNavBar();
        var scale = frame.Scale;
        var content = frame.Content;
        var theme = context.Theme;
        var progress = NavBarMetrics.Progress(scrollY, scale);
        var glass = NavBarMetrics.GlassOpacity(scrollY, scale);
        var inlineHeight = NavBarMetrics.InlineHeight * scale;
        var chromeBottom = content.Min.Y + inlineHeight + NavBarMetrics.EdgeFadeHeight * scale;
        var band = new Rect(content.Min, new Vector2(content.Max.X, MathF.Max(frame.Body.Min.Y, chromeBottom)));
        var buttonCount = Math.Min(buttons.Length, NavBarMetrics.MaxButtons);
        var pressedButton = -1;
        var backPressed = false;
        using (ScreenLayer.BeginPassive(id, band))
        {
            var drawList = ImGui.GetWindowDrawList();
            DrawScrollEdge(drawList, content, inlineHeight, style.Background, glass, scale);
            var barMin = new Vector2(content.Min.X + Metrics.Space.GlassInset * scale,
                content.Min.Y + NavBarMetrics.BarInsetY * scale);
            var barMax = new Vector2(content.Max.X - Metrics.Space.GlassInset * scale,
                content.Min.Y + inlineHeight - NavBarMetrics.BarInsetY * scale);
            Material.ThemedGlass(drawList, barMin, barMax, (barMax.Y - barMin.Y) * 0.5f, scale, theme, glass);
            if (glass > GlassReserveThreshold)
            {
                UiInteract.HoverOverlay(new Rect(new Vector2(content.Min.X, content.Min.Y),
                    new Vector2(content.Max.X, content.Min.Y + inlineHeight)));
            }

            var titleReserve = frame.TitleBandTop > 0f
                ? 0f
                : NavBarMetrics.ButtonsWidth(buttonCount, scale) + Metrics.Space.GlassInset * scale;
            DrawLargeTitle(drawList, content, frame.TitleBandTop,
                frame.Body.Min.Y - NavBarMetrics.TitleGap * scale, titleReserve, title, style.Ink, scrollY, progress,
                scale);
            var leftReserve = onBack is null
                ? 0f
                : DrawBackControl(drawList, content, backTitle, style.Accent, inlineHeight, scale, out backPressed);
            var rightReserve = NavBarMetrics.ButtonsWidth(buttonCount, scale);
            DrawInlineTitle(drawList, content, title, style.Ink, progress, MathF.Max(leftReserve, rightReserve),
                inlineHeight, scale);
            pressedButton = DrawButtons(drawList, id, content, buttons, buttonCount, style.Ink, theme, inlineHeight,
                scale);
        }

        if (backPressed && onBack is not null)
        {
            onBack();
        }

        return pressedButton;
    }

    private static void DrawScrollEdge(ImDrawListPtr drawList, Rect content, float inlineHeight, Vector4 background,
        float strength, float scale)
    {
        if (strength <= 0f)
        {
            return;
        }

        var solid = ImGui.GetColorU32(background with { W = EdgeFillAlpha * strength });
        var clear = ImGui.GetColorU32(background with { W = 0f });
        var barBottom = content.Min.Y + inlineHeight;
        drawList.AddRectFilled(content.Min, new Vector2(content.Max.X, barBottom), solid);
        drawList.AddRectFilledMultiColor(new Vector2(content.Min.X, barBottom),
            new Vector2(content.Max.X, barBottom + NavBarMetrics.EdgeFadeHeight * scale), solid, solid, clear,
            clear);
    }

    private static void DrawLargeTitle(ImDrawListPtr drawList, Rect content, float titleBandTop, float bandBottom,
        float rightReserve, string title, Vector4 ink, float scrollY, float progress, float scale)
    {
        var alpha = NavBarMetrics.LargeTitleAlpha(progress);
        if (alpha <= 0.001f)
        {
            return;
        }

        var bandTop = content.Min.Y + titleBandTop;
        var maxWidth = MathF.Max(1f, content.Width - rightReserve);
        var fitted = Typography.FitText(title, maxWidth, TextStyles.LargeTitle);
        var size = Typography.Measure(fitted, TextStyles.LargeTitle);
        var position = new Vector2(content.Min.X,
            (bandTop + bandBottom - size.Y) * 0.5f - scrollY);
        drawList.PushClipRect(new Vector2(content.Min.X, bandTop), new Vector2(content.Max.X, bandBottom), true);
        Typography.Draw(drawList, position, fitted, ink with { W = ink.W * alpha }, TextStyles.LargeTitle);
        drawList.PopClipRect();
    }

    private static void DrawInlineTitle(ImDrawListPtr drawList, Rect content, string title, Vector4 ink,
        float progress, float sideReserve, float inlineHeight, float scale)
    {
        var alpha = NavBarMetrics.InlineTitleAlpha(progress);
        if (alpha <= 0.001f)
        {
            return;
        }

        var reserve = sideReserve + (Metrics.Space.GlassInset + InlineTitleMargin) * scale;
        var maxWidth = MathF.Max(1f, content.Width - reserve * 2f);
        var fitted = Typography.FitText(title, maxWidth, TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, content.Min.Y + inlineHeight * 0.5f), fitted,
            ink with { W = ink.W * alpha }, TextStyles.Headline);
    }

    private static float DrawBackControl(ImDrawListPtr drawList, Rect content, string backTitle, Vector4 accent,
        float inlineHeight, float scale, out bool pressed)
    {
        var left = content.Min.X + Metrics.Space.GlassInset * scale;
        var chevron = NavBarMetrics.ChevronSize * scale;
        var padX = BackPadX * scale;
        var centerY = content.Min.Y + inlineHeight * 0.5f;
        var label = backTitle.Length > 0
            ? Typography.FitText(backTitle, content.Width * BackLabelMaxFraction, TextStyles.Body)
            : string.Empty;
        var labelSize = label.Length > 0 ? Typography.Measure(label, TextStyles.Body) : Vector2.Zero;
        var labelWidth = label.Length > 0 ? BackLabelGap * scale + labelSize.X : 0f;
        var width = padX * 2f + chevron * ChevronWidthFactor + labelWidth;
        var halfHit = NavBarMetrics.BackHitHeight * scale * 0.5f;
        var hitMin = new Vector2(left, centerY - halfHit);
        var hitMax = new Vector2(left + width, centerY + halfHit);
        var hovered = BarHover(hitMin, hitMax);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var ink = accent with { W = accent.W * (down ? BackPressedAlpha : 1f) };
        var color = ImGui.GetColorU32(ink);
        var tip = new Vector2(left + padX, centerY);
        var armX = tip.X + chevron * ChevronWidthFactor;
        var armY = chevron * 0.5f;
        var thickness = ChevronThickness * scale;
        drawList.AddLine(new Vector2(armX, centerY - armY), tip, color, thickness);
        drawList.AddLine(tip, new Vector2(armX, centerY + armY), color, thickness);
        if (label.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(armX + BackLabelGap * scale, centerY - labelSize.Y * 0.5f), label,
                ink, TextStyles.Body);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        pressed = UiInteract.Click(hitMin, hitMax, hovered);
        return hitMax.X - content.Min.X;
    }

    private static int DrawButtons(ImDrawListPtr drawList, string id, Rect content, ReadOnlySpan<NavBarButton> buttons,
        int count, Vector4 ink, PhoneTheme theme, float inlineHeight, float scale)
    {
        var pressed = -1;
        var baseKey = ImGui.GetID(id);
        var radius = Metrics.Size.GlassButton * scale * 0.5f;
        var centerY = content.Min.Y + inlineHeight * 0.5f;
        var hit = new Vector2(radius, radius);
        for (var index = 0; index < count; index++)
        {
            var center = new Vector2(NavBarMetrics.ButtonCenterX(content.Max.X, index, count, scale), centerY);
            var hovered = BarHover(center - hit, center + hit);
            var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var grow = PressFx.Scale(unchecked(baseKey + (uint)index + 1u), down, PressFx.ControlPressedScale);
            var drawn = new Vector2(radius * grow, radius * grow);
            Material.ThemedGlass(drawList, center - drawn, center + drawn, radius * grow, scale, theme);
            AppSkin.Icon(drawList, center, buttons[index].Glyph, ink, ButtonGlyphScale * grow);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            HoverTooltip.Show(new Rect(center - hit, center + hit), buttons[index].Tooltip);
            if (UiInteract.Click(center - hit, center + hit, hovered))
            {
                pressed = index;
            }
        }

        return pressed;
    }
}
