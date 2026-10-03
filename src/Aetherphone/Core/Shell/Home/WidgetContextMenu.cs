using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.Shell.Home;

internal sealed class WidgetContextMenu
{
    private const float WidthUnits = 232f;
    private const float RowUnits = 44f;
    private const float SizeRowUnits = 58f;
    private const float GroupGapUnits = 7f;
    private const float RadiusUnits = 18f;
    private const float AnchorGapUnits = 10f;
    private const float EdgeUnits = 6f;
    private const float RowPadUnits = 16f;
    private const float GlyphUnits = 16f;
    private const float SizeGlyphUnits = 10f;
    private const float GroupGapAlpha = 0.10f;
    private const float HairlineAlpha = 0.16f;
    private const float HoverAlpha = 0.08f;
    private const float SelectedAlpha = 0.14f;
    private const float InteractiveThreshold = 0.9f;
    private const float CloseThreshold = 0.03f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly WidgetSize[] AllSizes = { WidgetSize.Small, WidgetSize.Medium, WidgetSize.Large };

    private readonly HomeLayoutService layout;
    private readonly WidgetEditSheet editSheet;
    private readonly StackEditSheet stackSheet;
    private HomeTile? tile;
    private Spring pop;
    private bool closing;
    private int openedFrame = -1;

    public WidgetContextMenu(HomeLayoutService layout, WidgetHost widgetHost)
    {
        this.layout = layout;
        editSheet = new WidgetEditSheet(layout, widgetHost);
        stackSheet = new StackEditSheet(layout);
    }

    public bool Active => tile is not null || editSheet.Active || stackSheet.Active;

    public HomeTile? Tile => tile;

    public void Open(HomeTile target)
    {
        tile = target;
        closing = false;
        openedFrame = ImGui.GetFrameCount();
        pop.SnapTo(0f);
    }

    public void Close()
    {
        closing = true;
    }

    public void CloseImmediate()
    {
        tile = null;
        closing = false;
        pop.SnapTo(0f);
        editSheet.CloseImmediately();
        stackSheet.CloseImmediately();
    }

    public void DrawSheets(Rect screen, PhoneTheme theme, float delta)
    {
        editSheet.Draw(screen, theme, delta);
        stackSheet.Draw(screen, theme);
    }

    public void Draw(Rect content, Rect anchor, PhoneTheme theme, float delta, float scale)
    {
        if (tile is null)
        {
            return;
        }

        pop.Step(closing ? 0f : 1f, Motion.Appear, delta);
        if (closing && pop.Value < CloseThreshold)
        {
            tile = null;
            closing = false;
            return;
        }

        var current = tile;
        var sizes = HomeLayoutService.SizesOf(current);
        var sizeCount = CountSizes(sizes);
        var canEdit = current.Visible.Widget is { } visibleWidget && visibleWidget.Options.Count > 0;
        var rowCount = 1 + (canEdit ? 1 : 0) + (current.IsStack ? 1 : 0);
        var hasSizes = sizeCount > 1;
        var hasMiddle = canEdit || current.IsStack;
        var height = rowCount * RowUnits * scale + (hasSizes ? SizeRowUnits * scale : 0f) +
                     GroupGapUnits * scale * ((hasSizes ? 1 : 0) + (hasMiddle ? 1 : 0));
        var width = WidthUnits * scale;
        var gap = AnchorGapUnits * scale;
        var below = anchor.Max.Y + gap + height <= content.Max.Y;
        var top = below ? anchor.Max.Y + gap : MathF.Max(content.Min.Y, anchor.Min.Y - gap - height);
        var left = Math.Clamp(anchor.Center.X - width * 0.5f, content.Min.X + EdgeUnits * scale,
            content.Max.X - width - EdgeUnits * scale);
        var panel = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        var pivot = below ? new Vector2(anchor.Center.X, panel.Min.Y) : new Vector2(anchor.Center.X, panel.Max.Y);
        var eased = Math.Clamp(pop.Value, 0f, 1f);
        var scaled = new Rect(pivot + (panel.Min - pivot) * eased, pivot + (panel.Max - pivot) * eased);
        var drawList = ImGui.GetWindowDrawList();
        var radius = RadiusUnits * scale;
        Elevation.Floating(drawList, scaled.Min, scaled.Max, radius, scale, eased);
        Material.ThemedGlass(drawList, scaled.Min, scaled.Max, radius, scale, theme, eased);
        var interactive = !closing && eased > InteractiveThreshold;
        if (!interactive)
        {
            return;
        }

        var ink = Material.ToneFor(theme) == GlassTone.Light ? theme.TextStrong : White;
        drawList.PushClipRect(panel.Min, panel.Max, true);
        var cursorY = panel.Min.Y;
        var handled = false;
        if (hasSizes)
        {
            handled = DrawSizes(drawList, panel, cursorY, current, sizes, sizeCount, ink, scale);
            cursorY += SizeRowUnits * scale;
            cursorY = GroupGap(drawList, panel, cursorY, ink, scale);
        }

        if (canEdit && Row(drawList, panel, ref cursorY, Loc.T(L.WidgetStacks.EditWidget),
                FontAwesomeIcon.SlidersH, ink, scale, false))
        {
            editSheet.Open(current.Visible);
            Close();
            handled = true;
        }

        if (current.IsStack && Row(drawList, panel, ref cursorY, Loc.T(L.WidgetStacks.EditStack),
                FontAwesomeIcon.LayerGroup, ink, scale, canEdit))
        {
            stackSheet.Open(current);
            Close();
            handled = true;
        }

        if (hasMiddle)
        {
            cursorY = GroupGap(drawList, panel, cursorY, ink, scale);
        }

        var removeLabel = current.IsStack ? L.WidgetStacks.RemoveStack : L.WidgetStacks.RemoveWidget;
        if (Row(drawList, panel, ref cursorY, Loc.T(removeLabel), FontAwesomeIcon.MinusCircle, theme.Danger, scale,
                false))
        {
            layout.RemoveTile(current);
            Close();
            handled = true;
        }

        drawList.PopClipRect();
        if (!handled && ImGui.GetFrameCount() != openedFrame && UiInteract.ClickedOutside(panel.Min, panel.Max, false))
        {
            Close();
        }
    }

