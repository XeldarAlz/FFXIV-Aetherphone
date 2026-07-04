using System.Net.Http;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Aetherphone.Core.Net;

internal sealed class ScopedHttp
{
    private readonly HttpService http;
    private readonly string? appScope;

    public ScopedHttp(HttpService http, string? appScope)
    {
        this.http = http;
        this.appScope = string.IsNullOrEmpty(appScope) ? null : appScope;
    }

    public Task<byte[]?> GetBytesAsync(Uri uri, CancellationToken token)
    {
        return http.GetBytesAsync(uri, token);
    }

    public Task<T?> GetJsonAsync<T>(string url, JsonTypeInfo<T> typeInfo, string? bearer, CancellationToken token, Action<int>? onStatus = null)
    {
        return http.GetJsonAsync(url, typeInfo, bearer, token, onStatus, appScope);
    }

    public Task<TResponse?> PostJsonAsync<TRequest, TResponse>(string url, TRequest body, JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, string? bearer, CancellationToken token, Action<int>? onStatus = null)
    {
        return http.PostJsonAsync(url, body, requestInfo, responseInfo, bearer, token, onStatus, appScope);
    }

    public Task<TResponse?> SendJsonAsync<TRequest, TResponse>(HttpMethod method, string url, TRequest body, JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, string? bearer, CancellationToken token, Action<int>? onStatus = null)
    {
        return http.SendJsonAsync(method, url, body, requestInfo, responseInfo, bearer, token, onStatus, appScope);
    }

    public Task<TResponse?> RequestJsonAsync<TResponse>(HttpMethod method, string url, JsonTypeInfo<TResponse> responseInfo, string? bearer, CancellationToken token, Action<int>? onStatus = null)
    {
        return http.RequestJsonAsync(method, url, responseInfo, bearer, token, onStatus, appScope);
    }

    public Task<bool> SendAsync(HttpMethod method, string url, string? bearer, CancellationToken token, Action<int>? onStatus = null)
    {
        return http.SendAsync(method, url, bearer, token, onStatus, appScope);
    }

    public Task<bool> SendJsonForStatusAsync<TRequest>(HttpMethod method, string url, TRequest body, JsonTypeInfo<TRequest> requestInfo, string? bearer, CancellationToken token, Action<int>? onStatus = null)
    {
        return http.SendJsonForStatusAsync(method, url, body, requestInfo, bearer, token, onStatus, appScope);
    }

    public Task<bool> PutBytesAsync(Uri uri, byte[] content, string contentType, CancellationToken token)
    {
        return http.PutBytesAsync(uri, content, contentType, token);
    }
}
