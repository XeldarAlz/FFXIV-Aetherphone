using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class GilLedgerView
{
    private readonly CasinoTablesStore tables;
    private readonly LedgerEntryRows rows = new();

    private CasinoLedgerEntryDto[]? owedSource;
    private LanguageInfo? owedLanguage;
    private string owedNote = string.Empty;

    public GilLedgerView(CasinoTablesStore tables)
    {
        this.tables = tables;
    }

    public void Enter()
    {
        tables.RefreshMyLedgerNow();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        tables.EnsureMyLedgerFresh();
        using var surface = AppSurface.Begin(body);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        TableLedger.Note(ui, Loc.T(L.Tables.MyLedgerLine), ui.MutedInk, scale);
        if (!tables.MyLedgerLoaded)
        {
            TableLedger.Note(ui, Loc.T(L.Tables.LedgerLoading), ui.MutedInk, scale);
            return;
        }

        var entries = tables.MyEntries;
        rows.Refresh(entries, tables.AccountId, false, CasinoCurrencies.Gil);
        if (rows.Count == 0)
        {
            TableLedger.Note(ui, Loc.T(L.Tables.MyLedgerEmpty), ui.MutedInk, scale);
            return;
        }

        RefreshOwed(entries);
        if (owedNote.Length > 0)
        {
            TableLedger.Note(ui, owedNote, TableLedger.Amber, scale);
        }

        _ = rows.Draw(ui, false, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void RefreshOwed(CasinoLedgerEntryDto[] entries)
    {
        if (ReferenceEquals(entries, owedSource) && ReferenceEquals(owedLanguage, Loc.Current))
        {
            return;
        }

        owedSource = entries;
        owedLanguage = Loc.Current;
        owedNote = TableLedger.OwedNoteFor(entries);
    }
}
