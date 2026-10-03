namespace Aetherphone.Core.Market;

internal readonly struct MarketTrade
{
    public readonly long Unix;
    public readonly int Price;
    public readonly int Quantity;
    public readonly bool Hq;

    public MarketTrade(long unix, int price, int quantity, bool hq)
    {
        Unix = unix;
        Price = price;
        Quantity = quantity;
        Hq = hq;
    }
}

internal enum MarketHistoryWindow : byte
{
    Week,
    Month,
}

internal sealed class MarketHistory
{
    public readonly uint ItemId;
    public readonly MarketTrade[] Trades;
    public readonly double VelocityNq;
    public readonly double VelocityHq;

    public MarketHistory(uint itemId, MarketTrade[] trades, double velocityNq, double velocityHq)
    {
        ItemId = itemId;
        Trades = trades;
        VelocityNq = velocityNq;
        VelocityHq = velocityHq;
    }

    public double Velocity(bool hq) => hq ? VelocityHq : VelocityNq;

    public static MarketHistory From(uint itemId, UniversalisHistory? source)
    {
        var entries = source?.Entries;
        if (entries is null || entries.Length == 0)
        {
            return new MarketHistory(itemId, Array.Empty<MarketTrade>(), source?.NqSaleVelocity ?? 0d,
                source?.HqSaleVelocity ?? 0d);
        }

        var trades = new MarketTrade[entries.Length];
        var kept = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry is null || entry.PricePerUnit <= 0 || entry.Timestamp <= 0)
            {
                continue;
            }

            var unix = entry.Timestamp > 1_000_000_000_000L ? entry.Timestamp / 1000L : entry.Timestamp;
            var price = (int)Math.Min(entry.PricePerUnit, int.MaxValue);
            trades[kept] = new MarketTrade(unix, price, Math.Max(1, entry.Quantity), entry.Hq);
            kept++;
        }

        if (kept != trades.Length)
        {
            Array.Resize(ref trades, kept);
        }

        MarketTrend.SortByTime(trades);
        return new MarketHistory(itemId, trades, source!.NqSaleVelocity, source.HqSaleVelocity);
    }
}

internal sealed class MarketHistoryEntry
{
    public volatile MarketState State = MarketState.Idle;
    public volatile MarketHistory? History;
    public DateTime FetchedUtc;
}
