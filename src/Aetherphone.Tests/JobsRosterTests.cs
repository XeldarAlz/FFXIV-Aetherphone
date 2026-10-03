using Aetherphone.Core.Jobs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class JobsRosterTests
{
    private static readonly ClassJobFacts Gladiator = new(1, 1, 1, 0, 1, 30, 10, 1, false, 0);
    private static readonly ClassJobFacts Paladin = new(19, 1, 1, 1, 1, 30, 11, 30, false, 65000);
    private static readonly ClassJobFacts Arcanist = new(26, 26, 15, 0, 3, 31, 40, 1, false, 0);
    private static readonly ClassJobFacts Summoner = new(27, 26, 15, 5, 3, 31, 41, 30, false, 65001);
    private static readonly ClassJobFacts Scholar = new(28, 26, 15, 6, 4, 31, 22, 30, false, 65002);
    private static readonly ClassJobFacts DarkKnight = new(32, 32, 19, 1, 1, 30, 12, 30, false, 65003);
    private static readonly ClassJobFacts Carpenter = new(8, 8, 2, 0, 0, 33, 60, 1, false, 0);
    private static readonly ClassJobFacts BlueMage = new(36, 36, 25, 0, 3, 31, 45, 1, true, 0);

    private static readonly ClassJobFacts[] All =
    {
        Gladiator, Paladin, Arcanist, Summoner, Scholar, DarkKnight, Carpenter, BlueMage,
    };

    private static JobSlot[] Compose(params bool[] unlocked)
    {
        var slots = new JobSlot[All.Length];
        var count = JobsRoster.Compose(All, unlocked, slots);
        return slots[..count];
    }

    private static uint[] Ids(JobSlot[] slots)
    {
        var ids = new uint[slots.Length];
        for (var index = 0; index < slots.Length; index++)
        {
            ids[index] = All[slots[index].FactIndex].Id;
        }

        return ids;
    }

    [Fact]
    public void BaseClassStandsInUntilItsJobIsUnlocked()
    {
        var slots = Compose(true, false, true, false, false, true, true, false);
        var ids = Ids(slots);
        Assert.Contains(1u, ids);
        Assert.DoesNotContain(19u, ids);
        Assert.Contains(26u, ids);
        Assert.DoesNotContain(27u, ids);
        Assert.DoesNotContain(28u, ids);
    }

    [Fact]
    public void UnlockedJobReplacesItsBaseClass()
    {
        var ids = Ids(Compose(true, true, true, true, false, true, true, false));
        Assert.Contains(19u, ids);
        Assert.DoesNotContain(1u, ids);
        Assert.Contains(27u, ids);
        Assert.DoesNotContain(26u, ids);
    }

    [Fact]
    public void SiblingJobStaysVisibleButLockedWhenOnlyOneIsUnlocked()
    {
        var slots = Compose(true, true, true, true, false, true, true, false);
        var scholar = Array.Find(slots, slot => All[slot.FactIndex].Id == 28u);
        Assert.True(scholar.Locked);
        Assert.Equal(JobRole.Healer, scholar.Role);
    }

    [Fact]
    public void StandaloneJobsShowLockedWhenNeverLevelled()
    {
        var slots = Compose(true, true, true, true, true, false, true, false);
        var darkKnight = Array.Find(slots, slot => All[slot.FactIndex].Id == 32u);
        Assert.True(darkKnight.Locked);
    }

    [Fact]
    public void RosterIsOrderedByRoleThenPriority()
    {
        var ids = Ids(Compose(true, true, true, true, true, true, true, true));
        Assert.Equal(new uint[] { 19, 32, 28, 27, 36, 8 }, ids);
    }

    [Fact]
    public void BaseClassesFallBackToRoleAndCategory()
    {
        Assert.Equal((int)JobRole.Tank, JobsRoster.BucketFor(0, 1, JobsRoster.WarCategoryId));
        Assert.Equal((int)JobRole.PhysicalRanged, JobsRoster.BucketFor(0, 3, JobsRoster.WarCategoryId));
        Assert.Equal((int)JobRole.MagicalRanged, JobsRoster.BucketFor(0, 3, JobsRoster.MagicCategoryId));
        Assert.Equal((int)JobRole.Healer, JobsRoster.BucketFor(2, 4, JobsRoster.MagicCategoryId));
        Assert.Equal((int)JobRole.Hand, JobsRoster.BucketFor(0, 0, JobsRoster.HandCategoryId));
        Assert.Equal((int)JobRole.Land, JobsRoster.BucketFor(0, 0, JobsRoster.LandCategoryId));
        Assert.Equal(-1, JobsRoster.BucketFor(0, 0, 0));
    }

    [Fact]
    public void LimitedJobsCapBelowTheExpansionCap()
    {
        Assert.Equal(JobsRoster.LimitedJobCap, JobsRoster.CapFor(true, 100));
        Assert.Equal(70, JobsRoster.CapFor(true, 70));
        Assert.Equal(100, JobsRoster.CapFor(false, 100));
    }

    [Fact]
    public void FractionIsFullAtTheCapAndClampedOtherwise()
    {
        Assert.Equal(1f, JobsRoster.Fraction(0, 0, true));
        Assert.Equal(0.25f, JobsRoster.Fraction(250, 1000, false));
        Assert.Equal(1f, JobsRoster.Fraction(5000, 1000, false));
        Assert.Equal(0f, JobsRoster.Fraction(10, 0, false));
    }

    [Fact]
    public void RestedShareNeverExceedsTheRemainingExperience()
    {
        Assert.Equal(300, JobsRoster.RestedShare(300, 200, 1000, false));
        Assert.Equal(800, JobsRoster.RestedShare(5000, 200, 1000, false));
        Assert.Equal(0, JobsRoster.RestedShare(5000, 200, 1000, true));
        Assert.Equal(0, JobsRoster.RestedShare(0, 200, 1000, false));
    }

    [Fact]
    public void SummaryCountsCappedJobsAndLevels()
    {
        var summary = JobsRoster.Summarize(new[] { 100, 80, 50, 0 }, new[] { 100, 80, 100, 100 });
        Assert.Equal(2, summary.CappedCount);
        Assert.Equal(4, summary.TrackedCount);
        Assert.Equal(230, summary.LevelsEarned);
        Assert.Equal(380, summary.LevelsPossible);
    }

    [Fact]
    public void CustomOrderFollowsStoredIdsAndSkipsMissingOnes()
    {
        var output = new int[4];
        var count = JobsRoster.OrderedByIds(new[] { 1, 2, 3 }, new List<int> { 3, 9, 1, 3 }, output);
        Assert.Equal(new[] { 3, 1 }, output[..count]);
    }
}
