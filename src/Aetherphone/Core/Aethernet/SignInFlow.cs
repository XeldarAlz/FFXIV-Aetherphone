using Aetherphone.Core.Analytics;
using Aetherphone.Core.Localization;
using Aetherphone.Windows;

namespace Aetherphone.Core.Aethernet;

internal sealed class SignInFlow : IDisposable
{
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly CancellationTokenSource cancellation = new();
    private CancellationTokenSource? xivFlowCancellation;
    private volatile bool busy;
    private volatile string status = string.Empty;
    private volatile string code = string.Empty;
    private volatile string? challengeId;
    private volatile string? failureReason;
    private volatile bool xivAuthActive;
    private volatile string xivUserCode = string.Empty;
    private volatile string? xivVerificationUri;

    public SignInFlow(AethernetSession session, AethernetClient client)
    {
        this.session = session;
        this.client = client;
    }

    public bool Busy => busy;
    public string Status => status;
    public string LodestoneCode => code;
    public bool LodestoneActive => challengeId is not null;
    public bool XivAuthActive => xivAuthActive;
    public string XivUserCode => xivUserCode;
    public string? XivVerificationUri => xivVerificationUri;

    public string? ConsumeFailure()
    {
        var reason = failureReason;
        if (reason is not null)
        {
            failureReason = null;
        }

        return reason;
    }

    public void StartLodestone(string name, string world)
    {
        busy = true;
        status = Loc.T(L.Account.RequestingCode);
        Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Began));
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await client.ChallengeAsync(name, world, token).ConfigureAwait(false);
                if (response is null)
                {
                    status = Loc.T(L.Account.CannotReach);
                    Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed));
                    return;
                }

                code = response.Code;
                challengeId = response.ChallengeId;
                status = string.Empty;
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.CodeShown));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Aethernet challenge failed: {exception.Message}");
                status = Loc.T(L.Account.CannotReach);
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed));
            }
            finally
            {
                busy = false;
            }
        });
    }

    public void VerifyLodestone()
    {
        var id = challengeId;
        if (id is null)
        {
            return;
        }

        busy = true;
        status = Loc.T(L.Account.Verifying);
        Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.VerifyPending));
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await client.VerifyAsync(id, token).ConfigureAwait(false);
                if (result.Auth is { } auth)
                {
                    session.SignIn(auth.Token, auth.User);
                    Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Linked));
                    Reset();
                    return;
                }

                var reason = result.FailureReason ?? VerifyFailure.CodeNotFound;
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed, reason));
                status = string.Empty;
                failureReason = reason;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Aethernet verify failed: {exception.Message}");
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed, VerifyFailure.Network));
                status = string.Empty;
                failureReason = VerifyFailure.Network;
            }
            finally
            {
                busy = false;
            }
        });
    }

    public void StartXivAuth(string name, string world)
    {
        busy = true;
        status = Loc.T(L.Account.XivConnecting);
        Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Began));
        var flowCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        Interlocked.Exchange(ref xivFlowCancellation, flowCancellation)?.Dispose();
        var token = flowCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await client.StartXivAuthAsync(name, world, token).ConfigureAwait(false);
                if (response is null || !response.Ok || response.FlowId is null || response.VerificationUri is null)
                {
                    var reason = response?.Reason ?? VerifyFailure.Network;
                    status = string.Empty;
                    failureReason = reason;
                    Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed, reason));
                    busy = false;
                    return;
                }

                xivUserCode = response.UserCode ?? string.Empty;
                xivVerificationUri = response.VerificationUriComplete ?? response.VerificationUri;
                xivAuthActive = true;
                status = string.Empty;
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.CodeShown));
                UrlActions.OpenInBrowser(xivVerificationUri);
                await PollXivLoopAsync(response.FlowId, response.IntervalSeconds, response.ExpiresInSeconds, token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"XIVAuth sign-in failed: {exception.Message}");
                status = string.Empty;
                failureReason = VerifyFailure.Network;
                ResetXivFlow();
            }
        });
    }

    private async Task PollXivLoopAsync(string flowId, int intervalSeconds, int expiresInSeconds,
        CancellationToken token)
    {
        var interval = Math.Max(3, intervalSeconds);
        var deadline = DateTime.UtcNow.AddSeconds(expiresInSeconds > 0 ? expiresInSeconds : 600);
        var consecutiveNetworkFailures = 0;
        while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), token).ConfigureAwait(false);
            var result = await client.PollXivAuthAsync(flowId, token).ConfigureAwait(false);
            if (result.Auth is { } auth)
            {
                session.SignIn(auth.Token, auth.User);
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Linked));
                Reset();
                return;
            }

            var reason = result.FailureReason ?? VerifyFailure.Pending;
            if (reason == VerifyFailure.Pending)
            {
                consecutiveNetworkFailures = 0;
                continue;
            }

            if (reason == VerifyFailure.RateLimited)
            {
                consecutiveNetworkFailures = 0;
                interval += 2;
                continue;
            }

            if (reason == VerifyFailure.Network && ++consecutiveNetworkFailures < 5)
            {
                continue;
            }

            if (reason == VerifyFailure.CharacterNotFound)
            {
                reason = VerifyFailure.XivCharacterNotVerified;
            }

            Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed, reason));
            status = string.Empty;
            failureReason = reason;
            ResetXivFlow();
            return;
        }

        if (!token.IsCancellationRequested)
        {
            Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed, VerifyFailure.Timeout));
            status = string.Empty;
            failureReason = VerifyFailure.Timeout;
            ResetXivFlow();
        }
    }

    public void CancelXivAuth()
    {
        var flowCancellation = xivFlowCancellation;
        try
        {
            flowCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        ResetXivFlow();
    }

    private void ResetXivFlow()
    {
        xivAuthActive = false;
        xivUserCode = string.Empty;
        xivVerificationUri = null;
        busy = false;
        Interlocked.Exchange(ref xivFlowCancellation, null)?.Dispose();
    }

    public void Reset()
    {
        challengeId = null;
        code = string.Empty;
        status = string.Empty;
        failureReason = null;
        busy = false;
        xivAuthActive = false;
        xivUserCode = string.Empty;
        xivVerificationUri = null;
        Interlocked.Exchange(ref xivFlowCancellation, null)?.Dispose();
    }

    public void Dispose()
    {
        cancellation.Cancel();
        Interlocked.Exchange(ref xivFlowCancellation, null)?.Dispose();
        cancellation.Dispose();
    }
}
