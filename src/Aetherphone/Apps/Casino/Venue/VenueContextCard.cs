using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal readonly record struct VenueContextActions(
    Action<string> OpenTable,
    Action<string> OpenDoor,
    Action<string> OpenLedger,
    Action HostAgain,
    Action HostNew);

internal sealed class VenueContextCard
{
    private const float RimAlpha = 0.40f;
    private const float AccentFillAlpha = 0.14f;
    private const float AccentRimAlpha = 0.45f;
    private const string PendingLetter = "·";

    private readonly CasinoTablesStore tables;
    private readonly CasinoStore chips;
    private readonly CasinoPreferences preferences;
    private readonly NearbyTablesCard nearby;
    private readonly VenueContextActions actions;
    private readonly Action<CasinoTableRowDto> openNearby;
    private readonly string doorId;
    private readonly string ledgerId;
    private readonly string hostId;
    private readonly string newTableId;
    private readonly string[] letters = new string[CasinoRoomCodes.Length];

    private CasinoTableRowDto? ownRow;
    private CasinoTableLedgerDto? ownLedger;
    private TableRowView ownView;
    private string ownLine = string.Empty;
    private string ownCurrencyLine = string.Empty;
    private LanguageInfo? ownLanguage;
    private string lettersOf = string.Empty;
    private CasinoHostDraftMemory? summaryOf;
    private bool summaryGilOpen;
    private LanguageInfo? summaryLanguage;
    private string summary = string.Empty;

    public VenueContextCard(string id, CasinoTablesStore tables, CasinoStore chips, CasinoPreferences preferences,
        NearbyTablesCard nearby, in VenueContextActions actions)
    {
        this.tables = tables;
        this.chips = chips;
        this.preferences = preferences;
        this.nearby = nearby;
        this.actions = actions;
        openNearby = row => this.actions.OpenTable(row.TableId);
        doorId = id + ".door";
        ledgerId = id + ".ledger";
        hostId = id + ".host";
        newTableId = id + ".new";
        Array.Fill(letters, PendingLetter);
    }

    public VenueContextKind Kind { get; private set; } = VenueContextKind.HostNew;

    public bool ShowsNearby => Kind == VenueContextKind.AtThisVenue;

