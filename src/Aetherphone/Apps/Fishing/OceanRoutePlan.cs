using Aetherphone.Core.Game;

namespace Aetherphone.Apps.Fishing;

internal readonly record struct OceanBlueFish(string Name, string Bait);

internal readonly record struct OceanRoutePlan(
    char Destination,
    char Time,
    string RouteName,
    string FinalStop,
    string Stops,
    OceanTimeOfDay TimeOfDay,
    OceanBlueFish[] BlueFish);

internal static class OceanRoutes
{
    private static readonly OceanBlueFish Sothis = new("Sothis", "Glowworm");

    private static readonly OceanBlueFish CoralManta = new("Coral Manta", "Shrimp Cage Feeder");

    private static readonly OceanBlueFish Elasmosaurus = new("Elasmosaurus", "Heavy Steel Jig");

    private static readonly OceanBlueFish Stonescale = new("Stonescale", "Rat Tail");

    private static readonly OceanBlueFish Hafgufa = new("Hafgufa", "Squid Strip");

    private static readonly OceanBlueFish SeafaringToad = new("Seafaring Toad", "Pill Bug");

    private static readonly OceanBlueFish Placodus = new("Placodus", "Glowworm");

    private static readonly OceanBlueFish[] None = Array.Empty<OceanBlueFish>();

    private static readonly OceanRoutePlan[] Plans =
    {
        new('N', 'D', "Northern Strait", "The Northern Strait of Merlthor", "Southern Strait · Galadion Bay · Northern Strait", OceanTimeOfDay.Day, new[] { Sothis, Elasmosaurus }),
        new('N', 'S', "Northern Strait", "The Northern Strait of Merlthor", "Southern Strait · Galadion Bay · Northern Strait", OceanTimeOfDay.Sunset, new[] { CoralManta }),
        new('N', 'N', "Northern Strait", "The Northern Strait of Merlthor", "Southern Strait · Galadion Bay · Northern Strait", OceanTimeOfDay.Night, None),
        new('R', 'D', "Rhotano Sea", "Open Rhotano Sea", "Galadion Bay · Southern Strait · Rhotano Sea", OceanTimeOfDay.Day, new[] { CoralManta }),
        new('R', 'S', "Rhotano Sea", "Open Rhotano Sea", "Galadion Bay · Southern Strait · Rhotano Sea", OceanTimeOfDay.Sunset, new[] { Sothis, Stonescale }),
        new('R', 'N', "Rhotano Sea", "Open Rhotano Sea", "Galadion Bay · Southern Strait · Rhotano Sea", OceanTimeOfDay.Night, None),
        new('B', 'D', "Bloodbrine Sea", "Open Bloodbrine Sea", "Cieldalaes · Northern Strait · Bloodbrine Sea", OceanTimeOfDay.Day, new[] { SeafaringToad }),
        new('B', 'S', "Bloodbrine Sea", "Open Bloodbrine Sea", "Cieldalaes · Northern Strait · Bloodbrine Sea", OceanTimeOfDay.Sunset, new[] { Elasmosaurus, Hafgufa }),
        new('B', 'N', "Bloodbrine Sea", "Open Bloodbrine Sea", "Cieldalaes · Northern Strait · Bloodbrine Sea", OceanTimeOfDay.Night, None),
        new('T', 'D', "Rothlyt Sound", "Outer Rothlyt Sound", "Cieldalaes · Rhotano Sea · Rothlyt Sound", OceanTimeOfDay.Day, None),
        new('T', 'S', "Rothlyt Sound", "Outer Rothlyt Sound", "Cieldalaes · Rhotano Sea · Rothlyt Sound", OceanTimeOfDay.Sunset, new[] { Hafgufa, Placodus }),
        new('T', 'N', "Rothlyt Sound", "Outer Rothlyt Sound", "Cieldalaes · Rhotano Sea · Rothlyt Sound", OceanTimeOfDay.Night, new[] { Stonescale }),
    };

    public static OceanRoutePlan Resolve(char destination, char time)
    {
        for (var index = 0; index < Plans.Length; index++)
        {
            var plan = Plans[index];
            if (plan.Destination == destination && plan.Time == time)
            {
                return plan;
            }
        }

        return Plans[0];
    }
}
