using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackDealPlayback
{
    public const int DealerSlot = BlackjackRules.SeatCount * BlackjackRules.MaxHandsPerSeat;

    public const int CardsPerHand = 12;

    private const int HandSlots = DealerSlot + 1;
    private const float SnappedBirth = -1000f;
    private const byte HandUnseen = 0;
    private const byte HandClosed = 1;
    private const byte HandOpen = 2;

    private readonly float[] births = new float[HandSlots * CardsPerHand];
    private readonly int[] knownCounts = new int[HandSlots];
    private readonly int[] currentCounts = new int[HandSlots];
    private readonly byte[] handStates = new byte[HandSlots];
    private readonly float[] handRevealStarts = new float[HandSlots];

    private string handId = string.Empty;
    private float clock;
    private float holeRevealStart = SnappedBirth;
    private int knownHoleCard = PlayingCards.FaceDown;
    private bool primed;
    private int pendingDealSounds;
    private float nextDealSoundAt;

    public BlackjackDealPlayback()
    {
        ForgetHandReveals();
    }

    public static int SlotOf(int seatIndex, int handIndex)
    {
        return seatIndex * BlackjackRules.MaxHandsPerSeat + handIndex;
    }

    public void Reset()
    {
        handId = string.Empty;
        clock = 0f;
        holeRevealStart = SnappedBirth;
        knownHoleCard = PlayingCards.FaceDown;
        primed = false;
        pendingDealSounds = 0;
        Array.Clear(knownCounts);
        ForgetHandReveals();
    }

    public void Update(CasinoBlackjackRoomStateDto? board, float deltaSeconds)
    {
        Update(board, deltaSeconds, -1);
    }

    public void Update(CasinoBlackjackRoomStateDto? board, float deltaSeconds, int ownSeat)
    {
        if (board is null)
        {
            Reset();
            return;
        }

        clock += deltaSeconds;
        var newHand = !string.Equals(handId, board.HandId, StringComparison.Ordinal);
        if (newHand)
        {
            handId = board.HandId;
            clock = 0f;
            holeRevealStart = SnappedBirth;
            knownHoleCard = PlayingCards.FaceDown;
            pendingDealSounds = 0;
            Array.Clear(knownCounts);
            ForgetHandReveals();
        }

        var snap = newHand && (!primed || board.Phase > BlackjackPhases.Dealing);
        primed = true;
        CountCards(board);
        AssignBirths(snap);
        ObserveHole(board, snap);
        ObserveHandReveals(board, snap, ownSeat);
        SoundNextDeal();
    }

    public float HandReveal(int slot)
    {
        if (slot < 0 || slot >= HandSlots || handRevealStarts[slot] <= SnappedBirth)
        {
            return 1f;
        }

        var progress = (clock - handRevealStarts[slot]) / BlackjackDealChoreography.RevealSeconds;
        if (progress <= 0f)
        {
            return 0f;
        }

        return progress >= 1f ? 1f : progress;
    }

    public bool HandRevealing(int slot)
    {
        return slot >= 0 && slot < HandSlots && handRevealStarts[slot] > SnappedBirth && HandReveal(slot) < 1f;
    }

    private void SoundNextDeal()
    {
        if (pendingDealSounds <= 0 || clock < nextDealSoundAt)
        {
            return;
        }

        pendingDealSounds--;
        nextDealSoundAt += BlackjackDealChoreography.StaggerSeconds;
        UiFeedback.Play(UiSound.CasinoDeal);
    }

    public float TravelOf(int slot, int cardIndex)
    {
        if (slot < 0 || slot >= HandSlots || cardIndex < 0)
        {
            return 1f;
        }

        if (cardIndex >= CardsPerHand)
        {
            return 1f;
        }

        var elapsed = clock - births[slot * CardsPerHand + cardIndex];
        if (elapsed <= 0f)
        {
            return 0f;
        }

        var travel = elapsed / BlackjackDealChoreography.TravelSeconds;
        return travel >= 1f ? 1f : travel;
    }

    public float HoleReveal()
    {
        if (holeRevealStart <= SnappedBirth)
        {
            return 1f;
        }

        var progress = (clock - holeRevealStart) / BlackjackDealChoreography.RevealSeconds;
        if (progress <= 0f)
        {
            return 0f;
        }

        return progress >= 1f ? 1f : progress;
    }

    public bool HoleRevealing()
    {
        return holeRevealStart > SnappedBirth && HoleReveal() < 1f;
    }

    private void CountCards(CasinoBlackjackRoomStateDto board)
    {
        Array.Clear(currentCounts);
        currentCounts[DealerSlot] = CapCount(board.DealerCards?.Length ?? 0);
        var seats = board.Seats;
        if (seats is null)
        {
            return;
        }

        for (var index = 0; index < seats.Length; index++)
        {
            var seatIndex = seats[index].SeatIndex;
            if (!BlackjackRules.IsSeat(seatIndex))
            {
                continue;
            }

            var hands = seats[index].Hands;
            if (hands is null)
            {
                continue;
            }

            for (var handIndex = 0; handIndex < hands.Length && handIndex < BlackjackRules.MaxHandsPerSeat;
                handIndex++)
            {
                currentCounts[SlotOf(seatIndex, handIndex)] = CapCount(hands[handIndex].Cards?.Length ?? 0);
            }
        }
    }

    private void AssignBirths(bool snap)
    {
        var batchIndex = 0;
        for (var row = 0; row < CardsPerHand; row++)
        {
            for (var slot = 0; slot < HandSlots; slot++)
            {
                if (row < knownCounts[slot] || row >= currentCounts[slot])
                {
                    continue;
                }

                births[slot * CardsPerHand + row] = snap
                    ? SnappedBirth
                    : clock + batchIndex * BlackjackDealChoreography.StaggerSeconds;
                batchIndex++;
            }
        }

        Array.Copy(currentCounts, knownCounts, HandSlots);
        if (snap || batchIndex == 0)
        {
            return;
        }

        if (pendingDealSounds <= 0)
        {
            nextDealSoundAt = clock;
        }

        pendingDealSounds += batchIndex;
    }

    private void ObserveHole(CasinoBlackjackRoomStateDto board, bool snap)
    {
        var dealerCards = board.DealerCards;
        var hole = dealerCards is { Length: > 1 } ? dealerCards[1] : PlayingCards.FaceDown;
        if (hole == knownHoleCard)
        {
            return;
        }

        if (!snap && knownHoleCard == PlayingCards.FaceDown && PlayingCards.IsCard(hole)
            && TravelOf(DealerSlot, 1) >= 1f)
        {
            holeRevealStart = clock;
            UiFeedback.Play(UiSound.GameCardFlip);
        }

        knownHoleCard = hole;
    }

    private void ObserveHandReveals(CasinoBlackjackRoomStateDto board, bool snap, int ownSeat)
    {
        var seats = board.Seats;
        if (seats is null)
        {
            return;
        }

        var flipped = false;
        for (var index = 0; index < seats.Length; index++)
        {
            var seatIndex = seats[index].SeatIndex;
            var hands = seats[index].Hands;
            if (!BlackjackRules.IsSeat(seatIndex) || seatIndex == ownSeat || hands is null)
            {
                continue;
            }

            for (var handIndex = 0; handIndex < hands.Length && handIndex < BlackjackRules.MaxHandsPerSeat;
                handIndex++)
            {
                var slot = SlotOf(seatIndex, handIndex);
                var state = HandStateOf(hands[handIndex].Cards);
                if (!snap && state == HandOpen && handStates[slot] == HandClosed)
                {
                    handRevealStarts[slot] = clock;
                    flipped = true;
                }

                handStates[slot] = state;
            }
        }

        if (flipped)
        {
            UiFeedback.Play(UiSound.GameCardFlip);
        }
    }

    private static byte HandStateOf(int[]? cards)
    {
        if (cards is null || cards.Length == 0)
        {
            return HandUnseen;
        }

        return PlayingCards.IsCard(cards[0]) ? HandOpen : HandClosed;
    }

    private void ForgetHandReveals()
    {
        Array.Clear(handStates);
        Array.Fill(handRevealStarts, SnappedBirth);
    }

    private static int CapCount(int count)
    {
        return count > CardsPerHand ? CardsPerHand : count;
    }
}
