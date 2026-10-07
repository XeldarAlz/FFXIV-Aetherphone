namespace Aetherphone.Apps.Games.Framework;

internal readonly struct GameStart
{
    public readonly int Mode;
    public readonly ulong Seed;
    public readonly bool Daily;
    public readonly int Level;
    public readonly int Seats;

    public GameStart(int mode, ulong seed, bool daily, int level = 0, int seats = 1)
    {
        Mode = mode;
        Seed = seed;
        Daily = daily;
        Level = level;
        Seats = Math.Max(1, seats);
    }

    public GameRandom Random => GameRandom.FromSeed(Seed);

    public bool HotSeat => Seats > 1;
}
