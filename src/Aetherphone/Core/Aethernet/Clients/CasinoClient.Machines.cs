using System.Globalization;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed partial class CasinoClient
{
    internal const string GambleSlotsPath = "/casino/slots/gamble";
    internal const string SlotsMetersPath = "/casino/slots/meters";

    internal static string SlotsMetersQuery(string machineId, long bet)
    {
        return string.Concat(SlotsMetersPath, "?machineId=", Uri.EscapeDataString(machineId), "&bet=",
            bet.ToString(CultureInfo.InvariantCulture));
    }

    public Task<CasinoSlotsGambleDto?> GambleSlotsAsync(string sittingId, string clientRoundId, string parentRoundId,
        int pick, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(GambleSlotsPath, new CasinoSlotsGambleRequest(sittingId, clientRoundId, parentRoundId, pick),
            AethernetJsonContext.Default.CasinoSlotsGambleRequest, AethernetJsonContext.Default.CasinoSlotsGambleDto,
            token, null, onFailure);
    }

    public Task<CasinoSlotsMetersDto?> SlotsMetersAsync(string machineId, long bet, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(SlotsMetersQuery(machineId, bet), AethernetJsonContext.Default.CasinoSlotsMetersDto, token,
            null, onFailure);
    }
}
