using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct RowListCard
{
    private const float SideInset = 14f;
    private const float SeparatorAlpha = 0.06f;

    private readonly ImDrawListPtr drawList;
    private readonly Vector4 titleInk;
    private readonly Vector2 origin;
    private readonly float width;
    private readonly float rowHeight;
    private readonly float sideInset;
    private readonly int rowCount;

    private RowListCard(ImDrawListPtr drawList, Vector4 titleInk, Vector2 origin, float width, float rowHeight,
        float sideInset, int rowCount)
    {
        this.drawList = drawList;
        this.titleInk = titleInk;
        this.origin = origin;
        this.width = width;
        this.rowHeight = rowHeight;
        this.sideInset = sideInset;
        this.rowCount = rowCount;
    }

    public static RowListCard Begin(AppSkin ui, int rowCount, float rowHeightUnscaled, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = rowHeightUnscaled * scale;
        var panelMax = new Vector2(origin.X + width, origin.Y + rowCount * rowHeight);
        ui.Card(drawList, origin, panelMax, Metrics.Radius.Card * scale, elevated: true);
        return new RowListCard(drawList, ui.TitleInk, origin, width, rowHeight, SideInset * scale, rowCount);
    }

    public Rect Row(int index)
    {
        var rowTop = origin.Y + index * rowHeight;
        if (index > 0)
        {
            drawList.AddLine(new Vector2(origin.X + sideInset, rowTop),
                new Vector2(origin.X + width - sideInset, rowTop),
                ImGui.GetColorU32(Palette.WithAlpha(titleInk, SeparatorAlpha)), 1f);
        }

        return new Rect(new Vector2(origin.X + sideInset, rowTop),
            new Vector2(origin.X + width - sideInset, rowTop + rowHeight));
    }

    public void End()
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowCount * rowHeight));
    }
}
