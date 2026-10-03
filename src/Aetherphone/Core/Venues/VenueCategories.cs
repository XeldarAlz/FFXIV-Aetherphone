namespace Aetherphone.Core.Venues;

internal static class VenueCategories
{
    public const int Nightclubs = 0;
    public const int Bars = 1;
    public const int Cafes = 2;
    public const int Taverns = 3;
    public const int BathHouses = 4;
    public const int Casinos = 5;
    public const int Roleplay = 6;
    public const int Photography = 7;
    public const int Count = 8;

    private static readonly string[][] Tags =
    {
        new[] { "Nightclub", "DJ", "Twitch DJ", "Sync DJ", "Dancers" },
        new[] { "Bar", "Drink", "Lounge" },
        new[] { "Cafe", "Maid cafe", "Restaurant", "Food" },
        new[] { "Tavern", "Inn" },
        new[] { "Bath house", "Spa" },
        new[] { "Casino", "Gambling", "Blackjack", "Roulette", "Deathroll" },
        new[] { "RP Heavy", "IC RP Only", "RP" },
        new[] { "Photography", "Artists" },
    };

    public static bool Matches(VenueEvent venue, int category)
    {
        if (category < 0 || category >= Count)
        {
            return true;
        }

        var wanted = Tags[category];
        for (var tagIndex = 0; tagIndex < venue.Tags.Count; tagIndex++)
        {
            var tag = venue.Tags[tagIndex];
            for (var wantedIndex = 0; wantedIndex < wanted.Length; wantedIndex++)
            {
                if (string.Equals(tag, wanted[wantedIndex], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
