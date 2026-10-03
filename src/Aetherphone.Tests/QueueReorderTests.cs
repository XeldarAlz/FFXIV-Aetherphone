using Aetherphone.Apps.Music;
using Aetherphone.Apps.Music.NowPlaying;
using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class QueueReorderTests
{
    private static readonly float[] Centers = [25f, 75f, 125f, 175f, 225f];

    [Fact]
    public void DraggingDownPastTwoRowsTargetsTheirSlot()
    {
        Assert.Equal(3, QueueReorder.TargetIndex(Centers, 1, 190f));
    }

    [Fact]
    public void DraggingUpToTheTopTargetsZero()
    {
        Assert.Equal(0, QueueReorder.TargetIndex(Centers, 3, 10f));
    }

    [Fact]
    public void SmallMovementKeepsTheSourceSlot()
    {
        Assert.Equal(2, QueueReorder.TargetIndex(Centers, 2, 140f));
    }

    [Fact]
    public void RowsBetweenSourceAndTargetShiftTowardTheGap()
    {
        Assert.Equal(0, QueueReorder.Shift(0, 1, 3));
        Assert.Equal(0, QueueReorder.Shift(1, 1, 3));
        Assert.Equal(-1, QueueReorder.Shift(2, 1, 3));
        Assert.Equal(-1, QueueReorder.Shift(3, 1, 3));
        Assert.Equal(0, QueueReorder.Shift(4, 1, 3));
        Assert.Equal(1, QueueReorder.Shift(0, 3, 0));
        Assert.Equal(1, QueueReorder.Shift(2, 3, 0));
        Assert.Equal(0, QueueReorder.Shift(4, 3, 0));
    }

    [Fact]
    public void TargetMatchesPlayQueueMoveSemantics()
    {
        var queue = new PlayQueue();
        var songs = new Song[6];
        for (var index = 0; index < songs.Length; index++)
        {
            songs[index] = new Song("v" + index, "Song " + index, "Artist", string.Empty, 180);
        }

        queue.SetContext(songs, 0, false, "context", "Context");
        var moved = queue.QueuedAt(1);
        var target = QueueReorder.TargetIndex(Centers, 1, 190f);

        Assert.True(queue.Move(moved.Id, target));
        Assert.Equal(moved.Id, queue.QueuedAt(3).Id);
    }

    [Fact]
    public void AutoscrollRampsNearEdges()
    {
        Assert.Equal(0f, QueueReorder.AutoscrollSpeed(150f, 0f, 300f, 40f, 500f));
        Assert.Equal(-500f, QueueReorder.AutoscrollSpeed(0f, 0f, 300f, 40f, 500f));
        Assert.Equal(250f, QueueReorder.AutoscrollSpeed(280f, 0f, 300f, 40f, 500f));
    }
}
