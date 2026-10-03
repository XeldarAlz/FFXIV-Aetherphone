using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed partial class WidgetGallery
{
    private const int QueryMaxLength = 64;
    private const float FieldTopUnits = 4f;
    private const float FieldHeightUnits = 40f;
    private const float FieldGapUnits = 12f;
    private const float StaticFieldTextInsetUnits = 35f;
    private const float WheelStepUnits = 46f;
    private const float ListTopUnits = 4f;
    private const float SectionGapUnits = 26f;
    private const float TitleGapUnits = 10f;
    private const float RailGapUnits = 14f;
    private const float CaptionGapUnits = 8f;
    private const float CaptionLineGapUnits = 1f;
    private const float RowHeightUnits = 58f;
    private const float RowIconUnits = 38f;
    private const float RowIconGapUnits = 12f;
    private const float RowHoverInsetUnits = 4f;
    private const float RowHoverRadiusUnits = 13f;
    private const float GroupRadiusUnits = 18f;
    private const float GroupInsetUnits = 12f;
    private const float GroupFillAlpha = 0.07f;
    private const float RowHoverAlpha = 0.07f;
    private const float HairlineAlpha = 0.12f;
    private const float ChevronUnits = 14f;
    private const float ChevronGapUnits = 8f;
    private const float NameCountGapUnits = 2f;
    private const float BottomPadUnits = 28f;
    private const float EmptyTopFraction = 0.2f;
    private const float EmptyGlyphUnits = 34f;
    private const float EmptyGlyphGapUnits = 12f;
    private const float EmptyLineGapUnits = 6f;
    private const float StackGlyphWidth = 0.54f;
    private const float StackGlyphHeight = 0.3f;
    private const float StackGlyphBackLift = 0.13f;
    private const float StackGlyphFrontDrop = 0.05f;
    private const float StackGlyphBackShrink = 0.78f;
    private const float StackGlyphBackAlpha = 0.55f;
    private const int RailSlots = WidgetGalleryIndex.FeaturedCapacity + 1;
    private static readonly Vector4 StackTileSurface = new(0.33f, 0.35f, 0.44f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly GalleryDrag railDrag = new();
    private readonly Spring[] cardHover = new Spring[RailSlots];
    private string query = string.Empty;
    private float scrollY;
    private float contentHeight;
    private float railOffset;

    private void DrawRoot(in SheetFrame frame, PhoneTheme theme, in HomeMetrics metrics, float delta, float offsetX,
        float clipRight, float progress, bool interactive)
    {
        var drawList = frame.DrawList;
        var scale = metrics.Scale;
        var content = frame.Content;
        var side = SidePaddingUnits * scale;
        var left = content.Min.X + offsetX;
        var right = content.Max.X + offsetX;
        var vertexStart = drawList.VtxBuffer.Size;
        drawList.PushClipRect(frame.Panel.Min, new Vector2(clipRight, frame.Panel.Max.Y), true);
        var fieldTop = content.Min.Y + FieldTopUnits * scale;
        var field = new Rect(new Vector2(left + side, fieldTop),
            new Vector2(right - side, fieldTop + FieldHeightUnits * scale));
        DrawSearchField(drawList, field, theme, scale, interactive);
        var view = new Rect(new Vector2(left, field.Max.Y + FieldGapUnits * scale),
            new Vector2(right, frame.Panel.Max.Y));
        DrawRootList(drawList, view, theme, frame.Ink, in metrics, delta, interactive);
        drawList.PopClipRect();
        LayerCompositor.Fade(drawList, vertexStart, 1f - RootDim * progress);
    }

    private void DrawSearchField(ImDrawListPtr drawList, Rect field, PhoneTheme theme, float scale, bool editable)
    {
        GlassField.Surface(drawList, field, GlassField.Radius(field), scale, WallpaperLegibility.Strength(theme), 1f);
        var hint = Loc.T(L.WidgetGallery.SearchHint);
        if (editable)
        {
            var previous = query;
            GlassField.Search(drawList, field, "##widgetGalleryQuery", hint, ref query, theme, scale, QueryMaxLength,
                false);
            if (string.Equals(previous, query, StringComparison.Ordinal))
            {
                return;
            }

            scrollY = 0f;
            index.Refresh(query, false);
            return;
        }

        GlassField.SearchGlyph(drawList, field, theme, scale, 1f);
        var hasQuery = query.Length > 0;
        var inset = StaticFieldTextInsetUnits * scale;
        var text = Typography.FitText(hasQuery ? query : hint, field.Width - inset * 2f, TextStyles.Body);
        var height = Typography.Measure(text, TextStyles.Body).Y;
        Typography.Draw(drawList, new Vector2(field.Min.X + inset, field.Center.Y - height * 0.5f), text,
            hasQuery ? theme.TextStrong : theme.TextMuted, TextStyles.Body);
    }

    private void DrawRootList(ImDrawListPtr drawList, Rect view, PhoneTheme theme, Vector4 ink,
        in HomeMetrics metrics, float delta, bool interactive)
    {
        var scale = metrics.Scale;
        var pointerInView = interactive && UiInteract.Hover(view.Min, view.Max);
        var io = ImGui.GetIO();
        if (pointerInView && !io.KeyShift)
        {
            scrollY -= io.MouseWheel * WheelStepUnits * scale;
        }

        scrollY = Math.Clamp(scrollY, 0f, MathF.Max(0f, contentHeight - view.Height));
        drawList.PushClipRect(view.Min, view.Max, true);
        var top = view.Min.Y - scrollY;
        if (index.IsEmpty)
        {
            DrawEmpty(drawList, view, ink, scale);
            contentHeight = 0f;
            drawList.PopClipRect();
            return;
        }

        var side = SidePaddingUnits * scale;
        var left = view.Min.X + side;
        var width = view.Width - side * 2f;
        var cursorY = top + ListTopUnits * scale;
        if (!index.Searching)
        {
            cursorY = DrawSectionTitle(drawList, left, cursorY, width, Loc.T(L.WidgetGallery.Featured), ink, scale);
            cursorY = DrawRail(drawList, view, cursorY, theme, ink, in metrics, delta, pointerInView);
            cursorY += SectionGapUnits * scale;
            cursorY = DrawSectionTitle(drawList, left, cursorY, width, Loc.T(L.WidgetGallery.AllWidgets), ink,
                scale);
        }

        cursorY = DrawAppGroup(drawList, view, left, width, cursorY, ink, scale, pointerInView);
        contentHeight = cursorY + BottomPadUnits * scale - top;
        scrollY = Math.Clamp(scrollY, 0f, MathF.Max(0f, contentHeight - view.Height));
        drawList.PopClipRect();
    }

    private static float DrawSectionTitle(ImDrawListPtr drawList, float left, float top, float width, string title,
        Vector4 ink, float scale)
    {
        var fitted = Typography.FitText(title, width, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(left, top), fitted, ink, TextStyles.Title3);
        return top + Typography.Measure(fitted, TextStyles.Title3).Y + TitleGapUnits * scale;
    }

    private float DrawRail(ImDrawListPtr drawList, Rect view, float top, PhoneTheme theme, Vector4 ink,
        in HomeMetrics metrics, float delta, bool pointerInView)
    {
        var scale = metrics.Scale;
        var side = SidePaddingUnits * scale;
        var gap = RailGapUnits * scale;
        var room = new Vector2(view.Width - side * 2f, float.MaxValue);
        var stack = index.Suggestions(WidgetSize.Small);
        var showsStack = index.ShowsSmartStack && stack.Count > 0;
        var featured = index.Featured;
        var contentWidth = side * 2f - gap;
        var cardHeight = 0f;
        if (showsStack)
        {
            var footprint = RailFootprint(WidgetSize.Small, in metrics, room);
            contentWidth += footprint.X + gap;
            cardHeight = footprint.Y;
        }

        for (var pickIndex = 0; pickIndex < featured.Count; pickIndex++)
        {
            var footprint = RailFootprint(featured[pickIndex].Size, in metrics, room);
            contentWidth += footprint.X + gap;
            cardHeight = MathF.Max(cardHeight, footprint.Y);
        }

        var nameHeight = Typography.Measure(" ", TextStyles.FootnoteEmphasized).Y;
        var captionHeight = Typography.Measure(" ", TextStyles.Caption1).Y;
        var bottom = top + cardHeight + (CaptionGapUnits + CaptionLineGapUnits) * scale + nameHeight + captionHeight;
        var rail = new Rect(new Vector2(view.Min.X, top), new Vector2(view.Max.X, bottom));
        TrackRail(rail, contentWidth - view.Width, scale, delta, pointerInView);
        var cursorX = view.Min.X + side - railOffset;
        var slot = 0;
        if (showsStack)
        {
            var footprint = RailFootprint(WidgetSize.Small, in metrics, room);
            var card = new Rect(new Vector2(cursorX, top), new Vector2(cursorX + footprint.X, top + footprint.Y));
            if (DrawRailCard(drawList, view, card, slot, null, WidgetSize.Small, Loc.T(L.WidgetGallery.SmartStack),
                    Loc.T(L.WidgetGallery.SmartStackCaption), theme, ink, in metrics, delta, pointerInView))
            {
                PushStack(WidgetSize.Small);
            }

            cursorX = card.Max.X + gap;
            slot++;
        }

        for (var pickIndex = 0; pickIndex < featured.Count; pickIndex++)
        {
            var pick = featured[pickIndex];
            var footprint = RailFootprint(pick.Size, in metrics, room);
            var card = new Rect(new Vector2(cursorX, top), new Vector2(cursorX + footprint.X, top + footprint.Y));
            var entry = index.Find(pick.Widget.AppId);
            var appName = entry is null ? string.Empty : entry.App.DisplayName;
            if (DrawRailCard(drawList, view, card, slot, pick.Widget, pick.Size, pick.Widget.DisplayName, appName,
                    theme, ink, in metrics, delta, pointerInView) && entry is not null)
            {
                PushApp(entry, pick.Widget, pick.Size);
            }

            cursorX = card.Max.X + gap;
            slot++;
        }

        return bottom;
    }

    private static Vector2 RailFootprint(WidgetSize size, in HomeMetrics metrics, Vector2 room)
    {
        var footprint = WidgetGalleryPreview.Footprint(size, in metrics);
        return footprint * WidgetGalleryPreview.Fit(footprint, room);
    }

    private void TrackRail(Rect rail, float overflow, float scale, float delta, bool pointerInView)
    {
        var maximum = MathF.Max(0f, overflow);
        var phase = railDrag.Track(rail, pointerInView, scale, delta, out var deltaX);
        if (phase == GalleryDragPhase.Dragging)
        {
            sheet.YieldPointer();
            railOffset -= deltaX;
        }

        if (pointerInView && UiInteract.Hover(rail.Min, rail.Max))
        {
            var io = ImGui.GetIO();
            var horizontal = io.MouseWheelH != 0f ? io.MouseWheelH : io.KeyShift ? io.MouseWheel : 0f;
            railOffset -= horizontal * WheelStepUnits * scale;
        }

        railOffset = Math.Clamp(railOffset, 0f, maximum);
    }

    private bool DrawRailCard(ImDrawListPtr drawList, Rect view, Rect card, int slot, IHomeWidget? widget,
        WidgetSize size, string name, string caption, PhoneTheme theme, Vector4 ink, in HomeMetrics metrics,
        float delta, bool pointerInView)
    {
        if (card.Max.X < view.Min.X || card.Min.X > view.Max.X)
        {
            cardHover[slot].SnapTo(0f);
            return false;
        }

        var scale = metrics.Scale;
        var hovered = pointerInView && !railDrag.Claimed && UiInteract.Hover(card.Min, card.Max);
        cardHover[slot].Step(hovered ? 1f : 0f, Motion.HoverLift, delta);
        var factor = card.Width / WidgetGalleryPreview.Footprint(size, in metrics).X;
        var grow = 1f + Motion.HoverLiftCard * cardHover[slot].Value;
        var drawn = WidgetGalleryPreview.Centered(card.Center, card.Size * grow);
        if (widget is null)
        {
            WidgetGalleryPreview.DrawStack(drawList, widgetHost, index.Suggestions(size), size, drawn, theme, ink,
                scale * factor * grow, delta);
        }
        else
        {
            WidgetGalleryPreview.Draw(drawList, widgetHost, widget, size, drawn, theme, scale * factor * grow, delta);
        }

        var nameTop = card.Max.Y + CaptionGapUnits * scale;
        var fittedName = Typography.FitText(name, card.Width, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(card.Min.X, nameTop), fittedName, ink, TextStyles.FootnoteEmphasized);
        var captionTop = nameTop + Typography.Measure(fittedName, TextStyles.FootnoteEmphasized).Y +
                         CaptionLineGapUnits * scale;
        Typography.Draw(drawList, new Vector2(card.Min.X, captionTop),
            Typography.FitText(caption, card.Width, TextStyles.Caption1), Secondary(ink), TextStyles.Caption1);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private float DrawAppGroup(ImDrawListPtr drawList, Rect view, float left, float width, float top, Vector4 ink,
        float scale, bool pointerInView)
    {
        var apps = index.Apps;
        var showsStack = index.ShowsSmartStack;
        var rows = apps.Count + (showsStack ? 1 : 0);
        if (rows == 0)
        {
            return top;
        }

        var rowHeight = RowHeightUnits * scale;
        var group = new Rect(new Vector2(left, top), new Vector2(left + width, top + rows * rowHeight));
        Squircle.Fill(drawList, group.Min, group.Max, GroupRadiusUnits * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ink, GroupFillAlpha)));
        var row = 0;
        if (showsStack)
        {
            var rect = RowRect(group, row, rowHeight);
            if (DrawAppRow(drawList, view, rect, null, Loc.T(L.WidgetGallery.SmartStack),
                    Loc.T(L.WidgetGallery.SmartStackCaption), ink, scale, pointerInView, row < rows - 1))
            {
                PushStack(WidgetSize.Small);
            }

            row++;
        }

        for (var appIndex = 0; appIndex < apps.Count; appIndex++)
        {
            var entry = apps[appIndex];
            var rect = RowRect(group, row, rowHeight);
            if (DrawAppRow(drawList, view, rect, entry, entry.App.DisplayName, entry.CountText, ink, scale,
                    pointerInView, row < rows - 1))
            {
                PushApp(entry, null, WidgetSize.Small);
            }

            row++;
        }

        return group.Max.Y;
    }

    private static Rect RowRect(Rect group, int row, float rowHeight) =>
        new(new Vector2(group.Min.X, group.Min.Y + row * rowHeight),
            new Vector2(group.Max.X, group.Min.Y + (row + 1) * rowHeight));

    private static bool DrawAppRow(ImDrawListPtr drawList, Rect view, Rect row, WidgetGalleryApp? entry,
        string name, string detail, Vector4 ink, float scale, bool pointerInView, bool separator)
    {
        if (row.Max.Y < view.Min.Y || row.Min.Y > view.Max.Y)
        {
            return false;
        }

        var hovered = pointerInView && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            var inset = RowHoverInsetUnits * scale;
            Squircle.Fill(drawList, row.Min + new Vector2(inset, inset), row.Max - new Vector2(inset, inset),
                RowHoverRadiusUnits * scale, ImGui.GetColorU32(Palette.WithAlpha(ink, RowHoverAlpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconSize = RowIconUnits * scale;
        var iconCenter = new Vector2(row.Min.X + GroupInsetUnits * scale + iconSize * 0.5f, row.Center.Y);
        if (entry is null)
        {
            DrawStackIcon(drawList, iconCenter, iconSize);
        }
        else
        {
            IconTile.DrawApp(drawList, entry.App.Id, iconCenter, iconSize, IconTile.Surface(entry.App.Accent));
        }

        var textLeft = iconCenter.X + iconSize * 0.5f + RowIconGapUnits * scale;
        var chevronSize = ChevronUnits * scale;
        var chevronCenter = new Vector2(row.Max.X - GroupInsetUnits * scale - chevronSize * 0.5f, row.Center.Y);
        var textWidth = chevronCenter.X - chevronSize * 0.5f - ChevronGapUnits * scale - textLeft;
        var fittedName = Typography.FitText(name, textWidth, TextStyles.Headline);
        var fittedDetail = Typography.FitText(detail, textWidth, TextStyles.Footnote);
        var nameHeight = Typography.Measure(fittedName, TextStyles.Headline).Y;
        var detailHeight = Typography.Measure(fittedDetail, TextStyles.Footnote).Y;
        var gap = NameCountGapUnits * scale;
        var textTop = row.Center.Y - (nameHeight + gap + detailHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), fittedName, ink, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + gap), fittedDetail, Secondary(ink),
            TextStyles.Footnote);
        PhoneIcon.Draw(drawList, chevronCenter, PhoneIcons.ChevronRight, Secondary(ink), chevronSize);
        if (separator)
        {
            drawList.AddLine(new Vector2(textLeft, row.Max.Y), new Vector2(row.Max.X, row.Max.Y),
                ImGui.GetColorU32(Palette.WithAlpha(ink, HairlineAlpha)), 1f);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static void DrawStackIcon(ImDrawListPtr drawList, Vector2 center, float size)
    {
        var half = size * 0.5f;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        IconTile.FillShaded(drawList, min, max, size * Metrics.Radius.TileFactor, StackTileSurface);
        var glyphHalf = new Vector2(size * StackGlyphWidth, size * StackGlyphHeight) * 0.5f;
        var glyphRadius = glyphHalf.Y * 0.6f;
        var backCenter = center - new Vector2(0f, size * StackGlyphBackLift);
        var backHalf = new Vector2(glyphHalf.X * StackGlyphBackShrink, glyphHalf.Y);
        Squircle.Fill(drawList, backCenter - backHalf, backCenter + backHalf, glyphRadius,
            ImGui.GetColorU32(Palette.WithAlpha(White, StackGlyphBackAlpha)));
        var frontCenter = center + new Vector2(0f, size * StackGlyphFrontDrop);
        Squircle.Fill(drawList, frontCenter - glyphHalf, frontCenter + glyphHalf, glyphRadius,
            ImGui.GetColorU32(White));
    }

    private static void DrawEmpty(ImDrawListPtr drawList, Rect view, Vector4 ink, float scale)
    {
        var width = view.Width - SidePaddingUnits * scale * 2f;
        var glyph = EmptyGlyphUnits * scale;
        var top = view.Min.Y + view.Height * EmptyTopFraction;
        PhoneIcon.Draw(drawList, new Vector2(view.Center.X, top + glyph * 0.5f), PhoneIcons.Search, Secondary(ink),
            glyph);
        var titleTop = top + glyph + EmptyGlyphGapUnits * scale;
        var titleBottom = Typography.DrawWrappedCentered(drawList, Loc.T(L.WidgetGallery.NoResults),
            TextStyles.Title3, ink, new Vector2(view.Center.X, titleTop), width);
        Typography.DrawWrappedCentered(drawList, Loc.T(L.WidgetGallery.NoResultsHint), TextStyles.Subheadline,
            Secondary(ink), new Vector2(view.Center.X, titleBottom + EmptyLineGapUnits * scale), width);
    }
}
