using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

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
    public const float TitleGap = 8f;
    public const float ShelfGap = 22f;

    private const string RecentRailId = "##casino.recent";

    private readonly TileRail[] rails = new TileRail[StripCatalog.Shelves.Length];
    private readonly TileRail recentRail = new();
    private readonly CabinetPreview[] previews = new CabinetPreview[Machines.MachineCabinet.MachineIds.Length];
    private readonly string[] railIds = new string[StripCatalog.Shelves.Length];

    public StripShelves()
    {
        for (var index = 0; index < rails.Length; index++)
        {
            rails[index] = new TileRail();
            railIds[index] = "##casino.shelf." + index;
        }

        for (var index = 0; index < previews.Length; index++)
        {
            previews[index] = new CabinetPreview();
        }
    }

    public void Reset()
    {
        for (var index = 0; index < rails.Length; index++)
        {
            rails[index].Reset();
        }

        recentRail.Reset();
    }

    public static float ShelfHeight(float width, float scale) =>
        ShelfHeight(width, scale, Typography.LineHeight(TextStyles.Title3), PosterTile.TextBlock(scale));

    public static float ShelfHeight(float width, float scale, float titleHeight, float textBlock)
    {
        var tile = PosterTile.Width(width, TileGap * scale, scale);
        return titleHeight + TitleGap * scale + PosterTile.Height(tile, textBlock) + RailPad * scale * 2f;
    }

    public static float RowWidth(int count, float tileWidth, float gap) =>
        count <= 0 ? 0f : count * tileWidth + (count - 1) * gap;

    public static float RailContentWidth(int count, float tileWidth, float gap, float pad) =>
        count <= 0 ? 0f : RowWidth(count, tileWidth, gap) + pad * 2f;

    public float Draw(ImDrawListPtr drawList, AppSkin ui, IStripFloor floor, Vector2 origin, float width, float phase,
        float deltaSeconds, bool live, float scale, out StripEntry tapped, out Rect source)
    {
        tapped = default;
        source = default;
        var top = origin.Y;
        for (var shelfIndex = 0; shelfIndex < StripCatalog.Shelves.Length; shelfIndex++)
        {
            var shelf = StripCatalog.Shelves[shelfIndex];
            if (shelfIndex == 0)
            {
                UiAnchors.Report("casino.shelves",
                    new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + ShelfHeight(width, scale))));
            }

            top = DrawRail(drawList, ui, floor, new Vector2(origin.X, top), width, Loc.T(StripCatalog.TitleOf(shelf)),
                StripCatalog.EntriesOf(shelf), rails[shelfIndex], railIds[shelfIndex], phase, deltaSeconds, live, scale,
                out var railTapped, out var railSource);
            if (railTapped.GameId is not null)
            {
                tapped = railTapped;
                source = railSource;
            }

            top += ShelfGap * scale;
        }

        return top - ShelfGap * scale;
    }

    public float DrawRecent(ImDrawListPtr drawList, AppSkin ui, IStripFloor floor, Vector2 origin, float width,
        float phase, float deltaSeconds, bool live, float scale, ReadOnlySpan<StripEntry> entries,
        out StripEntry tapped, out Rect source) =>
        DrawRail(drawList, ui, floor, origin, width, Loc.T(L.Floor.JumpBackIn), entries, recentRail, RecentRailId,
            phase, deltaSeconds, live, scale, out tapped, out source);

    private float DrawRail(ImDrawListPtr drawList, AppSkin ui, IStripFloor floor, Vector2 origin, float width,
        string title, ReadOnlySpan<StripEntry> entries, TileRail rail, string railId, float phase, float deltaSeconds,
        bool live, float scale, out StripEntry tapped, out Rect source)
    {
        tapped = default;
        source = default;
        var gap = TileGap * scale;
        var tileWidth = PosterTile.Width(width, gap, scale);
        var tileHeight = PosterTile.Height(tileWidth, PosterTile.TextBlock(scale));
        Typography.Draw(drawList, origin, Typography.FitText(title, width, TextStyles.Title3), CasinoColors.InkTitle,
            TextStyles.Title3);
        var top = origin.Y + Typography.LineHeight(TextStyles.Title3) + TitleGap * scale;
        var pad = RailPad * scale;
        var contentWidth = RailContentWidth(entries.Length, tileWidth, gap, pad);
        var row = new Rect(new Vector2(origin.X - pad, top - pad),
            new Vector2(origin.X + width + pad, top + tileHeight + pad));
        rail.Begin(drawList, railId, row, row, contentWidth);
        var interactive = rail.TapAllowed;
        for (var index = 0; index < entries.Length; index++)
        {
            var left = origin.X + index * (tileWidth + gap) - rail.Offset;
            if (left > row.Max.X || left + tileWidth < row.Min.X)
            {
                continue;
            }

            var tile = new Rect(new Vector2(left, top), new Vector2(left + tileWidth, top + tileHeight));
            using (ImRaii.PushId(railId))
            using (ImRaii.PushId(index))
            {
                if (DrawEntry(drawList, floor, entries[index], tile, phase, deltaSeconds, live, interactive, scale,
                        out var pressed))
                {
                    tapped = entries[index];
                    source = pressed;
                }
            }
        }

        rail.End(drawList, row, contentWidth, ui, tileWidth + gap);
        return top + tileHeight;
    }

    private bool DrawEntry(ImDrawListPtr drawList, IStripFloor floor, in StripEntry entry, Rect tile, float phase,
        float deltaSeconds, bool live, bool interactive, float scale, out Rect pressed)
    {
        var info = floor.Describe(entry);
        var tint = CasinoArt.TintOf(entry.GameId);
        var id = ImGui.GetID("tile");
        var hovered = CasinoArt.PressCard(id, tile.Min, tile.Max, out var pressedMin, out var pressedMax, interactive);
        pressed = new Rect(pressedMin, pressedMax);
        PosterTile.DrawFrame(drawList, pressed, tint, scale);
        var idle = entry.LiveIdle ? floor.IdleFor(entry.GameId) : null;
        if (idle is not null)
        {
            var preview = previews[Math.Max(0, Machines.MachineCabinet.IndexOf(entry.GameId))];
            preview.Prepare(idle);
            var art = new Rect(pressed.Min, new Vector2(pressed.Max.X, pressed.Max.Y - PosterTile.TextBlock(scale)));
            preview.Draw(drawList, art, Metrics.Radius.Widget * scale, tint, live, hovered, deltaSeconds, scale);
            PosterTile.DrawSignScrim(drawList, pressed, scale);
        }
        else
        {
            PosterTile.DrawGlyph(drawList, pressed, entry.GameId, scale);
        }

        PosterTile.DrawSign(drawList, pressed, entry.Sign, phase, scale, PosterTile.SignBandTop(scale));
        PosterTile.DrawText(drawList, pressed, Loc.T(entry.Title), info, scale);
        PosterTile.DrawCrowd(drawList, pressed, info.Crowd, scale);
        PosterTile.DrawBadge(drawList, pressed, info.Badge, scale);
        if (!info.Open)
        {
            PosterTile.DrawClosed(drawList, pressed, scale);
        }

        PosterTile.DrawRim(drawList, pressed, tint, hovered, scale);
        return interactive && UiInteract.Click(tile.Min, tile.Max, hovered);
    }
}
