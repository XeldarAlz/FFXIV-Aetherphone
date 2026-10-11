using System.Text.Json;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Casino.Venue;

internal enum BroadcastGame : byte
{
    Blackjack,
    Holdem,
}

internal sealed record BroadcastSeat(
    int SeatIndex,
    string Name,
    long Stack,
    long Bet,
    int[] Cards,
    int Total,
    bool Acting,
    bool Out);

internal sealed record BroadcastTable(
    BroadcastGame Game,
    string HandId,
    int Phase,
    int[] DealerCards,
    int DealerTotal,
    int[] Board,
    long Pot,
    BroadcastSeat[] Seats)
{
    public static readonly BroadcastTable Empty = new(BroadcastGame.Blackjack, string.Empty, 0, Array.Empty<int>(), 0,
        Array.Empty<int>(), 0, Array.Empty<BroadcastSeat>());

    public bool Waiting => HandId.Length == 0;
}

internal static class BroadcastProjection
{
    public const int HiddenCard = -1;

    public const string HoldemKind = "casino.holdem";

    public static BroadcastTable FromBlackjack(CasinoBlackjackRoomStateDto board)
    {
        var seats = board.Seats ?? Array.Empty<CasinoBlackjackSeatDto>();
        var count = 0;
        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].UserId.Length > 0)
            {
                count++;
            }
        }

        var projected = new BroadcastSeat[count];
        var write = 0;
        for (var index = 0; index < seats.Length; index++)
        {
            var seat = seats[index];
            if (seat.UserId.Length == 0)
            {
                continue;
            }

            var hand = seat.Hands is { Length: > 0 } hands ? hands[0] : null;
            var cards = hand?.Cards ?? Array.Empty<int>();
            var bet = 0L;
            if (seat.Hands is not null)
            {
                for (var handIndex = 0; handIndex < seat.Hands.Length; handIndex++)
                {
                    bet += seat.Hands[handIndex].Bet;
                }
            }

            projected[write++] = new BroadcastSeat(seat.SeatIndex, seat.DisplayName, seat.Chips,
                bet > 0 ? bet : seat.Committed, cards, hand?.Total ?? 0, board.ActiveSeat == seat.SeatIndex,
                hand?.Busted ?? false);
        }

        return new BroadcastTable(BroadcastGame.Blackjack, board.HandId, board.Phase,
            board.DealerCards ?? Array.Empty<int>(), board.DealerTotal, Array.Empty<int>(), 0, projected);
    }

    public static BroadcastTable FromHoldem(string gameState)
    {
        if (gameState.Length == 0)
        {
            return BroadcastTable.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(gameState);
            var root = document.RootElement;
            var cursor = Int(root, "cursorSeat", -1);
            var seats = new List<BroadcastSeat>();
            if (root.TryGetProperty("seats", out var seatArray) && seatArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var seat in seatArray.EnumerateArray())
                {
                    var state = Int(seat, "state", 0);
                    if (state == 0)
                    {
                        continue;
                    }

                    var seatIndex = Int(seat, "seatIndex", -1);
                    seats.Add(new BroadcastSeat(seatIndex, Text(seat, "displayName"), Long(seat, "stack"),
                        Long(seat, "bet"), Cards(seat, "cards"), 0, seatIndex == cursor, state == 2));
                }
            }

            return new BroadcastTable(BroadcastGame.Holdem, Text(root, "handId"), Int(root, "phase", 0),
                Array.Empty<int>(), 0, Cards(root, "board"), Long(root, "potTotal"), seats.ToArray());
        }
        catch (JsonException)
        {
            return BroadcastTable.Empty;
        }
    }

    private static int Int(JsonElement element, string name, int fallback)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var parsed)
            ? parsed
            : fallback;
    }

    private static long Long(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var parsed)
            ? parsed
            : 0;
    }

    private static string Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int[] Cards(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<int>();
        }

        var cards = new int[value.GetArrayLength()];
        var index = 0;
        foreach (var card in value.EnumerateArray())
        {
            cards[index++] = card.ValueKind == JsonValueKind.Number && card.TryGetInt32(out var parsed)
                ? parsed
                : HiddenCard;
        }

        return cards;
    }
}
