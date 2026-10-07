using Aetherphone.Apps.Games.LuckyDraw;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;

namespace Aetherphone.Apps.Games.Online;

internal sealed class OnlineLuckyDrawModel : ILuckyScoreboard
{
    public const int MaxSeats = LuckyDrawBoard.MaxSeats;
    public const int RowCapacity = LuckyDrawBoard.RowCapacity;
    private const int NoSeat = -1;

    private readonly byte[] rows = new byte[MaxSeats * RowCapacity];
    private readonly int[] rowCounts = new int[MaxSeats];
    private readonly int[] states = new int[MaxSeats];
    private readonly int[] totals = new int[MaxSeats];
    private readonly int[] roundScores = new int[MaxSeats];
    private readonly int[] bestRounds = new int[MaxSeats];
    private readonly int[] sevens = new int[MaxSeats];
    private readonly int[] discard = new int[LuckyCards.DeckSize];
    private readonly int[] unseen = new int[LuckyCards.FaceCount];
    private int discardCount;

    public int Seats { get; private set; }

    public int DeckCount { get; private set; }

    public int Phase { get; private set; }

    public int Round { get; private set; }

    public int Dealer { get; private set; } = NoSeat;

    public int TurnSeat { get; private set; } = NoSeat;

    public bool Dealing { get; private set; }

    public int Actor { get; private set; } = NoSeat;

    public int PendingFace { get; private set; } = NoSeat;

    public int ForcedSeat { get; private set; } = NoSeat;

    public int ForcedLeft { get; private set; }

    public int SevenSeat { get; private set; } = NoSeat;

    public int Winner { get; private set; } = NoSeat;

    public int DiscardCount => discardCount;

    public ReadOnlySpan<int> Discard => discard.AsSpan(0, discardCount);

    public int RowCount(int seat) => rowCounts[seat];

    public int RowFace(int seat, int slot) => rows[seat * RowCapacity + slot];

    public int SeatState(int seat) => states[seat];

    public LuckySeatState State(int seat) => states[seat] switch
    {
        LuckyDrawWire.SeatActive => LuckySeatState.Active,
        LuckyDrawWire.SeatFrozen => LuckySeatState.Frozen,
        LuckyDrawWire.SeatBusted => LuckySeatState.Busted,
        _ => LuckySeatState.Stayed,
    };

    public int Total(int seat) => totals[seat];

    public int LastRoundScore(int seat) => roundScores[seat];

    public int BestRound(int seat) => bestRounds[seat];

    public int Sevens(int seat) => sevens[seat];

    public int HandScore(int seat) =>
        LuckyHand.Score(Row(seat), states[seat] == LuckyDrawWire.SeatBusted, SevenSeat == seat);

    public int UniqueNumbers(int seat) => LuckyHand.UniqueNumbers(Row(seat));

    public bool HasSecondChance(int seat) => Row(seat).IndexOf(LuckyCards.SecondChance) >= 0;

    public bool IsValidTarget(int seat)
    {
        if (Phase != LuckyDrawWire.PhaseTarget || !Valid(seat) || states[seat] != LuckyDrawWire.SeatActive)
        {
            return false;
        }

        return PendingFace != LuckyCards.SecondChance || (seat != Actor && !HasSecondChance(seat));
    }

    public void Load(LuckyDrawRoomStateDto board)
    {
        var players = board.Players ?? Array.Empty<LuckyDrawPlayerDto>();
        Seats = Math.Min(players.Length, MaxSeats);
        Array.Clear(rowCounts);
        for (var seat = 0; seat < Seats; seat++)
        {
            var player = players[seat];
            var cards = player.Cards ?? Array.Empty<int>();
            var count = Math.Min(cards.Length, RowCapacity);
            for (var slot = 0; slot < count; slot++)
            {
                rows[seat * RowCapacity + slot] = (byte)Math.Clamp(cards[slot], 0, LuckyCards.FaceCount - 1);
            }

            rowCounts[seat] = count;
            states[seat] = player.State;
        }

        var pile = board.Discard ?? Array.Empty<int>();
        discardCount = Math.Min(pile.Length, discard.Length);
        for (var index = 0; index < discardCount; index++)
        {
            discard[index] = Math.Clamp(pile[index], 0, LuckyCards.FaceCount - 1);
        }

        CopyScores(board);
        DeckCount = board.DeckCount;
        Phase = board.Phase;
        Round = board.Round;
        Dealer = board.Dealer;
        TurnSeat = board.TurnSeat;
        Dealing = board.Dealing;
        Actor = board.Actor;
        PendingFace = board.PendingFace;
        ForcedSeat = board.ForcedSeat;
        ForcedLeft = board.ForcedLeft;
        SevenSeat = board.SevenSeat;
        Winner = board.WinnerSeat;
    }

