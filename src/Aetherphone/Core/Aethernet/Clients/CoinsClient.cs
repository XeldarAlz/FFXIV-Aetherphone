using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class CoinsClient
{
    private readonly AethernetTransport net;

    public CoinsClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<CoinWalletDto?> WalletAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/coins/", AethernetJsonContext.Default.CoinWalletDto, token, null, onFailure);
    }

    public Task<CoinLedgerPage?> LedgerAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = "/coins/ledger";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.CoinLedgerPage, token, null, onFailure);
    }

    public Task<CoinCatalogDto?> CatalogAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/coins/catalog", AethernetJsonContext.Default.CoinCatalogDto, token, null, onFailure);
    }

    public Task<CoinShopDto?> ShopAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/coins/shop", AethernetJsonContext.Default.CoinShopDto, token, null, onFailure);
    }

    public Task<CoinShopShelfPage?> ShelfAsync(string categoryId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = "/coins/shop/items";
        var separator = '?';
        if (categoryId.Length > 0)
        {
            path += $"?category={Uri.EscapeDataString(categoryId)}";
            separator = '&';
        }

        if (cursor is not null)
        {
            path += $"{separator}cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.CoinShopShelfPage, token, null, onFailure);
    }

    public Task<CoinEntitlementsDto?> EntitlementsAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/coins/entitlements", AethernetJsonContext.Default.CoinEntitlementsDto, token, null,
            onFailure);
    }

    public Task<CoinAwardDto?> CheckInAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, "/coins/checkin", AethernetJsonContext.Default.CoinAwardDto, token,
            null, onFailure);
    }

    public Task<CoinGameSessionDto?> StartGameSessionAsync(string gameId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/coins/games/session", new CoinGameSessionRequest(gameId),
            AethernetJsonContext.Default.CoinGameSessionRequest, AethernetJsonContext.Default.CoinGameSessionDto, token,
            null, onFailure);
    }

    public Task<CoinAwardDto?> EndGameSessionAsync(string sessionId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/coins/games/session/{Uri.EscapeDataString(sessionId)}/end",
            AethernetJsonContext.Default.CoinAwardDto, token, null, onFailure);
    }

    public Task<CoinQuestBoardDto?> QuestsAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/coins/quests", AethernetJsonContext.Default.CoinQuestBoardDto, token, null, onFailure);
    }

    public Task<CoinAwardDto?> ClaimQuestAsync(string questId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/coins/quests/{Uri.EscapeDataString(questId)}/claim",
            AethernetJsonContext.Default.CoinAwardDto, token, null, onFailure);
    }

    public Task<HalloweenTreatsDto?> ReportHalloweenTreatsAsync(int found, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/coins/events/halloween", new HalloweenTreatsReport(found),
            AethernetJsonContext.Default.HalloweenTreatsReport, AethernetJsonContext.Default.HalloweenTreatsDto, token,
            null, onFailure);
    }

    public Task<CoinListenStartDto?> StartRadioListenAsync(string stationId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/coins/radio/start", new CoinListenStartRequest(stationId),
            AethernetJsonContext.Default.CoinListenStartRequest, AethernetJsonContext.Default.CoinListenStartDto,
            token, null, onFailure);
    }

    public Task<CoinListenEndDto?> EndRadioListenAsync(string sessionId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/coins/radio/{Uri.EscapeDataString(sessionId)}/end",
            AethernetJsonContext.Default.CoinListenEndDto, token, null, onFailure);
    }

    public Task<CoinPurchaseResult?> PurchaseAsync(string skuId, long expectedPrice, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/coins/purchase", new CoinPurchaseRequest(skuId, expectedPrice),
            AethernetJsonContext.Default.CoinPurchaseRequest, AethernetJsonContext.Default.CoinPurchaseResult, token,
            null, onFailure);
    }
}
