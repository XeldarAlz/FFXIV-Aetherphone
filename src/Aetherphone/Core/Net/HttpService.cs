using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Aetherphone.Core.Net;

internal sealed class HttpService : IDisposable
{
    private const int MaxAttempts = 3;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient client;

    public HttpService()
    {
        client = new HttpClient
        {
            Timeout = RequestTimeout,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Aetherphone/{AepConstants.Version} (+https://github.com/XeldarAlz/FFXIV-Aetherphone)");
    }

    public async Task<byte[]?> GetBytesAsync(Uri uri, CancellationToken token)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(uri, token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await BackOffAsync(attempt, response, token).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (attempt == MaxAttempts)
                {
                    AepLog.Warning($"HTTP GET failed for {uri}: {exception.Message}");
                    return null;
                }

                await BackOffAsync(attempt, null, token).ConfigureAwait(false);
            }
        }

        return null;
    }

    private static async Task BackOffAsync(int attempt, HttpResponseMessage? response, CancellationToken token)
    {
        var retryAfter = response?.Headers.RetryAfter?.Delta;
        var delay = retryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
        await Task.Delay(delay, token).ConfigureAwait(false);
    }

    public void Dispose() => client.Dispose();
}
