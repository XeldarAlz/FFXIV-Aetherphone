namespace Aetherphone.Core.Hunts;

internal sealed class HuntsLoginFlow
{
    private readonly HuntsClient client;
    private readonly HuntsAuthTokenStore tokens;
    private volatile bool busy;
    private volatile bool succeededJustNow;
    private volatile string? failureReason;

    public HuntsLoginFlow(HuntsClient client, HuntsAuthTokenStore tokens)
    {
        this.client = client;
        this.tokens = tokens;
    }

    public bool Busy => busy;

    public event Action? Succeeded;

    public bool ConsumeSucceeded()
    {
        if (!succeededJustNow)
        {
            return false;
        }

        succeededJustNow = false;
        return true;
    }

    public string? ConsumeFailure()
    {
        var reason = failureReason;
        if (reason is not null)
        {
            failureReason = null;
        }

        return reason;
    }

    public void Login(string username, string password)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await client
                    .LoginAsync(username, password, tokens.SessionId, CancellationToken.None)
                    .ConfigureAwait(false);
                if (response is not { Success: true, Data: { Token: { Length: > 0 } token } data })
                {
                    failureReason = "failed";
                    return;
                }

                tokens.ApplyLogin(token, data.SessionId);
                succeededJustNow = true;
                Succeeded?.Invoke();
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Hunts login failed");
                failureReason = "failed";
            }
            finally
            {
                busy = false;
            }
        });
    }
}
