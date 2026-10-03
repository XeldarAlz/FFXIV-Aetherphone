using Aetherphone.Core.Fishing;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Fishing;

internal sealed class OceanVoyageLabels
{
    public required string Destination { get; init; }
    public required OceanTimeOfDay TimeOfDay { get; init; }
    public required OceanStop[] Stops { get; init; }
    public required string[] StopNames { get; init; }
    public required string[] StopTimes { get; init; }
    public required OceanBlueFish[] BlueFish { get; init; }
    public required string[] BlueNames { get; init; }
    public required string[] BlueBaits { get; init; }
    public required string[] BlueStops { get; init; }
    public required string[] BlueIntuition { get; init; }
    public required string BlueSummary { get; init; }
}

internal sealed class OceanVoyageText
{
    private const string Separator = " · ";
    private readonly FishingCatalog catalog;
    private readonly Dictionary<int, OceanVoyageLabels> cache = new();
    private string languageCode = string.Empty;

    public OceanVoyageText(FishingCatalog catalog)
    {
        this.catalog = catalog;
    }

    public OceanVoyageLabels For(char destination, char time)
    {
        var code = Loc.Current.Code;
        if (!string.Equals(code, languageCode, StringComparison.Ordinal))
        {
            languageCode = code;
            cache.Clear();
        }

        var key = destination * 256 + time;
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var built = Build(destination, time);
        cache[key] = built;
        return built;
    }

    private OceanVoyageLabels Build(char destination, char time)
    {
        Span<OceanStop> stopBuffer = stackalloc OceanStop[OceanItinerary.StopCount];
        var stops = OceanItinerary.TryStops(destination, time, stopBuffer)
            ? stopBuffer.ToArray()
            : Array.Empty<OceanStop>();
        var stopNames = new string[stops.Length];
        var stopTimes = new string[stops.Length];
        for (var index = 0; index < stops.Length; index++)
        {
            stopNames[index] = catalog.OceanSpotName(stops[index].SpotId);
            stopTimes[index] = Loc.T(L.Fishing.StopTime, index + 1, FishingText.TimeOfDay(stops[index].TimeOfDay));
        }

        var blueBuffer = new OceanBlueFish[OceanItinerary.StopCount];
        var blueCount = OceanItinerary.BlueFishFor(destination, time, blueBuffer);
        var blue = blueBuffer.AsSpan(0, blueCount).ToArray();
        var names = new string[blue.Length];
        var baits = new string[blue.Length];
        var blueStops = new string[blue.Length];
        var intuition = new string[blue.Length];
        for (var index = 0; index < blue.Length; index++)
        {
            var fish = blue[index];
            names[index] = catalog.ItemName(fish.ItemId);
            var baitName = catalog.ItemName(fish.BaitItemId);
            baits[index] = fish.Mooch ? Loc.T(L.Fishing.MoochWith, baitName) : Loc.T(L.Fishing.BaitWith, baitName);
            blueStops[index] = catalog.OceanSpotName(fish.SpotId);
            var predators = FishingText.Predators(catalog, fish.Intuition);
            intuition[index] = predators.Length > 0 ? Loc.T(L.Fishing.IntuitionLine, predators) : string.Empty;
        }

        var destinationName = stopNames.Length > 0 ? stopNames[^1] : string.Empty;
        return new OceanVoyageLabels
        {
            Destination = destinationName.Length > 0 ? destinationName : Loc.T(L.Timers.OceanFishing),
            TimeOfDay = OceanItinerary.FinalTime(time),
            Stops = stops,
            StopNames = stopNames,
            StopTimes = stopTimes,
            BlueFish = blue,
            BlueNames = names,
            BlueBaits = baits,
            BlueStops = blueStops,
            BlueIntuition = intuition,
            BlueSummary = names.Length == 0 ? Loc.T(L.Fishing.NoBlueFish) : string.Join(Separator, names),
        };
    }
}
