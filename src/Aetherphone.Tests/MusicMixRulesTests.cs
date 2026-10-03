using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MusicMixRulesTests
{
    [Fact]
    public void Favourites_interleave_most_played_and_loved_without_duplicates()
    {
        var mostPlayed = new[] { Make("a"), Make("b"), Make("c") };
        var loved = new[] { Make("b"), Make("x"), Make("y") };

        var mix = MusicMixRules.Favourites(mostPlayed, loved, 10);

        Assert.Equal(new[] { "a", "b", "x", "c", "y" }, Ids(mix));
    }

    [Fact]
    public void Favourites_respect_the_size_cap()
    {
        var mostPlayed = new[] { Make("a"), Make("b"), Make("c") };
        var loved = new[] { Make("x"), Make("y") };

        Assert.Equal(3, MusicMixRules.Favourites(mostPlayed, loved, 3).Length);
        Assert.Empty(MusicMixRules.Favourites(mostPlayed, loved, 0));
    }

    [Fact]
    public void Discovery_takes_turns_between_seed_mixes_and_drops_known_songs()
    {
        var mixes = new[]
        {
            new[] { Make("a1"), Make("known"), Make("a3") },
            new[] { Make("b1"), Make("a1"), Make("b3") },
        };
        var known = new HashSet<string>(StringComparer.Ordinal) { "known" };

        var mix = MusicMixRules.Discovery(mixes, known, 10);

        Assert.Equal(new[] { "a1", "b1", "a3", "b3" }, Ids(mix));
        Assert.Single(known);
    }

    [Fact]
    public void Seeds_rotate_by_day_and_stay_stable_within_a_day()
    {
        var loved = new[] { Make("l1"), Make("l2") };
        var mostPlayed = new[] { Make("l1"), Make("m1") };

        var monday = MusicMixRules.PickSeeds(loved, mostPlayed, 2, 10);
        var mondayAgain = MusicMixRules.PickSeeds(loved, mostPlayed, 2, 10);
        var tuesday = MusicMixRules.PickSeeds(loved, mostPlayed, 2, 11);

        Assert.Equal(monday, mondayAgain);
        Assert.Equal(new[] { "l2", "m1" }, monday);
        Assert.Equal(new[] { "m1", "l1" }, tuesday);
    }

    [Fact]
    public void Seeds_are_empty_without_any_listening_history()
    {
        Assert.Empty(MusicMixRules.PickSeeds(ReadOnlySpan<Song>.Empty, ReadOnlySpan<Song>.Empty, 3, 5));
    }

    [Fact]
    public void Because_seed_comes_from_the_most_recent_songs()
    {
        var recent = new[] { Make("r0"), Make("r1"), Make("r2"), Make("r3"), Make("r4"), Make("r5"), Make("r6") };

        for (var day = 0; day < 20; day++)
        {
            var seed = MusicMixRules.BecauseSeed(recent, day);
            Assert.True(Array.IndexOf(Ids(recent), seed.VideoId) < MusicMixRules.BecauseWindow);
        }

        Assert.True(MusicMixRules.BecauseSeed(ReadOnlySpan<Song>.Empty, 3).IsEmpty);
    }

    [Fact]
    public void Daily_shuffle_is_a_stable_permutation_per_day()
    {
        var songs = new[] { Make("a"), Make("b"), Make("c"), Make("d"), Make("e"), Make("f") };

        var first = Ids(MusicMixRules.DailyShuffle(songs, 42));
        var second = Ids(MusicMixRules.DailyShuffle(songs, 42));
        var sorted = (string[])first.Clone();
        Array.Sort(sorted, StringComparer.Ordinal);

        Assert.Equal(first, second);
        Assert.Equal(Ids(songs), sorted);
        Assert.Equal("a", songs[0].VideoId);
    }

    [Fact]
    public void Day_counts_whole_utc_days()
    {
        Assert.Equal(0, MusicMixRules.Day(MusicMixRules.SecondsPerDay - 1));
        Assert.Equal(1, MusicMixRules.Day(MusicMixRules.SecondsPerDay));
    }

    private static Song Make(string id) => new(id, id + " title", "Artist", string.Empty, 180, "channel");

    private static string[] Ids(Song[] songs)
    {
        var ids = new string[songs.Length];
        for (var index = 0; index < songs.Length; index++)
        {
            ids[index] = songs[index].VideoId;
        }

        return ids;
    }
}
