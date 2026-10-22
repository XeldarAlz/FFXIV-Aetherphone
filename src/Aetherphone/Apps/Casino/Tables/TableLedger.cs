using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal readonly record struct LedgerRowView(string Name, string BuyIns, string Stack, string Net, string Hands,
    long NetValue, bool Seated);

internal readonly record struct LedgerPlayer(string UserId, string DisplayName);

internal sealed class TableLedger
{
    public static readonly Vector4 Amber = new(1f, 0.71f, 0.22f, 1f);

    internal static readonly Vector4 SettledInk = new(0.36f, 0.84f, 0.52f, 1f);

    private static readonly LocString[] ColumnLabels =
        { L.Tables.ColumnBuyIns, L.Tables.ColumnStack, L.Tables.ColumnNet };

    private const float Pad = 14f;
    private const float FieldHeight = 38f;
    private const float SegmentHeight = 34f;
    private const int AmountDigits = 11;

    private readonly CasinoTablesStore tables;
    private readonly CasinoStore chips;
    private readonly ConfirmService confirm;
    private readonly CasinoTradeSync trade;
    private readonly ChipRail playerRail = new();
    private readonly LedgerEntryRows entries = new();
    private readonly GilLedgerSummary summary = new("casino.ledger.tradeSync");
    private readonly ActionSheet entryMenu = new();
    private readonly ActionSheet.Item[] entryItems = new ActionSheet.Item[1];
    private readonly string[] kindOptions = new string[2];

    private CasinoTableLedgerDto? viewSource;
    private LanguageInfo? viewLanguage;
    private string totalsText = string.Empty;
    private string owedNote = string.Empty;
    private string viewMe = string.Empty;
    private bool viewGuests;
    private LedgerRowView[] rows = Array.Empty<LedgerRowView>();
    private string[] playerIds = Array.Empty<string>();
    private string[] choiceNames = Array.Empty<string>();
    private bool[] choiceActive = Array.Empty<bool>();
    private string roomId = string.Empty;
    private string amountBuffer = string.Empty;
    private string guestBuffer = string.Empty;
    private int proposeKind;
    private int playerIndex;
    private bool handEntryOpen;
    private string tableName = string.Empty;
    private CasinoTableRowDto? tableNameSource;
    private LanguageInfo? tableNameLanguage;
    private LedgerEntryView menuEntry;
    private string menuTitle = string.Empty;

    public TableLedger(CasinoTablesStore tables, CasinoStore chips, ConfirmService confirm, CasinoTradeSync trade)
    {
        this.tables = tables;
        this.chips = chips;
        this.confirm = confirm;
        this.trade = trade;
    }

    public void Enter(string tableId)
    {
        roomId = tableId;
        amountBuffer = string.Empty;
        guestBuffer = string.Empty;
        proposeKind = 0;
        playerIndex = 0;
        handEntryOpen = false;
        viewSource = null;
        entryMenu.Close();
        playerRail.Reset();
        tables.RefreshLedgerNow(tableId);
    }

    public void Reset()
    {
        roomId = string.Empty;
        viewSource = null;
        amountBuffer = string.Empty;
        guestBuffer = string.Empty;
        handEntryOpen = false;
        entryMenu.Close();
        tables.ForgetBookLedgerAdmission();
    }

    internal static LocString TitleFor(CasinoTableRowDto? card) =>
        card is not null && CasinoCurrencies.Of(card) == CasinoCurrencies.Gil
            ? L.Tables.GilLedgerHeading
            : L.Tables.LedgerHeading;

    public void Gate() => entryMenu.Gate();

