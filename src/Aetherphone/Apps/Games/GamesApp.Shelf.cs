using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string ShelfNavId = "games.shelf.nav";

    private ReadOnlySpan<int> ShelfEntries(GamesShelf shelf) => shelf switch
    {
        GamesShelf.Latest => library.Latest,
        GamesShelf.All => library.Ordered,
        _ => library.Genre((GameGenre)shelf),
    };

    private string ShelfTitle(GamesShelf shelf) => shelf switch
    {
        GamesShelf.Latest => Loc.T(L.Games.ShelfLatest),
        GamesShelf.All => Loc.T(L.Games.LibraryHeading),
        _ => Loc.T(GameGenres.Label((GameGenre)shelf)),
    };

    private void DrawShelfPage(in PhoneContext context, GamesShelf shelf)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        using (ImRaii.PushId("games.shelf"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            entrance = GameJuice.Advance(entrance, frameSeconds, EntranceSpeed);
            var entries = ShelfEntries(shelf);
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var drawList = ImGui.GetWindowDrawList();
            Typography.Draw(drawList, origin, CountLabel(entries.Length), ui.MutedInk, TextStyles.Subheadline);
            var y = origin.Y + Typography.LineHeight(TextStyles.Subheadline) + Metrics.Space.Md * scale;
            var gridTop = y;
            y = DrawGrid(entries, origin.X, y, width, scale);
            GamesHubArt.ReportAnchor("games.shelf", new Rect(new Vector2(origin.X, gridTop),
                new Vector2(origin.X + width, y)));
            FinishPage(origin, width, y, scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, ShelfNavId, ShelfTitle(shelf), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, TabTitle(tab), back);
    }
}
