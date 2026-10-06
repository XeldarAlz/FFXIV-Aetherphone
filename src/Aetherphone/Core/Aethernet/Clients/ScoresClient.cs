using System.Globalization;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class ScoresClient
{
    internal const string SubmitPath = "/games/scores";
    internal const string MyRanksPath = "/games/scores/me";
    internal const string PrivacyPath = "/me/games-privacy";
    internal const string ScopeGlobal = "global";
    internal const string ScopeFriends = "friends";
    internal const string SpanAll = "all";
    internal const string SpanWeek = "week";
    internal const int DefaultLimit = 50;

    private readonly AethernetTransport net;

    public ScoresClient(AethernetTransport net)
    {
        this.net = net;
    }

    internal static string BoardPath(string gameId, string scope, string span, int limit)
    {
        return string.Concat(SubmitPath, "/", Uri.EscapeDataString(gameId), "?scope=", scope, "&span=", span,
            "&limit=", limit.ToString(CultureInfo.InvariantCulture));
    }

    public Task<GameScoreSubmitDto?> SubmitAsync(string gameId, int value, string? sessionId,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(SubmitPath, new GameScoreSubmitRequest(gameId, value, sessionId),
            AethernetJsonContext.Default.GameScoreSubmitRequest, AethernetJsonContext.Default.GameScoreSubmitDto,
            token, null, onFailure);
    }

    public Task<GameLeaderboardDto?> BoardAsync(string gameId, string scope, string span, int limit,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(BoardPath(gameId, scope, span, limit), AethernetJsonContext.Default.GameLeaderboardDto,
            token, null, onFailure);
    }

    public Task<GameScoreRanksDto?> MyRanksAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(MyRanksPath, AethernetJsonContext.Default.GameScoreRanksDto, token, null, onFailure);
    }

    public Task<UserDto?> SetShowOnLeaderboardsAsync(bool show, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(PrivacyPath, new UpdateGamesPrivacyRequest(show),
            AethernetJsonContext.Default.UpdateGamesPrivacyRequest, AethernetJsonContext.Default.UserDto, token,
            null, onFailure);
    }
}
