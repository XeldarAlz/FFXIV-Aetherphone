using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.LuckyDraw;

internal enum LuckyPhase : byte
{
    Auto,
    Turn,
    Target,
    RoundOver,
    MatchOver,
}

internal enum LuckySeatState : byte
{
    Active,
    Stayed,
    Frozen,
    Busted,
}

internal enum LuckyEventKind : byte
{
    None,
    Drew,
    Kept,
    Bust,
    Saved,
    Seven,
    SecondChanceKept,
    SecondChanceGiven,
    SecondChanceDiscarded,
    ChooseTarget,
    Frozen,
    FlipThree,
    ActionDeferred,
    ActionDiscarded,
    Stayed,
    Turn,
    RoundOver,
    MatchOver,
}

internal readonly struct LuckyEvent
{
    public readonly LuckyEventKind Kind;
    public readonly int Seat;
    public readonly int Target;
    public readonly int Face;
    public readonly int Slot;
    public readonly int OtherSlot;
    public readonly bool Reshuffled;

    public LuckyEvent(LuckyEventKind kind, int seat, int target = -1, int face = -1, int slot = -1,
        int otherSlot = -1, bool reshuffled = false)
    {
        Kind = kind;
        Seat = seat;
        Target = target;
        Face = face;
        Slot = slot;
        OtherSlot = otherSlot;
        Reshuffled = reshuffled;
    }
}

internal sealed class LuckyDrawBoard : ILuckyScoreboard
{
    public const int MinSeats = 2;
    public const int MaxSeats = GameSeats.Max;
    public const int RowCapacity = 24;
    public const int WinTarget = 200;
    public const int SevenBonus = 15;
    public const int SevenNumbers = 7;
    public const int FlipThreeCards = 3;
    private const int DeferredCapacity = 8;

    private readonly byte[] deck = new byte[LuckyCards.DeckSize];
    private readonly int[] discard = new int[LuckyCards.DeckSize];
    private readonly byte[] rows = new byte[MaxSeats * RowCapacity];
    private readonly int[] rowCounts = new int[MaxSeats];
    private readonly LuckySeatState[] states = new LuckySeatState[MaxSeats];
    private readonly int[] totals = new int[MaxSeats];
    private readonly int[] roundScores = new int[MaxSeats];
    private readonly int[] bestRounds = new int[MaxSeats];
    private readonly int[] sevens = new int[MaxSeats];
    private readonly byte[] deferredFaces = new byte[DeferredCapacity];
    private readonly int[] deferredActors = new int[DeferredCapacity];
    private GameRandom random;
    private int deckCount;
    private int discardCount;
    private int deferredCount;
    private int dealCount;
    private int drawnSeat = -1;
    private int drawnSlot = -1;
    private int pendingSlot = -1;
    private int forcedSeat = -1;
    private int forcedLeft;
    private bool dealing;
    private bool resolvePending;

    public int Seats { get; private set; } = MinSeats;

    public int Round { get; private set; }

    public int Dealer { get; private set; }

    public int TurnSeat { get; private set; }

    public int SevenSeat { get; private set; } = -1;

    public int Winner { get; private set; } = -1;

    public int Actor { get; private set; } = -1;

    public int PendingFace { get; private set; } = -1;

    public LuckyPhase Phase { get; private set; } = LuckyPhase.MatchOver;

    public int DeckCount => deckCount;

    public int DiscardCount => discardCount;

    public ReadOnlySpan<int> Discard => discard.AsSpan(0, discardCount);

    public bool Dealing => dealing;

    public int ForcedSeat => forcedLeft > 0 ? forcedSeat : -1;

    public int ForcedLeft => forcedLeft;

    public bool Playing => Phase is LuckyPhase.Auto or LuckyPhase.Turn or LuckyPhase.Target;

