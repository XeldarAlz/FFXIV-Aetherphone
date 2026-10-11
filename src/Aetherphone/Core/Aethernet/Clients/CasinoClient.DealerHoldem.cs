using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed partial class CasinoClient
{
    internal const string DealerHoldemStartPath = "/casino/dealerholdem/start";
    internal const string DealerHoldemDecidePath = "/casino/dealerholdem/decide";
    internal const string DealerHoldemOpenPath = "/casino/dealerholdem/open";

    public Task<CasinoDealerHoldemDto?> StartDealerHoldemAsync(string sittingId, string clientRoundId, long ante,
        long trips, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(DealerHoldemStartPath,
            new CasinoDealerHoldemStartRequest(sittingId, clientRoundId, ante, trips),
            AethernetJsonContext.Default.CasinoDealerHoldemStartRequest,
            AethernetJsonContext.Default.CasinoDealerHoldemDto, token, null, onFailure);
    }

    public Task<CasinoDealerHoldemDto?> DecideDealerHoldemAsync(string roundId, int step, string action, int multiple,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(DealerHoldemDecidePath,
            new CasinoDealerHoldemDecideRequest(roundId, step, action, multiple),
            AethernetJsonContext.Default.CasinoDealerHoldemDecideRequest,
            AethernetJsonContext.Default.CasinoDealerHoldemDto, token, null, onFailure);
    }

    public Task<CasinoDealerHoldemOpenDto?> OpenDealerHoldemAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(DealerHoldemOpenPath, AethernetJsonContext.Default.CasinoDealerHoldemOpenDto, token, null,
            onFailure);
    }
}
