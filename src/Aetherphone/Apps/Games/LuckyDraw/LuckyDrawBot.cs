namespace Aetherphone.Apps.Games.LuckyDraw;

internal enum LuckyTemper : byte
{
    Careful,
    Steady,
    Bold,
}

internal readonly struct LuckyAppetite
{
    public readonly LuckyTemper Temper;
    public readonly float MaxRisk;
    public readonly int Comfort;

    public LuckyAppetite(LuckyTemper temper, float maxRisk, int comfort)
    {
        Temper = temper;
        MaxRisk = maxRisk;
        Comfort = comfort;
    }
}

internal static class LuckyDrawBot
{
    public static readonly LuckyAppetite Careful = new(LuckyTemper.Careful, 0.16f, 18);
    public static readonly LuckyAppetite Steady = new(LuckyTemper.Steady, 0.26f, 26);
    public static readonly LuckyAppetite Bold = new(LuckyTemper.Bold, 0.38f, 36);
    private static readonly LuckyAppetite[] Rotation = { Steady, Bold, Careful };
    private const float ChaseSevenRisk = 0.45f;
    private const int FlipThreeHandWeight = 2;

    public static LuckyAppetite AppetiteFor(int botIndex) => Rotation[Math.Abs(botIndex) % Rotation.Length];

    public static bool WantsHit(LuckyDrawBoard board, int seat, in LuckyAppetite appetite)
    {
        var hand = board.HandScore(seat);
        if (WouldWinByStaying(board, seat, hand))
        {
            return false;
        }

        if (board.UniqueNumbers(seat) == 0 || board.HasSecondChance(seat))
        {
            return true;
        }

        var risk = board.BustChance(seat);
        if (board.UniqueNumbers(seat) == LuckyDrawBoard.SevenNumbers - 1)
        {
            return risk < MathF.Max(appetite.MaxRisk, ChaseSevenRisk * (appetite.MaxRisk / Bold.MaxRisk));
        }

        var shortfall = (appetite.Comfort - hand) / (float)appetite.Comfort;
        var tolerance = appetite.MaxRisk * (1f + shortfall);
        return risk < tolerance;
    }

    public static int ChooseTarget(LuckyDrawBoard board, int actor, in LuckyAppetite appetite)
    {
        var face = board.PendingFace;
        var best = -1;
        var bestScore = float.MinValue;
        for (var seat = 0; seat < board.Seats; seat++)
        {
            if (!board.IsValidTarget(seat))
            {
                continue;
            }

            var score = face switch
            {
                LuckyCards.SecondChance => -board.Total(seat),
                LuckyCards.Freeze => FreezeValue(board, actor, seat, appetite),
                _ => FlipThreeValue(board, actor, seat, appetite),
            };
            if (score > bestScore)
            {
                bestScore = score;
                best = seat;
            }
        }

        return best;
    }

    private static bool WouldWinByStaying(LuckyDrawBoard board, int seat, int hand)
    {
        var mine = board.Total(seat) + hand;
        if (mine < LuckyDrawBoard.WinTarget)
        {
            return false;
        }

        for (var other = 0; other < board.Seats; other++)
        {
            if (other != seat && board.Total(other) + board.HandScore(other) >= mine)
            {
                return false;
            }
        }

        return true;
    }

    private static float FreezeValue(LuckyDrawBoard board, int actor, int seat, in LuckyAppetite appetite)
    {
        if (seat == actor)
        {
            var hand = board.HandScore(actor);
            return hand >= appetite.Comfort || board.BustChance(actor) >= appetite.MaxRisk ? 1000f + hand : -1000f;
        }

        return board.Total(seat) - board.HandScore(seat) * 2f;
    }

    private static float FlipThreeValue(LuckyDrawBoard board, int actor, int seat, in LuckyAppetite appetite)
    {
        if (seat == actor)
        {
            return board.BustChance(actor) < appetite.MaxRisk * 0.5f && board.HandScore(actor) < appetite.Comfort
                ? 0f
                : -1000f;
        }

        return board.BustChance(seat) * (board.HandScore(seat) + 1) * FlipThreeHandWeight + board.Total(seat) * 0.01f;
    }
}
