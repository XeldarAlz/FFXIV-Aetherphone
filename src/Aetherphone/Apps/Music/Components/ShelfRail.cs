using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.Components;

internal sealed class ShelfRail
{
    public const float TileGap = 12f;

    private readonly PanRail rail = new();
    private Vector2 origin;
    private float width;
    private float height;
    private float tileWidth;
    private float gap;
    private float inset;
    private bool open;

    public bool SeeAllTapped { get; private set; }

    public float Offset => rail.Offset;

    public void Begin(AppSkin ui, string title, bool seeAll, int count, float tileWidth, float tileHeight)
    {
        var scale = UiScale.Current;
        SeeAllTapped = title.Length > 0 && SectionHeader.Draw(ui, title, seeAll) && seeAll;
        origin = ImGui.GetCursorScreenPos();
        width = ScrollLayout.StableContentWidth();
        height = tileHeight;
        this.tileWidth = tileWidth;
        gap = TileGap * scale;
        inset = MusicUi.Inset * scale;
        var contentWidth = inset * 2f + count * tileWidth + Math.Max(0, count - 1) * gap;
        rail.Begin(new Rect(origin, origin + new Vector2(width, height)), contentWidth);
        open = true;
    }

    public bool Tile(int index, out Rect tile)
    {
        var left = origin.X + inset + index * (tileWidth + gap) - rail.Offset;
        tile = new Rect(new Vector2(left, origin.Y), new Vector2(left + tileWidth, origin.Y + height));
        return tile.Max.X >= origin.X && tile.Min.X <= origin.X + width;
    }

    public bool Hover(Rect tile) => rail.Hover(tile.Min, tile.Max);

    public bool Tapped(Rect tile, bool hovered) => rail.Tapped(tile.Min, tile.Max, hovered);

    public void End()
    {
        if (!open)
        {
            return;
        }

        open = false;
        rail.End();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    public void Reset() => rail.Reset();
}
