using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hub;

internal sealed class SnapRail
{
    public const float Pad = 4f;

    private readonly TileRail rail = new();
    private bool swiping;
    private float pressOffset;

    public float Offset => rail.Offset;

    public bool TapAllowed => rail.TapAllowed;

    public bool Swiping => rail.Swiping;

    public static float ContentWidth(int count, float itemWidth, float gap, float bleed) =>
        count <= 0 ? 0f : bleed * 2f + count * itemWidth + (count - 1) * gap;

    public static float SnapOffset(float pressOffset, float offset, float stride, float maxOffset)
    {
        if (stride <= 0f || maxOffset <= 0f)
        {
            return 0f;
        }

        var stops = (int)MathF.Ceiling(maxOffset / stride) + 1;
        var index = PageSnap.Target(pressOffset / stride, offset / stride, stops);
        return MathF.Min(index * stride, maxOffset);
    }

    public void Reset()
    {
        rail.Reset();
        swiping = false;
    }

    public static Rect Row(float left, float top, float width, float height, float scale) =>
        new(new Vector2(left - HubMetrics.RailBleed * scale, top - Pad * scale),
            new Vector2(left + width + HubMetrics.RailBleed * scale, top + height + Pad * scale));

    public void Begin(ImDrawListPtr drawList, string id, Rect row, float left, float width, float contentWidth,
        float stride)
    {
        var before = rail.Offset;
        rail.Begin(drawList, id, row, new Rect(new Vector2(left, row.Min.Y), new Vector2(left + width, row.Max.Y)),
            contentWidth);
        if (rail.Swiping && !swiping)
        {
            pressOffset = before;
        }
        else if (swiping && !rail.Swiping)
        {
            rail.SettleTo(SnapOffset(pressOffset, rail.Offset, stride, MathF.Max(0f, contentWidth - row.Width)));
        }

        swiping = rail.Swiping;
    }

    public void End(ImDrawListPtr drawList, Rect row, float contentWidth, AppSkin ui, float stride) =>
        rail.End(drawList, row, contentWidth, ui, stride);
}

internal sealed class ShelfColumns
{
    public const int Rows = 3;
    public const float RowHeight = 68f;
    public const float Peek = 28f;
    public const float ColumnGap = 16f;
    private const float IconSize = 56f;
    private const float TextGap = 12f;
    private const float ButtonMinWidth = 64f;
    private const float WashBleed = 6f;

    private readonly SnapRail rail = new();

    public bool Swiping => rail.Swiping;

    public static float Height(float scale) => Rows * RowHeight * scale;

    public static float ColumnWidth(float width, float scale) => MathF.Max(1f, width - Peek * scale);

    public static int ColumnCount(int entries) => (entries + Rows - 1) / Rows;

    public void Reset() => rail.Reset();

    public int Draw(ImDrawListPtr drawList, AppSkin ui, GamesLibrary library, ReadOnlySpan<int> entries,
        string railId, float left, float top, float width, out Rect source)
    {
        source = default;
        var scale = UiScale.Current;
        var columnWidth = ColumnWidth(width, scale);
        var gap = ColumnGap * scale;
        var stride = columnWidth + gap;
        var columns = ColumnCount(entries.Length);
        var contentWidth = SnapRail.ContentWidth(columns, columnWidth, gap, HubMetrics.RailBleed * scale);
        var row = SnapRail.Row(left, top, width, Height(scale), scale);
        rail.Begin(drawList, railId, row, left, width, contentWidth, stride);
        ImGui.PushID(railId);
        var interactive = rail.TapAllowed;
        var rowHeight = RowHeight * scale;
        var tapped = -1;
        for (var column = 0; column < columns; column++)
        {
            var columnLeft = left + column * stride - rail.Offset;
            if (columnLeft > row.Max.X || columnLeft + columnWidth < row.Min.X)
            {
                continue;
            }

            for (var rowIndex = 0; rowIndex < Rows; rowIndex++)
            {
                var position = column * Rows + rowIndex;
                if (position >= entries.Length)
                {
                    break;
                }

                var cell = new Rect(new Vector2(columnLeft, top + rowIndex * rowHeight),
                    new Vector2(columnLeft + columnWidth, top + (rowIndex + 1) * rowHeight));
                var divided = rowIndex < Rows - 1 && position + 1 < entries.Length;
                if (DrawRow(drawList, ui, library, entries[position], cell, divided, interactive, scale,
                        out var icon))
                {
                    tapped = entries[position];
                    source = icon;
                }
            }
        }

        ImGui.PopID();
        rail.End(drawList, row, contentWidth, ui, stride);
        return tapped;
    }

    private static bool DrawRow(ImDrawListPtr drawList, AppSkin ui, GamesLibrary library, int entryIndex, Rect cell,
        bool divided, bool interactive, float scale, out Rect icon)
    {
        var tileId = library.TileIds[entryIndex];
        var label = Loc.T(L.Games.Play);
        var buttonHeight = Button.SmallHeight * scale;
        var buttonWidth = MathF.Max(ButtonMinWidth * scale, Button.WidthFor(label, ButtonSize.Small));
        var button = new Rect(new Vector2(cell.Max.X - buttonWidth, cell.Center.Y - buttonHeight * 0.5f),
            new Vector2(cell.Max.X, cell.Center.Y + buttonHeight * 0.5f));
        var overButton = interactive && UiInteract.Hover(button.Min, button.Max);
        var hovered = interactive && !overButton && UiInteract.Hover(cell.Min, cell.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(tileId, pressed, Motion.PressScaleCard);
        if (hovered)
        {
            var bleed = WashBleed * scale;
            Squircle.Fill(drawList, new Vector2(cell.Min.X - bleed, cell.Min.Y),
                new Vector2(cell.Max.X + bleed, cell.Max.Y), Metrics.Radius.Md * scale,
                ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconHalf = IconSize * scale * 0.5f;
        var iconCenter = new Vector2(cell.Min.X + iconHalf, cell.Center.Y);
        var half = new Vector2(iconHalf * press);
        icon = new Rect(iconCenter - half, iconCenter + half);
        GameIconArt.Draw(drawList, library.IconIds[entryIndex], library.Accent(entryIndex), icon.Min, icon.Max, null,
            true);
        DrawText(drawList, ui, library, entryIndex, cell, button.Min.X, scale);
        if (divided)
        {
            drawList.AddLine(new Vector2(cell.Min.X + (IconSize + TextGap) * scale, cell.Max.Y),
                new Vector2(cell.Max.X, cell.Max.Y), ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
        }

        var play = Button.Draw(drawList, button, label, ui.Ink, ButtonStyle.Gray, id: library.PlayIds[entryIndex]);
        var tapped = UiInteract.Click(cell.Min, cell.Max, hovered);
        return tapped || (play && interactive);
    }

    private static void DrawText(ImDrawListPtr drawList, AppSkin ui, GamesLibrary library, int entryIndex, Rect cell,
        float right, float scale)
    {
        var textLeft = cell.Min.X + (IconSize + TextGap) * scale;
        var textWidth = MathF.Max(1f, right - TextGap * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var hookHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = cell.Center.Y - (titleHeight + hookHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(library.Title(entryIndex), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        var hook = library.Hook(entryIndex);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(hook.Length > 0 ? hook : library.Meta(entryIndex), textWidth, TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
    }
}
