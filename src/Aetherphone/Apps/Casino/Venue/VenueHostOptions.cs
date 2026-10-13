using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Venue;

[Flags]
internal enum VenuePinNotes : byte
{
    None = 0,
    InviteOnly = 1,
    NoPlot = 2,
}

internal sealed class VenueHostOptions
{
    private readonly CasinoVenueStore venue;
    private string locationText = string.Empty;
    private CasinoHousingPosition locationOf;
    private LanguageInfo? locationLanguage;

    public bool PinLocation = true;

    public VenueHostOptions(CasinoVenueStore venue)
    {
        this.venue = venue;
    }

    public void Reset()
    {
        PinLocation = true;
    }

    public CasinoTableLocationDto? Location() => PinLocation ? venue.Position.ToLocation() : null;

    public VenuePinNotes Notes(int listing)
    {
        venue.EnsureNearby();
        return NotesFor(PinLocation, listing, venue.Position);
    }

    internal static VenuePinNotes NotesFor(bool pinLocation, int listing, in CasinoHousingPosition position)
    {
        if (!pinLocation || !position.InWard)
        {
            return VenuePinNotes.None;
        }

        var notes = VenuePinNotes.None;
        if (listing == CasinoListings.Private)
        {
            notes |= VenuePinNotes.InviteOnly;
        }

        if (position.Plot <= 0)
        {
            notes |= VenuePinNotes.NoPlot;
        }

        return notes;
    }

    public void DrawLocation(AppSkin ui, int listing, float scale)
    {
        venue.EnsureNearby();
        var position = venue.Position;
        var card = GroupCard.Begin(ui, 2, VenueFields.RowUnits);
        VenueFields.ValueRow(ui, card.NextRow(VenueFields.RowUnits), Loc.T(L.Venue.Location),
            position.InWard ? LocationText(position) : Loc.T(L.Venue.LocationNone), scale);
        var pin = VenueFields.ToggleRow(ui, card.NextRow(VenueFields.RowUnits), "venue.pin", Loc.T(L.Venue.PinLocation),
            PinLocation && position.InWard, scale);
        if (position.InWard)
        {
            PinLocation = pin;
        }

        card.End();
        VenueFields.Hint(ui, Loc.T(L.Venue.LocationHint), scale);
        var notes = NotesFor(PinLocation, listing, position);
        if ((notes & VenuePinNotes.InviteOnly) != 0)
        {
            VenueFields.Hint(ui, Loc.T(L.Venue.PinInviteOnlyNote), scale);
        }

        if ((notes & VenuePinNotes.NoPlot) != 0)
        {
            VenueFields.Hint(ui, Loc.T(L.Venue.PinNoPlotNote), scale);
        }
    }

    private string LocationText(CasinoHousingPosition position)
    {
        if (position == locationOf && ReferenceEquals(locationLanguage, Loc.Current) && locationText.Length > 0)
        {
            return locationText;
        }

        locationOf = position;
        locationLanguage = Loc.Current;
        var district = HousingDistricts.TryGet((uint)position.Territory, out var known) ? known.Name : string.Empty;
        locationText = position.Plot > 0
            ? Loc.T(L.Venue.LocationPlot, district, position.Ward.ToString(Loc.Culture),
                position.Plot.ToString(Loc.Culture))
            : Loc.T(L.Venue.LocationWard, district, position.Ward.ToString(Loc.Culture));
        return locationText;
    }
}
