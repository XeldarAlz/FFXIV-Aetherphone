using System.Globalization;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal readonly record struct BuyInBounds(long MinCoins, long MaxCoins, long WalletCoins, long RoomCoins,
    long Rate, bool TopUp)
{
    public static BuyInBounds Of(CasinoStateDto? state, long walletCoins)
    {
        var rate = CasinoCashier.Rate(state);
        var topUp = state?.Sitting is not null;
        var minBuyIn = state is { MinBuyIn: > 0 } ? state.MinBuyIn : CasinoHostingRules.ChipMinBuyIn;
        var maxBuyIn = state is { MaxBuyIn: > 0 } ? state.MaxBuyIn : CasinoHostingRules.ChipMaxBuyIn;
        var minCoins = topUp ? 1 : (minBuyIn + rate - 1) / rate;
        var room = topUp ? Math.Max(0, maxBuyIn - (state?.Sitting?.ChipsIn ?? 0)) : maxBuyIn;
        var roomCoins = room / rate;
        var wallet = Math.Max(0, walletCoins);
        return new BuyInBounds(minCoins, Math.Min(wallet, roomCoins), wallet, roomCoins, rate, topUp);
    }

    public bool Allows(long coins) => coins >= MinCoins && coins <= MaxCoins;

    public long ChipsFor(long coins) => coins <= 0 ? 0 : coins * Rate;
}

internal sealed class CashierBuyIn
{
    private const float FieldHeight = 40f;
    private const float Gap = 8f;
    private const float ButtonHeight = Button.LargeHeight;
    private const int FieldDigits = 9;

    private static readonly long[] QuickCoins = { 20, 50, 100, 250, 500, 1_000, 5_000 };

    private readonly CasinoStore store;
    private readonly ConfirmService confirm;
    private readonly CasinoTextCache texts = new();
    private readonly ChipRail rail = new();
    private readonly string[] quickLabels = new string[QuickCoins.Length];
    private readonly bool[] quickActive = new bool[QuickCoins.Length];
    private LanguageInfo? quickLanguage;
    private string buffer = string.Empty;

    public CashierBuyIn(CasinoStore store, ConfirmService confirm)
    {
        this.store = store;
        this.confirm = confirm;
    }

    public long Coins =>
        long.TryParse(buffer, NumberStyles.None, CultureInfo.InvariantCulture, out var coins) && coins > 0 ? coins : 0;

    public void Reset(long suggestedChips)
    {
        rail.Reset();
        var coins = suggestedChips <= 0 ? 0 : (suggestedChips + store.Rate - 1) / store.Rate;
        buffer = coins > 0 ? coins.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    public void Clear()
    {
        buffer = string.Empty;
    }

    public float Height(float scale)
    {
        return Typography.LineHeight(TextStyles.FootnoteEmphasized) + Gap * scale + FieldHeight * scale
            + Gap * scale + ChipRail.RowHeight * scale + Gap * scale + Typography.LineHeight(TextStyles.Footnote)
            + Gap * scale + ButtonHeight * scale;
    }

    public float Draw(ImDrawListPtr drawList, AppSkin ui, in BuyInBounds bounds, float left, float top, float width,
        float scale, bool interactive)
    {
        var y = top;
        var heading = bounds.TopUp ? Loc.T(L.Casino.TopUp) : Loc.T(L.Strip.GetChips);
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(heading, width * 0.5f,
            TextStyles.FootnoteEmphasized), ui.BodyInk, TextStyles.FootnoteEmphasized);
        DrawRate(drawList, ui, bounds.Rate, new Vector2(left + width, y));
        y += Typography.LineHeight(TextStyles.FootnoteEmphasized) + Gap * scale;

        var field = new Rect(new Vector2(left, y), new Vector2(left + width, y + FieldHeight * scale));
        DrawField(drawList, ui, field, interactive);
        y = field.Max.Y + Gap * scale;

        RefreshQuickLabels();
        var coins = Coins;
        for (var index = 0; index < QuickCoins.Length; index++)
        {
            quickActive[index] = QuickCoins[index] == coins;
        }

        var railRect = new Rect(new Vector2(left, y), new Vector2(left + width, y + ChipRail.RowHeight * scale));
        var tapped = rail.Draw(railRect, ui, quickLabels, quickActive, true, null, ChipRail.CompactLabelPadding,
            interactive: interactive);
        if (tapped >= 0)
        {
            buffer = QuickCoins[tapped].ToString(CultureInfo.InvariantCulture);
            coins = QuickCoins[tapped];
        }

        y = railRect.Max.Y + Gap * scale;
        var allowed = bounds.Allows(coins);
        var line = LineFor(bounds, coins, out var lineIsWarning);
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(line, width, TextStyles.Footnote),
            lineIsWarning ? ui.Accent : ui.BodyInk, TextStyles.Footnote);
        y += Typography.LineHeight(TextStyles.Footnote) + Gap * scale;