    public void DrawOverlay(Rect screen, AppSkin ui, bool active)
    {
        if (!active)
        {
            if (entryMenu.IsOpen)
            {
                entryMenu.Close();
            }

            return;
        }

        if ((!entryMenu.IsOpen && !entryMenu.CapturesPointer) || menuEntry.Option == LedgerEntryOption.None)
        {
            return;
        }

        entryItems[0] = new ActionSheet.Item(LedgerEntryRows.OptionLabel(menuEntry.Option),
            Danger: menuEntry.Option == LedgerEntryOption.Remove);
        var picked = entryMenu.Draw(screen, ActionSheetStyle.From(ui), entryItems, Loc.T(L.Common.Cancel), false,
            menuTitle);
        if (picked < 0 || tables.IntentInFlight)
        {
            return;
        }

        switch (menuEntry.Option)
        {
            case LedgerEntryOption.MarkPaid:
                tables.RequestSettle(roomId, menuEntry.EntryId);
                break;
            case LedgerEntryOption.MarkUnpaid:
                tables.RequestRemove(roomId, menuEntry.EntryId);
                break;
            case LedgerEntryOption.Remove:
                AskRemove(menuEntry.EntryId);
                break;
        }
    }

    public void Draw(AppSkin ui, string myUserId, float scale)
    {
        if (roomId.Length == 0)
        {
            return;
        }

        tables.EnsureLedgerFresh(roomId);
        tables.AttendTable(roomId);
        if (tables.TakeIntentFailure())
        {
            Toast(new CasinoStakeOutcome(false, CasinoReasons.Unreachable));
        }

        Toast(tables.TakeNoticeOutcome());
        var ledger = tables.LedgerFor(roomId);
        if (ledger is null)
        {
            Note(ui, Loc.T(L.Tables.LedgerLoading), ui.MutedInk, scale);
            return;
        }

        Refresh(ledger, myUserId, chips.HasFeature(CasinoFeatures.VenueBook));
        var card = tables.CardFor(roomId);
        var gameKind = card?.GameKind ?? string.Empty;
        var book = CasinoBookRules.IsBook(gameKind);
        var seatless = book || VenueKinds.IsVenue(gameKind);
        var gil = ledger.Currency == CasinoCurrencies.Gil;
        DrawHeading(ui, ledger, TableName(card), scale);
        if (gil)
        {
            DrawGil(ui, ledger, myUserId, book, scale);
        }

        if (seatless)
        {
            return;
        }

        if (gil)
        {
            ui.SectionHeading(Loc.T(L.Tables.LedgerHeading), Metrics.Space.Lg);
        }

        DrawRows(ui, ledger.Currency, scale);
    }

    private string TableName(CasinoTableRowDto? card)
    {
        if (ReferenceEquals(card, tableNameSource) && ReferenceEquals(tableNameLanguage, Loc.Current))
        {
            return tableName;
        }

        tableNameSource = card;
        tableNameLanguage = Loc.Current;
        tableName = card is null ? string.Empty : TableRowViews.NameOf(card);
        return tableName;
    }

    private void DrawGil(AppSkin ui, CasinoTableLedgerDto ledger, string myUserId, bool book, float scale)
    {
        Note(ui, Loc.T(ledger.Owner ? L.HostDesk.LedgerIntro : L.Tables.PlayerLedgerHint), ui.MutedInk, scale);
        if (ledger.Owner)
        {
            TradeSyncCard.Draw(ui, trade, scale, false);
            DrawSummary(ui, ledger, myUserId, scale);
        }
        else
        {
            if (totalsText.Length > 0)
            {
                Note(ui, totalsText, ui.BodyInk, scale);
            }

            if (owedNote.Length > 0)
            {
                Note(ui, owedNote, Amber, scale);
            }
        }

        DrawEntries(ui, ledger.Owner, scale);
        if (ledger.Owner)
        {
            DrawHandEntry(ui, book, scale);
        }
    }

