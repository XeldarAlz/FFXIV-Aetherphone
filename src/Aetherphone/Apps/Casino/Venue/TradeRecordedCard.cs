using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal enum TradeCardPress : byte
{
    None,
    Undo,
    Close,
}

internal sealed class TradeRecordedCard
{
    private const float Pad = 16f;
    private const float BottomInset = 96f;
    private const float Gap = 8f;

    private readonly CasinoTradeSync trade;
    private readonly CasinoTextCache texts = new();

    private Rect shield;

    public TradeRecordedCard(CasinoTradeSync trade)
    {
        this.trade = trade;
    }

    public bool Visible => trade.RecordedAt(Environment.TickCount64) is not null;

    public void Gate()
    {
        if (Visible && shield.Width > 0f && UiInteract.HoverWindowOnly(shield.Min, shield.Max, false))
        {
            UiInteract.BlockThisFrame();
        }
    }

    public static bool ButtonsStack(float rowWidth, float undoWidth, float closeWidth, float gap) =>
        undoWidth + gap + closeWidth > rowWidth + 0.5f;

    public void Draw(Rect screen, AppSkin ui)
    {
        Tables.TableLedger.Toast(trade.TakeUndoOutcome());
        var shown = trade.RecordedAt(Environment.TickCount64);
        if (shown is null)
        {
            shield = default;
            return;
        }

        var title = texts.Named(L.Venue.TradeRecorded, Loc.T(CasinoGilLedger.KindLabel(shown.Entry.Kind)));
        var body = texts.NamedNumber(shown.Incoming ? L.Venue.TradeReceivedFrom : L.Venue.TradeSentTo,
            shown.PartnerName, shown.Amount);
        switch (DrawCard(screen, ui, title, body))
        {
            case TradeCardPress.Undo:
                trade.Undo();
                break;
            case TradeCardPress.Close:
                trade.DismissRecorded();
                break;
        }
    }

    private TradeCardPress DrawCard(Rect screen, AppSkin ui, string title, string body)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var pad = Pad * scale;
        var gap = Gap * scale;
        var width = screen.Width - pad * 2f;
        var inner = width - pad * 2f;
        var titleBlock = Typography.MeasureWrappedBlock(title, TextStyles.Headline, inner);
        var bodyBlock = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, inner);
        var buttonHeight = Button.LargeHeight * scale;
        var undoLabel = Loc.T(L.Venue.TradeUndo);
        var closeLabel = Loc.T(L.Venue.TradeOk);
        var undoWidth = Button.WidthFor(undoLabel, ButtonSize.Large);
        var closeWidth = Button.WidthFor(closeLabel, ButtonSize.Large);
        var stacked = ButtonsStack(inner, undoWidth, closeWidth, gap);
        var buttons = stacked ? buttonHeight * 2f + gap : buttonHeight;
        var height = pad + titleBlock.Y + gap + bodyBlock.Y + gap * 1.5f + buttons + pad;
        var bottom = screen.Max.Y - BottomInset * scale;
        var min = new Vector2(screen.Min.X + pad, bottom - height);
        var max = new Vector2(screen.Max.X - pad, bottom);
        shield = new Rect(min, max);
        Material.LiquidGlass(drawList, min, max, Metrics.Radius.Grouped * scale, scale, GlassTone.Dark, 0f);
        var left = min.X + pad;
        var right = max.X - pad;
        var top = min.Y + pad;
        Typography.DrawWrappedLeft(new Vector2(left, top), title, Stage.CasinoColors.InkTitle, TextStyles.Headline,
            inner);
        top += titleBlock.Y + gap;
        Typography.DrawWrappedLeft(new Vector2(left, top), body, Stage.CasinoColors.InkBody, TextStyles.Subheadline,
            inner);
        top += bodyBlock.Y + gap * 1.5f;
        var closeRect = stacked
            ? new Rect(new Vector2(left, top), new Vector2(right, top + buttonHeight))
            : new Rect(new Vector2(right - closeWidth, top), new Vector2(right, top + buttonHeight));
        var undoTop = stacked ? closeRect.Max.Y + gap : top;
        var undoRect = stacked
            ? new Rect(new Vector2(left, undoTop), new Vector2(right, undoTop + buttonHeight))
            : new Rect(new Vector2(left, top), new Vector2(right - closeWidth - gap, top + buttonHeight));
        var press = TradeCardPress.None;
        if (Button.Draw(drawList, closeRect, closeLabel, ui.Ink, ButtonStyle.Prominent, overlay: true,
                id: "venue.trade.ok"))
        {
            press = TradeCardPress.Close;
        }

        if (Button.Draw(drawList, undoRect, undoLabel, ui.Ink, ButtonStyle.Gray, enabled: !trade.Undoing,
                overlay: true, id: "venue.trade.undo"))
        {
            press = TradeCardPress.Undo;
        }

        return press;
    }
}
