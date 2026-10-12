using Aetherphone.Apps.Casino.Venue;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VenueSectionStateTests
{
    [Theory]
    [InlineData(true, 3, true, (int)VenueContextKind.YourTable)]
    [InlineData(true, 0, false, (int)VenueContextKind.YourTable)]
    [InlineData(false, 2, true, (int)VenueContextKind.AtThisVenue)]
    [InlineData(false, 1, false, (int)VenueContextKind.AtThisVenue)]
    [InlineData(false, 0, true, (int)VenueContextKind.HostAgain)]
    [InlineData(false, 0, false, (int)VenueContextKind.HostNew)]
    public void TheOwnTableWinsThenNearbyTablesThenTheSavedDraft(bool ownTable, int nearby, bool draft, int expected)
    {
        Assert.Equal((VenueContextKind)expected, VenueSectionState.Pick(ownTable, nearby, draft));
    }

    [Fact]
    public void ANegativeNearbyCountCountsAsNobodyNearby()
    {
        Assert.Equal(VenueContextKind.HostNew, VenueSectionState.Pick(false, -1, false));
    }
}
