using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal readonly record struct LedgerSums(long TakenIn, long PaidOut, long Owed)
{
    public static LedgerSums Of(CasinoLedgerEntryDto[]? entries, string hostId)
    {
        var totals = GilLedgerTotals.Of(entries, hostId);
        var owed = 0L;
        if (entries is not null)
        {
            for (var index = 0; index < entries.Length; index++)
            {
                if (!entries[index].Settled)
                {
                    owed += entries[index].Amount;
                }
            }
        }

        return new LedgerSums(totals.In, totals.Out, owed);
    }
}

internal sealed class GilLedgerSummary
{
    private const string Pending = "·";
    private const float ColumnGap = 8f;
    private const float LabelGap = 1f;
    private const float ChevronReserve = 22f;
    private const float ToggleWidth = 51f;
    private const float ToggleHeight = 31f;

    private readonly string toggleId;
    private CasinoLedgerEntryDto[]? source;
    private LanguageInfo? language;
    private string sourceHost = string.Empty;
    private bool loaded;
    private string takenIn = Pending;
    private string paidOut = Pending;
    private string owed = Pending;

    public GilLedgerSummary(string toggleId)
    {
        this.toggleId = toggleId;
    }

    public static float TotalsHeight(float scale) =>
        DoorLayout.Pad * scale * 2f + Typography.LineHeight(TextStyles.FootnoteEmphasized) + LabelGap * scale
        + Typography.LineHeight(TextStyles.Title3);

    public static float SyncHeight(float width, float scale)
    {
        var textWidth = SyncTextWidth(width, scale);
        var block = Typography.LineHeight(TextStyles.Headline) + DoorLayout.ManageLineGap * scale
            + PitText.Height(Loc.T(L.HostDesk.TradeSyncLine), TextStyles.Footnote, textWidth);
        return MathF.Max(block, ToggleHeight * scale) + DoorLayout.ManagePadY * 2f * scale;
    }

    public void Refresh(CasinoTableLedgerDto? ledger, string hostId)
    {
        var entries = ledger?.Entries;
        var hasLedger = ledger is not null;
        if (ReferenceEquals(entries, source) && hasLedger == loaded && ReferenceEquals(language, Loc.Current)
            && string.Equals(hostId, sourceHost, StringComparison.Ordinal))
        {
            return;
        }

        source = entries;
        loaded = hasLedger;
        language = Loc.Current;
        sourceHost = hostId;
        if (!hasLedger)
        {
            takenIn = Pending;
            paidOut = Pending;
            owed = Pending;
            return;
        }

        var sums = LedgerSums.Of(entries, hostId);
        takenIn = NumberText.Compact(sums.TakenIn);
        paidOut = NumberText.Compact(sums.PaidOut);
        owed = NumberText.Compact(sums.Owed);
    }

    public bool DrawSync(ImDrawListPtr drawList, AppSkin ui, in Rect row, bool enabled, float scale)
    {
        var left = row.Min.X + DoorLayout.Pad * scale;
        var textWidth = SyncTextWidth(row.Width, scale);
        var line = Loc.T(L.HostDesk.TradeSyncLine);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var block = titleHeight + DoorLayout.ManageLineGap * scale + PitText.Height(line, TextStyles.Footnote, textWidth);
        var top = row.Center.Y - block * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(Loc.T(L.Venue.TradeSync), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        PitText.Draw(drawList, line, TextStyles.Footnote, ui.MutedInk, left,
            top + titleHeight + DoorLayout.ManageLineGap * scale, textWidth, false);
        var toggleMax = new Vector2(row.Max.X - DoorLayout.Pad * scale, row.Center.Y + ToggleHeight * 0.5f * scale);
        var toggle = new Rect(new Vector2(toggleMax.X - ToggleWidth * scale, toggleMax.Y - ToggleHeight * scale),
            toggleMax);
        return Toggle.Draw(toggleId, toggle, enabled, ui.Theme);
    }

    public bool DrawTotals(ImDrawListPtr drawList, AppSkin ui, in Rect row, bool tappable, float scale)
    {
        var hovered = tappable && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            var radius = Metrics.Radius.Grouped * scale;
            drawList.PushClipRect(row.Min, row.Max, true);
            Squircle.Fill(drawList, new Vector2(row.Min.X, row.Min.Y - radius), row.Max, radius,
                ImGui.GetColorU32(ui.HoverTint));
            drawList.PopClipRect();
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = DoorLayout.Pad * scale;
        var left = row.Min.X + pad;
        var right = row.Max.X - pad - (tappable ? ChevronReserve * scale : 0f);
        var gap = ColumnGap * scale;
        var column = MathF.Max(1f, (right - left - gap * 2f) / 3f);
        var top = row.Min.Y + pad;
        DrawColumn(drawList, ui, Loc.T(L.HostDesk.TakenIn), takenIn, CasinoColors.Money, left, top, column, scale);
        DrawColumn(drawList, ui, Loc.T(L.HostDesk.PaidOut), paidOut, ui.TitleInk, left + column + gap, top, column,
            scale);
        DrawColumn(drawList, ui, Loc.T(L.HostDesk.Owed), owed, TableLedger.Amber, left + (column + gap) * 2f, top,
            column, scale);
        if (!tappable)
        {
            return false;
        }

        CasinoArt.Chevron(drawList, new Vector2(row.Max.X - pad - ChevronReserve * 0.3f * scale, row.Center.Y),
            ui.MutedInk);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static float SyncTextWidth(float width, float scale) =>
        MathF.Max(1f, width - (DoorLayout.Pad * 2f + ToggleWidth + DoorLayout.TextGap) * scale);

    private static void DrawColumn(ImDrawListPtr drawList, AppSkin ui, string label, string value, Vector4 ink,
        float left, float top, float width, float scale)
    {
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(label, width, TextStyles.FootnoteEmphasized), ui.MutedInk, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList,
            new Vector2(left, top + Typography.LineHeight(TextStyles.FootnoteEmphasized) + LabelGap * scale),
            Typography.FitText(value, width, TextStyles.Title3), ink, TextStyles.Title3);
    }
}