    public void Apply(LuckyDrawEventDto change, LuckyDrawRoomStateDto final)
    {
        var seat = change.Seat;
        switch (change.Kind)
        {
            case LuckyDrawWire.EventRound:
                BeginRound(change, final);
                return;
            case LuckyDrawWire.EventDrew:
                Drew(seat, change.Face, change.Reshuffled);
                return;
            case LuckyDrawWire.EventBust:
                SetState(seat, LuckyDrawWire.SeatBusted);
                if (ForcedSeat == seat)
                {
                    ForcedLeft = 0;
                }

                return;
            case LuckyDrawWire.EventSwept:
                Sweep(seat);
                return;
            case LuckyDrawWire.EventSaved:
                RemoveAt(seat, Math.Max(change.Slot, change.OtherSlot));
                RemoveAt(seat, Math.Min(change.Slot, change.OtherSlot));
                PushDiscard(change.Face);
                PushDiscard(LuckyCards.SecondChance);
                return;
            case LuckyDrawWire.EventSeven:
                SevenSeat = seat;
                ForcedLeft = 0;
                return;
            case LuckyDrawWire.EventChanceGiven:
                RemoveAt(seat, change.Slot);
                Append(change.Target, LuckyCards.SecondChance);
                ClearPending();
                return;
            case LuckyDrawWire.EventChanceDiscarded:
            case LuckyDrawWire.EventDiscarded:
                RemoveAt(seat, change.Slot);
                PushDiscard(change.Face);
                ClearPending();
                return;
            case LuckyDrawWire.EventChooseTarget:
                Actor = seat;
                PendingFace = change.Face;
                Phase = LuckyDrawWire.PhaseTarget;
                return;
            case LuckyDrawWire.EventFrozen:
                RemoveAt(seat, change.Slot);
                PushDiscard(change.Face);
                SetState(change.Target, LuckyDrawWire.SeatFrozen);
                if (ForcedSeat == change.Target)
                {
                    ForcedLeft = 0;
                }

                ClearPending();
                return;
            case LuckyDrawWire.EventFlipThree:
                RemoveAt(seat, change.Slot);
                PushDiscard(change.Face);
                ForcedSeat = change.Target;
                ForcedLeft = LuckyDrawBoard.FlipThreeCards;
                ClearPending();
                return;
            case LuckyDrawWire.EventStayed:
                SetState(seat, LuckyDrawWire.SeatStayed);
                return;
            case LuckyDrawWire.EventTurn:
                TurnSeat = seat;
                Phase = LuckyDrawWire.PhaseTurn;
                Dealing = false;
                ClearPending();
                return;
            case LuckyDrawWire.EventRoundOver:
                Phase = LuckyDrawWire.PhaseRoundOver;
                Dealing = false;
                ForcedLeft = 0;
                ClearPending();
                CopyScores(final);
                return;
            case LuckyDrawWire.EventMatchOver:
                Phase = LuckyDrawWire.PhaseMatchOver;
                Winner = seat;
                ClearPending();
                CopyScores(final);
                return;
            default:
                return;
        }
    }

    public bool Matches(LuckyDrawRoomStateDto board)
    {
        var players = board.Players ?? Array.Empty<LuckyDrawPlayerDto>();
        var pile = board.Discard ?? Array.Empty<int>();
        if (players.Length != Seats || pile.Length != discardCount || board.DeckCount != DeckCount)
        {
            return false;
        }

        for (var seat = 0; seat < Seats; seat++)
        {
            var cards = players[seat].Cards ?? Array.Empty<int>();
            if (cards.Length != rowCounts[seat] || players[seat].State != states[seat])
            {
                return false;
            }

            for (var slot = 0; slot < cards.Length; slot++)
            {
                if (cards[slot] != rows[seat * RowCapacity + slot])
                {
                    return false;
                }
            }
        }

        for (var index = 0; index < discardCount; index++)
        {
            if (pile[index] != discard[index])
            {
                return false;
            }
        }

        return true;
    }

    public float BustChance(int seat)
    {
        if (seat < 0 || seat >= Seats)
        {
            return 0f;
        }

        var source = DeckCount > 0 ? DeckCount : discardCount;
        if (source == 0)
        {
            return 0f;
        }

        if (DeckCount > 0)
        {
            CountUnseen();
        }
        else
        {
            Array.Clear(unseen);
            for (var index = 0; index < discardCount; index++)
            {
                unseen[discard[index]]++;
            }
        }

        var held = 0;
        var row = Row(seat);
        for (var slot = 0; slot < row.Length; slot++)
        {
            if (LuckyCards.IsNumber(row[slot]))
            {
                held |= 1 << row[slot];
            }
        }

        var busting = 0;
        for (var number = 0; number <= LuckyCards.MaxNumber; number++)
        {
            if ((held & (1 << number)) != 0)
            {
                busting += unseen[number];
            }
        }

        return Math.Clamp(busting / (float)source, 0f, 1f);
    }

