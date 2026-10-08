using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class DeathrollPlayback
{
    public const float RollSeconds = 1.1f;

    private bool primed;
    private long duelSeq;
    private long lastRollSeq;
    private long fromValue;
    private long toValue;
    private float rollSeconds = RollSeconds;
    private bool rollLive;
    private string rollerUserId = string.Empty;
    private string loserUserId = string.Empty;
    private long finishedSeq = -1;
    private long reportedSeq = -1;
    private bool finishedLive;
    private CasinoDeathrollDuelDto? duel;

    public CasinoDeathrollDuelDto? Duel => duel;

    public bool Rolling => rollLive && rollSeconds < RollSeconds;

    public long Shown => Rolling
        ? Math.Max(1, VenueTumble.Value(lastRollSeq, VenueTumble.StepAt(rollSeconds), Math.Max(1, fromValue)))
        : toValue;

    public long Bound => fromValue;

    public string RollerUserId => rollerUserId;

    public string LoserUserId => loserUserId;

    public bool Finished => finishedSeq >= 0 && finishedSeq == duelSeq && !Rolling;

    public float RollProgress => Math.Clamp(rollSeconds / RollSeconds, 0f, 1f);

    public void Reset()
    {
        primed = false;
        duelSeq = 0;
        lastRollSeq = 0;
        fromValue = 0;
        toValue = 0;
        rollSeconds = RollSeconds;
        rollLive = false;
        rollerUserId = string.Empty;
        loserUserId = string.Empty;
        finishedSeq = -1;
        reportedSeq = -1;
        finishedLive = false;
        duel = null;
    }

    public void Update(CasinoDeathrollStateDto? board, float deltaSeconds, bool snap)
    {
        rollSeconds += deltaSeconds;
        if (board is null)
        {
            return;
        }

        var active = ActiveDuel(board, duelSeq);
        if (!primed)
        {
            primed = true;
            Absorb(active, board.StartAt, false);
            if (active is not null && active.Phase >= DuelPhases.Finished)
            {
                reportedSeq = active.Seq;
            }

            return;
        }

        Absorb(active, board.StartAt, !snap);
        if (snap)
        {
            rollSeconds = RollSeconds;
        }
    }

    public bool TakeFinish(out string loser)
    {
        loser = loserUserId;
        if (finishedSeq < 0 || finishedSeq == reportedSeq || Rolling)
        {
            return false;
        }

        reportedSeq = finishedSeq;
        return finishedLive;
    }

    internal static CasinoDeathrollDuelDto? ActiveDuel(CasinoDeathrollStateDto board, long trackedSeq)
    {
        if (board.Duel is not null)
        {
            return board.Duel;
        }

        var recent = board.Recent;
        if (recent is null || recent.Length == 0)
        {
            return null;
        }

        return trackedSeq > 0 && recent[0].Seq == trackedSeq ? recent[0] : null;
    }

    private void Absorb(CasinoDeathrollDuelDto? active, int startAt, bool live)
    {
        duel = active;
        if (active is null)
        {
            if (duelSeq != 0 && finishedSeq != duelSeq)
            {
                duelSeq = 0;
            }

            if (duelSeq == 0)
            {
                fromValue = startAt;
                toValue = startAt;
            }

            return;
        }

        if (active.Seq != duelSeq)
        {
            duelSeq = active.Seq;
            lastRollSeq = 0;
            fromValue = active.Current > 0 ? active.Current : startAt;
            toValue = fromValue;
            rollSeconds = RollSeconds;
            rollLive = false;
            loserUserId = string.Empty;
        }

        var rolls = active.Rolls;
        if (rolls is not null && rolls.Length > 0)
        {
            var newest = rolls[^1];
            for (var index = 0; index < rolls.Length; index++)
            {
                if (rolls[index].Seq > newest.Seq)
                {
                    newest = rolls[index];
                }
            }

            if (newest.Seq > lastRollSeq)
            {
                var first = lastRollSeq == 0 && !live;
                lastRollSeq = newest.Seq;
                fromValue = newest.Bound;
                toValue = newest.Value;
                rollerUserId = newest.UserId;
                rollLive = live && !first;
                rollSeconds = rollLive ? 0f : RollSeconds;
            }
        }
        else
        {
            fromValue = active.Current > 0 ? active.Current : startAt;
            toValue = fromValue;
        }

        if (active.Phase == DuelPhases.Finished && finishedSeq != active.Seq)
        {
            finishedSeq = active.Seq;
            loserUserId = active.LoserUserId;
            finishedLive = live;
        }
    }
}
