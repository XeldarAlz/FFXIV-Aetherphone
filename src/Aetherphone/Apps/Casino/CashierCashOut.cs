using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal readonly record struct CashOutSplit(long Stack, long NowCoins, long WaitChips, long AllowanceCoins,
    long DailyCapCoins, bool Capped)
{
    public static CashOutSplit Of(CasinoStateDto? state)
    {
        var stack = state?.Sitting?.Stack ?? 0;
        var rate = CasinoCashier.Rate(state);
        var cashier = state?.Cashier;
        if (cashier is null || cashier.DailyNetCashOutCoins <= 0)
        {
            return new CashOutSplit(stack, CasinoCashier.WholeCoins(stack, rate), 0, 0, 0, false);
        }

        return new CashOutSplit(stack, CasinoCashier.ConvertsNow(stack, cashier.AllowanceCoins, rate),
            CasinoCashier.WaitsChips(stack, cashier.AllowanceCoins, rate), Math.Max(0, cashier.AllowanceCoins),
            cashier.DailyNetCashOutCoins, true);
    }

    public float AllowanceFraction => DailyCapCoins <= 0 ? 0f : Math.Clamp((float)AllowanceCoins / DailyCapCoins, 0f, 1f);
}

internal sealed class CashierCashOut
{
    private const float RowHeight = 24f;
    private const float BarHeight = 6f;
    private const float Gap = 8f;
    private const float ButtonHeight = Button.LargeHeight;

    private readonly CasinoStore store;
    private readonly ConfirmService confirm;
    private readonly CasinoTextCache texts = new();

    public CashierCashOut(CasinoStore store, ConfirmService confirm)
    {
        this.store = store;
        this.confirm = confirm;
    }

    public float Height(in CashOutSplit split, float innerWidth, float scale)
    {
        var rows = split.WaitChips > 0 ? 2 : 1;
        var height = rows * RowHeight * scale + (Gap + ButtonHeight) * scale;
        if (!split.Capped)
        {
            return height;
        }

        var hint = AllowanceHint(split);
        return height + (Gap + BarHeight + Gap * 0.5f) * scale + Typography.LineHeight(TextStyles.Footnote)
            + Gap * 0.5f * scale + Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, innerWidth).Y;
    }

    public float Draw(ImDrawListPtr drawList, AppSkin ui, in CashOutSplit split, float left, float top, float width,
        float scale, bool overlay, bool interactive)
    {
        var y = top;
        y = DrawLine(drawList, ui, Loc.T(L.Strip.ConvertsNow), texts.Number(L.Strip.CoinsAmount, split.NowCoins),
            CurrencyKind.Coins, CasinoColors.Money, left, y, width, scale);
        if (split.WaitChips > 0)
        {
            y = DrawLine(drawList, ui, Loc.T(L.Strip.WaitsTomorrow),
                texts.Number(L.Strip.ChipsAmount, split.WaitChips), CurrencyKind.Chips, ui.BodyInk, left, y, width,
                scale);
        }

        if (split.Capped)
        {
            y += Gap * scale;
            CoinArt.Bar(drawList, new Vector2(left, y), new Vector2(left + width, y + BarHeight * scale),
                split.AllowanceFraction, Palette.WithAlpha(ui.MutedInk, 0.25f), CasinoColors.Money);
            y += (BarHeight + Gap * 0.5f) * scale;
            var allowance = texts.Numbers(L.Strip.AllowanceLeft, split.AllowanceCoins, split.DailyCapCoins);
            Typography.Draw(drawList, new Vector2(left, y),
                Typography.FitText(allowance, width, TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);
            y += Typography.LineHeight(TextStyles.Footnote) + Gap * 0.5f * scale;
            y += Typography.DrawWrappedLeft(new Vector2(left, y), AllowanceHint(split), ui.MutedInk,
                TextStyles.Footnote, width);
        }

        y += Gap * scale;
        var rect = new Rect(new Vector2(left, y), new Vector2(left + width, y + ButtonHeight * scale));
        var enabled = interactive && split.Stack > 0 && !store.MovingMoney;
        if (Button.Draw(drawList, rect, Loc.T(L.Casino.CashOut), ui.Ink, ButtonStyle.Tinted, enabled: enabled,
                overlay: overlay, id: "cashier.cashout"))
        {
            Ask(split);
        }

        return rect.Max.Y;
    }

    public void Ask(in CashOutSplit split)
    {
        var stackText = NumberText.Group(split.Stack);
        var nowText = NumberText.Group(split.NowCoins);
        var body = split.WaitChips > 0
            ? Loc.T(L.Strip.CashOutSplitBody, nowText, NumberText.Group(split.WaitChips))
            : Loc.T(L.Strip.CashOutWholeBody, nowText);
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Strip.CashOutConfirmTitle, stackText),
            Message = body,
            ConfirmLabel = Loc.T(L.Casino.CashOut),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = store.CloseSitting,
        });
    }

    public string ResultLine(CasinoSittingResultDto result)
    {
        if (result.QueuedChips > 0)
        {
            return texts.Numbers(L.Strip.CashedOutQueued, result.ConvertedCoins, result.QueuedChips);
        }

        return texts.Number(L.Strip.CashedOut, result.ConvertedCoins);
    }

    private static string AllowanceHint(in CashOutSplit split)
    {
        return split.AllowanceCoins <= 0 ? Loc.T(L.Strip.AllowanceSpent) : Loc.T(L.Strip.AllowanceHint);
    }

    private static float DrawLine(ImDrawListPtr drawList, AppSkin ui, string label, string amount, CurrencyKind kind,
        Vector4 ink, float left, float top, float width, float scale)
    {
        var height = RowHeight * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Subheadline);
        var amountSize = CurrencyGlyph.MeasureAmount(amount, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, top + (height - labelHeight) * 0.5f),
            Typography.FitText(label, MathF.Max(1f, width - amountSize.X - Gap * scale), TextStyles.Subheadline),
            ui.BodyInk, TextStyles.Subheadline);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(left + width - amountSize.X, top + (height - amountSize.Y) * 0.5f),
            amount, kind, ink, TextStyles.SubheadlineEmphasized);
        return top + height;
    }
}
