using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Aetherphone.Core.Net;

internal sealed class HttpService : IDisposable
{
    private const int MaxAttempts = 3;
    private const long MaxResponseBytes = 32 * 1024 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(60);
    private readonly HttpClient client;

    public HttpService()
    {
        client = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10), })
        {
            Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = MaxResponseBytes,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"Aetherphone/{AepConstants.Version} (+https://github.com/XeldarAlz/FFXIV-Aetherphone)");
    }

    private static CancellationTokenSource TimeoutScope(CancellationToken token, TimeSpan timeout)
    {
        var scope = CancellationTokenSource.CreateLinkedTokenSource(token);
        scope.CancelAfter(timeout);
        return scope;
    }

    public async Task<byte[]?> GetBytesAsync(Uri uri, CancellationToken token)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var scope = TimeoutScope(token, RequestTimeout);
                using var response = await client.GetAsync(uri, scope.Token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await BackOffAsync(attempt, response, token).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadAsByteArrayAsync(scope.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
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

    public async Task<T?> GetJsonAsync<T>(string url, JsonTypeInfo<T> typeInfo, string? bearer, CancellationToken token,
        Action<int>? onStatus = null, string? appScope = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendForJsonAsync(request, typeInfo, bearer, onStatus, appScope, token).ConfigureAwait(false);
    }

    public Task<TResponse?> PostJsonAsync<TRequest, TResponse>(string url, TRequest body,
        JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, string? bearer,
        CancellationToken token, Action<int>? onStatus = null, string? appScope = null)
    {
        return SendJsonAsync(HttpMethod.Post, url, body, requestInfo, responseInfo, bearer, token, onStatus, appScope);
    }

    public async Task<TResponse?> SendJsonAsync<TRequest, TResponse>(HttpMethod method, string url, TRequest body,
        JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, string? bearer,
        CancellationToken token, Action<int>? onStatus = null, string? appScope = null)
    {
        using var request = new HttpRequestMessage(method, url);
        var payload = JsonSerializer.Serialize(body, requestInfo);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        return await SendForJsonAsync(request, responseInfo, bearer, onStatus, appScope, token).ConfigureAwait(false);
    }

    public async Task<TResponse?> RequestJsonAsync<TResponse>(HttpMethod method, string url,
        JsonTypeInfo<TResponse> responseInfo, string? bearer, CancellationToken token, Action<int>? onStatus = null,
        string? appScope = null)
    {
        using var request = new HttpRequestMessage(method, url);
        return await SendForJsonAsync(request, responseInfo, bearer, onStatus, appScope, token).ConfigureAwait(false);
    }

    public async Task<bool> PutBytesAsync(Uri uri, byte[] content, string contentType, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = new ByteArrayContent(content), };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        try
        {
            using var scope = TimeoutScope(token, UploadTimeout);
            using var response = await client.SendAsync(request, scope.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"HTTP PUT failed for {uri}: {exception.Message}");
            return false;
        }
    }

    public async Task<bool> SendJsonForStatusAsync<TRequest>(HttpMethod method, string url, TRequest body,
        JsonTypeInfo<TRequest> requestInfo, string? bearer, CancellationToken token, Action<int>? onStatus = null,
        string? appScope = null)
    {
        using var request = new HttpRequestMessage(method, url);
        var payload = JsonSerializer.Serialize(body, requestInfo);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        ApplyHeaders(request, bearer, appScope);
        try
        {
            using var scope = TimeoutScope(token, RequestTimeout);
            using var response = await client.SendAsync(request, scope.Token).ConfigureAwait(false);
            onStatus?.Invoke((int)response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"HTTP {method} failed for {url}: {exception.Message}");
            return false;
        }
    }

    public async Task<bool> SendAsync(HttpMethod method, string url, string? bearer, CancellationToken token,
        Action<int>? onStatus = null, string? appScope = null)
    {
        using var request = new HttpRequestMessage(method, url);
        ApplyHeaders(request, bearer, appScope);
        try
        {
            using var scope = TimeoutScope(token, RequestTimeout);
            using var response = await client.SendAsync(request, scope.Token).ConfigureAwait(false);
            onStatus?.Invoke((int)response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"HTTP {method} failed for {url}: {exception.Message}");
            return false;
        }
    }

    private async Task<T?> SendForJsonAsync<T>(HttpRequestMessage request, JsonTypeInfo<T> typeInfo, string? bearer,
        Action<int>? onStatus, string? appScope, CancellationToken token)
    {
        ApplyHeaders(request, bearer, appScope);
        try
        {
            using var scope = TimeoutScope(token, RequestTimeout);
            using var response = await client.SendAsync(request, scope.Token).ConfigureAwait(false);
            onStatus?.Invoke((int)response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(scope.Token).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, scope.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"HTTP {request.Method} failed for {request.RequestUri}: {exception.Message}");
            return default;
        }
    }

    private static void ApplyHeaders(HttpRequestMessage request, string? bearer, string? appScope)
    {
        if (!string.IsNullOrEmpty(bearer))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        if (!string.IsNullOrEmpty(appScope))
        {
            request.Headers.TryAddWithoutValidation("X-Aep-App", appScope);
        }
    }

    private static async Task BackOffAsync(int attempt, HttpResponseMessage? response, CancellationToken token)
    {
        var retryAfter = response?.Headers.RetryAfter?.Delta;
        var delay = retryAfter is { } directed
            ? directed + TimeSpan.FromSeconds(Random.Shared.NextDouble())
            : TimeSpan.FromSeconds(Math.Pow(2, attempt) * (0.5 + Random.Shared.NextDouble()));
        await Task.Delay(delay, token).ConfigureAwait(false);
    }

    public void Dispose() => client.Dispose();
}
