using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed partial class WidgetGallery
{
    private const float HeaderHeightUnits = 44f;
    private const float BackGlyphUnits = 18f;
    private const float BackGapUnits = 2f;
    private const float BackPadUnits = 6f;
    private const float BackHoverAlpha = 0.75f;
    private const float PageTextTopUnits = 6f;
    private const float NameGapUnits = 6f;
    private const float TextWidthFraction = 0.86f;
    private const float PreviewGapUnits = 22f;
    private const float DotsHeightUnits = 20f;
    private const float DotsGapUnits = 16f;
    private const float DotRadiusUnits = 3.5f;
    private const float DotSpacingUnits = 14f;
    private const float DotActiveAlpha = 0.9f;
    private const float DotIdleAlpha = 0.28f;
    private const float ButtonHeightUnits = 50f;
    private const float ButtonMaxWidthUnits = 300f;
    private const float ButtonBottomUnits = 30f;
    private const float ButtonGlyphUnits = 16f;
    private const float ButtonGlyphGapUnits = 8f;
    private const float ButtonTextPadUnits = 28f;
    private const float ButtonHoverLighten = 0.06f;
    private const float FlingVelocityUnits = 500f;
    private const float EdgeResistance = 0.35f;
    private const float RestPosition = 0.001f;

    private readonly GalleryDrag pagerDrag = new();
    private readonly List<GalleryPage> pages = new();
    private WidgetGalleryApp? detailApp;
    private Spring pagerSpring;
    private Spring buttonPress;
    private int pagerPage;
    private int placedPage = -1;
    private Rect previewRect;
    private float previewFactor = 1f;

    private void PushApp(WidgetGalleryApp entry, IHomeWidget? focus, WidgetSize focusSize)
    {
        pages.Clear();
        detailApp = entry;
        var start = 0;
        var widgets = entry.Widgets;
        for (var widgetIndex = 0; widgetIndex < widgets.Count; widgetIndex++)
        {
            var widget = widgets[widgetIndex];
            for (var size = WidgetSize.Small; size <= WidgetSize.Large; size++)
            {
                if (!WidgetSizes.Contains(widget.Sizes, size))
                {
                    continue;
                }

                if (ReferenceEquals(widget, focus) && size == focusSize)
                {
                    start = pages.Count;
                }

                pages.Add(new GalleryPage(widget, size));
            }
        }

        ShowDetail(start);
    }

    private void PushStack(WidgetSize focusSize)
    {
        pages.Clear();
        detailApp = null;
        var start = 0;
        for (var size = WidgetSize.Small; size <= WidgetSize.Medium; size++)
        {
            if (index.Suggestions(size).Count == 0)
            {
                continue;
            }

            if (size == focusSize)
            {
                start = pages.Count;
            }

            pages.Add(new GalleryPage(null, size));
        }

        ShowDetail(start);
    }

    private void ShowDetail(int start)
    {
        if (pages.Count == 0)
        {
            return;
        }

        pagerPage = Math.Clamp(start, 0, pages.Count - 1);
        pagerSpring.SnapTo(pagerPage);
        pagerDrag.Reset();
        buttonPress.SnapTo(1f);
        placedPage = -1;
        detailOpen = true;
    }

    private void Pop()
    {
        detailOpen = false;
        pagerDrag.Reset();
    }

    private void DrawDetail(in SheetFrame frame, PhoneTheme theme, in HomeMetrics metrics, float delta,
        float offsetX, bool interactive)
    {
        if (detailOpen && detailApp is { } shown && index.Find(shown.App.Id) is null)
        {
            Pop();
        }

        if (pages.Count == 0)
        {
            return;
        }

        var drawList = frame.DrawList;
        var scale = metrics.Scale;
        var panel = frame.Panel;
        var left = panel.Min.X + offsetX;
        var width = panel.Width;
        var right = left + width;
        var ink = frame.Ink;
        drawList.PushClipRect(new Vector2(MathF.Max(panel.Min.X, left), panel.Min.Y), panel.Max, true);
        var headerBottom = DrawDetailHeader(drawList, frame.Content.Min.Y, left, right, theme, ink, scale,
            interactive);
        var side = SidePaddingUnits * scale;
        var buttonWidth = MathF.Min(width - side * 2f, ButtonMaxWidthUnits * scale);
        var buttonBottom = panel.Max.Y - ButtonBottomUnits * scale;
        var button = new Rect(new Vector2(left + (width - buttonWidth) * 0.5f, buttonBottom - ButtonHeightUnits * scale),
            new Vector2(left + (width + buttonWidth) * 0.5f, buttonBottom));
        var dotsCenterY = button.Min.Y - (DotsGapUnits + DotsHeightUnits * 0.5f) * scale;
        var pagesTop = headerBottom + PageTextTopUnits * scale;
        var textWidth = width * TextWidthFraction;
        var previewTop = pagesTop + TextBlockHeight(textWidth, scale) + PreviewGapUnits * scale;
        var previewBottom = dotsCenterY - (DotsHeightUnits * 0.5f + PreviewGapUnits) * scale;
        var pagerArea = new Rect(new Vector2(left, pagesTop),
            new Vector2(right, dotsCenterY - DotsHeightUnits * 0.5f * scale));
        StepPager(pagerArea, width, scale, delta, interactive);
        var value = pagerSpring.Value;
        var first = Math.Max(0, (int)MathF.Floor(value));
        var last = Math.Min(pages.Count - 1, first + 1);
        for (var pageIndex = first; pageIndex <= last; pageIndex++)
        {
            var pageLeft = left + (pageIndex - value) * width;
            if (pageLeft >= right || pageLeft + width <= left)
            {
                continue;
            }

            DrawPage(drawList, pageIndex, pageLeft, width, pagesTop, previewTop, previewBottom, textWidth, theme, ink,
                in metrics, delta);
        }

        DrawDots(drawList, new Vector2(left + width * 0.5f, dotsCenterY), ink, scale, interactive);
        if (DrawAddButton(drawList, button, theme, scale, delta, interactive))
        {
            Place(pages[pagerPage]);
        }

        drawList.PopClipRect();
    }

    private float DrawDetailHeader(ImDrawListPtr drawList, float top, float left, float right, PhoneTheme theme,
        Vector4 ink, float scale, bool interactive)
    {
        var height = HeaderHeightUnits * scale;
        var centerY = top + height * 0.5f;
        var side = SidePaddingUnits * scale;
        var backLabel = Loc.T(L.Home.Widgets);
        var glyph = BackGlyphUnits * scale;
        var labelSize = Typography.Measure(backLabel, TextStyles.Body);
        var backLeft = left + side - BackPadUnits * scale;
        var backRight = left + side + glyph + BackGapUnits * scale + labelSize.X + BackPadUnits * scale;
        var back = new Rect(new Vector2(backLeft, top), new Vector2(backRight, top + height));
        var hovered = interactive && UiInteract.Hover(back.Min, back.Max);
        var accent = Palette.WithAlpha(theme.Accent, hovered ? BackHoverAlpha : 1f);
        PhoneIcon.Draw(drawList, new Vector2(left + side + glyph * 0.5f, centerY), PhoneIcons.ChevronLeft, accent,
            glyph);
        Typography.Draw(drawList,
            new Vector2(left + side + glyph + BackGapUnits * scale, centerY - labelSize.Y * 0.5f), backLabel, accent,
            TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(back.Min, back.Max, hovered))
        {
            Pop();
        }

        var title = detailApp is { } entry ? entry.App.DisplayName : Loc.T(L.WidgetGallery.SmartStack);
        var reserved = backRight - left;
        var fitted = Typography.FitText(title, right - left - reserved * 2f, TextStyles.Headline);
        var titleSize = Typography.Measure(fitted, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2((left + right - titleSize.X) * 0.5f, centerY - titleSize.Y * 0.5f),
            fitted, ink, TextStyles.Headline);
        return top + height;
    }

    private float TextBlockHeight(float textWidth, float scale)
    {
        var tallest = 0f;
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var nameHeight = Typography.Measure(PageName(pages[pageIndex]), TextStyles.Title3).Y;
            var descriptionHeight = Typography
                .MeasureWrappedBlock(PageDescription(pages[pageIndex]), TextStyles.Subheadline, textWidth).Y;
            tallest = MathF.Max(tallest, nameHeight + NameGapUnits * scale + descriptionHeight);
        }

        return tallest;
    }

    private static string PageName(in GalleryPage page) =>
        page.Widget is { } widget ? widget.DisplayName : Loc.T(L.WidgetGallery.SmartStack);

    private static string PageDescription(in GalleryPage page) =>
        page.Widget is { } widget ? widget.Description : Loc.T(L.WidgetGallery.SmartStackDescription);

    private void StepPager(Rect area, float width, float scale, float delta, bool interactive)
    {
        var lastPage = pages.Count - 1;
        pagerPage = Math.Clamp(pagerPage, 0, lastPage);
        var phase = pagerDrag.Track(area, interactive && pages.Count > 1, scale, delta, out var deltaX);
        if (phase == GalleryDragPhase.Dragging)
        {
            sheet.YieldPointer();
            var current = pagerSpring.Value;
            var outside = current < 0f || current > lastPage;
            pagerSpring.SnapTo(current - deltaX / width * (outside ? EdgeResistance : 1f));
            return;
        }

        if (phase == GalleryDragPhase.Released)
        {
            var velocity = pagerDrag.Velocity;
            var current = pagerSpring.Value;
            var fling = FlingVelocityUnits * scale;
            var target = (int)MathF.Round(current);
            if (velocity <= -fling)
            {
                target = (int)MathF.Floor(current) + 1;
            }
            else if (velocity >= fling)
            {
                target = (int)MathF.Ceiling(current) - 1;
            }

            pagerPage = Math.Clamp(target, 0, lastPage);
            pagerSpring.Velocity = -velocity / width;
        }

        pagerSpring.Step(pagerPage, Motion.PageSettle, delta);
        if (pagerSpring.IsResting(pagerPage, RestPosition, RestPosition))
        {
            pagerSpring.SnapTo(pagerPage);
        }
    }

    private void DrawPage(ImDrawListPtr drawList, int pageIndex, float pageLeft, float width, float top,
        float previewTop, float previewBottom, float textWidth, PhoneTheme theme, Vector4 ink, in HomeMetrics metrics,
        float delta)
    {
        var page = pages[pageIndex];
        var scale = metrics.Scale;
        var centerX = pageLeft + width * 0.5f;
        var name = Typography.FitText(PageName(page), textWidth, TextStyles.Title3);
        var nameSize = Typography.Measure(name, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(centerX - nameSize.X * 0.5f, top), name, ink, TextStyles.Title3);
        Typography.DrawWrappedCentered(drawList, PageDescription(page), TextStyles.Subheadline, Secondary(ink),
            new Vector2(centerX, top + nameSize.Y + NameGapUnits * scale), textWidth);
        var footprint = WidgetGalleryPreview.Footprint(page.Size, in metrics);
        var room = new Vector2(width - SidePaddingUnits * scale * 2f, MathF.Max(1f, previewBottom - previewTop));
        var factor = WidgetGalleryPreview.Fit(footprint, room);
        var rect = WidgetGalleryPreview.Centered(new Vector2(centerX, (previewTop + previewBottom) * 0.5f),
            footprint * factor);
        if (pageIndex == pagerPage)
        {
            previewRect = rect;
            previewFactor = factor;
        }

        if (pageIndex == placedPage && !sheet.IsOpen)
        {
            return;
        }

        if (page.Widget is { } widget)
        {
            WidgetGalleryPreview.Draw(drawList, widgetHost, widget, page.Size, rect, theme, scale * factor, delta);
            return;
        }

        WidgetGalleryPreview.DrawStack(drawList, widgetHost, index.Suggestions(page.Size), page.Size, rect, theme,
            ink, scale * factor, delta);
    }

    private void DrawDots(ImDrawListPtr drawList, Vector2 center, Vector4 ink, float scale, bool interactive)
    {
        var count = pages.Count;
        if (count <= 1)
        {
            return;
        }

        var spacing = DotSpacingUnits * scale;
        var radius = DotRadiusUnits * scale;
        var active = Math.Clamp((int)MathF.Round(pagerSpring.Value), 0, count - 1);
        var startX = center.X - (count - 1) * spacing * 0.5f;
        var halfHit = new Vector2(spacing * 0.5f, DotsHeightUnits * scale * 0.5f);
        for (var dotIndex = 0; dotIndex < count; dotIndex++)
        {
            var dot = new Vector2(startX + dotIndex * spacing, center.Y);
            var hovered = interactive && !pagerDrag.Claimed && UiInteract.Hover(dot - halfHit, dot + halfHit);
            var alpha = dotIndex == active ? DotActiveAlpha : hovered ? (DotActiveAlpha + DotIdleAlpha) * 0.5f
                : DotIdleAlpha;
            drawList.AddCircleFilled(dot, radius, ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * alpha)), 16);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(dot - halfHit, dot + halfHit, hovered))
            {
                pagerPage = dotIndex;
            }
        }
    }

    private bool DrawAddButton(ImDrawListPtr drawList, Rect button, PhoneTheme theme, float scale, float delta,
        bool interactive)
    {
        var hovered = interactive && !pagerDrag.Claimed && UiInteract.Hover(button.Min, button.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        buttonPress.Step(pressed ? Motion.PressScaleControl : 1f, pressed ? Motion.PressIn : Motion.Release, delta);
        var drawn = WidgetGalleryPreview.Centered(button.Center, button.Size * buttonPress.Value);
        var fill = hovered ? Palette.Lighten(theme.Accent, ButtonHoverLighten) : theme.Accent;
        Material.AccentGlass(drawList, drawn.Min, drawn.Max, drawn.Height * 0.5f, scale, fill);
        var glyph = ButtonGlyphUnits * scale;
        var glyphGap = ButtonGlyphGapUnits * scale;
        var label = Typography.FitText(Loc.T(L.Home.AddWidget),
            drawn.Width - glyph - glyphGap - ButtonTextPadUnits * scale * 2f, TextStyles.Headline);
        var labelSize = Typography.Measure(label, TextStyles.Headline);
        var contentLeft = drawn.Center.X - (glyph + glyphGap + labelSize.X) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(contentLeft + glyph * 0.5f, drawn.Center.Y), PhoneIcons.Plus, White,
            glyph);
        Typography.Draw(drawList, new Vector2(contentLeft + glyph + glyphGap, drawn.Center.Y - labelSize.Y * 0.5f),
            label, White, TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(button.Min, button.Max, hovered);
    }
}
