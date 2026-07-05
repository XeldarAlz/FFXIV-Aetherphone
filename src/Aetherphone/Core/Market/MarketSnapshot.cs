namespace Aetherphone.Core.Market;

internal readonly struct MarketListing
{
    public readonly long PricePerUnit;
    public readonly int Quantity;
    public readonly long Total;
    public readonly bool Hq;
    public readonly string World;
    public readonly string Retainer;

    public MarketListing(long pricePerUnit, int quantity, long total, bool hq, string world, string retainer)
    {
        PricePerUnit = pricePerUnit;
        Quantity = quantity;
        Total = total;
        Hq = hq;
        World = world;
        Retainer = retainer;
    }
}

internal readonly struct MarketSale
{
    public readonly long PricePerUnit;
    public readonly int Quantity;
    public readonly bool Hq;
    public readonly DateTime Time;
    public readonly string World;
    public readonly string Buyer;

    public MarketSale(long pricePerUnit, int quantity, bool hq, DateTime time, string world, string buyer)
    {
        PricePerUnit = pricePerUnit;
        Quantity = quantity;
        Hq = hq;
        Time = time;
        World = world;
        Buyer = buyer;
    }
}

internal sealed class MarketSnapshot
{
    public readonly uint ItemId;
    public readonly DateTime LastUpload;
    public readonly bool MultiWorld;
    public readonly bool HasHq;
    public readonly MarketListing[] Listings;
    public readonly MarketSale[] Sales;
    public readonly long MinNq;
    public readonly long MinHq;
    public readonly double AvgNq;
    public readonly double AvgHq;
    public readonly long MaxNq;
    public readonly long MaxHq;
    public readonly double VelocityNq;
    public readonly double VelocityHq;
    public readonly int UnitsForSale;
    public readonly int UnitsSold;

    public MarketSnapshot(uint itemId, DateTime lastUpload, bool multiWorld, bool hasHq, MarketListing[] listings,
        MarketSale[] sales, long minNq, long minHq, double avgNq, double avgHq, long maxNq, long maxHq,
        double velocityNq, double velocityHq, int unitsForSale, int unitsSold)
    {
        ItemId = itemId;
        LastUpload = lastUpload;
        MultiWorld = multiWorld;
        HasHq = hasHq;
        Listings = listings;
        Sales = sales;
        MinNq = minNq;
        MinHq = minHq;
        AvgNq = avgNq;
        AvgHq = avgHq;
        MaxNq = maxNq;
        MaxHq = maxHq;
        VelocityNq = velocityNq;
        VelocityHq = velocityHq;
        UnitsForSale = unitsForSale;
        UnitsSold = unitsSold;
    }

    public long Min(bool hq) => hq ? MinHq : MinNq;
    public double Average(bool hq) => hq ? AvgHq : AvgNq;
    public long Max(bool hq) => hq ? MaxHq : MaxNq;
    public double Velocity(bool hq) => hq ? VelocityHq : VelocityNq;
}
