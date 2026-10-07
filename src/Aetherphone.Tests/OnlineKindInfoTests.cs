using System.Reflection;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OnlineKindInfoTests
{
    private const string KindSuffix = "Kind";

    private static List<string> WireKinds()
    {
        var kinds = new List<string>();
        var fields = typeof(GameRoomWire).GetFields(BindingFlags.Public | BindingFlags.Static);
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith(KindSuffix, StringComparison.Ordinal))
            {
                kinds.Add((string)field.GetRawConstantValue()!);
            }
        }

        return kinds;
    }

    [Fact]
    public void EveryWireKindHasATableEntry()
    {
        var kinds = WireKinds();

        Assert.NotEmpty(kinds);
        for (var index = 0; index < kinds.Count; index++)
        {
            Assert.True(OnlineGameArt.IndexOf(kinds[index]) >= 0, $"{kinds[index]} has no OnlineKindInfo");
        }

        Assert.Equal(kinds.Count, OnlineGameArt.Infos.Length);
    }

    [Fact]
    public void KindsFollowTheTableOrder()
    {
        Assert.Equal(OnlineGameArt.Infos.Length, OnlineGameArt.Kinds.Length);
        for (var index = 0; index < OnlineGameArt.Infos.Length; index++)
        {
            Assert.Equal(OnlineGameArt.Infos[index].Kind, OnlineGameArt.Kinds[index]);
            Assert.Equal(index, OnlineGameArt.IndexOf(OnlineGameArt.Kinds[index]));
        }

        Assert.Equal(GameRoomWire.UnoKind, OnlineGameArt.Kinds[0]);
    }

    [Fact]
    public void AccentAndHostIdsAreUniqueAndKeepTheirOldValues()
    {
        var accents = new HashSet<string>(StringComparer.Ordinal);
        var hosts = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < OnlineGameArt.Infos.Length; index++)
        {
            ref readonly var info = ref OnlineGameArt.Infos[index];
            Assert.True(accents.Add(info.AccentId), $"duplicate accent id {info.AccentId}");
            Assert.True(hosts.Add(info.HostId), $"duplicate host id {info.HostId}");
            Assert.Equal("games.host." + info.AccentId, info.HostId);
            Assert.InRange(info.MaxPlayers, OnlineGameArt.MinPlayers, 6);
        }

        Assert.Equal("uno", OnlineGameArt.AccentId(GameRoomWire.UnoKind));
        Assert.Equal("connectfour", OnlineGameArt.AccentId(GameRoomWire.ConnectFourKind));
        Assert.Equal(6, OnlineGameArt.MaxPlayers(GameRoomWire.UnoKind));
        Assert.Equal(2, OnlineGameArt.MaxPlayers(GameRoomWire.ChessKind));
        Assert.Equal(2, OnlineGameArt.MaxPlayers(GameRoomWire.PoolKind));
    }

    [Fact]
    public void AnUnknownKindFallsBackToUno()
    {
        Assert.Equal(-1, OnlineGameArt.IndexOf("games.unknown"));
        Assert.Equal(-1, OnlineGameArt.IndexOf(null));
        Assert.Equal("uno", OnlineGameArt.AccentId("games.unknown"));
        Assert.Equal(6, OnlineGameArt.MaxPlayers(string.Empty));
    }

    [Fact]
    public void EveryKindHasAHint()
    {
        for (var index = 0; index < OnlineGameArt.Kinds.Length; index++)
        {
            Assert.False(string.IsNullOrWhiteSpace(OnlineGameArt.Hint(OnlineGameArt.Kinds[index])));
        }

        Assert.Contains("6", OnlineGameArt.Hint(GameRoomWire.UnoKind), StringComparison.Ordinal);
    }
}