    private void DrawSummary(AppSkin ui, CasinoTableLedgerDto ledger, string myUserId, float scale)
    {
        summary.Refresh(ledger, myUserId);
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = GilLedgerSummary.TotalsHeight(scale);
        var frame = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, frame.Min, frame.Max, Metrics.Radius.Grouped * scale);
        summary.DrawTotals(drawList, ui, frame, false, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawHandEntry(AppSkin ui, bool book, float scale)
    {
        if (!ShowsRecord(playerIds.Length, viewGuests))
        {
            return;
        }

        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var label = Loc.T(L.HostDesk.AddByHand);
        var fitted = Typography.FitText(label, width, TextStyles.Footnote);
        var size = Typography.Measure(fitted, TextStyles.Footnote);
        var height = MathF.Max(size.Y, DoorLayout.Touch * scale);
        var textMin = new Vector2(origin.X + (width - size.X) * 0.5f, origin.Y + (height - size.Y) * 0.5f);
        var hitMin = new Vector2(textMin.X, origin.Y);
        var hitMax = new Vector2(textMin.X + size.X, origin.Y + height);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var ink = hovered ? ui.TitleInk : ui.MutedInk;
        Typography.Draw(drawList, textMin, fitted, ink, TextStyles.Footnote);
        drawList.AddLine(new Vector2(textMin.X, textMin.Y + size.Y), new Vector2(textMin.X + size.X, textMin.Y + size.Y),
            ImGui.GetColorU32(ink), Metrics.Stroke.Hairline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(hitMin, hitMax, hovered))
        {
            handEntryOpen = !handEntryOpen;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (handEntryOpen)
        {
            DrawRecord(ui, book, scale);
        }
    }

    public static void Toast(CasinoStakeOutcome? outcome)
    {
        if (outcome is null)
        {
            return;
        }

        ShellToast.Show(outcome.Granted
            ? Loc.T(L.Tables.LedgerUpdated)
            : Loc.T(CasinoReasons.MessageFor(outcome.Reason)));
    }

    internal static string AmountText(long amount, int currency)
    {
        return currency == CasinoCurrencies.Gil
            ? Loc.T(L.Tables.GilAmount, NumberText.Group(amount))
            : NumberText.Group(amount);
    }

    internal static string StateText(CasinoLedgerEntryDto entry)
    {
        return Loc.T(entry.Settled ? L.Tables.EntryRecorded : L.Tables.EntryOwed);
    }

    internal static string OwedNoteFor(CasinoLedgerEntryDto[]? entries)
    {
        var owed = CasinoGilLedger.UnsettledCount(entries);
        return owed > 0 ? Loc.T(L.Tables.OwedNote, owed.ToString(Loc.Culture)) : string.Empty;
    }

    public string CopyText()
    {
        var ledger = tables.LedgerFor(roomId);
        if (ledger is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(Loc.T(L.Tables.LedgerHeading)).Append('\n');
        var source = ledger.Rows ?? Array.Empty<CasinoTableLedgerRowDto>();
        for (var index = 0; index < source.Length; index++)
        {
            var row = source[index];
            builder.Append(Loc.T(L.Tables.CopyRow, row.DisplayName, AmountText(row.BuyIns, ledger.Currency),
                AmountText(row.Stack, ledger.Currency), CasinoTextCache.SignedText(row.Net),
                row.Hands.ToString(Loc.Culture))).Append('\n');
        }

        var gil = ledger.Entries ?? Array.Empty<CasinoLedgerEntryDto>();
        for (var index = 0; index < gil.Length; index++)
        {
            builder.Append(EntryLine(gil[index], ledger.Currency)).Append('\n');
        }

        var totals = GilLedgerTotals.Of(gil, viewMe);
        if (totals.Settled > 0)
        {
            builder.Append(TotalsText(totals)).Append('\n');
        }

        return builder.ToString();
    }

    internal static string EntryLine(CasinoLedgerEntryDto entry, int currency)
    {
        return Loc.T(L.Tables.CopyEntry, Loc.T(CasinoGilLedger.KindLabel(entry.Kind)), SideName(entry,
            entry.PayerUserId, entry.PayerName), SideName(entry, entry.PayeeUserId, entry.PayeeName),
            AmountText(entry.Amount, currency), StateText(entry));
    }

    internal static string SideName(CasinoLedgerEntryDto entry, string userId, string name)
    {
        return entry.Guest && userId.Length == 0 ? Loc.T(L.Book.GuestNamed, name) : name;
    }

    internal static string TotalsText(in GilLedgerTotals totals)
    {
        return Loc.T(L.Book.NightTotals, NumberText.Group(totals.In), NumberText.Group(totals.Out));
    }

    private void Refresh(CasinoTableLedgerDto ledger, string myUserId, bool guestsOpen)
    {
        if (ReferenceEquals(ledger, viewSource) && ReferenceEquals(viewLanguage, Loc.Current)
            && string.Equals(myUserId, viewMe, StringComparison.Ordinal) && guestsOpen == viewGuests)
        {
            return;
        }

        viewSource = ledger;
        viewLanguage = Loc.Current;
        viewMe = myUserId;
        viewGuests = guestsOpen;
        kindOptions[0] = Loc.T(L.Tables.KindBuyIn);
        kindOptions[1] = Loc.T(L.Tables.KindPayout);
        var source = ledger.Rows ?? Array.Empty<CasinoTableLedgerRowDto>();
        var gil = ledger.Entries ?? Array.Empty<CasinoLedgerEntryDto>();
        rows = new LedgerRowView[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var row = source[index];
            rows[index] = new LedgerRowView(row.DisplayName, NumberText.Compact(row.BuyIns),
                NumberText.Compact(row.Stack), SignedCompact(row.Net), row.Hands.ToString(Loc.Culture), row.Net,
                row.Seated);
        }

        var players = Players(source, gil, myUserId);
        playerIds = new string[players.Count];
        var choices = ChoiceCount(players.Count, guestsOpen);
        choiceNames = new string[choices];
        choiceActive = new bool[choices];
        for (var index = 0; index < players.Count; index++)
        {
            playerIds[index] = players[index].UserId;
            choiceNames[index] = players[index].DisplayName;
        }

        if (guestsOpen)
        {
            choiceNames[players.Count] = Loc.T(L.Book.GuestTag);
        }

        playerIndex = Math.Clamp(playerIndex, 0, Math.Max(0, choices - 1));
        entries.Refresh(gil, myUserId, ledger.Owner, ledger.Currency);
        owedNote = OwedNoteFor(gil);
        var totals = GilLedgerTotals.Of(gil, myUserId);
        totalsText = totals.Settled > 0 ? TotalsText(totals) : string.Empty;
    }

    internal static List<LedgerPlayer> Players(CasinoTableLedgerRowDto[] rows, CasinoLedgerEntryDto[] entries,
        string myUserId)
    {
        var players = new List<LedgerPlayer>();
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].Seated || rows[index].BuyIns > 0)
            {
                AddPlayer(players, rows[index].UserId, rows[index].DisplayName, myUserId);
            }
        }

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            AddPlayer(players, entry.PayerUserId, entry.PayerName, myUserId);
            AddPlayer(players, entry.PayeeUserId, entry.PayeeName, myUserId);
        }

