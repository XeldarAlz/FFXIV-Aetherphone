using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino;

internal readonly record struct BuyInBounds(long WalletCoins, long Rate)
{
    public static BuyInBounds Of(CasinoStateDto? state, long walletCoins) =>
        new(Math.Max(0, walletCoins), CasinoCashier.Rate(state));

    public bool Allows(long coins) => coins >= ChipsAmounts.MinimumCoins && coins <= WalletCoins;

    public long ChipsFor(long coins) => coins <= 0 ? 0 : coins * Rate;
}

internal sealed class CashierBuyIn
{
    private readonly CasinoStore store;
    private readonly CasinoTextCache texts = new();
    private readonly CashierField amount = new("##cashierBuyCoins");
    private readonly long[] tiles = new long[CashierAmounts.TileCount];

    public CashierBuyIn(CasinoStore store)
    {
        this.store = store;
    }

    public CashierField Field => amount;

    public long Coins => amount.Coins;

    public void Reset(long suggestedChips)
    {
        amount.Set(ChipsAmounts.CoinsFor(suggestedChips, store.Rate));
    }

    public void Clear()
    {
        amount.Clear();
    }

    public int Tiles(in BuyInBounds bounds) => CashierAmounts.BuyTiles(bounds.WalletCoins, tiles);

    public long Tile(int index) => tiles[index];

    public string TileLabel(int index) => index == CashierAmounts.MaxTileIndex
        ? Loc.T(L.CashierDesk.TileMax)
        : NumberText.Compact(tiles[index]);

    public void Pick(int index)
    {
        amount.Set(tiles[index]);
    }

    public CashierAmountState State(in BuyInBounds bounds) => CashierAmounts.BuyState(Coins, bounds.WalletCoins);

    public string Status(in BuyInBounds bounds, CashierAmountState state, out CashierTone tone)
    {
        switch (state)
        {
            case CashierAmountState.Ready:
                tone = CashierTone.Ready;
                return texts.Number(L.CashierDesk.BuyReady, bounds.ChipsFor(Coins));
            case CashierAmountState.Over:
                tone = CashierTone.Warn;
                return texts.Number(L.CashierDesk.BuyOver, bounds.WalletCoins);
        }

        if (bounds.WalletCoins < ChipsAmounts.MinimumCoins)
        {
            tone = CashierTone.Warn;
            return Loc.T(L.Casino.NotEnoughCoins);
        }

        tone = CashierTone.Idle;
        return Loc.T(L.CashierDesk.BuyIdle);
    }

    public string Primary(in BuyInBounds bounds, CashierAmountState state, bool compact)
    {
        if (state != CashierAmountState.Ready)
        {
            return Loc.T(L.Chips.BuyHeading);
        }

        var chips = bounds.ChipsFor(Coins);
        return compact ? texts.Compact(L.CashierDesk.BuyAction, chips) : texts.Number(L.CashierDesk.BuyAction, chips);
    }

    public CashierPreview Preview(in BuyInBounds bounds, long stackChips) =>
        CashierAmounts.BuyPreview(Coins, bounds.Rate, bounds.WalletCoins, stackChips);
}
