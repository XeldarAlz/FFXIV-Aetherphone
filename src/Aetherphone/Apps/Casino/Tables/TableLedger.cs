using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
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
    string State, LedgerTone Tone, bool CanConfirm, string ConfirmLabel, bool CanDispute, string DisputeLabel);

internal sealed class TableLedger
{
    public static readonly Vector4 Amber = new(1f, 0.71f, 0.22f, 1f);

    private static readonly Vector4 SettledInk = new(0.36f, 0.84f, 0.52f, 1f);

    private const float Pad = 14f;
    private const float RowHeight = 60f;
    private const float EntryHeight = 74f;
    private const float FieldHeight = 38f;
    private const int AmountDigits = 11;

    private readonly CasinoTablesStore tables;
    private readonly ConfirmService confirm;
    private readonly ChipRail playerRail = new();
    private readonly string[] kindOptions = new string[2];

    private CasinoTableLedgerDto? viewSource;
    private LanguageInfo? viewLanguage;
    private string viewMe = string.Empty;
    private LedgerRowView[] rows = Array.Empty<LedgerRowView>();
    private LedgerEntryView[] entries = Array.Empty<LedgerEntryView>();
    private string[] playerNames = Array.Empty<string>();
    private string[] playerIds = Array.Empty<string>();
    private bool[] playerActive = Array.Empty<bool>();
    private string waitingNote = string.Empty;
    private string roomId = string.Empty;
    private string amountBuffer = string.Empty;
    private int proposeKind;
    private int playerIndex;

    public TableLedger(CasinoTablesStore tables, ConfirmService confirm)
    {
        this.tables = tables;
        this.confirm = confirm;
    }

    public void Enter(string tableId)
    {
        roomId = tableId;
        amountBuffer = string.Empty;
        proposeKind = 0;
        playerIndex = 0;
        viewSource = null;
        playerRail.Reset();
        tables.RefreshLedgerNow(tableId);
    }

    public void Reset()
    {
        roomId = string.Empty;
        viewSource = null;
        amountBuffer = string.Empty;
        tables.ForgetLedger();
    }

    public void Draw(AppSkin ui, string myUserId, string hostUserId, float scale)
    {
        if (roomId.Length == 0)
        {
            return;
        }

        tables.EnsureLedgerFresh(roomId);
        var ledger = tables.LedgerFor(roomId);
        if (ledger is null)
        {
            Note(ui, Loc.T(L.Tables.LedgerLoading), ui.MutedInk, scale);
            return;
        }

        Refresh(ledger, myUserId);
        DrawHeading(ui, ledger, scale);
        DrawRows(ui, ledger.Currency, scale);
        if (ledger.Currency != CasinoCurrencies.Gil)
        {
            return;
        }

        ui.SectionHeading(Loc.T(L.Tables.GilLedgerHeading), Metrics.Space.Md);
        if (waitingNote.Length > 0)
        {
            Note(ui, waitingNote, Amber, scale);
        }

        DrawEntries(ui, scale);
        DrawPropose(ui, ledger, myUserId, hostUserId, scale);
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
            var entry = gil[index];
            builder.Append(Loc.T(L.Tables.CopyEntry, Loc.T(KindLabel(entry.Kind)), entry.PayerName, entry.PayeeName,
                AmountText(entry.Amount, ledger.Currency), StateText(entry))).Append('\n');
        }

