using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed partial class CasinoClient
{
    internal const string PlinkoDropPath = "/casino/plinko/drop";

    public Task<CasinoPlinkoDropDto?> DropPlinkoAsync(string sittingId, string clientRoundId, int rows, int risk,
        long stake, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(PlinkoDropPath, new CasinoPlinkoDropRequest(sittingId, clientRoundId, rows, risk, stake),
            AethernetJsonContext.Default.CasinoPlinkoDropRequest, AethernetJsonContext.Default.CasinoPlinkoDropDto,
            token, null, onFailure);
    }
}
