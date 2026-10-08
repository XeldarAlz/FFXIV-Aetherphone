using Aetherphone.Apps.Games.Framework;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackIdleScript
{
    public const int Spots = 3;
    public const int CardsPerRound = Spots * 2 + 2;
    public const float DealSeconds = 0.45f;
    public const float HoldSeconds = 2.2f;
    public const float SweepSeconds = 0.5f;
    public const float RoundSeconds = CardsPerRound * DealSeconds + HoldSeconds + SweepSeconds;
    public const ulong DefaultSeed = 0x21B1ACC0FFEEUL;

    private readonly int[] cards = new int[CardsPerRound];
    private GameRandom random;
    private float clock;
    private int round = -1;

    public BlackjackIdleScript(ulong seed = DefaultSeed)
    {
        random = GameRandom.FromSeed(seed);
        Advance(0f);
    }

    public int Round => round;

    public float RoundClock => clock - round * RoundSeconds;

    public void Advance(float deltaSeconds)
    {
        clock += MathF.Max(0f, deltaSeconds);
        var wanted = (int)(clock / RoundSeconds);
        while (round < wanted)
        {
            round++;
            Shuffle();
        }
    }

    public int CardAt(int index)
    {
        return index >= 0 && index < cards.Length ? cards[index] : PlayingCards.FaceDown;
    }

    public static int TargetOf(int index)
    {
        return index % (Spots + 1);
    }

    public static bool IsDealerCard(int index)
    {
        return TargetOf(index) == Spots;
    }

    public static int LayerOf(int index)
    {
        return index / (Spots + 1);
    }

    public float TravelOf(int index)
    {
        var local = RoundClock - index * DealSeconds;
        if (local <= 0f)
        {
            return 0f;
        }

        return MathF.Min(1f, local / BlackjackDealChoreography.TravelSeconds);
    }

    public float Sweep
    {
        get
        {
            var start = CardsPerRound * DealSeconds + HoldSeconds;
            var local = RoundClock - start;
            return local <= 0f ? 0f : MathF.Min(1f, local / SweepSeconds);
        }
    }

    public bool HoleShown(int index)
    {
        if (!IsDealerCard(index) || LayerOf(index) == 0)
        {
            return true;
        }

        return RoundClock >= CardsPerRound * DealSeconds + HoldSeconds * 0.35f;
    }

    private void Shuffle()
    {
        for (var index = 0; index < cards.Length; index++)
        {
            cards[index] = random.Next(PlayingCards.DeckSize);
        }
    }
}