        return builder.ToString();
    }

    private void Refresh(CasinoTableLedgerDto ledger, string myUserId)
    {
        if (ReferenceEquals(ledger, viewSource) && ReferenceEquals(viewLanguage, Loc.Current)
            && string.Equals(myUserId, viewMe, StringComparison.Ordinal))
        {
            return;
        }

        viewSource = ledger;
        viewLanguage = Loc.Current;
        viewMe = myUserId;
        kindOptions[0] = Loc.T(L.Tables.KindBuyIn);
        kindOptions[1] = Loc.T(L.Tables.KindPayout);
        var source = ledger.Rows ?? Array.Empty<CasinoTableLedgerRowDto>();
        rows = new LedgerRowView[source.Length];
        var seated = 0;
        for (var index = 0; index < source.Length; index++)
        {
            var row = source[index];
            rows[index] = new LedgerRowView(row.DisplayName,
                Loc.T(L.Tables.RowBuyIns, AmountText(row.BuyIns, ledger.Currency)),
                Loc.T(L.Tables.RowStack, AmountText(row.Stack, ledger.Currency)),
                CasinoTextCache.SignedText(row.Net), Loc.T(L.Tables.RowHands, row.Hands.ToString(Loc.Culture)),
                row.Net, row.Seated);
            if (row.Seated || row.BuyIns > 0)
            {
                seated++;
            }
        }

        playerNames = new string[seated];
        playerIds = new string[seated];
        playerActive = new bool[seated];
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

        playerIndex = Math.Clamp(playerIndex, 0, Math.Max(0, seated - 1));
        var gil = ledger.Entries ?? Array.Empty<CasinoLedgerEntryDto>();
        entries = new LedgerEntryView[gil.Length];
        for (var index = 0; index < gil.Length; index++)
        {
            var entry = gil[index];
            var side = CasinoGilLedger.SideOf(entry, myUserId);
            var confirmLabel = side == LedgerSide.Payer ? Loc.T(L.Tables.IPaid) : Loc.T(L.Tables.IReceived);
            var disputeLabel = entry.Kind is CasinoLedgerKinds.Payout or CasinoLedgerKinds.CashOut
                or CasinoLedgerKinds.Prize or CasinoLedgerKinds.Stake
                ? Loc.T(L.Tables.PayoutNotReceived)
                : Loc.T(L.Tables.GilNotReceived);
            entries[index] = new LedgerEntryView(entry.EntryId, Loc.T(KindLabel(entry.Kind)),
                Loc.T(L.Tables.EntryParties, entry.PayerName, entry.PayeeName),
                AmountText(entry.Amount, ledger.Currency), StateText(entry), ToneOf(entry),
                CasinoGilLedger.CanConfirm(entry, myUserId), confirmLabel, CasinoGilLedger.CanDispute(entry, myUserId),
                disputeLabel);
        }

        var unsettled = CasinoGilLedger.UnsettledCount(gil);
        waitingNote = unsettled > 0 ? Loc.T(L.Tables.EntriesWaiting, unsettled.ToString(Loc.Culture)) : string.Empty;
    }

    private void DrawHeading(AppSkin ui, CasinoTableLedgerDto ledger, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = Typography.LineHeight(TextStyles.Headline) + Metrics.Space.Sm * scale;
        var buttonHeight = Button.SmallHeight * scale;
        var copyLabel = Loc.T(L.Tables.CopyLedger);
        var buttonWidth = Button.WidthFor(copyLabel, ButtonSize.Small);
        Typography.Draw(drawList, origin + new Vector2(0f, Metrics.Space.Md * scale),
            Typography.FitText(Loc.T(L.Tables.LedgerHeading), width - buttonWidth - Metrics.Space.Md * scale,
                TextStyles.Headline), ui.HeaderInk, TextStyles.Headline);
        var top = origin.Y + Metrics.Space.Md * scale + (Typography.LineHeight(TextStyles.Headline) - buttonHeight) * 0.5f;
        var rect = new Rect(new Vector2(origin.X + width - buttonWidth, top),
            new Vector2(origin.X + width, top + buttonHeight));
        if (Button.Draw(drawList, rect, copyLabel, ui.Ink, ButtonStyle.Tinted, enabled: (ledger.Rows?.Length ?? 0) > 0,
                id: "ledger.copy"))
        {
            ImGui.SetClipboardText(CopyText());
            ShellToast.Show();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
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
        var rowHeight = RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight * rows.Length);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Pad * scale;
        for (var index = 0; index < rows.Length; index++)
        {
            var view = rows[index];
            var top = origin.Y + index * rowHeight;
            if (index > 0)
            {
                drawList.AddLine(new Vector2(origin.X + pad, top), new Vector2(max.X, top),
                    ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            }

            var netInk = view.NetValue > 0 ? SettledInk : ui.MutedInk;
            var netSize = Typography.Measure(view.Net, TextStyles.Headline);
            Typography.Draw(drawList, new Vector2(max.X - pad - netSize.X, top + (rowHeight - netSize.Y) * 0.5f),
                view.Net, netInk, TextStyles.Headline);
            var textWidth = width - pad * 3f - netSize.X;
            var nameInk = view.Seated ? ui.TitleInk : ui.BodyInk;
            Typography.Draw(drawList, new Vector2(origin.X + pad, top + 10f * scale),
                Typography.FitText(view.Name, textWidth, TextStyles.SubheadlineEmphasized), nameInk,
                TextStyles.SubheadlineEmphasized);
            var detail = Typography.FitText(view.BuyIns, textWidth * 0.5f, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(origin.X + pad, top + 32f * scale), detail, ui.MutedInk,
                TextStyles.Footnote);
            var stack = Typography.FitText(view.Stack, textWidth * 0.5f, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(origin.X + pad + textWidth * 0.5f, top + 32f * scale), stack,
                ui.MutedInk, TextStyles.Footnote);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, max.Y - origin.Y + Metrics.Space.Sm * scale));
    }

    private void DrawEntries(AppSkin ui, float scale)
    {
        if (entries.Length == 0)
        {
            Note(ui, Loc.T(L.Tables.EntriesEmpty), ui.MutedInk, scale);
            return;
        }

        var width = ScrollLayout.StableContentWidth();
        var drawList = ImGui.GetWindowDrawList();
        var pad = Pad * scale;
        for (var index = 0; index < entries.Length; index++)
        {
            using var entryId = ImRaii.PushId(index);
            var view = entries[index];
            var origin = ImGui.GetCursorScreenPos();
            var height = EntryHeight * scale;
            var max = new Vector2(origin.X + width, origin.Y + height);
            var tone = view.Tone switch
            {
                LedgerTone.Waiting => Amber,
                LedgerTone.Disputed => ui.Accent,
                _ => SettledInk,
            };
            ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
            if (view.Tone != LedgerTone.Settled)
            {
                Squircle.Stroke(drawList, origin, max, Metrics.Radius.Grouped * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(tone, 0.65f)), 1.2f * scale);
            }

            var amountSize = Typography.Measure(view.Amount, TextStyles.SubheadlineEmphasized);
            Typography.Draw(drawList, new Vector2(max.X - pad - amountSize.X, origin.Y + 10f * scale), view.Amount,
                TableRow.CurrencyTint(CasinoCurrencies.Gil, ui.Accent), TextStyles.SubheadlineEmphasized);
            var textWidth = width - pad * 3f - amountSize.X;
            Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + 10f * scale),
                Typography.FitText(view.Title, textWidth, TextStyles.SubheadlineEmphasized), ui.TitleInk,
                TextStyles.SubheadlineEmphasized);
            Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + 30f * scale),
                Typography.FitText(view.Parties, width - pad * 2f, TextStyles.Footnote), ui.BodyInk,
                TextStyles.Footnote);

            var buttonHeight = Button.SmallHeight * scale;
            var buttonTop = max.Y - pad * 0.7f - buttonHeight;
            var right = max.X - pad;
            if (view.CanDispute)
            {
                var disputeWidth = Button.WidthFor(view.DisputeLabel, ButtonSize.Small);
                var rect = new Rect(new Vector2(right - disputeWidth, buttonTop),
                    new Vector2(right, buttonTop + buttonHeight));
                if (Button.Draw(drawList, rect, view.DisputeLabel, ui.Ink, ButtonStyle.Gray, ButtonRole.Destructive,
                        !tables.IntentInFlight, id: "ledger.dispute"))
                {
                    AskDispute(view);
                }

                right = rect.Min.X - Metrics.Space.Sm * scale;
            }

            if (view.CanConfirm)
            {
                var confirmWidth = Button.WidthFor(view.ConfirmLabel, ButtonSize.Small);
                var rect = new Rect(new Vector2(right - confirmWidth, buttonTop),
                    new Vector2(right, buttonTop + buttonHeight));
                if (Button.Draw(drawList, rect, view.ConfirmLabel, ui.Ink, ButtonStyle.Prominent,
                        enabled: !tables.IntentInFlight, id: "ledger.confirm"))
                {
                    tables.ConfirmLedgerEntry(roomId, view.EntryId);
                }

                right = rect.Min.X - Metrics.Space.Sm * scale;
            }

            var stateWidth = right - origin.X - pad;
            var stateHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(origin.X + pad, buttonTop + (buttonHeight - stateHeight) * 0.5f),
                Typography.FitText(view.State, MathF.Max(1f, stateWidth), TextStyles.FootnoteEmphasized), tone,
                TextStyles.FootnoteEmphasized);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        }
    }

    private void DrawPropose(AppSkin ui, CasinoTableLedgerDto ledger, string myUserId, string hostUserId,
        float scale)
    {
        var host = ledger.Owner;
        if (host && playerNames.Length == 0)
        {
            return;
        }

        if (!host && hostUserId.Length == 0)
        {
            return;
        }

        ui.SectionHeading(Loc.T(host ? L.Tables.RecordHeading : L.Tables.RecordPaidHeading), Metrics.Space.Md);
        var width = ScrollLayout.StableContentWidth();
        if (host)
        {
            for (var index = 0; index < playerActive.Length; index++)
            {
                playerActive[index] = index == playerIndex;
            }

            var tapped = playerRail.Draw(ui, playerNames, playerActive);
            if (tapped >= 0)
            {
                playerIndex = tapped;
            }

            var segment = ImGui.GetCursorScreenPos();
            var segmentRect = new Rect(segment, new Vector2(segment.X + width, segment.Y + 34f * scale));
            proposeKind = SegmentStrip.Draw("##ledgerKind", segmentRect, kindOptions, proposeKind, ui.Palette);
            ImGui.Dummy(new Vector2(width, 34f * scale + Metrics.Space.Sm * scale));
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

        var amount = HostDraft.Parse(amountBuffer);
        var buttonHeight = Button.RegularHeight * scale;
        var buttonTop = field.Center.Y - buttonHeight * 0.5f;
        var rect = new Rect(new Vector2(origin.X + width - buttonWidth, buttonTop),
            new Vector2(origin.X + width, buttonTop + buttonHeight));
        var ready = amount > 0 && amount <= CasinoHostingRules.MaxGil && !tables.IntentInFlight;
        if (Button.Draw(drawList, rect, buttonLabel, ui.Ink, ButtonStyle.Prominent, enabled: ready, id: "ledger.record"))
        {
            Propose(ledger, myUserId, hostUserId, amount);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, FieldHeight * scale + Metrics.Space.Sm * scale));
        Note(ui, Loc.T(host ? L.Tables.RecordHostHint : L.Tables.RecordPlayerHint), ui.MutedInk, scale);
    }

    private void Propose(CasinoTableLedgerDto ledger, string myUserId, string hostUserId, long amount)
    {
        if (ledger.Owner)
        {
            if (playerIds.Length == 0)
            {
                return;
            }

            var kind = proposeKind == 0 ? BuyInKindFor(ledger, playerIds[playerIndex]) : CasinoLedgerKinds.Payout;
            tables.ProposeLedgerEntry(roomId, kind, playerIds[playerIndex], amount);
            amountBuffer = string.Empty;
            return;
        }

        tables.ProposeLedgerEntry(roomId, BuyInKindFor(ledger, myUserId), hostUserId, amount);
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

    private void AskDispute(in LedgerEntryView view)
    {
        var targetRoom = roomId;
        var entryId = view.EntryId;
        confirm.Ask(new ConfirmRequest
        {
            Title = view.DisputeLabel,
            Message = Loc.T(L.Tables.DisputeBody),
            ConfirmLabel = view.DisputeLabel,
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => tables.DisputeLedgerEntry(targetRoom, entryId),
        });
    }

    private static void Note(AppSkin ui, string text, Vector4 ink, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + Metrics.Space.Xs * scale, origin.Y), text, ink,
            TextStyles.Footnote, width - Metrics.Space.Xs * 2f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }
}
