using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float HostSectionGap = 16f;
    private const float HostIntroGap = 2f;

    private readonly Venue.HostTableCard hostTableCard = new();
    private readonly Venue.HostAgainCard hostAgainCard = new();
    private readonly Venue.HostGameGrid hostGames = new();

    private void EnterHostTab()
    {
        casinoTables.RefreshNow();
    }

    private void DrawHostTab(Rect body)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId("casino.host"))
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var gap = HostSectionGap * scale;
            var top = Tables.PitText.Draw(drawList, Loc.T(L.HostDesk.Intro), TextStyles.Subheadline, ui.BodyInk,
                origin.X, origin.Y + HostIntroGap * scale, width, false) + gap;
            var hosted = HostedRow();
            if (hosted is not null)
            {
                top = DrawHostedTable(drawList, hosted, new Vector2(origin.X, top), width, scale) + gap;
            }
            else
            {
                top = DrawHostAgain(drawList, new Vector2(origin.X, top), width, scale);
                top = DrawHostGames(drawList, new Vector2(origin.X, top), width, scale) + gap;
            }

            top = DrawMyLedgerRow(drawList, new Vector2(origin.X, top), width, scale);
            Coin.CoinArt.Reserve(origin, width, top + Coin.CoinArt.BottomPad * scale);
        }
    }

    private CasinoTableRowDto? HostedRow()
    {
        var tableId = casinoTables.HostedTableId();
        if (tableId.Length == 0)
        {
            return null;
        }

        var rows = casinoTables.Listed;
        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].TableId, tableId, StringComparison.Ordinal))
            {
                return rows[index];
            }
        }

        return casinoTables.CardFor(tableId);
    }

    private float DrawHostedTable(ImDrawListPtr drawList, CasinoTableRowDto row, Vector2 origin, float width,
        float scale)
    {
        var book = CasinoBookRules.IsBook(row.GameKind);
        casinoTables.EnsureDoorFresh(row.TableId);
        if (book)
        {
            casinoTables.EnsureLedgerFresh(row.TableId);
        }

        var door = casinoTables.DoorFor(row.TableId);
        var ledger = book ? casinoTables.LedgerFor(row.TableId) : null;
        var action = hostTableCard.Draw(drawList, ui, origin, width, row, door, ledger, !router.IsTransitioning,
            scale, out var bottom);
        switch (action)
        {
            case Venue.HostTableAction.GoToTable when book:
                OpenLedger(row.TableId);
                break;
            case Venue.HostTableAction.GoToTable:
                OpenTable(row.TableId);
                break;
            case Venue.HostTableAction.Manage:
                OpenDoor(row.TableId, row.InviteToken);
                break;
        }

        return bottom;
    }

    private float DrawHostAgain(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        if (!preferences.TryLoadHostDraft(out var memory)
            || !Tables.HostDraft.Reopens(memory, casino.HasFeature(CasinoFeatures.VenueBook)))
        {
            return origin.Y;
        }

        if (hostAgainCard.Draw(drawList, ui, origin, width, memory, casino.HasFeature(CasinoFeatures.GilTables),
                !casinoTables.IntentInFlight && !router.IsTransitioning, scale, out var bottom))
        {
            OpenHostDraft();
        }

        return bottom + HostSectionGap * scale;
    }

    private float DrawHostGames(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var gilOpen = casino.HasFeature(CasinoFeatures.GilTables);
        var bottom = hostGames.Draw(drawList, ui, origin, width, casino.Features, gilOpen,
            casino.HasFeature(CasinoFeatures.VenueBook), !router.IsTransitioning, scale, out var tapped);
        if (tapped >= 0)
        {
            var game = Venue.VenuePosters.Games[tapped];
            OpenHostGame(game, Venue.VenuePosters.PrefersGil(game, gilOpen));
        }

        return bottom;
    }

    private float DrawMyLedgerRow(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        if (!casino.HasFeature(CasinoFeatures.HostingV2))
        {
            return origin.Y;
        }

        var line = Loc.T(L.HostDesk.MyLedgerLine);
        if (Tables.LinkRow.Draw(drawList, ui, origin, width, Loc.T(L.Tables.MyLedgerTitle), line, FontAwesomeIcon.Book,
                ui.Accent, true, scale))
        {
            OpenGilLedger();
        }

        return origin.Y + Tables.LinkRow.Height(line, width, scale);
    }
}
