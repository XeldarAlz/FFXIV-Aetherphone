using System.Text.Json.Serialization.Metadata;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet;

internal sealed class AethernetTransport
{
    private readonly HttpService http;
    private readonly string? appScope;
    private readonly Action<int> authStatusSink;

    public AethernetTransport(HttpService http, AethernetSession session, string appScope = "")
    {
        this.http = http;
        this.appScope = string.IsNullOrEmpty(appScope) ? null : appScope;
        Session = session;
        authStatusSink = session.ReportAuthStatus;
    }

    public AethernetSession Session { get; }

    public Task<T?> GetAsync<T>(string path, JsonTypeInfo<T> responseInfo, CancellationToken token,
        Action<int>? onStatus = null)
    {
        return http.GetJsonAsync(Url(path), responseInfo, Session.Token, token, Sink(onStatus), appScope);
    }

    public Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest body,
        JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, CancellationToken token,
        Action<int>? onStatus = null)
    {
        return http.PostJsonAsync(Url(path), body, requestInfo, responseInfo, Session.Token, token, Sink(onStatus),
            appScope);
    }

    public Task<TResponse?> SendJsonAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest body,
        JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, CancellationToken token,
        Action<int>? onStatus = null)
    {
        return http.SendJsonAsync(method, Url(path), body, requestInfo, responseInfo, Session.Token, token,
            Sink(onStatus), appScope);
    }

    public Task<TResponse?> RequestAsync<TResponse>(HttpMethod method, string path,
        JsonTypeInfo<TResponse> responseInfo, CancellationToken token, Action<int>? onStatus = null)
    {
        return http.RequestJsonAsync(method, Url(path), responseInfo, Session.Token, token, Sink(onStatus), appScope);
    }

    public Task<bool> SendAsync(HttpMethod method, string path, CancellationToken token, Action<int>? onStatus = null)
    {
        return http.SendAsync(method, Url(path), Session.Token, token, Sink(onStatus), appScope);
    }

    public Task<bool> SendWithBearerAsync(HttpMethod method, string path, string? bearer, CancellationToken token)
    {
        return http.SendAsync(method, Url(path), bearer, token, authStatusSink, appScope);
    }

    public Task<bool> SendJsonForStatusAsync<TRequest>(HttpMethod method, string path, TRequest body,
        JsonTypeInfo<TRequest> requestInfo, CancellationToken token, Action<int>? onStatus = null)
    {
        return http.SendJsonForStatusAsync(method, Url(path), body, requestInfo, Session.Token, token, Sink(onStatus),
            appScope);
    }

    public Task<TResponse?> PostAnonymousAsync<TRequest, TResponse>(string path, TRequest body,
        JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo, CancellationToken token,
        Action<int>? onStatus = null)
    {
        return http.PostJsonAsync(Url(path), body, requestInfo, responseInfo, null, token, onStatus, appScope);
    }

    public Task<T?> GetWithBearerAsync<T>(string path, string bearer, JsonTypeInfo<T> responseInfo,
        CancellationToken token)
    {
        return http.GetJsonAsync(Url(path), responseInfo, bearer, token, static _ => { }, appScope);
    }

    public Task<bool> PutBytesAsync(Uri uri, byte[] content, string contentType, CancellationToken token)
    {
        return http.PutBytesAsync(uri, content, contentType, token);
    }

    private Action<int> Sink(Action<int>? onStatus)
    {
        if (onStatus is null)
        {
            return authStatusSink;
        }

        var sink = authStatusSink;
        return statusCode =>
        {
            sink(statusCode);
            onStatus(statusCode);
        };
    }

    private string Url(string path) => $"{Session.BaseUrl.TrimEnd('/')}{path}";
}
