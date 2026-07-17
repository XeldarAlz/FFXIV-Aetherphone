using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class AuthClient
{
    private readonly AethernetTransport net;

    public AuthClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<ChallengeResponse?> ChallengeAsync(string name, string world, CancellationToken token)
    {
        return net.PostAnonymousAsync("/auth/challenge", new ChallengeRequest(name, world), AethernetJsonContext.Default.ChallengeRequest, AethernetJsonContext.Default.ChallengeResponse, token);
    }

    public async Task<VerifyResult> VerifyAsync(string challengeId, CancellationToken token)
    {
        var status = 0;
        var response = await net.PostAnonymousAsync("/auth/verify", new VerifyRequest(challengeId), AethernetJsonContext.Default.VerifyRequest, AethernetJsonContext.Default.VerifyResponse, token, statusCode => status = statusCode).ConfigureAwait(false);
        if (response is not null)
        {
            if (response.Ok && response.Token is not null && response.User is not null)
            {
                return VerifyResult.Success(new AuthResponse(response.Token, response.User));
            }

            return VerifyResult.Failure(response.Reason ?? VerifyFailure.CodeNotFound);
        }

        return VerifyResult.Failure(status == 429 ? VerifyFailure.RateLimited : VerifyFailure.Network);
    }

    public Task<XivAuthStartResponse?> StartXivAuthAsync(string name, string world, CancellationToken token)
    {
        return net.PostAnonymousAsync("/auth/xivauth/start", new XivAuthStartRequest(name, world), AethernetJsonContext.Default.XivAuthStartRequest, AethernetJsonContext.Default.XivAuthStartResponse, token);
    }

    public async Task<VerifyResult> PollXivAuthAsync(string flowId, CancellationToken token)
    {
        var status = 0;
        var response = await net.PostAnonymousAsync("/auth/xivauth/poll", new XivAuthPollRequest(flowId), AethernetJsonContext.Default.XivAuthPollRequest, AethernetJsonContext.Default.VerifyResponse, token, statusCode => status = statusCode).ConfigureAwait(false);
        if (response is not null)
        {
            if (response.Ok && response.Token is not null && response.User is not null)
            {
                return VerifyResult.Success(new AuthResponse(response.Token, response.User));
            }

            return VerifyResult.Failure(response.Reason ?? VerifyFailure.Pending);
        }

        return VerifyResult.Failure(status == 429 ? VerifyFailure.RateLimited : VerifyFailure.Network);
    }

    public Task<bool> RevokeTokenAsync(string bearer, CancellationToken token)
    {
        return net.SendWithBearerAsync(HttpMethod.Delete, "/auth/token", bearer, token);
    }
}
