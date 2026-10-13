using System.Globalization;
using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal enum LedgerTone : byte
{
    Settled,
    Waiting,
    Disputed,
}

internal readonly record struct LedgerRowView(string Name, string BuyIns, string Stack, string Net, string Hands,
    long NetValue, bool Seated);

internal readonly record struct LedgerEntryView(string EntryId, string Title, string Parties, string Amount,
    string State, LedgerTone Tone, bool CanConfirm, string ConfirmLabel, bool CanDispute, string DisputeLabel,
    string Tag = "");

internal sealed class TableLedger
{
    public static readonly Vector4 Amber = new(1f, 0.71f, 0.22f, 1f);

    internal static readonly Vector4 SettledInk = new(0.36f, 0.84f, 0.52f, 1f);

    private static readonly LocString[] ColumnLabels =
        { L.Tables.ColumnBuyIns, L.Tables.ColumnStack, L.Tables.ColumnNet };

    private const float Pad = 14f;
    private const float FieldHeight = 38f;
    private const int AmountDigits = 11;

    private readonly CasinoTablesStore tables;
    private readonly CasinoStore chips;
    private readonly ConfirmService confirm;
    private readonly CasinoTradeSync trade;
    private readonly ChipRail playerRail = new();
    private readonly LedgerEntryCards cards = new();
    private readonly string[] kindOptions = new string[2];

