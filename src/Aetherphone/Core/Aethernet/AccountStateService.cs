using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Aethernet;

internal sealed class AccountStateService : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(300);

    private readonly AethernetSession session;
    private readonly AccountClient client;
    private readonly IFramework framework;
    private readonly PollCadence cadence;
    private readonly CancellationTokenSource cancellation = new();
    private volatile bool polling;

    public AccountStateService(AethernetSession session, AccountClient client, IFramework framework,
        PhoneVisibility visibility)
    {
        this.session = session;
        this.client = client;
        this.framework = framework;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        framework.Update += OnFrameworkTick;
    }

    public void RefreshNow()
    {
        if (session.IsSignedIn)
        {
            cadence.RequestImmediate();
        }
    }

    private void OnFrameworkTick(IFramework _)
    {
        if (!session.IsSignedIn || session.CurrentUser is null)
        {
            return;
        }

        if (!cadence.Due(DateTime.UtcNow))
        {
            return;
        }

        Poll();
    }

    private void Poll()
    {
        if (polling)
        {
            return;
        }

        polling = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var fresh = await client.MeAsync(token).ConfigureAwait(false);
                if (fresh is not null && Differs(fresh))
                {
                    await framework.RunOnFrameworkThread(() => session.SetUser(fresh)).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[AccountState] poll failed: {exception.Message}");
            }
            finally
            {
                polling = false;
            }
        });
    }

    private bool Differs(UserDto fresh)
    {
        var current = session.CurrentUser;
        if (current is null || !string.Equals(current.Id, fresh.Id, StringComparison.Ordinal))
        {
            return false;
        }

        return current.Badges != fresh.Badges
            || current.GrantedBadges != fresh.GrantedBadges
            || !SameBadgeIds(current.ProfileBadges, fresh.ProfileBadges);
    }

    private static bool SameBadgeIds(string[]? current, string[]? fresh)
    {
        var currentLength = current?.Length ?? 0;
        var freshLength = fresh?.Length ?? 0;
        if (currentLength != freshLength)
        {
            return false;
        }

        for (var index = 0; index < currentLength; index++)
        {
            if (!string.Equals(current![index], fresh![index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkTick;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
