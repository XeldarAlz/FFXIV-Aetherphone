using Aetherphone.Core.Game;

namespace Aetherphone.Core.Market;

internal enum MarketScopeKind : byte
{
    World,
    DataCenter,
    Region,
}

internal readonly struct MarketScope
{
    public readonly MarketScopeKind Kind;
    public readonly string ApiName;
    public readonly string Label;

    public MarketScope(MarketScopeKind kind, string apiName, string label)
    {
        Kind = kind;
        ApiName = apiName;
        Label = label;
    }

    public bool IsValid => ApiName.Length > 0;
    public bool IsMultiWorld => Kind != MarketScopeKind.World;
    public string Key => $"{(byte)Kind}:{ApiName}";
    public static readonly MarketScope None = new(MarketScopeKind.World, string.Empty, "Unknown");
}

internal static class MarketScopes
{
    public static void Build(List<MarketScope> scopes, GameData gameData)
    {
        scopes.Clear();
        var worldId = gameData.LocalCurrentWorldId;
        if (worldId == 0)
        {
            return;
        }

        var world = gameData.WorldName(worldId);
        if (world.Length > 0)
        {
            scopes.Add(new MarketScope(MarketScopeKind.World, world, "World"));
        }

        var dataCenter = gameData.DataCenterName(worldId);
        if (dataCenter.Length > 0)
        {
            scopes.Add(new MarketScope(MarketScopeKind.DataCenter, dataCenter, "Data Center"));
        }

        var region = gameData.RegionName(worldId);
        if (region.Length > 0)
        {
            scopes.Add(new MarketScope(MarketScopeKind.Region, region, "Region"));
        }
    }

    public static int IndexOfKind(IReadOnlyList<MarketScope> scopes, MarketScopeKind kind)
    {
        for (var index = 0; index < scopes.Count; index++)
        {
            if (scopes[index].Kind == kind)
            {
                return index;
            }
        }

        return scopes.Count > 0 ? 0 : -1;
    }
}
