using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GameRoomResignGuardTests
{
    private const string Room = "room-resign";

    [Fact]
    public void TheRoundTheSheetWasOpenedForStillAccepts()
    {
        var held = Playing(Room, 4);

        Assert.True(held.IsPlayingRound(Room, 4));
    }

    [Fact]
    public void ANewRoundStartedWhileTheSheetWasOpenRefuses()
    {
        var held = Playing(Room, 4);
        var finished = GameRoomSession.Applied(held, 1, 11,
            new GameRoomEventDto(Phase: GameRoomWire.PhaseFinished, RoundIndex: 4));
        var restarted = GameRoomSession.Applied(finished, 1, 12,
            new GameRoomEventDto(Phase: GameRoomWire.PhasePlaying, RoundIndex: 5));

        Assert.False(finished.IsPlayingRound(Room, 4));
        Assert.False(restarted.IsPlayingRound(Room, 4));
    }

    [Fact]
    public void ADifferentRoomRefuses()
    {
        var held = Playing("room-other", 4);

        Assert.False(held.IsPlayingRound(Room, 4));
    }

    private static GameRoomState Playing(string roomId, long roundIndex) =>
        GameRoomSession.Build(roomId, 1, 10,
            new GameRoomSnapshotDto(roomId, Phase: GameRoomWire.PhasePlaying, RoundIndex: roundIndex));
}
