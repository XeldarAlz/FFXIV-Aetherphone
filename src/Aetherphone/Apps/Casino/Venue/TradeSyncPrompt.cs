using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

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
        if (Visible && shield.Width > 0f && ImGui.IsMouseHoveringRect(shield.Min, shield.Max, false))
        {
            UiInteract.BlockThisFrame();
        }
    }

    public bool Visible => trade.Pending.Kind != TradeProposalKind.None;

    public void Draw(Rect screen, AppSkin ui)
    {
        var proposal = trade.Pending;
        if (proposal.Kind == TradeProposalKind.None)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var pad = Pad * scale;
        var width = screen.Width - pad * 2f;
        var title = Loc.T(L.Venue.TradeNotifyTitle);
        var body = texts.NamedNumber(proposal.Incoming ? L.Venue.TradeReceivedFrom : L.Venue.TradeSentTo,
            proposal.CounterpartyName, proposal.Amount);
        var bodyBlock = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, width - pad * 2f);
        var buttonHeight = Button.LargeHeight * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var height = pad + titleHeight + Gap * scale + bodyBlock.Y + Gap * 1.5f * scale + buttonHeight + pad;
        var bottom = screen.Max.Y - BottomInset * scale;
        var min = new Vector2(screen.Min.X + pad, bottom - height);
        var max = new Vector2(screen.Max.X - pad, bottom);
        shield = new Rect(min, max);
        var radius = Metrics.Radius.Grouped * scale;
        Material.LiquidGlass(drawList, min, max, radius, scale, GlassTone.Dark, 0f);
        var left = min.X + pad;
        var top = min.Y + pad;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(title, width - pad * 2f,
            TextStyles.Headline), Stage.CasinoColors.InkTitle, TextStyles.Headline);
        top += titleHeight + Gap * scale;
        Typography.DrawWrappedLeft(new Vector2(left, top), body, Stage.CasinoColors.InkBody, TextStyles.Subheadline,
            width - pad * 2f);
        top += bodyBlock.Y + Gap * 1.5f * scale;
        var dismissLabel = Loc.T(L.Venue.NotNow);
        var dismissWidth = Button.WidthFor(dismissLabel, ButtonSize.Large);
        var confirmRect = new Rect(new Vector2(left, top),
            new Vector2(max.X - pad - dismissWidth - Gap * scale, top + buttonHeight));
        var dismissRect = new Rect(new Vector2(max.X - pad - dismissWidth, top), new Vector2(max.X - pad,
            top + buttonHeight));
        var confirmLabel = Loc.T(proposal.Kind == TradeProposalKind.Confirm
            ? L.Venue.TradeConfirmEntry
            : string.Equals(proposal.LedgerKind, CasinoLedgerKinds.BuyIn, StringComparison.Ordinal)
                ? L.Venue.TradeRecordBuyIn
                : L.Venue.TradeRecordPayout);
        if (Button.Draw(drawList, confirmRect, confirmLabel, ui.Ink, ButtonStyle.Prominent, enabled: !trade.Busy,
                overlay: true, id: "venue.trade.confirm"))
        {
            trade.Accept();
        }

        if (Button.Draw(drawList, dismissRect, dismissLabel, ui.Ink, ButtonStyle.Gray, overlay: true,
                id: "venue.trade.dismiss"))
        {
            trade.Dismiss();
        }
    }
}
