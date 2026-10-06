namespace Aetherphone.Apps.Games.Framework;

internal readonly struct GameStart
{
    public readonly int Mode;
    public readonly ulong Seed;
    public readonly bool Daily;
    public readonly int Level;

    public GameStart(int mode, ulong seed, bool daily, int level = 0)
    {
        Mode = mode;
        Seed = seed;
        Daily = daily;
        Level = level;
    }

    public GameRandom Random => GameRandom.FromSeed(Seed);
}
