using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Casino.Tables;

internal readonly struct BlackjackHandEntry
{
    public readonly long Sequence;
    public readonly long HandIndex;
    public readonly long PlayedAtUnixSeconds;
    public readonly long Net;
    public readonly CasinoBlackjackHandDto[] Hands;
    public readonly int[] DealerCards;
    public readonly int DealerTotal;
    public readonly BlackjackSeatRecap Recap;

    public BlackjackHandEntry(long sequence, long handIndex, long playedAtUnixSeconds, long net,
        CasinoBlackjackHandDto[] hands, int[] dealerCards, int dealerTotal, in BlackjackSeatRecap recap)
    {
        Sequence = sequence;
        HandIndex = handIndex;
        PlayedAtUnixSeconds = playedAtUnixSeconds;
        Net = net;
        Hands = hands;
        DealerCards = dealerCards;
        DealerTotal = dealerTotal;
        Recap = recap;
    }
}

internal sealed class BlackjackHandLog
{
    public const int Capacity = 10;

    private readonly BlackjackHandEntry[] entries = new BlackjackHandEntry[Capacity];
    private string roomId = string.Empty;
    private long sequence;
    private int next;
    private int count;

    public int Count => count;

    public void Follow(string room)
    {
        if (string.Equals(roomId, room, StringComparison.Ordinal))
        {
            return;
        }

        roomId = room;
        Array.Clear(entries);
        next = 0;
        count = 0;
    }

    public void Record(long handIndex, long playedAtUnixSeconds, long net, CasinoBlackjackHandDto[] hands,
        int[] dealerCards, int dealerTotal, in BlackjackSeatRecap recap)
    {
        if (count > 0 && Newest(0).HandIndex == handIndex)
        {
            return;
        }

        sequence++;
        entries[next] = new BlackjackHandEntry(sequence, handIndex, playedAtUnixSeconds, net, hands, dealerCards,
            dealerTotal, recap);
        next = (next + 1) % Capacity;
        if (count < Capacity)
        {
            count++;
        }
    }

    public ref readonly BlackjackHandEntry Newest(int offset) => ref entries[(next - 1 - offset + Capacity * 2) % Capacity];
}
