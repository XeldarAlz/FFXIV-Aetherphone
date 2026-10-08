using System.Globalization;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed partial class CasinoClient
{
    internal const string MissionsPath = "/casino/missions";
    internal const string ChallengesPath = "/casino/challenges";
    internal const string FamePath = "/casino/fame";
    internal const string FeedPath = "/casino/feed";

    internal static string MissionClaimPath(string missionId)
    {
        return string.Concat(MissionsPath, "/", Uri.EscapeDataString(missionId), "/claim");
    }

    internal static string FameBoardPath(string board, string span, int limit)
    {
        return string.Concat(FamePath, "?board=", Uri.EscapeDataString(board), "&span=", Uri.EscapeDataString(span),
            "&limit=", limit.ToString(CultureInfo.InvariantCulture));
    }

    internal static string FeedTabPath(string tab)
    {
        return string.Concat(FeedPath, "?tab=", Uri.EscapeDataString(tab));
    }

    public Task<CasinoMissionsDto?> MissionsAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(MissionsPath, AethernetJsonContext.Default.CasinoMissionsDto, token, null, onFailure);
    }

    public Task<CasinoMissionClaimDto?> ClaimMissionAsync(string missionId, string clientActionId,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(MissionClaimPath(missionId), new CasinoMissionClaimRequest(clientActionId),
            AethernetJsonContext.Default.CasinoMissionClaimRequest, AethernetJsonContext.Default.CasinoMissionClaimDto,
            token, null, onFailure);
    }

    public Task<CasinoChallengesDto?> ChallengesAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(ChallengesPath, AethernetJsonContext.Default.CasinoChallengesDto, token, null, onFailure);
    }

    public Task<CasinoFameBoardDto?> FameAsync(string board, string span, int limit, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(FameBoardPath(board, span, limit), AethernetJsonContext.Default.CasinoFameBoardDto, token,
            null, onFailure);
    }

    public Task<CasinoFeedDto?> FeedAsync(string tab, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(FeedTabPath(tab), AethernetJsonContext.Default.CasinoFeedDto, token, null, onFailure);
    }
}
