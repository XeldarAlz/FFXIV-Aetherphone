namespace Aetherphone.Core.Maps;

internal sealed class MapAetheryte
{
    public required uint RowId { get; init; }
    public required string Name { get; init; }
    public required byte Order { get; init; }
}

internal sealed class MapRegion
{
    public required string Name { get; init; }
    public required byte Order { get; init; }
    public required IReadOnlyList<MapAetheryte> Aetherytes { get; init; }
}

internal sealed class MapExpansion
{
    public required string Name { get; init; }
    public required byte Order { get; init; }
    public required IReadOnlyList<MapRegion> Regions { get; init; }
}

internal readonly record struct MapLocation(string Zone, string Region);
