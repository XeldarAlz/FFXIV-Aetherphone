namespace Aetherphone.Apps.Games.Broadside;

internal sealed class BroadsideBoard
{
    public const int Players = 2;

    private readonly BroadsideFleet[] fleets = { new(), new() };
    private readonly int[] shots = new int[Players];
    private readonly int[] hits = new int[Players];

    public int Turn { get; private set; }

    public int Winner { get; private set; } = -1;

    public bool Over => Winner >= 0;

    public BroadsideFleet Fleet(int player) => fleets[player];

    public int Shots(int player) => shots[player];

    public int Hits(int player) => hits[player];

    public int Accuracy(int player) => shots[player] == 0 ? 0 : (int)MathF.Round(hits[player] * 100f / shots[player]);

    public static int Opponent(int player) => 1 - player;

    public void Reset()
    {
        fleets[0].Clear();
        fleets[1].Clear();
        Array.Clear(shots);
        Array.Clear(hits);
        Turn = 0;
        Winner = -1;
    }

    public ShotResult Fire(int cell, out int ship)
    {
        ship = BroadsideFleet.NoShip;
        if (Over)
        {
            return ShotResult.Invalid;
        }

        var target = fleets[Opponent(Turn)];
        var result = target.Fire(cell, out ship);
        if (result == ShotResult.Invalid)
        {
            return result;
        }

        shots[Turn]++;
        if (result != ShotResult.Miss)
        {
            hits[Turn]++;
        }

        if (target.AllSunk)
        {
            Winner = Turn;
            return result;
        }

        Turn = Opponent(Turn);
        return result;
    }
}
