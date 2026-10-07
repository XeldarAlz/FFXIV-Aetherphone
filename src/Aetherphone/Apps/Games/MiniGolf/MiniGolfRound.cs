namespace Aetherphone.Apps.Games.MiniGolf;

internal enum RoundStep : byte
{
    NextPlayer,
    NextHole,
    Finished,
}

internal enum HoleResult : byte
{
    HoleInOne,
    Eagle,
    Birdie,
    Par,
    Bogey,
    DoubleBogey,
    Worse,
}

internal sealed class MiniGolfRound
{
    public const int MaxPlayers = 4;
    private readonly int[] strokes = new int[MaxPlayers * MiniGolfCourse.HoleCount];

    public int Holes { get; private set; }

    public int Players { get; private set; }

    public int Hole { get; private set; }

    public int Player { get; private set; }

    public bool Finished { get; private set; }

    public void Reset(int holes, int players)
    {
        Holes = Math.Clamp(holes, 1, MiniGolfCourse.HoleCount);
        Players = Math.Clamp(players, 1, MaxPlayers);
        Hole = 0;
        Player = 0;
        Finished = false;
        Array.Clear(strokes);
    }

    public void Record(int count)
    {
        strokes[Slot(Player, Hole)] = Math.Max(1, count);
    }

    public void Set(int player, int hole, int count)
    {
        strokes[Slot(Math.Clamp(player, 0, MaxPlayers - 1), Math.Clamp(hole, 0, MiniGolfCourse.HoleCount - 1))] =
            Math.Max(0, count);
    }

    public RoundStep Advance()
    {
        if (Player + 1 < Players)
        {
            Player++;
            return RoundStep.NextPlayer;
        }

        Player = 0;
        if (Hole + 1 < Holes)
        {
            Hole++;
            return RoundStep.NextHole;
        }

        Finished = true;
        return RoundStep.Finished;
    }

    public int Strokes(int player, int hole) => strokes[Slot(player, hole)];

    public int Total(int player)
    {
        var total = 0;
        for (var hole = 0; hole < Holes; hole++)
        {
            total += strokes[Slot(player, hole)];
        }

        return total;
    }

    public int ToPar(int player)
    {
        var difference = 0;
        for (var hole = 0; hole < Holes; hole++)
        {
            var count = strokes[Slot(player, hole)];
            if (count > 0)
            {
                difference += count - MiniGolfCourse.Get(hole).Par;
            }
        }

        return difference;
    }

    public int Count(int player, HoleResult best)
    {
        var count = 0;
        for (var hole = 0; hole < Holes; hole++)
        {
            var played = strokes[Slot(player, hole)];
            if (played > 0 && Classify(played, MiniGolfCourse.Get(hole).Par) <= best)
            {
                count++;
            }
        }

        return count;
    }

    public int Winner()
    {
        var winner = 0;
        var tied = false;
        for (var player = 1; player < Players; player++)
        {
            var total = Total(player);
            var best = Total(winner);
            if (total < best)
            {
                winner = player;
                tied = false;
            }
            else if (total == best)
            {
                tied = true;
            }
        }

        return tied ? -1 : winner;
    }

    public int Standings(Span<int> order)
    {
        var count = Math.Min(Players, order.Length);
        for (var index = 0; index < count; index++)
        {
            order[index] = index;
        }

        for (var index = 1; index < count; index++)
        {
            var player = order[index];
            var slot = index - 1;
            while (slot >= 0 && Total(order[slot]) > Total(player))
            {
                order[slot + 1] = order[slot];
                slot--;
            }

            order[slot + 1] = player;
        }

        return count;
    }

    public static HoleResult Classify(int strokes, int par)
    {
        if (strokes == 1)
        {
            return HoleResult.HoleInOne;
        }

        return (strokes - par) switch
        {
            <= -2 => HoleResult.Eagle,
            -1 => HoleResult.Birdie,
            0 => HoleResult.Par,
            1 => HoleResult.Bogey,
            2 => HoleResult.DoubleBogey,
            _ => HoleResult.Worse,
        };
    }

    private static int Slot(int player, int hole) => player * MiniGolfCourse.HoleCount + hole;
}
