using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlaylistLinkTests
{
    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLabc123XYZ", "https://www.youtube.com/playlist?list=PLabc123XYZ")]
    [InlineData("https://music.youtube.com/playlist?list=OLAK5uy_abcdefg", "https://www.youtube.com/playlist?list=OLAK5uy_abcdefg")]
    [InlineData("youtube.com/playlist?list=PLabc123XYZ", "https://www.youtube.com/playlist?list=PLabc123XYZ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLabc123XYZ&index=3", "https://www.youtube.com/playlist?list=PLabc123XYZ")]
    [InlineData("https://music.youtube.com/browse/VLPLabc123XYZ", "https://www.youtube.com/playlist?list=PLabc123XYZ")]
    public void PlaylistLinksNormalizeToThePlaylistPage(string text, string expected)
    {
        var link = PlaylistImporter.Classify(text);

        Assert.Equal(PlaylistLinkKind.Playlist, link.Kind);
        Assert.Equal(expected, link.Url);
        Assert.True(link.IsCollection);
    }

    [Fact]
    public void MixLinksKeepTheSeedVideo()
    {
        var link = PlaylistImporter.Classify("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=RDdQw4w9WgXcQ&start_radio=1");

        Assert.Equal(PlaylistLinkKind.Mix, link.Kind);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=RDdQw4w9WgXcQ", link.Url);
        Assert.Equal("dQw4w9WgXcQ", link.VideoId);
    }

    [Theory]
    [InlineData("https://www.youtube.com/channel/UCuAXFkgsw1L7xaCfnd5JJOw", "https://www.youtube.com/playlist?list=UUuAXFkgsw1L7xaCfnd5JJOw")]
    [InlineData("https://www.youtube.com/@SomeArtist", "https://www.youtube.com/@SomeArtist/videos")]
    [InlineData("https://www.youtube.com/@SomeArtist/featured", "https://www.youtube.com/@SomeArtist/videos")]
    [InlineData("https://www.youtube.com/c/SomeName", "https://www.youtube.com/c/SomeName/videos")]
    [InlineData("https://music.youtube.com/channel/UCuAXFkgsw1L7xaCfnd5JJOw", "https://www.youtube.com/playlist?list=UUuAXFkgsw1L7xaCfnd5JJOw")]
    public void ChannelLinksPointAtTheUploads(string text, string expected)
    {
        var link = PlaylistImporter.Classify(text);

        Assert.Equal(PlaylistLinkKind.Channel, link.Kind);
        Assert.Equal(expected, link.Url);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ&feature=share")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    public void SingleVideoLinksAreSongs(string text)
    {
        var link = PlaylistImporter.Classify(text);

        Assert.Equal(PlaylistLinkKind.Video, link.Kind);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", link.Url);
        Assert.False(link.IsCollection);
    }

    [Theory]
    [InlineData("")]
    [InlineData("lofi beats to study to")]
    [InlineData("https://example.com/playlist?list=PLabc123XYZ")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/feed/subscriptions")]
    [InlineData("ftp://www.youtube.com/playlist?list=PLabc123XYZ")]
    public void AnythingElseIsRejected(string text)
    {
        Assert.False(PlaylistImporter.Classify(text).IsValid);
    }

    [Fact]
    public void FetchedEntriesBecomeUniqueSongs()
    {
        var entries = new[]
        {
            new SongSearchEntry("aaaaaaaaaaa", "One", "Artist", "https://i.ytimg.com/vi/aaaaaaaaaaa/hqdefault.jpg", 100, "UC1"),
            new SongSearchEntry("bbbbbbbbbbb", "Two", "Artist", string.Empty, 0, "UC1"),
            new SongSearchEntry("aaaaaaaaaaa", "One again", "Artist", string.Empty, 100, "UC1"),
        };

        var songs = PlaylistImporter.ToSongs(entries);

        Assert.Equal(2, songs.Length);
        Assert.Equal("aaaaaaaaaaa", songs[0].VideoId);
        Assert.Equal("One", songs[0].Title);
        Assert.Equal("UC1", songs[1].ChannelId);
    }
}
