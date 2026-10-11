using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Venue;

internal enum RaffleStage : byte
{
    Idle,
    Selling,
    Spinning,
    Landed,
    Done,
}

internal sealed class RafflePlayback
{
    public const float SpinSeconds = 3.4f;
    public const float LandHoldSeconds = 1.1f;
    public const int MinTurns = 3;

    private const float FullTurn = MathF.PI * 2f;
    private const float SegmentMargin = 0.18f;

    private readonly int[] segmentOwner = new int[VenueRules.MaxEntrants];
    private readonly float[] segmentStart = new float[VenueRules.MaxEntrants + 1];
    private readonly int[] winnerEntrant = new int[VenueRules.MaxWinners];

    private bool primed;
    private long liveSeq = -1;
    private long replayedSeq = -1;
    private long finishedSeq = -1;
    private bool replayLive;
    private uint seed;
    private int winnerCount;
    private int winnerShown;
    private int segmentCount;
    private float angle;
    private float fromAngle;
    private float toAngle;
    private float stageSeconds;
    private RaffleStage stage;
    private CasinoRaffleDto? replaying;
    private CasinoRaffleDto? segmented;

    public RaffleStage Stage => stage;

    public float Angle => angle;

    public int SegmentCount => segmentCount;

    public int WinnersShown => winnerShown;

    public int WinnerCount => winnerCount;

    public CasinoRaffleDto? Replaying => replaying;

    public bool ReplayLive => replayLive;

    public float StageSeconds => stageSeconds;

    public int OwnerOf(int segment) => segmentOwner[segment];

    public float StartOf(int segment) => segmentStart[segment];

    public float EndOf(int segment) => segmentStart[segment + 1];

    public int WinnerEntrant(int index) => winnerEntrant[index];

    public bool CanReplay(CasinoRaffleStateDto? board)
    {
        var last = board?.Last;
        return last is not null && last.Drawn && (stage is RaffleStage.Done or RaffleStage.Idle);
    }

    public void Reset()
    {
        primed = false;
        liveSeq = -1;
        replayedSeq = -1;
        finishedSeq = -1;
        replayLive = false;
        winnerCount = 0;
        winnerShown = 0;
        segmentCount = 0;
        angle = 0f;
        stageSeconds = 0f;
        stage = RaffleStage.Idle;
        replaying = null;
        segmented = null;
    }

    public void Update(CasinoRaffleStateDto? board, float deltaSeconds, bool instant)
    {
        stageSeconds += deltaSeconds;
        if (board is null)
        {
            return;
        }

        var live = board.Raffle;
        var last = board.Last;
        if (!primed)
        {
            primed = true;
            liveSeq = live?.Seq ?? -1;
            if (last is not null && last.Drawn && live is null)
            {
                replayedSeq = last.DrawSeq;
                ShowFinal(last);
            }
        }

        if (live is not null)
        {
            liveSeq = live.Seq;
            if (stage is RaffleStage.Idle or RaffleStage.Done)
            {
                stage = RaffleStage.Selling;
                replaying = null;
            }

            if (!ReferenceEquals(live, segmented))
            {
                segmented = live;
                winnerCount = 0;
                BuildSegments(live, 0);
            }

            angle += deltaSeconds * 0.15f;
            return;
        }

        if (last is not null && last.Drawn && last.DrawSeq > replayedSeq)
        {
            replayedSeq = last.DrawSeq;
            var watched = liveSeq == last.Seq;
            Begin(last, watched && !instant);
            if (instant)
            {
                ShowFinal(last);
            }

            return;
        }

        if (stage == RaffleStage.Selling)
        {
            stage = RaffleStage.Idle;
            segmentCount = 0;
        }

        Advance(instant);
    }

    public void Replay(CasinoRaffleDto last)
    {
        if (!last.Drawn)
        {
            return;
        }

        Begin(last, false);
    }

    public bool TakeFinish(out CasinoRaffleDto? finished)
    {
        finished = replaying;
        if (stage != RaffleStage.Done || replaying is null || finishedSeq == replaying.DrawSeq)
        {
            return false;
        }

        finishedSeq = replaying.DrawSeq;
        return replayLive;
    }

