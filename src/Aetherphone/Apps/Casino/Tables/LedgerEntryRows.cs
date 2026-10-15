using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal readonly record struct LedgerEntryView(string EntryId, string Name, string Meta, string Amount, string State,
    bool Incoming, bool Owed, LedgerEntryOption Option);

internal sealed class LedgerEntryRows
{
    private const float Radius = 16f;
    private const float IconFillAlpha = 0.18f;
    private const float OutgoingFillAlpha = 0.10f;
    private const float OwedRimAlpha = 0.45f;
    private const float PillFillAlpha = 0.18f;
    private const float PillRounding = 6f;
    private const float ArrowShare = 0.42f;

    private CasinoLedgerEntryDto[]? source;
    private LanguageInfo? language;
    private string viewer = string.Empty;
    private bool host;
    private int clockVersion = -1;
    private LedgerEntryView[] views = Array.Empty<LedgerEntryView>();

    public int Count => views.Length;

    public LedgerEntryView this[int index] => views[index];

    public void Refresh(CasinoLedgerEntryDto[] entries, string viewerId, bool hosting, int currency)
    {
        if (ReferenceEquals(entries, source) && ReferenceEquals(language, Loc.Current) && hosting == host
            && string.Equals(viewerId, viewer, StringComparison.Ordinal) && clockVersion == TimeText.FormatVersion)
        {
            return;
        }

        source = entries;
        language = Loc.Current;
        viewer = viewerId;
        host = hosting;
        clockVersion = TimeText.FormatVersion;
        views = new LedgerEntryView[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            views[index] = ViewOf(entries[index], viewerId, hosting, currency);
        }
    }

    internal static LedgerEntryView ViewOf(CasinoLedgerEntryDto entry, string viewerId, bool hosting, int currency)
    {
        var side = CasinoGilLedger.SideOf(entry, viewerId);
        var incoming = side == LedgerSide.Payee;
        var name = side switch
        {
            LedgerSide.Payee => TableLedger.SideName(entry, entry.PayerUserId, entry.PayerName),
            LedgerSide.Payer => TableLedger.SideName(entry, entry.PayeeUserId, entry.PayeeName),
            _ => Loc.T(L.Tables.EntryParties, TableLedger.SideName(entry, entry.PayerUserId, entry.PayerName),
                TableLedger.SideName(entry, entry.PayeeUserId, entry.PayeeName)),
        };
        var kind = Loc.T(CasinoGilLedger.KindLabel(entry.Kind));
        var clock = TimeText.Clock(entry.CreatedAtUnixMs / 1000);
        var meta = clock.Length > 0 ? Loc.T(L.HostDesk.EntryMeta, kind, clock) : kind;
        var signed = side switch
        {
            LedgerSide.Payee => NumberText.Signed(entry.Amount),
            LedgerSide.Payer => NumberText.Group(-entry.Amount),
            _ => NumberText.Group(entry.Amount),
        };
        var amount = currency == CasinoCurrencies.Gil ? Loc.T(L.Tables.GilAmount, signed) : signed;
        var state = Loc.T(entry.Settled ? L.HostDesk.PillRecorded : L.HostDesk.PillOwed);
        return new LedgerEntryView(entry.EntryId, name, meta, amount, state, incoming, !entry.Settled,
            CasinoGilLedger.OptionFor(entry, hosting));
    }

    internal static string OptionLabel(LedgerEntryOption option) => option switch
    {
        LedgerEntryOption.MarkPaid => Loc.T(L.Tables.MarkPaid),
        LedgerEntryOption.MarkUnpaid => Loc.T(L.Tables.MarkUnpaid),
        LedgerEntryOption.Remove => Loc.T(L.Tables.RemoveEntry),
        _ => string.Empty,
    };

