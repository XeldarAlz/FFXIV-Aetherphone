using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal enum LedgerEntryPress : byte
{
    None,
    Confirm,
    Dispute,
}

internal readonly record struct LedgerEntryAction(LedgerEntryPress Press, string EntryId, string DisputeLabel)
{
    public static readonly LedgerEntryAction None = new(LedgerEntryPress.None, string.Empty, string.Empty);
}

internal sealed class LedgerEntryCards
{
    private const float TagPadX = 8f;
    private const float TagPadY = 2f;
    private const float TagGap = 6f;
    private const float TagFillAlpha = 0.18f;

    private CasinoLedgerEntryDto[]? source;
    private LanguageInfo? language;
    private string me = string.Empty;
    private LedgerEntryView[] views = Array.Empty<LedgerEntryView>();

    public int Count => views.Length;

    public void Refresh(CasinoLedgerEntryDto[] entries, string myUserId, int currency)
    {
        if (ReferenceEquals(entries, source) && ReferenceEquals(language, Loc.Current)
            && string.Equals(myUserId, me, StringComparison.Ordinal))
        {
            return;
        }

        source = entries;
        language = Loc.Current;
        me = myUserId;
        views = new LedgerEntryView[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var side = CasinoGilLedger.SideOf(entry, myUserId);
            var confirmLabel = side == LedgerSide.Payer ? Loc.T(L.Tables.IPaid) : Loc.T(L.Tables.IReceived);
            var disputeLabel = entry.Kind is CasinoLedgerKinds.Payout or CasinoLedgerKinds.CashOut
                or CasinoLedgerKinds.Prize or CasinoLedgerKinds.Stake
                ? Loc.T(L.Tables.PayoutNotReceived)
                : Loc.T(L.Tables.GilNotReceived);
            views[index] = new LedgerEntryView(entry.EntryId, Loc.T(TableLedger.KindLabel(entry.Kind)),
                Loc.T(L.Tables.EntryParties, entry.PayerName, entry.PayeeName),
                TableLedger.AmountText(entry.Amount, currency), TableLedger.StateText(entry),
                TableLedger.ToneOf(entry), CasinoGilLedger.CanConfirm(entry, myUserId), confirmLabel,
                CasinoGilLedger.CanDispute(entry, myUserId), disputeLabel,
                entry.Guest ? Loc.T(L.Book.GuestTag) : string.Empty);
        }
    }

