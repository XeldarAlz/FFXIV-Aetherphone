using Aetherphone.Core.Game;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Market;

namespace Aetherphone.Apps.Inventory;

internal sealed class InventoryMarketPrices : IInventoryPriceSource
{
    private readonly MarketboardService market;
    private readonly GameData gameData;
    private readonly Configuration configuration;
    private readonly List<MarketScope> scopes = new();
    private MarketScope scope = MarketScope.None;

    public InventoryMarketPrices(MarketboardService market, GameData gameData, Configuration configuration)
    {
        this.market = market;
        this.gameData = gameData;
        this.configuration = configuration;
    }

    public bool IsValid => scope.IsValid;
    public string ScopeName => scope.ApiName;

    public bool Refresh()
    {
        MarketScopes.Build(scopes, gameData);
        var index = MarketScopes.IndexOfKind(scopes, configuration.MarketScope);
        var next = index >= 0 && index < scopes.Count ? scopes[index] : MarketScope.None;
        if (string.Equals(next.Key, scope.Key, StringComparison.Ordinal))
        {
            return false;
        }

        scope = next;
        return true;
    }

    public void Request(List<uint> itemIds)
    {
        if (scope.IsValid)
        {
            market.PrefetchAggregated(itemIds, scope);
        }
    }

    public bool TryGet(uint itemId, out long unitPrice) => market.TryGetAggregated(itemId, scope, out unitPrice);
}
