using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class GilLedgerView
{
    private readonly CasinoTablesStore tables;
    private readonly ConfirmService confirm;
    private readonly LedgerEntryCards cards = new();

    private CasinoLedgerEntryDto[]? waitingSource;
    private LanguageInfo? waitingLanguage;
    private string waitingNote = string.Empty;

    public GilLedgerView(CasinoTablesStore tables, ConfirmService confirm)
    {
        this.tables = tables;
        this.confirm = confirm;
    }

    public void Enter()
    {
        tables.RefreshMyLedgerNow();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        tables.EnsureMyLedgerFresh();
        if (tables.TakeIntentFailure())
        {
            TableLedger.Toast(new CasinoStakeOutcome(false, CasinoReasons.Unreachable));
        }

        TableLedger.Toast(tables.TakeNoticeOutcome());
        using var surface = AppSurface.Begin(body);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        TableLedger.Note(ui, Loc.T(L.Tables.MyLedgerLine), ui.MutedInk, scale);
        if (!tables.MyLedgerLoaded)
        {
            TableLedger.Note(ui, Loc.T(L.Tables.LedgerLoading), ui.MutedInk, scale);
            return;
        }

        var entries = tables.MyEntries;
        cards.Refresh(entries, tables.AccountId, CasinoCurrencies.Gil);
        if (cards.Count == 0)
        {
            TableLedger.Note(ui, Loc.T(L.Tables.MyLedgerEmpty), ui.MutedInk, scale);
            return;
        }

        RefreshWaiting(entries);
        if (waitingNote.Length > 0)
        {
            TableLedger.Note(ui, waitingNote, TableLedger.Amber, scale);
        }

        var action = cards.Draw(ui, !tables.IntentInFlight, scale);
        if (action.Press == LedgerEntryPress.Confirm)
        {
            tables.ConfirmLedgerEntry(string.Empty, action.EntryId);
        }
        else if (action.Press == LedgerEntryPress.Dispute)
        {
            AskDispute(action);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void RefreshWaiting(CasinoLedgerEntryDto[] entries)
    {
        if (ReferenceEquals(entries, waitingSource) && ReferenceEquals(waitingLanguage, Loc.Current))
        {
            return;
        }

        waitingSource = entries;
        waitingLanguage = Loc.Current;
        var unsettled = CasinoGilLedger.UnsettledCount(entries);
        waitingNote = unsettled > 0 ? Loc.T(L.Tables.EntriesWaiting, unsettled.ToString(Loc.Culture)) : string.Empty;
    }

    private void AskDispute(in LedgerEntryAction action)
    {
        var entryId = action.EntryId;
        confirm.Ask(new ConfirmRequest
        {
            Title = action.DisputeLabel,
            Message = Loc.T(L.Tables.DisputeBody),
            ConfirmLabel = action.DisputeLabel,
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => tables.DisputeLedgerEntry(string.Empty, entryId),
        });
    }
}
