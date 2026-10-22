using System.Numerics;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CoinGameSessionTrackerTests
{
    [Fact]
    public void APaidSessionAlwaysReports()
    {
        var paid = new CoinAwardDto(true, 5, 100);

        Assert.True(CoinGameSessionTracker.Reports(paid, true));
        Assert.True(CoinGameSessionTracker.Reports(paid, false));
    }

    [Fact]
    public void ATooShortSessionReportsOnlyWhileThePlayerIsWatching()
    {
        var tooShort = new CoinAwardDto(false, 0, 0, CoinGameSessionTracker.TooShortReason);

        Assert.True(CoinGameSessionTracker.Reports(tooShort, true));
        Assert.False(CoinGameSessionTracker.Reports(tooShort, false));
    }

    [Fact]
    public void OtherRefusalsStaySilent()
    {
        var capped = new CoinAwardDto(false, 0, 0, "daily_cap");

        Assert.False(CoinGameSessionTracker.Reports(capped, true));
        Assert.False(CoinGameSessionTracker.Reports(capped, false));
    }

    [Fact]
    public void ATooShortSessionNotesThatItPlayedTooShort()
    {
        var tooShort = new CoinAwardDto(false, 0, 0, CoinGameSessionTracker.TooShortReason);

        Assert.Equal(CoinAwardNote.TooShort, CoinGameSessionTracker.NoteFor(tooShort));
    }

    [Fact]
    public void APaidSessionNotesItsReward()
    {
        var paid = new CoinAwardDto(true, 5, 100);

        Assert.Equal(CoinAwardNote.Reward, CoinGameSessionTracker.NoteFor(paid));
    }

    [Fact]
    public void AGrantWithoutCoinsAndOtherRefusalsNoteNothing()
    {
        var emptyGrant = new CoinAwardDto(true, 0, 100);
        var capped = new CoinAwardDto(false, 0, 0, "daily_cap");
        var expired = new CoinAwardDto(false, 0, 0, "expired");

        Assert.Equal(CoinAwardNote.None, CoinGameSessionTracker.NoteFor(emptyGrant));
        Assert.Equal(CoinAwardNote.None, CoinGameSessionTracker.NoteFor(capped));
        Assert.Equal(CoinAwardNote.None, CoinGameSessionTracker.NoteFor(expired));
    }

    [Fact]
    public void AFloatStaysActiveFromSpawnUntilItIsDrawnOut()
    {
        var floats = new CoinFloat();

        Assert.True(floats.IsIdle);
        floats.Spawn("Played too short to pay", Vector2.Zero, true);
        Assert.False(floats.IsIdle);
    }
}
