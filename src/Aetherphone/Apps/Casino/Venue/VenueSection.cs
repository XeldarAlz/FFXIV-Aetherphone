using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Apps.Coin;
using Aetherphone.Apps.Games;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class VenueSection
{
    private const string RailId = "##casino.venue.posters";
    private const float CardGap = 12f;

    private readonly VenueContextCard context;
    private readonly CasinoStore chips;
    private readonly Action<HostGame, bool> hostGame;
    private readonly TileRail rail = new();
    private readonly string[] metas = new string[VenuePosters.Games.Length];
    private string badge = string.Empty;
    private LanguageInfo? language;
    private bool metasGilOpen;

    public VenueSection(VenueContextCard context, CasinoStore chips, Action<HostGame, bool> hostGame)
    {
        this.context = context;
        this.chips = chips;
        this.hostGame = hostGame;
    }

    public void Reset() => rail.Reset();

    public float Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float scale)
    {
        var top = origin.Y + CoinArt.SectionGap * scale;
        top += CardSectionHeader.Draw(drawList, new Vector2(origin.X, top), width, Loc.T(L.VenueHub.Title),
            ui.TitleInk) + CoinArt.HeaderGap * scale;
        top = context.Draw(drawList, ui, new Vector2(origin.X, top), width, scale) + CardGap * scale;
        top = DrawPosters(drawList, ui, new Vector2(origin.X, top), width, scale);
        return top + CardGap * scale;
    }

    private float DrawPosters(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float scale)
    {
        var gilOpen = chips.HasFeature(CasinoFeatures.GilTables);
        RefreshTexts(gilOpen);
        var gap = StripShelves.TileGap * scale;
        var tileWidth = PosterTile.Width(width, gap, scale);
        var tileHeight = PosterTile.Height(tileWidth, PosterTile.TextBlock(scale));
        var games = VenuePosters.Games;
        var count = VenuePosters.Count(chips.HasFeature(CasinoFeatures.VenueBook));
        var pad = StripShelves.RailPad * scale;
        var contentWidth = StripShelves.RailContentWidth(count, tileWidth, gap, pad);
        var row = new Rect(new Vector2(origin.X - pad, origin.Y - pad),
            new Vector2(origin.X + width + pad, origin.Y + tileHeight + pad));
        rail.Begin(drawList, RailId, row, row, contentWidth);
        var interactive = rail.TapAllowed;
        var phase = (float)ImGui.GetTime();
        var features = chips.Features;
        var tapped = -1;
        for (var index = 0; index < count; index++)
        {
            var left = origin.X + index * (tileWidth + gap) - rail.Offset;
            if (left > row.Max.X || left + tileWidth < row.Min.X)
            {
                continue;
            }

            var tile = new Rect(new Vector2(left, origin.Y), new Vector2(left + tileWidth, origin.Y + tileHeight));
            using (ImRaii.PushId(index))
            {
                if (DrawPoster(drawList, games[index], index, tile, features, gilOpen, phase, interactive, scale))
                {
                    tapped = index;
                }
            }
        }

        rail.End(drawList, row, contentWidth, ui, tileWidth + gap);
        if (tapped >= 0)
        {
            hostGame(games[tapped], VenuePosters.PrefersGil(games[tapped], gilOpen));
        }

        return origin.Y + tileHeight;
    }

    private bool DrawPoster(ImDrawListPtr drawList, HostGame game, int index, Rect tile, CasinoFeatureSet features,
        bool gilOpen, float phase, bool interactive, float scale)
    {
        ref readonly var info = ref HostGames.Of(game);
        var hovered = CasinoArt.PressCard(ImGui.GetID("poster"), tile.Min, tile.Max, out var pressedMin,
            out var pressedMax, interactive);
        var pressed = new Rect(pressedMin, pressedMax);
        PosterTile.DrawFrame(drawList, pressed, info.Tint, scale);
        if (info.HasGlyph)
        {
            PosterTile.DrawGlyph(drawList, pressed, info.GameId, scale);
        }
        else
        {
            PosterTile.DrawIcon(drawList, pressed, info.Icon, scale);
        }

        PosterTile.DrawSign(drawList, pressed, info.Sign, phase, scale, PosterTile.SignBandTop(scale));
        var poster = new PosterInfo(CasinoGameGate.IsOpen(features, info.GameId), 0, metas[index], string.Empty,
            VenuePosters.ShowsGilBadge(game, gilOpen) ? badge : string.Empty);
        PosterTile.DrawText(drawList, pressed, Loc.T(info.Title), poster, scale);
        PosterTile.DrawBadge(drawList, pressed, poster.Badge, scale);
        if (!poster.Open)
        {
            PosterTile.DrawClosed(drawList, pressed, scale);
        }

        PosterTile.DrawRim(drawList, pressed, info.Tint, hovered, scale);
        return interactive && UiInteract.Click(tile.Min, tile.Max, hovered);
    }

    private void RefreshTexts(bool gilOpen)
    {
        if (ReferenceEquals(language, Loc.Current) && gilOpen == metasGilOpen && badge.Length > 0)
        {
            return;
        }

        language = Loc.Current;
        metasGilOpen = gilOpen;
        badge = Loc.T(L.VenueHub.GilBadge);
        for (var index = 0; index < metas.Length; index++)
        {
            metas[index] = Loc.T(VenuePosters.MetaOf(VenuePosters.Games[index], gilOpen));
        }
    }
}
