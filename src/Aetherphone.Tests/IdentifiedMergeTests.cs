using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Message;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MessageMergeTests
{
    private sealed record FakeMessage(string Id, long CreatedAtUnix) : IIdentified;

    private static readonly Comparison<FakeMessage> ByCreatedAt = (left, right) =>
    {
        var byTime = left.CreatedAtUnix.CompareTo(right.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(left.Id, right.Id);
    };

    private static FakeMessage[] Merge(FakeMessage[] existing, FakeMessage[] incoming) =>
        MessageMerge.MergeById(existing, incoming, ByCreatedAt);

    [Fact]
    public void EmptyExistingReturnsIncoming()
    {
        var incoming = new[] { new FakeMessage("a", 1) };
        Assert.Same(incoming, Merge(Array.Empty<FakeMessage>(), incoming));
    }

    [Fact]
    public void EmptyIncomingReturnsExisting()
    {
        var existing = new[] { new FakeMessage("a", 1) };
        Assert.Same(existing, Merge(existing, Array.Empty<FakeMessage>()));
    }

    [Fact]
    public void IncomingReplacesExistingWithSameId()
    {
        var existing = new[] { new FakeMessage("a", 1), new FakeMessage("b", 2) };
        var incoming = new[] { new FakeMessage("b", 5) };

        var merged = Merge(existing, incoming);

        Assert.Equal(2, merged.Length);
        Assert.Equal("a", merged[0].Id);
        Assert.Equal("b", merged[1].Id);
        Assert.Equal(5, merged[1].CreatedAtUnix);
    }

    [Fact]
    public void MergedResultIsSortedByTimeThenId()
    {
        var existing = new[] { new FakeMessage("d", 4), new FakeMessage("a", 1) };
        var incoming = new[] { new FakeMessage("c", 2), new FakeMessage("b", 2) };

        var merged = Merge(existing, incoming);

        Assert.Equal(new[] { "a", "b", "c", "d" }, new[] { merged[0].Id, merged[1].Id, merged[2].Id, merged[3].Id });
    }

    [Fact]
    public void OlderPageMergesInFrontWithoutDuplicates()
    {
        var existing = new[] { new FakeMessage("m3", 30), new FakeMessage("m4", 40) };
        var incoming = new[] { new FakeMessage("m1", 10), new FakeMessage("m2", 20), new FakeMessage("m3", 30) };

        var merged = Merge(existing, incoming);

        Assert.Equal(4, merged.Length);
        Assert.Equal("m1", merged[0].Id);
        Assert.Equal("m2", merged[1].Id);
        Assert.Equal("m3", merged[2].Id);
        Assert.Equal("m4", merged[3].Id);
    }
}
