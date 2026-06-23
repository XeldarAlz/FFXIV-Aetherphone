using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
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

    public async Task<T?> GetJsonAsync<T>(string url, JsonTypeInfo<T> typeInfo, string? bearer, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendForJsonAsync(request, typeInfo, bearer, token).ConfigureAwait(false);
    }

    public async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(string url, TRequest body, JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, string? bearer, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var payload = JsonSerializer.Serialize(body, requestInfo);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        return await SendForJsonAsync(request, responseInfo, bearer, token).ConfigureAwait(false);
    }

    public async Task<bool> SendAsync(HttpMethod method, string url, string? bearer, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, url);
        ApplyBearer(request, bearer);
        try
        {
            using var response = await client.SendAsync(request, token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"HTTP {method} failed for {url}: {exception.Message}");
            return false;
        }
    }

    private async Task<T?> SendForJsonAsync<T>(HttpRequestMessage request, JsonTypeInfo<T> typeInfo, string? bearer, CancellationToken token)
    {
        ApplyBearer(request, bearer);
        try
        {
            using var response = await client.SendAsync(request, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"HTTP {request.Method} failed for {request.RequestUri}: {exception.Message}");
            return default;
        }
    }

    private static void ApplyBearer(HttpRequestMessage request, string? bearer)
    {
        if (!string.IsNullOrEmpty(bearer))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
    }

    private static async Task BackOffAsync(int attempt, HttpResponseMessage? response, CancellationToken token)
    {
        var retryAfter = response?.Headers.RetryAfter?.Delta;
        var delay = retryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
        await Task.Delay(delay, token).ConfigureAwait(false);
    }

    public void Dispose() => client.Dispose();
}