    public LedgerEntryAction Draw(AppSkin ui, bool enabled, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var drawList = ImGui.GetWindowDrawList();
        var pad = LedgerEntryLayout.Pad * scale;
        var action = LedgerEntryAction.None;
        for (var index = 0; index < views.Length; index++)
        {
            using var entryId = ImRaii.PushId(index);
            var view = views[index];
            var origin = ImGui.GetCursorScreenPos();
            var titleLine = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
            var partiesLine = Typography.LineHeight(TextStyles.Footnote);
            var stateLine = Typography.LineHeight(TextStyles.FootnoteEmphasized);
            var confirmWidth = view.CanConfirm ? Button.WidthFor(view.ConfirmLabel, ButtonSize.Large) : 0f;
            var disputeWidth = view.CanDispute ? Button.WidthFor(view.DisputeLabel, ButtonSize.Large) : 0f;
            var buttonRows = LedgerEntryLayout.ButtonRows(width - pad * 2f, confirmWidth, disputeWidth, scale);
            var height = LedgerEntryLayout.Height(titleLine, partiesLine, stateLine, buttonRows, scale);
            var max = new Vector2(origin.X + width, origin.Y + height);
            var card = new Rect(origin, max);
            var tone = view.Tone switch
            {
                LedgerTone.Waiting => TableLedger.Amber,
                LedgerTone.Disputed => ui.Accent,
                _ => TableLedger.SettledInk,
            };
            ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
            if (view.Tone != LedgerTone.Settled)
            {
                Squircle.Stroke(drawList, origin, max, Metrics.Radius.Grouped * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(tone, 0.65f)), 1.2f * scale);
            }

            var top = origin.Y + pad;
            var amountSize = Typography.Measure(view.Amount, TextStyles.SubheadlineEmphasized);
            Typography.Draw(drawList, new Vector2(max.X - pad - amountSize.X, top), view.Amount,
                TableRow.CurrencyTint(CasinoCurrencies.Gil, ui.Accent), TextStyles.SubheadlineEmphasized);
            var textWidth = MathF.Max(1f, width - pad * 3f - amountSize.X);
            var tagWidth = view.Tag.Length > 0
                ? Typography.Measure(view.Tag, TextStyles.FootnoteEmphasized).X + TagPadX * 2f * scale
                : 0f;
            var titleWidth = tagWidth > 0f ? MathF.Max(1f, textWidth - tagWidth - TagGap * scale) : textWidth;
            var title = Typography.FitText(view.Title, titleWidth, TextStyles.SubheadlineEmphasized);
            Typography.Draw(drawList, new Vector2(origin.X + pad, top), title, ui.TitleInk,
                TextStyles.SubheadlineEmphasized);
            if (tagWidth > 0f)
            {
                var tagLeft = origin.X + pad + Typography.Measure(title, TextStyles.SubheadlineEmphasized).X
                    + TagGap * scale;
                DrawTag(drawList, ui, view.Tag, new Vector2(tagLeft, top + titleLine * 0.5f), tagWidth, scale);
            }

            Typography.Draw(drawList, new Vector2(origin.X + pad, top + titleLine + LedgerEntryLayout.LineGap * scale),
                Typography.FitText(view.Parties, width - pad * 2f, TextStyles.Footnote), ui.BodyInk,
                TextStyles.Footnote);
            Typography.Draw(drawList,
                new Vector2(origin.X + pad, LedgerEntryLayout.StateTop(origin.Y, titleLine, partiesLine, scale)),
                Typography.FitText(view.State, width - pad * 2f, TextStyles.FootnoteEmphasized), tone,
                TextStyles.FootnoteEmphasized);
            var press = DrawButtons(drawList, ui, view, card, buttonRows, confirmWidth, disputeWidth, enabled, scale);
            if (press != LedgerEntryPress.None)
            {
                action = new LedgerEntryAction(press, view.EntryId, view.DisputeLabel);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        }

        return action;
    }

    private static void DrawTag(ImDrawListPtr drawList, AppSkin ui, string tag, Vector2 leftCenter, float width,
        float scale)
    {
        var height = Typography.LineHeight(TextStyles.FootnoteEmphasized) + TagPadY * 2f * scale;
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(leftCenter.X + width, leftCenter.Y + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, TagFillAlpha)));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, tag, ui.BodyInk, TextStyles.FootnoteEmphasized);
    }

    private static LedgerEntryPress DrawButtons(ImDrawListPtr drawList, AppSkin ui, in LedgerEntryView view,
        in Rect card, int buttonRows, float confirmWidth, float disputeWidth, bool enabled, float scale)
    {
        if (buttonRows == 0)
        {
            return LedgerEntryPress.None;
        }

        var press = LedgerEntryPress.None;
        var stacked = buttonRows > 1;
        var right = card.Max.X - LedgerEntryLayout.Pad * scale;
        if (view.CanDispute)
        {
            var rect = stacked
                ? LedgerEntryLayout.StackedButton(card, 1, buttonRows, scale)
                : LedgerEntryLayout.RowButton(card, right, disputeWidth, scale);
            if (Button.Draw(drawList, rect, view.DisputeLabel, ui.Ink, ButtonStyle.Gray, ButtonRole.Destructive,
                    enabled, id: "ledger.dispute"))
            {
                press = LedgerEntryPress.Dispute;
            }

            right = rect.Min.X - LedgerEntryLayout.ButtonGap * scale;
        }

        if (!view.CanConfirm)
        {
            return press;
        }

        var confirmRect = stacked
            ? LedgerEntryLayout.StackedButton(card, 0, buttonRows, scale)
            : LedgerEntryLayout.RowButton(card, right, confirmWidth, scale);
        if (Button.Draw(drawList, confirmRect, view.ConfirmLabel, ui.Ink, ButtonStyle.Prominent, enabled: enabled,
                id: "ledger.confirm"))
        {
            press = LedgerEntryPress.Confirm;
        }

        return press;
    }
}
