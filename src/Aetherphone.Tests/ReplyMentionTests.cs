using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ReplyMentionTests
{
    [Fact]
    public void AnEmptyDraftStartsWithTheMention()
    {
        Assert.Equal("@moogle ", ReplyMention.Prefill(string.Empty, "moogle", 500));
    }

    [Fact]
    public void ATypedDraftKeepsItsTextBehindTheMention()
    {
        Assert.Equal("@moogle kupo", ReplyMention.Prefill("kupo", "moogle", 500));
    }

    [Fact]
    public void ADraftAlreadyOpeningWithTheMentionIsLeftAlone()
    {
        Assert.Equal("@Moogle kupo", ReplyMention.Prefill("@Moogle kupo", "moogle", 500));
    }

    [Fact]
    public void ADraftThatWouldOverflowTheLimitIsLeftAlone()
    {
        Assert.Equal("kupo", ReplyMention.Prefill("kupo", "moogle", 10));
    }

    [Fact]
    public void AnEmptyHandleLeavesTheDraftAlone()
    {
        Assert.Equal("kupo", ReplyMention.Prefill("kupo", string.Empty, 500));
    }
}
