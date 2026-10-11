using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierCashOut
{
    private readonly CasinoStore store;
    private readonly ConfirmService confirm;
    private readonly CasinoTextCache texts = new();
    private readonly ChipValueText value = new();
    private readonly Action closeSitting;

    public CashierCashOut(CasinoStore store, ConfirmService confirm)
    {
        this.store = store;
        this.confirm = confirm;
        closeSitting = store.CloseSitting;
    }

    public void Draw(ImDrawListPtr drawList, AppSkin ui, in CashierLayout layout, long stack, long rate,
        bool interactive, bool overlay)
    {
        var line = layout.CashLine;
        var amount = value.Full(stack, rate);
        var amountSize = CurrencyGlyph.MeasureAmount(amount, TextStyles.Title3);
        var labelHeight = Typography.LineHeight(TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(line.Min.X, line.Center.Y - labelHeight * 0.5f),
            Typography.FitText(Loc.T(L.Chips.YouGet), MathF.Max(1f, line.Width - amountSize.X - CashierLayout.Gap
                * UiScale.Current), TextStyles.Subheadline), ui.BodyInk, TextStyles.Subheadline);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(line.Max.X - amountSize.X, line.Center.Y - amountSize.Y * 0.5f),
            amount, CurrencyKind.Coins, CasinoColors.Money, TextStyles.Title3);

        var enabled = interactive && stack > 0 && !store.MovingMoney;
        if (Button.Draw(drawList, layout.CashOut, Loc.T(L.Casino.CashOut), ui.Ink, ButtonStyle.Tinted,
                enabled: enabled, overlay: overlay, id: "cashier.cashout"))
        {
            Press();
        }
    }

    public string ResultLine(CasinoSittingResultDto result) => texts.Number(L.Strip.CashedOut, result.ConvertedCoins);

    private void Press()
    {
        if ((store.State?.AtRisk ?? 0) <= 0)
        {
            store.CloseSitting();
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Chips.RoundOpenTitle),
            Message = Loc.T(L.Chips.RoundOpenBody),
            ConfirmLabel = Loc.T(L.Casino.CashOut),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = closeSitting,
        });
    }
}
