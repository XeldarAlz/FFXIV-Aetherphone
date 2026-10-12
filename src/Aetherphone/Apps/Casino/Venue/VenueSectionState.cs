namespace Aetherphone.Apps.Casino.Venue;

internal enum VenueContextKind : byte
{
    YourTable,
    AtThisVenue,
    HostAgain,
    HostNew,
}

internal static class VenueSectionState
{
    public static VenueContextKind Pick(bool hasOwnOpenTable, int nearbyCount, bool hasSavedDraft)
    {
        if (hasOwnOpenTable)
        {
            return VenueContextKind.YourTable;
        }

        if (nearbyCount > 0)
        {
            return VenueContextKind.AtThisVenue;
        }

        return hasSavedDraft ? VenueContextKind.HostAgain : VenueContextKind.HostNew;
    }
}