    internal static int EntrantIndexOf(CasinoRaffleEntrantDto[] entrants, string userId)
    {
        for (var index = 0; index < entrants.Length; index++)
        {
            if (string.Equals(entrants[index].UserId, userId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void Begin(CasinoRaffleDto last, bool live)
    {
        replaying = last;
        replayLive = live;
        seed = VenueTumble.SeedOf(last.Seed);
        var entrants = last.Entrants ?? Array.Empty<CasinoRaffleEntrantDto>();
        var drawn = last.WinnersDrawn ?? Array.Empty<CasinoRaffleEntrantDto>();
        winnerCount = 0;
        for (var index = 0; index < drawn.Length && winnerCount < winnerEntrant.Length; index++)
        {
            var entrant = EntrantIndexOf(entrants, drawn[index].UserId);
            if (entrant >= 0)
            {
                winnerEntrant[winnerCount++] = entrant;
            }
        }

        winnerShown = 0;
        if (winnerCount == 0)
        {
            stage = RaffleStage.Done;
            return;
        }

        StartSpin(last);
    }

    private void StartSpin(CasinoRaffleDto last)
    {
        BuildSegments(last, winnerShown);
        var target = SegmentFor(winnerEntrant[winnerShown]);
        if (target < 0)
        {
            stage = RaffleStage.Done;
            return;
        }

        var span = segmentStart[target + 1] - segmentStart[target];
        var inside = segmentStart[target] + span * (SegmentMargin
            + (1f - SegmentMargin * 2f) * VenueTumble.Unit(seed, (uint)winnerShown));
        var turns = MinTurns + (int)(VenueTumble.Hash(seed, (uint)winnerShown + 101u) % 2u);
        fromAngle = Normalize(angle);
        var landing = -inside * FullTurn;
        toAngle = fromAngle + turns * FullTurn + Normalize(landing - fromAngle);
        angle = fromAngle;
        stageSeconds = 0f;
        stage = RaffleStage.Spinning;
    }

    private void Advance(bool instant)
    {
        if (replaying is null)
        {
            return;
        }

        if (stage == RaffleStage.Spinning)
        {
            var progress = instant ? 1f : Math.Clamp(stageSeconds / SpinSeconds, 0f, 1f);
            var eased = 1f - (1f - progress) * (1f - progress) * (1f - progress);
            angle = fromAngle + (toAngle - fromAngle) * eased;
            if (progress >= 1f)
            {
                angle = toAngle;
                stage = RaffleStage.Landed;
                stageSeconds = 0f;
                winnerShown++;
            }

            return;
        }

        if (stage != RaffleStage.Landed || (!instant && stageSeconds < LandHoldSeconds))
        {
            return;
        }

        if (winnerShown >= winnerCount)
        {
            stage = RaffleStage.Done;
            return;
        }

        StartSpin(replaying);
    }

    private void ShowFinal(CasinoRaffleDto last)
    {
        replaying = last;
        replayLive = false;
        var entrants = last.Entrants ?? Array.Empty<CasinoRaffleEntrantDto>();
        var drawn = last.WinnersDrawn ?? Array.Empty<CasinoRaffleEntrantDto>();
        winnerCount = 0;
        for (var index = 0; index < drawn.Length && winnerCount < winnerEntrant.Length; index++)
        {
            var entrant = EntrantIndexOf(entrants, drawn[index].UserId);
            if (entrant >= 0)
            {
                winnerEntrant[winnerCount++] = entrant;
            }
        }

        winnerShown = winnerCount;
        BuildSegments(last, 0);
        stage = RaffleStage.Done;
        finishedSeq = last.DrawSeq;
    }

    private void BuildSegments(CasinoRaffleDto raffle, int skipWinners)
    {
        var entrants = raffle.Entrants ?? Array.Empty<CasinoRaffleEntrantDto>();
        var total = 0;
        for (var index = 0; index < entrants.Length && index < segmentOwner.Length; index++)
        {
            if (!Skipped(index, skipWinners))
            {
                total += Math.Max(0, entrants[index].Tickets);
            }
        }

        segmentCount = 0;
        if (total <= 0)
        {
            segmentStart[0] = 0f;
            return;
        }

        var cursor = 0f;
        for (var index = 0; index < entrants.Length && index < segmentOwner.Length; index++)
        {
            var tickets = entrants[index].Tickets;
            if (tickets <= 0 || Skipped(index, skipWinners))
            {
                continue;
            }

            segmentOwner[segmentCount] = index;
            segmentStart[segmentCount] = cursor;
            cursor += tickets / (float)total;
            segmentCount++;
        }

        segmentStart[segmentCount] = 1f;
    }

    private bool Skipped(int entrant, int skipWinners)
    {
        for (var index = 0; index < skipWinners && index < winnerCount; index++)
        {
            if (winnerEntrant[index] == entrant)
            {
                return true;
            }
        }

        return false;
    }

    private int SegmentFor(int entrant)
    {
        for (var segment = 0; segment < segmentCount; segment++)
        {
            if (segmentOwner[segment] == entrant)
            {
                return segment;
            }
        }

        return -1;
    }

    private static float Normalize(float radians)
    {
        var turned = radians % FullTurn;
        return turned < 0f ? turned + FullTurn : turned;
    }
}
