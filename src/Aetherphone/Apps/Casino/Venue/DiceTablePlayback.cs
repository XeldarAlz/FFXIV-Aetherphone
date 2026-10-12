using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class DiceTablePlayback
{
    public const float TumbleSeconds = 0.9f;
    public const float LogPopSeconds = 0.35f;

    private bool primed;
    private long lastSeenSeq;
    private long heroSeq;
    private long heroValue;
    private long heroBound;
    private string heroUserId = string.Empty;
    private string heroName = string.Empty;
    private float heroSeconds;
    private bool heroLive;
    private bool awaitingMine;
    private float awaitSeconds;
    private long awaitBound;
    private long awaitedSeq;
    private long celebratedRound = -1;
    private long closedRound = -1;
    private bool roundClosedLive;

    public bool Primed => primed;

    public long HeroSeq => heroSeq;

    public long HeroValue => heroValue;

    public long HeroBound => heroBound;

    public string HeroUserId => heroUserId;

    public string HeroName => heroName;

    public bool AwaitingMine => awaitingMine;

    public bool Tumbling => awaitingMine || (heroLive && heroSeconds < TumbleSeconds);

    public bool Landed => heroSeq > 0 && !Tumbling;

    public float HeroSeconds => heroSeconds;

    public long LastSeenSeq => lastSeenSeq;

    public long DisplayValue
    {
        get
        {
            if (awaitingMine)
            {
                return VenueTumble.Value(lastSeenSeq + 1, VenueTumble.StepAt(awaitSeconds), awaitBound);
            }

            return Tumbling ? VenueTumble.Value(heroSeq, VenueTumble.StepAt(heroSeconds), heroBound) : heroValue;
        }
    }

    public void Reset()
    {
        primed = false;
        lastSeenSeq = 0;
        heroSeq = 0;
        heroValue = 0;
        heroBound = 0;
        heroUserId = string.Empty;
        heroName = string.Empty;
        heroSeconds = 0f;
        heroLive = false;
        awaitingMine = false;
        awaitSeconds = 0f;
        awaitedSeq = 0;
        celebratedRound = -1;
        closedRound = -1;
        roundClosedLive = false;
    }

    public void BeginMine(long sides)
    {
        awaitingMine = true;
        awaitSeconds = 0f;
        awaitBound = Math.Max(1, sides);
        awaitedSeq = 0;
    }

    public void Confirm(long seq)
    {
        if (!awaitingMine)
        {
            return;
        }

        awaitedSeq = seq;
        if (seq > 0 && seq <= lastSeenSeq)
        {
            awaitingMine = false;
        }
    }

    public void CancelMine()
    {
        awaitingMine = false;
    }

    public void Update(CasinoDiceTableStateDto? board, string me, float deltaSeconds, bool snap)
    {
        heroSeconds += deltaSeconds;
        awaitSeconds += deltaSeconds;
        if (board is null)
        {
            return;
        }

        var rolls = board.Rolls ?? Array.Empty<CasinoVenueRollDto>();
        var newest = rolls.Length > 0 ? rolls[0] : null;
        if (!primed)
        {
            primed = true;
            lastSeenSeq = board.LastSeq;
            closedRound = board.LastRound?.Index ?? -1;
            celebratedRound = closedRound;
            if (newest is not null)
            {
                Hero(newest, false);
            }

            return;
        }

        if (newest is not null && newest.Seq > lastSeenSeq)
        {
            Hero(newest, !snap);
            if (RolledSince(rolls, lastSeenSeq, me))
            {
                awaitingMine = false;
            }
        }

        lastSeenSeq = Math.Max(lastSeenSeq, board.LastSeq);
        if (awaitedSeq > 0 && awaitedSeq <= lastSeenSeq)
        {
            awaitingMine = false;
        }
        var last = board.LastRound;
        if (last is not null && last.Closed && last.Index > closedRound)
        {
            closedRound = last.Index;
            roundClosedLive = !snap;
        }

        if (snap)
        {
            heroSeconds = TumbleSeconds;
        }
    }

    public bool TakeRoundClose(out long roundIndex)
    {
        roundIndex = closedRound;
        if (closedRound <= celebratedRound || Tumbling)
        {
            return false;
        }

        celebratedRound = closedRound;
        var live = roundClosedLive;
        roundClosedLive = false;
        return live;
    }

    private static bool RolledSince(CasinoVenueRollDto[] rolls, long seenSeq, string me)
    {
        for (var index = 0; index < rolls.Length && rolls[index].Seq > seenSeq; index++)
        {
            if (me.Length > 0 && string.Equals(rolls[index].UserId, me, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void Hero(CasinoVenueRollDto roll, bool live)
    {
        heroSeq = roll.Seq;
        heroValue = roll.Value;
        heroBound = roll.Bound;
        heroUserId = roll.UserId;
        heroName = roll.DisplayName;
        heroSeconds = live ? 0f : TumbleSeconds;
        heroLive = live;
    }
}
