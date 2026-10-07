using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GameRoomCardMergeTests
{
    [Fact]
    public void ACreatedRoomJoinsTheListWithItsCodeBeforeTheDirectoryRefreshes()
    {
        var existing = new[] { new GameRoomCardDto("room-a", JoinCode: "AAAAAA") };
        var created = new GameRoomCardDto("room-b", Member: true, JoinCode: "BBBBBB");

        var merged = GameRoomsStore.WithCard(existing, created);

        Assert.Equal(2, merged.Length);
        Assert.Equal("BBBBBB", merged[0].JoinCode);
        Assert.Equal("AAAAAA", merged[1].JoinCode);
        Assert.Single(existing);
    }

    [Fact]
    public void AJoinedRoomAlreadyListedIsReplacedInPlace()
    {
        var existing = new[]
        {
            new GameRoomCardDto("room-a", JoinCode: string.Empty),
            new GameRoomCardDto("room-b", JoinCode: "BBBBBB"),
        };
        var joined = new GameRoomCardDto("room-a", Member: true, JoinCode: "AAAAAA");

        var merged = GameRoomsStore.WithCard(existing, joined);

        Assert.Equal(2, merged.Length);
        Assert.Equal("AAAAAA", merged[0].JoinCode);
        Assert.Equal(string.Empty, existing[0].JoinCode);
    }
}
