using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed record VenueRoomView(
    string RoomId,
    VenueRoomKind Kind,
    int Epoch,
    long Seq,
    CasinoRoomSnapshotDto Snapshot,
    CasinoDiceTableStateDto? Dice,
    CasinoDeathrollStateDto? Deathroll,
    CasinoRaffleStateDto? Raffle)
{
    public int Currency => Kind switch
    {
        VenueRoomKind.Dice => Dice?.Currency ?? CasinoCurrencies.Practice,
        VenueRoomKind.Deathroll => Deathroll?.Currency ?? CasinoCurrencies.Practice,
        VenueRoomKind.Raffle => Raffle?.Currency ?? CasinoCurrencies.Practice,
        _ => CasinoCurrencies.Practice,
    };

    public bool Gil => Currency == CasinoCurrencies.Gil;

    public string Name => Kind switch
    {
        VenueRoomKind.Dice => Dice?.Name ?? string.Empty,
        VenueRoomKind.Deathroll => Deathroll?.Name ?? string.Empty,
        VenueRoomKind.Raffle => Raffle?.Name ?? string.Empty,
        _ => string.Empty,
    };

    public bool Ready => Kind switch
    {
        VenueRoomKind.Dice => Dice is not null,
        VenueRoomKind.Deathroll => Deathroll is not null,
        VenueRoomKind.Raffle => Raffle is not null,
        _ => false,
    };

    public static VenueRoomView? From(CasinoRoomState? state)
    {
        if (state is null)
        {
            return null;
        }

        var snapshot = state.Snapshot;
        var kind = VenueKinds.Of(snapshot.GameKind);
        return kind switch
        {
            VenueRoomKind.Dice => new VenueRoomView(state.RoomId, kind, state.Epoch, state.Seq, snapshot,
                Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoDiceTableStateDto), null, null),
            VenueRoomKind.Deathroll => new VenueRoomView(state.RoomId, kind, state.Epoch, state.Seq, snapshot, null,
                Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoDeathrollStateDto), null),
            VenueRoomKind.Raffle => new VenueRoomView(state.RoomId, kind, state.Epoch, state.Seq, snapshot, null, null,
                Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoRaffleStateDto)),
            _ => null,
        };
    }

    public static TState? Parse<TState>(string gameState, JsonTypeInfo<TState> typeInfo)
        where TState : class
    {
        if (gameState.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(gameState, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