    private bool DrawSizes(ImDrawListPtr drawList, Rect panel, float top, HomeTile current, WidgetSizeSet sizes,
        int sizeCount, Vector4 ink, float scale)
    {
        var segmentWidth = panel.Width / sizeCount;
        var segmentIndex = 0;
        var picked = false;
        for (var index = 0; index < AllSizes.Length; index++)
        {
            var size = AllSizes[index];
            if (!WidgetSizes.Contains(sizes, size))
            {
                continue;
            }

            var left = panel.Min.X + segmentWidth * segmentIndex;
            segmentIndex++;
            var segment = new Rect(new Vector2(left, top), new Vector2(left + segmentWidth, top + SizeRowUnits * scale));
            var inner = segment.Inset(EdgeUnits * scale);
            var selected = current.Size == size;
            var hovered = UiInteract.Hover(segment.Min, segment.Max);
            if (selected || hovered)
            {
                Squircle.Fill(drawList, inner.Min, inner.Max, RadiusUnits * 0.6f * scale,
                    ImGui.GetColorU32(ink with { W = selected ? SelectedAlpha : HoverAlpha }));
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var label = Loc.T(SizeLabel(size));
            var labelSize = Typography.Measure(label, TextStyles.Footnote);
            var glyphHeight = SizeGlyphUnits * WidgetSizes.RowSpan(size) * 0.5f * scale;
            var glyphWidth = SizeGlyphUnits * WidgetSizes.ColumnSpan(size) * 0.5f * scale;
            var glyphCenter = new Vector2(inner.Center.X, inner.Min.Y + (inner.Height - labelSize.Y) * 0.5f);
            var glyphMin = glyphCenter - new Vector2(glyphWidth, glyphHeight) * 0.5f;
            var glyphMax = glyphCenter + new Vector2(glyphWidth, glyphHeight) * 0.5f;
            var glyphRadius = 3f * scale;
            if (selected)
            {
                Squircle.Fill(drawList, glyphMin, glyphMax, glyphRadius, ImGui.GetColorU32(ink));
            }
            else
            {
                Squircle.Stroke(drawList, glyphMin, glyphMax, glyphRadius, ImGui.GetColorU32(ink), 1.4f * scale);
            }

            var fitted = Typography.FitText(label, MathF.Max(1f, inner.Width), TextStyles.Footnote);
            Typography.DrawCentered(drawList, new Vector2(inner.Center.X, inner.Max.Y - labelSize.Y * 0.5f), fitted,
                ink, TextStyles.Footnote);
            if (!picked && UiInteract.Click(segment.Min, segment.Max, hovered))
            {
                layout.ResizeWidget(current, size);
                Close();
                picked = true;
            }
        }

        return picked;
    }

    private static bool Row(ImDrawListPtr drawList, Rect panel, ref float cursorY, string label,
        FontAwesomeIcon icon, Vector4 ink, float scale, bool divider)
    {
        var row = new Rect(new Vector2(panel.Min.X, cursorY), new Vector2(panel.Max.X, cursorY + RowUnits * scale));
        cursorY = row.Max.Y;
        if (divider)
        {
            drawList.AddLine(new Vector2(row.Min.X + RowPadUnits * scale, row.Min.Y), new Vector2(row.Max.X, row.Min.Y),
                ImGui.GetColorU32(ink with { W = HairlineAlpha }), MathF.Max(1f, 0.5f * scale));
        }

        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(ink with { W = HoverAlpha }));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = RowPadUnits * scale;
        var glyphCenter = new Vector2(row.Max.X - pad - GlyphUnits * 0.5f * scale, row.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, IconGlyph.Of(icon), ink, GlyphUnits * scale);
        var labelWidth = MathF.Max(1f, row.Width - pad * 3f - GlyphUnits * scale);
        var fitted = Typography.FitText(label, labelWidth, TextStyles.Body);
        var size = Typography.Measure(fitted, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(row.Min.X + pad, row.Center.Y - size.Y * 0.5f), fitted, ink,
            TextStyles.Body);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static float GroupGap(ImDrawListPtr drawList, Rect panel, float cursorY, Vector4 ink, float scale)
    {
        var bottom = cursorY + GroupGapUnits * scale;
        drawList.AddRectFilled(new Vector2(panel.Min.X, cursorY), new Vector2(panel.Max.X, bottom),
            ImGui.GetColorU32(ink with { W = GroupGapAlpha }));
        return bottom;
    }

    private static int CountSizes(WidgetSizeSet sizes)
    {
        var count = 0;
        for (var index = 0; index < AllSizes.Length; index++)
        {
            if (WidgetSizes.Contains(sizes, AllSizes[index]))
            {
                count++;
            }
        }

        return count;
    }

    private static LocString SizeLabel(WidgetSize size) => size switch
    {
        WidgetSize.Small => L.Home.SizeSmall,
        WidgetSize.Large => L.Home.SizeLarge,
        _ => L.Home.SizeMedium,
    };
}
