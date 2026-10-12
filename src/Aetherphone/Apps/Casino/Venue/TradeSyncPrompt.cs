using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal enum TradePromptPress : byte
{
    None,
    Confirm,
    Dismiss,
}

internal sealed class TradeSyncPrompt
{
    private const float Pad = 16f;
    private const float BottomInset = 96f;
    private const float Gap = 8f;

    private readonly CasinoTradeSync trade;
    private readonly CasinoTextCache texts = new();

    private Rect shield;

    public TradeSyncPrompt(CasinoTradeSync trade)
    {
        this.trade = trade;
    }

    public void Gate()
    {
        if (Visible && shield.Width > 0f && UiInteract.HoverWindowOnly(shield.Min, shield.Max, false))
        {
            UiInteract.BlockThisFrame();
        }
    }

    public bool Visible => trade.Pending.Kind != TradeProposalKind.None || trade.PendingGuest is not null;

    public static bool ButtonsStack(float rowWidth, float confirmWidth, float dismissWidth, float gap) =>
        confirmWidth + gap + dismissWidth > rowWidth + 0.5f;

    public void Draw(Rect screen, AppSkin ui)
    {
        Tables.TableLedger.Toast(trade.TakeOutcome());
        Tables.TableLedger.Toast(trade.TakeGuestOutcome());
        var proposal = trade.Pending;
        if (proposal.Kind != TradeProposalKind.None)
        {
            DrawProposal(screen, ui, proposal);
            return;
        }

        var guest = trade.PendingGuest;
        if (guest is not null)
        {
            DrawGuest(screen, ui, guest);
        }
    }

    private void DrawProposal(Rect screen, AppSkin ui, in TradeProposal proposal)
    {
        var body = texts.NamedNumber(proposal.Incoming ? L.Venue.TradeReceivedFrom : L.Venue.TradeSentTo,
            proposal.CounterpartyName, proposal.Amount);
        var confirmLabel = Loc.T(proposal.Kind == TradeProposalKind.Confirm
            ? L.Venue.TradeConfirmEntry
            : string.Equals(proposal.LedgerKind, CasinoLedgerKinds.BuyIn, StringComparison.Ordinal)
                ? L.Venue.TradeRecordBuyIn
                : L.Venue.TradeRecordPayout);
        switch (DrawCard(screen, ui, body, string.Empty, confirmLabel, trade.Busy))
        {
            case TradePromptPress.Confirm:
                trade.Accept();
                break;
            case TradePromptPress.Dismiss:
                trade.Dismiss();
                break;
        }
    }

    private void DrawGuest(Rect screen, AppSkin ui, GuestOffer guest)
    {
        var body = texts.NamedNumber(guest.Incoming ? L.Venue.TradeReceivedFrom : L.Venue.TradeSentTo,
            guest.GuestName, guest.Amount);
        var hint = Loc.T(guest.Unchecked ? L.Book.GuestHintUnchecked : L.Book.GuestHint);
        switch (DrawCard(screen, ui, body, hint, Loc.T(GuestLedger.RecordLabel(guest.LedgerKind)), trade.GuestBusy))
        {
            case TradePromptPress.Confirm:
                trade.RecordGuest();
                break;
            case TradePromptPress.Dismiss:
                trade.DismissGuest();
                break;
        }
    }

    private TradePromptPress DrawCard(Rect screen, AppSkin ui, string body, string hint, string confirmLabel,
        bool busy)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var pad = Pad * scale;
        var width = screen.Width - pad * 2f;
        var inner = width - pad * 2f;
        var title = Loc.T(L.Venue.TradeNotifyTitle);
        var bodyBlock = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, inner);
        var hintBlock = hint.Length > 0 ? Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, inner).Y : 0f;
        var hintGap = hint.Length > 0 ? Gap * scale : 0f;
        var buttonHeight = Button.LargeHeight * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var dismissLabel = Loc.T(L.Venue.NotNow);
        var dismissWidth = Button.WidthFor(dismissLabel, ButtonSize.Large);
        var stacked = ButtonsStack(inner, Button.WidthFor(confirmLabel, ButtonSize.Large), dismissWidth,
            Gap * scale);
        var buttons = stacked ? buttonHeight * 2f + Gap * scale : buttonHeight;
        var height = pad + titleHeight + Gap * scale + bodyBlock.Y + hintGap + hintBlock + Gap * 1.5f * scale
            + buttons + pad;
        var bottom = screen.Max.Y - BottomInset * scale;
        var min = new Vector2(screen.Min.X + pad, bottom - height);
        var max = new Vector2(screen.Max.X - pad, bottom);
        shield = new Rect(min, max);
        var radius = Metrics.Radius.Grouped * scale;
        Material.LiquidGlass(drawList, min, max, radius, scale, GlassTone.Dark, 0f);
        var left = min.X + pad;
        var top = min.Y + pad;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(title, inner, TextStyles.Headline),
            Stage.CasinoColors.InkTitle, TextStyles.Headline);
        top += titleHeight + Gap * scale;
        Typography.DrawWrappedLeft(new Vector2(left, top), body, Stage.CasinoColors.InkBody, TextStyles.Subheadline,
            inner);
        top += bodyBlock.Y;
        if (hint.Length > 0)
        {
            top += hintGap;
            Typography.DrawWrappedLeft(new Vector2(left, top), hint, Stage.CasinoColors.InkMuted, TextStyles.Footnote,
                inner);
            top += hintBlock;
        }

        top += Gap * 1.5f * scale;
        var right = max.X - pad;
        var confirmRect = stacked
            ? new Rect(new Vector2(left, top), new Vector2(right, top + buttonHeight))
            : new Rect(new Vector2(left, top), new Vector2(right - dismissWidth - Gap * scale, top + buttonHeight));
        var dismissTop = stacked ? confirmRect.Max.Y + Gap * scale : top;
        var dismissRect = stacked
            ? new Rect(new Vector2(left, dismissTop), new Vector2(right, dismissTop + buttonHeight))
            : new Rect(new Vector2(right - dismissWidth, top), new Vector2(right, top + buttonHeight));
        var press = TradePromptPress.None;
        if (Button.Draw(drawList, confirmRect, confirmLabel, ui.Ink, ButtonStyle.Prominent, enabled: !busy,
                overlay: true, id: "venue.trade.confirm"))
        {
            press = TradePromptPress.Confirm;
        }

        if (Button.Draw(drawList, dismissRect, dismissLabel, ui.Ink, ButtonStyle.Gray, overlay: true,
                id: "venue.trade.dismiss"))
        {
            press = TradePromptPress.Dismiss;
        }

        return press;
    }
}