        var chips = bounds.ChipsFor(coins);
        var label = allowed ? texts.Compact(L.Strip.GetChipsFor, chips) : Loc.T(L.Strip.GetChips);
        var rect = new Rect(new Vector2(left, y), new Vector2(left + width, y + ButtonHeight * scale));
        if (Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Prominent,
                enabled: interactive && allowed && !store.MovingMoney, overlay: true, id: "cashier.getchips"))
        {
            Ask(bounds, coins);
        }

        return rect.Max.Y;
    }

    private string LineFor(in BuyInBounds bounds, long coins, out bool warning)
    {
        warning = false;
        if (bounds.MaxCoins < bounds.MinCoins)
        {
            warning = true;
            return Loc.T(L.Casino.NotEnoughCoins);
        }

        if (coins <= 0)
        {
            return texts.Numbers(L.Strip.BuyInRange, bounds.MinCoins, bounds.MaxCoins);
        }

        if (coins < bounds.MinCoins)
        {
            warning = true;
            return texts.Number(L.Strip.BuyInAtLeast, bounds.MinCoins);
        }

        if (coins > bounds.WalletCoins)
        {
            warning = true;
            return Loc.T(L.Casino.NotEnoughCoins);
        }

        if (coins > bounds.RoomCoins)
        {
            warning = true;
            return texts.Number(L.Strip.BuyInAtMost, bounds.RoomCoins);
        }

        return texts.Number(L.Strip.BecomesChips, bounds.ChipsFor(coins));
    }

    private void Ask(in BuyInBounds bounds, long coins)
    {
        var chips = bounds.ChipsFor(coins);
        var topUp = bounds.TopUp;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Strip.GetChipsConfirmTitle, NumberText.Group(coins)),
            Message = Loc.T(L.Strip.GetChipsConfirmBody, NumberText.Group(chips)),
            ConfirmLabel = topUp ? Loc.T(L.Casino.TopUp) : Loc.T(L.Strip.GetChips),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = () =>
            {
                if (topUp)
                {
                    store.TopUp(chips);
                    return;
                }

                store.OpenSitting(chips);
            },
        });
    }

    private void DrawField(ImDrawListPtr drawList, AppSkin ui, Rect field, bool interactive)
    {
        SearchBar.Surface(drawList, field, ui.Ink);
        var capsule = SearchBar.Capsule(field);
        var inset = capsule.Height * 0.4f;
        var glyph = Typography.LineHeight(TextStyles.Body) * CurrencyGlyph.GlyphFraction;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, new Vector2(capsule.Min.X + inset + glyph * 0.5f,
            field.Center.Y), glyph);
        var textLeft = capsule.Min.X + inset + glyph + inset * 0.5f;
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(textLeft, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(capsule.Max.X - inset - textLeft);
        using (ImRaii.Disabled(!interactive))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##cashierCoins", Loc.T(L.Strip.CoinsFieldHint), ref buffer, FieldDigits + 1,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }

        ImGui.SetCursorScreenPos(cursor);
    }

    private static void DrawRate(ImDrawListPtr drawList, AppSkin ui, long rate, Vector2 topRight)
    {
        var chipsText = NumberText.Group(rate);
        var coinsText = NumberText.Group(1L);
        const string equalsText = " = ";
        var chipsSize = CurrencyGlyph.MeasureAmount(chipsText, TextStyles.Footnote);
        var equalsSize = Typography.Measure(equalsText, TextStyles.Footnote);
        var coinsSize = CurrencyGlyph.MeasureAmount(coinsText, TextStyles.Footnote);
        var x = topRight.X - chipsSize.X - equalsSize.X - coinsSize.X;
        x += CurrencyGlyph.DrawAmount(drawList, new Vector2(x, topRight.Y), chipsText, CurrencyKind.Chips,
            ui.BodyInk, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(x, topRight.Y), equalsText, ui.BodyInk, TextStyles.Footnote);
        x += equalsSize.X;
        CurrencyGlyph.DrawAmount(drawList, new Vector2(x, topRight.Y), coinsText, CurrencyKind.Coins, ui.MutedInk,
            TextStyles.Footnote);
    }

    private void RefreshQuickLabels()
    {
        if (ReferenceEquals(quickLanguage, Loc.Current) && quickLabels[0] is not null)
        {
            return;
        }

        quickLanguage = Loc.Current;
        for (var index = 0; index < QuickCoins.Length; index++)
        {
            quickLabels[index] = Loc.T(L.Strip.CoinsShort, NumberText.Compact(QuickCoins[index]));
        }
    }
}
