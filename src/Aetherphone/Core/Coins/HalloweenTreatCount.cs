namespace Aetherphone.Core.Coins;

internal static class HalloweenTreatCount
{
    public static int Merge(int local, int server, int total) => Math.Clamp(Math.Max(local, server), 0, total);

    public static int Next(int local, int server, int total) => Math.Min(total, Merge(local, server, total) + 1);

    public static int Mask(int count) => count <= 0 ? 0 : (1 << count) - 1;
}
