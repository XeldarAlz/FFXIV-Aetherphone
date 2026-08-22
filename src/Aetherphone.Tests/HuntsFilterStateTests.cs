using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntsFilterStateTests
{
    [Fact]
    public void ApplySnapshotRestoresEveryFieldToSnapshotCaptured()
    {
        var original = new HuntsFilterState();
        original.RankB = true;
        original.RankS = false;
        original.StatusUnmet = false;
        original.ToggleExpansion(0);
        original.ToggleWorld("adamantoise");

        var restored = new HuntsFilterState();
        restored.ApplySnapshot(original.ToSnapshot());

        Assert.Equal(original.RankF, restored.RankF);
        Assert.Equal(original.RankB, restored.RankB);
        Assert.Equal(original.RankA, restored.RankA);
        Assert.Equal(original.RankS, restored.RankS);
        Assert.Equal(original.StatusClosed, restored.StatusClosed);
        Assert.Equal(original.StatusOpen, restored.StatusOpen);
        Assert.Equal(original.StatusCapped, restored.StatusCapped);
        Assert.Equal(original.StatusUnmet, restored.StatusUnmet);
        Assert.False(restored.IsExpansionActive(0));
        Assert.True(restored.IsWorldSelected("adamantoise"));
    }

    [Fact]
    public void ApplySnapshotToleratesAShorterExpansionListThanTheLiveOneHas()
    {
        var restored = new HuntsFilterState();
        var snapshot = new HuntsFilterSnapshot { ExpansionActive = { false } };

        restored.ApplySnapshot(snapshot);

        Assert.False(restored.IsExpansionActive(0));
        for (var index = 1; index < HuntExpansions.Ids.Length; index++)
        {
            Assert.True(restored.IsExpansionActive(index));
        }
    }
}
