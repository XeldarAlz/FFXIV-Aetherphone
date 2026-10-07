namespace Aetherphone.Core.MoogleClicker;

internal static class KupoBuildings
{
    public const int Count = 12;
    public const int MaxOwned = 5000;
    public const double Growth = 1.15d;

    private static readonly double LogGrowth = Math.Log(Growth);

    private static readonly double[] BaseCosts =
    {
        15d, 110d, 1_200d, 13_000d, 140_000d, 1_500_000d, 21_000_000d, 340_000_000d, 5.2e9, 7.8e10, 1.1e12, 1.5e13,
    };

    private static readonly double[] BaseRates =
    {
        0.1d, 1d, 8d, 50d, 270d, 1_500d, 8_000d, 45_000d, 270_000d, 1.65e6, 1.05e7, 6.8e7,
    };

    public static double BaseCost(int building) => BaseCosts[building];

    public static double BaseRate(int building) => BaseRates[building];

    public static double Cost(int building, int owned) => BaseCosts[building] * Math.Pow(Growth, owned);

    public static double BulkCost(int building, int owned, int count)
    {
        if (count <= 0)
        {
            return 0d;
        }

        return Cost(building, owned) * (Math.Pow(Growth, count) - 1d) / (Growth - 1d);
    }

    public static int MaxAffordable(int building, int owned, double kupo)
    {
        var room = MaxOwned - owned;
        if (room <= 0 || !(kupo > 0d))
        {
            return 0;
        }

        var first = Cost(building, owned);
        if (first > kupo)
        {
            return 0;
        }

        var estimate = Math.Floor(Math.Log(kupo * (Growth - 1d) / first + 1d) / LogGrowth);
        var count = (int)Math.Clamp(estimate, 0d, room);
        while (count > 0 && BulkCost(building, owned, count) > kupo)
        {
            count--;
        }

        while (count < room && BulkCost(building, owned, count + 1) <= kupo)
        {
            count++;
        }

        return count;
    }
}
