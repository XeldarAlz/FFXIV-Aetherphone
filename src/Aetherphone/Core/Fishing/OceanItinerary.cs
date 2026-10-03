using Aetherphone.Core.Game;

namespace Aetherphone.Core.Fishing;

internal readonly record struct OceanStop(uint SpotId, OceanTimeOfDay TimeOfDay);

internal readonly record struct OceanBlueFish(
    uint SpotId,
    OceanTimeOfDay TimeOfDay,
    uint ItemId,
    uint BaitItemId,
    bool Mooch,
    FishPredator[] Intuition);

internal static class OceanItinerary
{
    public const int StopCount = 3;
    private const uint GaladionBay = 1;
    private const uint SouthernMerlthor = 2;
    private const uint NorthernMerlthor = 3;
    private const uint RhotanoSea = 4;
    private const uint Cieldalaes = 5;
    private const uint BloodbrineSea = 6;
    private const uint RothlytSound = 7;
    private const uint SirensongSea = 8;
    private const uint Kugane = 9;
    private const uint RubySea = 10;
    private const uint OneRiver = 11;
    private const uint UnnamedIsland = 12;
    private const uint Thavnair = 13;

    private static readonly OceanBlueFish[] BlueFish =
    {
        new(GaladionBay, OceanTimeOfDay.Night, 29788, 2603, false,
            new[] { new FishPredator(29749, 2), new FishPredator(29752, 1) }),
        new(SouthernMerlthor, OceanTimeOfDay.Night, 29789, 2613, false, new[] { new FishPredator(29758, 2) }),
        new(NorthernMerlthor, OceanTimeOfDay.Day, 29791, 2619, false, new[] { new FishPredator(29781, 3) }),
        new(RhotanoSea, OceanTimeOfDay.Sunset, 29790, 2591, false,
            new[] { new FishPredator(29769, 1), new FishPredator(29768, 1) }),
        new(Cieldalaes, OceanTimeOfDay.Night, 32074, 27590, false,
            new[] { new FishPredator(32070, 2), new FishPredator(32067, 1) }),
        new(BloodbrineSea, OceanTimeOfDay.Day, 32094, 2587, false, new[] { new FishPredator(32089, 3) }),
        new(RothlytSound, OceanTimeOfDay.Sunset, 32114, 32107, true, new[] { new FishPredator(32110, 1) }),
        new(SirensongSea, OceanTimeOfDay.Day, 40540, 36593, false, new[] { new FishPredator(40534, 3) }),
        new(Kugane, OceanTimeOfDay.Night, 40560, 40551, true, new[] { new FishPredator(40558, 2) }),
        new(RubySea, OceanTimeOfDay.Sunset, 40580, 27590, false,
            new[] { new FishPredator(40571, 2), new FishPredator(40579, 1) }),
        new(OneRiver, OceanTimeOfDay.Day, 40600, 12704, false,
            new[] { new FishPredator(40595, 2), new FishPredator(40591, 1) }),
        new(UnnamedIsland, OceanTimeOfDay.Sunset, 51228, 51225, true, new[] { new FishPredator(51225, 2) }),
        new(Thavnair, OceanTimeOfDay.Night, 51247, 2591, false, new[] { new FishPredator(51243, 3) }),
    };

    public static ReadOnlySpan<OceanBlueFish> AllBlueFish => BlueFish;

    public static uint RouteRow(char destination, char time)
    {
        var group = destination switch
        {
            'N' => 1u,
            'R' => 4u,
            'B' => 7u,
            'T' => 10u,
            'O' => 13u,
            'Y' => 16u,
            'V' => 19u,
            _ => 0u,
        };
        if (group == 0)
        {
            return 0;
        }

        return group + time switch
        {
            'S' => 1u,
            'N' => 2u,
            _ => 0u,
        };
    }

    public static OceanTimeOfDay FinalTime(char time) =>
        time switch
        {
            'S' => OceanTimeOfDay.Sunset,
            'N' => OceanTimeOfDay.Night,
            _ => OceanTimeOfDay.Day,
        };

    public static bool TryStops(char destination, char time, Span<OceanStop> into)
    {
        if (into.Length < StopCount || !TrySpots(destination, out var first, out var second, out var final))
        {
            return false;
        }

        var last = FinalTime(time);
        into[0] = new OceanStop(first, Shift(last, -2));
        into[1] = new OceanStop(second, Shift(last, -1));
        into[2] = new OceanStop(final, last);
        return true;
    }

    public static int BlueFishFor(char destination, char time, Span<OceanBlueFish> into)
    {
        Span<OceanStop> stops = stackalloc OceanStop[StopCount];
        if (!TryStops(destination, time, stops))
        {
            return 0;
        }

        var written = 0;
        for (var stopIndex = 0; stopIndex < stops.Length; stopIndex++)
        {
            if (written < into.Length && TryBlueFish(stops[stopIndex], out var fish))
            {
                into[written++] = fish;
            }
        }

        return written;
    }

    public static bool TryBlueFish(in OceanStop stop, out OceanBlueFish fish)
    {
        for (var index = 0; index < BlueFish.Length; index++)
        {
            var candidate = BlueFish[index];
            if (candidate.SpotId == stop.SpotId && candidate.TimeOfDay == stop.TimeOfDay)
            {
                fish = candidate;
                return true;
            }
        }

        fish = default;
        return false;
    }

    private static bool TrySpots(char destination, out uint first, out uint second, out uint final)
    {
        (first, second, final) = destination switch
        {
            'N' => (SouthernMerlthor, GaladionBay, NorthernMerlthor),
            'R' => (GaladionBay, SouthernMerlthor, RhotanoSea),
            'B' => (Cieldalaes, NorthernMerlthor, BloodbrineSea),
            'T' => (Cieldalaes, RhotanoSea, RothlytSound),
            'O' => (SirensongSea, Kugane, OneRiver),
            'Y' => (SirensongSea, Kugane, RubySea),
            'V' => (UnnamedIsland, SirensongSea, Thavnair),
            _ => (0u, 0u, 0u),
        };
        return final != 0;
    }

    private static OceanTimeOfDay Shift(OceanTimeOfDay time, int steps)
    {
        const int phases = 3;
        var value = ((int)time + steps) % phases;
        return (OceanTimeOfDay)(value < 0 ? value + phases : value);
    }
}
