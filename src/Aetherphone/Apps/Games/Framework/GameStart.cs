namespace Aetherphone.Apps.Games.Framework;

internal readonly struct GameStart
{
    public readonly int Mode;
    public readonly ulong Seed;
    public readonly bool Daily;

    public GameStart(int mode, ulong seed, bool daily)
    {
        Mode = mode;
        Seed = seed;
        Daily = daily;
    }

    public GameRandom Random => GameRandom.FromSeed(Seed);
}
