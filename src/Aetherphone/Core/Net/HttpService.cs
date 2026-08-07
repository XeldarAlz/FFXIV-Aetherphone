using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Aetherphone.Core.Net;

internal sealed class HttpService : IDisposable
{
    private const int MaxAttempts = 3;
    private const int RateLimitedStatus = 429;
    private const long MaxResponseBytes = 32 * 1024 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultRateLimitPause = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxRateLimitPause = TimeSpan.FromSeconds(30);
    private readonly HttpClient client;
    private readonly EtagCache etagCache = new();
    private readonly ConcurrentDictionary<string, long> pausedHostsUntilTicks = new(StringComparer.OrdinalIgnoreCase);

    public HttpService()
    {
        client = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            AutomaticDecompression = DecompressionMethods.All,
        })
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
        if (IsPaused(uri))
        {
            return null;
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var scope = TimeoutScope(token, RequestTimeout);
                using var response = await client.GetAsync(uri, scope.Token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    PauseHost(uri, response);
                    return null;
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

    public async Task<bool> PutBytesAsync(Uri uri, byte[] content, string contentType, CancellationToken token,
        string? bearer = null)
    {
        if (IsPaused(uri))
        {
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = new ByteArrayContent(content), };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        ApplyHeaders(request, bearer, null);
        try
        {
            using var scope = TimeoutScope(token, UploadTimeout);
            using var response = await client.SendAsync(request, scope.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                PauseHost(uri, response);
                return false;
            }

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
        if (IsPaused(request.RequestUri))
        {
            onStatus?.Invoke(RateLimitedStatus);
            return false;
        }

        var payload = JsonSerializer.Serialize(body, requestInfo);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        ApplyHeaders(request, bearer, appScope);
        try
        {
            using var scope = TimeoutScope(token, RequestTimeout);
            using var response = await client.SendAsync(request, scope.Token).ConfigureAwait(false);
            onStatus?.Invoke((int)response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                PauseHost(request.RequestUri, response);
                return false;
            }

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
        if (IsPaused(request.RequestUri))
        {
            onStatus?.Invoke(RateLimitedStatus);
            return false;
        }

        ApplyHeaders(request, bearer, appScope);
        try
        {
            using var scope = TimeoutScope(token, RequestTimeout);
            using var response = await client.SendAsync(request, scope.Token).ConfigureAwait(false);
            onStatus?.Invoke((int)response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                PauseHost(request.RequestUri, response);
                return false;
            }

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
        if (IsPaused(request.RequestUri))
        {
            onStatus?.Invoke(RateLimitedStatus);
            return default;
        }

        ApplyHeaders(request, bearer, appScope);
        var cacheKey = request.Method == HttpMethod.Get
            ? EtagCache.Key(bearer, appScope, request.RequestUri)
            : null;
        var hasCached = false;
        var cachedBody = Array.Empty<byte>();
        if (cacheKey is not null && etagCache.TryGet(cacheKey, out var cachedTag, out cachedBody))
        {
            hasCached = true;
            request.Headers.TryAddWithoutValidation("If-None-Match", cachedTag);
        }

        try
        {
            using var scope = TimeoutScope(token, RequestTimeout);
            using var response = await client.SendAsync(request, scope.Token).ConfigureAwait(false);
            onStatus?.Invoke((int)response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                PauseHost(request.RequestUri, response);
                return default;
            }

            if (response.StatusCode == HttpStatusCode.NotModified && hasCached)
            {
                return JsonSerializer.Deserialize(cachedBody, typeInfo);
            }

            if (!response.IsSuccessStatusCode)
            {
                AepLog.Warning($"HTTP {request.Method} {request.RequestUri} returned {(int)response.StatusCode}");
                return default;
            }

            if (cacheKey is not null)
            {
                var etag = response.Headers.ETag?.ToString();
                if (!string.IsNullOrEmpty(etag))
                {
                    var body = await response.Content.ReadAsByteArrayAsync(scope.Token).ConfigureAwait(false);
                    etagCache.Store(cacheKey, etag, body);
                    return JsonSerializer.Deserialize(body, typeInfo);
                }
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

    public TimeSpan PauseRemaining(string host)
    {
        if (host.Length == 0 || !pausedHostsUntilTicks.TryGetValue(host, out var untilTicks))
        {
            return TimeSpan.Zero;
        }

        var remainingTicks = untilTicks - DateTime.UtcNow.Ticks;
        return remainingTicks > 0 ? TimeSpan.FromTicks(remainingTicks) : TimeSpan.Zero;
    }

    private bool IsPaused(Uri? uri)
    {
        if (uri is null || !pausedHostsUntilTicks.TryGetValue(uri.Host, out var untilTicks))
        {
            return false;
        }

        if (DateTime.UtcNow.Ticks < untilTicks)
        {
            return true;
        }

        pausedHostsUntilTicks.TryRemove(uri.Host, out _);
        return false;
    }

    private void PauseHost(Uri? uri, HttpResponseMessage response)
    {
        if (uri is null)
        {
            return;
        }

        var pause = ResolveRateLimitPause(response);
        var untilTicks = DateTime.UtcNow.Add(pause).Ticks;
        var applied = pausedHostsUntilTicks.AddOrUpdate(uri.Host, untilTicks,
            (_, existing) => existing > untilTicks ? existing : untilTicks);
        if (applied == untilTicks)
        {
            AepLog.Warning($"{uri.Host} rate limited; holding requests for {pause.TotalSeconds:F0}s");
        }
    }

    private static TimeSpan ResolveRateLimitPause(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delta = retryAfter?.Delta;
        if (delta is null && retryAfter?.Date is { } absolute)
        {
            delta = absolute - DateTimeOffset.UtcNow;
        }

        if (delta is not { } pause || pause <= TimeSpan.Zero)
        {
            pause = DefaultRateLimitPause;
        }

        if (pause > MaxRateLimitPause)
        {
            pause = MaxRateLimitPause;
        }

        return pause + TimeSpan.FromMilliseconds(Random.Shared.Next(250, 1500));
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
