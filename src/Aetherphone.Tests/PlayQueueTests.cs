using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlayQueueTests
{
    private static Song[] Album(int count)
    {
        var songs = new Song[count];
        for (var index = 0; index < count; index++)
        {
            songs[index] = new Song("video" + index.ToString("D6"), "Track " + index, "Artist", string.Empty, 180);
        }

        return songs;
    }

    private static string CurrentId(PlayQueue queue) => queue.Current.Song.VideoId;

    [Fact]
    public void SetContextStartsAtIndexAndQueuesTheRestInOrder()
    {
        var queue = new PlayQueue();
        var album = Album(5);

        Assert.True(queue.SetContext(album, 2, false, "playlist", "Mix"));

        Assert.Equal(album[2].VideoId, CurrentId(queue));
        Assert.Equal(2, queue.QueuedCount);
        Assert.Equal(album[3].VideoId, queue.QueuedAt(0).Song.VideoId);
        Assert.Equal(album[4].VideoId, queue.QueuedAt(1).Song.VideoId);
        Assert.Equal("playlist", queue.ContextId);
    }

    [Fact]
    public void AdvanceWalksTheQueueThenStopsWithRepeatOff()
    {
        var queue = new PlayQueue();
        var album = Album(3);
        queue.SetContext(album, 0, false, string.Empty, string.Empty);

        Assert.True(queue.TryAdvance(SongRepeatMode.Off, out _));
        Assert.True(queue.TryAdvance(SongRepeatMode.Off, out _));
        Assert.Equal(album[2].VideoId, CurrentId(queue));
        Assert.False(queue.TryAdvance(SongRepeatMode.Off, out _));
        Assert.Equal(2, queue.HistoryCount);
    }

    [Fact]
    public void RepeatAllWrapsToTheStartOfTheContext()
    {
        var queue = new PlayQueue();
        var album = Album(2);
        queue.SetContext(album, 0, false, string.Empty, string.Empty);
        queue.TryAdvance(SongRepeatMode.All, out _);

        Assert.True(queue.TryAdvance(SongRepeatMode.All, out var wrapped));

        Assert.Equal(album[0].VideoId, wrapped.Song.VideoId);
        Assert.Equal(1, queue.QueuedCount);
    }

    [Fact]
    public void PlayNextJumpsAheadOfTheContextAndPlayLastGoesToTheEnd()
    {
        var queue = new PlayQueue();
        var album = Album(3);
        var extraNext = new Song("nextnext001", "Next", "Artist", string.Empty, 100);
        var extraLast = new Song("lastlast001", "Last", "Artist", string.Empty, 100);
        queue.SetContext(album, 0, false, string.Empty, string.Empty);

        queue.PlayLast(extraLast);
        queue.PlayNext(extraNext);

        Assert.Equal(1, queue.PlayingNextCount);
        Assert.Equal(extraNext.VideoId, queue.QueuedAt(0).Song.VideoId);
        Assert.Equal(extraLast.VideoId, queue.QueuedAt(queue.QueuedCount - 1).Song.VideoId);
        queue.TryAdvance(SongRepeatMode.Off, out var next);
        Assert.Equal(extraNext.VideoId, next.Song.VideoId);
    }

    [Fact]
    public void BackReturnsTheCurrentEntryToTheFrontOfItsSection()
    {
        var queue = new PlayQueue();
        var album = Album(3);
        queue.SetContext(album, 0, false, string.Empty, string.Empty);
        queue.TryAdvance(SongRepeatMode.Off, out _);

        Assert.True(queue.TryBack(out var previous));

        Assert.Equal(album[0].VideoId, previous.Song.VideoId);
        Assert.Equal(album[1].VideoId, queue.QueuedAt(0).Song.VideoId);
        Assert.Equal(2, queue.QueuedCount);
    }

    [Fact]
    public void JumpToMovesSkippedEntriesIntoHistory()
    {
        var queue = new PlayQueue();
        var album = Album(5);
        queue.SetContext(album, 0, false, string.Empty, string.Empty);
        var target = queue.QueuedAt(2);

        Assert.True(queue.JumpTo(target.Id, out var landed));

        Assert.Equal(album[3].VideoId, landed.Song.VideoId);
        Assert.Equal(3, queue.HistoryCount);
        Assert.Equal(1, queue.QueuedCount);
    }

    [Fact]
    public void MoveReordersAcrossSections()
    {
        var queue = new PlayQueue();
        var album = Album(4);
        queue.SetContext(album, 0, false, string.Empty, string.Empty);
        var last = queue.QueuedAt(2);

        Assert.True(queue.Move(last.Id, 0));

        Assert.Equal(album[3].VideoId, queue.QueuedAt(0).Song.VideoId);
        Assert.Equal(album[1].VideoId, queue.QueuedAt(1).Song.VideoId);
        Assert.Equal(album[2].VideoId, queue.QueuedAt(2).Song.VideoId);
    }

    [Fact]
    public void RemoveDropsAnEntryById()
    {
        var queue = new PlayQueue();
        queue.SetContext(Album(3), 0, false, string.Empty, string.Empty);
        var first = queue.QueuedAt(0);

        Assert.True(queue.Remove(first.Id));
        Assert.Equal(1, queue.QueuedCount);
        Assert.False(queue.Remove(first.Id));
    }

    [Fact]
    public void ShuffleKeepsEveryOtherSongAndUnshuffleRestoresContextOrder()
    {
        var queue = new PlayQueue();
        var album = Album(30);
        queue.SetContext(album, 10, true, string.Empty, string.Empty);

        Assert.Equal(album[10].VideoId, CurrentId(queue));
        Assert.Equal(29, queue.QueuedCount);
        var seen = new HashSet<string>();
        for (var index = 0; index < queue.QueuedCount; index++)
        {
            seen.Add(queue.QueuedAt(index).Song.VideoId);
        }

        Assert.Equal(29, seen.Count);
        Assert.DoesNotContain(album[10].VideoId, seen);

        queue.SetShuffle(false);

        Assert.Equal(album[11].VideoId, queue.QueuedAt(0).Song.VideoId);
        Assert.Equal(album[29].VideoId, queue.QueuedAt(18).Song.VideoId);
        Assert.Equal(album[0].VideoId, queue.QueuedAt(19).Song.VideoId);
    }

    [Fact]
    public void AutoplaySkipsSongsAlreadyQueuedAndPlaysAfterTheQueueRunsOut()
    {
        var queue = new PlayQueue();
        var album = Album(2);
        queue.SetContext(album, 0, false, string.Empty, string.Empty);
        var radio = new[] { album[0], album[1], new Song("radio000001", "Radio", "Artist", string.Empty, 200) };

        queue.SetAutoplay(radio);

        Assert.Equal(1, queue.AutoplayCount);
        queue.TryAdvance(SongRepeatMode.Off, out _);
        Assert.True(queue.TryAdvance(SongRepeatMode.Off, out var fromRadio));
        Assert.Equal("radio000001", fromRadio.Song.VideoId);
    }

    [Fact]
    public void NeedsAutoplayWhenTheQueueIsNearlyEmpty()
    {
        var queue = new PlayQueue();
        queue.SetContext(Album(2), 0, false, string.Empty, string.Empty);

        Assert.True(queue.NeedsAutoplay);
        queue.SetAutoplay(new[] { new Song("radio000001", "Radio", "Artist", string.Empty, 200) });
        Assert.False(queue.NeedsAutoplay);
    }

    [Fact]
    public void UnshuffleRestoresTheOrderSongsWereAddedToTheEnd()
    {
        var queue = new PlayQueue();
        var album = Album(20);
        queue.SetContext(album, 0, false, string.Empty, string.Empty);
        var added = new Song[20];
        for (var index = 0; index < added.Length; index++)
        {
            added[index] = new Song("added" + index.ToString("D6"), "Added " + index, "Artist", string.Empty, 180);
            queue.PlayLast(added[index]);
        }

        queue.SetShuffle(true);
        queue.SetShuffle(false);

        Assert.Equal(album[1].VideoId, queue.QueuedAt(0).Song.VideoId);
        for (var index = 0; index < added.Length; index++)
        {
            Assert.Equal(added[index].VideoId, queue.QueuedAt(19 + index).Song.VideoId);
        }
    }

    [Fact]
    public void HistoryIsCapped()
    {
        var queue = new PlayQueue();
        queue.SetContext(Album(PlayQueue.HistoryCapacity + 20), 0, false, string.Empty, string.Empty);

        while (queue.TryAdvance(SongRepeatMode.Off, out _))
        {
        }

        Assert.Equal(PlayQueue.HistoryCapacity, queue.HistoryCount);
    }
}