    public int ActiveCount
    {
        get
        {
            var count = 0;
            for (var seat = 0; seat < Seats; seat++)
            {
                if (states[seat] == LuckySeatState.Active)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public LuckySeatState State(int seat) => states[seat];

    public int Total(int seat) => totals[seat];

    public int LastRoundScore(int seat) => roundScores[seat];

    public int BestRound(int seat) => bestRounds[seat];

    public int Sevens(int seat) => sevens[seat];

    public int RowCount(int seat) => rowCounts[seat];

    public int RowFace(int seat, int slot) => rows[seat * RowCapacity + slot];

    public void NewMatch(int seats, GameRandom source)
    {
        Seats = Math.Clamp(seats, MinSeats, MaxSeats);
        random = source;
        Array.Clear(totals);
        Array.Clear(roundScores);
        Array.Clear(bestRounds);
        Array.Clear(sevens);
        Array.Clear(rowCounts);
        deckCount = LuckyCards.Fill(deck);
        Shuffle();
        discardCount = 0;
        Round = 0;
        Winner = -1;
        Phase = LuckyPhase.RoundOver;
        StartRound();
    }

    public void StartRound()
    {
        if (Phase != LuckyPhase.RoundOver)
        {
            return;
        }

        CollectTable();
        Round++;
        Dealer = (Round - 2 + Seats) % Seats;
        for (var seat = 0; seat < MaxSeats; seat++)
        {
            states[seat] = seat < Seats ? LuckySeatState.Active : LuckySeatState.Stayed;
            roundScores[seat] = 0;
        }

        SevenSeat = -1;
        deferredCount = 0;
        forcedLeft = 0;
        forcedSeat = -1;
        resolvePending = false;
        drawnSeat = -1;
        drawnSlot = -1;
        ClearPending();
        dealCount = 0;
        dealing = true;
        TurnSeat = (Dealer + 1) % Seats;
        Phase = LuckyPhase.Auto;
    }

    public LuckyEvent Advance()
    {
        if (Phase != LuckyPhase.Auto)
        {
            return default;
        }

        if (resolvePending)
        {
            return Resolve();
        }

        if (forcedLeft > 0)
        {
            if (states[forcedSeat] == LuckySeatState.Active)
            {
                forcedLeft--;
                return Draw(forcedSeat);
            }

            forcedLeft = 0;
        }

        if (deferredCount > 0)
        {
            return ResolveDeferred();
        }

        if (ActiveCount == 0)
        {
            return EndRound();
        }

        return dealing ? DealNext() : BeginTurn(FirstActiveFrom(TurnSeat + 1));
    }

    public LuckyEvent Hit()
    {
        return Phase != LuckyPhase.Turn ? default : Draw(TurnSeat);
    }

    public LuckyEvent Stay()
    {
        if (Phase != LuckyPhase.Turn)
        {
            return default;
        }

        states[TurnSeat] = LuckySeatState.Stayed;
        Phase = LuckyPhase.Auto;
        return new LuckyEvent(LuckyEventKind.Stayed, TurnSeat);
    }

    public bool IsValidTarget(int seat)
    {
        if (Phase != LuckyPhase.Target || seat < 0 || seat >= Seats || states[seat] != LuckySeatState.Active)
        {
            return false;
        }

        return PendingFace != LuckyCards.SecondChance || (seat != Actor && !HasSecondChance(seat));
    }

    public LuckyEvent Target(int seat)
    {
        if (!IsValidTarget(seat))
        {
            return default;
        }

        var actor = Actor;
        var face = PendingFace;
        var slot = pendingSlot;
        ClearPending();
        Phase = LuckyPhase.Auto;
        return face == LuckyCards.SecondChance ? GiveSecondChance(actor, seat, slot) : ApplyAction(actor, slot, face, seat);
    }

    public int SweepBusted(int seat)
    {
        if (seat < 0 || seat >= Seats || states[seat] != LuckySeatState.Busted)
        {
            return 0;
        }

        var count = rowCounts[seat];
        for (var slot = 0; slot < count; slot++)
        {
            PushDiscard(rows[seat * RowCapacity + slot]);
        }

        rowCounts[seat] = 0;
        return count;
    }

    public int HandScore(int seat) =>
        LuckyHand.Score(Row(seat), states[seat] == LuckySeatState.Busted, SevenSeat == seat);

    public int UniqueNumbers(int seat) => LuckyHand.UniqueNumbers(Row(seat));

    private ReadOnlySpan<byte> Row(int seat) => rows.AsSpan(seat * RowCapacity, rowCounts[seat]);

    public bool HoldsNumber(int seat, int number) => FindFace(seat, number, -1) >= 0;

    public bool HasSecondChance(int seat) => FindFace(seat, LuckyCards.SecondChance, -1) >= 0;

    public float BustChance(int seat)
    {
        var source = deckCount > 0 ? deckCount : discardCount;
        if (source == 0)
        {
            return 0f;
        }

        var mask = 0;
        var count = rowCounts[seat];
        for (var slot = 0; slot < count; slot++)
        {
            var face = rows[seat * RowCapacity + slot];
            if (LuckyCards.IsNumber(face))
            {
                mask |= 1 << face;
            }
        }

        var busting = 0;
        for (var index = 0; index < source; index++)
        {
            var face = deckCount > 0 ? deck[index] : discard[index];
            if (LuckyCards.IsNumber(face) && (mask & (1 << face)) != 0)
            {
                busting++;
            }
        }

        return busting / (float)source;
    }

    public int CountRemaining(int face)
    {
        var count = 0;
        for (var index = 0; index < deckCount; index++)
        {
            if (deck[index] == face)
            {
                count++;
            }
        }

        return count;
    }

    internal bool PutOnTop(ReadOnlySpan<byte> faces)
    {
        for (var order = 0; order < faces.Length; order++)
        {
            var target = deckCount - 1 - order;
            if (target < 0)
            {
                return false;
            }

            var found = -1;
            for (var index = target; index >= 0; index--)
            {
                if (deck[index] == faces[order])
                {
                    found = index;
                    break;
                }
            }

            if (found < 0)
            {
                return false;
            }

            (deck[found], deck[target]) = (deck[target], deck[found]);
        }

        return true;
    }

    internal void SetTotal(int seat, int total)
    {
        totals[seat] = total;
    }

    private LuckyEvent DealNext()
    {
        while (dealCount < Seats)
        {
            var seat = (Dealer + 1 + dealCount) % Seats;
            dealCount++;
            if (states[seat] == LuckySeatState.Active)
            {
                return Draw(seat);
            }
        }

        dealing = false;
        return BeginTurn(FirstActiveFrom(Dealer + 1));
    }

    private LuckyEvent BeginTurn(int seat)
    {
        if (seat < 0)
        {
            return EndRound();
        }

        TurnSeat = seat;
        Phase = LuckyPhase.Turn;
        return new LuckyEvent(LuckyEventKind.Turn, seat);
    }

    private int FirstActiveFrom(int start)
    {
        for (var step = 0; step < Seats; step++)
        {
            var seat = (start + step) % Seats;
            if (states[seat] == LuckySeatState.Active)
            {
                return seat;
            }
        }

        return -1;
    }

    private LuckyEvent Draw(int seat)
    {
        var reshuffled = false;
        if (deckCount == 0)
        {
            reshuffled = Reshuffle();
            if (deckCount == 0)
            {
                return EndRound();
            }
        }

        if (rowCounts[seat] >= RowCapacity)
        {
            return EndRound();
        }

        var face = deck[--deckCount];
        var slot = rowCounts[seat];
        rows[seat * RowCapacity + slot] = face;
        rowCounts[seat] = slot + 1;
        drawnSeat = seat;
        drawnSlot = slot;
        resolvePending = true;
        Phase = LuckyPhase.Auto;
        return new LuckyEvent(LuckyEventKind.Drew, seat, -1, face, slot, -1, reshuffled);
    }

    private LuckyEvent Resolve()
    {
        resolvePending = false;
        var seat = drawnSeat;
        var slot = drawnSlot;
        var face = rows[seat * RowCapacity + slot];
        if (LuckyCards.IsNumber(face))
        {
            return ResolveNumber(seat, slot, face);
        }

        if (face == LuckyCards.SecondChance)
        {
            return ResolveSecondChance(seat, slot);
        }

        if (LuckyCards.IsTargeted(face))
        {
            if (forcedSeat == seat && forcedLeft > 0 && deferredCount < DeferredCapacity)
            {
                deferredFaces[deferredCount] = face;
                deferredActors[deferredCount] = seat;
                deferredCount++;
                return new LuckyEvent(LuckyEventKind.ActionDeferred, seat, -1, face, slot);
            }

            return BeginAction(seat, slot, face);
        }

        return new LuckyEvent(LuckyEventKind.Kept, seat, -1, face, slot);
    }

    private LuckyEvent ResolveNumber(int seat, int slot, int face)
    {
        var match = FindFace(seat, face, slot);
        if (match >= 0)
        {
            var chance = FindFace(seat, LuckyCards.SecondChance, -1);
            if (chance >= 0)
            {
                RemoveAt(seat, Math.Max(slot, chance));
                RemoveAt(seat, Math.Min(slot, chance));
                PushDiscard(face);
                PushDiscard(LuckyCards.SecondChance);
                return new LuckyEvent(LuckyEventKind.Saved, seat, -1, face, slot, chance);
            }

            states[seat] = LuckySeatState.Busted;
            if (forcedSeat == seat)
            {
                forcedLeft = 0;
            }

            DropDeferred(seat);
            return new LuckyEvent(LuckyEventKind.Bust, seat, -1, face, slot, match);
        }

        if (UniqueNumbers(seat) < SevenNumbers)
        {
            return new LuckyEvent(LuckyEventKind.Kept, seat, -1, face, slot);
        }

        SevenSeat = seat;
        sevens[seat]++;
        forcedLeft = 0;
        deferredCount = 0;
        EndRound();
        return new LuckyEvent(LuckyEventKind.Seven, seat, -1, face, slot);
    }

    private LuckyEvent ResolveSecondChance(int seat, int slot)
    {
        if (FindFace(seat, LuckyCards.SecondChance, slot) < 0)
        {
            return new LuckyEvent(LuckyEventKind.SecondChanceKept, seat, -1, LuckyCards.SecondChance, slot);
        }

        var recipients = 0;
        var only = -1;
        for (var other = 0; other < Seats; other++)
        {
            if (other == seat || states[other] != LuckySeatState.Active || HasSecondChance(other))
            {
                continue;
            }

            recipients++;
            only = other;
        }

        if (recipients == 0)
        {
            RemoveAt(seat, slot);
            PushDiscard(LuckyCards.SecondChance);
            return new LuckyEvent(LuckyEventKind.SecondChanceDiscarded, seat, -1, LuckyCards.SecondChance, slot);
        }

        if (recipients == 1)
        {
            return GiveSecondChance(seat, only, slot);
        }

        return AwaitTarget(seat, slot, LuckyCards.SecondChance);
    }

    private LuckyEvent BeginAction(int actor, int slot, int face)
    {
        var targets = 0;
        var only = -1;
        for (var seat = 0; seat < Seats; seat++)
        {
            if (states[seat] != LuckySeatState.Active)
            {
                continue;
            }

            targets++;
            only = seat;
        }

        if (targets == 0)
        {
            RemoveAt(actor, slot);
            PushDiscard(face);
            return new LuckyEvent(LuckyEventKind.ActionDiscarded, actor, -1, face, slot);
        }

        return targets == 1 ? ApplyAction(actor, slot, face, only) : AwaitTarget(actor, slot, face);
    }

    private LuckyEvent AwaitTarget(int actor, int slot, int face)
    {
        Actor = actor;
        PendingFace = face;
        pendingSlot = slot;
        Phase = LuckyPhase.Target;
        return new LuckyEvent(LuckyEventKind.ChooseTarget, actor, -1, face, slot);
    }

    private LuckyEvent ApplyAction(int actor, int slot, int face, int target)
    {
        RemoveAt(actor, slot);
        PushDiscard(face);
        if (face == LuckyCards.Freeze)
        {
            states[target] = LuckySeatState.Frozen;
            if (forcedSeat == target)
            {
                forcedLeft = 0;
            }

            return new LuckyEvent(LuckyEventKind.Frozen, actor, target, face, slot);
        }

        forcedSeat = target;
        forcedLeft = FlipThreeCards;
        return new LuckyEvent(LuckyEventKind.FlipThree, actor, target, face, slot);
    }

    private LuckyEvent GiveSecondChance(int actor, int target, int slot)
    {
        RemoveAt(actor, slot);
        var landing = rowCounts[target];
        rows[target * RowCapacity + landing] = LuckyCards.SecondChance;
        rowCounts[target] = landing + 1;
        return new LuckyEvent(LuckyEventKind.SecondChanceGiven, actor, target, LuckyCards.SecondChance, slot, landing);
    }

    private LuckyEvent ResolveDeferred()
    {
        var face = deferredFaces[0];
        var actor = deferredActors[0];
        for (var index = 1; index < deferredCount; index++)
        {
            deferredFaces[index - 1] = deferredFaces[index];
            deferredActors[index - 1] = deferredActors[index];
        }

        deferredCount--;
        var slot = states[actor] == LuckySeatState.Busted ? -1 : FindFace(actor, face, -1);
        return slot < 0 ? Advance() : BeginAction(actor, slot, face);
    }

    private void DropDeferred(int seat)
    {
        var kept = 0;
        for (var index = 0; index < deferredCount; index++)
        {
            if (deferredActors[index] == seat)
            {
                continue;
            }

            deferredFaces[kept] = deferredFaces[index];
            deferredActors[kept] = deferredActors[index];
            kept++;
        }

        deferredCount = kept;
    }

    private LuckyEvent EndRound()
    {
        ClearPending();
        dealing = false;
        forcedLeft = 0;
        deferredCount = 0;
        resolvePending = false;
        for (var seat = 0; seat < Seats; seat++)
        {
            var score = HandScore(seat);
            roundScores[seat] = score;
            totals[seat] += score;
            bestRounds[seat] = Math.Max(bestRounds[seat], score);
        }

        var leader = -1;
        var leading = int.MinValue;
        var tied = false;
        for (var seat = 0; seat < Seats; seat++)
        {
            if (totals[seat] > leading)
            {
                leader = seat;
                leading = totals[seat];
                tied = false;
            }
            else if (totals[seat] == leading)
            {
                tied = true;
            }
        }

        if (leading >= WinTarget && !tied)
        {
            Winner = leader;
            Phase = LuckyPhase.MatchOver;
            return new LuckyEvent(LuckyEventKind.MatchOver, leader);
        }

        Phase = LuckyPhase.RoundOver;
        return new LuckyEvent(LuckyEventKind.RoundOver, -1);
    }

    private void ClearPending()
    {
        Actor = -1;
        PendingFace = -1;
        pendingSlot = -1;
    }

    private int FindFace(int seat, int face, int excludeSlot)
    {
        var count = rowCounts[seat];
        for (var slot = 0; slot < count; slot++)
        {
            if (slot != excludeSlot && rows[seat * RowCapacity + slot] == face)
            {
                return slot;
            }
        }

        return -1;
    }

    private void RemoveAt(int seat, int slot)
    {
        var start = seat * RowCapacity;
        var count = rowCounts[seat];
        for (var index = slot + 1; index < count; index++)
        {
            rows[start + index - 1] = rows[start + index];
        }

        rowCounts[seat] = count - 1;
    }

    private void PushDiscard(int face)
    {
        if (discardCount < discard.Length)
        {
            discard[discardCount++] = face;
        }
    }

    private void CollectTable()
    {
        for (var seat = 0; seat < MaxSeats; seat++)
        {
            var count = rowCounts[seat];
            for (var slot = 0; slot < count; slot++)
            {
                PushDiscard(rows[seat * RowCapacity + slot]);
            }

            rowCounts[seat] = 0;
        }
    }

    private bool Reshuffle()
    {
        if (discardCount == 0)
        {
            return false;
        }

        for (var index = 0; index < discardCount; index++)
        {
            deck[index] = (byte)discard[index];
        }

        deckCount = discardCount;
        discardCount = 0;
        Shuffle();
        return true;
    }

    private void Shuffle()
    {
        for (var index = deckCount - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (deck[index], deck[swap]) = (deck[swap], deck[index]);
        }
    }
}