        return players;
    }

    private static void AddPlayer(List<LedgerPlayer> players, string userId, string displayName, string myUserId)
    {
        if (userId.Length == 0 || string.Equals(userId, myUserId, StringComparison.Ordinal))
        {
            return;
        }

        for (var index = 0; index < players.Count; index++)
        {
            if (string.Equals(players[index].UserId, userId, StringComparison.Ordinal))
            {
                return;
            }
        }

        players.Add(new LedgerPlayer(userId, displayName));
    }

    internal static bool CanCopy(CasinoTableLedgerDto ledger)
    {
        return (ledger.Rows?.Length ?? 0) > 0 || (ledger.Entries?.Length ?? 0) > 0;
    }

    private void DrawHeading(AppSkin ui, CasinoTableLedgerDto ledger, string title, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var buttonHeight = Button.LargeHeight * scale;
        var height = MathF.Max(titleHeight, buttonHeight);
        var copyLabel = Loc.T(L.Tables.CopyLedger);
        var buttonWidth = Button.WidthFor(copyLabel, ButtonSize.Large);
        var refreshRadius = RoundButton.SmallRadius * scale;
        var refreshReserve = refreshRadius * 2f + Metrics.Space.Sm * scale;
        var top = origin.Y + Metrics.Space.Lg * scale;
        Typography.Draw(drawList, new Vector2(origin.X, top + (height - titleHeight) * 0.5f),
            Typography.FitText(title, MathF.Max(1f, width - buttonWidth - refreshReserve - Metrics.Space.Md * scale),
                TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
        var buttonTop = top + (height - buttonHeight) * 0.5f;
        var rect = new Rect(new Vector2(origin.X + width - buttonWidth, buttonTop),
            new Vector2(origin.X + width, buttonTop + buttonHeight));
        if (Button.Draw(drawList, rect, copyLabel, ui.Ink, ButtonStyle.Tinted, enabled: CanCopy(ledger),
                id: "ledger.copy"))
        {
            ImGui.SetClipboardText(CopyText());
            ShellToast.Show();
        }

        var refreshCenter = new Vector2(rect.Min.X - Metrics.Space.Sm * scale - refreshRadius,
            top + height * 0.5f);
        if (RoundButton.Icon(drawList, refreshCenter, refreshRadius, PhoneIcons.Refresh, ui.Ink,
                tooltip: Loc.T(L.Common.Refresh)))
        {
            tables.RefreshLedgerNow(roomId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, top - origin.Y + height + Metrics.Space.Sm * scale));
    }

    private void DrawRows(AppSkin ui, int currency, float scale)
    {
        if (rows.Length == 0)
        {
            Note(ui, Loc.T(L.Tables.LedgerEmpty), ui.MutedInk, scale);
            return;
        }

        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var headerHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized) + Pad * scale;
        var rowHeight = MathF.Max(DoorLayout.Touch * scale,
            Typography.LineHeight(TextStyles.SubheadlineEmphasized) + Pad * scale);
        var max = new Vector2(origin.X + width, origin.Y + headerHeight + rowHeight * rows.Length);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var number = DoorLayout.LedgerNumberWidth(width, scale);
        var nameRight = DoorLayout.LedgerNameRight(origin.X, width, scale);
        var pad = Pad * scale;
        var headerTop = origin.Y + Pad * 0.5f * scale;
        HeaderCell(drawList, ui, Loc.T(L.Tables.ColumnPlayer), origin.X + pad, nameRight, headerTop, false);
        for (var column = 0; column < 3; column++)
        {
            var right = DoorLayout.LedgerColumnRight(origin.X, width, column, scale);
            HeaderCell(drawList, ui, Loc.T(ColumnLabels[column]), right - number, right, headerTop, true);
        }

        var tableTop = origin.Y + headerHeight;
        drawList.AddLine(new Vector2(origin.X + pad, tableTop), new Vector2(max.X - pad, tableTop),
            ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
        var tint = TableRow.CurrencyTint(currency, ui.Accent);
        for (var index = 0; index < rows.Length; index++)
        {
            var view = rows[index];
            var top = tableTop + index * rowHeight;
            if (index > 0)
            {
                drawList.AddLine(new Vector2(origin.X + pad, top), new Vector2(max.X, top),
                    ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            }

            var centerY = top + rowHeight * 0.5f;
            var nameInk = view.Seated ? ui.TitleInk : ui.BodyInk;
            Cell(drawList, view.Name, origin.X + pad, nameRight, centerY, nameInk, TextStyles.SubheadlineEmphasized,
                false);
            Cell(drawList, view.BuyIns, DoorLayout.LedgerColumnRight(origin.X, width, 0, scale) - number,
                DoorLayout.LedgerColumnRight(origin.X, width, 0, scale), centerY, ui.BodyInk, TextStyles.Subheadline,
                true);
            Cell(drawList, view.Stack, DoorLayout.LedgerColumnRight(origin.X, width, 1, scale) - number,
                DoorLayout.LedgerColumnRight(origin.X, width, 1, scale), centerY, tint, TextStyles.Subheadline, true);
            var netInk = view.NetValue > 0 ? SettledInk : ui.MutedInk;
            Cell(drawList, view.Net, DoorLayout.LedgerColumnRight(origin.X, width, 2, scale) - number,
                DoorLayout.LedgerColumnRight(origin.X, width, 2, scale), centerY, netInk,
                TextStyles.SubheadlineEmphasized, true);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, max.Y - origin.Y + Metrics.Space.Sm * scale));
    }

    private static void HeaderCell(ImDrawListPtr drawList, AppSkin ui, string label, float left, float right,
        float top, bool alignRight)
    {
        var fitted = Typography.FitText(label, MathF.Max(1f, right - left), TextStyles.FootnoteEmphasized);
        var x = alignRight ? right - Typography.Measure(fitted, TextStyles.FootnoteEmphasized).X : left;
        Typography.Draw(drawList, new Vector2(x, top), fitted, ui.MutedInk, TextStyles.FootnoteEmphasized);
    }

    private static void Cell(ImDrawListPtr drawList, string text, float left, float right, float centerY, Vector4 ink,
        in TextStyle style, bool alignRight)
    {
        var fitted = Typography.FitText(text, MathF.Max(1f, right - left), style);
        var size = Typography.Measure(fitted, style);
        var x = alignRight ? right - size.X : left;
        Typography.Draw(drawList, new Vector2(x, centerY - Typography.LineHeight(style) * 0.5f), fitted, ink, style);
    }

    internal static string SignedCompact(long value) =>
        value > 0 ? Loc.T(L.Stage.Plus, NumberText.Compact(value)) : NumberText.Compact(value);

    private void DrawEntries(AppSkin ui, bool owner, float scale)
    {
        if (entries.Count == 0)
        {
            Note(ui, Loc.T(L.Tables.EntriesEmpty), ui.MutedInk, scale);
            return;
        }

        var tapped = entries.Draw(ui, owner && !tables.IntentInFlight, scale);
        if (tapped < 0)
        {
            return;
        }

        menuEntry = entries[tapped];
        menuTitle = Loc.T(L.HostDesk.EntryMeta, menuEntry.Name, menuEntry.Amount);
        entryMenu.Open();
    }

    internal static bool BarsReader(CasinoTableRowDto card, bool owner)
    {
        return !owner && !card.Admitted;
    }

    internal static bool ShowsRecord(int players, bool guestsOpen)
    {
        return players > 0 || guestsOpen;
    }

    internal static int ChoiceCount(int players, bool guestsOpen)
    {
        return players + (guestsOpen ? 1 : 0);
    }

    internal static bool IsGuestChoice(int index, int players, bool guestsOpen)
    {
        return guestsOpen && index >= players;
    }

    private void DrawRecord(AppSkin ui, bool book, float scale)
    {
        ui.SectionHeading(Loc.T(L.Tables.RecordHeading), Metrics.Space.Sm);
        var width = ScrollLayout.StableContentWidth();
        for (var index = 0; index < choiceActive.Length; index++)
        {
            choiceActive[index] = index == playerIndex;
        }

        var tapped = playerRail.Draw(ui, choiceNames, choiceActive);
        if (tapped >= 0)
        {
            playerIndex = tapped;
        }

        var segment = ImGui.GetCursorScreenPos();
        var segmentRect = new Rect(segment, new Vector2(segment.X + width, segment.Y + SegmentHeight * scale));
        proposeKind = SegmentStrip.Draw("##ledgerKind", segmentRect, kindOptions, proposeKind, ui.Palette);
        ImGui.Dummy(new Vector2(width, SegmentHeight * scale + Metrics.Space.Sm * scale));
        var guest = IsGuestChoice(playerIndex, playerIds.Length, viewGuests);
        if (guest)
        {
            DrawGuestName(ui, width, scale);
        }

        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var buttonLabel = Loc.T(L.Tables.Record);
        var buttonWidth = Button.WidthFor(buttonLabel, ButtonSize.Regular);
        var field = new Rect(origin,
            new Vector2(origin.X + width - buttonWidth - Metrics.Space.Sm * scale, origin.Y + FieldHeight * scale));
        SearchBar.Surface(drawList, field, ui.Ink);
        var capsule = SearchBar.Capsule(field);
        var inset = capsule.Height * 0.4f;
        ImGui.SetCursorScreenPos(new Vector2(capsule.Min.X + inset, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(capsule.Width - inset * 2f);
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##ledgerAmount", Loc.T(L.Tables.AmountGilHint), ref amountBuffer,
                AmountDigits + 1, ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }

        var amount = Venue.VenueFields.Parse(amountBuffer);
        var buttonHeight = Button.RegularHeight * scale;
        var buttonTop = field.Center.Y - buttonHeight * 0.5f;
        var rect = new Rect(new Vector2(origin.X + width - buttonWidth, buttonTop),
            new Vector2(origin.X + width, buttonTop + buttonHeight));
        var ready = amount > 0 && amount <= CasinoHostingRules.MaxGil && !tables.IntentInFlight
                    && (!guest || CasinoBookRules.HasGuestName(guestBuffer));
        if (Button.Draw(drawList, rect, buttonLabel, ui.Ink, ButtonStyle.Prominent, enabled: ready, id: "ledger.record"))
        {
            Record(amount, guest);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, FieldHeight * scale + Metrics.Space.Sm * scale));
        Note(ui, Loc.T(book ? L.Book.RecordHostHint : L.Tables.RecordHostHint), ui.MutedInk, scale);
    }

    private void DrawGuestName(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + FieldHeight * scale));
        SearchBar.Surface(drawList, field, ui.Ink);
        var capsule = SearchBar.Capsule(field);
        var inset = capsule.Height * 0.4f;
        ImGui.SetCursorScreenPos(new Vector2(capsule.Min.X + inset, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(capsule.Width - inset * 2f);
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##ledgerGuest", Loc.T(L.Book.GuestNameHint), ref guestBuffer,
                CasinoHostingRules.GuestNameMaxLength + 1, ImGuiInputTextFlags.None);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, FieldHeight * scale + Metrics.Space.Sm * scale));
    }

    private void Record(long amount, bool guest)
    {
        var kind = proposeKind == 0 ? CasinoLedgerKinds.BuyIn : CasinoLedgerKinds.Payout;
        if (guest)
        {
            tables.RecordEntry(roomId, kind, string.Empty, guestBuffer, amount);
            amountBuffer = string.Empty;
            return;
        }

        if (playerIndex >= playerIds.Length)
        {
            return;
        }

        tables.RecordEntry(roomId, kind, playerIds[playerIndex], string.Empty, amount);
        amountBuffer = string.Empty;
    }

    private void AskRemove(string entryId)
    {
        var targetRoom = roomId;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Tables.RemoveTitle),
            Message = Loc.T(L.Tables.RemoveBody),
            ConfirmLabel = Loc.T(L.Tables.RemoveEntry),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => tables.RequestRemove(targetRoom, entryId),
        });
    }

    internal static void Note(AppSkin ui, string text, Vector4 ink, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + Metrics.Space.Xs * scale, origin.Y), text, ink,
            TextStyles.Footnote, width - Metrics.Space.Xs * 2f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }
}
