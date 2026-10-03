namespace Aetherphone.Core.Maps;

internal sealed class MapAetheryte
{
    public required uint RowId { get; init; }
    public required string Name { get; init; }
    public required byte Order { get; init; }
    public required uint TerritoryId { get; init; }
    public required uint MapId { get; init; }
    public required string ZoneName { get; init; }
    public required string RegionName { get; init; }
    public required string Subtitle { get; init; }
    public required float U { get; init; }
    public required float V { get; init; }
    public required float GameX { get; init; }
    public required float GameY { get; init; }
    public bool HasPosition => U >= 0f && V >= 0f;
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
    public required string Summary { get; init; }
}

internal enum MapLocationKind : byte
{
    Offline,
    Unknown,
    Zone,
    Ward,
    House,
    Duty,
}

internal readonly record struct MapLocation(string Title, string Subtitle, MapLocationKind Kind, uint TerritoryId)
{
    public bool IsKnown => Kind is not (MapLocationKind.Offline or MapLocationKind.Unknown);
}
