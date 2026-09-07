using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SocialRegionTests
{
    private static int Bit(string code) => 1 << Array.IndexOf(SocialRegion.Codes, code);

    [Fact]
    public void NoPickAndNoHideAllowsEveryRegion()
    {
        Assert.Equal(SocialRegion.AllMask, SocialRegion.AllowedMask(0, 0));
    }

    [Fact]
    public void ShownRegionsAreTheOnlyOnesAllowed()
    {
        Assert.Equal(Bit("NA"), SocialRegion.AllowedMask(Bit("NA"), 0));
        Assert.Equal(Bit("NA") | Bit("EU"), SocialRegion.AllowedMask(Bit("NA") | Bit("EU"), 0));
    }

    [Fact]
    public void HidingWithoutPickingLeavesEveryOtherRegion()
    {
        Assert.Equal(SocialRegion.AllMask & ~Bit("EU"), SocialRegion.AllowedMask(0, Bit("EU")));
    }

    [Fact]
    public void HidingSubtractsFromThePickedRegions()
    {
        var shown = Bit("NA") | Bit("EU") | Bit("JP");
        Assert.Equal(Bit("NA") | Bit("JP"), SocialRegion.AllowedMask(shown, Bit("EU")));
    }

    [Fact]
    public void HidingEveryRegionAllowsNone()
    {
        Assert.Equal(0, SocialRegion.AllowedMask(0, SocialRegion.AllMask));
        Assert.Equal(0, SocialRegion.AllowedMask(Bit("NA"), Bit("NA")));
    }

    [Fact]
    public void AllowedCsvNamesOnlyPartialSelections()
    {
        Assert.Null(SocialRegion.FilterCsv(SocialRegion.AllowedMask(0, 0)));
        Assert.Equal("NA", SocialRegion.FilterCsv(SocialRegion.AllowedMask(Bit("NA"), 0)));
        Assert.Equal("NA,JP,OCE,CN", SocialRegion.FilterCsv(SocialRegion.AllowedMask(0, Bit("EU"))));
    }
}
