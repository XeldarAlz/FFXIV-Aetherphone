using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NameMaskTests
{
    [Fact]
    public void MaskedNamesAreStableAnonymousAndDistinct()
    {
        var first = NameMask.Of("Xeldar Alz");
        var second = NameMask.Of("Nara Mielle");

        Assert.StartsWith("Player ", first, StringComparison.Ordinal);
        Assert.Equal(first, NameMask.Of("Xeldar Alz"));
        Assert.NotEqual(first, second);
        Assert.DoesNotContain("Xeldar", first, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayPassesNamesThroughWhileDisabled()
    {
        NameMask.Set(false);

        Assert.Equal("Xeldar Alz", NameMask.Display("Xeldar Alz"));
    }
}
