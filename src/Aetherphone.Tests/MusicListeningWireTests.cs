using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MusicListeningWireTests
{
    private const string ServerFriends =
        "{\"friends\":[{\"userId\":\"u1\",\"displayName\":\"Ana\",\"handle\":\"ana\",\"avatarUrl\":\"https://cdn/a.png\","
        + "\"track\":{\"videoId\":\"dQw4w9WgXcQ\",\"title\":\"Song\",\"author\":\"Artist\","
        + "\"thumbnailUrl\":\"https://i/t.jpg\",\"durationSeconds\":213},"
        + "\"positionSeconds\":42.5,\"paused\":true,\"updatedAtUnixMs\":1791000000123,\"jamCode\":\"ABCD12\"},"
        + "{\"userId\":\"u2\",\"displayName\":\"\",\"handle\":\"bo\",\"track\":{\"videoId\":\"x\"},"
        + "\"positionSeconds\":0,\"paused\":false,\"updatedAtUnixMs\":1}]}";

    [Fact]
    public void PathsMatchTheContract()
    {
        Assert.Equal("/music/listening", MusicListeningClient.ListeningPath);
        Assert.Equal("/music/listening/friends", MusicListeningClient.FriendsPath);
    }

    [Fact]
    public void PublishBodyUsesCamelCaseFields()
    {
        var request = new ListeningUpdateRequest("v1", "Title", "Author", "https://i/t.jpg", 200, 12.5, false,
            "ABCD12");

        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.ListeningUpdateRequest);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("v1", root.GetProperty("videoId").GetString());
        Assert.Equal("Title", root.GetProperty("title").GetString());
        Assert.Equal("Author", root.GetProperty("author").GetString());
        Assert.Equal("https://i/t.jpg", root.GetProperty("thumbnailUrl").GetString());
        Assert.Equal(200d, root.GetProperty("durationSeconds").GetDouble());
        Assert.Equal(12.5d, root.GetProperty("positionSeconds").GetDouble());
        Assert.False(root.GetProperty("paused").GetBoolean());
        Assert.Equal("ABCD12", root.GetProperty("jamCode").GetString());
    }

    [Fact]
    public void PublishBodyOmitsJamCodeWhenNotInAJam()
    {
        var request = new ListeningUpdateRequest("v1", "Title", "Author", string.Empty, 200, 0, true);

        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.ListeningUpdateRequest);

        Assert.DoesNotContain("jamCode", json);
    }

    [Fact]
    public void FriendsResponseDeserializes()
    {
        var page = JsonSerializer.Deserialize(ServerFriends, AethernetJsonContext.Default.ListeningFriendsPage);

        Assert.NotNull(page);
        Assert.NotNull(page.Friends);
        Assert.Equal(2, page.Friends.Length);
        var first = page.Friends[0];
        Assert.Equal("u1", first.UserId);
        Assert.Equal("Ana", first.DisplayName);
        Assert.Equal("ana", first.Handle);
        Assert.Equal("https://cdn/a.png", first.AvatarUrl);
        Assert.NotNull(first.Track);
        Assert.Equal("dQw4w9WgXcQ", first.Track.VideoId);
        Assert.Equal(213d, first.Track.DurationSeconds);
        Assert.Equal(42.5d, first.PositionSeconds);
        Assert.True(first.Paused);
        Assert.Equal(1791000000123L, first.UpdatedAtUnixMs);
        Assert.Equal("ABCD12", first.JamCode);
        Assert.Null(page.Friends[1].JamCode);
        Assert.Null(page.Friends[1].AvatarUrl);
    }

    [Fact]
    public void EmptyBodyYieldsNoFriends()
    {
        var page = JsonSerializer.Deserialize("{}", AethernetJsonContext.Default.ListeningFriendsPage);

        Assert.NotNull(page);
        Assert.Empty(Core.Songs.ListeningCadence.Usable(page.Friends));
    }
}
