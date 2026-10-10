using Aetherphone.Apps.Chirper;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChirperReactionOrderTests
{
    [Fact]
    public void SpookyOrderListsEveryReactionOnce()
    {
        var seen = new HashSet<int>();
        for (var slot = 0; slot < ChirperReactions.Count; slot++)
        {
            Assert.True(seen.Add(ChirperReactions.KindAt(slot, true)));
        }

        Assert.Equal(ChirperReactions.Count, seen.Count);
        Assert.InRange(seen.Min(), 0, ChirperReactions.Count - 1);
        Assert.InRange(seen.Max(), 0, ChirperReactions.Count - 1);
    }
}
