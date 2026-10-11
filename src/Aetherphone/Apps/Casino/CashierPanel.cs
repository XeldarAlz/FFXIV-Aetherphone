using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierPanel
{
    private const float ArrowAlpha = 0.10f;
    private const float ArrowGlyphScale = 0.7f;
    private const float NoteFillAlpha = 0.10f;
    private const float NoteStrokeAlpha = 0.35f;
    private const string EqualsText = " = ";

    private readonly CasinoStore store;
    private readonly CoinStore coins;
    private readonly CashierBuyIn buyIn;
    private readonly CashierCashOut cashOut;
    private readonly ChipValueText worth = new();

    private string note = string.Empty;
    private bool noteGood;
    private int drawnFrame = int.MinValue;

    public CashierPanel(CasinoStore store, CoinStore coins, ConfirmService confirm)
    {
        this.store = store;
        this.coins = coins;
        buyIn = new CashierBuyIn(store);
        cashOut = new CashierCashOut(store, confirm);
    }

    public bool Visible => ImGui.GetFrameCount() - drawnFrame <= 1;

    public void Reset(long suggestedChips)
    {
        ClearNote();
        buyIn.Reset(suggestedChips);
    }

    public void ClearBuy() => buyIn.Clear();

    public void ShowNote(string text, bool good)
    {
        note = text;
        noteGood = good;
    }

    public void ClearNote()
    {
        note = string.Empty;
        noteGood = false;
    }

    public string CashOutLine(Core.Aethernet.Contracts.CasinoSittingResultDto result) => cashOut.ResultLine(result);

    public float Height(float width, float scale) => Compute(0f, 0f, width, scale, out _, out _).Bottom;

    public float Draw(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale,
        bool interactive, bool overlay)
    {
        drawnFrame = ImGui.GetFrameCount();
        var layout = Compute(left, top, width, scale, out var noticeTitle, out var noticeHint);
        var state = store.State;
        var stack = state?.Sitting?.Stack ?? 0;
        var rate = store.Rate;
        if (layout.HasNotice)
        {
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Card, noticeTitle, noticeHint, layout.Notice.Min.X,
                layout.Notice.Min.Y, layout.Notice.Width, scale);
        }

        DrawBalances(drawList, ui, layout, stack, rate, scale);
        if (layout.HasNote)
        {
            DrawNote(drawList, ui, layout.Note, scale);
        }

        if (layout.HasBuy)
        {
            buyIn.Draw(drawList, ui, layout, new BuyInBounds(WalletCoins, rate), interactive, overlay);
        }

        if (layout.HasCashOut)
        {
            drawList.AddLine(new Vector2(left, layout.Divider), new Vector2(left + width, layout.Divider),
                ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            cashOut.Draw(drawList, ui, layout, stack, rate, interactive, overlay);
        }

        return layout.Bottom;
    }

    private long WalletCoins => Math.Max(0, coins.Wallet?.Balance ?? 0);

    private CashierLayout Compute(float left, float top, float width, float scale, out string noticeTitle,
        out string noticeHint)
    {
        NoticeFor(out noticeTitle, out noticeHint);
        var notice = noticeTitle.Length > 0
            ? CasinoNotice.Height(CasinoNoticeKind.Card, noticeTitle, noticeHint, width, scale)
            : 0f;
        var noteBlock = note.Length > 0
            ? Typography.MeasureWrappedBlock(note, TextStyles.Footnote, width - CashierLayout.NotePad * 2f * scale).Y
            : 0f;
        var blocks = new CashierBlocks(Typography.LineHeight(TextStyles.Footnote),
            Typography.LineHeight(TextStyles.Title3), Typography.LineHeight(TextStyles.Footnote),
            Typography.LineHeight(TextStyles.Footnote), Typography.LineHeight(TextStyles.SubheadlineEmphasized),
            Typography.LineHeight(TextStyles.Footnote), Typography.LineHeight(TextStyles.Subheadline), notice,
            noteBlock);
        var buy = noticeTitle.Length == 0;
        var cash = store.State?.Sitting is not null;
        return CashierLayout.Compute(left, top, width, blocks, buy, cash, buyIn.TileCount(WalletCoins), scale);
    }

    private void NoticeFor(out string title, out string hint)
    {
        title = string.Empty;
        hint = string.Empty;
        var state = store.State;
        if (coins.Wallet?.FrozenUntilUnix is not null)
        {
            title = Loc.T(L.Coin.FrozenTitle);
            hint = Loc.T(L.Coin.FrozenHint);
            return;
        }

        if (state?.StakesPaused == true)
        {
            title = Loc.T(L.Casino.PausedTitle);
            hint = Loc.T(L.Casino.PausedHint);
            return;
        }

        if (state?.Draining == true)
        {
            title = Loc.T(L.Casino.DrainingTitle);
            hint = Loc.T(L.Casino.DrainingHint);
        }
    }

    private void DrawBalances(ImDrawListPtr drawList, AppSkin ui, in CashierLayout layout, long stack, long rate,
        float scale)
    {
        var card = layout.Balances;
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        DrawColumn(drawList, ui, layout.WalletColumn, CurrencyKind.Coins, Loc.T(L.Casino.WalletRow),
            NumberText.Group(WalletCoins), ui.TitleInk, string.Empty, scale);
        DrawColumn(drawList, ui, layout.ChipsColumn, CurrencyKind.Chips, Loc.T(L.Casino.ChipsRow),
            NumberText.Group(stack), stack > 0 ? CasinoColors.Money : ui.MutedInk, worth.Full(stack, rate), scale);
        var arrow = layout.ArrowSlot;
        drawList.AddCircleFilled(arrow.Center, arrow.Width * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, ArrowAlpha)), 28);
        AppSkin.Icon(drawList, arrow.Center, IconGlyph.Of(FontAwesomeIcon.ExchangeAlt), ui.BodyInk, ArrowGlyphScale);
        DrawRate(drawList, ui, layout.RateLine, rate);
    }

    private static void DrawColumn(ImDrawListPtr drawList, AppSkin ui, Rect column, CurrencyKind kind,
        string label, string amount, Vector4 amountInk, string coinValue, float scale)
    {
        var disc = CashierLayout.Disc * scale;
        CurrencyGlyph.Draw(drawList, kind, new Vector2(column.Center.X, column.Min.Y + disc * 0.5f), disc);
        var captionTop = column.Min.Y + disc + CashierLayout.DiscGap * scale;
        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.DrawCentered(drawList, new Vector2(column.Center.X, captionTop + captionHeight * 0.5f),
            Typography.FitText(label, column.Width, TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);
        var amountTop = captionTop + captionHeight;
        var amountHeight = Typography.LineHeight(TextStyles.Title3);
        var amountScale = Typography.FitScale(amount, column.Width, TextStyles.Title3.Scale,
            TextStyles.Footnote.Scale, TextStyles.Title3.Weight);
        var amountSize = Typography.Measure(amount, amountScale, TextStyles.Title3.Weight);
        Typography.Draw(drawList, new Vector2(column.Center.X - amountSize.X * 0.5f,
                amountTop + (amountHeight - amountSize.Y) * 0.5f), amount, amountInk, amountScale,
            TextStyles.Title3.Weight);
        if (coinValue.Length == 0)
        {
            return;
        }

        var worthTop = amountTop + amountHeight;
        var worthScale = Typography.FitScale(coinValue, column.Width, TextStyles.Footnote.Scale,
            TextStyles.Caption2.Scale, TextStyles.Footnote.Weight);
        var worthSize = Typography.Measure(coinValue, worthScale, TextStyles.Footnote.Weight);
        Typography.Draw(drawList, new Vector2(column.Center.X - worthSize.X * 0.5f, worthTop), coinValue,
            ui.MutedInk, worthScale, TextStyles.Footnote.Weight);
    }

    private static void DrawRate(ImDrawListPtr drawList, AppSkin ui, Rect line, long rate)
    {
        var chipsText = NumberText.Group(rate);
        var coinsText = NumberText.Group(1L);
        var chipsSize = CurrencyGlyph.MeasureAmount(chipsText, TextStyles.Footnote);
        var equalsSize = Typography.Measure(EqualsText, TextStyles.Footnote);
        var coinsSize = CurrencyGlyph.MeasureAmount(coinsText, TextStyles.Footnote);
        var x = line.Center.X - (chipsSize.X + equalsSize.X + coinsSize.X) * 0.5f;
        x += CurrencyGlyph.DrawAmount(drawList, new Vector2(x, line.Min.Y), chipsText, CurrencyKind.Chips,
            ui.BodyInk, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(x, line.Min.Y), EqualsText, ui.BodyInk, TextStyles.Footnote);
        x += equalsSize.X;
        CurrencyGlyph.DrawAmount(drawList, new Vector2(x, line.Min.Y), coinsText, CurrencyKind.Coins, ui.BodyInk,
            TextStyles.Footnote);
    }

    private void DrawNote(ImDrawListPtr drawList, AppSkin ui, Rect rect, float scale)
    {
        var tint = noteGood ? CasinoColors.Money : ui.Accent;
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, NoteFillAlpha)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(tint, NoteStrokeAlpha)), Metrics.Stroke.Hairline);
        var pad = CashierLayout.NotePad * scale;
        Typography.DrawWrappedLeft(new Vector2(rect.Min.X + pad, rect.Min.Y + pad), note, ui.TitleInk,
            TextStyles.Footnote, rect.Width - pad * 2f);
    }
}
