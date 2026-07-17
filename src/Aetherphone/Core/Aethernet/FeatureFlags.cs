using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet;

internal sealed class FeatureFlags : IDisposable
{
    private readonly HttpService http;
    private readonly AethernetSession session;
    private readonly CancellationTokenSource cancellation = new();
    private volatile bool musicEnabled = true;
    private volatile bool requested;

    public FeatureFlags(HttpService http, AethernetSession session)
    {
        this.http = http;
        this.session = session;
    }

    public bool MusicEnabled
    {
        get
        {
            EnsureFetched();
            return musicEnabled;
        }
    }

    private void EnsureFetched()
    {
        if (requested)
        {
            return;
        }

        requested = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var flags = await http.GetJsonAsync($"{session.BaseUrl.TrimEnd('/')}/flags",
                    AethernetJsonContext.Default.FeatureFlagsDto, null, token).ConfigureAwait(false);
                if (flags is not null)
                {
                    musicEnabled = flags.Music;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Flags fetch failed: {exception.Message}");
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