    public float Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float scale)
    {
        nearby.Poll();
        var own = OwnTable();
        var hasDraft = preferences.TryLoadHostDraft(out var memory)
                       && HostDraft.Reopens(memory, chips.HasFeature(CasinoFeatures.VenueBook));
        Kind = VenueSectionState.Pick(own is not null, nearby.Count, hasDraft);
        switch (Kind)
        {
            case VenueContextKind.YourTable:
                return DrawOwnTable(drawList, ui, origin, width, scale, own!);
            case VenueContextKind.AtThisVenue:
                return nearby.Draw(drawList, ui, origin, width, openNearby);
            case VenueContextKind.HostAgain:
                return DrawHost(drawList, ui, origin, width, scale, Loc.T(L.VenueHub.HostAgain), Summary(memory!),
                    true);
            default:
                return DrawHost(drawList, ui, origin, width, scale, Loc.T(L.Tables.HostCta),
                    Loc.T(L.Tables.HostCtaLine), false);
        }
    }

    private CasinoTableRowDto? OwnTable()
    {
        var tableId = tables.HostedTableId();
        if (tableId.Length == 0)
        {
            return null;
        }

        var rows = tables.Listed;
        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].TableId, tableId, StringComparison.Ordinal))
            {
                return rows[index];
            }
        }

        return tables.CardFor(tableId);
    }

    private float DrawOwnTable(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float scale,
        CasinoTableRowDto row)
    {
        RefreshOwn(row);
        var book = ownView.Book;
        var textWidth = VenueContextLayout.TextWidth(width, scale);
        var layout = VenueContextLayout.Table(origin, width, scale,
            PitText.Height(ownView.Name, TextStyles.Headline, textWidth),
            PitText.Height(ownLine, TextStyles.Footnote, textWidth),
            PitText.Height(ownCurrencyLine, TextStyles.Footnote, textWidth), book);
        var card = layout.Card;
        var radius = Metrics.Radius.Grouped * scale;
        var overButton = UiInteract.Hover(layout.Door.Min, layout.Door.Max)
                         || (!book && UiInteract.Hover(layout.Ledger.Min, layout.Ledger.Max));
        var hovered = !overButton && UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, radius);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, RimAlpha)),
            1.2f * scale);
        DrawArt(drawList, layout.Art);
        PitText.Draw(drawList, ownView.Name, TextStyles.Headline, ui.TitleInk, layout.TextLeft, layout.NameTop,
            layout.TextWidth, false);
        PitText.Draw(drawList, ownLine, TextStyles.Footnote, ui.BodyInk, layout.TextLeft, layout.SeatsTop,
            layout.TextWidth, false);
        PitText.Draw(drawList, ownCurrencyLine, TextStyles.Footnote, TableRow.CurrencyTint(ownView.Currency, ui.Accent),
            layout.TextLeft, layout.CurrencyTop, layout.TextWidth, false);
        DrawCode(drawList, ui, layout, CodeOf(row), scale);
        DrawButtons(drawList, ui, layout, row.TableId, book);

        if (!overButton && UiInteract.Click(card.Min, card.Max, hovered))
        {
            actions.OpenTable(row.TableId);
        }

        return card.Max.Y;
    }

    private void DrawButtons(ImDrawListPtr drawList, AppSkin ui, in VenueTableCardLayout layout, string tableId,
        bool book)
    {
        if (book)
        {
            if (Button.Draw(drawList, layout.Door, Loc.T(L.Book.OpenLedger), ui.Ink, ButtonStyle.Prominent,
                    id: ledgerId))
            {
                actions.OpenLedger(tableId);
            }

            return;
        }

        if (Button.Draw(drawList, layout.Door, Loc.T(L.VenueHub.Door), ui.Ink, ButtonStyle.Prominent, id: doorId))
        {
            actions.OpenDoor(tableId);
        }

        if (Button.Draw(drawList, layout.Ledger, Loc.T(L.Tables.OpenLedger), ui.Ink, ButtonStyle.Gray, id: ledgerId))
        {
            actions.OpenLedger(tableId);
        }
    }

    private void DrawArt(ImDrawListPtr drawList, in Rect art)
    {
        if (ownView.Book)
        {
            TableRow.BookTile(drawList, art.Center, art.Width);
            return;
        }

        if (ownView.Room != VenueRoomKind.None)
        {
            CasinoArt.IconTileAt(drawList, art.Center, art.Width, HostGames.Of(HostGames.FromVenue(ownView.Room)).Tint,
                TableRow.IconOf(ownView.Room));
            return;
        }

        CasinoArt.GameTile(drawList, ownView.GameId.Length > 0 ? ownView.GameId : CasinoGames.Blackjack, art.Center,
            art.Width);
    }

    private void DrawCode(ImDrawListPtr drawList, AppSkin ui, in VenueTableCardLayout layout, string code, float scale)
    {
        RefreshLetters(code);
        var fill = ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Secondary));
        var rounding = Metrics.Radius.Md * scale;
        for (var index = 0; index < letters.Length; index++)
        {
            var cell = layout.Cell(index);
            Squircle.Fill(drawList, cell.Min, cell.Max, rounding, fill);
            Typography.DrawCentered(drawList, cell.Center, letters[index], ui.TitleInk, TextStyles.Title3);
        }
    }

    private string CodeOf(CasinoTableRowDto row)
    {
        if (row.JoinCode.Length > 0)
        {
            return row.JoinCode;
        }

        return tables.CardFor(row.TableId)?.JoinCode ?? string.Empty;
    }

    private void RefreshLetters(string code)
    {
        if (ReferenceEquals(code, lettersOf))
        {
            return;
        }

        lettersOf = code;
        var hasCode = code.Length == CasinoRoomCodes.Length;
        for (var index = 0; index < letters.Length; index++)
        {
            letters[index] = hasCode ? code.Substring(index, 1) : PendingLetter;
        }
    }

    private void RefreshOwn(CasinoTableRowDto row)
    {
        var ledger = tables.HeldLedger;
        if (ReferenceEquals(row, ownRow) && ReferenceEquals(ledger, ownLedger)
            && ReferenceEquals(ownLanguage, Loc.Current))
        {
            return;
        }

        ownRow = row;
        ownLedger = ledger;
        ownLanguage = Loc.Current;
        var entries = TableBrowser.BookEntriesOf(row, ledger);
        ownView = TableBrowser.ViewOf(row, tables.AccountId, entries);
        ownLine = OwnLineOf(ownView, entries);
        var bank = row.Config?.Bank ?? 0;
        var payout = row.Config?.MaxPayout ?? 0;
        ownCurrencyLine = ownView.Currency == CasinoCurrencies.Gil && bank > 0
            ? Loc.T(L.Tables.GilLimits, NumberText.Compact(bank), NumberText.Compact(payout > 0 ? payout : bank))
            : ownView.Stakes;
    }

    internal static string OwnLineOf(in TableRowView view, int bookEntries)
    {
        if (!view.Book)
        {
            return view.Seats;
        }

        return bookEntries >= 0 ? view.Stakes : Loc.T(L.Book.Plays);
    }

    private float DrawHost(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float scale, string title,
        string line, bool hasDraft)
    {
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var inner = VenueContextLayout.Inner(width, scale);
        var lines = Typography.WrapText(line, TextStyles.Subheadline, inner);
        var layout = VenueContextLayout.Host(origin, width, scale, titleHeight, lines.Length * lineHeight,
            hasDraft ? 2 : 1);
        var card = layout.Card;
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, card.Min, card.Max, radius);
        Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.Accent with { W = AccentFillAlpha }));
        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.Accent with { W = AccentRimAlpha }),
            1.2f * scale);
        Typography.Draw(drawList, layout.Title, Typography.FitText(title, layout.TextWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        var top = layout.Summary.Y;
        for (var index = 0; index < lines.Length; index++)
        {
            Typography.Draw(drawList, new Vector2(layout.Summary.X, top), lines[index], ui.BodyInk,
                TextStyles.Subheadline);
            top += lineHeight;
        }

        if (Button.Draw(drawList, layout.Primary, Loc.T(L.VenueHub.Host), ui.Ink, ButtonStyle.Prominent,
                enabled: !tables.IntentInFlight, id: hostId))
        {
            if (hasDraft)
            {
                actions.HostAgain();
            }
            else
            {
                actions.HostNew();
            }
        }

        if (hasDraft && Button.Draw(drawList, layout.Secondary, Loc.T(L.VenueHub.NewTable), ui.Ink, ButtonStyle.Gray,
                enabled: !tables.IntentInFlight, id: newTableId))
        {
            actions.HostNew();
        }

        return card.Max.Y;
    }

    private string Summary(CasinoHostDraftMemory memory)
    {
        var gilOpen = chips.HasFeature(CasinoFeatures.GilTables);
        if (ReferenceEquals(memory, summaryOf) && gilOpen == summaryGilOpen
            && ReferenceEquals(summaryLanguage, Loc.Current))
        {
            return summary;
        }

        summaryOf = memory;
        summaryGilOpen = gilOpen;
        summaryLanguage = Loc.Current;
        summary = HostDraftSummary.Of(memory, gilOpen);
        return summary;
    }
}
