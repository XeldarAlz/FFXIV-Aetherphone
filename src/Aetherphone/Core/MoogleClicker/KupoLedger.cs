namespace Aetherphone.Core.MoogleClicker;

internal static class KupoLedger
{
    public const double KupoPerStampCube = 1e9;
    public const double BonusPerStamp = 0.02d;
    public const int LevelCap = 100_000;

    public static double StampsFor(double lifetimeKupo)
    {
        if (!(lifetimeKupo > 0d))
        {
            return 0d;
        }

        var ratio = lifetimeKupo / KupoPerStampCube;
        var stamps = Math.Floor(Math.Cbrt(ratio));
        if (Cube(stamps + 1d) <= ratio)
        {
            return stamps + 1d;
        }

        return stamps > 0d && Cube(stamps) > ratio ? stamps - 1d : stamps;
    }

    public static double KupoForStamps(double stamps) => Cube(stamps) * KupoPerStampCube;

    public static double Multiplier(double stamps) => 1d + BonusPerStamp * stamps;

    public static int Level(double stamps) => (int)Math.Clamp(stamps, 0d, LevelCap);

    public static int BonusPercent(double stamps) => (int)Math.Min(BonusPerStamp * stamps * 100d, int.MaxValue);

    private static double Cube(double value) => value * value * value;
}