    private void CountUnseen()
    {
        for (var face = 0; face < LuckyCards.FaceCount; face++)
        {
            unseen[face] = LuckyCards.CopiesOf(face);
        }

        for (var seat = 0; seat < Seats; seat++)
        {
            var row = Row(seat);
            for (var slot = 0; slot < row.Length; slot++)
            {
                unseen[row[slot]]--;
            }
        }

        for (var index = 0; index < discardCount; index++)
        {
            unseen[discard[index]]--;
        }

        for (var face = 0; face < LuckyCards.FaceCount; face++)
        {
            unseen[face] = Math.Max(0, unseen[face]);
        }
    }

    private void BeginRound(LuckyDrawEventDto change, LuckyDrawRoomStateDto final)
    {
        var players = final.Players ?? Array.Empty<LuckyDrawPlayerDto>();
        Seats = Math.Min(players.Length, MaxSeats);
        Round = change.Target;
        Dealer = change.Seat;
        if (Round <= 1)
        {
            Array.Clear(rowCounts);
            Array.Clear(totals);
            Array.Clear(roundScores);
            Array.Clear(bestRounds);
            Array.Clear(sevens);
            discardCount = 0;
            DeckCount = LuckyCards.DeckSize;
            Winner = NoSeat;
        }

        for (var seat = 0; seat < Seats; seat++)
        {
            Sweep(seat);
            states[seat] = players[seat].Away ? LuckyDrawWire.SeatOut : LuckyDrawWire.SeatActive;
            roundScores[seat] = 0;
        }

        SevenSeat = NoSeat;
        ForcedSeat = NoSeat;
        ForcedLeft = 0;
        ClearPending();
        Dealing = true;
        Phase = LuckyDrawWire.PhaseIdle;
        TurnSeat = Seats > 0 ? (Dealer + 1) % Seats : NoSeat;
    }

    private void Drew(int seat, int face, bool reshuffled)
    {
        if (reshuffled)
        {
            DeckCount = discardCount;
            discardCount = 0;
        }

        DeckCount = Math.Max(0, DeckCount - 1);
        Append(seat, face);
        if (ForcedSeat == seat && ForcedLeft > 0)
        {
            ForcedLeft--;
        }
    }

    private void CopyScores(LuckyDrawRoomStateDto board)
    {
        var players = board.Players ?? Array.Empty<LuckyDrawPlayerDto>();
        var count = Math.Min(players.Length, MaxSeats);
        for (var seat = 0; seat < count; seat++)
        {
            totals[seat] = players[seat].Total;
            roundScores[seat] = players[seat].RoundScore;
            bestRounds[seat] = players[seat].BestRound;
            sevens[seat] = players[seat].Sevens;
        }
    }

    private void ClearPending()
    {
        Actor = NoSeat;
        PendingFace = NoSeat;
    }

    private bool Valid(int seat) => seat >= 0 && seat < Seats;

    private void SetState(int seat, int state)
    {
        if (Valid(seat))
        {
            states[seat] = state;
        }
    }

    private void Append(int seat, int face)
    {
        if (!Valid(seat) || rowCounts[seat] >= RowCapacity)
        {
            return;
        }

        rows[seat * RowCapacity + rowCounts[seat]] = (byte)Math.Clamp(face, 0, LuckyCards.FaceCount - 1);
        rowCounts[seat]++;
    }

    private void RemoveAt(int seat, int slot)
    {
        if (!Valid(seat) || slot < 0 || slot >= rowCounts[seat])
        {
            return;
        }

        var start = seat * RowCapacity;
        var count = rowCounts[seat];
        for (var index = slot + 1; index < count; index++)
        {
            rows[start + index - 1] = rows[start + index];
        }

        rowCounts[seat] = count - 1;
    }

    private void Sweep(int seat)
    {
        if (!Valid(seat))
        {
            return;
        }

        var count = rowCounts[seat];
        for (var slot = 0; slot < count; slot++)
        {
            PushDiscard(rows[seat * RowCapacity + slot]);
        }

        rowCounts[seat] = 0;
    }

    private void PushDiscard(int face)
    {
        if (discardCount < discard.Length && face >= 0 && face < LuckyCards.FaceCount)
        {
            discard[discardCount++] = face;
        }
    }

    private ReadOnlySpan<byte> Row(int seat) =>
        Valid(seat) ? rows.AsSpan(seat * RowCapacity, rowCounts[seat]) : ReadOnlySpan<byte>.Empty;
}