    private CasinoTableLedgerDto? viewSource;
    private LanguageInfo? viewLanguage;
    private TradeMiss? missSource;
    private LanguageInfo? missLanguage;
    private CasinoTableLedgerDto? missLedger;
    private string missText = string.Empty;
    private string missAction = string.Empty;
    private string totalsText = string.Empty;
    private string viewMe = string.Empty;
    private bool viewGuests;
    private LedgerRowView[] rows = Array.Empty<LedgerRowView>();
    private string[] playerNames = Array.Empty<string>();
    private string[] playerIds = Array.Empty<string>();
    private string[] choiceNames = Array.Empty<string>();
    private bool[] choiceActive = Array.Empty<bool>();
    private string waitingNote = string.Empty;
    private string roomId = string.Empty;
    private string amountBuffer = string.Empty;
    private string guestBuffer = string.Empty;
    private int proposeKind;
    private int playerIndex;
    private bool revealRecord;
    private TradeMiss? prefilledMiss;

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
        viewSource = null;
        prefilledMiss = null;
        playerRail.Reset();
        tables.RefreshLedgerNow(tableId);
    }

    public void Reset()
    {
        roomId = string.Empty;
        viewSource = null;
        amountBuffer = string.Empty;
        prefilledMiss = null;
        tables.ForgetLedger();
    }

    public void Draw(AppSkin ui, string myUserId, string hostUserId, float scale)
    {
        if (roomId.Length == 0)
        {
            return;
        }

        tables.EnsureLedgerFresh(roomId);
        if (tables.TakeIntentFailure())
        {
            Toast(new CasinoStakeOutcome(false, CasinoReasons.Unreachable));
        }

        Toast(tables.TakeNoticeOutcome());
        Toast(tables.TakeGuestOutcome());
        var ledger = tables.LedgerFor(roomId);
        if (ledger is null)
        {
            Note(ui, Loc.T(L.Tables.LedgerLoading), ui.MutedInk, scale);
            return;
        }

        Refresh(ledger, myUserId, chips.HasFeature(CasinoFeatures.VenueBook));
        var gameKind = tables.CardFor(roomId)?.GameKind ?? string.Empty;
        var seatless = CasinoBookRules.IsBook(gameKind) || VenueKinds.IsVenue(gameKind);
        DrawHeading(ui, ledger, seatless, scale);
        if (!seatless)
        {
            DrawRows(ui, ledger.Currency, scale);
        }

        if (ledger.Currency != CasinoCurrencies.Gil)
        {
            return;
        }

        ui.SectionHeading(Loc.T(L.Tables.GilLedgerHeading), Metrics.Space.Md);
        TradeSyncCard.Draw(ui, trade, scale, false, ledger.Owner);
        DrawMiss(ui, ledger, scale);
        if (waitingNote.Length > 0)
        {
            Note(ui, waitingNote, Amber, scale);
        }

        DrawEntries(ui, scale);
        DrawPropose(ui, ledger, myUserId, hostUserId, scale);
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

    internal static LedgerTone ToneOf(CasinoLedgerEntryDto entry)
    {
        if (entry.Disputed && !entry.DisputeResolved && !entry.Settled)
        {
            return LedgerTone.Disputed;
        }

        return entry.Settled ? LedgerTone.Settled : LedgerTone.Waiting;
    }

    internal static LocString KindLabel(string kind) => kind switch
    {
        CasinoLedgerKinds.BuyIn => L.Tables.KindBuyIn,
        CasinoLedgerKinds.Rebuy => L.Tables.KindRebuy,
        CasinoLedgerKinds.Payout => L.Tables.KindPayout,
        CasinoLedgerKinds.CashOut => L.Tables.KindCashOut,
        CasinoLedgerKinds.Stake => L.Tables.KindStake,
        CasinoLedgerKinds.Ticket => L.Tables.KindTicket,
        CasinoLedgerKinds.Prize => L.Tables.KindPrize,
        _ => L.Tables.KindOther,
    };

    internal static string AmountText(long amount, int currency)
    {
        return currency == CasinoCurrencies.Gil
            ? Loc.T(L.Tables.GilAmount, NumberText.Group(amount))
            : NumberText.Group(amount);
    }

    internal static string StateText(CasinoLedgerEntryDto entry)
    {
        if (entry.Guest)
        {
            return Loc.T(L.Book.GuestRecorded);
        }

        switch (ToneOf(entry))
        {
            case LedgerTone.Settled:
                return Loc.T(L.Tables.EntrySettled);
            case LedgerTone.Disputed:
                return Loc.T(L.Tables.EntryDisputed);
        }

        if (!entry.PayerConfirmed && !entry.PayeeConfirmed)
        {
            return Loc.T(L.Tables.EntryWaitingBoth);
        }

        return Loc.T(L.Tables.EntryWaitingOn, entry.PayerConfirmed ? entry.PayeeName : entry.PayerName);
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
        return Loc.T(L.Tables.CopyEntry, Loc.T(KindLabel(entry.Kind)), SideName(entry, entry.PayerUserId,
            entry.PayerName), SideName(entry, entry.PayeeUserId, entry.PayeeName), AmountText(entry.Amount, currency),
            StateText(entry));
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
        rows = new LedgerRowView[source.Length];
        var seated = 0;
        for (var index = 0; index < source.Length; index++)
        {
            var row = source[index];
            rows[index] = new LedgerRowView(row.DisplayName, NumberText.Compact(row.BuyIns),
                NumberText.Compact(row.Stack), SignedCompact(row.Net), row.Hands.ToString(Loc.Culture), row.Net,
                row.Seated);
            if (row.Seated || row.BuyIns > 0)
            {
                seated++;
            }
        }

        playerNames = new string[seated];
        playerIds = new string[seated];
        var next = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Seated || source[index].BuyIns > 0)
            {
                playerNames[next] = source[index].DisplayName;
                playerIds[next] = source[index].UserId;
                next++;
            }
        }

        var choices = ChoiceCount(seated, guestsOpen);
        choiceNames = new string[choices];
        choiceActive = new bool[choices];
        Array.Copy(playerNames, choiceNames, seated);
        if (guestsOpen)
        {
            choiceNames[seated] = Loc.T(L.Book.GuestTag);
        }

        playerIndex = Math.Clamp(playerIndex, 0, Math.Max(0, choices - 1));
        var gil = ledger.Entries ?? Array.Empty<CasinoLedgerEntryDto>();
        cards.Refresh(gil, myUserId, ledger.Currency);
        var unsettled = CasinoGilLedger.UnsettledCount(gil);
        waitingNote = unsettled > 0 ? Loc.T(L.Tables.EntriesWaiting, unsettled.ToString(Loc.Culture)) : string.Empty;
        var totals = GilLedgerTotals.Of(gil, myUserId);
        totalsText = totals.Settled > 0 ? TotalsText(totals) : string.Empty;
    }

    internal static LocString MissLine(TradeMiss miss)
    {
        if (miss.Kind != TradeMissKind.NotAtTable)
        {
            return L.Venue.TradeMissUnchecked;
        }

        return miss.Incoming ? L.Venue.TradeMissReceived : L.Venue.TradeMissSent;
    }

    internal static bool OffersGuest(TradeMiss miss, bool owner, bool guestsOpen)
    {
        return owner && guestsOpen && miss.GuestEligible;
    }

    private void DrawMiss(AppSkin ui, CasinoTableLedgerDto ledger, float scale)
    {
        var miss = trade.LastMiss;
        if (miss is null || !trade.Enabled || !miss.ShownAt(roomId, Environment.TickCount64))
        {
            return;
        }

        if (!ReferenceEquals(miss, missSource) || !ReferenceEquals(missLanguage, Loc.Current)
            || !ReferenceEquals(ledger, missLedger))
        {
            missSource = miss;
            missLanguage = Loc.Current;
            missLedger = ledger;
            missText = Loc.T(MissLine(miss), NumberText.Group(miss.Amount));
            missAction = Loc.T(GuestLedger.RecordLabel(GuestLedger.KindOf(miss,
                ledger.Entries ?? Array.Empty<CasinoLedgerEntryDto>())));
        }

        Note(ui, missText, Amber, scale);
        if (!OffersGuest(miss, ledger.Owner, viewGuests))
        {
            return;
        }

        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var buttonHeight = Button.RegularHeight * scale;
        var buttonWidth = MathF.Min(width, Button.WidthFor(missAction, ButtonSize.Regular));
        var rect = new Rect(origin, new Vector2(origin.X + buttonWidth, origin.Y + buttonHeight));
        if (Button.Draw(ImGui.GetWindowDrawList(), rect, missAction, ui.Ink, ButtonStyle.Tinted,
                enabled: !tables.IntentInFlight, id: "ledger.missGuest"))
        {
            Prefill(miss);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, buttonHeight + Metrics.Space.Sm * scale));
    }

    internal void Prefill(TradeMiss miss)
    {
        prefilledMiss = miss;
        playerIndex = playerIds.Length;
        proposeKind = miss.Incoming ? 0 : 1;
        amountBuffer = miss.Amount.ToString(CultureInfo.InvariantCulture);
        if (miss.PartnerName.Length > 0)
        {
            guestBuffer = miss.PartnerName;
        }

        revealRecord = true;
    }

    internal static bool CanCopy(CasinoTableLedgerDto ledger, bool book)
    {
        return (ledger.Rows?.Length ?? 0) > 0 || (book && (ledger.Entries?.Length ?? 0) > 0);
    }

    private void DrawHeading(AppSkin ui, CasinoTableLedgerDto ledger, bool book, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var buttonHeight = Button.LargeHeight * scale;
        var height = MathF.Max(titleHeight, buttonHeight);
        var copyLabel = Loc.T(L.Tables.CopyLedger);
        var buttonWidth = Button.WidthFor(copyLabel, ButtonSize.Large);
        var top = origin.Y + Metrics.Space.Lg * scale;
        Typography.Draw(drawList, new Vector2(origin.X, top + (height - titleHeight) * 0.5f),
            Typography.FitText(Loc.T(L.Tables.LedgerHeading), width - buttonWidth - Metrics.Space.Md * scale,
                TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
        var buttonTop = top + (height - buttonHeight) * 0.5f;
        var rect = new Rect(new Vector2(origin.X + width - buttonWidth, buttonTop),
            new Vector2(origin.X + width, buttonTop + buttonHeight));
        if (Button.Draw(drawList, rect, copyLabel, ui.Ink, ButtonStyle.Tinted, enabled: CanCopy(ledger, book),
                id: "ledger.copy"))
        {
            ImGui.SetClipboardText(CopyText());
            ShellToast.Show();
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

    private void DrawEntries(AppSkin ui, float scale)
    {
        if (cards.Count == 0)
        {
            Note(ui, Loc.T(L.Tables.EntriesEmpty), ui.MutedInk, scale);
            return;
        }

        var action = cards.Draw(ui, !tables.IntentInFlight, scale);
        if (totalsText.Length > 0)
        {
            Note(ui, totalsText, ui.BodyInk, scale);
        }

        if (action.Press == LedgerEntryPress.Confirm)
        {
            tables.ConfirmLedgerEntry(roomId, action.EntryId);
            return;
        }

        if (action.Press == LedgerEntryPress.Dispute)
        {
            AskDispute(action);
        }
    }

    internal static bool BarsReader(CasinoTableRowDto card, bool owner)
    {
        return !owner && !card.Admitted;
    }

    internal static bool ShowsRecord(bool host, int players, bool guestsOpen, string hostUserId)
    {
        return host ? players > 0 || guestsOpen : hostUserId.Length > 0;
    }

    internal static int ChoiceCount(int players, bool guestsOpen)
    {
        return players + (guestsOpen ? 1 : 0);
    }

    internal static bool IsGuestChoice(int index, int players, bool guestsOpen)
    {
        return guestsOpen && index >= players;
    }

    private void DrawPropose(AppSkin ui, CasinoTableLedgerDto ledger, string myUserId, string hostUserId,
        float scale)
    {
        var host = ledger.Owner;
        if (!ShowsRecord(host, playerIds.Length, viewGuests, hostUserId))
        {
            return;
        }

        ui.SectionHeading(Loc.T(host ? L.Tables.RecordHeading : L.Tables.RecordPaidHeading), Metrics.Space.Md);
        var width = ScrollLayout.StableContentWidth();
        var guest = false;
        if (host)
        {
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
            var segmentRect = new Rect(segment, new Vector2(segment.X + width, segment.Y + 34f * scale));
            proposeKind = SegmentStrip.Draw("##ledgerKind", segmentRect, kindOptions, proposeKind, ui.Palette);
            ImGui.Dummy(new Vector2(width, 34f * scale + Metrics.Space.Sm * scale));
            guest = IsGuestChoice(playerIndex, playerIds.Length, viewGuests);
            if (guest)
            {
                DrawGuestName(ui, width, scale);
            }
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
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
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
            Propose(ledger, myUserId, hostUserId, amount, guest);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, FieldHeight * scale + Metrics.Space.Sm * scale));
        RevealRecord();
        var hint = guest ? L.Book.ReasonGuestEntry : host ? L.Tables.RecordHostHint : L.Tables.RecordPlayerHint;
        Note(ui, Loc.T(hint), ui.MutedInk, scale);
    }

    private void RevealRecord()
    {
        if (!revealRecord)
        {
            return;
        }

        revealRecord = false;
        if (ImGui.GetCursorScreenPos().Y > ImGui.GetWindowPos().Y + ImGui.GetWindowHeight())
        {
            ImGui.SetScrollHereY(1f);
        }
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
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##ledgerGuest", Loc.T(L.Book.GuestNameHint), ref guestBuffer,
                CasinoHostingRules.GuestNameMaxLength + 1, ImGuiInputTextFlags.None);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, FieldHeight * scale + Metrics.Space.Sm * scale));
    }

    private void Propose(CasinoTableLedgerDto ledger, string myUserId, string hostUserId, long amount, bool guest)
    {
        var answered = prefilledMiss;
        prefilledMiss = null;
        if (guest)
        {
            ProposeGuest(ledger, amount, answered);
            return;
        }

        if (ledger.Owner && playerIds.Length == 0)
        {
            return;
        }

        if (answered is not null)
        {
            trade.Answered(answered);
        }

        if (ledger.Owner)
        {
            var kind = proposeKind == 0 ? BuyInKindFor(ledger, playerIds[playerIndex]) : CasinoLedgerKinds.Payout;
            tables.ProposeLedgerEntry(roomId, kind, playerIds[playerIndex], amount);
            amountBuffer = string.Empty;
            return;
        }

        tables.ProposeLedgerEntry(roomId, BuyInKindFor(ledger, myUserId), hostUserId, amount);
        amountBuffer = string.Empty;
    }

    private void ProposeGuest(CasinoTableLedgerDto ledger, long amount, TradeMiss? answered)
    {
        var offer = GuestLedger.Typed(roomId, guestBuffer, proposeKind != 0, amount,
            ledger.Entries ?? Array.Empty<CasinoLedgerEntryDto>());
        if (offer is null)
        {
            return;
        }

        trade.RecordGuest(offer, answered);
        amountBuffer = string.Empty;
    }

    internal static string BuyInKindFor(CasinoTableLedgerDto ledger, string playerId)
    {
        var source = ledger.Rows ?? Array.Empty<CasinoTableLedgerRowDto>();
        for (var index = 0; index < source.Length; index++)
        {
            if (string.Equals(source[index].UserId, playerId, StringComparison.Ordinal) && source[index].BuyIns > 0)
            {
                return CasinoLedgerKinds.Rebuy;
            }
        }

        return CasinoLedgerKinds.BuyIn;
    }

    private void AskDispute(in LedgerEntryAction action)
    {
        var targetRoom = roomId;
        var entryId = action.EntryId;
        confirm.Ask(new ConfirmRequest
        {
            Title = action.DisputeLabel,
            Message = Loc.T(L.Tables.DisputeBody),
            ConfirmLabel = action.DisputeLabel,
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => tables.DisputeLedgerEntry(targetRoom, entryId),
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
