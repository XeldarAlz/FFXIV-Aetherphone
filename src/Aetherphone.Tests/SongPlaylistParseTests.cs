using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SongPlaylistParseTests
{
    [Fact]
    public void ParsesEntriesAndThePlaylistHeader()
    {
        const string output =
            "dQw4w9WgXcQ\t213\tRick Astley\tUCuAXFkgsw1L7xaCfnd5JJOw\tNever Gonna Give You Up\n" +
            "abcdefghijk\tNA\tSomeone\tNA\tNo duration here\n" +
            "zzzzzzzzzzz\tNA\tNA\tNA\t[Private video]\n" +
            "#playlist\tFavourites\tMy Channel\n";

        var result = SongLinkResolver.ParsePlaylistOutput(output);

        Assert.Equal("Favourites", result.Title);
        Assert.Equal("My Channel", result.Author);
        Assert.Equal(2, result.Entries.Length);
        Assert.Equal("dQw4w9WgXcQ", result.Entries[0].VideoId);
        Assert.Equal(213, result.Entries[0].DurationSeconds);
        Assert.Equal("UCuAXFkgsw1L7xaCfnd5JJOw", result.Entries[0].ChannelId);
        Assert.Equal(0, result.Entries[1].DurationSeconds);
        Assert.Equal(string.Empty, result.Entries[1].ChannelId);
    }

    [Fact]
    public void SearchParsingDropsEntriesWithoutDuration()
    {
        const string output =
            "dQw4w9WgXcQ\t213\tRick Astley\tUCuAXFkgsw1L7xaCfnd5JJOw\tNever Gonna Give You Up\r\n" +
            "abcdefghijk\tNA\tSomeone\tNA\tLive stream\r\n";

        var entries = SongLinkResolver.ParseEntries(output, false);

        Assert.Single(entries);
        Assert.Equal("Never Gonna Give You Up", entries[0].Title);
    }
}
