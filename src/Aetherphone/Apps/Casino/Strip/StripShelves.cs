using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Strip;

internal interface IStripFloor
{
    PosterInfo Describe(in StripEntry entry);

    ICabinetIdle? IdleFor(string gameId);
}

internal sealed class StripShelves
{
    public const float TileGap = 12f;
    public const float RailPad = 4f;
    public const float GridGap = 8f;
    public const int CardColumns = 3;
    public const int QuickColumns = 4;

    private const string TileId = "tile";

    private readonly CabinetPreview[] previews = new CabinetPreview[Machines.MachineCabinet.MachineIds.Length];
    private readonly string[] shelfIds = new string[StripCatalog.Shelves.Length];

    public StripShelves()
    {
        for (var index = 0; index < previews.Length; index++)
        {
            previews[index] = new CabinetPreview();
        }

        for (var index = 0; index < shelfIds.Length; index++)
        {
            shelfIds[index] = "##casino.grid." + index;
        }
    }

    public static float RowWidth(int count, float tileWidth, float gap) =>
        count <= 0 ? 0f : count * tileWidth + (count - 1) * gap;

    public static float RailContentWidth(int count, float tileWidth, float gap, float pad) =>
        count <= 0 ? 0f : RowWidth(count, tileWidth, gap) + pad * 2f;

    public static float GridHeight(int count, int columns, float tileHeight, float scale) =>
        StripGrid.Height(count, columns, tileHeight, GridGap * scale);

    public float DrawGrid(ImDrawListPtr drawList, IStripFloor floor, StripShelf shelf, Vector2 origin, float width,
        float phase, float deltaSeconds, bool live, float scale, out StripEntry tapped, out Rect source)
    {
        tapped = default;
        source = default;
        var entries = StripCatalog.EntriesOf(shelf);
        var columns = StripCatalog.ColumnsOf(shelf);
        var layout = StripCatalog.LayoutOf(shelf);
        var gap = GridGap * scale;
        var tileWidth = StripGrid.CellWidth(width, columns, gap);
        var textBlock = PosterTile.TextBlock(layout, scale);
        var tileHeight = PosterTile.TileHeight(layout, textBlock, scale);
        using (UiScope.PushId(shelfIds[(int)shelf]))
        {
            for (var index = 0; index < entries.Length; index++)
            {
                var min = StripGrid.CellOrigin(origin, index, columns, tileWidth, tileHeight, gap);
                var tile = new Rect(min, new Vector2(min.X + tileWidth, min.Y + tileHeight));
                if (!ImGui.IsRectVisible(tile.Min, tile.Max))
                {
                    continue;
                }

                using (UiScope.PushId(index))
                {
                    if (DrawEntry(drawList, floor, entries[index], tile, layout, textBlock, phase, deltaSeconds, live,
                            scale, out var pressed))
                    {
                        tapped = entries[index];
                        source = pressed;
                    }
                }
            }
        }

        return origin.Y + GridHeight(entries.Length, columns, tileHeight, scale);
    }

    private bool DrawEntry(ImDrawListPtr drawList, IStripFloor floor, in StripEntry entry, Rect tile,
        in PosterLayout layout, float textBlock, float phase, float deltaSeconds, bool live, float scale,
        out Rect pressed)
    {
        var info = floor.Describe(entry);
        var tint = CasinoArt.TintOf(entry.GameId);
        var hovered = CasinoArt.PressCard(ImGui.GetID(TileId), tile.Min, tile.Max, out var pressedMin,
            out var pressedMax);
        pressed = new Rect(pressedMin, pressedMax);
        PosterTile.DrawFrame(drawList, pressed, tint, layout, scale);
        var idle = entry.LiveIdle ? floor.IdleFor(entry.GameId) : null;
        if (idle is not null)
        {
            var preview = previews[Math.Max(0, Machines.MachineCabinet.IndexOf(entry.GameId))];
            preview.Prepare(idle);
            preview.Draw(drawList, PosterTile.ArtBelowSign(pressed, layout, textBlock, scale),
                Metrics.Radius.Md * scale, tint, live, hovered, deltaSeconds, scale);
            PosterTile.DrawSignScrim(drawList, pressed, layout, scale);
        }
        else
        {
            PosterTile.DrawGlyph(drawList, pressed, entry.GameId, layout, textBlock, scale);
        }

        PosterTile.DrawSign(drawList, pressed, StripCatalog.SignOf(entry), phase, layout, scale);
        PosterTile.DrawText(drawList, pressed, Loc.T(entry.Title), info, layout, textBlock, scale);
        PosterTile.DrawCrowd(drawList, pressed, info.Crowd, layout, scale);
        PosterTile.DrawBadge(drawList, pressed, info.Badge, layout, scale);
        if (!info.Open)
        {
            PosterTile.DrawClosed(drawList, pressed, layout, scale);
        }

        PosterTile.DrawRim(drawList, pressed, tint, hovered, layout, scale);
        return UiInteract.Click(tile.Min, tile.Max, hovered);
    }
}
