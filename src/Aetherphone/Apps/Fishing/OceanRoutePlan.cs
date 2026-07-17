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
    private static readonly OceanBlueFish Taniwha = new("Taniwha", "Mackerel Strip");
    private static readonly OceanBlueFish GlassDragon = new("Glass Dragon", "Mooch Snapping Koban");
    private static readonly OceanBlueFish HellsClaw = new("Hells' Claw", "Squid Strip");
    private static readonly OceanBlueFish JewelOfPlumSpring = new("Jewel of Plum Spring", "Stonefly Nymph");
    private static readonly OceanBlueFish Akupara = new("Akupara", "Mooch Cieldalaes Roosterfish");
    private static readonly OceanBlueFish Manasvin = new("Manasvin", "Rat Tail");
    private static readonly OceanBlueFish[] None = Array.Empty<OceanBlueFish>();

    private static readonly OceanRoutePlan[] Plans =
    {
        new('N', 'D', "Northern Strait", "The Northern Strait of Merlthor",
            "Southern Strait · Galadion Bay · Northern Strait", OceanTimeOfDay.Day, new[] { Sothis, Elasmosaurus }),
        new('N', 'S', "Northern Strait", "The Northern Strait of Merlthor",
            "Southern Strait · Galadion Bay · Northern Strait", OceanTimeOfDay.Sunset, new[] { CoralManta }),
        new('N', 'N', "Northern Strait", "The Northern Strait of Merlthor",
            "Southern Strait · Galadion Bay · Northern Strait", OceanTimeOfDay.Night, None),
        new('R', 'D', "Rhotano Sea", "Open Rhotano Sea", "Galadion Bay · Southern Strait · Rhotano Sea",
            OceanTimeOfDay.Day, new[] { CoralManta }),
        new('R', 'S', "Rhotano Sea", "Open Rhotano Sea", "Galadion Bay · Southern Strait · Rhotano Sea",
            OceanTimeOfDay.Sunset, new[] { Sothis, Stonescale }),
        new('R', 'N', "Rhotano Sea", "Open Rhotano Sea", "Galadion Bay · Southern Strait · Rhotano Sea",
            OceanTimeOfDay.Night, None),
        new('B', 'D', "Bloodbrine Sea", "Open Bloodbrine Sea", "Cieldalaes · Northern Strait · Bloodbrine Sea",
            OceanTimeOfDay.Day, new[] { SeafaringToad }),
        new('B', 'S', "Bloodbrine Sea", "Open Bloodbrine Sea", "Cieldalaes · Northern Strait · Bloodbrine Sea",
            OceanTimeOfDay.Sunset, new[] { Elasmosaurus, Hafgufa }),
        new('B', 'N', "Bloodbrine Sea", "Open Bloodbrine Sea", "Cieldalaes · Northern Strait · Bloodbrine Sea",
            OceanTimeOfDay.Night, None),
        new('T', 'D', "Rothlyt Sound", "Outer Rothlyt Sound", "Cieldalaes · Rhotano Sea · Rothlyt Sound",
            OceanTimeOfDay.Day, None),
        new('T', 'S', "Rothlyt Sound", "Outer Rothlyt Sound", "Cieldalaes · Rhotano Sea · Rothlyt Sound",
            OceanTimeOfDay.Sunset, new[] { Hafgufa, Placodus }),
        new('T', 'N', "Rothlyt Sound", "Outer Rothlyt Sound", "Cieldalaes · Rhotano Sea · Rothlyt Sound",
            OceanTimeOfDay.Night, new[] { Stonescale }),
        new('Y', 'D', "Ruby Sea", "Open Ruby Sea", "Sirensong Sea · Kugane · Ruby Sea",
            OceanTimeOfDay.Day, new[] { GlassDragon }),
        new('Y', 'S', "Ruby Sea", "Open Ruby Sea", "Sirensong Sea · Kugane · Ruby Sea",
            OceanTimeOfDay.Sunset, new[] { HellsClaw }),
        new('Y', 'N', "Ruby Sea", "Open Ruby Sea", "Sirensong Sea · Kugane · Ruby Sea",
            OceanTimeOfDay.Night, new[] { Taniwha }),
        new('O', 'D', "One River", "Lower One River", "Sirensong Sea · Kugane · One River",
            OceanTimeOfDay.Day, new[] { GlassDragon, JewelOfPlumSpring }),
        new('O', 'S', "One River", "Lower One River", "Sirensong Sea · Kugane · One River",
            OceanTimeOfDay.Sunset, None),
        new('O', 'N', "One River", "Lower One River", "Sirensong Sea · Kugane · One River",
            OceanTimeOfDay.Night, new[] { Taniwha }),
        new('V', 'D', "Thavnair", "Thavnair Margin", "Unnamed Margin · Sirensong Sea · Thavnair",
            OceanTimeOfDay.Day, new[] { Akupara }),
        new('V', 'S', "Thavnair", "Thavnair Margin", "Unnamed Margin · Sirensong Sea · Thavnair",
            OceanTimeOfDay.Sunset, new[] { Taniwha }),
        new('V', 'N', "Thavnair", "Thavnair Margin", "Unnamed Margin · Sirensong Sea · Thavnair",
            OceanTimeOfDay.Night, new[] { Manasvin }),
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
