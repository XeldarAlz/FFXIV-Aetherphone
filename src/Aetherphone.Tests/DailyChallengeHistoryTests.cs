using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DailyChallengeHistoryTests
{
    private const string DailyId = "sudoku";
    private const int Start = 20000;

    private static GameStatsStore Build(FakeStatsConfiguration configuration) =>
        new(configuration) { DailyGameId = DailyId };

    [Fact]
    public void ConsecutiveDaysFillConsecutiveBitsAndRaiseTheBest()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = Build(configuration);

        for (var day = Start; day < Start + 4; day++)
        {
            stats.CompleteDaily(DailyId, day);
        }

        Assert.Equal(0b1111UL, configuration.DailyChallengeHistory);
        Assert.Equal(4, configuration.DailyChallengeStreak);
        Assert.Equal(4, stats.DailyBestStreak);
        for (var day = Start; day < Start + 4; day++)
        {
            Assert.True(stats.DailyDoneOn(day));
        }

        Assert.False(stats.DailyDoneOn(Start - 1));
        Assert.False(stats.DailyDoneOn(Start + 4));
    }

    [Fact]
    public void AGapLeavesTheMissedDaysEmptyAndKeepsTheBest()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = Build(configuration);
        stats.CompleteDaily(DailyId, Start);
        stats.CompleteDaily(DailyId, Start + 1);

        stats.CompleteDaily(DailyId, Start + 4);

        Assert.Equal(0b11001UL, configuration.DailyChallengeHistory);
        Assert.Equal(1, configuration.DailyChallengeStreak);
        Assert.Equal(2, stats.DailyBestStreak);
        Assert.True(stats.DailyDoneOn(Start));
        Assert.True(stats.DailyDoneOn(Start + 1));
        Assert.False(stats.DailyDoneOn(Start + 2));
        Assert.False(stats.DailyDoneOn(Start + 3));
        Assert.True(stats.DailyDoneOn(Start + 4));
    }

    [Fact]
    public void ASecondCompletionOnTheSameDayChangesNothing()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = Build(configuration);
        stats.CompleteDaily(DailyId, Start);

        stats.CompleteDaily(DailyId, Start);

        Assert.Equal(1UL, configuration.DailyChallengeHistory);
        Assert.Equal(1, configuration.Saves);
    }

    [Fact]
    public void OtherGamesNeverTouchTheHistory()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = Build(configuration);

        stats.CompleteDaily("snake", Start);
        stats.CompleteDaily("sudokuish", Start);

        Assert.Equal(0UL, configuration.DailyChallengeHistory);
        Assert.False(stats.DailyDoneOn(Start));
    }

    [Fact]
    public void ATierStatIdCountsForItsGame()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = Build(configuration);

        stats.CompleteDaily("sudoku.hard", Start);

        Assert.True(stats.DailyDoneOn(Start));
    }

    [Fact]
    public void TheHistoryKeepsSixtyFourDays()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = Build(configuration);
        stats.CompleteDaily(DailyId, Start);

        stats.CompleteDaily(DailyId, Start + GameStatsStore.DailyHistoryDays - 1);

        Assert.True(stats.DailyDoneOn(Start));
        Assert.Equal((1UL << (GameStatsStore.DailyHistoryDays - 1)) | 1UL, configuration.DailyChallengeHistory);
        Assert.False(stats.DailyDoneOn(Start - 1));
    }

    [Fact]
    public void AGapOfSixtyFourDaysOrMoreStartsAFreshHistory()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = Build(configuration);
        stats.CompleteDaily(DailyId, Start);

        stats.CompleteDaily(DailyId, Start + GameStatsStore.DailyHistoryDays);

        Assert.Equal(1UL, configuration.DailyChallengeHistory);
        Assert.False(stats.DailyDoneOn(Start));
        Assert.True(stats.DailyDoneOn(Start + GameStatsStore.DailyHistoryDays));
    }

    [Fact]
    public void AClockThatRanBackwardsStartsAFreshHistory()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = Build(configuration);
        stats.CompleteDaily(DailyId, Start);
        stats.CompleteDaily(DailyId, Start + 1);

        stats.CompleteDaily(DailyId, Start - 3);

        Assert.Equal(1UL, configuration.DailyChallengeHistory);
        Assert.Equal(Start - 3, configuration.DailyChallengeLastDay);
        Assert.Equal(2, stats.DailyBestStreak);
    }

    [Fact]
    public void AnOldStreakIsSeededIntoTheHistory()
    {
        var configuration = new FakeStatsConfiguration
        {
            DailyChallengeStreak = 5,
            DailyChallengeLastDay = Start,
        };

        var stats = Build(configuration);

        Assert.Equal(0b11111UL, configuration.DailyChallengeHistory);
        Assert.Equal(5, configuration.DailyChallengeBestStreak);
        Assert.Equal(5, stats.DailyBestStreak);
        Assert.True(stats.DailyDoneOn(Start - 4));
        Assert.False(stats.DailyDoneOn(Start - 5));
        Assert.Equal(0, configuration.Saves);
    }

    [Fact]
    public void ASeededStreakLongerThanTheWindowFillsEveryBit()
    {
        var configuration = new FakeStatsConfiguration
        {
            DailyChallengeStreak = 90,
            DailyChallengeLastDay = Start,
        };

        var stats = Build(configuration);

        Assert.Equal(ulong.MaxValue, configuration.DailyChallengeHistory);
        Assert.Equal(90, stats.DailyBestStreak);
        Assert.True(stats.DailyDoneOn(Start - (GameStatsStore.DailyHistoryDays - 1)));
        Assert.False(stats.DailyDoneOn(Start - GameStatsStore.DailyHistoryDays));
    }

    [Fact]
    public void SeedingRunsOnlyOnce()
    {
        var configuration = new FakeStatsConfiguration
        {
            DailyChallengeStreak = 3,
            DailyChallengeLastDay = Start,
            DailyChallengeHistory = 0b101UL,
            DailyChallengeBestStreak = 7,
        };

        var stats = Build(configuration);

        Assert.Equal(0b101UL, configuration.DailyChallengeHistory);
        Assert.Equal(7, stats.DailyBestStreak);
    }

    [Fact]
    public void ANeverPlayedDailyHasNoHistory()
    {
        var configuration = new FakeStatsConfiguration();

        var stats = Build(configuration);

        Assert.Equal(0UL, configuration.DailyChallengeHistory);
        Assert.Equal(0, stats.DailyBestStreak);
        Assert.False(stats.DailyDoneOn(0));
        Assert.False(stats.DailyDoneOn(Start));
    }

    [Fact]
    public void AStreakContinuedAfterSeedingShiftsTheSeededBits()
    {
        var configuration = new FakeStatsConfiguration
        {
            DailyChallengeStreak = 2,
            DailyChallengeLastDay = Start,
        };
        var stats = Build(configuration);

        stats.CompleteDaily(DailyId, Start + 1);

        Assert.Equal(0b111UL, configuration.DailyChallengeHistory);
        Assert.Equal(3, stats.DailyBestStreak);
    }
}