    public int Draw(AppSkin ui, bool tappable, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var drawList = ImGui.GetWindowDrawList();
        var nameHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var metaHeight = Typography.LineHeight(TextStyles.Footnote);
        var pillHeight = LedgerEntryLayout.PillHeight(Typography.LineHeight(TextStyles.FootnoteEmphasized), scale);
        var height = LedgerEntryLayout.Height(nameHeight, metaHeight, nameHeight, pillHeight, scale);
        var tapped = -1;
        for (var index = 0; index < views.Length; index++)
        {
            using var id = ImRaii.PushId(index);
            var origin = ImGui.GetCursorScreenPos();
            var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
            if (DrawRow(drawList, ui, views[index], row, tappable, nameHeight, metaHeight, pillHeight, scale))
            {
                tapped = index;
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + LedgerEntryLayout.RowGap * scale));
        }

        return tapped;
    }

    private static bool DrawRow(ImDrawListPtr drawList, AppSkin ui, in LedgerEntryView view, in Rect row,
        bool tappable, float nameHeight, float metaHeight, float pillHeight, float scale)
    {
        var radius = Radius * scale;
        var active = tappable && view.Option != LedgerEntryOption.None;
        var hovered = active && UiInteract.Hover(row.Min, row.Max);
        ui.Card(drawList, row.Min, row.Max, radius);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (view.Owed)
        {
            Squircle.Stroke(drawList, row.Min, row.Max, radius,
                ImGui.GetColorU32(TableLedger.Amber with { W = OwedRimAlpha }), 1f * scale);
        }

        var icon = LedgerEntryLayout.Icon * scale;
        var center = LedgerEntryLayout.IconCenter(row, scale);
        var arrowInk = view.Incoming ? CasinoColors.Money : ui.TitleInk;
        drawList.AddCircleFilled(center, icon * 0.5f,
            ImGui.GetColorU32(arrowInk with { W = view.Incoming ? IconFillAlpha : OutgoingFillAlpha }), 32);
        ProgressRing.CenterIcon(drawList, center, view.Incoming ? FontAwesomeIcon.ArrowDown : FontAwesomeIcon.ArrowUp,
            arrowInk, icon * ArrowShare);
        var stateInk = view.Owed ? TableLedger.Amber : TableLedger.SettledInk;
        var amountSize = Typography.Measure(view.Amount, TextStyles.SubheadlineEmphasized);
        var stateSize = Typography.Measure(view.State, TextStyles.FootnoteEmphasized);
        var pillWidth = stateSize.X + LedgerEntryLayout.PillPadX * 2f * scale;
        var valueWidth = MathF.Max(amountSize.X, pillWidth);
        var right = LedgerEntryLayout.ValueRight(row, scale);
        var valueTop = row.Center.Y - (nameHeight + LedgerEntryLayout.PillGap * scale + pillHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(right - amountSize.X, valueTop), view.Amount,
            view.Incoming ? CasinoColors.Money : ui.TitleInk, TextStyles.SubheadlineEmphasized);
        var pillMin = new Vector2(right - pillWidth, valueTop + nameHeight + LedgerEntryLayout.PillGap * scale);
        var pillMax = new Vector2(right, pillMin.Y + pillHeight);
        Squircle.Fill(drawList, pillMin, pillMax, PillRounding * scale,
            ImGui.GetColorU32(stateInk with { W = PillFillAlpha }));
        Typography.Draw(drawList, new Vector2(pillMin.X + LedgerEntryLayout.PillPadX * scale,
            pillMin.Y + LedgerEntryLayout.PillPadY * scale), view.State, stateInk, TextStyles.FootnoteEmphasized);
        var textLeft = LedgerEntryLayout.TextLeft(row, scale);
        var textWidth = LedgerEntryLayout.TextWidth(row, valueWidth, scale);
        var textTop = row.Center.Y - (nameHeight + LedgerEntryLayout.LineGap * scale + metaHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(view.Name, textWidth, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + LedgerEntryLayout.LineGap * scale),
            Typography.FitText(view.Meta, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        return active && UiInteract.Click(row.Min, row.Max, hovered);
    }
}
